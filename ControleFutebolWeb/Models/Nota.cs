namespace ControleFutebolWeb.Models
{
    public class Nota
    {
        public int Id { get; set; }
        public int Valor { get; set; }   // pontuação total calculada
        public string Comentario { get; set; }  // observação

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; }
        public ICollection<Notadetalhe> Detalhes { get; set; } = new List<Notadetalhe>();
    }
}