using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleFutebolWeb.Models
{
    public class Competicao
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; } = string.Empty;

        [Required]
        public string Regiao { get; set; } = string.Empty;

        [Required]
        public string Tipo { get; set; } = string.Empty;

        // Indica se é uma competição de seleções nacionais (Copa do Mundo, Eliminatórias, etc.)
        // Usado para não confundir o time de um jogador no clube com o time dele na seleção.
        public bool EhSelecaoNacional { get; set; } = false;

        // ID da liga na api-football (ex.: 71 = Brasileirão Série A)
        public int? IdApi { get; set; }

        public ICollection<Jogo> Jogos { get; set; } = new List<Jogo>();

        // Fases declaradas (grupos + mata-mata, pontos corridos + playoffs...).
        // Vazio = competição de fase única, comportamento guiado só pelo Tipo.
        public ICollection<CompeticaoFase> Fases { get; set; } = new List<CompeticaoFase>();
        [System.ComponentModel.DataAnnotations.Schema.Column("linktransfermarket")]
        public string? LinkTransfermarket { get; set; }

        // Competição principal — aparece primeiro nos filtros e na tela de competições
        public bool TopTier { get; set; } = false;

        // URL do escudo/logo da competição (sobrepõe o logo padrão do helper)
        public string? LogoUrl { get; set; }
    }

}
