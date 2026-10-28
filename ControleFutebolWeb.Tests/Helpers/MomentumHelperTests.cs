using System.Text.Json;
using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Aba Momentum do pós-jogo: curva do FotMob ou montada da marcação por vídeo,
    // sempre de -100 (visitante em cima) a +100 (mandante).
    public class MomentumHelperTests
    {
        [Fact]
        public void DeAcoes_NormalizaPeloPicoEDecaiComOTempo()
        {
            var pontos = MomentumHelper.DeAcoes(new[] { new MomentumHelper.Acao(10, true, 3) }, 30);

            Assert.Equal(0, pontos[9].Valor);
            Assert.Equal(100, pontos[10].Valor);
            Assert.Equal(50, pontos[10 + (int)MomentumHelper.MeiaVida].Valor);
        }

        [Fact]
        public void DeAcoes_VisitanteFicaAbaixoDaLinha()
        {
            var pontos = MomentumHelper.DeAcoes(new[]
            {
                new MomentumHelper.Acao(20, true, 1),
                new MomentumHelper.Acao(60, false, 3),
            });

            Assert.True(pontos[20].Valor > 0);
            Assert.Equal(-100, pontos[60].Valor);
        }

        [Fact]
        public void DeAcoes_SemAcao_DevolveCurvaVazia()
        {
            Assert.Empty(MomentumHelper.DeAcoes(Array.Empty<MomentumHelper.Acao>()));
        }

        [Fact]
        public void LerFotMob_LeOsPontosComAcrescimoFracionario()
        {
            using var doc = JsonDocument.Parse("""
                {"content":{"momentum":{"main":{"data":[
                  {"minute":0,"value":0},{"minute":45,"value":49},{"minute":45.5,"value":-53},{"minute":90,"value":130}
                ]}}}}
                """);

            var pontos = MomentumHelper.LerFotMob(doc.RootElement)!;

            Assert.Equal(4, pontos.Count);
            Assert.Equal(45.5, pontos[2].Minuto);
            Assert.Equal(-53, pontos[2].Valor);
            Assert.Equal(100, pontos[3].Valor); // fora da escala é cortado
        }

        [Theory]
        [InlineData("""{"content":{}}""")]
        [InlineData("""{"content":{"momentum":null}}""")]
        [InlineData("""{"content":{"momentum":{"main":{"data":[{"minute":0,"value":0}]}}}}""")]
        public void LerFotMob_SemCurva_DevolveNull(string json)
        {
            using var doc = JsonDocument.Parse(json);
            Assert.Null(MomentumHelper.LerFotMob(doc.RootElement));
        }

        [Fact]
        public void DominioCasa_EAreaAcimaDaLinha()
        {
            var pontos = new[] { new MomentumHelper.Ponto(1, 60), new MomentumHelper.Ponto(2, -20), new MomentumHelper.Ponto(3, 20) };

            Assert.Equal(80, MomentumHelper.DominioCasa(pontos));
        }

        [Fact]
        public void Periodos_PegaSequenciasFortesDoMesmoLado()
        {
            var valores = new[] { 10, 60, 70, 80, 20, -55, -60, -70, -65, 30, 90 };
            var pontos = valores.Select((v, i) => new MomentumHelper.Ponto(i, v)).ToList();

            var periodos = MomentumHelper.Periodos(pontos);

            Assert.Equal(2, periodos.Count);
            Assert.Equal(new MomentumHelper.Periodo(1, 3, true), periodos[0]);
            Assert.Equal(new MomentumHelper.Periodo(5, 8, false), periodos[1]);
        }
    }
}
