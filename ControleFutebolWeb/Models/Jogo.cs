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

        // Placar parcial do jogo em andamento, gravado pelo AtualizarJogosAoVivoService
        // (e pelo botão "Reimportar dados") a cada visita à api-football.
        //
        // Vive fora de PlacarCasa/PlacarVisitante de propósito: aqueles dois são o que o
        // sistema inteiro (classificação, chaveamento, relatórios, agrupamento da tela
        // Jogos/Hoje) lê como "jogo realizado", e gravar um parcial neles daria o jogo
        // por encerrado no meio do segundo tempo. Ninguém além da exibição do placar ao
        // vivo lê estes campos aqui.
        //
        // São zerados quando o jogo termina — aí o placar de verdade assume.
        public int? PlacarParcialCasa { get; set; }
        [ValidateNever]

        public int? PlacarParcialVisitante { get; set; }
        [ValidateNever]

        // Quando o parcial foi lido da API (UTC). Serve para a tela dizer "há X min",
        // para o relógio do card contar para a frente a partir de MinutoParcial e para
        // descartar parcial velho de jogo que o serviço parou de acompanhar.
        public DateTime? PlacarParcialEm { get; set; }
        [ValidateNever]

        // Relógio da partida no instante em que o parcial foi lido, direto da
        // api-football (fixture.status): o minuto, os acréscimos e o código do período
        // ("1H", "HT", "2H", "ET", "BT", "P", "SUSP", "INT"...).
        //
        // É uma leitura datada, não um cronômetro: entre um ciclo e outro do
        // AtualizarJogosAoVivoService o minuto envelhece, e é o card que conta para a
        // frente a partir de PlacarParcialEm. Seguem a mesma vida do placar parcial —
        // são zerados quando o jogo acaba.
        public int? MinutoParcial { get; set; }
        [ValidateNever]

        public int? AcrescimoParcial { get; set; }
        [ValidateNever]

        public string? StatusParcial { get; set; }
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

        // Onde assistir (ex.: "SporTV", "Prime Video"), obtido via FutnatvService.
        public string? TransmissaoTv { get; set; }
        public DateTime? TransmissaoAtualizadaEm { get; set; }
    }
}

