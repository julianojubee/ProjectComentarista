using System.Text.Json;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    // Em fixtures/players a api-football chama de "accuracy" o que na verdade é a
    // CONTAGEM de passes certos — conferido contra fixtures/statistics do mesmo
    // jogo, onde a soma dos jogadores bate com "Passes accurate" do time. O campo
    // vem ora como número, ora como string, e o lineup inteiro deixaria de ser
    // desserializado se o tipo não aceitasse os dois.
    public class AfPassesTests
    {
        private static AfPasses Ler(string json) =>
            JsonSerializer.Deserialize<AfPasses>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        [Fact]
        public void PassesCertos_LeAccuracyComoNumero()
        {
            var p = Ler("""{"total":57,"key":1,"accuracy":24}""");
            Assert.Equal(57, p.Total);
            Assert.Equal(24, p.PassesCertos);
        }

        [Fact]
        public void PassesCertos_LeAccuracyComoString()
        {
            var p = Ler("""{"total":33,"key":1,"accuracy":"17"}""");
            Assert.Equal(17, p.PassesCertos);
        }

        [Theory]
        [InlineData("""{"total":40,"key":null,"accuracy":null}""")]
        [InlineData("""{"total":40,"key":null}""")]
        [InlineData("""{"total":40,"accuracy":"n/d"}""")]
        public void PassesCertos_EhNuloQuandoNaoVemNumeroUtil(string json)
        {
            Assert.Null(Ler(json).PassesCertos);
        }

        // A precisão exibida é derivada — a api-football não manda percentual por
        // jogador, só o time tem "Passes %".
        [Theory]
        [InlineData(400, 245, 61.3)]
        [InlineData(57, 24, 42.1)]
        [InlineData(10, 10, 100.0)]
        public void PrecisaoPasses_SaiDaRazaoEntreCertosETentados(int total, int certos, double esperado)
        {
            var e = new EstatisticaJogador { PassesTotal = total, PassesCertos = certos };
            Assert.Equal(esperado, e.PrecisaoPasses);
        }

        [Fact]
        public void PrecisaoPasses_EhNulaSemPasseTentado()
        {
            Assert.Null(new EstatisticaJogador { PassesTotal = 0, PassesCertos = 0 }.PrecisaoPasses);
        }

        // Jogo importado antes da coluna PassesCertos existir fica com passes
        // tentados e zero certos. Isso é ausência de dado, não passe ruim — se
        // virasse 0% a tela acusaria de erro todos os passes do jogador.
        [Fact]
        public void PrecisaoPasses_EhNulaQuandoNaoHaAcertoRegistrado()
        {
            Assert.Null(new EstatisticaJogador { PassesTotal = 400, PassesCertos = 0 }.PrecisaoPasses);
        }
    }
}
