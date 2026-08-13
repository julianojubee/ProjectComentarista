using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class CriteriosDesempateHelperTests
    {
        private static Time T(int id, string nome) => new() { Id = id, Nome = nome, Cidade = "" };

        private static Jogo J(int id, Time casa, Time visitante, int? placarCasa, int? placarVisitante) => new()
        {
            Id = id,
            TimeCasa = casa,
            TimeCasaId = casa.Id,
            TimeVisitante = visitante,
            TimeVisitanteId = visitante.Id,
            PlacarCasa = placarCasa,
            PlacarVisitante = placarVisitante
        };

        private static Cartao C(int jogoId, Jogador jogador, string tipo) => new()
        {
            JogoId = jogoId,
            JogadorId = jogador.Id,
            Jogador = jogador,
            Tipo = tipo
        };

        [Fact]
        public void Parse_VazioOuInvalido_UsaPadrao()
        {
            Assert.Equal(CriteriosDesempateHelper.Padrao, CriteriosDesempateHelper.Parse(null));
            Assert.Equal(CriteriosDesempateHelper.Padrao, CriteriosDesempateHelper.Parse(""));
            Assert.Equal(CriteriosDesempateHelper.Padrao, CriteriosDesempateHelper.Parse("NAO_EXISTE;OUTRO"));
        }

        [Fact]
        public void Parse_MantemOrdem_DescartaInvalidosERepetidos()
        {
            var lista = CriteriosDesempateHelper.Parse("saldo_gols; VITORIAS ;SALDO_GOLS;xpto");

            Assert.Equal(new[] { "SALDO_GOLS", "VITORIAS" }, lista);
        }

        [Fact]
        public void Serializar_NormalizaParaStringSalva()
        {
            var texto = CriteriosDesempateHelper.Serializar(new[] { "GOLS_PRO", "gols_pro", "invalido", "VITORIAS" });

            Assert.Equal("GOLS_PRO;VITORIAS", texto);
        }

        [Fact]
        public void Ordenar_MesmosPontos_PrioridadeSegueOrdemDosCriterios()
        {
            // A: 3 vitórias e saldo 3 | B: 2 vitórias e saldo 6 — mesmos pontos.
            var a = new Classificacao { TimeId = 1, Time = T(1, "A"), Pontos = 10, Vitorias = 3, GolsPro = 8, GolsContra = 5 };
            var b = new Classificacao { TimeId = 2, Time = T(2, "B"), Pontos = 10, Vitorias = 2, GolsPro = 10, GolsContra = 4 };

            var porVitorias = CriteriosDesempateHelper.Ordenar(new[] { b, a },
                new[] { CriteriosDesempateHelper.Vitorias, CriteriosDesempateHelper.SaldoGols });
            Assert.Equal(1, porVitorias[0].TimeId);

            var porSaldo = CriteriosDesempateHelper.Ordenar(new[] { a, b },
                new[] { CriteriosDesempateHelper.SaldoGols, CriteriosDesempateHelper.Vitorias });
            Assert.Equal(2, porSaldo[0].TimeId);
        }

        [Fact]
        public void ConfrontoDireto_DesempataApenasEntreDuasEquipes()
        {
            var timeA = T(1, "A"); var timeB = T(2, "B");
            var jogos = new List<Jogo> { J(1, timeA, timeB, 0, 2) }; // B venceu o confronto

            var a = new Classificacao { TimeId = 1, Time = timeA, Pontos = 10, Vitorias = 3, GolsPro = 6, GolsContra = 4 };
            var b = new Classificacao { TimeId = 2, Time = timeB, Pontos = 10, Vitorias = 3, GolsPro = 6, GolsContra = 4 };

            var ordenada = CriteriosDesempateHelper.Ordenar(
                new[] { a, b },
                CriteriosDesempateHelper.Padrao,
                DadosDesempate.Construir(jogos));

            Assert.Equal(2, ordenada[0].TimeId);
            Assert.Equal(1, ordenada[0].Posicao);
        }

        [Fact]
        public void ConfrontoDireto_ComTresEmpatados_EhIgnorado_CaiNosCartoes()
        {
            var timeA = T(1, "A"); var timeB = T(2, "B"); var timeC = T(3, "C");
            var jogos = new List<Jogo>
            {
                J(1, timeA, timeB, 1, 1), J(2, timeB, timeC, 1, 1), J(3, timeC, timeA, 1, 1)
            };

            // Só o time B levou vermelho — com três empatados o confronto direto não vale,
            // então o desempate desce para "menos cartões vermelhos".
            var jogadorB = new Jogador { Id = 20, Nome = "Jogador B", TimeId = timeB.Id };
            var cartoes = new List<Cartao> { C(1, jogadorB, "Vermelho") };

            var linhas = new[]
            {
                new Classificacao { TimeId = 1, Time = timeA, Pontos = 10, Vitorias = 3, GolsPro = 5, GolsContra = 5 },
                new Classificacao { TimeId = 2, Time = timeB, Pontos = 10, Vitorias = 3, GolsPro = 5, GolsContra = 5 },
                new Classificacao { TimeId = 3, Time = timeC, Pontos = 10, Vitorias = 3, GolsPro = 5, GolsContra = 5 },
            };

            var ordenada = CriteriosDesempateHelper.Ordenar(
                linhas, CriteriosDesempateHelper.Padrao, DadosDesempate.Construir(jogos, cartoes));

            Assert.Equal(2, ordenada[2].TimeId); // B é o último por causa do vermelho
        }

        [Fact]
        public void Ordenar_SemCriterioQueDecida_UsaOrdemAlfabetica()
        {
            var a = new Classificacao { TimeId = 1, Time = T(1, "Zebra"), Pontos = 5 };
            var b = new Classificacao { TimeId = 2, Time = T(2, "Abelha"), Pontos = 5 };

            var ordenada = CriteriosDesempateHelper.Ordenar(new[] { a, b }, new[] { CriteriosDesempateHelper.Sorteio });

            Assert.Equal("Abelha", ordenada[0].Time?.Nome);
        }

        [Fact]
        public void ClassificacaoCalculator_ComCriterios_AplicaOrdemConfigurada()
        {
            var a = T(1, "A"); var b = T(2, "B"); var c = T(3, "C"); var d = T(4, "D");

            // Ambos com 6 pontos: A tem 2 vitórias e saldo +2; B tem 1 vitória e saldo +5.
            var jogos = new List<Jogo>
            {
                J(1, a, c, 1, 0), J(2, a, d, 1, 0),
                J(3, b, c, 5, 0), J(4, b, d, 0, 0), J(5, c, b, 1, 1), J(6, d, b, 2, 2),
            };

            var porSaldo = ClassificacaoCalculator.Calcular(jogos, null,
                new[] { CriteriosDesempateHelper.SaldoGols, CriteriosDesempateHelper.Vitorias });
            var porVitorias = ClassificacaoCalculator.Calcular(jogos, null,
                new[] { CriteriosDesempateHelper.Vitorias, CriteriosDesempateHelper.SaldoGols });

            Assert.Equal(6, porSaldo.Single(t => t.TimeId == 1).Pontos);
            Assert.Equal(6, porSaldo.Single(t => t.TimeId == 2).Pontos);
            Assert.Equal(2, porSaldo[0].TimeId);    // B lidera pelo saldo (+5)
            Assert.Equal(1, porVitorias[0].TimeId); // A lidera pelas vitórias (2)
        }
    }
}
