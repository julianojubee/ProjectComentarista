using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Helpers.Rating;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    // A ESPN é fonte parcial: publica gols, finalizações, faltas, cartões e defesas,
    // mas não passes, desarmes, interceptações, duelos, dribles nem pênaltis. Como as
    // colunas são int, o que ela não manda fica gravado como 0 — e zero de "não
    // desarmou" tem que continuar diferente de zero de "a fonte não informa".
    public class FonteEstatisticaTests
    {
        [Theory]
        [InlineData("gol")]
        [InlineData("finalizacao_gol")]
        [InlineData("cartao_amarelo")]
        [InlineData("defesa")]
        [InlineData("faltas_cometidas")]
        [InlineData("evento_gol")]
        public void Espn_CobreOQuePublica(string id)
        {
            Assert.True(FonteEstatistica.Cobre(FonteEstatistica.Espn, id));
        }

        [Theory]
        [InlineData("desarme")]
        [InlineData("interceptacao")]
        [InlineData("duelo_vencido")]
        [InlineData("passe_chave")]
        [InlineData("penalti_defendido")]
        [InlineData("passes_precisao")]
        [InlineData("duelos")]
        [InlineData("evento_penalti_perdido")]
        public void Espn_NaoCobreOQueNaoPublica(string id)
        {
            Assert.False(FonteEstatistica.Cobre(FonteEstatistica.Espn, id));
        }

        // Métrica nova entra como não coberta até alguém conferir a ESPN: descartar
        // dado só tira base da nota, enquanto o contrário inventa desempenho.
        [Fact]
        public void Espn_MetricaDesconhecida_NaoEhCoberta()
        {
            Assert.False(FonteEstatistica.Cobre(FonteEstatistica.Espn, "metrica_que_ninguem_conferiu"));
        }

        [Theory]
        [InlineData(FonteEstatistica.ApiFootball)]
        [InlineData("")]
        [InlineData(null)]
        public void ApiFootballEDesconhecida_CobremTudo(string? fonte)
        {
            Assert.True(FonteEstatistica.Cobre(fonte, "desarme"));
            Assert.True(FonteEstatistica.Cobre(fonte, "penalti_defendido"));
        }

        // O ponto do exercício todo: sem a marca de fonte, o volante da ESPN levaria
        // z-score negativo em desarmes, interceptações e duelos que ele pode ter feito.
        [Fact]
        public void VolanteDaEspn_NaoEhPunidoPeloQueAFonteNaoInforma()
        {
            EstatisticaJogador Linha(string fonte) => new()
            {
                Fonte = fonte,
                Minutos = 90,
                // O que as duas fontes têm em comum, num jogo discreto.
                FinalizacoesTotal = 1,
                FaltasCometidas = 1,
                FaltasSofridas = 1,
            };

            var comoEspn = RatingAutomaticoHelper.Calcular(Linha(FonteEstatistica.Espn), "VOLANTE")!.Nota;
            var comoApi = RatingAutomaticoHelper.Calcular(Linha(FonteEstatistica.ApiFootball), "VOLANTE")!.Nota;

            // Lida como api-football, a linha afirma zero desarme, zero duelo e zero
            // passe — desempenho ruim de verdade. Lida como ESPN, esses campos somem
            // do cálculo e a nota fica mais perto do neutro.
            Assert.True(comoEspn > comoApi);
            Assert.True(comoEspn <= RatingAutomaticoHelper.NotaNeutra + 0.5);
        }

        // Pênalti não vem da ESPN. Sem a marca, um zero ali é só ausência de bônus;
        // o risco real é o contrário — a soma por critérios não pode contar como
        // vermelho/pênalti algo que a fonte nunca informou.
        [Fact]
        public void SomaPorCriterios_IgnoraCriterioQueAFonteNaoInforma()
        {
            var e = new EstatisticaJogador
            {
                Fonte = FonteEstatistica.Espn,
                Minutos = 90,
                Desarmes = 7,          // lixo: a ESPN não manda isso
                Interceptacoes = 4,    // idem
                Gols = 1,
            };

            var detalhes = CriteriosNotaHelper.ConstruirDetalhes(e);

            Assert.Contains(detalhes, d => d.AcaoId == "gol");
            Assert.DoesNotContain(detalhes, d => d.AcaoId == "desarme");
            Assert.DoesNotContain(detalhes, d => d.AcaoId == "interceptacao");
        }
    }
}
