using ControleFutebolWeb.Helpers;
using Xunit;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class TreinadorHistoricoNormalizadorTests
    {
        private static (DateTime? Inicio, DateTime? Fim) P(string inicio, string? fim = null)
            => (DateTime.Parse(inicio), fim == null ? null : DateTime.Parse(fim));

        [Fact]
        public void PassagemMaisRecenteAberta_ContinuaAberta()
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new[]
            {
                P("2026-04-01"),              // atual
                P("2025-05-01", "2026-03-01")
            });

            Assert.Null(fins[0]);
            Assert.Equal(new DateTime(2026, 3, 1), fins[1]);
        }

        // O caso real: Fernando Diniz aparecia dirigindo Corinthians e Vasco ao mesmo tempo.
        [Fact]
        public void PassagemAntigaAberta_FechaNoInicioDaSeguinte()
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new[]
            {
                P("2026-04-01"),   // Corinthians — atual
                P("2025-05-01"),   // Vasco — a API não fechou
                P("2024-09-01", "2025-01-01")
            });

            Assert.Null(fins[0]);
            Assert.Equal(new DateTime(2026, 4, 1), fins[1]);
            Assert.Equal(new DateTime(2025, 1, 1), fins[2]);
        }

        [Fact]
        public void VariasAbertasSeguidas_CadaUmaFechaNaSeguinte()
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new[]
            {
                P("2026-04-01"),
                P("2025-05-01"),
                P("2024-09-01")
            });

            Assert.Null(fins[0]);
            Assert.Equal(new DateTime(2026, 4, 1), fins[1]);
            Assert.Equal(new DateTime(2025, 5, 1), fins[2]);
        }

        [Fact]
        public void FimJaPreenchido_NaoEhAlterado()
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new[]
            {
                P("2026-04-01"),
                P("2025-05-01", "2025-12-31")
            });

            Assert.Equal(new DateTime(2025, 12, 31), fins[1]);
        }

        [Fact]
        public void SemInicioNaSeguinte_DeixaAbertaEmVezDeInventarData()
        {
            var fins = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new (DateTime?, DateTime?)[]
            {
                (null, null),      // início desconhecido
                (DateTime.Parse("2025-05-01"), null)
            });

            Assert.Null(fins[1]);
        }

        [Fact]
        public void ListaVaziaOuUnica_NaoQuebra()
        {
            Assert.Empty(TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(
                Array.Empty<(DateTime?, DateTime?)>()));

            var uma = TreinadorHistoricoNormalizador.FecharPassagensAbertasAntigas(new[] { P("2026-04-01") });
            Assert.Single(uma);
            Assert.Null(uma[0]);
        }
    }
}
