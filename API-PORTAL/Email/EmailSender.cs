namespace API_PORTAL.Email
{
    public interface IEmailSender
    {
        Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken);
    }
}
