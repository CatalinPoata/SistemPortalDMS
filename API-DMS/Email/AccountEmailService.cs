using API_DMS.Entities;
using Microsoft.Extensions.Options;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Email
{
    public interface IAccountEmailService
    {
        Task SendConfirmationAsync(
            User user,
            string rawToken,
            CancellationToken cancellationToken);

        Task SendPasswordResetAsync(
            User user,
            string rawToken,
            CancellationToken cancellationToken);
    }

    public sealed class AccountEmailService
        : IAccountEmailService
    {
        private readonly IEmailSender _emailSender;
        private readonly EmailOptions _options;

        public AccountEmailService(
            IEmailSender emailSender,
            IOptions<EmailOptions> options)
        {
            _emailSender = emailSender;
            _options = options.Value;
        }

        public Task SendConfirmationAsync(
            User user,
            string rawToken,
            CancellationToken cancellationToken)
        {
            var link = BuildLink(
                "confirm-email",
                rawToken);

            var name =
                WebUtility.HtmlEncode(user.full_name);

            return _emailSender.SendAsync(
                new EmailMessage(
                    user.email,
                    "Confirmarea adresei de email",
                    $"Confirmă adresa accesând: {link}",
                    $"""
                <p>Bună, {name}!</p>
                <p>
                    Pentru confirmarea adresei de email,
                    accesează linkul:
                </p>
                <p>
                    <a href="{link}">
                        Confirmă adresa de email
                    </a>
                </p>
                """),
                cancellationToken);
        }

        public Task SendPasswordResetAsync(
            User user,
            string rawToken,
            CancellationToken cancellationToken)
        {
            var link = BuildLink(
                "reset-password",
                rawToken);

            var name =
                WebUtility.HtmlEncode(user.full_name);

            return _emailSender.SendAsync(
                new EmailMessage(
                    user.email,
                    "Resetarea parolei",
                    $"Resetează parola accesând: {link}",
                    $"""
                <p>Bună, {name}!</p>
                <p>
                    Pentru resetarea parolei,
                    accesează linkul:
                </p>
                <p>
                    <a href="{link}">
                        Resetează parola
                    </a>
                </p>
                """),
                cancellationToken);
        }

        private string BuildLink(
            string route,
            string rawToken)
        {
            var token =
                Uri.EscapeDataString(rawToken);

            return
                $"{_options.PublicAppBaseUrl.TrimEnd('/')}" +
                $"/{route}?token={token}";
        }
    }
}
