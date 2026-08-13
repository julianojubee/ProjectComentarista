using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    using ContextoNota = CriteriosNotaHelper.ContextoNota;
    using LadoPorJogadorJogo = IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo>;

    // Monta o CriteriosNotaHelper.ContextoNota de cada (jogador, jogo): minutos em
    // campo, "goleiro decisivo" (pegou 100% do que o adversário mandou no alvo) e
    // autoria do gol da vitória.
    //
    // Fica fora do CriteriosNotaHelper porque, ao contrário do resto do cálculo, isto
    // não sai da linha do próprio jogador: as finalizações no alvo são a soma das
    // estatísticas do OUTRO time, e o gol decisivo depende de todos os gols da partida.
    // Quem consome recebe um mapa pronto, no mesmo formato do LadoJogadorHelper.
    public static class ContextoNotaHelper
    {
        public static readonly IReadOnlyDictionary<(int JogadorId, int JogoId), ContextoNota> Vazio =
            new Dictionary<(int, int), ContextoNota>();

        // Projeções mínimas: só o que o contexto precisa, para não puxar as linhas
        // inteiras de todo o elenco dos jogos consultados.
        public readonly record struct LinhaEstatistica(int JogadorId, int JogoId, int? Minutos, int Defesas, int FinalizacoesNoGol);
        public readonly record struct LinhaGol(int Id, int JogadorId, int JogoId, int Minuto, bool Contra);
        public readonly record struct LinhaPlacar(int JogoId, int? PlacarCasa, int? PlacarVisitante);

        public static Dictionary<(int JogadorId, int JogoId), ContextoNota> Montar(
            IEnumerable<LinhaEstatistica> estatisticas,
            LadoPorJogadorJogo? lados,
            IEnumerable<LinhaGol>? gols = null,
            IEnumerable<LinhaPlacar>? placares = null)
        {
            var linhas = estatisticas.ToList();

            // Finalizações no alvo de cada lado, por jogo — a base do aproveitamento
            // do goleiro. Sem escalação não dá para saber de que lado veio o chute,
            // então essas linhas ficam de fora da soma.
            var alvoPorLado = new Dictionary<(int JogoId, bool IsTimeCasa), int>();
            foreach (var l in linhas)
            {
                if (lados == null || !lados.TryGetValue((l.JogadorId, l.JogoId), out var atuacao)) continue;
                var chave = (l.JogoId, atuacao.IsTimeCasa);
                alvoPorLado[chave] = alvoPorLado.GetValueOrDefault(chave) + l.FinalizacoesNoGol;
            }

            var autoresDoGolDaVitoria = AutoresDoGolDaVitoria(gols, placares, lados);

            var mapa = new Dictionary<(int JogadorId, int JogoId), ContextoNota>();
            foreach (var l in linhas)
                mapa[(l.JogadorId, l.JogoId)] = new ContextoNota(
                    Minutos: l.Minutos,
                    GoleiroDecisivo: EhGoleiroDecisivo(l, lados, alvoPorLado),
                    GolDaVitoria: autoresDoGolDaVitoria.Contains((l.JogadorId, l.JogoId)));

            // Quem marcou mas não tem linha de estatística importada ainda precisa
            // aparecer no mapa, senão o bônus se perde.
            foreach (var chave in autoresDoGolDaVitoria)
                if (!mapa.ContainsKey(chave))
                    mapa[chave] = new ContextoNota(GolDaVitoria: true);

            return mapa;
        }

        // Goleiro decisivo: jogou, defendeu e o adversário não passou por ele nenhuma
        // vez. Exige que o adversário tenha finalizado no alvo — sem chute no gol não
        // há o que defender, e um jogo com o dado zerado no banco (importação antiga)
        // daria o bônus de graça.
        private static bool EhGoleiroDecisivo(
            LinhaEstatistica l,
            LadoPorJogadorJogo? lados,
            IReadOnlyDictionary<(int JogoId, bool IsTimeCasa), int> alvoPorLado)
        {
            if (l.Minutos is not > 0 || l.Defesas <= 0) return false;
            if (lados == null || !lados.TryGetValue((l.JogadorId, l.JogoId), out var atuacao)) return false;
            if (CriteriosNotaHelper.GrupoDaPosicao(atuacao.Posicao) != "GOLEIRO") return false;

            var noAlvoAdversario = alvoPorLado.GetValueOrDefault((l.JogoId, !atuacao.IsTimeCasa));
            return noAlvoAdversario > 0 && l.Defesas >= noAlvoAdversario;
        }

        // Quem marcou o gol que decidiu cada partida.
        //
        // Num jogo que terminou N x M a favor, é o (M+1)-ésimo gol do vencedor em ordem
        // de minuto: o que colocou o time à frente para não ser mais alcançado. Em 1x0
        // é o primeiro; em 3x1, o segundo. Empate não tem gol da vitória.
        private static HashSet<(int JogadorId, int JogoId)> AutoresDoGolDaVitoria(
            IEnumerable<LinhaGol>? gols, IEnumerable<LinhaPlacar>? placares, LadoPorJogadorJogo? lados)
        {
            var autores = new HashSet<(int, int)>();
            if (gols == null || placares == null || lados == null) return autores;

            var golsPorJogo = gols.GroupBy(g => g.JogoId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var placar in placares)
            {
                if (placar.PlacarCasa is not int casa || placar.PlacarVisitante is not int visitante) continue;
                if (casa == visitante) continue;   // empate não decide nada

                bool vencedorEhCasa = casa > visitante;
                int golsDoVencedor = Math.Max(casa, visitante);
                int golsDoPerdedor = Math.Min(casa, visitante);

                if (!golsPorJogo.TryGetValue(placar.JogoId, out var doJogo)) continue;

                // Gols A FAVOR do vencedor: os dele próprio mais os contra marcados
                // pelo adversário. Sem escalação não dá para saber o lado do autor —
                // a linha fica de fora e a checagem de consistência abaixo reprova.
                var aFavor = new List<LinhaGol>();
                bool ladoDesconhecido = false;
                foreach (var g in doJogo)
                {
                    if (!lados.TryGetValue((g.JogadorId, g.JogoId), out var atuacao)) { ladoDesconhecido = true; continue; }
                    // Gol contra conta para o outro lado.
                    bool contaParaCasa = g.Contra ? !atuacao.IsTimeCasa : atuacao.IsTimeCasa;
                    if (contaParaCasa == vencedorEhCasa) aFavor.Add(g);
                }

                // Só decide com a série completa: faltando gol registrado, o
                // (M+1)-ésimo apontaria para o jogador errado.
                if (ladoDesconhecido || aFavor.Count != golsDoVencedor) continue;

                // Minuto e, no desempate, ordem de inserção — determinístico para dois
                // gols no mesmo minuto.
                var decisivo = aFavor.OrderBy(g => g.Minuto).ThenBy(g => g.Id).ElementAt(golsDoPerdedor);

                // Gol contra do adversário decidiu o jogo: ninguém ganha o bônus.
                if (decisivo.Contra) continue;

                autores.Add((decisivo.JogadorId, decisivo.JogoId));
            }

            return autores;
        }

        /// <param name="lados">
        /// Mapa do LadoJogadorHelper cobrindo o ELENCO INTEIRO desses jogos. Um mapa
        /// filtrado por jogador some com as finalizações e os gols dos outros, e tanto
        /// o aproveitamento do goleiro quanto o gol da vitória saem errados — nesse
        /// caso passe null e deixe carregar aqui.
        /// </param>
        public static async Task<Dictionary<(int JogadorId, int JogoId), ContextoNota>> CarregarAsync(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            LadoPorJogadorJogo? lados = null,
            CancellationToken ct = default)
        {
            if (jogoIds.Count == 0) return new();
            lados ??= await LadoJogadorHelper.CarregarAsync(context, jogoIds, usuarioId, ct: ct);
            return Montar(
                await ConsultaEstatisticas(context, jogoIds).ToListAsync(ct),
                lados,
                await ConsultaGols(context, jogoIds).ToListAsync(ct),
                await ConsultaPlacares(context, jogoIds).ToListAsync(ct));
        }

        /// <inheritdoc cref="CarregarAsync"/>
        public static Dictionary<(int JogadorId, int JogoId), ContextoNota> Carregar(
            FutebolContext context,
            IReadOnlyCollection<int> jogoIds,
            string? usuarioId,
            LadoPorJogadorJogo? lados = null)
        {
            if (jogoIds.Count == 0) return new();
            lados ??= LadoJogadorHelper.Carregar(context, jogoIds, usuarioId);
            return Montar(
                ConsultaEstatisticas(context, jogoIds).ToList(),
                lados,
                ConsultaGols(context, jogoIds).ToList(),
                ConsultaPlacares(context, jogoIds).ToList());
        }

        // Sem filtro por jogador: as finalizações no alvo do adversário e a série de
        // gols da partida só fecham com o elenco inteiro dos jogos consultados.
        private static IQueryable<LinhaEstatistica> ConsultaEstatisticas(FutebolContext context, IReadOnlyCollection<int> jogoIds) =>
            context.EstatisticasJogador
                .AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId))
                .Select(e => new LinhaEstatistica(e.JogadorId, e.JogoId, e.Minutos, e.Defesas, e.FinalizacoesNoGol));

        private static IQueryable<LinhaGol> ConsultaGols(FutebolContext context, IReadOnlyCollection<int> jogoIds) =>
            context.Gols
                .AsNoTracking()
                .Where(g => jogoIds.Contains(g.JogoId))
                .Select(g => new LinhaGol(g.Id, g.JogadorId, g.JogoId, g.Minuto, g.Contra));

        private static IQueryable<LinhaPlacar> ConsultaPlacares(FutebolContext context, IReadOnlyCollection<int> jogoIds) =>
            context.Jogos
                .AsNoTracking()
                .Where(j => jogoIds.Contains(j.Id))
                .Select(j => new LinhaPlacar(j.Id, j.PlacarCasa, j.PlacarVisitante));

        public static ContextoNota De(
            IReadOnlyDictionary<(int JogadorId, int JogoId), ContextoNota>? mapa, int jogadorId, int jogoId)
            => mapa != null && mapa.TryGetValue((jogadorId, jogoId), out var c) ? c : ContextoNota.Vazio;
    }
}
