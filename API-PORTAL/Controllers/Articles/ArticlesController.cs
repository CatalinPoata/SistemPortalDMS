using API_PORTAL.Data;
using API_PORTAL.DTO.Articles;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using API_PORTAL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace API_PORTAL.Controllers.Articles
{
    [ApiController]
    [Route("api/articles")]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class ArticlesController : ControllerBase
    {
        private static readonly Regex SlugPattern = new(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex SlugSeparatorPattern = new(
            "[^a-z0-9]+",
            RegexOptions.CultureInvariant);

        private readonly PortalDbContext db;
        private readonly IServiceHtmlSanitizer htmlSanitizer;

        public ArticlesController(
            PortalDbContext db,
            IServiceHtmlSanitizer htmlSanitizer)
        {
            this.db = db;
            this.htmlSanitizer = htmlSanitizer;
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedArticleResponse<ArticleListItemResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var query = db.Articles.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(article => article.published_at)
                .ThenByDescending(article => article.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(article => new ArticleListItemResponse(
                    article.id,
                    article.slug,
                    article.title,
                    article.summary,
                    article.published_at))
                .ToListAsync(cancellationToken);

            return Ok(new PagedArticleResponse<
                ArticleListItemResponse>(items, page, pageSize, total));
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<ArticleDetailsResponse>> GetBySlug(
            string slug,
            CancellationToken cancellationToken)
        {
            var article = await FindAsync(slug, cancellationToken);

            return article is null
                ? NotFound(ArticleNotFound())
                : Ok(ToDetails(article));
        }

        [HttpPost]
        public async Task<ActionResult<ArticleDetailsResponse>> Create(
            CreateArticleRequest request,
            CancellationToken cancellationToken)
        {
            var title = request.Title?.Trim() ?? string.Empty;
            var slug = ResolveSlug(request.Slug, title);
            var body = htmlSanitizer.Sanitize(request.Body);
            var errors = ValidateWriteRequest(title, slug, body);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            if (await db.Articles.AnyAsync(
                    article => article.slug == slug,
                    cancellationToken))
            {
                return Conflict(DuplicateSlug());
            }

            var article = new Article
            {
                id = Guid.NewGuid(),
                slug = slug,
                title = title,
                summary = Normalize(request.Summary),
                body = body!,
                published_at = request.PublishedAt?.ToUniversalTime(),
                is_published = false
            };

            db.Articles.Add(article);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                return Conflict(DuplicateSlug());
            }

            return CreatedAtAction(
                nameof(GetBySlug),
                new { slug = article.slug },
                ToDetails(article));
        }

        [HttpPut("{slug}")]
        public async Task<ActionResult<ArticleDetailsResponse>> Update(
            string slug,
            UpdateArticleRequest request,
            CancellationToken cancellationToken)
        {
            var article = await FindAsync(slug, cancellationToken);

            if (article is null)
            {
                return NotFound(ArticleNotFound());
            }

            var title = request.Title?.Trim() ?? string.Empty;
            var updatedSlug = ResolveSlug(request.Slug, title);
            var body = htmlSanitizer.Sanitize(request.Body);
            var errors = ValidateWriteRequest(title, updatedSlug, body);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            if (updatedSlug != article.slug &&
                await db.Articles.AnyAsync(
                    item => item.slug == updatedSlug && item.id != article.id,
                    cancellationToken))
            {
                return Conflict(DuplicateSlug());
            }

            article.slug = updatedSlug;
            article.title = title;
            article.summary = Normalize(request.Summary);
            article.body = body!;
            article.published_at = request.PublishedAt?.ToUniversalTime();

            if (article.is_published && article.published_at is null)
            {
                article.published_at = DateTimeOffset.UtcNow;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                return Conflict(DuplicateSlug());
            }

            return Ok(ToDetails(article));
        }

        [HttpPost("{slug}/publish")]
        public async Task<ActionResult<ArticleDetailsResponse>> Publish(
            string slug,
            CancellationToken cancellationToken)
        {
            var article = await FindAsync(slug, cancellationToken);

            if (article is null)
            {
                return NotFound(ArticleNotFound());
            }

            article.is_published = true;
            article.published_at ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToDetails(article));
        }

        [HttpPost("{slug}/unpublish")]
        public async Task<ActionResult<ArticleDetailsResponse>> Unpublish(
            string slug,
            CancellationToken cancellationToken)
        {
            var article = await FindAsync(slug, cancellationToken);

            if (article is null)
            {
                return NotFound(ArticleNotFound());
            }

            article.is_published = false;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToDetails(article));
        }

        [HttpDelete("{slug}")]
        public async Task<IActionResult> Delete(
            string slug,
            CancellationToken cancellationToken)
        {
            var article = await FindAsync(slug, cancellationToken);

            if (article is null)
            {
                return NotFound(ArticleNotFound());
            }

            db.Articles.Remove(article);
            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private Task<Article?> FindAsync(
            string slug,
            CancellationToken cancellationToken)
        {
            return db.Articles.SingleOrDefaultAsync(
                article => article.slug == slug,
                cancellationToken);
        }

        private static ArticleDetailsResponse ToDetails(Article article)
        {
            return new ArticleDetailsResponse(
                article.id,
                article.slug,
                article.title,
                article.summary,
                article.body,
                article.published_at,
                article.is_published,
                article.created_at,
                article.updated_at);
        }

        private static string ResolveSlug(string? value, string title)
        {
            var normalized = Normalize(value)?.ToLowerInvariant();

            return string.IsNullOrWhiteSpace(normalized)
                ? Slugify(title)
                : normalized;
        }

        private static string Slugify(string value)
        {
            var builder = new StringBuilder();

            foreach (var character in value.Normalize(
                NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return SlugSeparatorPattern.Replace(
                builder.ToString().ToLowerInvariant(),
                "-").Trim('-');
        }

        private static IReadOnlyDictionary<string, string[]>
            ValidateWriteRequest(string title, string slug, string? body)
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(title))
            {
                errors["title"] = ["Titlul este obligatoriu."];
            }

            if (!SlugPattern.IsMatch(slug))
            {
                errors["slug"] =
                [
                    "Slug-ul poate conține litere mici, cifre și cratimă."
                ];
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                errors["body"] =
                [
                    "Conținutul HTML nu conține elemente permise."
                ];
            }

            return errors;
        }

        private ActionResult ValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Date de articol invalide",
                    "Unul sau mai multe câmpuri sunt invalide.",
                    errors.ToDictionary(item => item.Key, item => item.Value)));
        }

        private ActionResult InvalidPage(int page, int pageSize)
        {
            var errors = new Dictionary<string, string[]>();

            if (page < 1)
            {
                errors["page"] = ["Pagina trebuie să fie cel puțin 1."];
            }

            if (pageSize is < 1 or > 100)
            {
                errors["pageSize"] =
                ["pageSize trebuie să fie între 1 și 100."];
            }

            return ValidationFailure(errors);
        }

        private ProblemDetails ArticleNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Articol inexistent",
                "Articolul solicitat nu există.");
        }

        private ProblemDetails DuplicateSlug()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Slug duplicat",
                "Există deja un articol cu acest slug.");
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
