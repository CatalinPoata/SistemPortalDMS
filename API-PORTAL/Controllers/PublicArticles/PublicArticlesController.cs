using API_PORTAL.Data;
using API_PORTAL.DTO.Articles;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.PublicArticles
{
    [ApiController]
    [Route("api/public/articles")]
    [AllowAnonymous]
    public sealed class PublicArticlesController : ControllerBase
    {
        private readonly PortalDbContext db;

        public PublicArticlesController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedArticleResponse<ArticleListItemResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var now = DateTimeOffset.UtcNow;
            var query = db.Articles
                .AsNoTracking()
                .Where(article => article.is_published &&
                    (article.published_at == null ||
                     article.published_at <= now));
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
            var now = DateTimeOffset.UtcNow;
            var article = await db.Articles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.slug == slug &&
                        item.is_published &&
                        (item.published_at == null ||
                         item.published_at <= now),
                    cancellationToken);

            if (article is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Articol indisponibil",
                    "Articolul nu există sau nu este publicat."));
            }

            return Ok(new ArticleDetailsResponse(
                article.id,
                article.slug,
                article.title,
                article.summary,
                article.body,
                article.published_at,
                article.is_published,
                article.created_at,
                article.updated_at));
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

            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Paginare invalidă",
                    "Parametrii de paginare sunt invalizi.",
                    errors));
        }
    }
}
