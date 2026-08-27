using Xunit;
using LoyaltyCloud.Infrastructure.Configuration;

namespace LoyaltyCloud.Tests.Integration;

public sealed class BillingUxTests
{
    [Fact]
    public void Card_action_is_available_when_billing_and_stripe_are_configured()
    {
        var stripe = new StripeOptions
        {
            Enabled = true,
            SecretKey = "sk_test_placeholder",
            PublishableKey = "pk_test_placeholder",
            WebhookSecret = "whsec_placeholder"
        };

        var cardPaymentsAvailable = true && stripe.IsConfigured;

        Assert.True(cardPaymentsAvailable);
    }

    [Fact]
    public void Card_action_is_hidden_when_stripe_is_not_configured()
    {
        var stripe = new StripeOptions { Enabled = true };

        var cardPaymentsAvailable = true && stripe.IsConfigured;

        Assert.False(cardPaymentsAvailable);
    }

    [Fact]
    public void Bank_transfer_action_is_controlled_by_billing_setting()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");

        Assert.Contains("model.Settings.BankTransferEnabled", page);
        Assert.Contains("Pagar por transferencia", page);
    }

    [Fact]
    public void Active_billing_reuses_the_normal_tenant_layout()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");
        var adaptiveLayout = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "BillingAdaptiveLayout.razor");
        var mainLayout = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor");

        Assert.Contains("@layout LoyaltyCloud.Admin.Components.Layout.BillingAdaptiveLayout", page);
        Assert.Contains("LayoutView Layout=\"@typeof(MainLayout)\"", adaptiveLayout);
        Assert.Contains("class=\"kb-sidebar ", mainLayout);
        Assert.Contains("Dashboard", mainLayout);
        Assert.Contains("Suscripción", mainLayout);
    }

    [Fact]
    public void Billing_never_shows_the_back_to_dashboard_link()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");

        Assert.DoesNotContain("Volver al panel", page);
        Assert.DoesNotContain("href=\"/dashboard\"", page);
    }

    [Fact]
    public void Suspended_billing_uses_restricted_shell_and_keeps_logout()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");
        var adaptiveLayout = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "BillingAdaptiveLayout.razor");

        Assert.Contains("if (billingOnly)", adaptiveLayout);
        Assert.Contains("max-width:960px", adaptiveLayout);
        Assert.Contains("@if (BillingOnly)", page);
        Assert.Contains("action=\"/logout\"", page);
        Assert.Contains("Cerrar sesión", page);
        Assert.DoesNotContain("kb-sidebar", adaptiveLayout);
    }

    [Fact]
    public void Billing_layout_uses_real_auth_state_and_rechecks_after_reactivation()
    {
        var adaptiveLayout = Read("src", "LoyaltyCloud.Admin", "Components", "Layout", "BillingAdaptiveLayout.razor");

        Assert.Contains("Auth.IsBillingOnlyAsync(principal)", adaptiveLayout);
        Assert.Contains("OnParametersSetAsync", adaptiveLayout);
        Assert.Contains("Value=\"@false\"", adaptiveLayout);
        Assert.Contains("Value=\"@true\"", adaptiveLayout);
    }

    [Fact]
    public void Billing_history_uses_spanish_presentation_labels()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");

        Assert.Contains("BillingPaymentMethod.Card => \"Tarjeta\"", page);
        Assert.Contains("BillingPaymentMethod.BankTransfer => \"Transferencia\"", page);
        Assert.Contains("BillingOrderStatus.Paid => \"Pagado\"", page);
        Assert.Contains("BillingOrderStatus.Pending => \"Pendiente\"", page);
        Assert.Contains("BillingOrderStatus.Expired => \"Expirado\"", page);
        Assert.Contains("value == 1 ? \"1 mes\" : $\"{value} meses\"", page);
        Assert.DoesNotContain("mes(es)", page);
    }

    [Fact]
    public void Payment_result_has_pending_paid_authenticated_and_anonymous_states()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "BillingPaymentResult.razor");

        Assert.Contains("Estamos confirmando tu pago", page);
        Assert.Contains("Pago confirmado", page);
        Assert.Contains("Tu suscripción está activa", page);
        Assert.Contains("Continuar al panel", page);
        Assert.Contains("Iniciar sesión", page);
        Assert.Contains("result?.TenantOperational == true", page);
        Assert.Contains("@attribute [AllowAnonymous]", page);
    }

    [Fact]
    public void Payment_result_cancelled_and_polling_are_safe_and_bounded()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "BillingPaymentResult.razor");

        Assert.Contains("Pago cancelado", page);
        Assert.Contains("No se realizó ningún cargo", page);
        Assert.Contains("PollAttempts = 15", page);
        Assert.Contains("TimeSpan.FromSeconds(2)", page);
        Assert.Contains("result?.Status == BillingOrderStatus.Pending", page);
        Assert.Contains("pollingTimedOut = true", page);
        Assert.Contains("Tu pago todavía se está procesando", page);
    }

    [Fact]
    public void Tenant_billing_offers_only_effectively_available_payment_methods()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");
        var service = Read("src", "LoyaltyCloud.Infrastructure", "Services", "BillingService.cs");

        Assert.Contains("model.CardPaymentsAvailable", page);
        Assert.Contains("model.Settings.BankTransferEnabled", page);
        Assert.Contains("Pagar con tarjeta", page);
        Assert.Contains("Pagar por transferencia", page);
        Assert.Contains("settings.CardPaymentsEnabled && _gateway.IsAvailable", service);
        Assert.Contains("!s.CardPaymentsEnabled||!_gateway.IsAvailable", service);
        Assert.Contains("!s.BankTransferEnabled", service);
    }

    [Fact]
    public void Tenant_billing_prevents_double_submit_and_uses_backend_quote_and_order()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");

        Assert.Contains("if (busy)", page);
        Assert.Contains("if (quote is null)", page);
        Assert.Contains("disabled=\"@busy\"", page);
        Assert.Contains("Procesando...", page);
        Assert.Contains("BillingService.QuoteAsync", page);
        Assert.Contains("BillingService.CreateOrderAsync", page);
        Assert.Contains("await InvokeAsync(StateHasChanged)", page);
        Assert.Contains("type=\"button\"", page);
        Assert.Contains("forceLoad: true", page);
        Assert.Contains("No fue posible iniciar el pago. Intenta nuevamente.", page);
        Assert.Contains("Stripe Checkout no devolvió una URL de pago.", page);
        Assert.Contains("Logger.LogError(ex, \"Failed to start payment.", page);
        Assert.DoesNotContain("Subtotal =", page);
        Assert.DoesNotContain("Tax =", page);
        Assert.DoesNotContain("Total =", page);
    }

    [Fact]
    public void Billing_copy_is_valid_utf8_without_mojibake()
    {
        var billing = Read("src", "LoyaltyCloud.Admin", "Pages", "Billing.razor");
        var settings = Read("src", "LoyaltyCloud.Admin", "Pages", "PlatformBillingSettings.razor");
        var combined = billing + settings;

        Assert.Contains("Cotización", billing);
        Assert.Contains("Suscripción", combined);
        Assert.Contains("Configuración", combined);
        Assert.Contains("Período", billing);
        Assert.Contains("Métodos", settings);
        Assert.DoesNotContain("Ã", combined);
        Assert.DoesNotContain("Â", combined);
    }

    [Fact]
    public void Billing_settings_exposes_fixed_success_and_error_notifications()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "PlatformBillingSettings.razor");
        var css = Read("src", "LoyaltyCloud.Admin", "wwwroot", "css", "site.css");

        Assert.Contains("Configuración guardada correctamente.", page);
        Assert.Contains("No fue posible guardar la configuración.", page);
        Assert.Contains("kb-toast--success", page);
        Assert.Contains("kb-toast--error", page);
        Assert.Contains("role=\"@(toastIsError ? \"alert\" : \"status\")\"", page);
        Assert.Contains("TimeSpan.FromSeconds(4)", page);
        Assert.Contains("position: fixed", css);
    }

    [Fact]
    public void App_shows_visible_feedback_when_Blazor_circuit_disconnects()
    {
        var app = Read("src", "LoyaltyCloud.Admin", "App.razor");
        var css = Read("src", "LoyaltyCloud.Admin", "wwwroot", "css", "site.css");

        Assert.Contains("id=\"components-reconnect-modal\"", app);
        Assert.Contains("Se perdió la conexión.", app);
        Assert.Contains("autostart=\"false\"", app);
        Assert.Contains("Blazor.start()", app);
        Assert.Contains("components-reconnect-show", css);
        Assert.Contains("position: fixed", css);
    }

    [Fact]
    public void Billing_settings_reports_save_failure_and_reloads_persisted_plan()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "PlatformBillingSettings.razor");

        Assert.Contains("catch (Exception ex)", page);
        Assert.Contains("Logger.LogError(ex, \"Failed to save billing plan", page);
        Assert.Contains("No fue posible guardar el plan.", page);
        Assert.Contains("plans = await Billing.GetPlansAsync();", page);
        Assert.Contains("ApplyPlan(persisted);", page);
        Assert.Contains("Plan guardado correctamente.", page);
    }

    [Fact]
    public void Super_admin_can_configure_all_recurring_Stripe_price_ids()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "PlatformBillingSettings.razor");
        Assert.Contains("Stripe Price ID - 1 mes", page);
        Assert.Contains("Stripe Price ID - 3 meses", page);
        Assert.Contains("Stripe Price ID - 6 meses", page);
        Assert.Contains("Stripe Price ID - 12 meses", page);
        Assert.Contains("plan.StripeOneMonthPriceId", page);
    }

    [Fact]
    public void Billing_email_settings_are_restricted_and_do_not_render_secrets()
    {
        var page = Read("src", "LoyaltyCloud.Admin", "Pages", "PlatformBillingSettings.razor");

        Assert.Contains("Authorize(Roles = LoyaltyCloud.Admin.Auth.SuperAdminAuthDefaults.Role)", page);
        Assert.Contains("Notificaciones por email", page);
        Assert.Contains("Credenciales:", page);
        Assert.DoesNotContain("Email__Password", page);
        Assert.DoesNotContain("SmtpHost", page);
        Assert.DoesNotContain("SmtpPort", page);
    }
    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([GetRepositoryRoot(), .. parts]));

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
