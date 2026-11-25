using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using WebApplication1.Core.Repositries;

namespace WebApplication1.Service
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration configuration;

        public EmailService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task sendEmailAsync(string to, string subject, string body)
        {
            var settings = configuration.GetSection("EmailSetting");
            var hostname = settings["SmtpServer"];
            var username = settings["Username"];
            var password = settings["Password"];

            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(password) || !int.TryParse(settings["port"], out var port))
            {
                throw new InvalidOperationException("SMTP settings are incomplete.");
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(settings["From"] ?? username));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var smtpClient = new SmtpClient();
            var tls = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await smtpClient.ConnectAsync(hostname, port, tls);
            await smtpClient.AuthenticateAsync(username, password);
            await smtpClient.SendAsync(new SmtpSendRequest(message));
            await smtpClient.DisconnectAsync(true);
        }
    }
}
