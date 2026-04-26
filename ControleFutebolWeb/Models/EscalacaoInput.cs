namespace ControleFutebolWeb.Models
{
    public class EscalacaoInput
    {
        public int Id { get; set; }  // ← adicionar
        public int JogadorId { get; set; }
        public int PosicaoId { get; set; }
        public int PosicaoX { get; set; }
        public int PosicaoY { get; set; }
    }

}