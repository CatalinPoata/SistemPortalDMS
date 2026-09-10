namespace API_DMS.Email
{
    public interface IEmailSender
    {
        Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken);
    }
}
