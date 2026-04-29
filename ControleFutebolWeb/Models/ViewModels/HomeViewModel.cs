using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Jogo> JogosRecentes { get; set; } = new();
        public List<ClassificacaoResumo> Classificacao { get; set; } = new();
        public int TotalTimes { get; set; }
        public int TotalJogadores { get; set; }
        public int TotalJogos { get; set; }
        public int TotalCompeticoes { get; set; }
    }

    public class ClassificacaoResumo
    {
        public int Posicao { get; set; }
        public Time Time { get; set; }
        public int Pontos { get; set; }
        public int Jogos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int SaldoGols => GolsPro - GolsContra;
    }
}
