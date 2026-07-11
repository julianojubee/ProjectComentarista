using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class ChaveamentoMataMataBuilderTests
    {
        private static Time T(int id, string nome) => new() { Id = id, Nome = nome, Cidade = "" };

        private static Jogo J(int id, Time casa, Time visitante, int? placarCasa, int? placarVisitante,
            string grupo, DateTime? data = null, int? penaltisCasa = null, int? penaltisVisitante = null) => new()
        {
            Id = id,
            TimeCasa = casa,
            TimeCasaId = casa.Id,
            TimeVisitante = visitante,
            TimeVisitanteId = visitante.Id,
            PlacarCasa = placarCasa,
            PlacarVisitante = placarVisitante,
            Grupo = grupo,
            Data = data,
            PenaltisCasa = penaltisCasa,
            PenaltisVisitante = penaltisVisitante
        };

        [Fact]
        public void SemJogos_RetornaSemDados_ComSlotsVazios()
        {
            var vm = ChaveamentoMataMataBuilder.Construir(new List<Jogo>());

            Assert.False(vm.TemDados);
            Assert.True(vm.Final.Vazio);
            Assert.All(vm.OitavasEsq, t => Assert.True(t.Vazio));
        }

        [Fact]
        public void FinalJogoUnico_PlacarEVencedorCorretos()
        {
            var a = T(1, "A"); var b = T(2, "B");
            var vm = ChaveamentoMataMataBuilder.Construir(new List<Jogo>
            {
                J(1, a, b, 2, 1, "Final", new DateTime(2026, 7, 1)),
            });

            Assert.True(vm.TemDados);
            Assert.Equal(1, vm.Final.Lado1.Time?.Id);
            Assert.Equal(2, vm.Final.PlacarLado1);
            Assert.Equal(1, vm.Final.PlacarLado2);
            Assert.True(vm.Final.Lado1Venceu);
            Assert.False(vm.Final.Lado2Venceu);
        }

        [Fact]
        public void IdaEVolta_AgregaPlacar_EUsaMandanteDaVoltaComoLado1()
        {
            var a = T(1, "A"); var b = T(2, "B");
            var vm = ChaveamentoMataMataBuilder.Construir(new List<Jogo>
            {
                // Ida: A 2x1 B | Volta: B 2x0 A → agregado B 3x2 A
                J(1, a, b, 2, 1, "Quarter-finals", new DateTime(2026, 6, 1)),
                J(2, b, a, 2, 0, "Quarter-finals", new DateTime(2026, 6, 8)),
            });

            var quartas = vm.QuartasEsq.Concat(vm.QuartasDir)
                .Single(t => t.JogoIds.Count == 2);

            // Lado1 = mandante da perna decisiva (a volta), ou seja, B.
            Assert.Equal(2, quartas.Lado1.Time?.Id);
            Assert.Equal(3, quartas.PlacarLado1);
            Assert.Equal(2, quartas.PlacarLado2);
            Assert.True(quartas.Lado1Venceu);
        }

        [Fact]
        public void EmpateAgregado_DecideNosPenaltis()
        {
            var a = T(1, "A"); var b = T(2, "B");
            var vm = ChaveamentoMataMataBuilder.Construir(new List<Jogo>
            {
                J(1, a, b, 1, 1, "Final", new DateTime(2026, 7, 1), penaltisCasa: 4, penaltisVisitante: 2),
            });

            Assert.True(vm.Final.TevePenaltis);
            Assert.True(vm.Final.Lado1Venceu);
            Assert.False(vm.Final.Lado2Venceu);
        }

        [Fact]
        public void VencedoresDasOitavas_AlimentamAsQuartasCorretas()
        {
            var a = T(1, "A"); var b = T(2, "B"); var c = T(3, "C"); var d = T(4, "D");
            var vm = ChaveamentoMataMataBuilder.Construir(new List<Jogo>
            {
                // Oitavas: A elimina B, C elimina D.
                J(1, a, b, 1, 0, "Round of 16", new DateTime(2026, 6, 1)),
                J(2, c, d, 2, 0, "Round of 16", new DateTime(2026, 6, 2)),
                // Quartas: A x C — deve "puxar" os dois duelos de oitavas para o mesmo lado.
                J(3, a, c, 0, 0, "Quarter-finals", new DateTime(2026, 6, 10)),
            });

            var idsOitavasEsq = vm.OitavasEsq
                .Where(t => !t.Vazio)
                .Select(t => (t.Lado1.Time?.Id, t.Lado2.Time?.Id))
                .ToList();

            // Os dois duelos reais de oitavas ficam do lado esquerdo, ligados às quartas A x C.
            Assert.Contains(((int?)1, (int?)2), idsOitavasEsq);
            Assert.Contains(((int?)3, (int?)4), idsOitavasEsq);
            Assert.Equal(1, vm.QuartasEsq[0].Lado1.Time?.Id);
            Assert.Equal(3, vm.QuartasEsq[0].Lado2.Time?.Id);
        }
    }
}
