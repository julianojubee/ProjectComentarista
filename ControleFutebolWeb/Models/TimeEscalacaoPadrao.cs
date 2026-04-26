namespace ControleFutebolWeb.Models
{
    public class TimeEscalacaoPadrao
    {
        public int Id { get; set; }
        public int TimeId { get; set; }
        public Time Time { get; set; }

        public int FormacaoId { get; set; }   // 🔹 novo campo obrigatório
        public Formacao Formacao { get; set; } // navegação opcional

        public int PosicaoId { get; set; }
        public string Posicao { get; set; } = string.Empty;
        public int PosicaoX { get; set; }
        public int PosicaoY { get; set; }
        public bool Titular { get; set; }
        public int? JogadorId { get; set; }
        public Jogador Jogador { get; set; }
    }
}
