namespace ControleFutebolWeb.Models.Api
{
    // Resumo dos relatórios para o app mobile: os rankings mais usados da página
    // /Relatorios. Rankings menos usados (estatísticas por competição, match-up,
    // stats de jogo por time) ficam de fora por enquanto.
    public class RelatorioResumoDto
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        // Todas as competições (para o filtro do app), na mesma ordem da web
        // (top tier do usuário primeiro)
        public List<CompeticaoRefDto> Competicoes { get; set; } = new();
        public bool ExibirSelecao { get; set; }

        public int TotalJogos { get; set; }
        public int TotalGols { get; set; }
        public int TotalGolsContra { get; set; }
        public int TotalCartaoAmarelo { get; set; }
        public int TotalCartaoVermelho { get; set; }

        public List<RankingNotaDto> RankingNotas { get; set; } = new();
        public List<RankingNotaDto> RankingGoleiros { get; set; } = new();
        public List<RankingNotaDto> RankingDefensores { get; set; } = new();
        public List<RankingNotaDto> RankingMeias { get; set; } = new();
        public List<RankingNotaDto> RankingAtacantes { get; set; } = new();

        public List<JogadorValorDto> Artilheiros { get; set; } = new();
        public List<JogadorValorDto> Assistencias { get; set; } = new();
        public List<JogadorValorDto> MaisPartidas { get; set; } = new();
        public List<JogadorValorDto> MaisCartoesAmarelos { get; set; } = new();
        public List<JogadorValorDto> MaisCartoesVermelhos { get; set; } = new();

        public List<TimeEstatisticaDto> TimesAproveitamento { get; set; } = new();
        public List<TimeEstatisticaDto> TimesMaisPontos { get; set; } = new();
        public List<TimeEstatisticaDto> TimesGols { get; set; } = new();
        public List<TimeEstatisticaDto> TimesMenosGolsSofridos { get; set; } = new();

        public List<MediaPosicaoDto> MediasPorPosicao { get; set; } = new();
    }

    public class RankingNotaDto
    {
        public int JogadorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string NomeExibicao { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;
        public string? TimeNome { get; set; }
        public string? FotoUrl { get; set; }
        public double NotaFinal { get; set; }
        public string NotaLabel { get; set; } = string.Empty;
        public string NotaColor { get; set; } = string.Empty;
        public int Partidas { get; set; }
        public int Vitorias { get; set; }
        public int Derrotas { get; set; }
        public int Empates { get; set; }
    }

    public class JogadorValorDto
    {
        public int JogadorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string NomeExibicao { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;
        public string? TimeNome { get; set; }
        public string? FotoUrl { get; set; }
        public int Valor { get; set; }
        public string Detalhe { get; set; } = string.Empty;
    }

    public class TimeEstatisticaDto
    {
        public int TimeId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? EscudoUrl { get; set; }
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int SaldoGols { get; set; }
        public int Pontos { get; set; }
        public double Aproveitamento { get; set; }
    }

    public class MediaPosicaoDto
    {
        public string Posicao { get; set; } = string.Empty;
        public double Media { get; set; }
        public int TotalJogadores { get; set; }
    }
}
