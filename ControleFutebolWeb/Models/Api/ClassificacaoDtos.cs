namespace ControleFutebolWeb.Models.Api
{
    // GET api/v1/competicoes/{id}/classificacao — tabela de pontos corridos da
    // competição (mesma regra e desempate da página /Tabela/Brasileirao, mas
    // genérica para qualquer competição) + artilheiros.
    public class ClassificacaoDto
    {
        public int CompeticaoId { get; set; }
        public string CompeticaoNome { get; set; } = string.Empty;
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<ClassificacaoLinhaDto> Tabela { get; set; } = new();
        public List<ArtilheiroDto> Artilheiros { get; set; } = new();
    }

    public class ClassificacaoLinhaDto
    {
        public int Posicao { get; set; }
        public int TimeId { get; set; }
        public string TimeNome { get; set; } = string.Empty;
        public string? EscudoUrl { get; set; }
        public int Pontos { get; set; }
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int SaldoGols { get; set; }
    }

    public class ArtilheiroDto
    {
        public int JogadorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? TimeNome { get; set; }
        public string? FotoUrl { get; set; }
        public int Gols { get; set; }
    }
}
