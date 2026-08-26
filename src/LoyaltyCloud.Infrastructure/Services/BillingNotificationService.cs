using System.Globalization;
using System.Net;
using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;


namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class BillingNotificationService(
    ITransactionalEmailSender sender,
    IBillingEmailConfigurationProvider configuration,
    ILogger<BillingNotificationService> logger) : IBillingNotificationService
{
    public async Task SendAsync(BillingNotification n, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(n.Recipient))
        {
            logger.LogWarning("Billing notification skipped: no billing contact email. TenantId={TenantId}, Type={Type}.", n.TenantId, n.Type);
            return;
        }

        var settings = await configuration.GetAsync(ct);
        if (!settings.Enabled)
        {
            logger.LogInformation("Billing notification skipped: email disabled. TenantId={TenantId}, Type={Type}.", n.TenantId, n.Type);
            return;
        }
        if (!settings.IsComplete)
        {
            logger.LogError("Billing notification skipped: email configuration incomplete. TenantId={TenantId}, Type={Type}, Provider={Provider}.", n.TenantId, n.Type, settings.Provider);
            return;
        }

        try
        {
            await sender.SendAsync(Build(n, settings), ct);
            logger.LogInformation("Billing email sent. TenantId={TenantId}, Type={Type}, ExternalId={ExternalId}, Provider={Provider}.", n.TenantId, n.Type, n.ExternalId, settings.Provider);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Billing email delivery failed. TenantId={TenantId}, Type={Type}, ExternalId={ExternalId}, Provider={Provider}.", n.TenantId, n.Type, n.ExternalId, settings.Provider);
        }
    }

    private static TransactionalEmail Build(BillingNotification n, BillingEmailSettingsDto options)
    {
        var (subject, title, intro) = n.Type switch
        {
            BillingNotificationType.UpcomingCharge => ("Tu suscripción se renovará próximamente", "Próximo cobro", $"La suscripción de {n.BusinessName} se renovará próximamente."),
            BillingNotificationType.PaymentSucceeded => ("Pago de suscripción confirmado", "Pago confirmado", $"Recibimos correctamente el pago de la suscripción de {n.BusinessName}."),
            BillingNotificationType.PaymentFailed => ("No pudimos procesar tu renovación", "Pago no procesado", $"No pudimos procesar la renovación de {n.BusinessName}. Actualiza tu método de pago para evitar la suspensión."),
            BillingNotificationType.AutoRenewDisabled => ("Renovación automática desactivada", "Renovación desactivada", $"La renovación automática de {n.BusinessName} fue desactivada. No se realizarán nuevos cargos."),
            BillingNotificationType.AutoRenewEnabled => ("Renovación automática activada", "Renovación activada", $"La renovación automática de {n.BusinessName} está activa nuevamente."),
            _ => ("Actualización de tu suscripción", "Actualización de suscripción", $"Hay una actualización para la suscripción de {n.BusinessName}.")
        };

        var rows = new List<(string Label, string Value)>();
        if (n.Amount.HasValue) rows.Add(("Monto", $"{n.Amount.Value:N2} {n.Currency}".Trim()));
        if (n.PeriodMonths.HasValue) rows.Add(("Periodo", n.PeriodMonths == 1 ? "1 mes" : $"{n.PeriodMonths} meses"));
        if (n.EffectiveUtc.HasValue) rows.Add((n.Type == BillingNotificationType.UpcomingCharge ? "Fecha del próximo cobro" : "Fecha", Date(n.EffectiveUtc)));
        if (n.PaidThroughUtc.HasValue) rows.Add(("Nueva vigencia", Date(n.PaidThroughUtc)));
        if (n.NextRenewalUtc.HasValue) rows.Add(("Próxima renovación", Date(n.NextRenewalUtc)));
        if (n.GraceEndsUtc.HasValue) rows.Add(("Fecha límite del periodo de gracia", Date(n.GraceEndsUtc)));
        if (!string.IsNullOrWhiteSpace(n.CardLast4)) rows.Add(("Método", $"{n.CardBrand ?? "Tarjeta"} •••• {n.CardLast4}"));

        var url = AbsoluteUrl(options.ApplicationBaseUrl, n.BillingUrl);
        var textRows = string.Join(Environment.NewLine, rows.Select(x => $"{x.Label}: {x.Value}"));
        var text = $"{title}{Environment.NewLine}{Environment.NewLine}{intro}{Environment.NewLine}{Environment.NewLine}{textRows}{Environment.NewLine}{Environment.NewLine}Administrar suscripción: {url}{Environment.NewLine}{Environment.NewLine}LoyaltyCloud";
        var htmlRows = string.Join("", rows.Select(x => $"<tr><td style=\"padding:8px;color:#64748b\">{WebUtility.HtmlEncode(x.Label)}</td><td style=\"padding:8px;font-weight:600\">{WebUtility.HtmlEncode(x.Value)}</td></tr>"));
        var html = $"<!doctype html><html><body style=\"margin:0;background:#f1f5f9;font-family:Arial,sans-serif;color:#0f172a\"><table role=\"presentation\" width=\"100%\"><tr><td align=\"center\" style=\"padding:24px 12px\"><table role=\"presentation\" width=\"100%\" style=\"max-width:600px;background:#fff;border-radius:12px\"><tr><td style=\"padding:28px\"><div style=\"font-weight:700;color:#7c3aed\">LoyaltyCloud</div><h1 style=\"font-size:24px\">{WebUtility.HtmlEncode(title)}</h1><p style=\"line-height:1.6\">{WebUtility.HtmlEncode(intro)}</p><table role=\"presentation\" width=\"100%\" style=\"margin:20px 0\">{htmlRows}</table><a href=\"{WebUtility.HtmlEncode(url)}\" style=\"display:inline-block;background:#7c3aed;color:#fff;text-decoration:none;padding:12px 18px;border-radius:8px\">Administrar suscripción</a><p style=\"margin-top:28px;color:#94a3b8;font-size:12px\">Este es un mensaje transaccional de LoyaltyCloud.</p></td></tr></table></td></tr></table></body></html>";
        return new TransactionalEmail(n.Recipient!, subject, text, html, options.FromAddress!, options.FromName);
    }

    private static string Date(DateTime? value) => value!.Value.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-MX"));
    private static string AbsoluteUrl(string? baseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)) return absolute.ToString();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var root)) return path;
        return new Uri(root, path).ToString();
    }
}
