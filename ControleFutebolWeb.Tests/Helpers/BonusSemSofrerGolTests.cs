using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    // O bônus "não sofreu gol" (+2) tem que olhar a posição do jogador NAQUELE jogo,
    // vinda da escalação — não a posição do cadastro (Jogador.Posicao), que é o
    // agregado das posições em que ele mais atua.
    public class BonusSemSofrerGolTests
    {
        private const int JogadorId = 43332;
        private const int JogoId = 15867;

        // Lateral no cadastro, como o Bernabei ("Ala Esquerdo/Lateral Esquerdo").
        private static EstatisticaJogador EstatisticaEmJogoSemGols(int minutos = 90) => new()
        {
            JogadorId = JogadorId,
            JogoId = JogoId,
            Minutos = minutos,
            Jogador = new Jogador { Id = JogadorId, Nome = "Bernabei", Posicao = "Ala Esquerdo/Lateral Esquerdo", TimeId = 1753 },
            Jogo = new Jogo { Id = JogoId, TimeCasaId = 1760, TimeVisitanteId = 1753, PlacarCasa = 0, PlacarVisitante = 0 },
        };

        private static Dictionary<(int, int), AtuacaoNoJogo> Escalado(string? posicaoNoJogo, bool isTimeCasa = false)
            => new() { [(JogadorId, JogoId)] = new AtuacaoNoJogo(isTimeCasa, posicaoNoJogo) };

        private static double Pontuacao(EstatisticaJogador e, Dictionary<(int, int), AtuacaoNoJogo>? lados,
            IEnumerable<CriterioNota>? criterios = null)
            => CriteriosNotaHelper.CalcularPontuacao(e, criterios ?? new List<CriterioNota>(), lados);

        // Critério especial que a tela /CriteriosNota grava.
        private static List<CriterioNota> Configurado(double peso, params string[] posicoes) => new()
        {
            new CriterioNota
            {
                AcaoId = CriteriosNotaHelper.AcaoSemSofrerGol,
                Label = CriteriosNotaHelper.LabelSemSofrerGol,
                Peso = peso,
                Config = string.Join(';', posicoes),
                Ativo = true,
            }
        };

        [Fact]
        public void LateralEscaladoAdiantado_NaoGanhaOBonus()
        {
            var pontos = Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Meia Direita"));

            Assert.Equal(0, pontos);
        }

        [Fact]
        public void LateralEscaladoNaDefesa_GanhaOBonus()
        {
            var pontos = Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Lateral Esquerdo"));

            Assert.Equal(CriteriosNotaHelper.BonusGoleiroSemSofrerGol, pontos);
        }

        [Fact]
        public void PosicaoGenericaDaApi_TambemVale()
        {
            Assert.Equal(CriteriosNotaHelper.BonusGoleiroSemSofrerGol,
                Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Defensor")));
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Atacante")));
        }

        [Fact]
        public void SemEscalacaoNoJogo_NaoGanhaOBonus()
        {
            // Sem escalação não dá para saber a posição naquela partida — cair na
            // posição do cadastro é justamente o bug que essa regra corrige.
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), null));
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), new Dictionary<(int, int), AtuacaoNoJogo>()));
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado(null)));
        }

        [Fact]
        public void QuemNaoJogou_NaoGanhaOBonus()
        {
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(minutos: 0), Escalado("Zagueiro Central")));
        }

        [Fact]
        public void TimeSofreuGol_NaoGanhaOBonus()
        {
            var e = EstatisticaEmJogoSemGols();
            e.Jogo!.PlacarCasa = 1; // ele é visitante: 1 gol sofrido

            Assert.Equal(0, Pontuacao(e, Escalado("Zagueiro Central")));
        }

        [Fact]
        public void LadoVemDaEscalacao_NaoDoTimeAtual()
        {
            // Jogo 2×0: quem estava na casa não sofreu gol; o visitante sofreu 2.
            var e = EstatisticaEmJogoSemGols();
            e.Jogo!.PlacarCasa = 2;
            e.Jogo.PlacarVisitante = 0;

            Assert.Equal(CriteriosNotaHelper.BonusGoleiroSemSofrerGol,
                Pontuacao(e, Escalado("Zagueiro Central", isTimeCasa: true)));
            Assert.Equal(0, Pontuacao(e, Escalado("Zagueiro Central", isTimeCasa: false)));
        }

        [Fact]
        public void ConstruirDetalhes_SoListaOBonusQuandoEleVale()
        {
            var comBonus = CriteriosNotaHelper.ConstruirDetalhes(
                EstatisticaEmJogoSemGols(), new List<CriterioNota>(), Escalado("Lateral Esquerdo"));
            var semBonus = CriteriosNotaHelper.ConstruirDetalhes(
                EstatisticaEmJogoSemGols(), new List<CriterioNota>(), Escalado("Meia Direita"));

            Assert.Contains(comBonus, d => d.AcaoId == "sem_sofrer_gol");
            Assert.DoesNotContain(semBonus, d => d.AcaoId == "sem_sofrer_gol");
        }

        [Fact]
        public void ValorDoBonus_VemDoCriterioConfigurado()
        {
            var criterios = Configurado(1.0, "GOLEIRO", "ZAGUEIRO", "LATERAL", "ALA");

            Assert.Equal(1.0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Lateral Esquerdo"), criterios));
        }

        [Fact]
        public void PosicoesConfiguradas_DefinemQuemRecebe()
        {
            // Só volante e meia recebem: o lateral daquele jogo fica sem o bônus.
            var criterios = Configurado(1.5, "VOLANTE", "MEIA");

            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Lateral Esquerdo"), criterios));
            Assert.Equal(1.5, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Volante"), criterios));
            Assert.Equal(1.5, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Meia Direita"), criterios));
        }

        [Fact]
        public void SemNenhumaPosicaoMarcada_BonusDesligado()
        {
            var criterios = Configurado(2.0);

            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Zagueiro Central"), criterios));
            Assert.Equal(0, Pontuacao(EstatisticaEmJogoSemGols(), Escalado("Goleiro"), criterios));
        }

        [Fact]
        public void DetalheDaNota_MostraOValorConfigurado()
        {
            var detalhes = CriteriosNotaHelper.ConstruirDetalhes(
                EstatisticaEmJogoSemGols(), Configurado(1.0, "LATERAL"), Escalado("Lateral Esquerdo"));

            var bonus = Assert.Single(detalhes, d => d.AcaoId == CriteriosNotaHelper.AcaoSemSofrerGol);
            Assert.Equal(1.0, bonus.Peso);
            Assert.Equal(1, bonus.Quantidade);
        }

        [Theory]
        [InlineData("Goleiro", "GOLEIRO")]
        [InlineData("Zagueiro Central", "ZAGUEIRO")]
        [InlineData("Defensor", "ZAGUEIRO")]          // rótulo genérico da api-football
        [InlineData("Lateral Esquerdo", "LATERAL")]
        [InlineData("Ala Direito", "ALA")]
        [InlineData("Volante", "VOLANTE")]
        [InlineData("Meia Ofensivo", "MEIA")]
        [InlineData("Ponta Direita", "PONTA")]
        [InlineData("Centroavante Esquerdo", "ATACANTE")]
        [InlineData("Atacante", "ATACANTE")]
        public void GrupoDaPosicao_ReconheceGranularEGenerico(string posicao, string esperado)
        {
            Assert.Equal(esperado, CriteriosNotaHelper.GrupoDaPosicao(posicao));
        }

        [Fact]
        public void GrupoDaPosicao_DesconhecidaEhNull()
        {
            Assert.Null(CriteriosNotaHelper.GrupoDaPosicao(null));
            Assert.Null(CriteriosNotaHelper.GrupoDaPosicao(""));
            Assert.Null(CriteriosNotaHelper.GrupoDaPosicao("Técnico"));
        }

        [Fact]
        public void SemCriterioSalvo_MantemOPadrao()
        {
            Assert.Equal(CriteriosNotaHelper.PosicoesSemSofrerGolPadrao,
                CriteriosNotaHelper.PosicoesSemSofrerGol(null));
            Assert.Equal(CriteriosNotaHelper.BonusGoleiroSemSofrerGol,
                CriteriosNotaHelper.BonusSemSofrerGol(null));
        }

        [Fact]
        public void SerializarPosicoes_DescartaInvalidasERepetidas()
        {
            Assert.Equal("GOLEIRO;ZAGUEIRO",
                CriteriosNotaHelper.SerializarPosicoes(new[] { "goleiro", "ZAGUEIRO", "GOLEIRO", "TECNICO" }));
        }

        [Fact]
        public void SomenteAcoes_TiraOBonusDaListaDeAcoes()
        {
            var criterios = Configurado(2.0, "GOLEIRO");
            criterios.Add(new CriterioNota { AcaoId = "gol", Label = "Gol", Peso = 2.0, Ativo = true });

            var acoes = CriteriosNotaHelper.SomenteAcoes(criterios);

            Assert.Single(acoes);
            Assert.Equal("gol", acoes[0].AcaoId);
        }

        [Fact]
        public void Montar_PreferAEscalacaoInicialEUsaAPosicaoGranularDoSlot()
        {
            var slots = new Dictionary<int, List<PosicaoFormacao>>
            {
                [20] = new()
                {
                    new PosicaoFormacao { FormacaoId = 20, NomePosicao = "Meia Direita", PosicaoX = 68, PosicaoY = 36 },
                    new PosicaoFormacao { FormacaoId = 20, NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 80 },
                }
            };
            var formacoes = new Dictionary<int, (int? Casa, int? Visitante)> { [JogoId] = (20, 20) };

            var escalacoes = new List<Escalacao>
            {
                // Entrou na vaga de um lateral na escalação FINAL — a coordenada de lá
                // é do substituído e não pode virar a posição dele.
                new() { JogadorId = JogadorId, JogoId = JogoId, IsTimeCasa = false, FaseEscalacao = "FINAL",
                        Posicao = "Defensor", PosicaoX = 20, PosicaoY = 80 },
                new() { JogadorId = JogadorId, JogoId = JogoId, IsTimeCasa = false, FaseEscalacao = "INICIAL",
                        Posicao = "Atacante", PosicaoX = 68, PosicaoY = 36 },
            };

            var mapa = LadoJogadorHelper.Montar(escalacoes, slots, formacoes);

            Assert.Equal("Meia Direita", mapa[(JogadorId, JogoId)].Posicao);
            Assert.False(mapa[(JogadorId, JogoId)].IsTimeCasa);
        }

        [Fact]
        public void Montar_SemSlotsCaiNoTextoDaEscalacao()
        {
            var escalacoes = new List<Escalacao>
            {
                new() { JogadorId = JogadorId, JogoId = JogoId, IsTimeCasa = true, FaseEscalacao = "INICIAL", Posicao = "Defensor" },
            };

            var mapa = LadoJogadorHelper.Montar(escalacoes);

            Assert.Equal("Defensor", mapa[(JogadorId, JogoId)].Posicao);
        }

        [Fact]
        public void Montar_ReservaNaoTemPosicao()
        {
            var escalacoes = new List<Escalacao>
            {
                new() { JogadorId = JogadorId, JogoId = JogoId, IsTimeCasa = true, FaseEscalacao = "INICIAL", Posicao = "RES" },
            };

            Assert.Null(LadoJogadorHelper.Montar(escalacoes)[(JogadorId, JogoId)].Posicao);
        }
    }
}
