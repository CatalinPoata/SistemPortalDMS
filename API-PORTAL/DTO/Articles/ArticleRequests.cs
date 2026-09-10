using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Articles
{
    public sealed class CreateArticleRequest
    {
        [StringLength(160)]
        public string? Slug { get; set; }

        [Required]
        [StringLength(250)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? Summary { get; set; }

        [Required]
        public string? Body { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }
    }

    public sealed class UpdateArticleRequest
    {
        [StringLength(160)]
        public string? Slug { get; set; }

        [Required]
        [StringLength(250)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? Summary { get; set; }

        [Required]
        public string? Body { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }
    }
}
