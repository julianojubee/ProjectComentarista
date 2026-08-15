using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    // Anotação livre sobre um treinador — mesmo conceito de AnotacaoJogador, só que o
    // dono é o técnico (estilo de jogo, contrato, retrospecto…). Fica em /AnotacoesTreinador.
    public class AnotacaoTreinador
    {
        public int Id { get; set; }

        public int TreinadorId { get; set; }
        [ValidateNever]
        public Treinador Treinador { get; set; } = null!;

        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;

        public string? Categoria { get; set; } // ex.: "Estilo de Jogo", "Contrato", "Desempenho"

        public DateTime DtInc { get; set; } = DateTime.UtcNow;
        public DateTime? DtAlt { get; set; }

        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }
    }
}
