namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /ChampionsLeague (fase de liga + mata-mata).</summary>
    public class ChampionsLeagueIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<Classificacao> Ranking { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public List<Jogo> JogosMata { get; set; } = new();
        public List<string> FasesMata { get; set; } = new();
        public int TotalJogos { get; set; }
        public int JogosRealizados { get; set; }
    }
}
