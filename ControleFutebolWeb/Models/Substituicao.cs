using ControleFutebolWeb.Models;

public class Substituicao
{
    public int Id { get; set; }
    public int JogoId { get; set; }
    public Jogo Jogo { get; set; } = null!;

    public int JogadorEntrouId { get; set; }
    public Jogador JogadorEntrou { get; set; } = null!;

    public int? JogadorSaiuId { get; set; }
    public Jogador? JogadorSaiu { get; set; }

    public int Minuto { get; set; }
    public bool IsTimeCasa { get; set; }
}
