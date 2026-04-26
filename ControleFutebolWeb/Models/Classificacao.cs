namespace ControleFutebolWeb.Models
{
    public class Classificacao
    {
        public int TimeId { get; set; }
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int Saldo { get; set; }
        public int Pontos { get; set; }

        // Opcional: se quiser mostrar posição e nome do time na tabela
        public int Posicao { get; set; }
        public string Time { get; set; }

    }
}
