using ControleFutebolWeb.Models;

public class Gol
{
    public int Id { get; set; }
    public int JogoId { get; set; }
    public Jogo Jogo { get; set; }

    public int JogadorId { get; set; }
    public Jogador Jogador { get; set; }

    public int Minuto { get; set; }
    public bool Contra { get; set; } // gol contra
}
