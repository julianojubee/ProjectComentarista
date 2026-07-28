using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models
{
    // Categoria editorial do blog (ex.: Análises, Rodada, Bastidores).
    // CRUD/atribuição na UI é fase 2; a tabela já nasce na primeira migration.
    public class BlogCategoria
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string Nome { get; set; } = "";

        [Required, StringLength(80)]
        public string Slug { get; set; } = "";

        [StringLength(300)]
        public string? Descricao { get; set; }

        public int Ordem { get; set; }

        public ICollection<BlogPost> Posts { get; set; } = new List<BlogPost>();
    }
}
