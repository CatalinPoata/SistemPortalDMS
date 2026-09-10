namespace API_PORTAL.DTO.Articles
{
    public sealed record ArticleListItemResponse(
        Guid Id,
        string Slug,
        string Title,
        string? Summary,
        DateTimeOffset? PublishedAt);

    public sealed record ArticleDetailsResponse(
        Guid Id,
        string Slug,
        string Title,
        string? Summary,
        string Body,
        DateTimeOffset? PublishedAt,
        bool IsPublished,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record PagedArticleResponse<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int Total);
}
