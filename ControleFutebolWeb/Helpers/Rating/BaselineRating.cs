using System.Text.Json;
using System.Text.Json.Serialization;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers.Rating
{
    /// <summary>Média e desvio de uma métrica dentro de um grupo de posição.</summary>
    /// <param name="Media">Por 90 minutos, nas métricas de contagem; em %, nas taxas.</param>
    public sealed record ReferenciaMetrica(
        [property: JsonPropertyName("m")] double Media,
        [property: JsonPropertyName("d")] double Desvio,
        [property: JsonPropertyName("n")] int Amostras);

    /// <summary>
    /// A régua do rating automático: quanto um jogador de cada posição costuma
    /// produzir em cada métrica. É o que impede a inflação por volume — 12 desarmes
    /// só valem alguma coisa se 12 for muito PARA UM ZAGUEIRO.
    ///
    /// Nasce com valores embutidos (<see cref="Padrao"/>) e é substituída pela
    /// calibração sobre o histórico do próprio banco (CalibracaoRatingService).
    /// </summary>
    public sealed class BaselineRating
    {
        // Critério reservado que guarda a régua calibrada (JSON no Config). Fica em
        // criterionotas para não exigir migração: a coluna já é text e o registro é
        // compartilhado (UsuarioId null), como os demais padrões. Nunca soma pontos —
        // não tem extrator, e CriteriosNotaHelper.EhAcaoReservada o esconde da tela.
        public const string AcaoId = "baseline_rating";
        public const string Label = "Régua do rating automático (calibrada)";

        // Amostras mínimas para confiar na média de um (grupo, métrica) calibrado.
        // Abaixo disso o valor calculado é ruído e vale mais o padrão embutido.
        public const int MinimoAmostras = 25;

        // Jogos com menos que isto não entram na calibração: 8 minutos em campo
        // extrapolados para 90 viram números absurdos e explodem o desvio.
        public const int MinutosMinimosAmostra = 30;

        private readonly Dictionary<string, Dictionary<string, ReferenciaMetrica>> _porGrupo;

        public BaselineRating(Dictionary<string, Dictionary<string, ReferenciaMetrica>> porGrupo)
            => _porGrupo = porGrupo;

        /// <summary>Quando foi calibrado. Null = padrão embutido, nunca calibrado.</summary>
        public DateTime? CalibradoEm { get; init; }

        public IReadOnlyDictionary<string, Dictionary<string, ReferenciaMetrica>> Grupos => _porGrupo;

        // Grupo mais próximo, para quando o banco ainda não tem amostra suficiente
        // de uma posição: um ala se parece com um lateral muito mais do que com a
        // média geral do elenco.
        private static readonly IReadOnlyDictionary<string, string> GrupoVizinho =
            new Dictionary<string, string>
            {
                ["ALA"] = "LATERAL",
                ["LATERAL"] = "ALA",
                ["PONTA"] = "ATACANTE",
                ["ATACANTE"] = "PONTA",
                ["MEIA"] = "VOLANTE",
                ["VOLANTE"] = "MEIA",
                ["ZAGUEIRO"] = "VOLANTE",
            };

        /// <summary>
        /// Referência de (grupo, métrica), caindo para o grupo vizinho e depois para o
        /// padrão embutido quando a amostra calibrada é pequena demais. Null só quando
        /// nem o padrão conhece a combinação — aí a métrica não entra na nota.
        /// </summary>
        public ReferenciaMetrica? De(string grupo, string metricaId)
        {
            var direto = Bruto(grupo, metricaId);
            if (Suficiente(direto)) return direto;

            var vizinho = GrupoVizinho.GetValueOrDefault(grupo);
            var doVizinho = vizinho != null ? Bruto(vizinho, metricaId) : null;
            if (Suficiente(doVizinho)) return doVizinho;

            if (!ReferenceEquals(this, Padrao))
            {
                if (Padrao.Bruto(grupo, metricaId) is { } doPadrao) return doPadrao;
                if (vizinho != null && Padrao.Bruto(vizinho, metricaId) is { } vizinhoPadrao) return vizinhoPadrao;
            }

            // Uma amostra pequena ainda é melhor que nada quando nem o padrão cobre.
            return direto ?? doVizinho;

            static bool Suficiente(ReferenciaMetrica? r) => r != null && r.Amostras >= MinimoAmostras;
        }

        private ReferenciaMetrica? Bruto(string grupo, string metricaId)
            => _porGrupo.TryGetValue(grupo, out var doGrupo) && doGrupo.TryGetValue(metricaId, out var r)
                ? r : null;

        /// <summary>
        /// z-score truncado em ±<paramref name="limite"/> desvios. O truncamento é o
        /// que impede que um outlier de volume (um jogo com 20 duelos) domine a nota
        /// inteira — acima de 2σ o jogador já provou o ponto, o resto não paga mais.
        /// </summary>
        public static double ZTruncado(double valor, ReferenciaMetrica r, double limite = 2.0)
        {
            var desvio = DesvioUtil(r.Media, r.Desvio);
            return Math.Clamp((valor - r.Media) / desvio, -limite, limite);
        }

        // Piso de desvio. Sem ele, uma métrica quase constante numa posição (um
        // goleiro que nunca dribla) faria de qualquer ocorrência isolada um +2σ.
        private static double DesvioUtil(double media, double desvio)
            => Math.Max(desvio, Math.Max(0.10, 0.20 * Math.Abs(media)));

        // ---------------------------------------------------------------- montagem

        /// <summary>Uma linha da calibração: como o jogador atuou e o que produziu.</summary>
        public readonly record struct Amostra(string Grupo, int Minutos, EstatisticaJogador Estatistica);

        /// <summary>
        /// Calcula média e desvio de cada (grupo, métrica) a partir do histórico.
        /// Contagens são normalizadas para 90 minutos; taxas entram como estão.
        /// </summary>
        public static BaselineRating Montar(IEnumerable<Amostra> amostras)
        {
            var acumulado = new Dictionary<string, Dictionary<string, List<double>>>();

            foreach (var a in amostras)
            {
                if (string.IsNullOrWhiteSpace(a.Grupo) || a.Minutos < MinutosMinimosAmostra) continue;

                if (!acumulado.TryGetValue(a.Grupo, out var doGrupo))
                    acumulado[a.Grupo] = doGrupo = new Dictionary<string, List<double>>();

                foreach (var m in MetricasRating.Todas)
                {
                    var valor = ValorNormalizado(m, a.Estatistica, a.Minutos);
                    if (valor is not double v) continue;

                    if (!doGrupo.TryGetValue(m.Id, out var lista))
                        doGrupo[m.Id] = lista = new List<double>();
                    lista.Add(v);
                }
            }

            var porGrupo = acumulado.ToDictionary(
                g => g.Key,
                g => g.Value.ToDictionary(m => m.Key, m => Resumir(m.Value)));

            return new BaselineRating(porGrupo) { CalibradoEm = DateTime.UtcNow };
        }

        /// <summary>
        /// Valor comparável da métrica: contagem por 90 minutos ou a taxa em %.
        /// Null quando o dado não existe naquele jogo.
        /// </summary>
        public static double? ValorNormalizado(MetricaRating metrica, EstatisticaJogador e, int minutos)
        {
            if (metrica.Valor(e) is not double bruto) return null;
            if (!metrica.PorNoventa) return bruto;
            return minutos > 0 ? bruto * 90.0 / minutos : null;
        }

        private static ReferenciaMetrica Resumir(List<double> valores)
        {
            var n = valores.Count;
            var media = valores.Average();
            // Desvio amostral (n-1): com uma amostra só não há dispersão a estimar.
            var desvio = n > 1
                ? Math.Sqrt(valores.Sum(v => (v - media) * (v - media)) / (n - 1))
                : 0.0;
            return new ReferenciaMetrica(Math.Round(media, 4), Math.Round(desvio, 4), n);
        }

        // ------------------------------------------------------------ serialização

        private sealed class Envelope
        {
            [JsonPropertyName("calibradoEm")] public DateTime? CalibradoEm { get; set; }
            [JsonPropertyName("grupos")] public Dictionary<string, Dictionary<string, ReferenciaMetrica>> Grupos { get; set; } = new();
        }

        public string ParaJson() => JsonSerializer.Serialize(
            new Envelope { CalibradoEm = CalibradoEm, Grupos = _porGrupo });

        /// <summary>Null quando o JSON está vazio ou corrompido — quem chama cai no <see cref="Padrao"/>.</summary>
        public static BaselineRating? DeJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var envelope = JsonSerializer.Deserialize<Envelope>(json);
                if (envelope == null || envelope.Grupos.Count == 0) return null;
                return new BaselineRating(envelope.Grupos) { CalibradoEm = envelope.CalibradoEm };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ----------------------------------------------------------- padrão embutido

        // Referências de futebol de elite, por 90 minutos (taxas em %). Servem para o
        // sistema já nascer dando nota coerente, antes de qualquer calibração — e como
        // rede de segurança para posição com pouca amostra no banco.
        // Amostras = MinimoAmostras para o padrão nunca ser descartado por tamanho.
        public static readonly BaselineRating Padrao = ConstruirPadrao();

        private static BaselineRating ConstruirPadrao()
        {
            var grupos = new Dictionary<string, Dictionary<string, ReferenciaMetrica>>();

            // ordem: finalizacoes, finalizacoes_alvo, offsides, passes_chave, dribles,
            // dribles_precisao, faltas_sofridas, passes, passes_precisao, desarmes,
            // interceptacoes, bloqueios, duelos, duelos_precisao, dribles_sofridos,
            // defesas, gols_sofridos, faltas_cometidas, cartoes_amarelos
            Add("GOLEIRO",  0.02,0.10, 0.01,0.10, 0.00,0.05, 0.10,0.30, 0.05,0.20, 50,40, 0.15,0.40, 28,10, 68,14, 0.05,0.20, 0.20,0.50, 0.10,0.30, 1.2,1.2, 60,35, 0.10,0.30, 2.80,1.80, 1.20,1.10, 0.10,0.30, 0.08,0.28);
            Add("ZAGUEIRO", 0.50,0.80, 0.15,0.40, 0.10,0.30, 0.30,0.60, 0.40,0.70, 55,35, 0.70,0.90, 55,18, 85,8,  1.80,1.30, 1.50,1.20, 1.00,1.00, 8.0,3.5, 62,16, 0.50,0.70, 0.00,0.10, 0.00,0.10, 1.00,1.00, 0.18,0.40);
            Add("LATERAL",  0.70,0.90, 0.20,0.50, 0.15,0.40, 0.90,1.00, 1.30,1.40, 52,30, 1.00,1.10, 45,15, 79,9,  2.20,1.40, 1.20,1.10, 0.50,0.70, 9.0,3.5, 52,15, 1.10,1.10, 0.00,0.10, 0.00,0.10, 1.20,1.10, 0.20,0.42);
            Add("ALA",      1.00,1.10, 0.35,0.60, 0.20,0.50, 1.20,1.20, 2.00,1.80, 50,28, 1.30,1.30, 40,14, 77,10, 2.00,1.30, 1.00,1.00, 0.40,0.60, 9.5,3.6, 51,15, 1.20,1.20, 0.00,0.10, 0.00,0.10, 1.20,1.10, 0.20,0.42);
            Add("VOLANTE",  0.80,1.00, 0.25,0.50, 0.10,0.30, 0.80,1.00, 1.00,1.20, 60,30, 1.20,1.20, 62,20, 87,7,  2.60,1.50, 1.60,1.20, 0.60,0.80, 10.0,3.8, 55,14, 1.00,1.10, 0.00,0.10, 0.00,0.10, 1.50,1.20, 0.25,0.45);
            Add("MEIA",     1.60,1.40, 0.55,0.80, 0.25,0.55, 1.80,1.40, 2.20,1.90, 55,28, 1.60,1.40, 55,18, 84,8,  1.60,1.20, 0.90,0.90, 0.40,0.60, 9.0,3.5, 50,15, 1.00,1.10, 0.00,0.10, 0.00,0.10, 1.30,1.20, 0.20,0.42);
            Add("PONTA",    2.20,1.60, 0.80,1.00, 0.50,0.80, 1.50,1.30, 4.00,2.50, 48,24, 1.80,1.50, 32,12, 79,10, 1.20,1.10, 0.50,0.70, 0.20,0.50, 10.0,3.8, 45,14, 0.80,1.00, 0.00,0.10, 0.00,0.10, 1.00,1.00, 0.15,0.37);
            Add("ATACANTE", 2.80,1.80, 1.10,1.10, 0.90,1.10, 0.90,1.00, 2.20,1.90, 45,26, 1.50,1.40, 22,9,  74,12, 0.60,0.80, 0.30,0.60, 0.20,0.50, 11.0,4.0, 42,14, 0.50,0.80, 0.00,0.10, 0.00,0.10, 1.20,1.10, 0.15,0.37);

            return new BaselineRating(grupos);

            void Add(string grupo, params double[] pares)
            {
                // A tabela acima é posicional: acrescentar uma métrica em
                // MetricasRating sem acrescentar o par aqui desalinharia TODAS as
                // referências seguintes do grupo, silenciosamente.
                if (pares.Length != MetricasRating.Todas.Count * 2)
                    throw new InvalidOperationException(
                        $"Baseline padrão de {grupo}: esperados {MetricasRating.Todas.Count * 2} " +
                        $"valores (média/desvio de cada métrica), vieram {pares.Length}.");

                var doGrupo = new Dictionary<string, ReferenciaMetrica>();
                for (int i = 0; i < MetricasRating.Todas.Count; i++)
                    doGrupo[MetricasRating.Todas[i].Id] =
                        new ReferenciaMetrica(pares[i * 2], pares[i * 2 + 1], MinimoAmostras);
                grupos[grupo] = doGrupo;
            }
        }
    }
}
