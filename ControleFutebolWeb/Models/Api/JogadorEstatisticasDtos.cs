namespace ControleFutebolWeb.Models.Api
{
    // GET api/v1/jogadores/{id}/estatisticas — espelho enxuto da página
    // /Jogadores/Estatisticas: totais + histórico jogo a jogo com a nota final
    // (manual quando existe, senão calculada das estatísticas importadas).
    public class JogadorEstatisticasDto
    {
        public int JogadorId { get; set; }
        public string Nome { get; set; } = string.Empty;

        public int? CompeticaoIdFiltro { get; set; }
        public List<CompeticaoRefDto> Competicoes { get; set; } = new();

        public int Partidas { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        // Média das notas finais dos jogos analisados (null se nenhum analisado)
        public double? NotaMedia { get; set; }

        public List<JogadorJogoItemDto> Jogos { get; set; } = new();
    }

    public class CompeticaoRefDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    public class JogadorJogoItemDto
    {
        public int JogoId { get; set; }
        public DateTime? Data { get; set; }
        public string? CompeticaoNome { get; set; }
        public bool IsCasa { get; set; }
        public string AdversarioNome { get; set; } = string.Empty;
        public string? AdversarioEscudoUrl { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public string Resultado { get; set; } = "?"; // V | E | D | ?
        public string? Posicao { get; set; }
        public int? Minutos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }
        public bool Analisado { get; set; }
        public double? NotaFinal { get; set; }
        public bool OrigemManual { get; set; }
    }
}
