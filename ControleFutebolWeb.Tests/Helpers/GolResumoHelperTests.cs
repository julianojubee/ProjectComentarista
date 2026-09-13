using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    // O lado do gol tem que sair da escalação DAQUELE jogo, não do clube atual do
    // cadastro: quando um jogador é transferido, o Jogador.TimeId passa a ser o clube
    // novo e os gols antigos dele mudavam de lado — o placar do jogo passado
    // "desmontava" sozinho meses depois.
    public class GolResumoHelperTests
    {
        private const int Casa = 2314, Visitante = 1906, OutroClube = 1907;

        private static Jogo JogoBase() => new()
        {
            Id = 1,
            TimeCasaId = Casa,
            TimeVisitanteId = Visitante
        };

        private static Jogador Jog(int id, string nome, int timeId) =>
            new() { Id = id, Nome = nome, TimeId = timeId };

        private static Gol GolDe(Jogador j, int minuto, bool contra = false) =>
            new() { Id = j.Id, JogoId = 1, JogadorId = j.Id, Jogador = j, Minuto = minuto, Contra = contra };

        private static Escalacao Escalado(Jogador j, bool casa) =>
            new() { JogoId = 1, JogadorId = j.Id, IsTimeCasa = casa };

        [Fact]
        public void Montar_JogadorTransferidoDepoisDoJogo_ContaGolNoLadoEmQueAtuou()
        {
            var jogo = JogoBase();
            // Marcou pela casa e depois foi para outro clube: hoje o cadastro dele não
            // bate com nenhum dos dois times do jogo.
            var transferido = Jog(55262, "S. Martinez", OutroClube);
            var gols = new[] { GolDe(transferido, 34) };

            var resumo = GolResumoHelper.Montar(
                jogo, gols, Array.Empty<Assistencia>(), new[] { Escalado(transferido, casa: true) });

            Assert.True(resumo.Single().EhCasa);
        }

        [Fact]
        public void Montar_SemEscalacao_CaiNoClubeDoCadastro()
        {
            var jogo = JogoBase();
            var daCasa = Jog(1, "A. Martin", Casa);

            var resumo = GolResumoHelper.Montar(jogo, new[] { GolDe(daCasa, 21) }, Array.Empty<Assistencia>());

            Assert.True(resumo.Single().EhCasa);
        }

        [Fact]
        public void Montar_GolContra_ContaParaOAdversarioDoAutor()
        {
            var jogo = JogoBase();
            var zagueiroDaCasa = Jog(2, "Zagueiro", Casa);

            var resumo = GolResumoHelper.Montar(
                jogo, new[] { GolDe(zagueiroDaCasa, 55, contra: true) }, Array.Empty<Assistencia>(),
                new[] { Escalado(zagueiroDaCasa, casa: true) });

            Assert.False(resumo.Single().EhCasa);
        }

        [Fact]
        public void Montar_Assistencia_CasaPorMinutoELadoDoAutor()
        {
            var jogo = JogoBase();
            var autor = Jog(3, "P. Gueye", Visitante);
            var parceiro = Jog(4, "A. Moleiro", Visitante);
            var adversario = Jog(5, "Adversario", Casa);

            var assistencias = new[]
            {
                new Assistencia { JogoId = 1, JogadorId = adversario.Id, Jogador = adversario, Minuto = 45 },
                new Assistencia { JogoId = 1, JogadorId = parceiro.Id, Jogador = parceiro, Minuto = 45 }
            };

            var resumo = GolResumoHelper.Montar(
                jogo, new[] { GolDe(autor, 45) }, assistencias,
                new[] { Escalado(autor, false), Escalado(parceiro, false), Escalado(adversario, true) });

            Assert.Equal("A. Moleiro", resumo.Single().NomeAssistencia);
        }

        [Fact]
        public void Montar_DoisGolsNoMesmoMinuto_NaoRepetemAAssistencia()
        {
            var jogo = JogoBase();
            var gueye = Jog(3, "P. Gueye", Visitante);
            var pepe = Jog(4, "N. Pepe", Visitante);
            var moleiro = Jog(5, "A. Moleiro", Visitante);
            var mikautadze = Jog(6, "G. Mikautadze", Visitante);

            var assistencias = new[]
            {
                new Assistencia { Id = 1, JogoId = 1, JogadorId = moleiro.Id, Jogador = moleiro, Minuto = 45 },
                new Assistencia { Id = 2, JogoId = 1, JogadorId = mikautadze.Id, Jogador = mikautadze, Minuto = 45 }
            };

            var resumo = GolResumoHelper.Montar(
                jogo, new[] { GolDe(gueye, 45), GolDe(pepe, 45) }, assistencias,
                new[] { Escalado(gueye, false), Escalado(pepe, false),
                        Escalado(moleiro, false), Escalado(mikautadze, false) });

            Assert.Equal(new[] { "A. Moleiro", "G. Mikautadze" },
                         resumo.Select(g => g.NomeAssistencia));
        }

        [Fact]
        public void Montar_MaisGolsQueAssistencias_DeixaORestoSemAssistencia()
        {
            var jogo = JogoBase();
            var autor1 = Jog(7, "Autor 1", Casa);
            var autor2 = Jog(8, "Autor 2", Casa);
            var assistente = Jog(9, "Assistente", Casa);

            var assistencias = new[]
            {
                new Assistencia { Id = 1, JogoId = 1, JogadorId = assistente.Id, Jogador = assistente, Minuto = 70 }
            };

            var resumo = GolResumoHelper.Montar(
                jogo, new[] { GolDe(autor1, 70), GolDe(autor2, 70) }, assistencias,
                new[] { Escalado(autor1, true), Escalado(autor2, true), Escalado(assistente, true) });

            Assert.Equal(new[] { "Assistente", null }, resumo.Select(g => g.NomeAssistencia));
        }

        [Fact]
        public void Montar_ComEscalacaoInicialEFinal_SegueAInicial()
        {
            var jogo = JogoBase();
            var jogador = Jog(6, "Jogador", OutroClube);

            // O LadoJogadorHelper resolve o (jogador, jogo) pela linha INICIAL mesmo
            // quando a FINAL aparece antes na lista.
            var escalacoes = new[]
            {
                new Escalacao { JogoId = 1, JogadorId = jogador.Id, IsTimeCasa = false, FaseEscalacao = "FINAL" },
                new Escalacao { JogoId = 1, JogadorId = jogador.Id, IsTimeCasa = true, FaseEscalacao = "INICIAL" }
            };

            var resumo = GolResumoHelper.Montar(
                jogo, new[] { GolDe(jogador, 10) }, Array.Empty<Assistencia>(), escalacoes);

            Assert.True(resumo.Single().EhCasa);
        }
    }
}
