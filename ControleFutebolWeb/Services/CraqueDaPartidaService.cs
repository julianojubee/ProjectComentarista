// ControleFutebolWeb/Services/CraqueDaPartidaService.cs
// Eleição do craque da partida (a coroa) e sua persistência em CraqueDaPartida.
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Elege e grava o craque de cada partida — o jogador de maior nota no jogo, pela
    /// régua do usuário (ver <see cref="CraqueDaPartida"/> para o porquê de ser por usuário).
    ///
    /// A nota de cada jogador sai exatamente da mesma cadeia que os relatórios usam
    /// (nota manual &gt; nota do usuário pelas ações &gt; nota automática do motor
    /// escolhido em /CriteriosNota). Duplicar essa conta aqui faria a coroa pousar num
    /// jogador e o ranking de notas apontar outro na mesma partida.
    /// </summary>
    public class CraqueDaPartidaService
    {
        private readonly FutebolContext _context;

        public CraqueDaPartidaService(FutebolContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Craque de cada jogo pedido, pelo usuário. Os jogos que ainda não têm eleição
        /// gravada são calculados e gravados aqui (é o caminho que preenche a tabela para
        /// o histórico inteiro, sem precisar de backfill em migração).
        /// </summary>
        public async Task<Dictionary<int, CraqueDaPartida>> ObterAsync(
            IReadOnlyCollection<int> jogoIds, string usuarioId, CancellationToken ct = default)
        {
            if (jogoIds.Count == 0 || string.IsNullOrEmpty(usuarioId)) return new();

            var gravados = await _context.CraquesDaPartida
                .Where(c => c.UsuarioId == usuarioId && jogoIds.Contains(c.JogoId))
                .ToDictionaryAsync(c => c.JogoId, ct);

            var faltando = jogoIds.Where(id => !gravados.ContainsKey(id)).ToList();
            if (faltando.Count > 0)
                foreach (var novo in await RecalcularAsync(faltando, usuarioId, ct))
                    gravados[novo.JogoId] = novo;

            return gravados;
        }

        /// <summary>Craque de um jogo só (calcula e grava se ainda não existir).</summary>
        public async Task<CraqueDaPartida?> ObterDoJogoAsync(int jogoId, string usuarioId, CancellationToken ct = default)
            => (await ObterAsync(new[] { jogoId }, usuarioId, ct)).GetValueOrDefault(jogoId);

        /// <summary>
        /// Refaz a eleição destes jogos para o usuário, sobrescrevendo o que estiver
        /// gravado. É o que roda depois de salvar uma nota: a coroa pode ter mudado de
        /// jogador ou o eleito pode ter deixado de existir (jogo sem nenhuma nota nem
        /// estatística), e nesse caso a linha é apagada.
        /// </summary>
        public async Task<List<CraqueDaPartida>> RecalcularAsync(
            IReadOnlyCollection<int> jogoIds, string usuarioId, CancellationToken ct = default)
        {
            if (jogoIds.Count == 0 || string.IsNullOrEmpty(usuarioId)) return new();

            var eleitos = await ElegerAsync(jogoIds, usuarioId, ct);

            var antigos = await _context.CraquesDaPartida
                .Where(c => c.UsuarioId == usuarioId && jogoIds.Contains(c.JogoId))
                .ToListAsync(ct);
            _context.CraquesDaPartida.RemoveRange(antigos);

            var novos = eleitos.Values.ToList();
            _context.CraquesDaPartida.AddRange(novos);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Corrida com outra requisição que gravou a mesma eleição (o índice único
                // de jogo+usuário barrou a segunda). O resultado das duas é o mesmo, então
                // vale a que chegou primeiro: descarta o rascunho e devolve o que está no banco.
                foreach (var entrada in _context.ChangeTracker.Entries<CraqueDaPartida>().ToList())
                    entrada.State = EntityState.Detached;
                return await _context.CraquesDaPartida.AsNoTracking()
                    .Where(c => c.UsuarioId == usuarioId && jogoIds.Contains(c.JogoId))
                    .ToListAsync(ct);
            }

            return novos;
        }

        /// <inheritdoc cref="RecalcularAsync(IReadOnlyCollection{int}, string, CancellationToken)"/>
        public Task<List<CraqueDaPartida>> RecalcularJogoAsync(int jogoId, string usuarioId, CancellationToken ct = default)
            => RecalcularAsync(new[] { jogoId }, usuarioId, ct);

        /// <summary>
        /// Apaga a eleição de um jogo para TODOS os usuários, sem recalcular. É o que se
        /// chama quando as estatísticas importadas do jogo mudam: elas alimentam a nota
        /// automática de quem não avaliou à mão, então a coroa de todo mundo ficou
        /// suspeita — mas recalcular usuário por usuário aqui custaria caro à importação.
        /// A eleição se refaz sozinha na próxima leitura de cada um (ver ObterAsync).
        /// </summary>
        public static async Task InvalidarJogoAsync(FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var antigos = await context.CraquesDaPartida.Where(c => c.JogoId == jogoId).ToListAsync(ct);
            if (antigos.Count == 0) return;
            context.CraquesDaPartida.RemoveRange(antigos);
            await context.SaveChangesAsync(ct);
        }

        /// <inheritdoc cref="InvalidarJogoAsync(FutebolContext, int, CancellationToken)"/>
        public Task InvalidarJogoAsync(int jogoId, CancellationToken ct = default)
            => InvalidarJogoAsync(_context, jogoId, ct);

        // ── Eleição ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Um candidato à coroa: a nota que ele tirou no jogo e os números que decidem
        /// o desempate. Ver <see cref="Ordenar"/> para a ordem em que são aplicados.
        /// </summary>
        private sealed class Candidato
        {
            public int JogadorId { get; init; }
            public double Nota { get; set; }
            public int Gols { get; set; }
            public int Assistencias { get; set; }
            public int ChancesCriadas { get; set; }
            public int Defesas { get; set; }
            public int FinalizacoesNoGol { get; set; }
            public int AcoesDefensivas { get; set; }
            public int DuelosVencidos { get; set; }
            public int Cartoes { get; set; }
            public int Minutos { get; set; }
            public bool TimeVenceu { get; set; }

            /// <summary>Participou do gol de ponta a ponta (marcou E deu assistência).</summary>
            public bool GolEAssistencia => Gols > 0 && Assistencias > 0;
        }

        private async Task<Dictionary<int, CraqueDaPartida>> ElegerAsync(
            IReadOnlyCollection<int> jogoIds, string usuarioId, CancellationToken ct)
        {
            var jogos = await _context.Jogos.AsNoTracking()
                .Where(j => jogoIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, ct);
            if (jogos.Count == 0) return new();

            var idsValidos = jogos.Keys.ToList();

            var notas = await _context.Notas.AsNoTracking()
                .Include(n => n.Detalhes)
                .Where(n => idsValidos.Contains(n.JogoId) && n.UsuarioId == usuarioId)
                .ToListAsync(ct);

            // Mesmo filtro de "entrou em campo" dos relatórios: a api-football cria linha
            // para todo relacionado, inclusive quem ficou no banco o jogo inteiro.
            var estatisticas = await _context.EstatisticasJogador.AsNoTracking()
                .Include(e => e.Jogo)
                .Where(e => idsValidos.Contains(e.JogoId) && e.Minutos != null && e.Minutos > 0)
                .ToListAsync(ct);

            var gols = await _context.Gols.AsNoTracking()
                .Where(g => idsValidos.Contains(g.JogoId) && !g.Contra)
                .Select(g => new { g.JogoId, g.JogadorId })
                .ToListAsync(ct);

            var assistencias = await _context.Assistencias.AsNoTracking()
                .Where(a => idsValidos.Contains(a.JogoId))
                .Select(a => new { a.JogoId, a.JogadorId })
                .ToListAsync(ct);

            var cartoes = await _context.Cartoes.AsNoTracking()
                .Where(c => idsValidos.Contains(c.JogoId))
                .Select(c => new { c.JogoId, c.JogadorId })
                .ToListAsync(ct);

            var criterios = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(ct),
                await _context.CriteriosNota.Where(c => c.UsuarioId == usuarioId).ToListAsync(ct));

            var lados = await LadoJogadorHelper.CarregarAsync(_context, idsValidos, usuarioId, ct: ct);
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, idsValidos, usuarioId, lados, ct);
            var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                _context, idsValidos, usuarioId, criterios, lados, contextos, ct);

            var golsPorJogadorJogo = Contar(gols.Select(g => (g.JogoId, g.JogadorId)));
            var assistsPorJogadorJogo = Contar(assistencias.Select(a => (a.JogoId, a.JogadorId)));
            var cartoesPorJogadorJogo = Contar(cartoes.Select(c => (c.JogoId, c.JogadorId)));

            var estatsPorJogadorJogo = estatisticas
                .GroupBy(e => (e.JogoId, e.JogadorId))
                .ToDictionary(g => g.Key, g => g.ToList());

            var notasPorJogadorJogo = notas
                .GroupBy(n => (n.JogoId, n.JogadorId))
                .ToDictionary(g => g.Key, g => g.ToList());

            var resultado = new Dictionary<int, CraqueDaPartida>();
            var agora = DateTime.UtcNow;

            foreach (var jogoId in idsValidos)
            {
                var jogo = jogos[jogoId];

                var candidatosIds = notasPorJogadorJogo.Keys.Where(k => k.JogoId == jogoId).Select(k => k.JogadorId)
                    .Concat(estatsPorJogadorJogo.Keys.Where(k => k.JogoId == jogoId).Select(k => k.JogadorId))
                    .Distinct()
                    .ToList();

                var candidatos = new List<Candidato>();
                foreach (var jogadorId in candidatosIds)
                {
                    notasPorJogadorJogo.TryGetValue((jogoId, jogadorId), out var notasDele);
                    estatsPorJogadorJogo.TryGetValue((jogoId, jogadorId), out var estatsDele);

                    double? nota = NotaDoJogo(jogadorId, jogoId, notasDele, estatsDele,
                        criterios, contextos, calculadora);
                    if (nota == null) continue;

                    var candidato = new Candidato { JogadorId = jogadorId, Nota = nota.Value };
                    Preencher(candidato, estatsDele);

                    // Os lances registrados na partida (tabelas Gol/Assistencia/Cartao)
                    // mandam sobre a estatística importada quando divergem: são os que a
                    // tela do jogo mostra e são editáveis à mão. Nem toda fonte publica
                    // gol e assistência por jogador, então fica o maior dos dois em vez de
                    // o lance ausente apagar uma estatística que existe.
                    candidato.Gols = Math.Max(candidato.Gols, golsPorJogadorJogo.GetValueOrDefault((jogoId, jogadorId)));
                    candidato.Assistencias = Math.Max(candidato.Assistencias, assistsPorJogadorJogo.GetValueOrDefault((jogoId, jogadorId)));
                    candidato.Cartoes = Math.Max(candidato.Cartoes, cartoesPorJogadorJogo.GetValueOrDefault((jogoId, jogadorId)));

                    if (lados.TryGetValue((jogadorId, jogoId), out var atuacao)
                        && jogo.PlacarCasa.HasValue && jogo.PlacarVisitante.HasValue)
                    {
                        candidato.TimeVenceu = atuacao.IsTimeCasa
                            ? jogo.PlacarCasa > jogo.PlacarVisitante
                            : jogo.PlacarVisitante > jogo.PlacarCasa;
                    }

                    candidatos.Add(candidato);
                }

                if (candidatos.Count == 0) continue;

                var craque = Ordenar(candidatos).First();

                resultado[jogoId] = new CraqueDaPartida
                {
                    JogoId = jogoId,
                    JogadorId = craque.JogadorId,
                    UsuarioId = usuarioId,
                    Nota = Math.Round(craque.Nota, 2),
                    Gols = craque.Gols,
                    Assistencias = craque.Assistencias,
                    ChancesCriadas = craque.ChancesCriadas,
                    Empatados = candidatos.Count(c => Math.Round(c.Nota, 2) == Math.Round(craque.Nota, 2)),
                    CalculadoEm = agora,
                };
            }

            return resultado;
        }

        /// <summary>
        /// A ordem de desempate, do critério mais forte para o mais fraco: nota, gols,
        /// participação completa no gol (marcou E deu assistência), assistências e
        /// chances criadas. Daí para baixo é o que separa quem fez o jogo de quem só
        /// esteve nele — defesas (o goleiro que segurou o resultado), finalizações no
        /// alvo, trabalho defensivo, duelos —, e o fim da fila é cartão, vitória do time
        /// e minutos em campo. O JogadorId fecha a ordenação para a mesma partida eleger
        /// sempre o mesmo craque, em vez de sortear a cada recálculo.
        /// </summary>
        private static IOrderedEnumerable<Candidato> Ordenar(IEnumerable<Candidato> candidatos) =>
            candidatos
                .OrderByDescending(c => Math.Round(c.Nota, 2))
                .ThenByDescending(c => c.Gols)
                .ThenByDescending(c => c.GolEAssistencia)
                .ThenByDescending(c => c.Assistencias)
                .ThenByDescending(c => c.ChancesCriadas)
                .ThenByDescending(c => c.Defesas)
                .ThenByDescending(c => c.FinalizacoesNoGol)
                .ThenByDescending(c => c.AcoesDefensivas)
                .ThenByDescending(c => c.DuelosVencidos)
                .ThenBy(c => c.Cartoes)
                .ThenByDescending(c => c.TimeVenceu)
                .ThenByDescending(c => c.Minutos)
                .ThenBy(c => c.JogadorId);

        private static void Preencher(Candidato candidato, List<EstatisticaJogador>? estats)
        {
            if (estats == null || estats.Count == 0) return;

            // Mais de uma linha para o mesmo jogador só acontece quando duas fontes
            // cobriram a partida (api-football e ESPN, por exemplo). Cada campo fica com
            // o maior valor visto: as fontes não cobrem os mesmos itens, e um zero de "a
            // fonte não informa" não pode apagar o número da fonte que informa.
            candidato.Gols = estats.Max(e => e.Gols);
            candidato.Assistencias = estats.Max(e => e.Assistencias);
            candidato.ChancesCriadas = estats.Max(e => e.PassesChave);
            candidato.Defesas = estats.Max(e => e.Defesas);
            candidato.FinalizacoesNoGol = estats.Max(e => e.FinalizacoesNoGol);
            candidato.AcoesDefensivas = estats.Max(e => e.Desarmes + e.Interceptacoes + e.Bloqueios);
            candidato.DuelosVencidos = estats.Max(e => e.DuelosVencidos);
            candidato.Cartoes = estats.Max(e => e.CartoesAmarelos + e.CartoesVermelhos);
            candidato.Minutos = estats.Max(e => e.Minutos ?? 0);
        }

        /// <summary>
        /// Nota final do jogador naquele jogo — a mesma cadeia de RelatoriosService:
        /// nota manual (override absoluto) &gt; nota do usuário pelas ações &gt; nota
        /// automática das estatísticas. Null quando não há nenhuma das três.
        /// </summary>
        private static double? NotaDoJogo(
            int jogadorId, int jogoId,
            List<Nota>? notas, List<EstatisticaJogador>? estats,
            IReadOnlyList<CriterioNota> criterios,
            IReadOnlyDictionary<(int JogadorId, int JogoId), CriteriosNotaHelper.ContextoNota> contextos,
            CalculadoraNotaAutomatica calculadora)
        {
            if (notas != null && notas.Count > 0)
            {
                var manuais = notas.Where(n => n.NotaManual.HasValue).ToList();
                if (manuais.Count > 0)
                    return Math.Round(Math.Max(0, Math.Min(10, manuais.Average(n => n.NotaManual!.Value))), 2);

                var ctx = ContextoNotaHelper.De(contextos, jogadorId, jogoId);
                var detalhes = notas.SelectMany(n => n.Detalhes).ToList();
                return CriteriosNotaHelper.NotaFinal(notas.Average(n => n.Valor), criterios,
                    ctx with { Acoes = CriteriosNotaHelper.ContarAcoes(detalhes) });
            }

            if (estats != null && estats.Count > 0)
                return calculadora.De(estats, jogadorId, jogoId).Nota;

            return null;
        }

        private static Dictionary<(int JogoId, int JogadorId), int> Contar(
            IEnumerable<(int JogoId, int JogadorId)> lances) =>
            lances.GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());
    }
}
