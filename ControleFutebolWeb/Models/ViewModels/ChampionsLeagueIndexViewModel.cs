using ControleFutebolWeb.Controllers;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /ChampionsLeague (fase de liga + mata-mata).</summary>
    public class ChampionsLeagueIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<Classificacao> Ranking { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public List<FaseMataUclViewModel> FasesMataMata { get; set; } = new();
        public int TotalJogos { get; set; }
        public int JogosRealizados { get; set; }
    }

    /// <summary>Uma fase eliminatória (ex.: Oitavas) com seus confrontos ida/volta.</summary>
    public class FaseMataUclViewModel
    {
        public string Nome { get; set; } = "";           // nome exibido (pt-br)
        public string GrupoOriginal { get; set; } = "";  // valor de Jogo.Grupo (API)
        public bool JogoUnico { get; set; }              // Final: jogo único, sem volta
        public List<ConfrontoMataMata> Confrontos { get; set; } = new();
    }
}
