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

        // Formato da competição: PONTOS_CORRIDOS | GRUPOS | MATA_MATA | JOGO_UNICO.
        // JOGO_UNICO = decidida em partida única (Supercopa da UEFA, Supercopa do Brasil
        // em jogo único, Recopa em jogo só): o vencedor da partida já é o campeão.
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

        // Ordem dos critérios de desempate da tabela, códigos separados por ';'
        // (ex.: "VITORIAS;SALDO_GOLS;GOLS_PRO;CONFRONTO_DIRETO;CARTOES_VERMELHOS;CARTOES_AMARELOS;SORTEIO").
        // Pontos é sempre o 1º critério e não entra na lista. Nulo = ordem padrão
        // (ver Helpers.CriteriosDesempateHelper.Padrao).
        public string? CriteriosDesempate { get; set; }

        // ── Zonas da tabela (cores e legenda da classificação) ─────────────
        // Cada liga tem as suas: a Bundesliga rebaixa 2 e manda 1 ao playoff, o
        // Brasileirão tem 6 vagas na Libertadores. Nulo/0 = zona não existe; sem
        // nenhuma configurada a tabela destaca só o líder, em vez de inventar faixas.

        /// <summary>Primeiras posições que vão para a principal vaga (ex.: 4 → "Champions League").</summary>
        public int? ZonaTopoVagas { get; set; }
        public string? ZonaTopoRotulo { get; set; }

        /// <summary>Faixa logo abaixo da principal (ex.: 2 → "Europa League" / "Sul-Americana").</summary>
        public int? ZonaTopo2Vagas { get; set; }
        public string? ZonaTopo2Rotulo { get; set; }

        /// <summary>Posições que disputam playoff de rebaixamento, logo acima da zona de queda.</summary>
        public int? ZonaPlayoffVagas { get; set; }

        /// <summary>Últimas posições, rebaixadas direto.</summary>
        public int? ZonaRebaixamentoVagas { get; set; }
    }

}
