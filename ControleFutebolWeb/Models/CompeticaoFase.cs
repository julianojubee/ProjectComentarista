using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Fase declarada de uma competição (ex.: "Fase de Grupos" + "Mata-Mata",
    /// "Pontos Corridos" + "Playoffs"). Quando uma competição tem fases cadastradas,
    /// a tela de detalhes monta uma visualização por fase conforme o Tipo de cada uma,
    /// em vez de usar apenas o Competicao.Tipo único.
    ///
    /// Os jogos NÃO referenciam a fase: a associação é calculada em tempo de leitura
    /// pelo <see cref="Helpers.FaseJogoClassifier"/> a partir de Jogo.Grupo (round da
    /// api-football), com override opcional via <see cref="RoundsPattern"/>.
    /// </summary>
    public class CompeticaoFase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int CompeticaoId { get; set; }

        [ValidateNever]
        public Competicao? Competicao { get; set; }

        [Required]
        public string Nome { get; set; } = string.Empty;

        // Mesmos valores de Competicao.Tipo: PONTOS_CORRIDOS | GRUPOS | MATA_MATA
        [Required]
        public string Tipo { get; set; } = string.Empty;

        // Ordem de exibição (1, 2, 3...)
        public int Ordem { get; set; }

        // Padrões opcionais (separados por ";") casados por Contains em Jogo.Grupo
        // para forçar jogos nesta fase quando a heurística não basta
        // (ex.: "Apertura" / "Clausura", ou "Quarter;Semi;Final").
        public string? RoundsPattern { get; set; }
    }
}
