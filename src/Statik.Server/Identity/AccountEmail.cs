using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using Statik.Server.Hosting;
using Statik.Server.Models;

namespace Statik.Server.Identity;

public interface IAccountEmailSender
{
    Task SendAsync(string email, string subject, string text, CancellationToken cancellationToken = default);
}

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string From { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
}

public class SmtpAccountEmailSender(IOptions<SmtpOptions> options) : IAccountEmailSender
{
    public async Task SendAsync(string email, string subject, string text, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.From))
        {
            throw new InvalidOperationException("Configure Smtp:Host and Smtp:From before sending account emails.");
        }
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public class AccountEmailDeliveryException(Exception inner) : Exception("Account email could not be sent.", inner);

public class AccountEmail(UserManager<User> manager, IAccountEmailSender sender, SiteOrigins sites, ILogger<AccountEmail> logger)
{
    public async Task SendConfirmationAsync(User user, CancellationToken cancellationToken = default)
    {
        var token = await manager.GenerateEmailConfirmationTokenAsync(user);
        var link = Link("confirm-email", user.Email!, token);
        await SendAsync(user.Email!, "Confirm your Statik email",
            $"Confirm your email address to use your Statik account:\n\n{link}\n\nIf you did not request this, ignore this email.", cancellationToken);
    }

    public async Task SendPasswordResetAsync(User user, CancellationToken cancellationToken = default)
    {
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        var link = Link("reset-password", user.Email!, token);
        await SendAsync(user.Email!, "Reset your Statik password",
            $"Choose a new password for your Statik account:\n\n{link}\n\nIf you did not request this, ignore this email.", cancellationToken);
    }

    private async Task SendAsync(string email, string subject, string text, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(email, subject, text, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Account email delivery failed");
            throw new AccountEmailDeliveryException(exception);
        }
    }

    // Fragment keeps tokens out of server logs.
    private string Link(string route, string email, string token) =>
        $"{sites.App.AbsoluteUri}#/{route}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
}
