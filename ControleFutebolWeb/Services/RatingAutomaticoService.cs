// ControleFutebolWeb/Services/RatingAutomaticoService.cs
// Liga o rating automático (Helpers/Rating) ao banco: monta o contexto de cada
// (jogador, jogo), calibra a régua de referência sobre o histórico e mede a
// aderência das notas geradas contra o rating do provedor.
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Helpers.Rating;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>Aderência das nossas notas ao rating do provedor, num recorte.</summary>
    /// <param name="Correlacao">Pearson. 1 = concordância perfeita, 0 = nenhuma relação.</param>
    /// <param name="ErroMedio">Erro absoluto médio, em pontos de nota.</param>
    /// <param name="Vies">Positivo = estamos dando nota mais alta que o provedor.</param>
    public sealed record AderenciaRating(
        string Recorte, int Amostras, double Correlacao, double ErroMedio, double Vies,
        double MediaNossa, double DesvioNosso, double MediaProvedor, double DesvioProvedor,
        double P50, double P90, double P99, double Maxima);

    public sealed record RelatorioCalibracao(
        int JogosAnalisados,
        int LinhasAnalisadas,
        int GruposCalibrados,
        AderenciaRating Geral,
        IReadOnlyList<AderenciaRating> PorPosicao,
        BaselineRating Baseline);

    public class RatingAutomaticoService
    {
        // Jogos por lote na calibração: o histórico inteiro de escalações não cabe
        // numa consulta só, e o LadoJogadorHelper precisa do elenco completo de cada
        // jogo que carrega.
        private const int TamanhoLote = 250;

        private readonly FutebolContext _context;

        public RatingAutomaticoService(FutebolContext context) => _context = context;

        // ------------------------------------------------------------------ baseline

        /// <summary>Régua calibrada salva no banco, ou a embutida quando não houver.</summary>
        public async Task<BaselineRating> CarregarBaselineAsync(CancellationToken ct = default)
            => BaselineRating.DeJson(
                (await ConsultaBaseline().FirstOrDefaultAsync(ct))?.Config) ?? BaselineRating.Padrao;

        /// <inheritdoc cref="CarregarBaselineAsync"/>
        public BaselineRating CarregarBaseline()
            => BaselineRating.DeJson(ConsultaBaseline().FirstOrDefault()?.Config) ?? BaselineRating.Padrao;

        private IQueryable<CriterioNota> ConsultaBaseline() =>
            _context.CriteriosNota.AsNoTracking()
                .Where(c => c.AcaoId == BaselineRating.AcaoId && c.UsuarioId == null);

        public async Task SalvarBaselineAsync(BaselineRating baseline, CancellationToken ct = default)
        {
            var registro = await _context.CriteriosNota
                .FirstOrDefaultAsync(c => c.AcaoId == BaselineRating.AcaoId && c.UsuarioId == null, ct);

            if (registro == null)
            {
                registro = new CriterioNota
                {
                    AcaoId = BaselineRating.AcaoId,
                    Label = BaselineRating.Label,
                    // Peso 0 e sem extrator: mesmo que algum caminho antigo leia este
                    // registro como critério de ação, ele não soma nada à nota manual.
                    Peso = 0,
                    Ativo = true,
                    Ordem = 0,
                };
                _context.CriteriosNota.Add(registro);
            }

            registro.Config = baseline.ParaJson();
            await _context.SaveChangesAsync(ct);
        }

        // -------------------------------------------------------------------- cálculo

        /// <summary>
        /// Nota automática de cada (jogador, jogo) dos jogos informados. Jogadores que
        /// não entraram em campo ficam de fora do mapa.
        /// </summary>
        public async Task<Dictionary<(int JogadorId, int JogoId), ComposicaoRating>> CalcularAsync(
            IReadOnlyCollection<int> jogoIds, string? usuarioId,
            BaselineRating? baseline = null, CancellationToken ct = default)
        {
            if (jogoIds.Count == 0) return new();

            baseline ??= await CarregarBaselineAsync(ct);
            var lados = await LadoJogadorHelper.CarregarAsync(_context, jogoIds, usuarioId, ct: ct);
            var contextos = await CarregarContextosAsync(jogoIds, usuarioId, lados, ct);

            var estatisticas = await _context.EstatisticasJogador.AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId))
                .ToListAsync(ct);

            var mapa = new Dictionary<(int, int), ComposicaoRating>();
            foreach (var e in estatisticas)
            {
                var posicao = lados.TryGetValue((e.JogadorId, e.JogoId), out var atuacao) ? atuacao.Posicao : null;
                var contexto = contextos.GetValueOrDefault((e.JogadorId, e.JogoId), ContextoRating.Vazio);
                if (RatingAutomaticoHelper.Calcular(e, posicao, contexto, baseline) is { } composicao)
                    mapa[(e.JogadorId, e.JogoId)] = composicao;
            }

            return mapa;
        }

        /// <summary>
        /// Contexto de cada (jogador, jogo): resultado, gols sofridos pelo time, gols
        /// contra e gol da vitória. Exposto para a fachada de nota automática montar a
        /// calculadora sem passar pelo cálculo inteiro.
        /// </summary>
        public async Task<Dictionary<(int JogadorId, int JogoId), ContextoRating>> CarregarContextosAsync(
            IReadOnlyCollection<int> jogoIds, string? usuarioId,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> lados,
            CancellationToken ct = default)
            => Montar(
                await ConsultaPlacares(jogoIds).ToDictionaryAsync(j => j.Id, j => (j.PlacarCasa, j.PlacarVisitante), ct),
                await ConsultaGolsContra(jogoIds).ToListAsync(ct),
                await ContextoNotaHelper.CarregarAsync(_context, jogoIds, usuarioId, lados, ct),
                lados);

        /// <inheritdoc cref="CarregarContextosAsync"/>
        public Dictionary<(int JogadorId, int JogoId), ContextoRating> CarregarContextos(
            IReadOnlyCollection<int> jogoIds, string? usuarioId,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> lados)
            => Montar(
                ConsultaPlacares(jogoIds).ToDictionary(j => j.Id, j => (j.PlacarCasa, j.PlacarVisitante)),
                ConsultaGolsContra(jogoIds).ToList(),
                ContextoNotaHelper.Carregar(_context, jogoIds, usuarioId, lados),
                lados);

        private IQueryable<PlacarJogo> ConsultaPlacares(IReadOnlyCollection<int> jogoIds) =>
            _context.Jogos.AsNoTracking()
                .Where(j => jogoIds.Contains(j.Id))
                .Select(j => new PlacarJogo(j.Id, j.PlacarCasa, j.PlacarVisitante));

        private IQueryable<AutorGolContra> ConsultaGolsContra(IReadOnlyCollection<int> jogoIds) =>
            _context.Gols.AsNoTracking()
                .Where(g => jogoIds.Contains(g.JogoId) && g.Contra)
                .Select(g => new AutorGolContra(g.JogadorId, g.JogoId));

        private readonly record struct PlacarJogo(int Id, int? PlacarCasa, int? PlacarVisitante);
        private readonly record struct AutorGolContra(int JogadorId, int JogoId);

        // O gol da vitória já é resolvido pelo ContextoNotaHelper (precisa da série
        // completa de gols da partida) — reaproveitado aqui em vez de reimplementado.
        private static Dictionary<(int JogadorId, int JogoId), ContextoRating> Montar(
            Dictionary<int, (int? PlacarCasa, int? PlacarVisitante)> placares,
            List<AutorGolContra> golsContraLinhas,
            IReadOnlyDictionary<(int JogadorId, int JogoId), CriteriosNotaHelper.ContextoNota> doHelper,
            IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> lados)
        {
            var golsContra = golsContraLinhas
                .GroupBy(g => (g.JogadorId, g.JogoId))
                .ToDictionary(g => g.Key, g => g.Count());

            var mapa = new Dictionary<(int, int), ContextoRating>();
            foreach (var ((jogadorId, jogoId), atuacao) in lados)
            {
                int? sinal = null, sofridos = null;
                if (placares.TryGetValue(jogoId, out var placar)
                    && placar.PlacarCasa is int casa && placar.PlacarVisitante is int visitante)
                {
                    var (pro, contra) = atuacao.IsTimeCasa ? (casa, visitante) : (visitante, casa);
                    sinal = Math.Sign(pro - contra);
                    sofridos = contra;
                }

                mapa[(jogadorId, jogoId)] = new ContextoRating(
                    ResultadoSinal: sinal,
                    GolsSofridosTime: sofridos,
                    GolsContra: golsContra.GetValueOrDefault((jogadorId, jogoId)),
                    GolDaVitoria: ContextoNotaHelper.De(doHelper, jogadorId, jogoId).GolDaVitoria);
            }

            return mapa;
        }

        // ----------------------------------------------------------------- calibração

        /// <summary>
        /// Recalcula a régua sobre todo o histórico com placar e, em seguida, mede a
        /// aderência das notas resultantes ao rating do provedor. É o passo que o
        /// Sofascore descreve como "analisamos incontáveis partidas e ajustamos a
        /// fórmula" — aqui com um alvo pronto: EstatisticaJogador.Rating.
        /// </summary>
        /// <param name="persistir">false = simulação, não grava a régua nova.</param>
        public async Task<RelatorioCalibracao> CalibrarAsync(
            string? usuarioId, bool persistir = true, CancellationToken ct = default)
        {
            var jogoIds = await _context.Jogos.AsNoTracking()
                .Where(j => j.PlacarCasa != null && j.PlacarVisitante != null)
                .Select(j => j.Id)
                .ToListAsync(ct);

            // Passo 1: amostras para média e desvio de cada (posição, métrica).
            var amostras = new List<BaselineRating.Amostra>();
            await PorLoteAsync(jogoIds, usuarioId, (estatistica, posicao, _) =>
            {
                var grupo = CriteriosNotaHelper.GrupoDaPosicao(posicao);
                if (grupo != null)
                    amostras.Add(new BaselineRating.Amostra(grupo, estatistica.Minutos ?? 0, estatistica));
            }, ct);

            var baseline = BaselineRating.Montar(amostras);
            if (persistir) await SalvarBaselineAsync(baseline, ct);

            // Passo 2: notas com a régua nova, comparadas ao rating do provedor.
            var comparacoes = new List<(string Grupo, double Nossa, double? Provedor)>();
            await PorLoteAsync(jogoIds, usuarioId, (estatistica, posicao, contexto) =>
            {
                if (RatingAutomaticoHelper.Calcular(estatistica, posicao, contexto, baseline) is not { } c) return;
                comparacoes.Add((c.Grupo ?? "(sem posição)", c.Nota, estatistica.Rating));
            }, ct);

            var porPosicao = comparacoes
                .GroupBy(c => c.Grupo)
                .Select(g => Medir(g.Key, g.ToList()))
                .OrderByDescending(a => a.Amostras)
                .ToList();

            return new RelatorioCalibracao(
                jogoIds.Count, comparacoes.Count, baseline.Grupos.Count,
                Medir("Geral", comparacoes), porPosicao, baseline);
        }

        /// <summary>Aderência da régua ATUAL, sem recalibrar nem gravar nada.</summary>
        public async Task<RelatorioCalibracao> DiagnosticarAsync(
            string? usuarioId, CancellationToken ct = default)
        {
            var baseline = await CarregarBaselineAsync(ct);
            var jogoIds = await _context.Jogos.AsNoTracking()
                .Where(j => j.PlacarCasa != null && j.PlacarVisitante != null)
                .Select(j => j.Id)
                .ToListAsync(ct);

            var comparacoes = new List<(string Grupo, double Nossa, double? Provedor)>();
            await PorLoteAsync(jogoIds, usuarioId, (estatistica, posicao, contexto) =>
            {
                if (RatingAutomaticoHelper.Calcular(estatistica, posicao, contexto, baseline) is not { } c) return;
                comparacoes.Add((c.Grupo ?? "(sem posição)", c.Nota, estatistica.Rating));
            }, ct);

            var porPosicao = comparacoes
                .GroupBy(c => c.Grupo)
                .Select(g => Medir(g.Key, g.ToList()))
                .OrderByDescending(a => a.Amostras)
                .ToList();

            return new RelatorioCalibracao(
                jogoIds.Count, comparacoes.Count, baseline.Grupos.Count,
                Medir("Geral", comparacoes), porPosicao, baseline);
        }

        // Percorre o histórico em lotes de jogos, entregando cada linha de estatística
        // com a posição e o contexto daquela partida. Em lotes porque o lado/posição
        // exige o elenco inteiro de cada jogo carregado de uma vez.
        private async Task PorLoteAsync(
            IReadOnlyList<int> jogoIds, string? usuarioId,
            Action<EstatisticaJogador, string?, ContextoRating> visitar,
            CancellationToken ct)
        {
            for (int i = 0; i < jogoIds.Count; i += TamanhoLote)
            {
                ct.ThrowIfCancellationRequested();
                var lote = jogoIds.Skip(i).Take(TamanhoLote).ToList();

                var lados = await LadoJogadorHelper.CarregarAsync(_context, lote, usuarioId, ct: ct);
                var contextos = await CarregarContextosAsync(lote, usuarioId, lados, ct);
                var estatisticas = await _context.EstatisticasJogador.AsNoTracking()
                    .Where(e => lote.Contains(e.JogoId))
                    .ToListAsync(ct);

                foreach (var e in estatisticas)
                {
                    var posicao = lados.TryGetValue((e.JogadorId, e.JogoId), out var atuacao) ? atuacao.Posicao : null;
                    visitar(e, posicao, contextos.GetValueOrDefault((e.JogadorId, e.JogoId), ContextoRating.Vazio));
                }
            }
        }

        // ------------------------------------------------------------------ estatística

        private static AderenciaRating Medir(string recorte, List<(string Grupo, double Nossa, double? Provedor)> linhas)
        {
            var nossas = linhas.Select(l => l.Nossa).ToList();

            // Correlação e erro só fazem sentido onde existe rating do provedor; a
            // distribuição das nossas notas vale para a amostra inteira.
            var pares = linhas.Where(l => l.Provedor is > 0)
                .Select(l => (Nossa: l.Nossa, Provedor: l.Provedor!.Value))
                .ToList();

            var (mediaNossa, desvioNosso) = MediaDesvio(nossas);
            var (mediaProvedor, desvioProvedor) = MediaDesvio(pares.Select(p => p.Provedor).ToList());

            return new AderenciaRating(
                recorte,
                nossas.Count,
                Math.Round(Pearson(pares), 4),
                pares.Count > 0 ? Math.Round(pares.Average(p => Math.Abs(p.Nossa - p.Provedor)), 3) : 0,
                pares.Count > 0 ? Math.Round(pares.Average(p => p.Nossa - p.Provedor), 3) : 0,
                Math.Round(mediaNossa, 3), Math.Round(desvioNosso, 3),
                Math.Round(mediaProvedor, 3), Math.Round(desvioProvedor, 3),
                Math.Round(Percentil(nossas, 0.50), 2),
                Math.Round(Percentil(nossas, 0.90), 2),
                Math.Round(Percentil(nossas, 0.99), 2),
                nossas.Count > 0 ? Math.Round(nossas.Max(), 2) : 0);
        }

        private static (double Media, double Desvio) MediaDesvio(List<double> valores)
        {
            if (valores.Count == 0) return (0, 0);
            var media = valores.Average();
            var desvio = valores.Count > 1
                ? Math.Sqrt(valores.Sum(v => (v - media) * (v - media)) / (valores.Count - 1))
                : 0;
            return (media, desvio);
        }

        private static double Pearson(List<(double Nossa, double Provedor)> pares)
        {
            if (pares.Count < 2) return 0;
            var mediaX = pares.Average(p => p.Nossa);
            var mediaY = pares.Average(p => p.Provedor);

            double covariancia = 0, varX = 0, varY = 0;
            foreach (var (x, y) in pares)
            {
                var dx = x - mediaX;
                var dy = y - mediaY;
                covariancia += dx * dy;
                varX += dx * dx;
                varY += dy * dy;
            }

            // Uma das séries constante (todo mundo com a mesma nota): não há relação
            // linear a medir, e a divisão seria por zero.
            return varX > 0 && varY > 0 ? covariancia / Math.Sqrt(varX * varY) : 0;
        }

        private static double Percentil(List<double> valores, double fracao)
        {
            if (valores.Count == 0) return 0;
            var ordenado = valores.OrderBy(v => v).ToList();
            var indice = (int)Math.Round(fracao * (ordenado.Count - 1));
            return ordenado[Math.Clamp(indice, 0, ordenado.Count - 1)];
        }
    }
}
