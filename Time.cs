namespace ControleFutebolWeb.Models
{
    public class Time
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Cidade { get; set; }

        // Id do provedor (ex.: football-data.org team id)
        public int? ProviderId { get; set; }

        // Relacionamento com Jogadores
        public List<Jogador> Jogadores { get; set; } = new List<Jogador>();
    }
}