namespace ControleFutebolWeb.Models
{
    // Junção N:N post ↔ tag (chave composta configurada no FutebolContext).
    public class BlogPostTag
    {
        public int PostId { get; set; }
        public BlogPost Post { get; set; } = null!;

        public int TagId { get; set; }
        public BlogTag Tag { get; set; } = null!;
    }
}
