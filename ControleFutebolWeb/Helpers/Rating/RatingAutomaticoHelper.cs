using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers.Rating
{
    /// <summary>O que a partida diz sobre o jogador além da própria linha estatística.</summary>
    /// <param name="ResultadoSinal">+1 vitória, 0 empate, -1 derrota. Null = placar desconhecido.</param>
    /// <param name="GolsSofridosTime">Gols que o TIME do jogador levou. Null = desconhecido.</param>
    /// <param name="GolsContra">Gols contra marcados pelo jogador na partida.</param>
    public readonly record struct ContextoRating(
        int? ResultadoSinal = null,
        int? GolsSofridosTime = null,
        int GolsContra = 0,
        bool GolDaVitoria = false)
    {
        public static readonly ContextoRating Vazio = new();
    }

    public sealed record ContribuicaoMetrica(
        string Id, string Label, double Valor, double Media, double Desvio, double Z, double Peso);

    public sealed record ContribuicaoFamilia(
        FamiliaRating Familia, double Peso, double Z, double Pontos,
        IReadOnlyList<ContribuicaoMetrica> Metricas);

    public sealed record ContribuicaoEvento(string Label, int Quantidade, double Pontos);

    /// <summary>A nota e cada parcela que a formou — a tela consegue explicar a conta.</summary>
    public sealed record ComposicaoRating(
        double Nota,
        string? Grupo,
        int Minutos,
        double Confianca,
        double NotaBase,
        double Desempenho,
        double Eventos,
        double Defensivo,
        double Resultado,
        double Bruta,
        IReadOnlyList<ContribuicaoFamilia> Familias,
        IReadOnlyList<ContribuicaoEvento> EventosDetalhe);

    /// <summary>
    /// Rating automático de um jogador numa partida, de 3,0 a 10,0.
    ///
    /// A ideia central é que a nota é um DESVIO a partir de um ponto neutro, não um
    /// acúmulo de pontos por ação. Isso ataca as três formas de inflar/desinflar que
    /// a soma "quantidade × peso" tem:
    ///
    ///   • volume  — cada métrica contínua vira z-score contra a média da POSIÇÃO
    ///     (BaselineRating), truncado em ±2σ: 12 desarmes só pontuam se 12 for muito
    ///     para um zagueiro, e o 20º duelo não paga mais que o 12º;
    ///   • erro    — o que é tentativa entra como volume E como aproveitamento, então
    ///     quem tenta muito e acerta pouco fica negativo;
    ///   • posição — os pesos de cada família mudam por posição (PesosRating), de modo
    ///     que todas as posições têm caminho até a nota alta.
    ///
    /// Eventos raros e decisivos (gol, assistência, pênalti, vermelho) ficam fora do
    /// z-score: entram com valor fixo e retorno decrescente, para que um hat-trick
    /// não estoure a escala sozinho.
    /// </summary>
    public static class RatingAutomaticoHelper
    {
        // Ponto neutro. Quem entrou em campo e não produziu nada acima nem abaixo do
        // esperado para a posição dele termina exatamente aqui.
        public const double NotaNeutra = 6.0;

        public const double NotaMinima = 3.0;
        public const double NotaMaxima = 10.0;

        // Quantos pontos de nota vale 1 desvio-padrão de desempenho agregado. Com o
        // truncamento em ±2σ, a parte contínua sozinha varia de 4,2 a 7,8 — sobra
        // espaço para os eventos decidirem quem passa de 8.
        public const double EscalaDesempenho = 0.9;

        // Minutos para a linha estatística ser levada a sério por inteiro. Abaixo
        // disso a nota encolhe na direção do neutro, em vez de ganhar um piso
        // artificial: quem entrou aos 80' e não tocou na bola fica em 6,0, não em 5,0.
        public const int MinutosConfiancaPlena = 60;

        // Peso do resultado da partida, proporcional ao tempo em campo. Pequeno de
        // propósito: o resultado é do time, a nota é do jogador.
        public const double PesoResultado = 0.30;

        // Gols sofridos pelo time como parte da nota de quem defende. Substitui o
        // bônus fixo de "não sofreu gol": um 0x0 vale mais que um 3x3, e um 1x0
        // sofrido não é o mesmo desastre que um 4x0.
        public const double PesoDefensivo = 0.35;
        public const double MediaGolsSofridos = 1.20;
        public const double DesvioGolsSofridos = 1.10;

        // A partir daqui a nota comprime: o caminho de 9 para 10 é muito mais caro
        // que o de 7 para 8. É o que faz a nota máxima ser rara em vez de recorrente.
        //
        // A amplitude é exatamente NotaMaxima - LimiteSuave de propósito: assim a
        // curva tem o 10 como assíntota e o teto é uma propriedade da fórmula, não
        // um clamp que corta o topo e empilha todo mundo em 10,00.
        public const double LimiteSuave = 8.0;
        public const double AmplitudeSuave = NotaMaxima - LimiteSuave;

        // Valor do primeiro evento de cada tipo; do segundo em diante cai por
        // FatorRepeticao. Dois gols valem mais que um, mas não o dobro.
        public const double FatorRepeticao = 0.75;

        // O Id é o que FonteEstatistica consulta para saber se a fonte publica aquele
        // evento — os quatro de pênalti não vêm da ESPN, e somá-los como zero seria
        // afirmar que não houve pênalti nenhum.
        private static readonly (string Id, string Label, double Valor, Func<EstatisticaJogador, int> Quantidade)[] Eventos =
        {
            ("evento_gol",               "Gol",               +1.20, e => e.Gols),
            ("evento_assistencia",       "Assistência",       +0.80, e => e.Assistencias),
            ("evento_penalti_defendido", "Pênalti defendido", +1.00, e => e.PenaltiDefendido),
            ("evento_penalti_sofrido",   "Pênalti sofrido",   +0.40, e => e.PenaltiSofrido),
            ("evento_penalti_perdido",   "Pênalti perdido",   -0.80, e => e.PenaltiPerdido),
            ("evento_penalti_cometido",  "Pênalti cometido",  -0.80, e => e.PenaltiCometido),
            ("evento_cartao_vermelho",   "Cartão vermelho",   -1.50, e => e.CartoesVermelhos),
        };

        public const double ValorGolContra = -1.20;
        public const double ValorGolDaVitoria = +0.50;

        // Jogo sem sofrer gol, para o goleiro. A métrica "gols sofridos" sozinha não
        // passa de ~+1σ (média 1,3, desvio 1,25), e o goleiro não tem gol nem
        // assistência para chegar à faixa alta: sem este evento o melhor jogo possível
        // de um goleiro parava em ~7,5. Vale o mesmo que uma assistência, e só para
        // quem esteve em campo tempo suficiente para o 0 ser mérito dele.
        public const double ValorGoleiroSemSofrerGol = +0.80;

        /// <summary>
        /// Nota da partida. Null quando o jogador não entrou em campo (sem minutos não
        /// há desempenho a medir — e dar 6,0 a quem ficou no banco poluiria as médias).
        /// </summary>
        /// <param name="posicaoNoJogo">
        /// Posição DAQUELA partida (LadoJogadorHelper), não Jogador.Posicao: um lateral
        /// escalado de meia tem que ser avaliado pela régua de meia naquele jogo.
        /// </param>
        public static ComposicaoRating? Calcular(
            EstatisticaJogador estatistica,
            string? posicaoNoJogo,
            ContextoRating contexto = default,
            BaselineRating? baseline = null)
        {
            var minutos = estatistica.Minutos ?? 0;
            if (minutos <= 0) return null;

            baseline ??= BaselineRating.Padrao;
            var grupo = CriteriosNotaHelper.GrupoDaPosicao(posicaoNoJogo);
            var pesos = PesosRating.Do(grupo);
            var confianca = Math.Clamp(minutos / (double)MinutosConfiancaPlena, 0, 1);

            var familias = Familias(estatistica, grupo, pesos, minutos, baseline);

            // Renormaliza pelo peso das famílias que este jogo conseguiu medir: se
            // faltou o dado de uma delas, o que sobra continua valendo a escala cheia
            // em vez de a nota encolher para o neutro por falta de informação.
            var pesoMedido = familias.Sum(f => f.Peso);
            var desempenho = pesoMedido > 0
                ? EscalaDesempenho * familias.Sum(f => f.Peso * f.Z) / pesoMedido * confianca
                : 0;

            var (eventos, eventosDetalhe) = CalcularEventos(estatistica, grupo, minutos, contexto);

            var defensivo = CalcularDefensivo(grupo, contexto) * confianca;

            var resultado = contexto.ResultadoSinal is int sinal
                ? PesoResultado * sinal * Math.Min(1.0, minutos / 90.0)
                : 0;

            var bruta = NotaNeutra + desempenho + eventos + defensivo + resultado;
            var nota = Math.Clamp(Comprimir(bruta), NotaMinima, NotaMaxima);

            return new ComposicaoRating(
                Math.Round(nota, 2), grupo, minutos, Math.Round(confianca, 3),
                NotaNeutra,
                Math.Round(desempenho, 3), Math.Round(eventos, 3),
                Math.Round(defensivo, 3), Math.Round(resultado, 3), Math.Round(bruta, 3),
                familias, eventosDetalhe);
        }

        /// <summary>Só a nota, para quem não precisa da explicação.</summary>
        public static double? Nota(
            EstatisticaJogador estatistica, string? posicaoNoJogo,
            ContextoRating contexto = default, BaselineRating? baseline = null)
            => Calcular(estatistica, posicaoNoJogo, contexto, baseline)?.Nota;

        private static List<ContribuicaoFamilia> Familias(
            EstatisticaJogador e, string? grupo, IReadOnlyDictionary<FamiliaRating, double> pesos,
            int minutos, BaselineRating baseline)
        {
            // Sem grupo conhecido a régua é a do jogador de linha médio — melhor que
            // usar a de goleiro por engano.
            var grupoBaseline = grupo ?? "MEIA";
            var resultado = new List<ContribuicaoFamilia>();

            foreach (var (familia, peso) in pesos)
            {
                if (peso <= 0) continue;

                var metricas = new List<ContribuicaoMetrica>();
                double somaZ = 0, somaPesos = 0;

                foreach (var m in MetricasRating.Todas.Where(m => m.Familia == familia))
                {
                    if (BaselineRating.ValorNormalizado(m, e, minutos) is not double valor) continue;
                    var referencia = baseline.De(grupoBaseline, m.Id);
                    if (referencia == null) continue;

                    var z = BaselineRating.ZTruncado(valor, referencia, m.LimiteZ) * m.Sinal;
                    somaZ += m.Peso * z;
                    somaPesos += m.Peso;
                    metricas.Add(new ContribuicaoMetrica(
                        m.Id, m.Label, Math.Round(valor, 2),
                        referencia.Media, referencia.Desvio, Math.Round(z, 3), m.Peso));
                }

                // Família sem nenhum dado neste jogo fica de fora e devolve o peso dela
                // às demais (ver renormalização em Calcular).
                if (somaPesos <= 0) continue;

                var zFamilia = somaZ / somaPesos;
                resultado.Add(new ContribuicaoFamilia(
                    familia, peso, Math.Round(zFamilia, 3),
                    Math.Round(EscalaDesempenho * peso * zFamilia, 3), metricas));
            }

            return resultado;
        }

        private static (double Total, List<ContribuicaoEvento> Detalhe) CalcularEventos(
            EstatisticaJogador e, string? grupo, int minutos, ContextoRating contexto)
        {
            var detalhe = new List<ContribuicaoEvento>();
            double total = 0;

            foreach (var (id, label, valor, quantidade) in Eventos)
            {
                if (!FonteEstatistica.Cobre(e.Fonte, id)) continue;
                var n = quantidade(e);
                if (n <= 0) continue;
                var pontos = Saturar(n, valor);
                total += pontos;
                detalhe.Add(new ContribuicaoEvento(label, n, Math.Round(pontos, 3)));
            }

            if (contexto.GolsContra > 0)
            {
                var pontos = Saturar(contexto.GolsContra, ValorGolContra);
                total += pontos;
                detalhe.Add(new ContribuicaoEvento("Gol contra", contexto.GolsContra, Math.Round(pontos, 3)));
            }

            if (contexto.GolDaVitoria)
            {
                total += ValorGolDaVitoria;
                detalhe.Add(new ContribuicaoEvento("Gol da vitória", 1, ValorGolDaVitoria));
            }

            // Placar desconhecido não dá o evento: sem ele não há como afirmar o 0.
            if (grupo == "GOLEIRO" && minutos >= MinutosConfiancaPlena && contexto.GolsSofridosTime == 0)
            {
                total += ValorGoleiroSemSofrerGol;
                detalhe.Add(new ContribuicaoEvento("Não sofreu gol", 1, ValorGoleiroSemSofrerGol));
            }

            return (total, detalhe);
        }

        // Gols sofridos pelo time, para quem tem responsabilidade defensiva. O goleiro
        // fica de fora: a família Goleiro já mede os gols sofridos na linha dele, e
        // contar duas vezes puniria o goleiro em dobro pelo mesmo placar.
        private static double CalcularDefensivo(string? grupo, ContextoRating contexto)
        {
            if (grupo == "GOLEIRO" || !PesosRating.ResponsavelDefensivo(grupo)) return 0;
            if (contexto.GolsSofridosTime is not int sofridos) return 0;

            var z = Math.Clamp((sofridos - MediaGolsSofridos) / DesvioGolsSofridos, -2, 2);
            return -PesoDefensivo * z;
        }

        /// <summary>
        /// Valor total de <paramref name="n"/> ocorrências do mesmo evento, com retorno
        /// decrescente: o 2º gol vale 75% do 1º, o 3º 75% do 2º, e assim por diante.
        /// </summary>
        public static double Saturar(int n, double valorBase)
        {
            double total = 0, atual = valorBase;
            for (int i = 0; i < n; i++)
            {
                total += atual;
                atual *= FatorRepeticao;
            }
            return total;
        }

        /// <summary>
        /// Comprime a faixa alta da nota. Acima de 8,5 cada ponto bruto rende cada vez
        /// menos, de modo que o 10 é assintótico — nota perfeita exige uma atuação
        /// realmente fora da curva, não um bom jogo com três gols.
        /// </summary>
        public static double Comprimir(double bruta)
            => bruta <= LimiteSuave
                ? bruta
                : LimiteSuave + AmplitudeSuave * Math.Tanh((bruta - LimiteSuave) / AmplitudeSuave);
    }
}
