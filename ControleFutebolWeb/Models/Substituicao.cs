using ControleFutebolWeb.Models;

public class Substituicao
{
    public int Id { get; set; }
    public int JogoId { get; set; }
    public Jogo Jogo { get; set; } = null!;

    // Nullable: a api-football às vezes não informa quem entrou (evento sem
    // "assist"). Antes o import gravava o próprio jogador que SAIU como quem
    // entrou — corrompia as setas de substituição da tela Analisar.
    public int? JogadorEntrouId { get; set; }
    public Jogador? JogadorEntrou { get; set; }

    public int? JogadorSaiuId { get; set; }
    public Jogador? JogadorSaiu { get; set; }

    public int Minuto { get; set; }
    public bool IsTimeCasa { get; set; }
}
