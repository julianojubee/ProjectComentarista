using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    // O cadastro tem duplicatas herdadas de importações antigas: o Union St.-Gilloise
    // tem "Keo Boets" e "K. Boets", os dois com a camisa 71. Antes, dois candidatos
    // faziam o casamento desistir — e o chamador cadastrava um TERCEIRO registro do
    // mesmo jogador. Ambiguidade tem de desempatar, nunca criar.
    public class EspnDesempateTests
    {
        [Fact]
        public void Desempatar_PrefereNomeIdenticoAAbreviacao()
        {
            var candidatos = new List<Jogador>
            {
                new() { Id = 10, Nome = "K. Boets",  NumeroCamisa = 71 },
                new() { Id = 20, Nome = "Keo Boets", NumeroCamisa = 71 },
            };

            var escolhido = EspnEscalacaoService.Desempatar(candidatos, "Keo Boets", 71);

            Assert.Equal(20, escolhido.Id);
        }

        [Fact]
        public void Desempatar_ComNomesIguais_PrefereACamisaQueConfere()
        {
            var candidatos = new List<Jogador>
            {
                new() { Id = 10, Nome = "Mateo Biondic", NumeroCamisa = 30 },
                new() { Id = 20, Nome = "Mateo Biondic", NumeroCamisa = 9 },
            };

            Assert.Equal(20, EspnEscalacaoService.Desempatar(candidatos, "Mateo Biondic", 9).Id);
        }

        // O registro vinculado à api-football é o que a importação mantém atualizado;
        // o solto tende a ser o duplicado criado à mão.
        [Fact]
        public void Desempatar_EmpateRestante_PrefereQuemTemIdApi()
        {
            var candidatos = new List<Jogador>
            {
                new() { Id = 10, Nome = "Promise David", NumeroCamisa = 12 },
                new() { Id = 20, Nome = "Promise David", NumeroCamisa = 12, IdApi = 12345 },
            };

            Assert.Equal(20, EspnEscalacaoService.Desempatar(candidatos, "Promise David", 12).Id);
        }

        // Sem nada que desempate, fica o mais antigo — é o que os jogos anteriores
        // já referenciam, então trocar quebraria o histórico do jogador.
        [Fact]
        public void Desempatar_SemCriterio_FicaComOMaisAntigo()
        {
            var candidatos = new List<Jogador>
            {
                new() { Id = 77, Nome = "Giorgi Kavlashvili", NumeroCamisa = 18 },
                new() { Id = 12, Nome = "Giorgi Kavlashvili", NumeroCamisa = 18 },
            };

            Assert.Equal(12, EspnEscalacaoService.Desempatar(candidatos, "Giorgi Kavlashvili", 18).Id);
        }
    }
}
