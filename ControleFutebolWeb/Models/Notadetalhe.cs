namespace ControleFutebolWeb.Models
{
    public class Notadetalhe
    {
        public int Id { get; set; }
        public int NotaId { get; set; }
        public Nota Nota { get; set; }

        public string AcaoId { get; set; }    // ex: "drible_certo"
        public string AcaoLabel { get; set; } // ex: "Drible bem-sucedido"
        public int Quantidade { get; set; }   // quantas vezes clicou
        public double Peso { get; set; }      // ex: +0.1, -0.5, +2
    }
}
