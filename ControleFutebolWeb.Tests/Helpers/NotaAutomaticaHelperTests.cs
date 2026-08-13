using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Helpers.Rating;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class MotorNotaTests
    {
        private static CriterioNota Motor(string? config, bool ativo = true) => new()
        {
            AcaoId = CriteriosNotaHelper.AcaoMotorNota,
            Label = CriteriosNotaHelper.LabelMotorNota,
            Config = config,
            Ativo = ativo,
        };

        [Fact]
        public void SemEscolha_UsaOClassico()
        {
            // Quem nunca abriu a tela não pode ter as notas trocadas por baixo.
            Assert.Equal(MotorNota.Classico, CriteriosNotaHelper.MotorDaNota(null));
            Assert.Equal(MotorNota.Classico, CriteriosNotaHelper.MotorDaNota(new List<CriterioNota>()));
        }

        [Theory]
        [InlineData("Automatico")]
        [InlineData("automatico")]
        [InlineData("  AUTOMATICO  ")]
        public void EscolhaAutomatica_EhLida(string config)
            => Assert.Equal(MotorNota.Automatico, CriteriosNotaHelper.MotorDaNota(new[] { Motor(config) }));

        [Theory]
        [InlineData("Classico")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("qualquer coisa")]
        public void ValorDesconhecido_CaiNoClassico(string? config)
            => Assert.Equal(MotorNota.Classico, CriteriosNotaHelper.MotorDaNota(new[] { Motor(config) }));

        [Fact]
        public void CriterioInativo_NaoVale()
            => Assert.Equal(MotorNota.Classico,
                CriteriosNotaHelper.MotorDaNota(new[] { Motor("Automatico", ativo: false) }));

        [Fact]
        public void MotorNaoApareceNaListaDeAcoes()
        {
            var lista = new List<CriterioNota>
            {
                Motor("Automatico"),
                new() { AcaoId = "gol", Label = "Gol", Peso = 2, Ativo = true },
            };

            var acoes = CriteriosNotaHelper.SomenteAcoes(lista);
            Assert.Single(acoes);
            Assert.Equal("gol", acoes[0].AcaoId);
        }

        [Fact]
        public void AcoesReservadasNaoPodemSerCriadasPeloUsuario()
        {
            Assert.True(CriteriosNotaHelper.EhAcaoReservada(CriteriosNotaHelper.AcaoMotorNota));
            Assert.True(CriteriosNotaHelper.EhAcaoReservada(CriteriosNotaHelper.AcaoPesoInicial));
            Assert.True(CriteriosNotaHelper.EhAcaoReservada(CriteriosNotaHelper.AcaoSemSofrerGol));
            Assert.True(CriteriosNotaHelper.EhAcaoReservada(BaselineRating.AcaoId));
            Assert.False(CriteriosNotaHelper.EhAcaoReservada("gol"));
        }
    }

    public class CalculadoraNotaAutomaticaTests
    {
        private const int JogadorId = 7;
        private const int JogoId = 99;

        private static readonly IReadOnlyDictionary<(int, int), AtuacaoNoJogo> Lados =
            new Dictionary<(int, int), AtuacaoNoJogo>
            {
                [(JogadorId, JogoId)] = new AtuacaoNoJogo(IsTimeCasa: true, Posicao: "Centroavante"),
            };

        private static CalculadoraNotaAutomatica Calc(MotorNota motor, ContextoRating? contexto = null)
            => new(motor,
                new List<CriterioNota>(),   // lista vazia = pesos padrão do CriteriosNotaHelper
                Lados,
                new Dictionary<(int, int), CriteriosNotaHelper.ContextoNota>
                {
                    [(JogadorId, JogoId)] = new CriteriosNotaHelper.ContextoNota(Minutos: 90),
                },
                new Dictionary<(int, int), ContextoRating>
                {
                    [(JogadorId, JogoId)] = contexto ?? new ContextoRating(ResultadoSinal: +1, GolsSofridosTime: 0),
                },
                BaselineRating.Padrao);

        private static EstatisticaJogador Jogo(int minutos = 90) => new()
        {
            JogadorId = JogadorId, JogoId = JogoId, Minutos = minutos,
            Gols = 1, FinalizacoesTotal = 4, FinalizacoesNoGol = 2,
            PassesTotal = 25, PassesCertos = 20, DuelosTotal = 10, DuelosVencidos = 6,
        };

        [Fact]
        public void MotorClassico_MantemOComportamentoDeSempre()
        {
            var resultado = Calc(MotorNota.Classico).De(Jogo());

            var esperada = CriteriosNotaHelper.Compor(
                resultado.ValorAcoes, new List<CriterioNota>(),
                new CriteriosNotaHelper.ContextoNota(
                    Minutos: 90, Acoes: CriteriosNotaHelper.ContarAcoes(resultado.Detalhes)));

            Assert.Equal(esperada.Nota, resultado.Nota);
            Assert.Null(resultado.Rating);
        }

        [Fact]
        public void MotorAutomatico_UsaORating()
        {
            var resultado = Calc(MotorNota.Automatico).De(Jogo());

            Assert.NotNull(resultado.Rating);
            Assert.Equal(resultado.Rating!.Nota, resultado.Nota);
            Assert.Equal("ATACANTE", resultado.Rating.Grupo);
            Assert.Equal(RatingAutomaticoHelper.NotaNeutra, resultado.NotaBase);
        }

        [Fact]
        public void OsDoisMotoresDaoNotasDiferentes()
        {
            var classico = Calc(MotorNota.Classico).De(Jogo()).Nota;
            var automatico = Calc(MotorNota.Automatico).De(Jogo()).Nota;

            Assert.NotEqual(classico, automatico);
            Assert.InRange(automatico, RatingAutomaticoHelper.NotaMinima, RatingAutomaticoHelper.NotaMaxima);
        }

        [Fact]
        public void DetalhesSaoOsMesmosNosDoisMotores()
        {
            // Os chips descrevem o que o jogador fez; não dependem de como a nota é montada.
            var classico = Calc(MotorNota.Classico).De(Jogo()).Detalhes;
            var automatico = Calc(MotorNota.Automatico).De(Jogo()).Detalhes;

            Assert.Equal(
                classico.Select(d => (d.AcaoId, d.Quantidade)).OrderBy(x => x.AcaoId),
                automatico.Select(d => (d.AcaoId, d.Quantidade)).OrderBy(x => x.AcaoId));
        }

        [Fact]
        public void SemMinutos_MesmoNoAutomatico_CaiNoClassico()
        {
            // Sem tempo em campo o rating não existe; ficar sem nota nenhuma seria pior.
            var resultado = Calc(MotorNota.Automatico).De(Jogo(minutos: 0));

            Assert.Null(resultado.Rating);
            Assert.Equal(CriteriosNotaHelper.NotaBasePadrao, resultado.NotaBase);
        }

        [Fact]
        public void LinhasParciaisDoMesmoJogo_SaoConsolidadas()
        {
            var metade = new EstatisticaJogador
            {
                JogadorId = JogadorId, JogoId = JogoId, Minutos = 45,
                Gols = 1, PassesTotal = 15, PassesCertos = 12, DuelosTotal = 5, DuelosVencidos = 3,
            };
            var outraMetade = new EstatisticaJogador
            {
                JogadorId = JogadorId, JogoId = JogoId, Minutos = 45,
                PassesTotal = 15, PassesCertos = 12, DuelosTotal = 5, DuelosVencidos = 3,
            };

            var partido = Calc(MotorNota.Automatico).De(new[] { metade, outraMetade }, JogadorId, JogoId);

            var inteiro = new EstatisticaJogador
            {
                JogadorId = JogadorId, JogoId = JogoId, Minutos = 90,
                Gols = 1, PassesTotal = 30, PassesCertos = 24, DuelosTotal = 10, DuelosVencidos = 6,
            };
            var deUmaVez = Calc(MotorNota.Automatico).De(inteiro);

            // Somar as duas linhas sem juntar os minutos dobraria tudo por 90.
            Assert.Equal(deUmaVez.Nota, partido.Nota);
        }

        [Fact]
        public void ContextoDoRating_ChegaNaNota()
        {
            var vitoria = Calc(MotorNota.Automatico, new ContextoRating(ResultadoSinal: +1)).De(Jogo()).Nota;
            var derrota = Calc(MotorNota.Automatico, new ContextoRating(ResultadoSinal: -1)).De(Jogo()).Nota;

            Assert.True(vitoria > derrota);
        }

        [Fact]
        public void SemContextoDeRating_AindaCalcula()
        {
            var calculadora = new CalculadoraNotaAutomatica(
                MotorNota.Automatico, new List<CriterioNota>(), Lados,
                new Dictionary<(int, int), CriteriosNotaHelper.ContextoNota>(),
                new Dictionary<(int, int), ContextoRating>(),
                BaselineRating.Padrao);

            var resultado = calculadora.De(Jogo());
            Assert.NotNull(resultado.Rating);
            Assert.Equal(0, resultado.Rating!.Resultado);
        }
    }
}
