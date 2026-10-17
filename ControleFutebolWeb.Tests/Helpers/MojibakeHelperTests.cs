using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Nomes que a api-football manda com o acento estragado (UTF-8 lido como Latin-1)
    public class MojibakeHelperTests
    {
        [Theory]
        [InlineData("JoÃ£o Pedro Chermont", "João Pedro Chermont")]
        [InlineData("Dani MartÃ­nez", "Dani Martínez")]
        [InlineData("C. Inao OulaÃ¯", "C. Inao Oulaï")]
        [InlineData("JosÃ© MarÃ­a GimÃ©nez", "José María Giménez")]
        public void Corrigir_TextoEstragado_VoltaAoOriginal(string estragado, string esperado)
        {
            Assert.Equal(esperado, MojibakeHelper.Corrigir(estragado));
        }

        [Fact]
        public void Corrigir_MisturadoComAcentoCerto_ConsertaSoOEstragado()
        {
            Assert.Equal("Snæhólm", MojibakeHelper.Corrigir("SnÃ¦hólm"));
        }

        [Theory]
        [InlineData("João Pedro")]
        [InlineData("Ãngel Romero")]
        [InlineData("Müller")]
        [InlineData("")]
        [InlineData(null)]
        public void Corrigir_TextoCerto_FicaComoEsta(string? texto)
        {
            Assert.Equal(texto, MojibakeHelper.Corrigir(texto));
        }
    }

    public class IndisponibilidadeMotivoTests
    {
        [Theory]
        [InlineData("Off the roster", "Fora dos relacionados")]
        [InlineData("Loan agreement", "Cláusula de empréstimo")]
        [InlineData("Transfer negotiations", "Em negociação de transferência")]
        [InlineData("Coach's decision", "Decisão do treinador")]
        [InlineData("Broken leg", "Fratura na perna")]
        public void Motivo_TraduzOsQueJaApareceramNoBanco(string original, string esperado)
        {
            Assert.Equal(esperado, IndisponibilidadeHelper.Motivo(original));
        }

        [Theory]
        [InlineData("Broken leg")]
        [InlineData("Concussion")]
        [InlineData("Heart problems")]
        public void Categoria_MotivosMedicos_SaoLesao(string motivo)
        {
            Assert.Equal(IndisponibilidadeHelper.CategoriaLesao, IndisponibilidadeHelper.Categoria(motivo));
        }
    }
}
