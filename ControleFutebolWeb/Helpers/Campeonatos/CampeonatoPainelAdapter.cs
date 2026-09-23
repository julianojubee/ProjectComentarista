using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Helpers.Campeonatos
{
    /// <summary>
    /// Converte um campeonato próprio em Competicao/Time/Jogo/Cartao EM MEMÓRIA
    /// para reaproveitar o CompeticaoPainelBuilder (tabela, grupos, chaveamento e
    /// critérios de desempate). Nada do que sai daqui pode ir para o
    /// FutebolContext: os ids são de participante/partida, não de Times/Jogos.
    ///
    /// Mapeamento:
    ///   - participante → Time com Id = CampeonatoParticipante.Id (a tabela é de
    ///     participantes, não de times);
    ///   - partida → Jogo com Id = PartidaCampeonato.Id;
    ///   - vaga do mata-mata ainda sem dono → Time "A definir" com id NEGATIVO,
    ///     igual na ida e na volta, para o builder agrupar o confronto;
    ///   - cartão → Cartao com um Jogador de mentira cujo TimeId é o participante,
    ///     que é o que o LadoJogadorHelper usa para saber de que lado ele é.
    ///
    /// Espera o campeonato carregado com Fases, Participantes (+ TimeProprio e
    /// Time) e Partidas (+ Eventos).
    /// </summary>
    public static class CampeonatoPainelAdapter
    {
        public const string NomeVagaEmAberto = "A definir";

        public sealed class Conversao
        {
            public Competicao Competicao { get; init; } = null!;
            public Dictionary<int, Time> TimesPorParticipante { get; init; } = new();
            public List<Jogo> Jogos { get; init; } = new();
            // Jogo.Id (= PartidaCampeonato.Id) → FaseId. O Jogo não tem onde guardar a fase.
            public Dictionary<int, int?> FasePorPartida { get; init; } = new();
            public List<Cartao> Cartoes { get; init; } = new();
            public IReadOnlyList<string> Criterios { get; init; } = Array.Empty<string>();
        }

        public static Conversao Converter(Campeonato campeonato)
        {
            var competicao = new Competicao
            {
                Id = campeonato.Id,
                Nome = campeonato.Nome,
                Regiao = "",
                Tipo = campeonato.Tipo,
                LogoUrl = campeonato.LogoUrl,
                CriteriosDesempate = campeonato.CriteriosDesempate
            };

            var times = campeonato.Participantes.ToDictionary(p => p.Id, TimeDe);
            var vagas = new Dictionary<(int?, string?, int?), (Time A, Time B)>();

            Time Vaga((int?, string?, int?) chave, bool primeira)
            {
                if (!vagas.TryGetValue(chave, out var par))
                {
                    var id = -(vagas.Count * 2 + 1);
                    par = (VagaEmAberto(id), VagaEmAberto(id - 1));
                    vagas[chave] = par;
                }
                return primeira ? par.A : par.B;
            }

            var jogos = new List<Jogo>();
            // Ordem de rodada: o builder monta ida/volta pela Data, e com Data vazia a
            // ordenação dele (estável) mantém esta.
            foreach (var partida in campeonato.Partidas.OrderBy(p => p.Rodada).ThenBy(p => p.Id))
            {
                var chave = (partida.FaseId, partida.Grupo, partida.ChaveOrdem);
                Time? casa = partida.ParticipanteCasaId is int c && times.TryGetValue(c, out var tc) ? tc : null;
                Time? visitante = partida.ParticipanteVisitanteId is int v && times.TryGetValue(v, out var tv) ? tv : null;

                // Vaga única em aberto fica sempre com a primeira; as duas em aberto,
                // uma para cada lado. Assim ida e volta mostram o mesmo par.
                if (casa == null) casa = Vaga(chave, true);
                if (visitante == null) visitante = Vaga(chave, casa.Id > 0);

                jogos.Add(new Jogo
                {
                    Id = partida.Id,
                    Rodada = partida.Rodada,
                    Data = partida.Data,
                    Temporada = campeonato.CriadoEm.Year,
                    TimeCasaId = casa.Id,
                    TimeCasa = casa,
                    TimeVisitanteId = visitante.Id,
                    TimeVisitante = visitante,
                    PlacarCasa = partida.PlacarCasa,
                    PlacarVisitante = partida.PlacarVisitante,
                    PenaltisCasa = partida.PenaltisCasa,
                    PenaltisVisitante = partida.PenaltisVisitante,
                    Grupo = partida.Grupo,
                    Estadio = partida.Local,
                    Observacoes = partida.Observacoes,
                    CompeticaoId = competicao.Id,
                    Competicao = competicao,
                    Gols = new List<Gol>(),
                    Escalacoes = new List<Escalacao>()
                });
            }

            var cartoes = campeonato.Partidas
                .SelectMany(p => p.Eventos)
                .Where(e => e.Tipo is TipoEventoPartida.CartaoAmarelo or TipoEventoPartida.CartaoVermelho)
                .Select(e => new Cartao
                {
                    Id = e.Id,
                    JogoId = e.PartidaId,
                    JogadorId = -e.Id,
                    Jogador = new Jogador { Id = -e.Id, Nome = e.NomeSnapshot, TimeId = e.ParticipanteId },
                    Minuto = e.Minuto ?? 0,
                    Tipo = e.Tipo == TipoEventoPartida.CartaoVermelho ? "Vermelho" : "Amarelo"
                })
                .ToList();

            return new Conversao
            {
                Competicao = competicao,
                TimesPorParticipante = times,
                Jogos = jogos,
                FasePorPartida = campeonato.Partidas.ToDictionary(p => p.Id, p => p.FaseId),
                Cartoes = cartoes,
                Criterios = CriteriosDesempateHelper.Parse(campeonato.CriteriosDesempate)
            };
        }

        /// <summary>
        /// Painel do campeonato no mesmo formato da tela de competições. Com uma fase
        /// só (ou nenhuma), o resultado vem nas coleções soltas; com duas ou mais,
        /// uma entrada em Fases por fase.
        /// </summary>
        public static CompeticaoPainelBuilder.Painel MontarPainel(Campeonato campeonato)
        {
            var conv = Converter(campeonato);
            var fases = campeonato.Fases.OrderBy(f => f.Ordem).ToList();

            if (fases.Count == 0)
                return CompeticaoPainelBuilder.Montar(conv.Competicao, new List<CompeticaoFase>(),
                    conv.Jogos, conv.Criterios, conv.Cartoes);

            var detalhes = fases.Select(f => MontarFase(conv, f)).ToList();

            if (detalhes.Count == 1)
            {
                return new CompeticaoPainelBuilder.Painel
                {
                    Classificacao = detalhes[0].Classificacao,
                    Grupos = detalhes[0].Grupos,
                    FasesMataMata = detalhes[0].FasesMataMata
                };
            }

            return new CompeticaoPainelBuilder.Painel { Fases = detalhes };
        }

        /// <summary>
        /// Uma fase pelo tipo dela. Diferente das competições reais, a partida sabe a
        /// fase (FaseId), então não passa pela heurística do FaseJogoClassifier.
        /// </summary>
        public static FaseDetalheViewModel MontarFase(Conversao conv, CampeonatoFase fase)
        {
            var jogosFase = conv.Jogos
                .Where(j => conv.FasePorPartida.GetValueOrDefault(j.Id) == fase.Id)
                .ToList();
            var realizados = jogosFase.Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue).ToList();

            var detalhe = new FaseDetalheViewModel
            {
                Fase = new CompeticaoFase
                {
                    Id = fase.Id,
                    CompeticaoId = conv.Competicao.Id,
                    Nome = fase.Nome,
                    Tipo = fase.Tipo,
                    Ordem = fase.Ordem
                }
            };

            switch (fase.Tipo)
            {
                case "PONTOS_CORRIDOS":
                    detalhe.Classificacao = TabelaComQuemNaoJogou(realizados, jogosFase, conv);
                    break;
                case "GRUPOS":
                    detalhe.Grupos = CompeticaoPainelBuilder.MontarGrupos(realizados, jogosFase, conv.Criterios, conv.Cartoes);
                    break;
                default:
                    detalhe.FasesMataMata = CompeticaoPainelBuilder.MontarMataMata(jogosFase);
                    break;
            }

            return detalhe;
        }

        // A tabela do builder só tem quem já jogou; no campeonato recém-gerado a tela
        // precisa mostrar todo mundo zerado, como MontarGrupos já faz com os grupos.
        private static List<Classificacao> TabelaComQuemNaoJogou(List<Jogo> realizados, List<Jogo> todos, Conversao conv)
        {
            var tabela = CompeticaoPainelBuilder.CalcularTabela(realizados, conv.Criterios, conv.Cartoes);
            var jaNaTabela = tabela.Select(c => c.TimeId).ToHashSet();

            var semJogo = todos
                .SelectMany(j => new[] { j.TimeCasa, j.TimeVisitante })
                .Where(t => t.Id > 0 && !jaNaTabela.Contains(t.Id))
                .DistinctBy(t => t.Id)
                .OrderBy(t => t.Nome, StringComparer.CurrentCulture)
                .Select(t => new Classificacao { TimeId = t.Id, Time = t });

            var linhas = tabela.Concat(semJogo).ToList();
            for (var i = 0; i < linhas.Count; i++) linhas[i].Posicao = i + 1;
            return linhas;
        }

        private static Time TimeDe(CampeonatoParticipante p) => new()
        {
            Id = p.Id,
            Nome = p.NomeExibicao,
            Cidade = "",
            EscudoUrl = p.EscudoUrlSnapshot ?? p.TimeProprio?.EscudoUrl ?? p.Time?.EscudoUrl,
            CorPrincipal = p.TimeProprio?.CorPrincipal ?? p.Time?.CorPrincipal,
            CorSecundaria = p.TimeProprio?.CorSecundaria ?? p.Time?.CorSecundaria
        };

        private static Time VagaEmAberto(int id) => new() { Id = id, Nome = NomeVagaEmAberto, Cidade = "" };
    }
}
