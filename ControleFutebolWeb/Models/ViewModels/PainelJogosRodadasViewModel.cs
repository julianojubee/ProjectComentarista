namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Painel "Jogos" das telas de competição (Champions, Libertadores, Sul-Americana):
    /// a temporada inteira, uma rodada por vez, com ‹ › para navegar. Ver
    /// Views/Shared/_PainelJogosRodadas.cshtml e Helpers/PainelJogosRodadasHelper.
    /// </summary>
    public class PainelJogosRodadasViewModel
    {
        public List<RodadaJogosViewModel> Rodadas { get; set; } = new();
        /// <summary>Índice da rodada que o painel abre (a do próximo jogo sem placar).</summary>
        public int Atual { get; set; }
    }

    /// <summary>Uma rodada do painel ("Fase de liga, rodada 3", "Oitavas de final"...).</summary>
    public class RodadaJogosViewModel
    {
        public string Rotulo { get; set; } = "";
        public List<Jogo> Jogos { get; set; } = new();
    }
}
