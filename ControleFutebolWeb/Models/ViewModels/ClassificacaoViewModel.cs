using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels; 

namespace ControleFutebolWeb.Models.ViewModels
{
    public class ClassificacaoViewModel
    {
        public int CompeticaoId { get; set; }   // Id da competição
        public Time Time { get; set; }
        public int Pontos { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public int SaldoGols { get; set; }
    }
}
