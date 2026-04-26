namespace ControleFutebolWeb.Models
{
    public class Formacao
    {
        public int Id { get; set; }
        public string Nome { get; set; } // Ex: "4-3-3"
        public ICollection<PosicaoFormacao> Posicoes { get; set; }
    }
}

