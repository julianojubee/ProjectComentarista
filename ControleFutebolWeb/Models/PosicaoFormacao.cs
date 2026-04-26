namespace ControleFutebolWeb.Models { 
    public class PosicaoFormacao
    {
        public int Id { get; set; }
        public string NomePosicao { get; set; }
        public double PosicaoX { get; set; }
        public double PosicaoY { get; set; }
        public int Ordem { get; set; }

        public int PosicaoId { get; set; }   // <-- novo campo lógico da posição
        public int FormacaoId { get; set; }
        public Formacao Formacao { get; set; }
    }
}