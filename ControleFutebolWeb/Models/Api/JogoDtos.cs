namespace ControleFutebolWeb.Models.Api
{
    public class JogoResumoDto
    {
        public int Id { get; set; }
        public DateTime? Data { get; set; }
        public int CompeticaoId { get; set; }
        public string? CompeticaoNome { get; set; }
        public int TimeCasaId { get; set; }
        public string TimeCasaNome { get; set; } = string.Empty;
        public string? TimeCasaEscudoUrl { get; set; }
        public int TimeVisitanteId { get; set; }
        public string TimeVisitanteNome { get; set; } = string.Empty;
        public string? TimeVisitanteEscudoUrl { get; set; }
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public string? Status { get; set; }
        // true quando o usuário logado marcou este jogo como analisado (mesma
        // regra do painel /Jogos/Hoje da web).
        public bool AnalisadoPorMim { get; set; }
    }

    public class JogoDetalheDto : JogoResumoDto
    {
        public string? Estadio { get; set; }
        public string? Arbitro { get; set; }
        public int? PenaltisCasa { get; set; }
        public int? PenaltisVisitante { get; set; }

        public List<EscalacaoJogadorDto> EscalacaoCasa { get; set; } = new();
        public List<EscalacaoJogadorDto> EscalacaoVisitante { get; set; } = new();
        public List<GolJogoDto> Gols { get; set; } = new();
        public List<CartaoJogoDto> Cartoes { get; set; } = new();
        // Estatísticas de partida por time (posse, finalizações etc.), extraídas
        // do EstatisticasJson importado da api-football.
        public List<EstatisticaTimeJogoDto> EstatisticasTimes { get; set; } = new();
    }

    public class EscalacaoJogadorDto
    {
        public int JogadorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Posicao { get; set; }
        public bool Titular { get; set; }
        public string? FotoUrl { get; set; }
    }

    public class GolJogoDto
    {
        public int JogadorId { get; set; }
        public string JogadorNome { get; set; } = string.Empty;
        public int Minuto { get; set; }
        public bool Contra { get; set; }
    }

    public class CartaoJogoDto
    {
        public int JogadorId { get; set; }
        public string JogadorNome { get; set; } = string.Empty;
        public int Minuto { get; set; }
        public string Tipo { get; set; } = string.Empty; // "Amarelo" | "Vermelho"
    }

    public class EstatisticaTimeJogoDto
    {
        public string Nome { get; set; } = string.Empty; // chave da api-football (ex.: "Ball Possession")
        public string? ValorCasa { get; set; }
        public string? ValorVisitante { get; set; }
    }
}
