namespace ControleFutebolWeb.Models
{
    public class Classificacao
    {
        public int TimeId { get; set; }
        public Time Time { get; set; } = null!;
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int Saldo { get; set; }
        public int SaldoGols => GolsPro - GolsContra;
        public int Pontos { get; set; }
        public int Posicao { get; set; }
        // Índice disciplinar FIFA: amarelo=1pt, vermelho direto=3pt, 2º amarelo+vermelho=4pt
        public int FairPlay { get; set; }
        // Grupo de origem (usado na tabela de terceiros)
        public string Grupo { get; set; } = string.Empty;
    }
}
