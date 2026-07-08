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

        // Tooltip da seta verde: id de quem entrou → "nome de quem saiu (minuto')".
        public Dictionary<int, string> EntrouNoLugarDe { get; set; } = new();

        public Dictionary<int, int> GolsPorJogador { get; set; } = new();
        public Dictionary<int, int> AssistsPorJogador { get; set; } = new();

        // Médias por jogo das estatísticas importadas (mesmas fórmulas de
        // /Jogadores/Estatisticas), por jogador — exibidas no tooltip de info.
        public Dictionary<int, MediasPorJogo> MediasPorJogador { get; set; } = new();

        // Total de jogos em que o jogador começou como titular (contagem de carreira,
        // não só desta partida) — exibido no tooltip de info.
        public Dictionary<int, int> TitularPorJogador { get; set; } = new();

        // Jogo analisado pelo usuário atual (existência de JogoAnalisadoUsuario).
        public bool Analisado { get; set; }

        // Observações categorizadas por tag (mandante/visitante/competição/jogador/marco).
        public List<ObservacaoJogoTag> ObservacoesTag { get; set; } = new();

        // Jogadores escalados neste jogo (qualquer fase), para o seletor da tag "Jogador".
        public List<Jogador> JogadoresEscalados { get; set; } = new();

        // Jogadores expulsos (cartão vermelho) neste jogo — o botão deles no campo
        // fica cinza e não pode mais ser arrastado/movimentado.
        public HashSet<int> JogadoresComCartaoVermelho { get; set; } = new();
    }
}
