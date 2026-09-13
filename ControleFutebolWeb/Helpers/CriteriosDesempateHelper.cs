using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Critérios de desempate configuráveis por competição (Competicao.CriteriosDesempate),
    /// aplicados na montagem da tabela de classificação.
    ///
    /// Pontos é sempre o primeiro critério (não é configurável) — a ordem cadastrada
    /// vale a partir do primeiro empate em pontos. Ex.: Brasileirão →
    /// Vitórias → Saldo de gols → Gols pró → Confronto direto → Vermelhos → Amarelos → Sorteio.
    /// </summary>
    public static class CriteriosDesempateHelper
    {
        public const string Vitorias = "VITORIAS";
        public const string SaldoGols = "SALDO_GOLS";
        public const string GolsPro = "GOLS_PRO";
        public const string GolsContra = "GOLS_CONTRA";
        public const string ConfrontoDireto = "CONFRONTO_DIRETO";
        public const string CartoesVermelhos = "CARTOES_VERMELHOS";
        public const string CartoesAmarelos = "CARTOES_AMARELOS";
        public const string FairPlay = "FAIR_PLAY";
        public const string Sorteio = "SORTEIO";

        /// <summary>Ordem do Campeonato Brasileiro (CBF) — usada quando a competição não define a sua.</summary>
        public static readonly IReadOnlyList<string> Padrao = new[]
        {
            Vitorias, SaldoGols, GolsPro, ConfrontoDireto, CartoesVermelhos, CartoesAmarelos, Sorteio
        };

        /// <summary>Critérios oferecidos na tela de edição da competição, na ordem de exibição.</summary>
        public static readonly IReadOnlyList<(string Codigo, string Rotulo, string Descricao)> Disponiveis = new[]
        {
            (Vitorias,         "Maior número de vitórias",   "Quem venceu mais jogos leva vantagem."),
            (SaldoGols,        "Maior saldo de gols",        "Diferença entre gols marcados e sofridos."),
            (GolsPro,          "Maior número de gols pró",   "Total de gols marcados."),
            (GolsContra,       "Menor número de gols sofridos", "Time menos vazado leva vantagem."),
            (ConfrontoDireto,  "Confronto direto",           "Válido só para empate entre duas equipes (jogos entre elas)."),
            (CartoesVermelhos, "Menos cartões vermelhos",    "Leva vantagem quem recebeu menos vermelhos."),
            (CartoesAmarelos,  "Menos cartões amarelos",     "Leva vantagem quem recebeu menos amarelos."),
            (FairPlay,         "Índice disciplinar (fair play)", "Amarelo = 1pt, vermelho = 3pt; menor pontuação leva vantagem."),
            (Sorteio,          "Sorteio público",            "Sem desempate automático — a tabela cai para ordem alfabética."),
        };

        public static string RotuloDe(string codigo)
        {
            foreach (var d in Disponiveis)
                if (d.Codigo == codigo) return d.Rotulo;
            return codigo;
        }

        /// <summary>
        /// Lê a string salva ("VITORIAS;SALDO_GOLS;...") descartando códigos inválidos e
        /// repetidos. Vazio/nulo (ou sem nenhum código válido) devolve <see cref="Padrao"/>.
        /// </summary>
        public static List<string> Parse(string? valor)
        {
            var validos = Disponiveis.Select(d => d.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var lista = (valor ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Where(validos.Contains)
                .Distinct()
                .ToList();

            return lista.Count > 0 ? lista : Padrao.ToList();
        }

        /// <summary>Serializa para gravar em Competicao.CriteriosDesempate (null quando vazio).</summary>
        public static string? Serializar(IEnumerable<string>? criterios)
        {
            var lista = Parse(string.Join(';', criterios ?? Enumerable.Empty<string>()));
            return lista.Count == 0 ? null : string.Join(';', lista);
        }

        /// <summary>
        /// Ordena a classificação por pontos e, nos empates, pelos critérios informados.
        /// Também renumera <see cref="Classificacao.Posicao"/>.
        /// </summary>
        public static List<Classificacao> Ordenar(
            IEnumerable<Classificacao> times,
            IReadOnlyList<string>? criterios = null,
            DadosDesempate? dados = null)
        {
            var ordem = (criterios == null || criterios.Count == 0) ? Padrao : criterios;
            var ctx = dados ?? new DadosDesempate();

            var lista = Resolver(times.ToList(), ordem, ctx, 0);

            for (int i = 0; i < lista.Count; i++)
                lista[i].Posicao = i + 1;

            return lista;
        }

        // nivel 0 = pontos (fixo); nivel N = criterios[N-1]. Cada nível agrupa os empatados
        // e resolve cada grupo com o critério seguinte, para que "confronto direto" só entre
        // em cena quando restarem exatamente duas equipes empatadas até ali.
        private static List<Classificacao> Resolver(
            List<Classificacao> grupo, IReadOnlyList<string> criterios, DadosDesempate dados, int nivel)
        {
            if (grupo.Count <= 1) return grupo;

            if (nivel == 0)
            {
                return grupo
                    .GroupBy(t => t.Pontos)
                    .OrderByDescending(g => g.Key)
                    .SelectMany(g => Resolver(g.ToList(), criterios, dados, 1))
                    .ToList();
            }

            int indice = nivel - 1;
            if (indice >= criterios.Count)
            {
                // Nada mais desempata (inclusive "sorteio"): ordem alfabética, estável entre requisições.
                return grupo
                    .OrderBy(t => t.Time?.Nome ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            var criterio = criterios[indice];

            if (criterio == ConfrontoDireto)
            {
                if (grupo.Count == 2)
                {
                    int cmp = CompararConfrontoDireto(grupo[0], grupo[1], dados);
                    if (cmp != 0)
                        return cmp > 0
                            ? new List<Classificacao> { grupo[0], grupo[1] }
                            : new List<Classificacao> { grupo[1], grupo[0] };
                }
                return Resolver(grupo, criterios, dados, nivel + 1);
            }

            var chave = SeletorDe(criterio, dados);
            if (chave == null) return Resolver(grupo, criterios, dados, nivel + 1);

            return grupo
                .GroupBy(chave)
                .OrderByDescending(g => g.Key)
                .SelectMany(g => Resolver(g.ToList(), criterios, dados, nivel + 1))
                .ToList();
        }

        // Seletor "maior é melhor"; critérios em que menos é melhor entram negativos.
        // null = critério sem ordenação própria (sorteio).
        private static Func<Classificacao, int>? SeletorDe(string criterio, DadosDesempate dados) => criterio switch
        {
            Vitorias         => t => t.Vitorias,
            SaldoGols        => t => t.GolsPro - t.GolsContra,
            GolsPro          => t => t.GolsPro,
            GolsContra       => t => -t.GolsContra,
            CartoesVermelhos => t => -dados.Vermelhos.GetValueOrDefault(t.TimeId),
            CartoesAmarelos  => t => -dados.Amarelos.GetValueOrDefault(t.TimeId),
            FairPlay         => t => -(dados.Amarelos.GetValueOrDefault(t.TimeId)
                                       + 3 * dados.Vermelhos.GetValueOrDefault(t.TimeId)),
            _                => null,
        };

        // > 0 se "a" leva vantagem no confronto direto; 0 se os jogos entre eles não decidem.
        private static int CompararConfrontoDireto(Classificacao a, Classificacao b, DadosDesempate dados)
        {
            int ptsA = 0, ptsB = 0, golsA = 0, golsB = 0;

            foreach (var j in dados.Jogos)
            {
                if (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue) continue;

                int ga, gb;
                if (j.TimeCasaId == a.TimeId && j.TimeVisitanteId == b.TimeId)
                { ga = j.PlacarCasa.Value; gb = j.PlacarVisitante.Value; }
                else if (j.TimeCasaId == b.TimeId && j.TimeVisitanteId == a.TimeId)
                { ga = j.PlacarVisitante.Value; gb = j.PlacarCasa.Value; }
                else continue;

                golsA += ga; golsB += gb;
                if (ga > gb) ptsA += 3;
                else if (gb > ga) ptsB += 3;
                else { ptsA++; ptsB++; }
            }

            if (ptsA != ptsB) return ptsA - ptsB;
            return (golsA - golsB);
        }
    }

    /// <summary>
    /// Dados extras necessários por alguns critérios: os jogos da fase (confronto direto)
    /// e os cartões por time. Sem eles, esses critérios são simplesmente pulados.
    /// </summary>
    public class DadosDesempate
    {
        public IReadOnlyList<Jogo> Jogos { get; init; } = Array.Empty<Jogo>();
        public IReadOnlyDictionary<int, int> Amarelos { get; init; } = new Dictionary<int, int>();
        public IReadOnlyDictionary<int, int> Vermelhos { get; init; } = new Dictionary<int, int>();

        /// <summary>
        /// Monta o contexto a partir dos jogos da fase e dos cartões deles. O time do cartão
        /// é aquele pelo qual o jogador entrou em campo naquele jogo (ver LadoJogadorHelper),
        /// mesma regra do EstatisticaTimeCalculator — passar <paramref name="escalacoes"/>
        /// evita que o fair play cobre de um clube o cartão que o jogador tomou por outro
        /// antes de ser transferido.
        /// </summary>
        public static DadosDesempate Construir(
            IEnumerable<Jogo> jogos, IEnumerable<Cartao>? cartoes = null,
            IEnumerable<Escalacao>? escalacoes = null)
        {
            var lista = jogos.ToList();
            var amarelos = new Dictionary<int, int>();
            var vermelhos = new Dictionary<int, int>();

            if (cartoes != null)
            {
                var jogoPorId = lista.GroupBy(j => j.Id).ToDictionary(g => g.Key, g => g.First());
                var atuacoes = LadoJogadorHelper.Montar(escalacoes ?? Array.Empty<Escalacao>());

                foreach (var c in cartoes)
                {
                    if (!jogoPorId.TryGetValue(c.JogoId, out var jogo)) continue;

                    var timeId = LadoJogadorHelper.TimeDoAutor(jogo, c.Jogador, atuacoes);
                    if (timeId == null) continue;

                    var destino = c.Tipo != null && c.Tipo.StartsWith("Verm", StringComparison.OrdinalIgnoreCase)
                        ? vermelhos : amarelos;
                    destino[timeId.Value] = destino.GetValueOrDefault(timeId.Value) + 1;
                }
            }

            return new DadosDesempate { Jogos = lista, Amarelos = amarelos, Vermelhos = vermelhos };
        }
    }
}
