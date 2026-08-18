using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    // Ao aplicar a escalação da ESPN, os titulares são distribuídos nos slots da
    // formação na ordem do próprio gol para o ataque. Essa ordem sai do TEXTO da
    // posição, então uma posição não reconhecida coloca o jogador no ataque e
    // embaralha o time inteiro no campo.
    public class EspnEscalacaoOrdemTests
    {
        [Theory]
        [InlineData("Goleiro", 0)]
        [InlineData("Goalkeeper", 0)]
        [InlineData("Zagueiro Central Direito", 1)]
        [InlineData("Center Right Defender", 1)]
        [InlineData("Lateral esquerdo", 1)]
        [InlineData("Left Back", 1)]
        [InlineData("Meia-atacante", 2)]
        [InlineData("Attacking Midfielder", 2)]
        [InlineData("Volante", 2)]
        [InlineData("Atacante", 3)]
        [InlineData("Forward", 3)]
        public void RankLinha_ReconheceOsDoisIdiomas(string posicao, int esperado)
        {
            Assert.Equal(esperado, EspnEscalacaoService.RankLinha(posicao));
        }

        [Theory]
        [InlineData("Lateral-direito", 0)]
        [InlineData("Right Back", 0)]
        [InlineData("Zagueiro Central", 1)]
        [InlineData("Lateral esquerdo", 2)]
        [InlineData("Left Midfielder", 2)]
        public void RankLado_OrdenaDaDireitaParaEsquerda(string posicao, int esperado)
        {
            Assert.Equal(esperado, EspnEscalacaoService.RankLado(posicao));
        }

        // Zagueiro pela direita tem de ficar mais central que o lateral-direito, senão
        // os dois disputam o mesmo slot e um deles vai para o lado errado do campo.
        [Fact]
        public void XEstimado_SeparaZagueiroDeLateral()
        {
            var zagueiroDireita = EspnEscalacaoService.XEstimado("Center Right Defender");
            var lateralDireito = EspnEscalacaoService.XEstimado("Right Back");
            var zagueiroEsquerda = EspnEscalacaoService.XEstimado("Center Left Defender");
            var lateralEsquerdo = EspnEscalacaoService.XEstimado("Left Back");

            Assert.True(lateralEsquerdo < zagueiroEsquerda);
            Assert.True(zagueiroEsquerda < zagueiroDireita);
            Assert.True(zagueiroDireita < lateralDireito);
        }

        // Em pt-BR a ESPN escreve "Zagueiro esquerdo", sem a palavra "central". Se isso
        // não contar como central, ele empata com "Lateral esquerdo" e os dois trocam
        // de lugar no campo — foi o que aconteceu na primeira aplicação real.
        [Fact]
        public void XEstimado_ZagueiroEmPortuguesNaoEmpataComLateral()
        {
            Assert.True(EspnEscalacaoService.XEstimado("Lateral esquerdo")
                      < EspnEscalacaoService.XEstimado("Zagueiro esquerdo"));
            Assert.True(EspnEscalacaoService.XEstimado("Zagueiro direito")
                      < EspnEscalacaoService.XEstimado("Lateral-direito"));
        }

        // Quem começa no banco vem da ESPN com posição "Substituto" — estado no jogo,
        // não posição do jogador. Gravar isso em Jogador.Posicao contaminaria o filtro
        // de posição e a régua de nota.
        [Theory]
        [InlineData("Substituto", true)]
        [InlineData("Substitute", true)]
        [InlineData("SUB", true)]
        [InlineData("Zagueiro esquerdo", false)]
        [InlineData("Goleiro", false)]
        public void EhSubstituto_SoPegaOEstadoDeBanco(string posicao, bool esperado)
        {
            Assert.Equal(esperado, EspnEscalacaoService.EhSubstituto(posicao));
        }

        // O XI real do São Paulo em 15/08/2026 e a formação 4-2-3-1 como está cadastrada.
        // O caso que motivou o teste: emparelhar por ordem punha o lateral-direito no
        // slot de zagueiro e o zagueiro no de lateral-esquerdo.
        [Fact]
        public void DistribuirNosSlots_ColocaCadaUmNaSuaPosicao()
        {
            var xi = new[]
            {
                ("Rafael", "Goalkeeper"),
                ("Domingos Duarte", "Center Left Defender"),
                ("Robert Arboleda", "Center Right Defender"),
                ("Wendell", "Left Back"),
                ("Lucas Ramon", "Right Back"),
                ("Pablo Maia", "Left Midfielder"),
                ("Marcos Antonio", "Right Midfielder"),
                ("Luciano", "Attacking Midfielder"),
                ("Joao Victor", "Attacking Midfielder Left"),
                ("Newton", "Attacking Midfielder Right"),
                ("Jonathan Calleri", "Forward"),
            };

            var titulares = xi
                .Select((x, i) => (new Jogador { Id = i + 1, Nome = x.Item1 },
                                   new EspnEscalacaoService.EspnAtleta(x.Item1, i + 1, x.Item2, true)))
                .ToList();

            var slots = new List<PosicaoFormacao>
            {
                new() { Ordem = 1,  NomePosicao = "Goleiro",          PosicaoX = 50, PosicaoY = 92 },
                new() { Ordem = 2,  NomePosicao = "Zagueiro",         PosicaoX = 34, PosicaoY = 84 },
                new() { Ordem = 3,  NomePosicao = "Zagueiro",         PosicaoX = 65, PosicaoY = 84 },
                new() { Ordem = 4,  NomePosicao = "Lateral Esquerdo", PosicaoX = 12, PosicaoY = 81 },
                new() { Ordem = 5,  NomePosicao = "Lateral Direito",  PosicaoX = 89, PosicaoY = 81 },
                new() { Ordem = 6,  NomePosicao = "Volante",          PosicaoX = 32, PosicaoY = 60 },
                new() { Ordem = 7,  NomePosicao = "Volante",          PosicaoX = 65, PosicaoY = 60 },
                new() { Ordem = 8,  NomePosicao = "Meia Esquerda",    PosicaoX = 18, PosicaoY = 36 },
                new() { Ordem = 9,  NomePosicao = "Meia Central",     PosicaoX = 50, PosicaoY = 32 },
                new() { Ordem = 10, NomePosicao = "Meia Direita",     PosicaoX = 78, PosicaoY = 36 },
                new() { Ordem = 11, NomePosicao = "Centroavante",     PosicaoX = 50, PosicaoY = 14 },
            };

            var mapa = EspnEscalacaoService.DistribuirNosSlots(titulares, slots)
                .ToDictionary(r => r.Jogador.Nome, r => r.Slot?.NomePosicao);

            Assert.Equal("Goleiro", mapa["Rafael"]);
            Assert.Equal("Lateral Direito", mapa["Lucas Ramon"]);
            Assert.Equal("Lateral Esquerdo", mapa["Wendell"]);
            Assert.Equal("Zagueiro", mapa["Robert Arboleda"]);
            Assert.Equal("Zagueiro", mapa["Domingos Duarte"]);
            Assert.Equal("Centroavante", mapa["Jonathan Calleri"]);

            // Ninguém fica sem slot quando as contagens batem.
            Assert.DoesNotContain(mapa.Values, v => v == null);
        }
    }
}
