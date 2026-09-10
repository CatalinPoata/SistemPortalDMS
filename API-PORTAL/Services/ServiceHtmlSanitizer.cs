using Ganss.Xss;

namespace API_PORTAL.Services
{
    public interface IServiceHtmlSanitizer
    {
        string? Sanitize(string? html);
    }

    public sealed class ServiceHtmlSanitizer
        : IServiceHtmlSanitizer
    {
        private readonly HtmlSanitizer sanitizer = CreateSanitizer();

        public string? Sanitize(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            return sanitizer.Sanitize(html.Trim());
        }

        private static HtmlSanitizer CreateSanitizer()
        {
            var sanitizer = new HtmlSanitizer();

            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.UnionWith(
            [
                "a", "blockquote", "br", "em", "h2", "h3",
                "li", "ol", "p", "strong", "ul"
            ]);

            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.UnionWith(
            [
                "href", "rel", "target", "title"
            ]);

            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.UnionWith(
            [
                "http", "https", "mailto"
            ]);

            return sanitizer;
        }
    }
}
