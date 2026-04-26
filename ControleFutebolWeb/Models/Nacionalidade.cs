using ControleFutebolWeb.Models;

public class Nacionalidade
{
    public int Id { get; set; }
    public string Nome { get; set; }

    // Relacionamento: uma nacionalidade pode ter vários jogadores
    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();
}
