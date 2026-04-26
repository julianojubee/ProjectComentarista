using ControleFutebolWeb.Models;
namespace ControleFutebolWeb.Models 
{ 
    public class Escalacao
        {
            public int Id { get; set; }
            public int JogoId { get; set; }
            public Jogo Jogo { get; set; }

            public int? JogadorId { get; set; }
            public Jogador Jogador { get; set; }

            public bool Titular { get; set; }
            public string Posicao { get; set; }

            public bool IsTimeCasa { get; set; } // true = casa, false = visitante

            // Novas propriedades para posição no campo
            public double PosicaoX { get; set; }
            public double PosicaoY { get; set; }

        }
}