// ControleFutebolWeb/Models/ViewModels/RelatoriosViewModel.cs
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // ── ViewModel principal ──────────────────────────────────────────────────
    public class RelatoriosViewModel
    {
        // Filtro
        public List<int> CompeticaoIdsFiltro { get; set; } = new();
        public List<int> TimeIdsFiltro { get; set; } = new();
        public int? TemporadaFiltro { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public bool IncluirNaoAnalisados { get; set; }
        // Aba "Estatísticas Jogadores": só lista jogadores com pelo menos esse número de jogos.
        // 1 = sem filtro (todo jogador com estatística já tem >= 1 jogo).
        public int MinJogos { get; set; } = 1;
        // true quando a competição filtrada é de seleções → exibe a seleção no lugar do clube
        public bool ExibirSelecao { get; set; }
        public List<Competicao> Competicoes { get; set; } = new();
        public List<Time> Times { get; set; } = new();

        // Totais gerais
        public int TotalJogos { get; set; }
        public int TotalGols { get; set; }
        public int TotalGolsContra { get; set; }
        public int TotalCartaoAmarelo { get; set; }
        public int TotalCartaoVermelho { get; set; }

        // Jogadores
        public List<RankingNotaItem> RankingNotas { get; set; } = new();
        public List<RankingNotaItem> RankingGoleiros { get; set; } = new();
        public List<RankingNotaItem> RankingDefensores { get; set; } = new();
        public List<RankingNotaItem> RankingMeias { get; set; } = new();
        public List<RankingNotaItem> RankingAtacantes { get; set; } = new();
        public List<JogadorEstatistica> Artilheiros { get; set; } = new();
        public List<JogadorEstatistica> GolsContraRanking { get; set; } = new();
        public List<JogadorEstatistica> Assistencias { get; set; } = new();
        public List<JogadorEstatistica> MaisPartidas { get; set; } = new();
        public List<JogadorEstatistica> MaisCartoesAmarelos { get; set; } = new();
        public List<JogadorEstatistica> MaisCartoesVermelhos { get; set; } = new();

        // Times
        public List<TimeEstatistica> TimesGols { get; set; } = new();
        public List<TimeEstatistica> TimesVitorias { get; set; } = new();
        public List<TimeEstatistica> TimesMenosGolsSofridos { get; set; } = new();
        public List<TimeEstatistica> TimesAproveitamento { get; set; } = new();
        public List<TimeEstatistica> TimesMaisPontos { get; set; } = new();
        public List<TimeEstatistica> TimesVitoriasCasa { get; set; } = new();
        public List<TimeEstatistica> TimesVitoriasVisitante { get; set; } = new();

        // Rankings de estatísticas de jogo (por partida)
        public List<TimeStatJogo> TimesFinalizacoes { get; set; } = new();
        public List<TimeStatJogo> TimesFinalizacoesNoGol { get; set; } = new();
        public List<TimeStatJogo> TimesEscanteios { get; set; } = new();
        public List<TimeStatJogo> TimesPassesCertos { get; set; } = new();
        public List<TimeStatJogo> TimesPosseBola { get; set; } = new();
        public List<TimeStatJogo> TimesExpectedGoals { get; set; } = new();
        public List<TimeStatJogo> TimesGolsEvitados { get; set; } = new();

        // Rankings de estatísticas individuais de jogadores
        public List<RankingEstatJogador> RankImpedimentos { get; set; } = new();
        public List<RankingEstatJogador> RankFinalizacoesNoGol { get; set; } = new();
        public List<RankingEstatJogador> RankPassesChave { get; set; } = new();
        public List<RankingEstatJogador> RankDesarmes { get; set; } = new();
        public List<RankingEstatJogador> RankBloqueios { get; set; } = new();
        public List<RankingEstatJogador> RankInterceptacoes { get; set; } = new();
        public List<RankingEstatJogador> RankDrilesCertos { get; set; } = new();
        public List<RankingEstatJogador> RankPenaltisDefendidos { get; set; } = new();
        public List<RankingEstatJogador> RankVezesCapitao { get; set; } = new();

        // Extras
        public List<GolsPorRodada> GolsPorRodada { get; set; } = new();
        public List<MediaPosicao> MediasPorPosicao { get; set; } = new();

        // Estatísticas por competição
        public List<EstatisticaCompeticao> EstatisticasCompeticoes { get; set; } = new();

        // Match Up: só preenchido quando exatamente 2 times estão no filtro (TimeIdsFiltro.Count == 2).
        // MatchUpTime1 é o 1º time do filtro (fica na metade esquerda do campo), MatchUpTime2 o 2º (direita).
        public MatchUpTimeViewModel? MatchUpTime1 { get; set; }
        public MatchUpTimeViewModel? MatchUpTime2 { get; set; }
    }

    // ── Match Up: última escalação titular de um time, já com as coordenadas
    // transformadas para o campo único e compartilhado (dois times de frente
    // um para o outro, como numa escalação de transmissão de TV) ────────────
    public class MatchUpTimeViewModel
    {
        public Time Time { get; set; } = null!;
        // Jogo de onde a escalação foi extraída (o mais recente com titulares registrados)
        public Jogo? JogoOrigem { get; set; }
        // true = o time jogou em casa naquele jogo; false = jogou como visitante
        public bool JogoOrigemEhCasa { get; set; }
        public List<MatchUpJogadorViewModel> Escalacao { get; set; } = new();
        // Demais jogadores do elenco (fora da escalação titular exibida) — banco
        // lateral de onde dá para arrastar substituições na simulação.
        public List<Jogador> Elenco { get; set; } = new();
    }

    public class MatchUpJogadorViewModel
    {
        public Jogador Jogador { get; set; } = null!;
        // Coordenadas (% do campo do Match Up, 0-100) já transformadas para a
        // metade correta (esquerda/direita) e espelhadas quando necessário.
        public double PosicaoX { get; set; }
        public double PosicaoY { get; set; }
        // Texto original da posição (Escalacao.Posicao) — usado com PosicaoJogadorHelper.Sigla.
        public string? Posicao { get; set; }
    }

    // ── Item do ranking de notas (com métrica base-5 + resultado) ────────────
    public class RankingNotaItem
    {
        public Jogador Jogador { get; set; } = null!;
        /// <summary>Nota final clampada em [0, 10]</summary>
        public double NotaFinal { get; set; }
        /// <summary>Média das ações (pontos de avaliação / partidas)</summary>
        public double NotaBase { get; set; }
        /// <summary>Bônus médio de resultado (+1 vitória, -1 derrota, /partidas)</summary>
        public double BonusResultado { get; set; }
        public int Partidas { get; set; }
        public int Vitorias { get; set; }
        public int Derrotas { get; set; }
        public int Empates { get; set; }

        public string NotaLabel => NotaFinal switch
        {
            >= 8.5 => "Elite",
            >= 7.5 => "Ótimo",
            >= 6.5 => "Bom",
            >= 5.5 => "Regular",
            >= 4.5 => "Abaixo",
            _ => "Fraco"
        };

        public string NotaColor => NotaFinal switch
        {
            >= 8.5 => "#f59e0b",
            >= 7.5 => "#22c55e",
            >= 6.5 => "#3b82f6",
            >= 5.5 => "#6b7280",
            >= 4.5 => "#f97316",
            _ => "#ef4444"
        };
    }

    // ── Estatística genérica de jogador ──────────────────────────────────────
    public class JogadorEstatistica
    {
        public Jogador Jogador { get; set; } = null!;
        public int Valor { get; set; }
        public string Detalhe { get; set; } = "";
    }

    // ── Estatística de time ──────────────────────────────────────────────────
    public class TimeEstatistica
    {
        public Time Time { get; set; } = null!;
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int Pontos { get; set; }
        public int VitoriasCasa { get; set; }
        public int VitoriasVisitante { get; set; }
        public int SaldoGols => GolsPro - GolsContra;
        public double Aproveitamento =>
            Jogos > 0 ? Math.Round((Pontos / (double)(Jogos * 3)) * 100, 1) : 0;
    }

    // ── Gols por rodada ──────────────────────────────────────────────────────
    public class GolsPorRodada
    {
        public int Rodada { get; set; }
        public int TotalGols { get; set; }
        public int Jogos { get; set; }
        public double MediaGols => Jogos > 0 ? Math.Round((double)TotalGols / Jogos, 1) : 0;
    }

    // ── Média por posição ────────────────────────────────────────────────────
    public class MediaPosicao
    {
        public string Posicao { get; set; } = "";
        public double Media { get; set; }
        public int TotalJogadores { get; set; }
    }

    // ── Estatísticas de jogo por time (médias por partida) ───────────────────
    public class TimeStatJogo
    {
        public Time Time { get; set; } = null!;
        public int Jogos { get; set; }
        public double Valor { get; set; }
    }

    // ── Ranking de estatística individual de jogador ─────────────────────────
    public class RankingEstatJogador
    {
        public Jogador Jogador { get; set; } = null!;
        public int Partidas { get; set; }
        public double Media { get; set; }
        public int Total { get; set; }
    }

    // ── Estatísticas por competição ──────────────────────────────────────────
    public class EstatisticaCompeticao
    {
        public Competicao Competicao { get; set; } = null!;
        public int TotalJogos { get; set; }
        public int JogosAnalisados { get; set; }
        public int TotalGols { get; set; }
        public int TotalGolsContra { get; set; }
        public int TotalCartaoAmarelo { get; set; }
        public int TotalCartaoVermelho { get; set; }
        public int VitoriasMandante { get; set; }
        public int VitoriasVisitante { get; set; }
        public int Empates { get; set; }
        public double MediaGolsPorJogo => TotalJogos > 0 ? Math.Round((double)(TotalGols + TotalGolsContra) / TotalJogos, 2) : 0;
        public double MediaCartoesPorJogo => TotalJogos > 0 ? Math.Round((double)(TotalCartaoAmarelo + TotalCartaoVermelho) / TotalJogos, 2) : 0;
        public double PctVitoriasMandante => TotalJogos > 0 ? Math.Round((double)VitoriasMandante / TotalJogos * 100, 1) : 0;
        public double PctVitoriasVisitante => TotalJogos > 0 ? Math.Round((double)VitoriasVisitante / TotalJogos * 100, 1) : 0;
        public double PctEmpates => TotalJogos > 0 ? Math.Round((double)Empates / TotalJogos * 100, 1) : 0;
        public string? ArtilheiroNome { get; set; }
        public int ArtilheiroId { get; set; }
        public string? ArtilheiroEscudoUrl { get; set; }
        public int ArtilheiroGols { get; set; }
        public string? AssistenteNome { get; set; }
        public int AssistenteId { get; set; }
        public string? AssistenteEscudoUrl { get; set; }
        public int AssistenciasTotal { get; set; }
        public string? TimeMaisVitorias { get; set; }
        public string? TimeMaisVitoriasEscudoUrl { get; set; }
        public int TimeMaisVitoriasQtd { get; set; }
        public string? JogoMaisGols { get; set; }
        public int JogoMaisGolsTotal { get; set; }
    }
}