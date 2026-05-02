namespace ControleFutebolWeb.Models
{
    public class Classificacao
    {
        public int TimeId { get; set; }
        public Time Time { get; set; }  // ← objeto Time, não string
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int Saldo { get; set; }
        public int SaldoGols => GolsPro - GolsContra; // a view usa SaldoGols
        public int Pontos { get; set; }
        public int Posicao { get; set; }
    }
}
