using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Helpers
{
    // Monta o painel de uma competição/temporada RESPEITANDO O FORMATO dela:
    // pontos corridos vira tabela, fase de grupos vira uma tabela por grupo,
    // eliminatória vira chaveamento — e uma competição com fases declaradas
    // (CompeticaoFase) monta cada fase pelo tipo dela.
    //
    // Saiu de dentro do CompeticoesController quando a área pública de creators
    // (/creators/tabelas) passou a precisar da mesma leitura: duplicar o if/else
    // de formato garantiria que as duas telas divergissem na primeira mudança.
    // O controller continua dono das consultas ao banco; aqui só entra o que já
    // foi carregado.
    public static class CompeticaoPainelBuilder
    {
        // Resultado da montagem. Fases preenchido = a competição declara fases e
        // a tela deve mostrar uma aba por fase; caso contrário valem as três
        // coleções soltas, conforme o Tipo da competição.
        public sealed class Painel
        {
            public List<Classificacao> Classificacao { get; set; } = new();
            public List<GrupoViewModel> Grupos { get; set; } = new();
            public List<FaseMataMataViewModel> FasesMataMata { get; set; } = new();
            public List<FaseDetalheViewModel> Fases { get; set; } = new();
        }

        /// <param name="jogosDaTemporada">Todos os jogos da competição na temporada, terminados ou não.</param>
        /// <param name="fasesDeclaradas">Fases cadastradas em Competicoes/Edit, em ordem; vazio = fase única guiada pelo Tipo.</param>
        public static Painel Montar(
            Competicao competicao,
            List<CompeticaoFase> fasesDeclaradas,
            List<Jogo> jogosDaTemporada,
            IReadOnlyList<string>? criterios = null,
            IEnumerable<Cartao>? cartoes = null,
            IEnumerable<Escalacao>? escalacoes = null)
        {
            var painel = new Painel();
            var jogosRealizados = jogosDaTemporada
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            if (fasesDeclaradas.Any())
            {
                // Fases declaradas pelo usuário (ex.: pontos corridos + playoffs):
                // distribui os jogos entre elas e monta a visualização do tipo de cada uma.
                var jogosPorFase = FaseJogoClassifier.DistribuirPorFases(fasesDeclaradas, jogosDaTemporada);

                painel.Fases = fasesDeclaradas.Select(fase =>
                {
                    var jogosFase = jogosPorFase[fase.Id];
                    var realizadosFase = jogosFase
                        .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                        .ToList();

                    return new FaseDetalheViewModel
                    {
                        Fase = fase,
                        Classificacao = fase.Tipo == "PONTOS_CORRIDOS" ? CalcularTabela(realizadosFase, criterios, cartoes, escalacoes) : new(),
                        Grupos = fase.Tipo == "GRUPOS" ? MontarGrupos(realizadosFase, jogosFase, criterios, cartoes, escalacoes) : new(),
                        FasesMataMata = fase.Tipo is "MATA_MATA" or "JOGO_UNICO"
                            ? MontarMataMata(jogosFase, fase.Tipo == "JOGO_UNICO")
                            : new(),
                    };
                }).ToList();

                // Tabela acumulada de todas as fases não-eliminatórias (a "tabela anual" do
                // Argentino: Apertura + Clausura somados definem o Campeão da Liga e as vagas
                // continentais). Só faz sentido com duas ou mais fases somáveis — com uma só,
                // a tabela geral seria a cópia da aba da própria fase.
                var fasesSomaveis = fasesDeclaradas
                    .Where(f => !FaseJogoClassifier.EhEliminatoria(f.Tipo))
                    .ToList();

                if (fasesSomaveis.Count >= 2)
                {
                    var jogosSomaveis = fasesSomaveis
                        .SelectMany(f => jogosPorFase[f.Id])
                        .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                        .ToList();

                    painel.Classificacao = CalcularTabela(jogosSomaveis, criterios, cartoes, escalacoes);
                }
            }
            else if (competicao.Tipo == "MATA_MATA" || competicao.Tipo == "JOGO_UNICO")
            {
                // JOGO_UNICO: a competição é decidida numa partida só (Supercopa da UEFA,
                // Recopa...). O confronto é montado como um chaveamento de uma chave só e
                // o vencedor da partida é anunciado como campeão, não como "classificado".
                painel.FasesMataMata = MontarMataMata(jogosDaTemporada, competicao.Tipo == "JOGO_UNICO");
            }
            else if (competicao.Tipo == "GRUPOS")
            {
                // Competições "GRUPOS" às vezes têm, na mesma temporada, uma fase eliminatória
                // depois da fase de grupos (ex.: Sul-Americana → grupos + oitavas/quartas/semi/final).
                // A api-football rotula essas fases com nomes que não começam com "Group"/"Grupo"
                // (ex.: "Round of 16", "Quarterfinals", "Qualification Round 1"), então separamos
                // esses jogos para montar o chaveamento em vez de tratá-los como um "grupo" de pontos corridos.
                var jogosFaseGrupos = jogosDaTemporada.Where(j => EhNomeDeGrupo(j.Grupo)).ToList();
                var jogosFaseGruposRealizados = jogosRealizados.Where(j => EhNomeDeGrupo(j.Grupo)).ToList();
                var jogosFaseEliminatoria = jogosDaTemporada
                    .Where(j => !string.IsNullOrEmpty(j.Grupo) && !EhNomeDeGrupo(j.Grupo))
                    .ToList();

                painel.Grupos = MontarGrupos(jogosFaseGruposRealizados, jogosFaseGrupos, criterios, cartoes, escalacoes);
                painel.FasesMataMata = MontarMataMata(jogosFaseEliminatoria);
            }
            else
            {
                // Pontos corridos: jogos de playoff/eliminatória (round tipo "Quarterfinals")
                // não entram na tabela — viram um chaveamento à parte, como já ocorre em GRUPOS.
                var jogosEliminatorios = jogosDaTemporada
                    .Where(j => FaseJogoClassifier.Classificar(j.Grupo) == FaseCategoria.MataMata)
                    .ToList();

                painel.Classificacao = CalcularTabela(jogosRealizados
                    .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                    .ToList(), criterios, cartoes, escalacoes);
                painel.FasesMataMata = jogosEliminatorios.Any() ? MontarMataMata(jogosEliminatorios) : new();
            }

            return painel;
        }

        public static List<Classificacao> CalcularTabela(
            ICollection<Jogo> jogos,
            IReadOnlyList<string>? criterios = null,
            IEnumerable<Cartao>? cartoes = null,
            IEnumerable<Escalacao>? escalacoes = null)
        {
            var tabela = new Dictionary<int, Classificacao>();

            foreach (var jogo in jogos)
            {
                // Garantir que os times existam na tabela
                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa };

                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante };
                var casa = tabela[jogo.TimeCasaId];
                var visitante = tabela[jogo.TimeVisitanteId];

                casa.Jogos++;
                visitante.Jogos++;

                casa.GolsPro += (int)jogo.PlacarCasa;
                casa.GolsContra += (int)jogo.PlacarVisitante;
                visitante.GolsPro += (int)jogo.PlacarVisitante;
                visitante.GolsContra += (int)jogo.PlacarCasa;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                {
                    casa.Vitorias++;
                    casa.Pontos += 3;
                    visitante.Derrotas++;
                }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                {
                    visitante.Vitorias++;
                    visitante.Pontos += 3;
                    casa.Derrotas++;
                }
                else
                {
                    casa.Empates++;
                    visitante.Empates++;
                    casa.Pontos++;
                    visitante.Pontos++;
                }
            }

            foreach (var item in tabela.Values)
            {
                item.Saldo = item.GolsPro - item.GolsContra;
            }

            // Critérios de desempate cadastrados na competição (Competicoes/Edit).
            return CriteriosDesempateHelper.Ordenar(
                tabela.Values,
                criterios ?? CriteriosDesempateHelper.Padrao,
                DadosDesempate.Construir(jogos, cartoes, escalacoes));
        }

        // Distingue uma "fase de grupos" real (pontos corridos, rotulada "Group X"/"Grupo X")
        // de uma fase eliminatória (mata-mata) que pode existir na mesma competição/temporada
        // — a api-football nomeia fases eliminatórias como "Round of 16", "Quarterfinals",
        // "Qualification Round 1" etc., que não devem virar uma tabela de pontos corridos.
        public static bool EhNomeDeGrupo(string? nomeGrupo)
            => FaseJogoClassifier.Classificar(nomeGrupo) == FaseCategoria.Grupos;

        /// <summary>
        /// Monta a tabela de cada grupo da fase.
        /// </summary>
        /// <param name="realizados">
        /// Jogos já terminados — são eles que dão pontos, saldo e a ordem da tabela.
        /// </param>
        /// <param name="todos">
        /// Todos os jogos da fase, terminados ou não. Servem para saber QUAIS grupos
        /// existem e QUEM está em cada um antes de a bola rolar: no sorteio de um
        /// Mundial a tabela inteira já é conhecida, e a tela mostrava "Sem dados de
        /// grupos ainda" até o fim da primeira rodada — justamente quando o analista
        /// quer conferir a chave que caiu para a seleção que vai cobrir.
        /// </param>
        public static List<GrupoViewModel> MontarGrupos(
            ICollection<Jogo> realizados,
            ICollection<Jogo> todos,
            IReadOnlyList<string>? criterios = null,
            IEnumerable<Cartao>? cartoes = null,
            IEnumerable<Escalacao>? escalacoes = null)
        {
            var grupos = new List<GrupoViewModel>();

            // Um jogo eliminatório que tenha caído nesta fase (fase declarada pelo
            // usuário com pattern largo) não vira "grupo": ele já é mostrado no
            // chaveamento e aqui só criaria uma tabela de uma linha chamada "Final".
            var nomesGrupos = todos
                .Where(j => !string.IsNullOrEmpty(j.Grupo) &&
                            FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(n => n, StringComparer.CurrentCulture)
                .ToList();

            foreach (var nome in nomesGrupos)
            {
                var classificacao = CalcularTabela(
                    realizados.Where(j => j.Grupo == nome).ToList(), criterios, cartoes, escalacoes);

                // Quem ainda não jogou entra zerado, em ordem alfabética, depois de quem
                // já pontuou. Sem isto o grupo apareceria pela metade enquanto a rodada
                // não fecha, e no sorteio não apareceria de jeito nenhum.
                var jaNaTabela = classificacao.Select(c => c.TimeId).ToHashSet();

                var semJogo = todos
                    .Where(j => j.Grupo == nome)
                    .SelectMany(j => new[]
                    {
                        (Id: j.TimeCasaId, Time: j.TimeCasa),
                        (Id: j.TimeVisitanteId, Time: j.TimeVisitante),
                    })
                    .Where(t => t.Id > 0 && !jaNaTabela.Contains(t.Id))
                    .GroupBy(t => t.Id)
                    .Select(g => new Classificacao { TimeId = g.Key, Time = g.First().Time })
                    .OrderBy(c => c.Time?.Nome, StringComparer.CurrentCulture)
                    .ToList();

                var linhas = classificacao.Concat(semJogo).ToList();
                for (var i = 0; i < linhas.Count; i++)
                    linhas[i].Posicao = i + 1;

                grupos.Add(new GrupoViewModel
                {
                    Nome = nome,
                    Times = linhas
                });
            }

            return grupos;
        }

        // decideTitulo = a fase decide o título (competição/fase de JOGO_UNICO):
        // o vencedor é anunciado como campeão em vez de "classificado".
        public static List<FaseMataMataViewModel> MontarMataMata(List<Jogo> jogos, bool decideTitulo = false)
        {
            // Ordena fases conhecidas; fases desconhecidas vão para o final
            var ordemFases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Qualification Round 1"] = -3, ["Preliminary Round"] = -3,
                ["Qualification Round 2"] = -2,
                ["Qualification Round 3"] = -1,
                ["32avos"]      = 1, ["Rodada 1"]    = 1, ["Round of 32"] = 1,
                ["16avos"]      = 2, ["Rodada 2"]    = 2, ["Round of 16"] = 2,
                ["Oitavas"]     = 3, ["Rodada 3"]    = 3,
                ["Quartas"]     = 4, ["Rodada 4"]    = 4, ["Quarterfinals"] = 4,
                ["Semifinal"]   = 5, ["Semi"]        = 5, ["Semifinals"]    = 5,
                ["Final"]       = 6,
                ["3rd Place Final"] = 7,
            };

            // Jogo sem round vindo da API: numa competição de jogo único a partida É a final.
            var nomePadrao = decideTitulo ? "Final" : "Fase Única";
            string NomeFaseDe(Jogo j) => string.IsNullOrWhiteSpace(j.Grupo) ? nomePadrao : j.Grupo;

            var faseNomes = jogos
                .Select(NomeFaseDe)
                .Distinct()
                .OrderBy(n => ordemFases.TryGetValue(n, out var o) ? o : 99)
                .ToList();

            var resultado = new List<FaseMataMataViewModel>();

            foreach (var fase in faseNomes)
            {
                var jogosFase = jogos.Where(j => NomeFaseDe(j) == fase).ToList();

                // Agrupa pares de times (ida e volta) pelo par de IDs ordenado
                var pares = jogosFase
                    .GroupBy(j => string.Join("-",
                        new[] { j.TimeCasaId, j.TimeVisitanteId }.OrderBy(x => x)))
                    .ToList();

                var confrontos = new List<ConfrontoViewModel>();
                foreach (var par in pares)
                {
                    var lista = par.OrderBy(j => j.Data).ToList();
                    var ida   = lista.FirstOrDefault();
                    var volta = lista.Count > 1 ? lista[1] : null;

                    confrontos.Add(new ConfrontoViewModel
                    {
                        JogoIda   = ida,
                        JogoVolta = volta,
                        TimeA     = ida?.TimeCasa,
                        TimeB     = ida?.TimeVisitante,
                    });
                }

                resultado.Add(new FaseMataMataViewModel
                {
                    Nome      = fase,
                    Ordem     = ordemFases.TryGetValue(fase, out var ord) ? ord : 99,
                    Confrontos = confrontos,
                    DecideTitulo = decideTitulo,
                });
            }

            return resultado;
        }

    }
}
