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
        /// <summary>Painel Jogos: grupos e mata-mata, uma rodada por vez.</summary>
        public PainelJogosRodadasViewModel PainelJogos { get; set; } = new();
        public ChaveamentoArvoreViewModel? Chaveamento { get; set; }
        // Fases eliminatórias antes das oitavas (preliminares, 16-avos), na ordem do torneio
        public List<FaseConfrontosViewModel> FasesAnteriores { get; set; } = new();
    }

    public class FaseConfrontosViewModel
    {
        public string Nome { get; set; } = "";
        public List<ConfrontoArvore> Confrontos { get; set; } = new();
    }
}
