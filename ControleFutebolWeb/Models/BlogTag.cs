using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models
{
    // Tag livre de post do blog. Atribuição na UI é fase 2.
    public class BlogTag
    {
        public int Id { get; set; }

        [Required, StringLength(60)]
        public string Nome { get; set; } = "";

        [Required, StringLength(60)]
        public string Slug { get; set; } = "";

        public ICollection<BlogPostTag> Posts { get; set; } = new List<BlogPostTag>();
    }
}
