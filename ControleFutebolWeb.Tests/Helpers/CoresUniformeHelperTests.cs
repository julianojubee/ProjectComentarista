using ControleFutebolWeb.Helpers;
using static ControleFutebolWeb.Helpers.CoresUniformeHelper;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Fallback da cor do uniforme quando a api-football manda colors: null. O erro que
    // importa aqui é o visitante sair com a mesma cor do mandante no campinho.
    public class CoresUniformeHelperTests
    {
        [Fact]
        public void Mandante_PrefereUniformeDeCasa()
        {
            var r = EscolherMandante(new Uniforme("ff0000", "ffffff"), new Uniforme("000000", "ffffff"));
            Assert.Equal("ff0000", r!.Camisa);
        }

        [Fact]
        public void Mandante_SemHistoricoEmCasa_UsaODeFora()
        {
            var r = EscolherMandante(null, new Uniforme("000000", "ffffff"));
            Assert.Equal("000000", r!.Camisa);
        }

        [Fact]
        public void Visitante_UsaUniformeDeFora()
        {
            var r = EscolherVisitante("ff0000", new Uniforme("ffffff", "000000"), new Uniforme("0054a8", "ffffff"));
            Assert.Equal("ffffff", r!.Camisa);
        }

        [Fact]
        public void Visitante_UniformeDeForaIgualAoMandante_TrocaPeloOutro()
        {
            // Mandante de vermelho × visitante cujo uniforme de fora também é vermelho: vai de uniforme 1.
            var r = EscolherVisitante("e41e2c", new Uniforme("d71920", "ffffff"), new Uniforme("fcfcfc", "000000"));
            Assert.Equal("fcfcfc", r!.Camisa);
        }

        [Fact]
        public void Visitante_TodosParecidos_FicaComODeFora()
        {
            var r = EscolherVisitante("ffffff", new Uniforme("f0efef", null), new Uniforme("fcfcfc", null));
            Assert.Equal("f0efef", r!.Camisa);
        }

        [Fact]
        public void Visitante_SemHistorico_Nulo()
        {
            Assert.Null(EscolherVisitante("ffffff", null, null));
        }

        [Theory]
        [InlineData("ffffff", "#FCFCFC", true)]
        [InlineData("ff0000", "0000ff", false)]
        [InlineData("ff0000", null, false)]
        [InlineData("xyz", "ff0000", false)]
        public void Parecidas(string? a, string? b, bool esperado)
        {
            Assert.Equal(esperado, CoresUniformeHelper.Parecidas(a, b));
        }

        [Theory]
        [InlineData("ffffff", "000000")]
        [InlineData("fbc546", "000000")]
        [InlineData("0054a8", "ffffff")]
        [InlineData("7b242f", "ffffff")]
        public void NumeroLegivel_ContrastaComACamisa(string camisa, string numero)
        {
            Assert.Equal(numero, NumeroLegivel(camisa));
        }
    }
}
