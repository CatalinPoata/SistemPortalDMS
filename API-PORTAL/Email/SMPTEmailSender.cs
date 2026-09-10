using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace API_PORTAL.Email
{
    public sealed class SmtpEmailSender
    : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(
            IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken)
        {
            var email = new MimeMessage();

            email.From.Add(
                new MailboxAddress(
                    _options.FromName,
                    _options.FromAddress));

            email.To.Add(
                MailboxAddress.Parse(message.To));

            email.Subject = message.Subject;

            var body = new BodyBuilder
            {
                TextBody = message.TextBody,
                HtmlBody = message.HtmlBody
            };

            email.Body = body.ToMessageBody();

            using var client = new SmtpClient();

            var security =
                _options.UseSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                security,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(
                    _options.Username))
            {
                await client.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);
            }

            await client.SendAsync(
                email,
                cancellationToken);

            await client.DisconnectAsync(
                true,
                cancellationToken);
        }
    }
}
