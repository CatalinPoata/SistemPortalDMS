namespace API_PORTAL.DTO.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Subject,
    string Body,
    string LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int Page,
    int PageSize,
    int Total);
