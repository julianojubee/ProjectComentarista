namespace ControleFutebolWeb.Models
{
    public class Nota
    {
        public int Id { get; set; }
        public int Valor { get; set; }   // Ex: nota de 0 a 10
        public string Comentario { get; set; }

        // Relacionamento com Jogador
        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; }

        // Relacionamento com Jogo
        public int JogoId { get; set; }
        public Jogo Jogo { get; set; }
    }
}