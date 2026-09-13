using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    // Anotação livre sobre uma competição — mesmo conceito de AnotacaoTime/AnotacaoJogador,
    // só que o dono é o torneio (regulamento, calendário, favoritos…).
    // Fica em /AnotacoesCompeticao.
    public class AnotacaoCompeticao
    {
        public int Id { get; set; }

        public int CompeticaoId { get; set; }
        [ValidateNever]
        public Competicao Competicao { get; set; } = null!;

        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;

        public string? Categoria { get; set; } // ex.: "Regulamento", "Calendário", "Favoritos"

        public DateTime DtInc { get; set; } = DateTime.UtcNow;
        public DateTime? DtAlt { get; set; }

        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }
    }
}
