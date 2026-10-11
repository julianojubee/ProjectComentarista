using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers;

public class RegiaoCompeticaoHelperTests
{
    [Theory]
    [InlineData("World", "Mundo")]
    [InlineData("Mundo", "Mundo")]
    [InlineData("America do Sul", "América do Sul")]
    [InlineData("Italia", "Itália")]
    [InlineData("Arabia Saudita", "Arábia Saudita")]
    [InlineData("Saudi Arabia", "Arábia Saudita")]
    [InlineData("  Brasil ", "Brasil")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizar_UnificaVariacoes(string? entrada, string esperado)
    {
        Assert.Equal(esperado, RegiaoCompeticaoHelper.Normalizar(entrada));
    }

    [Theory]
    [InlineData("Brasil", "Brasil")]
    [InlineData("Argentina", "América do Sul")]
    [InlineData("Inglaterra", "Europa")]
    [InlineData("Italia", "Europa")]
    [InlineData("World", "Mundo")]
    [InlineData("Qatar", "Ásia e Oriente Médio")]
    [InlineData("Arabia Saudita", "Ásia e Oriente Médio")]
    [InlineData("Marte", "Marte")]
    [InlineData(null, "Outras")]
    public void Bloco_AgrupaPorContinente(string? entrada, string esperado)
    {
        Assert.Equal(esperado, RegiaoCompeticaoHelper.Bloco(entrada));
    }

    [Fact]
    public void OrdemBloco_BrasilPrimeiroEDesconhecidosNoFim()
    {
        Assert.True(RegiaoCompeticaoHelper.OrdemBloco("Brasil") < RegiaoCompeticaoHelper.OrdemBloco("Europa"));
        Assert.True(RegiaoCompeticaoHelper.OrdemBloco("Mundo") < RegiaoCompeticaoHelper.OrdemBloco("Marte"));
    }
}
