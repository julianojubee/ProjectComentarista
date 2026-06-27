using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public int Rodada {  get; set; }
        public DateTime? Data { get; set; }

        // Temporada da competição (ano da season da api-football, ex.: 2025, 2026).
        // Permite separar as tabelas/relatórios por temporada na mesma competição.
        public int Temporada { get; set; }
        public int PartidaApiId { get; set; } // ID da partida na API

        public int EventKey { get; set; }
        public int TimeCasaId { get; set; }
        [ValidateNever]

        public Time TimeCasa { get; set; }
        [ValidateNever]

        public int? PlacarCasa {  get; set; }
        [ValidateNever]

        public int? PlacarVisitante { get; set; }
        [ValidateNever]

        // Placar da disputa de pênaltis (mata-mata). Nulo quando não houve disputa.
        public int? PenaltisCasa { get; set; }
        [ValidateNever]

        public int? PenaltisVisitante { get; set; }
        [ValidateNever]

        public int TimeVisitanteId { get; set; }
        [ValidateNever]

        public Time TimeVisitante { get; set; }

        public int? FormacaoCasaId { get; set; }

        [ValidateNever]
        public Formacao FormacaoCasa { get; set; }

        public int? FormacaoVisitanteId { get; set; }

        [ValidateNever]
        public Formacao FormacaoVisitante { get; set; }

        [ValidateNever]
        public ICollection<Escalacao> Escalacoes { get; set; }  // única lista

        [ValidateNever]
        public ICollection<Gol> Gols { get; set; }

        [ValidateNever]
        public ICollection<Cartao> Cartoes { get; set; } = new List<Cartao>();

        [ValidateNever]
        public int CompeticaoId { get; set; }

        [ValidateNever]
        public Competicao? Competicao { get; set; }

        public string? Grupo { get; set; }
        public string? Observacoes { get; set; }

        public string? Status { get; set; }

        // Novo campo para controlar se já foi atualizado pelo serviço Transfermarkt
        // 0 = não atualizado, 1 = atualizado
        public int Atualizado { get; set; } = 0;
        // 0 = não analisado, 1 = analisado
        public int Analisado { get; set; } = 0;

        public string? FotoUrl { get; set; }

        public string? LinkDetalhes { get; set; }
        public string? Estadio { get; set; }
        public string? Arbitro { get; set; }

        // Estatísticas da partida (posse, finalizações, etc.) vindas da api-football,
        // guardadas em JSON: [{ "TimeId": 22, "Stats": { "Ball Possession": "48%", ... } }, ...]
        public string? EstatisticasJson { get; set; }

        // Cores do uniforme dos times nesta partida (hex sem #), vindas de fixtures/lineups.
        public string? CorCamisaCasa { get; set; }
        public string? CorNumeroCasa { get; set; }
        public string? CorCamisaVisitante { get; set; }
        public string? CorNumeroVisitante { get; set; }
    }
}

