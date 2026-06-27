namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /CopaDoMundo (grupos + terceiros colocados + chaveamento).</summary>
    public class CopaDoMundoIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<GrupoViewModel> Grupos { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public int RodadaAtual { get; set; }
        public List<Classificacao> TerceirosColocados { get; set; } = new();
        public ChaveamentoCopaViewModel? Chaveamento { get; set; }
    }
}
