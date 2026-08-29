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

        // Gols/assistências na competição deste jogo — linha "Competição" do tooltip.
        public Dictionary<int, int> GolsPorJogador { get; set; } = new();
        public Dictionary<int, int> AssistsPorJogador { get; set; } = new();

        // Gols/assistências na temporada deste jogo (todas as competições do mesmo
        // ano) — linha "Temporada" do tooltip de info.
        public Dictionary<int, int> GolsTemporadaPorJogador { get; set; } = new();
        public Dictionary<int, int> AssistsTemporadaPorJogador { get; set; } = new();

        // Médias por jogo das estatísticas importadas (mesmas fórmulas de
        // /Jogadores/Estatisticas), por jogador — exibidas no tooltip de info.
        public Dictionary<int, MediasPorJogo> MediasPorJogador { get; set; } = new();

        // Clube do jogador na temporada anterior, quando ele não jogou pelo clube
        // atual naquela temporada (reforço) — linha "Vinha do" do tooltip.
        public Dictionary<int, TimeAnteriorJogador> TimeAnteriorPorJogador { get; set; } = new();

        // Jogos como titular na competição deste jogo (linha "Competição" do
        // tooltip) e na temporada (linha "Temporada").
        public Dictionary<int, int> TitularPorJogador { get; set; } = new();
        public Dictionary<int, int> TitularTemporadaPorJogador { get; set; } = new();

        // Temporada usada pelos números do tooltip (rótulo, igual a Jogo.Temporada);
        // 0 = todas as temporadas. Começa na temporada do jogo e o usuário troca no
        // seletor "ℹ Tooltip" da barra do campo (Jogos/TooltipTemporada).
        public int TemporadaTooltip { get; set; }

        // Opções do seletor: temporadas com estatística dos jogadores desta tela.
        public List<int> TemporadasTooltip { get; set; } = new();

        // Origem da escalação de cada lado, para o selo da barra de status: qual
        // fonte importou as linhas compartilhadas (UsuarioId == null) daquele time —
        // ver FonteEscalacao. Null = ninguém importou, e a tela caiu na última
        // escalação do time, que pode não ser a do jogo; o analista precisa saber
        // disso antes de confiar no que está em campo.
        public string? FonteEscalacaoCasa { get; set; }
        public string? FonteEscalacaoVisitante { get; set; }

        public bool EscalacaoApiCasa => FonteEscalacaoCasa != null;
        public bool EscalacaoApiVisitante => FonteEscalacaoVisitante != null;

        // A ESPN é uma fonte alternativa para esta partida (a competição tem slug
        // mapeado em ligas-espn.json) E falta a escalação de pelo menos um lado na
        // importação. É o que libera o botão "Buscar ESPN" da barra de ações: sem
        // slug não adianta tentar, e com os dois lados importados não há o que buscar.
        public bool EspnDisponivel { get; set; }

        // O botão vai buscar só o que complementa: a escalação dos dois lados já
        // está importada e o que falta são as estatísticas, os lances (gols, cartões,
        // substituições) ou os dois. Muda o rótulo e o texto de confirmação, para o
        // botão não prometer mexer numa escalação que ele não vai tocar.
        public bool EspnSoComplementos { get; set; }

        // Escalação importada existe, mas a que está na tela (fase INICIAL) já não
        // bate com ela — o usuário mexeu. Só é calculado na fase INICIAL: na FINAL a
        // diferença é o esperado (substituições).
        public bool EscalacaoEditada { get; set; }

        // Jogo analisado pelo usuário atual (existência de JogoAnalisadoUsuario).
        public bool Analisado { get; set; }

        // Observações categorizadas por tag (mandante/visitante/competição/jogador/marco).
        public List<ObservacaoJogoTag> ObservacoesTag { get; set; } = new();

        // Jogadores escalados neste jogo (qualquer fase), para o seletor da tag "Jogador".
        public List<Jogador> JogadoresEscalados { get; set; } = new();

        // Jogadores expulsos (cartão vermelho) neste jogo — o botão deles no campo
        // fica cinza e não pode mais ser arrastado/movimentado.
        public HashSet<int> JogadoresComCartaoVermelho { get; set; } = new();

        // Jogadores advertidos com cartão amarelo neste jogo (qualquer minuto) —
        // mostra um ícone de cartão amarelo no botão do jogador em campo/banco.
        public HashSet<int> JogadoresComCartaoAmarelo { get; set; } = new();

        // Jogadores marcados como capitão nas estatísticas importadas deste jogo —
        // mostra a braçadeira "C" no botão do jogador em campo/banco.
        public HashSet<int> JogadoresCapitao { get; set; } = new();

        // Craque da partida: quem tirou a maior nota do jogo na régua deste usuário
        // (ver CraqueDaPartida) — ganha a coroa no botão em campo/banco. Null quando
        // ninguém foi avaliado e o jogo também não tem estatística importada.
        public int? CraqueJogadorId { get; set; }

        // Nota que elegeu o craque, para o tooltip da coroa.
        public double CraqueNota { get; set; }
    }

    /// <summary>
    /// Números do tooltip de info do jogador em /Jogos/Analisar, todos no mesmo
    /// recorte de temporada. Serializado direto como JSON por
    /// JogosController.TooltipTemporada quando o usuário troca a temporada no
    /// seletor (por isso os nomes curtos: o JS lê estas chaves em camelCase).
    /// </summary>
    public class TooltipJogadorDados
    {
        // Rótulo da temporada aplicada (0 = todas).
        public int Temporada { get; set; }

        public Dictionary<int, int> Gols { get; set; } = new();
        public Dictionary<int, int> Assists { get; set; } = new();
        public Dictionary<int, int> TitularCompeticao { get; set; } = new();

        public Dictionary<int, int> GolsTemporada { get; set; } = new();
        public Dictionary<int, int> AssistsTemporada { get; set; } = new();
        public Dictionary<int, int> TitularTemporada { get; set; } = new();

        public Dictionary<int, MediasPorJogo> Medias { get; set; } = new();

        public Dictionary<int, TimeAnteriorJogador> TimeAnterior { get; set; } = new();
    }

    /// <summary>
    /// Clube pelo qual o jogador atuou na temporada anterior à do tooltip, quando
    /// é diferente do clube atual dele. Serve para marcar reforços ("chegou agora"):
    /// o tooltip mostra de onde ele veio.
    /// </summary>
    public class TimeAnteriorJogador
    {
        public string Nome { get; set; } = string.Empty;

        // Já pronto para o <img> (passa pelo MediaProxy); vazio quando o time não
        // tem escudo cadastrado.
        public string Escudo { get; set; } = string.Empty;

        // Rótulo da temporada anterior como aparece na tela: "2024/25" quando a
        // competição cruza o ano civil, "2024" quando é de ano civil.
        public string Temporada { get; set; } = string.Empty;

        // Jogos dele por aquele clube na temporada anterior (aparece como "12 jogos").
        public int Jogos { get; set; }
    }
}
