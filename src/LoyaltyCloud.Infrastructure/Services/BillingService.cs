using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class BillingService : IBillingService
{
    private readonly AppDbContext _db; private readonly IDateTimeProvider _clock; private readonly IPaymentGateway _gateway;
    private readonly IMutableTenantContext _tenantContext; private readonly ILogger<BillingService> _logger; private readonly IDataProtector _returnProtector;
    public BillingService(AppDbContext db, IDateTimeProvider clock, IPaymentGateway gateway, IMutableTenantContext tenantContext, ILogger<BillingService> logger, IDataProtectionProvider dataProtection)
    { _db=db; _clock=clock; _gateway=gateway; _tenantContext=tenantContext; _logger=logger; _returnProtector=dataProtection.CreateProtector("LoyaltyCloud.Billing.Return.v1"); }

    public async Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct=default) => Map(await Settings(ct));
    public async Task SaveSettingsAsync(BillingSettingsDto x, CancellationToken ct=default)
    { var s=await Settings(ct); s.Update(x.Currency,x.TaxRate,x.PricesIncludeTax,x.GracePeriodDays,x.CardPaymentsEnabled,x.BankTransferEnabled,x.RequireTransferReceipt,x.BankName,x.BeneficiaryName,x.Clabe,x.BankTransferInstructions,x.SupportEmail,_clock.UtcNow); await _db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(bool activeOnly=false,CancellationToken ct=default)
    { var q=_db.SubscriptionPlans.AsNoTracking(); if(activeOnly) q=q.Where(x=>x.IsActive); return await q.OrderBy(x=>x.Name).Select(x=>new SubscriptionPlanDto(x.Id,x.Code,x.Name,x.Currency,x.MonthlyPrice,x.ThreeMonthPrice,x.SixMonthPrice,x.TwelveMonthPrice,x.IsActive)).ToListAsync(ct); }
    public async Task<int> SavePlanAsync(SubscriptionPlanDto x,CancellationToken ct=default)
    {
        var normalizedCode=x.Code.Trim().ToLowerInvariant();
        var p=await _db.SubscriptionPlans.SingleOrDefaultAsync(y=>y.Id==x.Id || y.Code==normalizedCode,ct);
        if(p is null){p=new SubscriptionPlan(x.Id==Guid.Empty?Guid.NewGuid():x.Id,normalizedCode,x.Name,x.Currency,_clock.UtcNow);_db.Add(p);}
        p.Update(x.Name,x.Currency,x.OneMonthPrice,x.ThreeMonthPrice,x.SixMonthPrice,x.TwelveMonthPrice,x.IsActive,_clock.UtcNow);
        var affected=await _db.SaveChangesAsync(ct);
        await _db.Entry(p).ReloadAsync(ct);
        if(p.Name!=x.Name.Trim()||p.Currency!=x.Currency.Trim().ToUpperInvariant()||p.MonthlyPrice!=x.OneMonthPrice||p.ThreeMonthPrice!=x.ThreeMonthPrice||p.SixMonthPrice!=x.SixMonthPrice||p.TwelveMonthPrice!=x.TwelveMonthPrice||p.IsActive!=x.IsActive)
            throw new DbUpdateException("El plan no coincide con los valores persistidos.");
        return affected;
    }
    public async Task<BillingQuoteDto> QuoteAsync(Guid tenantId,string planCode,int months,CancellationToken ct=default)
    { await RequireTenant(tenantId,ct); var p=await _db.SubscriptionPlans.SingleOrDefaultAsync(x=>x.Code==planCode&&x.IsActive,ct)??throw new InvalidOperationException("Plan no disponible."); var s=await Settings(ct); if(p.Currency!=s.Currency)throw new InvalidOperationException("Moneda inconsistente."); var price=p.PriceFor(months); if(price<=0)throw new InvalidOperationException("Precio no configurado."); decimal subtotal,tax; if(s.PricesIncludeTax){var divisor=1+s.TaxRate/100m;subtotal=Math.Round(price/divisor,2);tax=price-subtotal;}else{subtotal=price;tax=Math.Round(price*s.TaxRate/100m,2);}return new(subtotal,tax,subtotal+tax,s.Currency); }
    public async Task<TenantBillingDto> GetTenantBillingAsync(Guid tenantId,CancellationToken ct=default)
    { var t=await RequireTenant(tenantId,ct); _tenantContext.SetTenant(t.Id,t.Slug); await ReconcileExpiredCardOrdersAsync(tenantId,ct); var settings=await GetSettingsAsync(ct); var plans=await GetPlansAsync(true,ct); var orders=await _db.BillingOrders.IgnoreQueryFilters().Where(x=>x.TenantId==tenantId).OrderByDescending(x=>x.CreatedAt).Take(20).AsNoTracking().ToListAsync(ct); return new(t.Id,t.Slug,t.DisplayName,t.Subscription!.PlanCode,t.Subscription.Status.ToString(),t.Subscription.PaidThroughUtc,t.Subscription.GracePeriodEndsAt,settings,settings.CardPaymentsEnabled && _gateway.IsAvailable,plans,orders.Select(x=>Map(x)).ToList()); }
    public async Task<BillingOrderDto> CreateOrderAsync(Guid tenantId,string planCode,int months,BillingPaymentMethod method,string baseUrl,CancellationToken ct=default)
    { var t=await RequireTenant(tenantId,ct); var s=await Settings(ct); if(method==BillingPaymentMethod.Card&&(!s.CardPaymentsEnabled||!_gateway.IsAvailable))throw new InvalidOperationException("Pago con tarjeta no disponible."); if(method==BillingPaymentMethod.BankTransfer&&!s.BankTransferEnabled)throw new InvalidOperationException("Transferencia no disponible."); var q=await QuoteAsync(tenantId,planCode,months,ct); var from=t.Subscription!.PaidThroughUtc>_clock.UtcNow?t.Subscription.PaidThroughUtc.Value:_clock.UtcNow; var o=new BillingOrder(Guid.NewGuid(),tenantId,planCode,months,q.Subtotal,q.Tax,q.Total,q.Currency,method,_clock.UtcNow,from,TenantSubscription.CalculateManualPaymentPaidThrough(from,months,_clock.UtcNow)); _tenantContext.SetTenant(t.Id,t.Slug);_db.Add(o);await _db.SaveChangesAsync(ct);string? url=null;if(method==BillingPaymentMethod.Card){var root=baseUrl.TrimEnd('/');var token=Uri.EscapeDataString(_returnProtector.Protect($"{tenantId:N}:{o.Id:N}"));var result=await _gateway.CreateCheckoutAsync(new(o.Id,tenantId,$"LoyaltyCloud {months} mes(es)",(long)Math.Round(o.Total*100m),o.Currency,$"{root}/{t.Slug}/billing/payment/success?token={token}",$"{root}/{t.Slug}/billing/payment/cancelled?token={token}"),ct);o.AttachCheckout(result.SessionId);await _db.SaveChangesAsync(ct);url=result.Url;}return Map(o,url); }
    public async Task<BillingOrderDto?> GetOrderAsync(Guid tenantId,Guid orderId,CancellationToken ct=default){var o=await _db.BillingOrders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==orderId&&x.TenantId==tenantId,ct);return o is null?null:Map(o);}
    public async Task<BillingPaymentResultDto?> GetPaymentResultAsync(string tenantSlug,string token,CancellationToken ct=default)
    {
        Guid tenantId,orderId;
        try
        {
            var parts=_returnProtector.Unprotect(token).Split(':');
            if(parts.Length!=2||!Guid.TryParseExact(parts[0],"N",out tenantId)||!Guid.TryParseExact(parts[1],"N",out orderId))return null;
        }
        catch(System.Security.Cryptography.CryptographicException){return null;}
        var tenant=await _db.Tenants.IgnoreQueryFilters().Include(x=>x.Subscription).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==tenantId&&x.Slug==tenantSlug,ct);
        if(tenant?.Subscription is null)return null;
        var order=await _db.BillingOrders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==orderId&&x.TenantId==tenantId,ct);
        return order is null?null:new BillingPaymentResultDto(order.Status,tenant.Subscription.PaidThroughUtc,tenant.Subscription.IsOperational(_clock.UtcNow));
    }
    public async Task<IReadOnlyList<BillingOrderDto>> GetAwaitingTransfersAsync(CancellationToken ct=default)=>await _db.BillingOrders.IgnoreQueryFilters().Where(x=>x.Status==BillingOrderStatus.AwaitingTransfer).OrderBy(x=>x.CreatedAt).AsNoTracking().Select(x=>new BillingOrderDto(x.Id,x.TenantId,x.PlanCode,x.Months,x.Subtotal,x.Tax,x.Total,x.Currency,x.Status,x.PaymentMethod,x.CreatedAt,x.SubscriptionThroughUtc,null,x.BankReference,x.ReceiptUrl)).ToListAsync(ct);
    public Task ApproveTransferAsync(Guid orderId,string by,CancellationToken ct=default)=>Confirm(orderId,$"manual:{orderId}",by,ct);
    public async Task RejectTransferAsync(Guid orderId,string by,CancellationToken ct=default){var o=await Order(orderId,ct);var t=await RequireTenant(o.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);o.Reject(by,_clock.UtcNow);await _db.SaveChangesAsync(ct);}
    public async Task ProcessStripeWebhookAsync(string payload,string signature,CancellationToken ct=default)
    {var c=_gateway.ParseWebhook(payload,signature);if(await _db.PaymentWebhookEvents.AnyAsync(x=>x.Provider==PaymentProvider.Stripe&&x.ProviderEventId==c.EventId,ct))return;var e=new PaymentWebhookEvent(Guid.NewGuid(),PaymentProvider.Stripe,c.EventId,c.EventType,_clock.UtcNow);_db.Add(e);if(c.Paid&&c.OrderId!=Guid.Empty){var o=await Order(c.OrderId,ct);if(o.TenantId!=c.TenantId||o.Currency!=c.Currency.ToUpperInvariant()||(long)Math.Round(o.Total*100m)!=c.AmountTotalMinor)throw new InvalidOperationException("El pago no coincide con la orden.");await Confirm(o.Id,c.PaymentIntentId,null,ct,false);}else if(c.EventType=="checkout.session.expired"&&c.OrderId!=Guid.Empty){var o=await Order(c.OrderId,ct);if(c.TenantId!=Guid.Empty&&o.TenantId!=c.TenantId)throw new InvalidOperationException("La sesión expirada no coincide con la orden.");var t=await RequireTenant(o.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);o.MarkExpired();}e.Processed(_clock.UtcNow);await _db.SaveChangesAsync(ct);}
    private async Task ReconcileExpiredCardOrdersAsync(Guid tenantId,CancellationToken ct)
    {
        if(!_gateway.IsAvailable)return;
        var candidates=await _db.BillingOrders.IgnoreQueryFilters().Where(x=>x.TenantId==tenantId&&x.PaymentMethod==BillingPaymentMethod.Card&&x.Status==BillingOrderStatus.Pending&&x.ExpiresAt<=_clock.UtcNow&&x.ExternalCheckoutId!=null).ToListAsync(ct);
        var changed=false;
        foreach(var order in candidates)
        {
            try
            {
                var session=await _gateway.GetCheckoutSessionAsync(order.ExternalCheckoutId!,ct);
                if(session.Status==CheckoutSessionStatus.Expired)changed|=order.MarkExpired();
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex,"Could not reconcile Stripe Checkout Session. OrderId={OrderId}.",order.Id);
            }
        }
        if(changed)await _db.SaveChangesAsync(ct);
    }
    private async Task Confirm(Guid orderId,string externalId,string? by,CancellationToken ct,bool save=true){var o=await Order(orderId,ct);if(o.Status==BillingOrderStatus.Paid)return;var t=await RequireTenant(o.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);if(!o.MarkPaid(by,_clock.UtcNow))return;t.Subscription!.RecordManualPayment(o.Months,_clock.UtcNow);_db.Add(new PaymentTransaction(Guid.NewGuid(),o,externalId,_clock.UtcNow));if(save)await _db.SaveChangesAsync(ct);}
    private async Task<BillingSettings> Settings(CancellationToken ct){var s=await _db.BillingSettings.SingleOrDefaultAsync(x=>x.Code==BillingSettings.SingletonCode,ct);if(s is null){s=new BillingSettings(Guid.NewGuid(),_clock.UtcNow);_db.Add(s);await _db.SaveChangesAsync(ct);}return s;}
    private async Task<Tenant> RequireTenant(Guid id,CancellationToken ct)=>await _db.Tenants.Include(x=>x.Subscription).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new InvalidOperationException("Tenant no encontrado.");
    private async Task<BillingOrder> Order(Guid id,CancellationToken ct)=>await _db.BillingOrders.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new InvalidOperationException("Orden no encontrada.");
    private static BillingSettingsDto Map(BillingSettings s)=>new(s.Currency,s.TaxRate,s.PricesIncludeTax,s.GracePeriodDays,s.CardPaymentsEnabled,s.BankTransferEnabled,s.RequireTransferReceipt,s.AutomaticRenewalEnabled,s.CfdiEnabled,s.BankName,s.BeneficiaryName,s.Clabe,s.BankTransferInstructions,s.SupportEmail);
    private static BillingOrderDto Map(BillingOrder o,string? url=null)=>new(o.Id,o.TenantId,o.PlanCode,o.Months,o.Subtotal,o.Tax,o.Total,o.Currency,o.Status,o.PaymentMethod,o.CreatedAt,o.SubscriptionThroughUtc,url,o.BankReference,o.ReceiptUrl);
}
