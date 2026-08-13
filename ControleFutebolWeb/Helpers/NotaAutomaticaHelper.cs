using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers.Rating;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    using ContextoNota = CriteriosNotaHelper.ContextoNota;
    using LadoPorJogadorJogo = IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo>;

    /// <summary>Qual régua calcula a nota dos jogos que o usuário não avaliou à mão.</summary>
    public enum MotorNota
    {
        /// <summary>Soma "peso inicial + quantidade × peso", com os pisos de merecimento.</summary>
        Classico,

        /// <summary>Rating por posição, comparado à média da posição (Helpers/Rating).</summary>
        Automatico,
    }

    /// <summary>
    /// Nota de um jogo que não foi avaliado à mão, já pela régua escolhida.
    /// </summary>
    /// <param name="ValorAcoes">
    /// Soma "quantidade × peso" das ações. Existe nos dois motores porque as telas a
    /// exibem como "pontos do jogo", mas só no Clássico ela forma a nota.
    /// </param>
    /// <param name="Classica">Parcelas do motor clássico. Vazia quando o motor é o automático.</param>
    /// <param name="Rating">Parcelas do rating. Null quando o motor é o clássico.</param>
    public sealed record NotaAutomatica(
        double Nota,
        double ValorAcoes,
        List<Notadetalhe> Detalhes,
        CriteriosNotaHelper.ComposicaoNota Classica,
        ComposicaoRating? Rating)
    {
        /// <summary>De onde a nota parte, para a tela explicar a conta.</summary>
        public double NotaBase => Rating != null ? Rating.NotaBase : Classica.NotaBase;
    }

    /// <summary>
    /// Ponto único onde se decide qual motor calcula a nota automática.
    ///
    /// Existe para que a escolha do usuário (CriteriosNotaHelper.AcaoMotorNota) seja
    /// lida em UM lugar: os nove pontos que exibem nota automática — relatórios,
    /// perfil, histórico do jogador, competições, pré-jogo — chamam esta fachada em
    /// vez de montar a conta cada um do seu jeito.
    ///
    /// A nota MANUAL não passa por aqui: quando o usuário avaliou o jogo (marcando as
    /// ações ou informando a nota final), a régua é a dele e nenhum motor a substitui.
    /// </summary>
    public sealed class CalculadoraNotaAutomatica
    {
        private readonly IReadOnlyDictionary<(int, int), ContextoNota> _contextosClassicos;
        private readonly IReadOnlyDictionary<(int, int), ContextoRating> _contextosRating;
        private readonly BaselineRating _baseline;
        private readonly LadoPorJogadorJogo? _lados;

        public MotorNota Motor { get; }
        public IReadOnlyList<CriterioNota> Criterios { get; }

        public CalculadoraNotaAutomatica(
            MotorNota motor,
            IReadOnlyList<CriterioNota> criterios,
            LadoPorJogadorJogo? lados,
            IReadOnlyDictionary<(int, int), ContextoNota> contextosClassicos,
            IReadOnlyDictionary<(int, int), ContextoRating> contextosRating,
            BaselineRating baseline)
        {
            Motor = motor;
            Criterios = criterios;
            _lados = lados;
            _contextosClassicos = contextosClassicos;
            _contextosRating = contextosRating;
            _baseline = baseline;
        }

        /// <summary>Nota de uma linha de estatística.</summary>
        public NotaAutomatica De(EstatisticaJogador estatistica)
            => De(new[] { estatistica }, estatistica.JogadorId, estatistica.JogoId);

        /// <summary>
        /// Nota de um jogo com mais de uma linha de estatística para o mesmo jogador
        /// (acontece quando a importação gravou a partida em partes).
        /// </summary>
        public NotaAutomatica De(IReadOnlyCollection<EstatisticaJogador> estatisticas, int jogadorId, int jogoId)
        {
            var valorAcoes = Math.Round(
                estatisticas.Sum(e => CriteriosNotaHelper.CalcularPontuacao(e, Criterios, _lados)), 2);

            // Os detalhes (os chips do jogo) valem nos dois motores: eles descrevem o
            // que o jogador fez, não como a nota foi montada.
            var detalhes = estatisticas
                .SelectMany(e => CriteriosNotaHelper.ConstruirDetalhes(e, Criterios, _lados))
                .ToList();

            var contextoClassico = _contextosClassicos.TryGetValue((jogadorId, jogoId), out var c)
                ? c : ContextoNota.Vazio;
            contextoClassico = contextoClassico with { Acoes = CriteriosNotaHelper.ContarAcoes(detalhes) };

            if (Motor == MotorNota.Classico)
            {
                var composicao = CriteriosNotaHelper.Compor(valorAcoes, Criterios, contextoClassico);
                return new NotaAutomatica(composicao.Nota, valorAcoes, detalhes, composicao, null);
            }

            var contextoRating = _contextosRating.TryGetValue((jogadorId, jogoId), out var r)
                ? r : ContextoRating.Vazio;
            var posicao = _lados != null && _lados.TryGetValue((jogadorId, jogoId), out var atuacao)
                ? atuacao.Posicao : null;

            // Uma linha só por (jogador, jogo) para o rating: as métricas dele são por
            // 90 minutos, e somar duas linhas parciais dobraria os minutos junto.
            var linha = Consolidar(estatisticas);
            var rating = RatingAutomaticoHelper.Calcular(linha, posicao, contextoRating, _baseline);

            // Sem minutos não há rating a calcular (jogador que não entrou em campo).
            // A nota clássica é o único valor disponível, e é melhor que nenhum.
            if (rating == null)
            {
                var composicao = CriteriosNotaHelper.Compor(valorAcoes, Criterios, contextoClassico);
                return new NotaAutomatica(composicao.Nota, valorAcoes, detalhes, composicao, null);
            }

            return new NotaAutomatica(rating.Nota, valorAcoes, detalhes, default, rating);
        }

        // Junta linhas parciais do mesmo (jogador, jogo) numa só: contagens somam,
        // minutos somam. Com uma linha só — o caso normal — devolve a própria.
        private static EstatisticaJogador Consolidar(IReadOnlyCollection<EstatisticaJogador> estatisticas)
        {
            if (estatisticas.Count == 1) return estatisticas.First();

            var somada = new EstatisticaJogador
            {
                Minutos = estatisticas.Sum(e => e.Minutos ?? 0),
                Offsides = estatisticas.Sum(e => e.Offsides),
                FinalizacoesTotal = estatisticas.Sum(e => e.FinalizacoesTotal),
                FinalizacoesNoGol = estatisticas.Sum(e => e.FinalizacoesNoGol),
                Gols = estatisticas.Sum(e => e.Gols),
                GolsSofridos = estatisticas.Sum(e => e.GolsSofridos),
                Assistencias = estatisticas.Sum(e => e.Assistencias),
                Defesas = estatisticas.Sum(e => e.Defesas),
                PassesTotal = estatisticas.Sum(e => e.PassesTotal),
                PassesCertos = estatisticas.Sum(e => e.PassesCertos),
                PassesChave = estatisticas.Sum(e => e.PassesChave),
                Desarmes = estatisticas.Sum(e => e.Desarmes),
                Bloqueios = estatisticas.Sum(e => e.Bloqueios),
                Interceptacoes = estatisticas.Sum(e => e.Interceptacoes),
                DuelosTotal = estatisticas.Sum(e => e.DuelosTotal),
                DuelosVencidos = estatisticas.Sum(e => e.DuelosVencidos),
                DriblesTentados = estatisticas.Sum(e => e.DriblesTentados),
                DriblesCertos = estatisticas.Sum(e => e.DriblesCertos),
                DriblesSofridos = estatisticas.Sum(e => e.DriblesSofridos),
                FaltasSofridas = estatisticas.Sum(e => e.FaltasSofridas),
                FaltasCometidas = estatisticas.Sum(e => e.FaltasCometidas),
                CartoesAmarelos = estatisticas.Sum(e => e.CartoesAmarelos),
                CartoesVermelhos = estatisticas.Sum(e => e.CartoesVermelhos),
                PenaltiSofrido = estatisticas.Sum(e => e.PenaltiSofrido),
                PenaltiCometido = estatisticas.Sum(e => e.PenaltiCometido),
                PenaltiPerdido = estatisticas.Sum(e => e.PenaltiPerdido),
                PenaltiDefendido = estatisticas.Sum(e => e.PenaltiDefendido),
                PenaltiConvertido = estatisticas.Sum(e => e.PenaltiConvertido),
            };

            var primeira = estatisticas.First();
            somada.JogadorId = primeira.JogadorId;
            somada.JogoId = primeira.JogoId;
            return somada;
        }
    }

    public static class NotaAutomaticaHelper
    {
        /// <summary>
        /// Monta a calculadora para um conjunto de jogos: lê o motor escolhido pelo
        /// usuário e carrega só o que aquele motor precisa.
        /// </summary>
        /// <param name="criterios">
        /// Critérios já mesclados (MergeCriterios). É deles que sai o motor escolhido,
        /// então precisam incluir os do usuário — não só os compartilhados.
        /// </param>
        /// <param name="lados">Mapa do LadoJogadorHelper, quando o chamador já o carregou.</param>
        public static async Task<CalculadoraNotaAutomatica> CarregarAsync(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            IReadOnlyList<CriterioNota> criterios,
            LadoPorJogadorJogo? lados = null,
            IReadOnlyDictionary<(int, int), ContextoNota>? contextosClassicos = null,
            CancellationToken ct = default)
        {
            var motor = CriteriosNotaHelper.MotorDaNota(criterios);

            if (jogoIds.Count == 0)
                return new CalculadoraNotaAutomatica(motor, criterios, lados,
                    contextosClassicos ?? new Dictionary<(int, int), ContextoNota>(),
                    new Dictionary<(int, int), ContextoRating>(), BaselineRating.Padrao);

            lados ??= await LadoJogadorHelper.CarregarAsync(context, jogoIds, usuarioId, ct: ct);
            contextosClassicos ??= await ContextoNotaHelper.CarregarAsync(context, jogoIds, usuarioId, lados, ct);

            // A régua e o contexto do rating só são carregados no motor automático:
            // quem ficou no clássico não paga nenhuma consulta a mais que já pagava.
            var baseline = BaselineRating.Padrao;
            IReadOnlyDictionary<(int, int), ContextoRating> contextosRating =
                new Dictionary<(int, int), ContextoRating>();

            if (motor == MotorNota.Automatico)
            {
                var servico = new Services.RatingAutomaticoService(context);
                baseline = await servico.CarregarBaselineAsync(ct);
                contextosRating = await servico.CarregarContextosAsync(jogoIds, usuarioId, lados, ct);
            }

            return new CalculadoraNotaAutomatica(
                motor, criterios, lados, contextosClassicos, contextosRating, baseline);
        }

        /// <inheritdoc cref="CarregarAsync"/>
        public static CalculadoraNotaAutomatica Carregar(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            IReadOnlyList<CriterioNota> criterios,
            LadoPorJogadorJogo? lados = null,
            IReadOnlyDictionary<(int, int), ContextoNota>? contextosClassicos = null)
        {
            var motor = CriteriosNotaHelper.MotorDaNota(criterios);

            if (jogoIds.Count == 0)
                return new CalculadoraNotaAutomatica(motor, criterios, lados,
                    contextosClassicos ?? new Dictionary<(int, int), ContextoNota>(),
                    new Dictionary<(int, int), ContextoRating>(), BaselineRating.Padrao);

            lados ??= LadoJogadorHelper.Carregar(context, jogoIds, usuarioId);
            contextosClassicos ??= ContextoNotaHelper.Carregar(context, jogoIds, usuarioId, lados);

            var baseline = BaselineRating.Padrao;
            IReadOnlyDictionary<(int, int), ContextoRating> contextosRating =
                new Dictionary<(int, int), ContextoRating>();

            if (motor == MotorNota.Automatico)
            {
                var servico = new Services.RatingAutomaticoService(context);
                baseline = servico.CarregarBaseline();
                contextosRating = servico.CarregarContextos(jogoIds, usuarioId, lados);
            }

            return new CalculadoraNotaAutomatica(
                motor, criterios, lados, contextosClassicos, contextosRating, baseline);
        }
    }
}
