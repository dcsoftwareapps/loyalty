using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : ITransactionalEmailSender
{
    public async Task SendAsync(TransactionalEmail email, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.CredentialsConfigured)
            throw new InvalidOperationException("Email está habilitado pero su configuración SMTP está incompleta.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.Recipient));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(settings.Username, settings.Password!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
