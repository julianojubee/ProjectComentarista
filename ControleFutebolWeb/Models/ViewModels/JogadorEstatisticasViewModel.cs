namespace ControleFutebolWeb.Models.ViewModels
{
    public class JogadorEstatisticasViewModel
    {
        public Jogador Jogador { get; set; }
        public double MediaNotas { get; set; }
        public int TotalJogos { get; set; }
        public int TotalGols { get; set; }
        public int TotalAssistencias { get; set; } // se tiver o campo em Gol
        public List<NotaJogoItem> NotasPorJogo { get; set; }
    }

    public class NotaJogoItem
    {
        public Jogo Jogo { get; set; }
        public int Nota { get; set; }   // pontos de ação brutos
        public string Comentario { get; set; }
        public int Gols { get; set; }
        public string Resultado { get; set; }   // "V", "E", "D"
        public double BonusResultado { get; set; }   // +1, 0, -1
        public double NotaFinal { get; set; }   // clamp(0,10, 5+ação+resultado)
        public List<Notadetalhe> Detalhes { get; set; } = new();
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
    }
}