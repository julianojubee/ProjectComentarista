using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using static ControleFutebolWeb.Helpers.CuriosidadesJogoHelper;

namespace ControleFutebolWeb.Tests.Helpers
{
    // Regras da aba Curiosidades do pós-jogo. O risco aqui é inventar destaque:
    // chamar de "recorde" um número com pouco histórico, ou comparar com um 0 que
    // só existe porque a fonte não publica a métrica.
    public class CuriosidadesJogoHelperTests
    {
        private const int JogoAtual = 100;
        private const int Casa = 1, Visitante = 2;

        private static EstatisticaJogador Linha(int jogoId, int jogadorId, int desarmes = 0, string fonte = FonteEstatistica.ApiFootball, int minutos = 90) =>
            new() { JogoId = jogoId, JogadorId = jogadorId, Desarmes = desarmes, Fonte = fonte, Minutos = minutos };

        private static Entrada Base(IReadOnlyList<EstatisticaJogador> jogo, IReadOnlyList<EstatisticaJogador> historico,
            IReadOnlyList<JogoResumo>? jogos = null, IReadOnlyList<GolCampeonato>? gols = null) => new()
        {
            JogoId = JogoAtual,
            TimeCasaId = Casa,
            TimeVisitanteId = Visitante,
            NomeCasa = "Casa FC",
            NomeVisitante = "Visitante FC",
            EstatisticasJogo = jogo,
            HistoricoCampeonato = historico,
            HistoricoTemporada = historico,
            JogosCampeonato = jogos ?? Array.Empty<JogoResumo>(),
            GolsCampeonato = gols ?? Array.Empty<GolCampeonato>(),
            NomeJogador = id => "Jogador " + id,
            LadoJogador = id => true,
        };

        // 6 jogos anteriores, jogador 7 com 2 desarmes por jogo, outro com até 6.
        private static List<EstatisticaJogador> Historico(int maxOutro = 6) =>
            Enumerable.Range(1, 6).SelectMany(j => new[] { Linha(j, 7, desarmes: 2), Linha(j, 8, desarmes: j == 3 ? maxOutro : 1) }).ToList();

        [Fact]
        public void RecordeDoCampeonato_QuandoSuperaOMaximoAnterior()
        {
            var r = Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 10) }, Historico()));

            var c = Assert.Single(r, x => x.Tipo == "recorde");
            Assert.Contains("10 desarmes", c.Texto);
            Assert.Contains("recorde anterior era 6", c.Texto);
            // Recorde do campeonato já implica recorde pessoal: não repete.
            Assert.DoesNotContain(r, x => x.Tipo == "pessoal");
        }

        [Fact]
        public void RecordePessoal_QuandoNaoBateOCampeonato()
        {
            var r = Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 5) }, Historico(maxOutro: 9)));

            var c = Assert.Single(r);
            Assert.Equal("pessoal", c.Tipo);
            Assert.Contains("O máximo antes era 2, em 6 jogos", c.Texto);
        }

        [Fact]
        public void PoucoHistorico_NaoViraRecorde()
        {
            var historico = new[] { Linha(1, 8, desarmes: 1), Linha(2, 8, desarmes: 1) };
            Assert.Empty(Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 10) }, historico)));
        }

        [Fact]
        public void FonteQueNaoPublicaAMetrica_FicaForaDaComparacao()
        {
            // A ESPN não publica desarmes: a linha do jogo não gera nada...
            Assert.Empty(Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 10, fonte: FonteEstatistica.Espn) }, Historico())));

            // ...e o histórico ESPN (zeros) não conta como jogos para o recorde.
            var historicoEspn = Enumerable.Range(1, 6).Select(j => Linha(j, 8, fonte: FonteEstatistica.Espn)).ToList();
            Assert.Empty(Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 10) }, historicoEspn)));
        }

        [Fact]
        public void AbaixoDoMinimo_NaoGeraCuriosidade()
        {
            Assert.Empty(Gerar(Base(new[] { Linha(JogoAtual, 7, desarmes: 4) }, Historico(maxOutro: 1))));
        }

        [Fact]
        public void PrimeiroHatTrickDoCampeonato()
        {
            var jogos = Enumerable.Range(1, 6).Select(j => new JogoResumo(j, new DateTime(2026, 1, j), 3, 4, 1, 0))
                .Append(new JogoResumo(JogoAtual, new DateTime(2026, 2, 1), Casa, Visitante, 3, 0)).ToList();
            var gols = jogos.Where(j => j.Id != JogoAtual).Select(j => new GolCampeonato(j.Id, 50 + j.Id, 30))
                .Concat(new[] { new GolCampeonato(JogoAtual, 7, 10), new GolCampeonato(JogoAtual, 7, 20), new GolCampeonato(JogoAtual, 7, 80) })
                .ToList();

            var r = Gerar(Base(Array.Empty<EstatisticaJogador>(), Array.Empty<EstatisticaJogador>(), jogos, gols));

            Assert.Contains(r, x => x.Titulo == "Primeiro hat-trick do campeonato");
            Assert.Contains(r, x => x.Titulo == "Artilharia" && x.Texto.Contains("assumiu"));
        }

        [Fact]
        public void SequenciaDeVitorias_EFimDaInvencibilidadeDoAdversario()
        {
            var jogos = new List<JogoResumo>
            {
                new(1, new DateTime(2026, 1, 1), Casa, 9, 1, 0),
                new(2, new DateTime(2026, 1, 2), 9, Casa, 0, 2),
                new(3, new DateTime(2026, 1, 3), Casa, 9, 3, 1),
            };
            // Visitante vinha de 5 jogos sem perder.
            for (int i = 0; i < 5; i++) jogos.Add(new(10 + i, new DateTime(2026, 1, 1 + i), Visitante, 9, 1, 1));
            jogos.Add(new(JogoAtual, new DateTime(2026, 2, 1), Casa, Visitante, 2, 0));

            var r = Gerar(Base(Array.Empty<EstatisticaJogador>(), Array.Empty<EstatisticaJogador>(), jogos));

            Assert.Contains(r, x => x.Texto == "Casa FC chegou a 4 vitórias seguidas no campeonato.");
            Assert.Contains(r, x => x.Texto == "Casa FC acabou com a invencibilidade de 5 jogos de Visitante FC.");

            // Fim da invencibilidade fala dos dois clubes: vai para o perfil dos dois.
            Assert.True(r.Single(x => x.Titulo == "Fim da invencibilidade").AmbosTimes);
        }

        // A chave é o que impede gravar a mesma curiosidade duas vezes no perfil:
        // tem que existir sempre e não pode repetir dentro do mesmo jogo.
        [Fact]
        public void Chaves_PreenchidasEUnicasNoJogo()
        {
            var jogos = Enumerable.Range(1, 6).Select(j => new JogoResumo(j, new DateTime(2026, 1, j), Casa, 9, 1, 0))
                .Append(new JogoResumo(JogoAtual, new DateTime(2026, 2, 1), Casa, Visitante, 6, 0)).ToList();
            var gols = Enumerable.Range(1, 12).Select(i => new GolCampeonato(1 + i % 6, 50 + i, 30))
                .Concat(Enumerable.Range(0, 4).Select(i => new GolCampeonato(JogoAtual, 7, 5 + i)))
                .ToList();
            var jogo = new[] { Linha(JogoAtual, 7, desarmes: 10), Linha(JogoAtual, 8, desarmes: 9) };

            var r = Gerar(Base(jogo, Historico(), jogos, gols));

            Assert.True(r.Count >= 4);
            Assert.All(r, c => Assert.False(string.IsNullOrEmpty(c.Chave)));
            Assert.Equal(r.Count, r.Select(c => c.Chave).Distinct().Count());
        }
    }
}
