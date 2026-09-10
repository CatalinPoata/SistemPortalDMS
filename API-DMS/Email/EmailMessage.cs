namespace API_DMS.Email
{
    public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string HtmlBody);
}
