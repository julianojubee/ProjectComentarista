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

        // ── FotMob ────────────────────────────────────────────────────────────
        //
        // Terceira fonte (ver FotMobService), usada onde nem a api-football nem a ESPN
        // têm o jogo. Fica entre as duas em profundidade: cobre tudo da ESPN e mais os
        // campos por jogador que ela não publica.

        [Theory]
        // O que a ESPN também tem
        [InlineData("gol")]
        [InlineData("defesa")]
        [InlineData("cartao_amarelo")]
        [InlineData("evento_gol")]
        // E o que só o FotMob tem
        [InlineData("desarme")]
        [InlineData("interceptacao")]
        [InlineData("bloqueio")]
        [InlineData("duelo_vencido")]
        [InlineData("passe_chave")]
        [InlineData("drible_certo")]
        [InlineData("drible_sofrido")]
        [InlineData("penalti_sofrido")]
        [InlineData("penalti_cometido")]
        [InlineData("passes_precisao")]
        [InlineData("duelos_precisao")]
        [InlineData("dribles")]
        public void FotMob_CobreOQuePublica(string id)
        {
            Assert.True(FonteEstatistica.Cobre(FonteEstatistica.FotMob, id));
        }

        [Theory]
        // O FotMob traz pênalti sofrido e cometido, mas não separa perdido de defendido.
        [InlineData("penalti_perdido")]
        [InlineData("penalti_defendido")]
        // A nota do jogador existe no payload, mas FotMobService não a importa de
        // propósito — é métrica proprietária deles. Chega null, e declarar o campo
        // não coberto é o que impede tratá-lo como zero.
        [InlineData("baseline_rating")]
        public void FotMob_NaoCobreOQueNaoImporta(string id)
        {
            Assert.False(FonteEstatistica.Cobre(FonteEstatistica.FotMob, id));
        }

        // Mesma regra da ESPN, mesmo motivo: allowlist, não blocklist.
        [Fact]
        public void FotMob_MetricaDesconhecida_NaoEhCoberta()
        {
            Assert.False(FonteEstatistica.Cobre(FonteEstatistica.FotMob, "metrica_que_ninguem_conferiu"));
        }

        // A razão de o FotMob existir como fonte separada, e não como "ESPN com outro
        // nome": no mesmo jogo, o volante importado dele é avaliado pelos desarmes e
        // duelos que fez de verdade, enquanto o da ESPN só pode ser avaliado pelo que
        // ela publica.
        [Fact]
        public void VolanteDoFotMob_EhAvaliadoPelosDuelosQueFez()
        {
            EstatisticaJogador Linha(string fonte) => new()
            {
                Fonte = fonte,
                Minutos = 90,
                Desarmes = 6,
                Interceptacoes = 4,
                DuelosTotal = 12,
                DuelosVencidos = 9,
                PassesTotal = 60,
                PassesCertos = 54,
            };

            var detalhesFotMob = CriteriosNotaHelper.ConstruirDetalhes(Linha(FonteEstatistica.FotMob));
            var detalhesEspn = CriteriosNotaHelper.ConstruirDetalhes(Linha(FonteEstatistica.Espn));

            Assert.Contains(detalhesFotMob, d => d.AcaoId == "desarme");
            Assert.Contains(detalhesFotMob, d => d.AcaoId == "duelo_vencido");
            Assert.DoesNotContain(detalhesEspn, d => d.AcaoId == "desarme");
        }
    }
}
