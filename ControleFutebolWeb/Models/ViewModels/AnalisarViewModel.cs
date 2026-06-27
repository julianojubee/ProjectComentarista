using Microsoft.AspNetCore.Mvc.Rendering;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Dados da tela de análise tática de um jogo (/Jogos/Analisar).
    /// Substitui o uso de ViewBag — todas as coleções têm default não-nulo para
    /// que os dois caminhos de renderização (fase intermediária e fase normal)
    /// funcionem sem null reference.
    /// </summary>
    public class AnalisarViewModel
    {
        public Jogo Jogo { get; set; } = null!;

        public List<FaseTatica> FasesTaticas { get; set; } = new();

        public List<Escalacao> EscalacoesCasa { get; set; } = new();
        public List<Escalacao> EscalacoesVisitante { get; set; } = new();
        public List<Escalacao> ReservasCasa { get; set; } = new();
        public List<Escalacao> ReservasVisitante { get; set; } = new();

        public List<Jogador> JogadoresCasa { get; set; } = new();
        public List<Jogador> JogadoresVisitante { get; set; } = new();

        public SelectList? FormacoesCasa { get; set; }
        public SelectList? FormacoesVisitante { get; set; }
        public int? FormacaoCasaSelecionada { get; set; }
        public int? FormacaoVisitanteSelecionada { get; set; }

        public string FaseEscalacaoAtual { get; set; } = "INICIAL";
        public bool MostrarBancoReservas { get; set; }
        public bool EscalacaoFinalDisponivel { get; set; }

        public Treinador? TreinadorCasa { get; set; }
        public Treinador? TreinadorVisitante { get; set; }

        public List<int> JogadoresEntraramCasa { get; set; } = new();
        public List<int> JogadoresEntraramVisitante { get; set; } = new();
        public List<int> JogadoresSairamCasa { get; set; } = new();
        public List<int> JogadoresSairamVisitante { get; set; } = new();

        public Dictionary<int, int> GolsPorJogador { get; set; } = new();
        public Dictionary<int, int> AssistsPorJogador { get; set; } = new();

        public string? ObservacoesUsuario { get; set; }
    }
}
