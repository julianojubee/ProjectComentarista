using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Garante que o jogador cadastrado à mão (tela de análise) seja reconhecido
    // quando a API trouxer o mesmo nome escrito de outro jeito — é o que impede a
    // importação de criar um segundo cadastro do mesmo jogador.
    public class NomeJogadorHelperTests
    {
        [Theory]
        [InlineData("Kauã Prates", "Kaua Prates")]        // acento
        [InlineData("Kauã Prates", "K. Prates")]          // primeiro nome abreviado
        [InlineData("Lucas Emanuel", "L. E. Jales do Nascimento")] // feed de eventos
        [InlineData("  joão  silva ", "Joao Silva")]      // espaços e caixa
        [InlineData("Vitor Roque", "Vitor Roque Ferreira")] // sobrenome extra na API
        public void NomesDaMesmaPessoa_Correspondem(string cadastrado, string daApi)
        {
            Assert.True(NomeJogadorHelper.Corresponde(cadastrado, daApi));
        }

        [Theory]
        [InlineData("Kauã Prates", "Bruno Prates")]       // mesmo sobrenome, outro jogador
        [InlineData("Lucas Silva", "Leandro Souza")]      // só a inicial em comum
        [InlineData("Pedro", "Paulo")]
        [InlineData("Endrick", "")]
        public void JogadoresDiferentes_NaoCorrespondem(string cadastrado, string daApi)
        {
            Assert.False(NomeJogadorHelper.Corresponde(cadastrado, daApi));
        }

        [Fact]
        public void Normalizar_TiraAcentosCaixaEEspacos()
        {
            Assert.Equal("kaua prates", NomeJogadorHelper.Normalizar("  Kauã   PRATES "));
        }
    }
}
