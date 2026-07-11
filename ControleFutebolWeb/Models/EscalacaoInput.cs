namespace ControleFutebolWeb.Models
{
    public class EscalacaoInput
    {
        public int Id { get; set; }  // ← adicionar
        public int JogadorId { get; set; }
        public int PosicaoId { get; set; }
        // double: as coordenadas dos slots de formação aceitam casas decimais
        // (Escalacao.PosicaoX/Y também são double) — int truncava ao salvar.
        public double PosicaoX { get; set; }
        public double PosicaoY { get; set; }
    }

}