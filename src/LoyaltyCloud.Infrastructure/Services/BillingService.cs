using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using System.Net.Mail;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class BillingService : IBillingService
{
    private readonly AppDbContext _db; private readonly IDateTimeProvider _clock; private readonly IPaymentGateway _gateway;
    private readonly IMutableTenantContext _tenantContext; private readonly ILogger<BillingService> _logger; private readonly IDataProtector _returnProtector; private readonly IBillingNotificationService _notifications; private readonly IBillingEmailConfigurationProvider _emailConfiguration; private readonly IHostEnvironment? _environment;
    public BillingService(AppDbContext db, IDateTimeProvider clock, IPaymentGateway gateway, IMutableTenantContext tenantContext, ILogger<BillingService> logger, IDataProtectionProvider dataProtection, IBillingNotificationService notifications, IBillingEmailConfigurationProvider emailConfiguration, IHostEnvironment? environment = null)
    { _db=db; _clock=clock; _gateway=gateway; _tenantContext=tenantContext; _logger=logger; _returnProtector=dataProtection.CreateProtector("LoyaltyCloud.Billing.Return.v1"); _notifications=notifications; _emailConfiguration=emailConfiguration; _environment=environment; }

    public async Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct=default) => Map(await Settings(ct));
    public async Task SaveSettingsAsync(BillingSettingsDto x, CancellationToken ct=default)
    { var s=await Settings(ct); s.Update(x.Currency,x.TaxRate,x.PricesIncludeTax,x.GracePeriodDays,x.CardPaymentsEnabled,x.BankTransferEnabled,x.RequireTransferReceipt,x.BankName,x.BeneficiaryName,x.Clabe,x.BankTransferInstructions,x.SupportEmail,_clock.UtcNow); await _db.SaveChangesAsync(ct); }
    public Task<BillingEmailSettingsDto> GetEmailSettingsAsync(CancellationToken ct=default)=>_emailConfiguration.GetAsync(ct);
    public async Task SaveEmailSettingsAsync(BillingEmailSettingsDto x,CancellationToken ct=default)
    {
        var runtime=await _emailConfiguration.GetAsync(ct);
        var provider=x.Provider?.Trim();var from=x.FromAddress?.Trim();var name=x.FromName?.Trim();var baseUrl=x.ApplicationBaseUrl?.Trim();
        var emailValid=!string.IsNullOrWhiteSpace(from)&&MailAddress.TryCreate(from,out _);
        var urlValid=Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri)&&(_environment?.IsDevelopment()==true||uri.Scheme==Uri.UriSchemeHttps);
        if(x.Enabled&&(!runtime.CredentialsConfigured||string.IsNullOrWhiteSpace(provider)||string.IsNullOrWhiteSpace(name)||!emailValid||!urlValid))throw new InvalidOperationException("No se pueden habilitar las notificaciones hasta completar la configuración requerida.");
        if(!string.IsNullOrWhiteSpace(from)&&!emailValid)throw new InvalidOperationException("El correo remitente no tiene un formato válido.");
        if(!string.IsNullOrWhiteSpace(baseUrl)&&!Uri.TryCreate(baseUrl,UriKind.Absolute,out _))throw new InvalidOperationException("La URL pública no tiene un formato válido.");
        var settings=await Settings(ct);settings.UpdateEmailNotifications(x.Enabled,provider??"Cloudflare",from,name??"LoyaltyCloud",baseUrl,_clock.UtcNow);await _db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(bool activeOnly=false,CancellationToken ct=default)
    { var q=_db.SubscriptionPlans.AsNoTracking(); if(activeOnly) q=q.Where(x=>x.IsActive); return await q.OrderBy(x=>x.Name).Select(x=>new SubscriptionPlanDto(x.Id,x.Code,x.Name,x.Currency,x.MonthlyPrice,x.ThreeMonthPrice,x.SixMonthPrice,x.TwelveMonthPrice,x.IsActive,x.StripeOneMonthPriceId,x.StripeThreeMonthPriceId,x.StripeSixMonthPriceId,x.StripeTwelveMonthPriceId)).ToListAsync(ct); }
    public async Task<int> SavePlanAsync(SubscriptionPlanDto x,CancellationToken ct=default)
    {
        var normalizedCode=x.Code.Trim().ToLowerInvariant();
        var p=await _db.SubscriptionPlans.SingleOrDefaultAsync(y=>y.Id==x.Id || y.Code==normalizedCode,ct);
        if(p is null){p=new SubscriptionPlan(x.Id==Guid.Empty?Guid.NewGuid():x.Id,normalizedCode,x.Name,x.Currency,_clock.UtcNow);_db.Add(p);}
        p.Update(x.Name,x.Currency,x.OneMonthPrice,x.ThreeMonthPrice,x.SixMonthPrice,x.TwelveMonthPrice,x.IsActive,_clock.UtcNow); p.SetStripePriceIds(x.StripeOneMonthPriceId,x.StripeThreeMonthPriceId,x.StripeSixMonthPriceId,x.StripeTwelveMonthPriceId);
        var affected=await _db.SaveChangesAsync(ct);
        await _db.Entry(p).ReloadAsync(ct);
        if(p.Name!=x.Name.Trim()||p.Currency!=x.Currency.Trim().ToUpperInvariant()||p.MonthlyPrice!=x.OneMonthPrice||p.ThreeMonthPrice!=x.ThreeMonthPrice||p.SixMonthPrice!=x.SixMonthPrice||p.TwelveMonthPrice!=x.TwelveMonthPrice||p.IsActive!=x.IsActive)
            throw new DbUpdateException("El plan no coincide con los valores persistidos.");
        return affected;
    }
    public async Task<BillingQuoteDto> QuoteAsync(Guid tenantId,string planCode,int months,CancellationToken ct=default)
    { await RequireTenant(tenantId,ct); var p=await _db.SubscriptionPlans.SingleOrDefaultAsync(x=>x.Code==planCode&&x.IsActive,ct)??throw new InvalidOperationException("Plan no disponible."); var s=await Settings(ct); if(p.Currency!=s.Currency)throw new InvalidOperationException("Moneda inconsistente."); var price=p.PriceFor(months); if(price<=0)throw new InvalidOperationException("Precio no configurado."); decimal subtotal,tax; if(s.PricesIncludeTax){var divisor=1+s.TaxRate/100m;subtotal=Math.Round(price/divisor,2);tax=price-subtotal;}else{subtotal=price;tax=Math.Round(price*s.TaxRate/100m,2);}return new(subtotal,tax,subtotal+tax,s.Currency); }
    public async Task<TenantBillingDto> GetTenantBillingAsync(Guid tenantId,CancellationToken ct=default)
    { var t=await RequireTenant(tenantId,ct); _tenantContext.SetTenant(t.Id,t.Slug); await ReconcileExpiredCardOrdersAsync(tenantId,ct); var settings=await GetSettingsAsync(ct); var plans=await GetPlansAsync(true,ct); var orders=await _db.BillingOrders.IgnoreQueryFilters().Where(x=>x.TenantId==tenantId).OrderByDescending(x=>x.CreatedAt).Take(20).AsNoTracking().ToListAsync(ct); var profile=await Profile(tenantId,ct); return new(t.Id,t.Slug,t.DisplayName,t.Subscription!.PlanCode,t.Subscription.Status.ToString(),t.Subscription.PaidThroughUtc,t.Subscription.GracePeriodEndsAt,settings,settings.CardPaymentsEnabled && _gateway.IsAvailable,plans,orders.Select(x=>Map(x)).ToList(),profile.AutoRenewEnabled,profile.BillingContactEmail,profile.StripeSubscriptionStatus,profile.StripeCurrentPeriodEndUtc,profile.CancelAtPeriodEnd,profile.RecurringAmount,profile.RecurringCurrency,profile.CardBrand,profile.CardLast4); }
    public async Task<BillingOrderDto> CreateOrderAsync(Guid tenantId,string planCode,int months,BillingPaymentMethod method,string baseUrl,CancellationToken ct=default)
    { var t=await RequireTenant(tenantId,ct); var s=await Settings(ct); if(method==BillingPaymentMethod.Card&&(!s.CardPaymentsEnabled||!_gateway.IsAvailable))throw new InvalidOperationException("Pago con tarjeta no disponible."); if(method==BillingPaymentMethod.BankTransfer&&!s.BankTransferEnabled)throw new InvalidOperationException("Transferencia no disponible."); var q=await QuoteAsync(tenantId,planCode,months,ct); var profile=await Profile(tenantId,ct); var plan=await _db.SubscriptionPlans.SingleAsync(x=>x.Code==planCode,ct); if(method==BillingPaymentMethod.Card&&profile.AutoRenewEnabled&&string.IsNullOrWhiteSpace(plan.StripePriceFor(months)))throw new InvalidOperationException($"Stripe Price ID no configurado para {months} mes(es). Contacta al administrador."); var from=t.Subscription!.PaidThroughUtc>_clock.UtcNow?t.Subscription.PaidThroughUtc.Value:_clock.UtcNow; var o=new BillingOrder(Guid.NewGuid(),tenantId,planCode,months,q.Subtotal,q.Tax,q.Total,q.Currency,method,_clock.UtcNow,from,TenantSubscription.CalculateManualPaymentPaidThrough(from,months,_clock.UtcNow)); _tenantContext.SetTenant(t.Id,t.Slug);_db.Add(o);await _db.SaveChangesAsync(ct);string? url=null;if(method==BillingPaymentMethod.Card){var root=baseUrl.TrimEnd('/');var token=Uri.EscapeDataString(_returnProtector.Protect($"{tenantId:N}:{o.Id:N}"));var result=await _gateway.CreateCheckoutAsync(new(o.Id,tenantId,$"LoyaltyCloud {months} mes(es)",(long)Math.Round(o.Total*100m),o.Currency,$"{root}/{t.Slug}/billing/payment/success?token={token}",$"{root}/{t.Slug}/billing/payment/cancelled?token={token}",profile.AutoRenewEnabled,profile.StripeCustomerId,profile.AutoRenewEnabled?plan.StripePriceFor(months):null,months),ct);o.AttachCheckout(result.SessionId);await _db.SaveChangesAsync(ct);url=result.Url;}return Map(o,url); }
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
    public async Task<IReadOnlyList<BillingOrderDto>> GetAwaitingTransfersAsync(CancellationToken ct=default)=>await _db.BillingOrders.IgnoreQueryFilters().Where(x=>x.Status==BillingOrderStatus.AwaitingTransfer).OrderBy(x=>x.CreatedAt).AsNoTracking().Select(x=>new BillingOrderDto(x.Id,x.TenantId,x.PlanCode,x.Months,x.Subtotal,x.Tax,x.Total,x.Currency,x.Status,x.PaymentMethod,x.CreatedAt,x.SubscriptionThroughUtc,null,x.BankReference,x.ReceiptUrl,x.PaymentKind)).ToListAsync(ct);
    public Task ApproveTransferAsync(Guid orderId,string by,CancellationToken ct=default)=>Confirm(orderId,$"manual:{orderId}",by,ct);
    public async Task RejectTransferAsync(Guid orderId,string by,CancellationToken ct=default){var o=await Order(orderId,ct);var t=await RequireTenant(o.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);o.Reject(by,_clock.UtcNow);await _db.SaveChangesAsync(ct);}
    public async Task UpdateAutoRenewAsync(Guid tenantId,bool enabled,string? billingContactEmail=null,CancellationToken ct=default)
    { var t=await RequireTenant(tenantId,ct); _tenantContext.SetTenant(t.Id,t.Slug); var p=await Profile(tenantId,ct); if(p.StripeSubscriptionId is not null) await _gateway.SetSubscriptionCancellationAsync(p.StripeSubscriptionId,!enabled,ct); p.SetAutoRenew(enabled); if(billingContactEmail is not null)p.SetContactEmail(billingContactEmail); await _db.SaveChangesAsync(ct); await SafeNotify(new BillingNotification(t.Id,p.BillingContactEmail,enabled?BillingNotificationType.AutoRenewEnabled:BillingNotificationType.AutoRenewDisabled,$"auto-renew:{enabled}:{_clock.UtcNow:O}",p.RecurringAmount,p.RecurringCurrency,p.StripeCurrentPeriodEndUtc,null,$"/{t.Slug}/billing",t.DisplayName,p.RecurringMonths,t.Subscription!.PaidThroughUtc,p.StripeCurrentPeriodEndUtc,p.CardBrand,p.CardLast4),ct); }
    public async Task ProcessStripeWebhookAsync(string payload,string signature,CancellationToken ct=default)
    {
        var c=_gateway.ParseWebhook(payload,signature);
        if(await _db.PaymentWebhookEvents.AnyAsync(x=>x.Provider==PaymentProvider.Stripe&&x.ProviderEventId==c.EventId,ct))return;
        var e=new PaymentWebhookEvent(Guid.NewGuid(),PaymentProvider.Stripe,c.EventId,c.EventType,_clock.UtcNow);_db.Add(e);
        if(c.EventType=="checkout.session.completed"&&c.OrderId!=Guid.Empty)
        {
            var o=await Order(c.OrderId,ct); var t=await RequireTenant(o.TenantId,ct); _tenantContext.SetTenant(t.Id,t.Slug); var p=await Profile(t.Id,ct);
            if(c.CustomerId is not null)p.AttachCustomer(c.CustomerId);
            if(c.SubscriptionId is not null)p.AttachSubscription(c.SubscriptionId,"active",c.PeriodEndUtc,false,o.Months,o.Total,o.Currency,c.CardBrand,c.CardLast4);
            else if(c.Paid){ValidateOrder(o,c);await Confirm(o.Id,c.PaymentIntentId,null,ct,false);}
        }
        else if(c.EventType=="checkout.session.expired"&&c.OrderId!=Guid.Empty)
        {var o=await Order(c.OrderId,ct);var t=await RequireTenant(o.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);o.MarkExpired();}
        else if(c.EventType.StartsWith("invoice.",StringComparison.Ordinal))
        {
            var p=await FindProfile(c,ct); if(p is not null){var t=await RequireTenant(p.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);
                if(c.EventType=="invoice.upcoming") await Notify(t,p,BillingNotificationType.UpcomingCharge,c,c.PeriodEndUtc,null,ct);
                else if(c.EventType=="invoice.payment_failed")
                {var settings=await Settings(ct);t.Subscription!.ChangeGracePeriod(_clock.UtcNow.AddDays(settings.GracePeriodDays));await Notify(t,p,BillingNotificationType.PaymentFailed,c,c.PeriodEndUtc,t.Subscription.GracePeriodEndsAt,ct);}
                else if(c.EventType=="invoice.paid")
                {
                    if(c.OrderId!=Guid.Empty){var o=await Order(c.OrderId,ct);ValidateOrder(o,c);await Confirm(o.Id,c.InvoiceId??c.PaymentIntentId,null,ct,false);p.AttachSubscription(c.SubscriptionId??p.StripeSubscriptionId!,"active",c.PeriodEndUtc,false,o.Months,o.Total,o.Currency);}
                    else if(c.InvoiceId is not null&&!await _db.PaymentTransactions.IgnoreQueryFilters().AnyAsync(x=>x.Provider==PaymentProvider.Stripe&&x.ExternalTransactionId==c.InvoiceId,ct))
                    {var months=p.RecurringMonths??1;var amount=p.RecurringAmount??c.AmountTotalMinor/100m;var from=t.Subscription!.PaidThroughUtc>_clock.UtcNow?t.Subscription.PaidThroughUtc.Value:_clock.UtcNow;var o=new BillingOrder(Guid.NewGuid(),t.Id,t.Subscription.PlanCode,months,amount,0,amount,c.Currency, BillingPaymentMethod.Card,_clock.UtcNow,from,TenantSubscription.CalculateManualPaymentPaidThrough(from,months,_clock.UtcNow),BillingPaymentKind.AutomaticRenewal);o.MarkPaid(null,_clock.UtcNow);_db.Add(o);t.Subscription.RecordManualPayment(months,_clock.UtcNow);_db.Add(new PaymentTransaction(Guid.NewGuid(),o,c.InvoiceId,_clock.UtcNow,c.CardBrand,c.CardLast4));p.SyncSubscription("active",c.PeriodEndUtc,false);}
                    await Notify(t,p,BillingNotificationType.PaymentSucceeded,c,c.PeriodEndUtc,null,ct);
                }
            }
        }
        else if(c.EventType is "customer.subscription.updated" or "customer.subscription.deleted")
        {
            var p=await FindProfile(c,ct);if(p is not null){var t=await RequireTenant(p.TenantId,ct);_tenantContext.SetTenant(t.Id,t.Slug);if(c.EventType.EndsWith("deleted",StringComparison.Ordinal))p.SubscriptionDeleted(c.SubscriptionStatus,c.PeriodEndUtc);else p.SyncSubscription(c.SubscriptionStatus,c.PeriodEndUtc,c.CancelAtPeriodEnd);}
        }
        e.Processed(_clock.UtcNow);await _db.SaveChangesAsync(ct);
    }
    private static void ValidateOrder(BillingOrder o,StripePaymentConfirmation c)
    {if((c.TenantId!=Guid.Empty&&o.TenantId!=c.TenantId)||o.Currency!=c.Currency.ToUpperInvariant()||(c.AmountTotalMinor>0&&(long)Math.Round(o.Total*100m)!=c.AmountTotalMinor))throw new InvalidOperationException("El pago no coincide con la orden.");}
    private async Task<TenantBillingProfile?> FindProfile(StripePaymentConfirmation c,CancellationToken ct)
    {if(c.SubscriptionId is not null)return await _db.TenantBillingProfiles.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.StripeSubscriptionId==c.SubscriptionId,ct);if(c.TenantId!=Guid.Empty)return await Profile(c.TenantId,ct);return null;}
    private Task Notify(Tenant t,TenantBillingProfile p,BillingNotificationType type,StripePaymentConfirmation c,DateTime? effective,DateTime? grace,CancellationToken ct)
    =>SafeNotify(new BillingNotification(t.Id,p.BillingContactEmail,type,c.InvoiceId??c.EventId,c.AmountTotalMinor/100m,c.Currency,effective,grace,$"/{t.Slug}/billing",t.DisplayName,p.RecurringMonths,t.Subscription!.PaidThroughUtc,p.StripeCurrentPeriodEndUtc,p.CardBrand??c.CardBrand,p.CardLast4??c.CardLast4),ct);
    private async Task SafeNotify(BillingNotification notification,CancellationToken ct){try{await _notifications.SendAsync(notification,ct);}catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}catch(Exception ex){_logger.LogError(ex,"Billing notification failed after business operation. TenantId={TenantId}, Type={Type}, ExternalId={ExternalId}.",notification.TenantId,notification.Type,notification.ExternalId);}}
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
    private async Task<TenantBillingProfile> Profile(Guid tenantId,CancellationToken ct){var p=await _db.TenantBillingProfiles.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.TenantId==tenantId,ct);if(p is null){var slug=await _db.Tenants.IgnoreQueryFilters().Where(x=>x.Id==tenantId).Select(x=>x.Slug).SingleAsync(ct);_tenantContext.SetTenant(tenantId,slug);p=new TenantBillingProfile(Guid.NewGuid(),tenantId);_db.Add(p);await _db.SaveChangesAsync(ct);}return p;}
    private async Task<BillingSettings> Settings(CancellationToken ct){var s=await _db.BillingSettings.SingleOrDefaultAsync(x=>x.Code==BillingSettings.SingletonCode,ct);if(s is null){s=new BillingSettings(Guid.NewGuid(),_clock.UtcNow);_db.Add(s);await _db.SaveChangesAsync(ct);}return s;}
    private async Task<Tenant> RequireTenant(Guid id,CancellationToken ct)=>await _db.Tenants.Include(x=>x.Subscription).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new InvalidOperationException("Tenant no encontrado.");
    private async Task<BillingOrder> Order(Guid id,CancellationToken ct)=>await _db.BillingOrders.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new InvalidOperationException("Orden no encontrada.");
    private static BillingSettingsDto Map(BillingSettings s)=>new(s.Currency,s.TaxRate,s.PricesIncludeTax,s.GracePeriodDays,s.CardPaymentsEnabled,s.BankTransferEnabled,s.RequireTransferReceipt,s.AutomaticRenewalEnabled,s.CfdiEnabled,s.BankName,s.BeneficiaryName,s.Clabe,s.BankTransferInstructions,s.SupportEmail);
    private static BillingOrderDto Map(BillingOrder o,string? url=null)=>new(o.Id,o.TenantId,o.PlanCode,o.Months,o.Subtotal,o.Tax,o.Total,o.Currency,o.Status,o.PaymentMethod,o.CreatedAt,o.SubscriptionThroughUtc,url,o.BankReference,o.ReceiptUrl,o.PaymentKind);
}
