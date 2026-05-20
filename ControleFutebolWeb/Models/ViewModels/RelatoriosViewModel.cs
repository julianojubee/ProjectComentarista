// ControleFutebolWeb/Models/ViewModels/RelatoriosViewModel.cs
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // ── ViewModel principal ──────────────────────────────────────────────────
    public class RelatoriosViewModel
    {
        // Filtro
        public int? CompeticaoIdFiltro { get; set; }
        public List<Competicao> Competicoes { get; set; } = new();

        // Totais gerais
        public int TotalJogos { get; set; }
        public int TotalGols { get; set; }
        public int TotalGolsContra { get; set; }
        public int TotalCartaoAmarelo { get; set; }
        public int TotalCartaoVermelho { get; set; }

        // Jogadores
        public List<RankingNotaItem> RankingNotas { get; set; } = new();
        public List<JogadorEstatistica> Artilheiros { get; set; } = new();
        public List<JogadorEstatistica> GolsContraRanking { get; set; } = new();
        public List<JogadorEstatistica> MaisPartidas { get; set; } = new();
        public List<JogadorEstatistica> MaisCartoesAmarelos { get; set; } = new();
        public List<JogadorEstatistica> MaisCartoesVermelhos { get; set; } = new();

        // Times
        public List<TimeEstatistica> TimesGols { get; set; } = new();
        public List<TimeEstatistica> TimesVitorias { get; set; } = new();
        public List<TimeEstatistica> TimesMenosGolsSofridos { get; set; } = new();
        public List<TimeEstatistica> TimesAproveitamento { get; set; } = new();
        public List<TimeEstatistica> TimesMaisPontos { get; set; } = new();

        // Extras
        public List<GolsPorRodada> GolsPorRodada { get; set; } = new();
        public List<MediaPosicao> MediasPorPosicao { get; set; } = new();
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
}