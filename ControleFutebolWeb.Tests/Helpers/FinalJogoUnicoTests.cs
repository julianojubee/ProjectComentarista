using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class FinalJogoUnicoTests
    {
        private static List<int> Ids(int n) => Enumerable.Range(1, n).ToList();

        private static List<PartidaCampeonato> Gerar(int n, bool terceiro = false)
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(n), idaEVolta: true, rodadaInicial: 1,
                terceiroLugar: terceiro, finalJogoUnico: true);
            for (var i = 0; i < partidas.Count; i++) partidas[i].Id = i + 1;
            return partidas;
        }

        [Fact]
        public void IdaEVolta_FinalETerceiroComUmJogoSo_NaRodadaSeguinteAsSemis()
        {
            var partidas = Gerar(4, terceiro: true);

            Assert.Equal(4, partidas.Count(p => p.Grupo == "Semifinal"));
            var final = Assert.Single(partidas, p => p.Grupo == "Final");
            var terceiro = Assert.Single(partidas, p => p.Grupo == GeradorPartidasCampeonato.NomeTerceiroLugar);
            Assert.Equal(3, final.Rodada);
            Assert.Equal(3, terceiro.Rodada);
        }

        [Fact]
        public void SemIdaEVolta_OpcaoNaoMudaNada()
        {
            var comOpcao = GeradorPartidasCampeonato.MataMata(Ids(8), false, 1, finalJogoUnico: true);
            var semOpcao = GeradorPartidasCampeonato.MataMata(Ids(8), false, 1);

            Assert.Equal(semOpcao.Select(p => (p.Rodada, p.Grupo, p.ParticipanteCasaId, p.ParticipanteVisitanteId)),
                         comOpcao.Select(p => (p.Rodada, p.Grupo, p.ParticipanteCasaId, p.ParticipanteVisitanteId)));
        }

        [Fact]
        public void VencedoresDasSemisDeIdaEVolta_VaoParaAFinalUnica()
        {
            var partidas = Gerar(4, terceiro: true);
            // Semi 1 (1 x 4) fica com o 1; semi 2 (2 x 3) com o 3, nos pênaltis da volta.
            foreach (var p in partidas.Where(p => p.Grupo == "Semifinal"))
            {
                p.PlacarCasa = p.ChaveOrdem == 1 && p.ParticipanteCasaId == 1 ? 2 : (p.ChaveOrdem == 1 ? 0 : 1);
                p.PlacarVisitante = p.ChaveOrdem == 1 && p.ParticipanteVisitanteId == 1 ? 2 : (p.ChaveOrdem == 1 ? 0 : 1);
            }
            var volta2 = partidas.Where(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 2).OrderBy(p => p.Rodada).Last();
            volta2.PenaltisCasa = volta2.ParticipanteCasaId == 3 ? 5 : 3;
            volta2.PenaltisVisitante = volta2.ParticipanteVisitanteId == 3 ? 5 : 3;

            GeradorPartidasCampeonato.AvancarVencedores(partidas);

            var final = partidas.Single(p => p.Grupo == "Final");
            Assert.Equal((1, 3), (final.ParticipanteCasaId!.Value, final.ParticipanteVisitanteId!.Value));
            var terceiro = partidas.Single(p => p.Grupo == GeradorPartidasCampeonato.NomeTerceiroLugar);
            Assert.Equal((4, 2), (terceiro.ParticipanteCasaId!.Value, terceiro.ParticipanteVisitanteId!.Value));
        }

        [Fact]
        public void FinalUnica_DecideOCampeonato_EAparecePainelComoJogoUnico()
        {
            var c = new Campeonato { Id = 1, Nome = "T", Tipo = "MATA_MATA", Status = StatusCampeonato.EmAndamento, CriadoEm = DateTime.UtcNow };
            c.Fases.Add(new CampeonatoFase { Id = 10, Nome = "Mata-mata", Tipo = "MATA_MATA", Ordem = 1, IdaEVolta = true, FinalJogoUnico = true });
            foreach (var i in Ids(4)) c.Participantes.Add(new CampeonatoParticipante { Id = i, Nome = $"P{i}", Ordem = i });
            foreach (var p in Gerar(4)) { p.FaseId = 10; c.Partidas.Add(p); }

            foreach (var p in c.Partidas.Where(p => p.Grupo == "Semifinal")) { p.PlacarCasa = 1; p.PlacarVisitante = 0; }
            // Cada semi: 1 x 0 na ida e na volta → empate no agregado sem pênaltis. Desempata na volta.
            foreach (var volta in c.Partidas.Where(p => p.Grupo == "Semifinal").GroupBy(p => p.ChaveOrdem).Select(g => g.OrderBy(p => p.Rodada).Last()))
            { volta.PenaltisCasa = 3; volta.PenaltisVisitante = 1; }
            GeradorPartidasCampeonato.AvancarVencedores(c.Partidas.ToList());

            var final = c.Partidas.Single(p => p.Grupo == "Final");
            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            final.PlacarCasa = 0; final.PlacarVisitante = 2;
            Assert.Equal(new SituacaoCampeonato(true, final.ParticipanteVisitanteId), SituacaoCampeonatoHelper.Avaliar(c));

            var painel = CampeonatoPainelAdapter.MontarPainel(c);
            var confrontoFinal = Assert.Single(painel.FasesMataMata.Single(f => f.Nome == "Final").Confrontos);
            Assert.True(confrontoFinal.SoIda);
            Assert.All(painel.FasesMataMata.Single(f => f.Nome == "Semifinal").Confrontos, cf => Assert.False(cf.SoIda));
        }
    }
}
