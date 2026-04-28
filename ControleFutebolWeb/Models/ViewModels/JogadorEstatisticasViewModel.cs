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
        public int Nota { get; set; }
        public string Comentario { get; set; }
        public int Gols { get; set; }
    }
}