using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// "Quem joga parecido com X": aba Similares do Scout (/Relatorios/Scout).
    ///
    /// Cada jogador vira um vetor de métricas por 90 minutos (mais duas taxas de
    /// acerto), e cada métrica vira percentil dentro do grupo da mesma função. A
    /// semelhança é 100 menos a distância quadrática média entre os percentis dos
    /// dois — percentil, e não valor cru, para que "0,3 desarme a mais" e "15 passes
    /// a mais" pesem o mesmo quando separam o grupo na mesma medida.
    ///
    /// Respeita a cobertura das fontes (<see cref="FonteEstatistica"/>): uma métrica
    /// só soma minutos das linhas cuja fonte a publica. Sem isso um volante com jogos
    /// importados da ESPN teria desarmes "zero" e pareceria um meia que não marca.
    /// Métrica sem amostra suficiente fica fora do perfil — e fora da comparação —
    /// em vez de virar zero.
    /// </summary>
    public static class SimilaridadeJogadorHelper
    {
        /// <param name="Cobertura">Id de métrica do rating, perguntado a FonteEstatistica.Cobre.</param>
        /// <param name="Denominador">Null = por 90 minutos; senão, taxa de acerto (numerador / denominador).</param>
        /// <param name="MinimoDenominador">Para taxa: abaixo disso a amostra é ruído.</param>
        /// <param name="LinhaValida">Filtro extra por linha (ex.: import antigo sem passes certos).</param>
        public sealed record Metrica(
            string Id, string Rotulo, string Cobertura,
            Func<EstatisticaJogador, int> Numerador,
            Func<EstatisticaJogador, int>? Denominador = null,
            int MinimoDenominador = 0,
            Func<EstatisticaJogador, bool>? LinhaValida = null)
        {
            public bool EhTaxa => Denominador != null;
        }

        /// <summary>
        /// Minutos cobertos que uma métrica por 90 exige. Mesmo piso do "por 90 min"
        /// da tabela do Scout: abaixo disso uma ação isolada infla o número.
        /// </summary>
        public const int MinMinutosMetrica = 270;

        // Zero passe certo com passes tentados é import anterior à coluna PassesCertos,
        // não passe ruim (ver EstatisticaJogador.PrecisaoPasses).
        private static bool TemPassesCertos(EstatisticaJogador e) => e.PassesCertos > 0;

        public static readonly IReadOnlyList<Metrica> MetricasLinha = new Metrica[]
        {
            new("gols",              "Gols /90",               "evento_gol",         e => e.Gols),
            new("assistencias",      "Assistências /90",       "evento_assistencia", e => e.Assistencias),
            new("finalizacoes",      "Finalizações /90",       "finalizacoes",       e => e.FinalizacoesTotal),
            new("finalizacoes_alvo", "Finalizações no alvo /90", "finalizacoes_alvo", e => e.FinalizacoesNoGol),
            new("passes",            "Passes /90",             "passes",             e => e.PassesTotal),
            new("passes_precisao",   "% de passes certos",     "passes_precisao",    e => e.PassesCertos, e => e.PassesTotal, 50, TemPassesCertos),
            new("passes_chave",      "Passes-chave /90",       "passes_chave",       e => e.PassesChave),
            new("dribles",           "Dribles certos /90",     "dribles",            e => e.DriblesCertos),
            new("desarmes",          "Desarmes /90",           "desarmes",           e => e.Desarmes),
            new("interceptacoes",    "Interceptações /90",     "interceptacoes",     e => e.Interceptacoes),
            new("bloqueios",         "Bloqueios /90",          "bloqueios",          e => e.Bloqueios),
            new("duelos",            "Duelos ganhos /90",      "duelos",             e => e.DuelosVencidos),
            new("duelos_precisao",   "% de duelos ganhos",     "duelos_precisao",    e => e.DuelosVencidos, e => e.DuelosTotal, 30),
            new("faltas_sofridas",   "Faltas sofridas /90",    "faltas_sofridas",    e => e.FaltasSofridas),
        };

        public static readonly IReadOnlyList<Metrica> MetricasGoleiro = new Metrica[]
        {
            new("defesas",           "Defesas /90",            "defesas",            e => e.Defesas),
            new("gols_sofridos",     "Gols sofridos /90",      "gols_sofridos",      e => e.GolsSofridos),
            new("defesas_precisao",  "% de chutes defendidos", "defesas",            e => e.Defesas, e => e.Defesas + e.GolsSofridos, 15),
            new("passes",            "Passes /90",             "passes",             e => e.PassesTotal),
            new("passes_precisao",   "% de passes certos",     "passes_precisao",    e => e.PassesCertos, e => e.PassesTotal, 50, TemPassesCertos),
        };

        public static IReadOnlyList<Metrica> MetricasDaFuncao(string funcao) =>
            funcao == "GOL" ? MetricasGoleiro : MetricasLinha;

        /// <summary>
        /// Valor de cada métrica com amostra suficiente. Por 90: soma do numerador
        /// sobre os minutos das linhas cobertas, vezes 90. Taxa: percentual (0–100).
        /// </summary>
        public static Dictionary<string, double> Perfil(
            IEnumerable<EstatisticaJogador> linhas, IReadOnlyList<Metrica> metricas)
        {
            var lista = linhas.Where(e => (e.Minutos ?? 0) > 0).ToList();
            var perfil = new Dictionary<string, double>();

            foreach (var m in metricas)
            {
                var cobertas = lista.Where(e => FonteEstatistica.Cobre(e.Fonte, m.Cobertura)
                                             && (m.LinhaValida == null || m.LinhaValida(e)));
                if (m.Denominador is { } den)
                {
                    long soma = 0, total = 0;
                    foreach (var e in cobertas) { soma += m.Numerador(e); total += den(e); }
                    if (total < Math.Max(1, m.MinimoDenominador)) continue;
                    perfil[m.Id] = 100.0 * soma / total;
                }
                else
                {
                    long soma = 0, minutos = 0;
                    foreach (var e in cobertas) { soma += m.Numerador(e); minutos += e.Minutos!.Value; }
                    if (minutos < MinMinutosMetrica) continue;
                    perfil[m.Id] = soma * 90.0 / minutos;
                }
            }

            return perfil;
        }

        /// <summary>
        /// Percentil (0–100) de <paramref name="valor"/> numa amostra já ordenada:
        /// fração abaixo dele mais metade dos empates. Empate no meio é o que faz uma
        /// métrica em que metade do grupo tem zero não jogar todos para o percentil 0.
        /// </summary>
        public static double Percentil(double valor, IReadOnlyList<double> ordenados)
        {
            if (ordenados.Count == 0) return 50;
            int abaixo = 0, iguais = 0;
            foreach (var v in ordenados)
            {
                if (Math.Abs(v - valor) < 1e-9) iguais++;
                else if (v < valor) abaixo++;
            }
            return 100.0 * (abaixo + iguais / 2.0) / ordenados.Count;
        }

        /// <summary>Percentil de cada jogador em cada métrica que ele tem, dentro do grupo.</summary>
        public static Dictionary<int, Dictionary<string, double>> Percentis(
            IReadOnlyDictionary<int, Dictionary<string, double>> perfis, IReadOnlyList<Metrica> metricas)
        {
            var resultado = perfis.Keys.ToDictionary(id => id, _ => new Dictionary<string, double>());
            foreach (var m in metricas)
            {
                var ordenados = perfis.Values
                    .Where(p => p.ContainsKey(m.Id))
                    .Select(p => p[m.Id])
                    .OrderBy(v => v)
                    .ToList();
                foreach (var (id, p) in perfis)
                    if (p.TryGetValue(m.Id, out var v))
                        resultado[id][m.Id] = Percentil(v, ordenados);
            }
            return resultado;
        }

        /// <param name="Diferencas">Métricas comparadas, da mais próxima à mais distante; Delta = candidato − alvo, em pontos de percentil.</param>
        public sealed record Similar(int JogadorId, double Similaridade, IReadOnlyList<(string Id, double Delta)> Diferencas);

        /// <summary>
        /// Os <paramref name="top"/> jogadores mais parecidos com o alvo. Só entra
        /// quem divide com ele pelo menos <paramref name="minimoMetricas"/> métricas
        /// (ou todas as do alvo, se ele tiver menos) — semelhança medida em duas
        /// métricas diz pouco e venceria por acaso de quem foi medido em dez.
        /// </summary>
        public static List<Similar> Ranquear(
            int alvoId,
            IReadOnlyDictionary<int, Dictionary<string, double>> percentis,
            int top = 12, int minimoMetricas = 5)
        {
            if (!percentis.TryGetValue(alvoId, out var alvo) || alvo.Count == 0) return new();
            var exigidas = Math.Min(minimoMetricas, alvo.Count);

            var lista = new List<Similar>();
            foreach (var (id, p) in percentis)
            {
                if (id == alvoId) continue;
                var deltas = alvo.Keys
                    .Where(p.ContainsKey)
                    .Select(k => (Id: k, Delta: p[k] - alvo[k]))
                    .ToList();
                if (deltas.Count < exigidas) continue;

                var rms = Math.Sqrt(deltas.Average(d => d.Delta * d.Delta));
                lista.Add(new Similar(id, Math.Round(100 - rms, 1),
                    deltas.OrderBy(d => Math.Abs(d.Delta)).ToList()));
            }

            return lista
                .OrderByDescending(s => s.Similaridade)
                .ThenByDescending(s => s.Diferencas.Count)
                .Take(top)
                .ToList();
        }
    }
}
