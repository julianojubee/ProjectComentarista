namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /Libertadores (fase de grupos + chaveamento).</summary>
    public class LibertadoresIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<GrupoViewModel> Grupos { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public int RodadaAtual { get; set; }
        public ChaveamentoArvoreViewModel? Chaveamento { get; set; }
    }
}
