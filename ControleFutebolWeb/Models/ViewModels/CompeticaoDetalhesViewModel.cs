namespace ControleFutebolWeb.Models.ViewModels
{
    public class CompeticaoDetalhesViewModel
    {
        public Competicao Competicao { get; set; }
        public string Tipo { get; set; }
        public List<Classificacao> Classificacao { get; set; }
        public List<GrupoViewModel> Grupos { get; set; }
    }

    public class GrupoViewModel
    {
        public string Nome { get; set; }
        public List<Classificacao> Times { get; set; }
    }

}
