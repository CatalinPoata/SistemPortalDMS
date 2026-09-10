using API_PORTAL.Email;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API_PORTAL_TESTS.Infrastructure
{
    public sealed class FakeAccountEmailService
    : IAccountEmailService
    {
        public List<(string Email, string Token)>
            ConfirmationMessages
        { get; } = [];

        public List<(string Email, string Token)>
            ResetMessages
        { get; } = [];

        public Task SendConfirmationAsync(
            API_PORTAL.Entities.User user,
            string rawToken,
            CancellationToken cancellationToken)
        {
            ConfirmationMessages.Add(
                (user.email, rawToken));

            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(
            API_PORTAL.Entities.User user,
            string rawToken,
            CancellationToken cancellationToken)
        {
            ResetMessages.Add(
                (user.email, rawToken));

            return Task.CompletedTask;
        }
    }
}
