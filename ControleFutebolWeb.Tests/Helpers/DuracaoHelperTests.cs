using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers;

public class DuracaoHelperTests
{
    [Theory]
    [InlineData(0, "menos de 1 mês")]
    [InlineData(1, "1 mês")]
    [InlineData(5, "5 meses")]
    [InlineData(12, "1 ano")]
    [InlineData(13, "1 ano e 1 mês")]
    [InlineData(27, "2 anos e 3 meses")]
    [InlineData(48, "4 anos")]
    public void Formatar_Meses(int meses, string esperado)
    {
        Assert.Equal(esperado, DuracaoHelper.Formatar(meses));
    }

    [Fact]
    public void MesesCompletos_ConsideraODiaDoMes()
    {
        Assert.Equal(0, DuracaoHelper.MesesCompletos(new DateTime(2024, 1, 15), new DateTime(2024, 2, 14)));
        Assert.Equal(1, DuracaoHelper.MesesCompletos(new DateTime(2024, 1, 15), new DateTime(2024, 2, 15)));
        Assert.Equal(27, DuracaoHelper.MesesCompletos(new DateTime(2017, 4, 1), new DateTime(2019, 7, 1)));
    }

    [Fact]
    public void MesesCompletos_FimAntesDoInicio_Zero()
    {
        Assert.Equal(0, DuracaoHelper.MesesCompletos(new DateTime(2016, 12, 1), new DateTime(2016, 12, 1)));
        Assert.Equal(0, DuracaoHelper.MesesCompletos(new DateTime(2020, 5, 1), new DateTime(2019, 5, 1)));
    }

    [Fact]
    public void Formatar_PassagemAberta_ContaAteHoje()
    {
        Assert.Equal("6 anos", DuracaoHelper.Formatar(new DateTime(2020, 10, 1), null, new DateTime(2026, 10, 8)));
    }
}
