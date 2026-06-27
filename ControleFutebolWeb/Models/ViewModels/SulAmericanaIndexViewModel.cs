namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /SulAmericana (fase de grupos).</summary>
    public class SulAmericanaIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<GrupoViewModel> Grupos { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public int RodadaAtual { get; set; }
    }
}
