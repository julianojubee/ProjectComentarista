using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Monta um chaveamento (mata-mata) em árvore de dois lados a partir dos jogos
    /// importados — oitavas → quartas → semifinais → final. Funciona para qualquer
    /// competição cujo campo Grupo guarde o round da API ("Round of 16", "Quarter-finals"…).
    ///
    /// • Confrontos de ida e volta são agregados num único duelo (placar somado).
    /// • A posição de cada duelo no bracket é inferida pelos vencedores: o duelo de
    ///   quartas que contém os vencedores de dois duelos de oitavas liga esses dois.
    /// • Enquanto as fases seguintes não existem, os slots ficam vazios ("A definir")
    ///   e os duelos da primeira fase aparecem em ordem estável (data → menor jogo).
    /// </summary>
    public static class ChaveamentoMataMataBuilder
    {
        public static ChaveamentoArvoreViewModel Construir(List<Jogo> jogosMataMata)
        {
            List<ConfrontoArvore> TiesDaFase(string chave, string rotulo)
            {
                var daFase = jogosMataMata
                    .Where(j => ChaveamentoCopaBuilder.NormalizarFase(j.Grupo) == chave)
                    .ToList();
                return AgruparTies(daFase, rotulo)
                    .OrderBy(t => t.Data ?? DateTime.MaxValue)
                    .ThenBy(t => t.JogoIds.Count > 0 ? t.JogoIds.Min() : int.MaxValue)
                    .ToList();
            }

            var poolR16 = TiesDaFase("R16", "Oitavas");
            var poolQF = TiesDaFase("QF", "Quartas");
            var poolSF = TiesDaFase("SF", "Semifinal");
            var tiesF = TiesDaFase("F", "Final");

            var vm = new ChaveamentoArvoreViewModel
            {
                TemDados = poolR16.Any() || poolQF.Any() || poolSF.Any() || tiesF.Any()
            };

            var finalTie = tiesF.FirstOrDefault() ?? Vazio("Final");

            // Semifinais que alimentam a final.
            var (semiL, semiR) = Feeders(finalTie, poolSF);
            semiL ??= Take(poolSF, "Semifinal");
            semiR ??= Take(poolSF, "Semifinal");

            // Quartas que alimentam cada semifinal.
            var (qLt, qLb) = Feeders(semiL, poolQF);
            qLt ??= Take(poolQF, "Quartas");
            qLb ??= Take(poolQF, "Quartas");
            var (qRt, qRb) = Feeders(semiR, poolQF);
            qRt ??= Take(poolQF, "Quartas");
            qRb ??= Take(poolQF, "Quartas");

            // Oitavas que alimentam cada duelo de quartas.
            var (o1, o2) = Feeders(qLt, poolR16);
            o1 ??= Take(poolR16, "Oitavas");
            o2 ??= Take(poolR16, "Oitavas");
            var (o3, o4) = Feeders(qLb, poolR16);
            o3 ??= Take(poolR16, "Oitavas");
            o4 ??= Take(poolR16, "Oitavas");
            var (o5, o6) = Feeders(qRt, poolR16);
            o5 ??= Take(poolR16, "Oitavas");
            o6 ??= Take(poolR16, "Oitavas");
            var (o7, o8) = Feeders(qRb, poolR16);
            o7 ??= Take(poolR16, "Oitavas");
            o8 ??= Take(poolR16, "Oitavas");

            vm.OitavasEsq = new List<ConfrontoArvore> { o1, o2, o3, o4 };
            vm.OitavasDir = new List<ConfrontoArvore> { o5, o6, o7, o8 };
            vm.QuartasEsq = new List<ConfrontoArvore> { qLt, qLb };
            vm.QuartasDir = new List<ConfrontoArvore> { qRt, qRb };
            vm.SemiEsq = semiL;
            vm.SemiDir = semiR;
            vm.Final = finalTie;

            return vm;
        }

        // Retorna os dois duelos da fase anterior cujos vencedores são os lados de `tie`.
        private static (ConfrontoArvore?, ConfrontoArvore?) Feeders(ConfrontoArvore tie, List<ConfrontoArvore> pool)
        {
            var f1 = TomarVencedor(pool, tie.Lado1.Time);
            var f2 = TomarVencedor(pool, tie.Lado2.Time);
            return (f1, f2);
        }

        private static ConfrontoArvore? TomarVencedor(List<ConfrontoArvore> pool, Time? time)
        {
            if (time == null) return null;
            var f = pool.FirstOrDefault(t => VencedorId(t) == time.Id);
            if (f != null) pool.Remove(f);
            return f;
        }

        private static int? VencedorId(ConfrontoArvore t)
        {
            if (t.Lado1Venceu) return t.Lado1.Time?.Id;
            if (t.Lado2Venceu) return t.Lado2.Time?.Id;
            return null;
        }

        private static ConfrontoArvore Take(List<ConfrontoArvore> pool, string rotulo)
        {
            if (pool.Count > 0)
            {
                var t = pool[0];
                pool.RemoveAt(0);
                return t;
            }
            return Vazio(rotulo);
        }

        private static ConfrontoArvore Vazio(string rotulo) => new() { Rotulo = rotulo };

        // Agrupa as pernas (ida/volta) de cada confronto e calcula o placar agregado.
        private static List<ConfrontoArvore> AgruparTies(List<Jogo> jogos, string rotulo)
        {
            var ties = new List<ConfrontoArvore>();

            var grupos = jogos.GroupBy(j =>
            {
                int a = Math.Min(j.TimeCasaId, j.TimeVisitanteId);
                int b = Math.Max(j.TimeCasaId, j.TimeVisitanteId);
                return (a, b);
            });

            foreach (var g in grupos)
            {
                var legs = g.OrderBy(j => j.Data ?? DateTime.MaxValue).ToList();
                var decider = legs.Last();          // perna que decide (a mais recente)
                int lado1Id = decider.TimeCasaId;
                int lado2Id = decider.TimeVisitanteId;

                bool realizado = legs.All(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue);

                int? ag1 = null, ag2 = null;
                if (realizado)
                {
                    ag1 = 0;
                    ag2 = 0;
                    foreach (var j in legs)
                    {
                        ag1 += j.TimeCasaId == lado1Id ? j.PlacarCasa : j.PlacarVisitante;
                        ag2 += j.TimeCasaId == lado2Id ? j.PlacarCasa : j.PlacarVisitante;
                    }
                }

                ties.Add(new ConfrontoArvore
                {
                    Lado1 = new SlotChaveamento { Time = decider.TimeCasa, Rotulo = decider.TimeCasa?.Nome ?? "" },
                    Lado2 = new SlotChaveamento { Time = decider.TimeVisitante, Rotulo = decider.TimeVisitante?.Nome ?? "" },
                    PlacarLado1 = ag1,
                    PlacarLado2 = ag2,
                    PenaltisLado1 = decider.PenaltisCasa,
                    PenaltisLado2 = decider.PenaltisVisitante,
                    Data = decider.Data,
                    JogoIds = legs.Select(j => j.Id).ToList(),
                    Rotulo = rotulo
                });
            }

            return ties;
        }
    }
}
