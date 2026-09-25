using ControleFutebolWeb.Helpers.Rating;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class RatingAutomaticoHelperTests
    {
        // Jogador "médio" da posição: todos os valores iguais à média do baseline
        // padrão, para 90 minutos. Serve de linha de base dos testes — a nota dele
        // tem que ser o neutro.
        private static EstatisticaJogador Mediano(string grupo, int minutos = 90)
        {
            var e = new EstatisticaJogador { Minutos = minutos };
            double Media(string id) => BaselineRating.Padrao.De(grupo, id)!.Media;
            var fator = minutos / 90.0;

            int Conta(string id) => (int)Math.Round(Media(id) * fator);

            e.FinalizacoesTotal = Conta("finalizacoes");
            e.FinalizacoesNoGol = Conta("finalizacoes_alvo");
            e.Offsides = Conta("offsides");
            e.PassesChave = Conta("passes_chave");
            e.FaltasSofridas = Conta("faltas_sofridas");
            e.Desarmes = Conta("desarmes");
            e.Interceptacoes = Conta("interceptacoes");
            e.Bloqueios = Conta("bloqueios");
            e.DriblesSofridos = Conta("dribles_sofridos");
            e.Defesas = Conta("defesas");
            e.GolsSofridos = Conta("gols_sofridos");
            e.FaltasCometidas = Conta("faltas_cometidas");
            e.CartoesAmarelos = Conta("cartoes_amarelos");

            // Tentativa + acerto na média da taxa
            e.PassesTotal = Conta("passes");
            e.PassesCertos = (int)Math.Round(e.PassesTotal * Media("passes_precisao") / 100.0);
            e.DuelosTotal = Conta("duelos");
            e.DuelosVencidos = (int)Math.Round(e.DuelosTotal * Media("duelos_precisao") / 100.0);
            e.DriblesTentados = Conta("dribles");
            e.DriblesCertos = (int)Math.Round(e.DriblesTentados * Media("dribles_precisao") / 100.0);

            return e;
        }

        private static double NotaDe(EstatisticaJogador e, string posicao, ContextoRating contexto = default)
            => RatingAutomaticoHelper.Calcular(e, posicao, contexto)!.Nota;

        [Fact]
        public void SemMinutos_NaoTemNota()
        {
            Assert.Null(RatingAutomaticoHelper.Calcular(new EstatisticaJogador { Minutos = 0 }, "Zagueiro"));
            Assert.Null(RatingAutomaticoHelper.Calcular(new EstatisticaJogador { Minutos = null }, "Zagueiro"));
        }

        [Theory]
        [InlineData("Goleiro")]
        [InlineData("Zagueiro Central")]
        [InlineData("Lateral Direito")]
        [InlineData("Volante")]
        [InlineData("Meia Ofensivo")]
        [InlineData("Ponta Esquerda")]
        [InlineData("Centroavante")]
        public void JogadorMedianoDaPosicao_FicaNoNeutro(string posicao)
        {
            var grupo = ControleFutebolWeb.Helpers.CriteriosNotaHelper.GrupoDaPosicao(posicao)!;
            var nota = NotaDe(Mediano(grupo), posicao);

            // O arredondamento das contagens para inteiro tira a nota do 6,00 exato.
            Assert.InRange(nota, 5.4, 6.6);
        }

        [Fact]
        public void MesmaAtuacaoDefensiva_ValeMaisParaZagueiroQueParaAtacante()
        {
            var e = new EstatisticaJogador
            {
                Minutos = 90, Desarmes = 7, Interceptacoes = 5, Bloqueios = 3,
                DuelosTotal = 14, DuelosVencidos = 11, PassesTotal = 60, PassesCertos = 54,
            };

            Assert.True(NotaDe(e, "Zagueiro Central") > NotaDe(e, "Centroavante"),
                "a régua de desarme/interceptação tem que pesar mais para quem defende");
        }

        [Fact]
        public void Volume_Satura_EmDoisDesvios()
        {
            EstatisticaJogador Com(int desarmes) => new()
            {
                Minutos = 90, Desarmes = desarmes, PassesTotal = 55, PassesCertos = 47,
                DuelosTotal = 8, DuelosVencidos = 5,
            };

            // 6 desarmes já passa de +2σ para zagueiro (μ 1,8 / σ 1,3); de lá para 20
            // não pode sobrar quase nada a ganhar.
            var seis = NotaDe(Com(6), "Zagueiro Central");
            var vinte = NotaDe(Com(20), "Zagueiro Central");

            Assert.Equal(seis, vinte, precision: 2);
        }

        [Fact]
        public void TentarMuitoEErrar_Desconta()
        {
            EstatisticaJogador Com(int certos) => new()
            {
                Minutos = 90, DriblesTentados = 10, DriblesCertos = certos,
                PassesTotal = 40, PassesCertos = 32, DuelosTotal = 12, DuelosVencidos = 6,
            };

            Assert.True(NotaDe(Com(1), "Ponta Esquerda") < NotaDe(Com(8), "Ponta Esquerda"),
                "1/10 nos dribles não pode valer o mesmo que 8/10");
        }

        [Fact]
        public void Gols_TemRetornoDecrescente()
        {
            var um = RatingAutomaticoHelper.Saturar(1, 1.2);
            var dois = RatingAutomaticoHelper.Saturar(2, 1.2);
            var tres = RatingAutomaticoHelper.Saturar(3, 1.2);

            Assert.True(dois > um && tres > dois);
            Assert.True(dois - um < um, "o 2º gol tem que valer menos que o 1º");
            Assert.True(tres - dois < dois - um, "o 3º tem que valer menos que o 2º");
        }

        [Fact]
        public void NotaMaxima_ExigeAtuacaoForaDaCurva()
        {
            // Jogo excelente, mas não absurdo: 2 gols e 1 assistência com bom volume.
            var bom = new EstatisticaJogador
            {
                Minutos = 90, Gols = 2, Assistencias = 1, FinalizacoesTotal = 5, FinalizacoesNoGol = 4,
                PassesChave = 3, PassesTotal = 30, PassesCertos = 26,
                DriblesTentados = 5, DriblesCertos = 4, DuelosTotal = 12, DuelosVencidos = 8,
            };

            var nota = NotaDe(bom, "Centroavante", new ContextoRating(ResultadoSinal: +1));
            Assert.InRange(nota, 8.0, 9.6);
            Assert.True(nota < RatingAutomaticoHelper.NotaMaxima, "2 gols não podem já ser nota 10");
        }

        [Fact]
        public void Compressao_NuncaPassaDoTeto()
        {
            // O teto é assíntota da própria curva: nem precisa do clamp para valer.
            Assert.True(RatingAutomaticoHelper.Comprimir(20) <= RatingAutomaticoHelper.NotaMaxima);
            Assert.True(RatingAutomaticoHelper.Comprimir(1000) <= RatingAutomaticoHelper.NotaMaxima);
            Assert.True(RatingAutomaticoHelper.Comprimir(12) < RatingAutomaticoHelper.NotaMaxima);
            // Abaixo do limite suave nada é alterado.
            Assert.Equal(7.3, RatingAutomaticoHelper.Comprimir(7.3), precision: 6);
        }

        [Fact]
        public void ParticipacaoCurta_PuxaParaONeutro_SemPisoArtificial()
        {
            var ruim = new EstatisticaJogador
            {
                Minutos = 10, PassesTotal = 4, PassesCertos = 1, DuelosTotal = 3, DuelosVencidos = 0,
                FaltasCometidas = 1,
            };
            var mesmoJogoInteiro = new EstatisticaJogador
            {
                Minutos = 90, PassesTotal = 36, PassesCertos = 9, DuelosTotal = 27, DuelosVencidos = 0,
                FaltasCometidas = 9,
            };

            var curta = NotaDe(ruim, "Meia Ofensivo");
            var inteira = NotaDe(mesmoJogoInteiro, "Meia Ofensivo");

            Assert.True(curta > inteira, "10 minutos ruins não podem pesar como 90 minutos ruins");
            Assert.True(curta < RatingAutomaticoHelper.NotaNeutra, "mas também não podem virar bônus");
        }

        [Fact]
        public void GolContraEVermelho_Derrubam()
        {
            var e = Mediano("ZAGUEIRO");
            var limpo = NotaDe(e, "Zagueiro Central");

            var comVermelho = new EstatisticaJogador { Minutos = 90, CartoesVermelhos = 1 };
            CopiarDe(e, comVermelho);
            Assert.True(NotaDe(comVermelho, "Zagueiro Central") < limpo - 1.0);

            var golContra = NotaDe(e, "Zagueiro Central", new ContextoRating(GolsContra: 1));
            Assert.True(golContra < limpo - 1.0);
        }

        [Fact]
        public void GolsSofridosDoTime_EntramNaNotaDoDefensor_EmEscala()
        {
            var e = Mediano("ZAGUEIRO");

            var semSofrer = NotaDe(e, "Zagueiro Central", new ContextoRating(GolsSofridosTime: 0));
            var umGol = NotaDe(e, "Zagueiro Central", new ContextoRating(GolsSofridosTime: 1));
            var goleada = NotaDe(e, "Zagueiro Central", new ContextoRating(GolsSofridosTime: 4));

            Assert.True(semSofrer > umGol && umGol > goleada,
                "não sofrer gol tem que valer mais que sofrer 1, que vale mais que levar 4");
        }

        [Fact]
        public void GoleiroNaoEPunidoDuasVezesPeloMesmoPlacar()
        {
            var e = Mediano("GOLEIRO");
            e.GolsSofridos = 3;

            var comContexto = RatingAutomaticoHelper.Calcular(e, "Goleiro", new ContextoRating(GolsSofridosTime: 3))!;
            Assert.Equal(0, comContexto.Defensivo);
        }

        [Fact]
        public void Goleiro_DefesasAlemDeDoisDesviosAindaContam()
        {
            var sete = Mediano("GOLEIRO");
            sete.Defesas = 7;
            var onze = Mediano("GOLEIRO");
            onze.Defesas = 11;

            Assert.True(NotaDe(onze, "Goleiro") > NotaDe(sete, "Goleiro") + 0.3,
                "11 defesas não podem valer o mesmo que 7");
        }

        [Fact]
        public void Goleiro_SemSofrerGol_EhEvento_SoComTempoEmCampo()
        {
            var e = Mediano("GOLEIRO");
            e.GolsSofridos = 0;

            var limpo = RatingAutomaticoHelper.Calcular(e, "Goleiro", new ContextoRating(GolsSofridosTime: 0))!;
            var vazado = RatingAutomaticoHelper.Calcular(e, "Goleiro", new ContextoRating(GolsSofridosTime: 1))!;
            Assert.Contains(limpo.EventosDetalhe, ev => ev.Label == "Não sofreu gol");
            Assert.DoesNotContain(vazado.EventosDetalhe, ev => ev.Label == "Não sofreu gol");

            var entrouNoFim = new EstatisticaJogador { Minutos = 20 };
            var curta = RatingAutomaticoHelper.Calcular(entrouNoFim, "Goleiro", new ContextoRating(GolsSofridosTime: 0))!;
            Assert.Empty(curta.EventosDetalhe);

            // Jogador de linha continua pela parcela defensiva, não pelo evento.
            var zagueiro = RatingAutomaticoHelper.Calcular(Mediano("ZAGUEIRO"), "Zagueiro Central",
                new ContextoRating(GolsSofridosTime: 0))!;
            Assert.DoesNotContain(zagueiro.EventosDetalhe, ev => ev.Label == "Não sofreu gol");
        }

        [Fact]
        public void Goleiro_JogoExcepcional_ChegaNaFaixaAlta()
        {
            // Caso real (Weverton, 0x0): 11 defesas, 0 gols, 11/23 passes.
            var e = new EstatisticaJogador
            {
                Minutos = 90, Defesas = 11, PassesTotal = 23, PassesCertos = 11,
                DuelosTotal = 2, DuelosVencidos = 2, FaltasSofridas = 2,
            };
            var nota = NotaDe(e, "Goleiro", new ContextoRating(ResultadoSinal: 0, GolsSofridosTime: 0));
            Assert.True(nota >= 8.0, $"jogo de 11 defesas sem sofrer gol ficou em {nota}");
        }

        [Fact]
        public void Resultado_PesaPoucoEProporcionalAoTempoEmCampo()
        {
            var e = Mediano("MEIA");

            var vitoria = NotaDe(e, "Meia Ofensivo", new ContextoRating(ResultadoSinal: +1));
            var derrota = NotaDe(e, "Meia Ofensivo", new ContextoRating(ResultadoSinal: -1));

            Assert.True(vitoria > derrota);
            Assert.True(vitoria - derrota <= 2 * RatingAutomaticoHelper.PesoResultado + 0.01,
                "o resultado é do time, não do jogador — não pode virar a nota");
        }

        [Fact]
        public void PosicaoDesconhecida_UsaReguaNeutra_SemQuebrar()
        {
            var composicao = RatingAutomaticoHelper.Calcular(
                new EstatisticaJogador { Minutos = 90, PassesTotal = 40, PassesCertos = 34 }, null);

            Assert.NotNull(composicao);
            Assert.Null(composicao!.Grupo);
            Assert.InRange(composicao.Nota, RatingAutomaticoHelper.NotaMinima, RatingAutomaticoHelper.NotaMaxima);
        }

        [Fact]
        public void Composicao_ExplicaAConta()
        {
            var c = RatingAutomaticoHelper.Calcular(
                new EstatisticaJogador
                {
                    Minutos = 90, Gols = 1, PassesTotal = 30, PassesCertos = 25,
                    DuelosTotal = 10, DuelosVencidos = 6,
                },
                "Centroavante", new ContextoRating(ResultadoSinal: +1))!;

            Assert.Equal("ATACANTE", c.Grupo);
            Assert.Contains(c.EventosDetalhe, ev => ev.Label == "Gol" && ev.Quantidade == 1);
            Assert.NotEmpty(c.Familias);

            // As parcelas relatadas têm que reconstruir a nota bruta.
            var soma = c.NotaBase + c.Desempenho + c.Eventos + c.Defensivo + c.Resultado;
            Assert.Equal(c.Bruta, soma, precision: 2);
        }

        // Copia as métricas contínuas de uma linha para outra, preservando o que já
        // foi setado no destino (usado para variar um único fator por vez).
        private static void CopiarDe(EstatisticaJogador origem, EstatisticaJogador destino)
        {
            destino.FinalizacoesTotal = origem.FinalizacoesTotal;
            destino.FinalizacoesNoGol = origem.FinalizacoesNoGol;
            destino.PassesTotal = origem.PassesTotal;
            destino.PassesCertos = origem.PassesCertos;
            destino.PassesChave = origem.PassesChave;
            destino.Desarmes = origem.Desarmes;
            destino.Interceptacoes = origem.Interceptacoes;
            destino.Bloqueios = origem.Bloqueios;
            destino.DuelosTotal = origem.DuelosTotal;
            destino.DuelosVencidos = origem.DuelosVencidos;
            destino.FaltasCometidas = origem.FaltasCometidas;
        }
    }

    public class BaselineRatingTests
    {
        [Fact]
        public void PadraoCobreTodasAsMetricasDeTodososGrupos()
        {
            foreach (var (_, doGrupo) in BaselineRating.Padrao.Grupos)
                foreach (var m in MetricasRating.Todas)
                    Assert.True(doGrupo.ContainsKey(m.Id), $"faltou {m.Id}");
        }

        [Fact]
        public void PesosDeCadaPosicaoSomamUm()
        {
            foreach (var grupo in BaselineRating.Padrao.Grupos.Keys)
                Assert.Equal(1.0, PesosRating.Do(grupo).Values.Sum(), precision: 6);

            Assert.Equal(1.0, PesosRating.Neutro.Values.Sum(), precision: 6);
        }

        [Fact]
        public void ZTruncado_RespeitaOLimite()
        {
            var r = new ReferenciaMetrica(2.0, 1.0, 100);
            Assert.Equal(2.0, BaselineRating.ZTruncado(50, r));
            Assert.Equal(-2.0, BaselineRating.ZTruncado(-50, r));
            Assert.Equal(0.0, BaselineRating.ZTruncado(2.0, r));
        }

        [Fact]
        public void ZTruncado_NaoExplodeComDesvioZero()
        {
            var r = new ReferenciaMetrica(0.0, 0.0, 100);
            var z = BaselineRating.ZTruncado(0.01, r);
            Assert.InRange(z, -2, 2);
            Assert.True(Math.Abs(z) < 0.5, "uma ocorrência mínima não pode virar +2σ por falta de dispersão");
        }

        [Fact]
        public void Montar_CalculaMediaPor90Minutos()
        {
            // Dois jogos de 45 minutos com 1 desarme cada = 2 desarmes por 90.
            var amostras = Enumerable.Range(0, 40).Select(_ => new BaselineRating.Amostra(
                "ZAGUEIRO", 45, new EstatisticaJogador { Minutos = 45, Desarmes = 1 }));

            var baseline = BaselineRating.Montar(amostras.ToList());
            Assert.Equal(2.0, baseline.Grupos["ZAGUEIRO"]["desarmes"].Media, precision: 4);
        }

        [Fact]
        public void Montar_IgnoraParticipacoesCurtas()
        {
            var amostras = Enumerable.Range(0, 40).Select(_ => new BaselineRating.Amostra(
                "ZAGUEIRO", 5, new EstatisticaJogador { Minutos = 5, Desarmes = 1 }));

            var baseline = BaselineRating.Montar(amostras.ToList());
            Assert.Empty(baseline.Grupos);
        }

        [Fact]
        public void AmostraPequena_CaiNoPadrao()
        {
            var baseline = BaselineRating.Montar(new[]
            {
                new BaselineRating.Amostra("ZAGUEIRO", 90, new EstatisticaJogador { Minutos = 90, Desarmes = 99 }),
            });

            var referencia = baseline.De("ZAGUEIRO", "desarmes")!;
            Assert.Equal(BaselineRating.Padrao.De("ZAGUEIRO", "desarmes")!.Media, referencia.Media);
        }

        [Fact]
        public void RoundTripJson()
        {
            var original = BaselineRating.Montar(Enumerable.Range(0, 30).Select(i =>
                new BaselineRating.Amostra("MEIA", 90, new EstatisticaJogador { Minutos = 90, PassesChave = i % 4 })));

            var voltou = BaselineRating.DeJson(original.ParaJson())!;

            Assert.Equal(original.CalibradoEm, voltou.CalibradoEm);
            Assert.Equal(original.Grupos["MEIA"]["passes_chave"].Media, voltou.Grupos["MEIA"]["passes_chave"].Media);
        }

        [Fact]
        public void JsonInvalido_NaoQuebra()
        {
            Assert.Null(BaselineRating.DeJson(null));
            Assert.Null(BaselineRating.DeJson(""));
            Assert.Null(BaselineRating.DeJson("{nao é json"));
            Assert.Null(BaselineRating.DeJson("{\"grupos\":{}}"));
        }
    }
}
