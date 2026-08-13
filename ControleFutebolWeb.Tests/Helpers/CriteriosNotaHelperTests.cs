using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class CriteriosNotaHelperTests
    {
        private static CriterioNota PesoInicial(double peso, bool ativo = true) => new()
        {
            AcaoId = CriteriosNotaHelper.AcaoPesoInicial,
            Label = CriteriosNotaHelper.LabelPesoInicial,
            Peso = peso,
            Ativo = ativo
        };

        private static CriterioNota Acao(string acaoId, double peso) => new()
        {
            AcaoId = acaoId, Label = acaoId, Peso = peso, Ativo = true
        };

        private static CriteriosNotaHelper.ContextoNota Ctx(
            int? minutos = null, bool goleiroDecisivo = false, (int Verdes, int Vermelhas)? acoes = null,
            bool golDaVitoria = false)
            => new(minutos, goleiroDecisivo,
                acoes is null ? null : new CriteriosNotaHelper.ContagemAcoes(acoes.Value.Verdes, acoes.Value.Vermelhas),
                golDaVitoria);

        private static Notadetalhe Chip(double peso, int quantidade = 1) => new()
        {
            AcaoId = "acao", AcaoLabel = "acao", Peso = peso, Quantidade = quantidade
        };

        [Fact]
        public void NotaBase_SemCriterioDoUsuario_UsaPadrao()
        {
            Assert.Equal(4.0, CriteriosNotaHelper.NotaBase(null));
            Assert.Equal(4.0, CriteriosNotaHelper.NotaBase(new List<CriterioNota>()));
            Assert.Equal(4.0, CriteriosNotaHelper.NotaBase(new[] { Acao("gol", 2.0) }));
        }

        [Fact]
        public void NotaBase_UsaPesoInicialDoUsuario()
        {
            Assert.Equal(6.5, CriteriosNotaHelper.NotaBase(new[] { PesoInicial(6.5), Acao("gol", 2.0) }));
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(10.5)]
        public void NotaBase_ForaDeZeroADez_CaiNoPadrao(double peso)
        {
            Assert.Equal(4.0, CriteriosNotaHelper.NotaBase(new[] { PesoInicial(peso) }));
        }

        [Fact]
        public void NotaBase_CriterioInativo_CaiNoPadrao()
        {
            Assert.Equal(4.0, CriteriosNotaHelper.NotaBase(new[] { PesoInicial(7.0, ativo: false) }));
        }

        [Fact]
        public void NotaFinal_SomaAcoesSobreOPesoInicial()
        {
            var criterios = new[] { PesoInicial(5.0) };

            Assert.Equal(7.5, CriteriosNotaHelper.NotaFinal(2.5, criterios));
            Assert.Equal(5.0, CriteriosNotaHelper.NotaFinal(0, criterios));
        }

        [Fact]
        public void NotaFinal_SaldoPositivoAbaixoDeSeis_SobeAtePisoDeMerecimento()
        {
            var criterios = new[] { PesoInicial(0.0) };

            // Caso do jogo "ok" de ações leves: +1,0 em ações não pode virar nota 1,0.
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinal(1.0, criterios));
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinal(0.1, criterios));
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinal(1.0, new[] { PesoInicial(4.0) }));
        }

        // ── Gatilho do merecimento: contagem de chips, não soma de pontos ─────

        [Fact]
        public void ContarAcoes_ContaCadaAcaoUmaVez_IgnorandoQuantidadeEPeso()
        {
            // O jogo do print: 3 verdes (finalização, duelo, falta sofrida) contra
            // 2 vermelhas (impedimento, falta cometida ×4).
            var detalhes = new List<Notadetalhe>
            {
                Chip(+0.3), Chip(+0.1), Chip(+0.1),
                Chip(-0.5), Chip(-0.5, quantidade: 4),
            };

            Assert.Equal(new CriteriosNotaHelper.ContagemAcoes(3, 2), CriteriosNotaHelper.ContarAcoes(detalhes));
        }

        [Fact]
        public void ContarAcoes_IgnoraAcoesNaoMarcadas_ENullSemNenhuma()
        {
            Assert.Equal(new CriteriosNotaHelper.ContagemAcoes(1, 0),
                CriteriosNotaHelper.ContarAcoes(new[] { Chip(+0.3), Chip(-0.5, quantidade: 0) }));
            Assert.Null(CriteriosNotaHelper.ContarAcoes(null));
            Assert.Null(CriteriosNotaHelper.ContarAcoes(new List<Notadetalhe>()));
            Assert.Null(CriteriosNotaHelper.ContarAcoes(new[] { Chip(+0.3, quantidade: 0) }));
        }

        [Fact]
        public void MaisChipsVerdes_GanhaMerecimentoAindaQueOsPontosFechemNegativo()
        {
            // Exatamente o caso do print: -2,0 em pontos, mas 3 verdes contra 2 vermelhas.
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 0.0, Ctx(minutos: 82, acoes: (3, 2))));
        }

        [Fact]
        public void MaisChipsVermelhos_NaoGanhaMerecimentoAindaQueOsPontosFechemPositivo()
        {
            // 1 verde pesada (gol) contra 2 vermelhas leves: os pontos sobem, o jogo não
            // vira "ok" — a nota calculada é que vale.
            Assert.Equal(1.8, CriteriosNotaHelper.NotaFinalComBase(1.8, 0.0, Ctx(minutos: 90, acoes: (1, 2))));
        }

        [Fact]
        public void EmpateDeChips_ValeCincoQuandoAnotaEstaAbaixo()
        {
            // O jogo do print: 1 verde (duelo vencido) x 1 vermelha (impedimento).
            Assert.Equal(5.0, CriteriosNotaHelper.NotaFinalComBase(-0.4, 0.0, Ctx(minutos: 58, acoes: (1, 1))));
            Assert.Equal(5.0, CriteriosNotaHelper.NotaFinalComBase(1.0, 0.0, Ctx(minutos: 90, acoes: (3, 3))));
        }

        [Fact]
        public void EmpateDeChips_NaoDerrubaQuemJaEstavaAcimaDeCinco()
        {
            Assert.Equal(6.5, CriteriosNotaHelper.NotaFinalComBase(2.5, 4.0, Ctx(minutos: 90, acoes: (2, 2))));
        }

        [Fact]
        public void PisoDeMerecimento_ResolveAsTresFaixas()
        {
            Assert.Equal(6.0, CriteriosNotaHelper.PisoDeMerecimento(0, new CriteriosNotaHelper.ContagemAcoes(3, 2)));
            Assert.Equal(5.0, CriteriosNotaHelper.PisoDeMerecimento(0, new CriteriosNotaHelper.ContagemAcoes(1, 1)));
            Assert.Equal(0.0, CriteriosNotaHelper.PisoDeMerecimento(0, new CriteriosNotaHelper.ContagemAcoes(1, 2)));
            // Contagem zerada não é empate: é jogo sem ação nenhuma.
            Assert.Equal(0.0, CriteriosNotaHelper.PisoDeMerecimento(0, new CriteriosNotaHelper.ContagemAcoes(0, 0)));
        }

        [Fact]
        public void SemDetalhes_OGatilhoCaiNoValorSomadoDasAcoes()
        {
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinalComBase(1.0, 0.0, Ctx(minutos: 90)));
            Assert.Equal(2.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx(minutos: 90)));
        }

        [Fact]
        public void NotaFinal_SaldoPositivoAcimaDeSeis_MantemONotaCalculada()
        {
            Assert.Equal(7.0, CriteriosNotaHelper.NotaFinal(7.0, new[] { PesoInicial(0.0) }));
            Assert.Equal(8.5, CriteriosNotaHelper.NotaFinal(4.5, new[] { PesoInicial(4.0) }));
        }

        [Fact]
        public void NotaFinal_SaldoNegativoOuZerado_NaoRecebeAjuste()
        {
            Assert.Equal(1.0, CriteriosNotaHelper.NotaFinal(-3.0, new[] { PesoInicial(4.0) }));
            Assert.Equal(0.0, CriteriosNotaHelper.NotaFinal(-1.0, new[] { PesoInicial(0.0) })); // não fica negativa
            Assert.Equal(4.0, CriteriosNotaHelper.NotaFinal(0.0, new[] { PesoInicial(4.0) }));  // sem ações, sem ajuste
        }

        [Fact]
        public void NotaFinal_NaoPassaDeDez()
        {
            Assert.Equal(10.0, CriteriosNotaHelper.NotaFinal(+9.0, new[] { PesoInicial(6.0) }));
        }

        [Fact]
        public void Compor_SeparaAsParcelasDoAjuste()
        {
            var c = CriteriosNotaHelper.Compor(1.0, 0.0, Ctx());
            Assert.Equal(5.0, c.Merecimento);  // 1,0 → 6,0
            Assert.Equal(6.0, c.Nota);
            Assert.Equal(6.0, c.PisoMerecimento);

            Assert.Equal(0.0, CriteriosNotaHelper.Compor(7.0, 0.0, Ctx()).TotalAjustes);  // já passou de 6
            Assert.Equal(0.0, CriteriosNotaHelper.Compor(-2.0, 4.0, Ctx()).TotalAjustes); // saldo negativo
        }

        // ── Participação curta (< 45 min) ────────────────────────────────────

        [Fact]
        public void MenosDe45Minutos_SobeAte5()
        {
            // Saldo negativo derrubaria para 2,0; entrou faltando pouco, então vale 5,0.
            Assert.Equal(5.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx(minutos: 20)));
            // Sem ação nenhuma em 10 minutos: 0,0 viraria nota; o piso segura em 5,0.
            Assert.Equal(5.0, CriteriosNotaHelper.NotaFinalComBase(0.0, 0.0, Ctx(minutos: 10)));
        }

        [Fact]
        public void MenosDe45Minutos_NaoDerrubaQuemJaEstavaAcimaDe5()
        {
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinalComBase(0.5, 0.0, Ctx(minutos: 20)));   // piso de merecimento
            Assert.Equal(8.0, CriteriosNotaHelper.NotaFinalComBase(4.0, 4.0, Ctx(minutos: 20)));
        }

        [Fact]
        public void PisoDeParticipacao_NaoValeParaQuemJogouOSuficiente()
        {
            Assert.Equal(2.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx(minutos: 45)));
            Assert.Equal(2.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx(minutos: 90)));
            Assert.Equal(2.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx()));          // minutos desconhecidos
            Assert.Equal(2.0, CriteriosNotaHelper.NotaFinalComBase(-2.0, 4.0, Ctx(minutos: 0))); // não entrou
        }

        // ── Goleiro decisivo (100% dos chutes no alvo) ───────────────────────

        [Fact]
        public void GoleiroDecisivo_GanhaDoisPontosQuandoAbaixoDeSete()
        {
            // 2 defesas (+1,0) com base 0 → piso de merecimento 6,0 → +2 do bônus.
            Assert.Equal(8.0, CriteriosNotaHelper.NotaFinalComBase(1.0, 0.0, Ctx(goleiroDecisivo: true)));
            Assert.Equal(6.5, CriteriosNotaHelper.NotaFinalComBase(-0.5, 5.0, Ctx(goleiroDecisivo: true)));
        }

        [Fact]
        public void GoleiroDecisivo_NaoGanhaNadaQuandoAnotaJaAlcancouSete()
        {
            Assert.Equal(7.0, CriteriosNotaHelper.NotaFinalComBase(3.0, 4.0, Ctx(goleiroDecisivo: true)));
            Assert.Equal(9.0, CriteriosNotaHelper.NotaFinalComBase(5.0, 4.0, Ctx(goleiroDecisivo: true)));
        }

        [Fact]
        public void GoleiroDecisivo_ApareceComoParcelaPropria()
        {
            var c = CriteriosNotaHelper.Compor(1.0, 0.0, Ctx(minutos: 90, goleiroDecisivo: true));

            Assert.Equal(5.0, c.Merecimento);
            Assert.Equal(0.0, c.ParticipacaoCurta);
            Assert.Equal(2.0, c.GoleiroDecisivo);
            Assert.Equal(8.0, c.Nota);
        }

        // ── Gol da vitória ───────────────────────────────────────────────────

        [Fact]
        public void GolDaVitoria_SomaUmPonto()
        {
            // Gol (+2) sobre base 4 dá 6,0; o gol foi o que decidiu → 7,0.
            Assert.Equal(7.0, CriteriosNotaHelper.NotaFinalComBase(2.0, 4.0, Ctx(minutos: 90, golDaVitoria: true)));
            Assert.Equal(6.0, CriteriosNotaHelper.NotaFinalComBase(2.0, 4.0, Ctx(minutos: 90)));
        }

        [Fact]
        public void GolDaVitoria_NaoTemTeto_MasRespeitaODez()
        {
            // Ao contrário do bônus do goleiro, entra mesmo com a nota já alta.
            Assert.Equal(9.0, CriteriosNotaHelper.NotaFinalComBase(4.0, 4.0, Ctx(minutos: 90, golDaVitoria: true)));
            Assert.Equal(10.0, CriteriosNotaHelper.NotaFinalComBase(6.0, 4.0, Ctx(minutos: 90, golDaVitoria: true)));
        }

        [Fact]
        public void GolDaVitoria_ApareceComoParcelaPropria()
        {
            var c = CriteriosNotaHelper.Compor(2.0, 4.0, Ctx(minutos: 90, golDaVitoria: true));

            Assert.Equal(1.0, c.GolDaVitoria);
            Assert.Equal(0.0, c.Merecimento);
            Assert.Equal(7.0, c.Nota);
        }

        [Fact]
        public void Ajustes_NuncaPassamDeDez()
        {
            // O bônus só entra abaixo de 7, então o máximo que ele alcança é 8,99...
            Assert.Equal(8.5, CriteriosNotaHelper.NotaFinalComBase(2.5, 6.0, Ctx(goleiroDecisivo: true)));
            Assert.Equal(8.9, CriteriosNotaHelper.NotaFinalComBase(2.9, 4.0, Ctx(goleiroDecisivo: true)));
            // O teto continua vindo do próprio "base + ações".
            Assert.Equal(10.0, CriteriosNotaHelper.NotaFinalComBase(9.0, 6.0, Ctx(minutos: 20, goleiroDecisivo: true)));
        }

        [Fact]
        public void CalcularPontuacao_IgnoraOCriterioDePesoInicial()
        {
            var e = new EstatisticaJogador { Gols = 1 };
            var criterios = new List<CriterioNota> { PesoInicial(9.0), Acao("gol", 2.0) };

            // O peso inicial não tem extrator: entra na base, nunca no valor das ações.
            Assert.Equal(2.0, CriteriosNotaHelper.CalcularPontuacao(e, criterios));
            Assert.Equal(11.0, 9.0 + CriteriosNotaHelper.CalcularPontuacao(e, criterios));
            Assert.Equal(10.0, CriteriosNotaHelper.NotaFinal(CriteriosNotaHelper.CalcularPontuacao(e, criterios), criterios));
        }

        [Fact]
        public void ConstruirDetalhes_NaoListaOPesoInicial()
        {
            var e = new EstatisticaJogador { Gols = 1 };
            var criterios = new List<CriterioNota> { PesoInicial(5.0), Acao("gol", 2.0) };

            var detalhes = CriteriosNotaHelper.ConstruirDetalhes(e, criterios);

            Assert.Single(detalhes);
            Assert.Equal("gol", detalhes[0].AcaoId);
        }

        [Fact]
        public void SomenteAcoes_RemoveOPesoInicial()
        {
            var criterios = new List<CriterioNota> { PesoInicial(5.0), Acao("gol", 2.0), Acao("assistencia", 1.0) };

            var acoes = CriteriosNotaHelper.SomenteAcoes(criterios);

            Assert.Equal(2, acoes.Count);
            Assert.DoesNotContain(acoes, c => c.AcaoId == CriteriosNotaHelper.AcaoPesoInicial);
        }

        [Fact]
        public void MergeCriterios_PesoInicialDoUsuarioSobrepoeOCompartilhado()
        {
            var compartilhados = new List<CriterioNota> { PesoInicial(4.0), Acao("gol", 2.0) };
            var doUsuario = new List<CriterioNota> { PesoInicial(6.0) };

            var merged = CriteriosNotaHelper.MergeCriterios(compartilhados, doUsuario);

            Assert.Equal(6.0, CriteriosNotaHelper.NotaBase(merged));
        }
    }
}
