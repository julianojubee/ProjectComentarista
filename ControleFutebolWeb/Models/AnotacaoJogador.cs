using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    // Anotação livre sobre um jogador — mesmo conceito de AnotacaoTime, só que o
    // dono é o atleta (lesão, contrato, curiosidade…). Fica em /AnotacoesJogador.
    public class AnotacaoJogador
    {
        public int Id { get; set; }

        public int JogadorId { get; set; }
        [ValidateNever]
        public Jogador Jogador { get; set; } = null!;

        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;

        public string? Categoria { get; set; } // ex.: "Lesão", "Contrato", "Curiosidade"

        public DateTime DtInc { get; set; } = DateTime.UtcNow;
        public DateTime? DtAlt { get; set; }

        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }
    }
}
