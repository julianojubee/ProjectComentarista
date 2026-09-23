using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class SituacaoCampeonatoHelperTests
    {
        private static Campeonato Montar(params (int Id, string Tipo, Func<List<int>, List<PartidaCampeonato>> Gerar)[] fases)
        {
            var c = new Campeonato { Id = 1, Nome = "Teste", Tipo = fases[0].Tipo, Status = StatusCampeonato.EmAndamento, CriadoEm = DateTime.UtcNow };
            for (var i = 1; i <= 4; i++)
                c.Participantes.Add(new CampeonatoParticipante { Id = i, CampeonatoId = 1, Nome = $"P{i}", Ordem = i });

            var proximoId = 100;
            var ordem = 0;
            foreach (var (id, tipo, gerar) in fases)
            {
                c.Fases.Add(new CampeonatoFase { Id = id, CampeonatoId = 1, Nome = tipo, Tipo = tipo, Ordem = ++ordem });
                foreach (var p in gerar(c.Participantes.Select(x => x.Id).ToList()))
                {
                    p.Id = proximoId++;
                    p.FaseId = id;
                    p.CampeonatoId = 1;
                    c.Partidas.Add(p);
                }
            }
            return c;
        }

        private static void Placar(PartidaCampeonato p, int casa, int vis) { p.PlacarCasa = casa; p.PlacarVisitante = vis; }

        [Fact]
        public void PontosCorridos_SoEncerraComTodosOsPlacares_ECampeaoEhOLider()
        {
            var c = Montar((10, "PONTOS_CORRIDOS", ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1)));
            // P3 vence todos os seus jogos; o resto empata.
            foreach (var p in c.Partidas)
            {
                if (p.ParticipanteCasaId == 3) Placar(p, 2, 0);
                else if (p.ParticipanteVisitanteId == 3) Placar(p, 0, 2);
                else Placar(p, 1, 1);
            }
            var ultima = c.Partidas.Last();
            var (casa, vis) = (ultima.PlacarCasa, ultima.PlacarVisitante);
            ultima.PlacarCasa = null; ultima.PlacarVisitante = null;

            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            ultima.PlacarCasa = casa; ultima.PlacarVisitante = vis;
            Assert.Equal(new SituacaoCampeonato(true, 3), SituacaoCampeonatoHelper.Avaliar(c));
        }

        [Fact]
        public void MataMata_FinalEmpatadaSemPenaltis_NaoEncerra()
        {
            var c = Montar((10, "MATA_MATA", ids => GeradorPartidasCampeonato.MataMata(ids, false, 1)));
            var semis = c.Partidas.Where(p => p.Grupo == "Semifinal").ToList();
            Placar(semis[0], 1, 0);
            Placar(semis[1], 0, 1);
            GeradorPartidasCampeonato.AvancarVencedores(c.Partidas.ToList());
            var final = c.Partidas.Single(p => p.Grupo == "Final");

            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            Placar(final, 2, 2);
            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            final.PenaltisCasa = 5; final.PenaltisVisitante = 4;
            Assert.Equal(new SituacaoCampeonato(true, final.ParticipanteCasaId), SituacaoCampeonatoHelper.Avaliar(c));
        }

        [Fact]
        public void GruposComMataMataAindaNaoGerado_NaoEncerra()
        {
            var c = Montar(
                (10, "GRUPOS", ids => GeradorPartidasCampeonato.Grupos(GeradorPartidasCampeonato.DistribuirEmGrupos(ids, 2), false, 1)),
                (11, "MATA_MATA", _ => new List<PartidaCampeonato>()));
            foreach (var p in c.Partidas) Placar(p, 1, 0);

            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);
        }

        [Fact]
        public void AplicarStatus_EncerraEReabre_ENaoMexeEmRascunho()
        {
            var c = Montar((10, "MATA_MATA", ids => GeradorPartidasCampeonato.MataMata(ids.Take(2).ToList(), false, 1)));
            var final = c.Partidas.Single();
            var agora = new DateTime(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

            Placar(final, 3, 1);
            Assert.True(SituacaoCampeonatoHelper.AplicarStatus(c, agora));
            Assert.Equal(StatusCampeonato.Encerrado, c.Status);
            Assert.Equal(agora, c.EncerradoEm);
            Assert.False(SituacaoCampeonatoHelper.AplicarStatus(c, agora.AddHours(1)));
            Assert.Equal(agora, c.EncerradoEm);

            final.PlacarCasa = null; final.PlacarVisitante = null;
            Assert.True(SituacaoCampeonatoHelper.AplicarStatus(c, agora));
            Assert.Equal(StatusCampeonato.EmAndamento, c.Status);
            Assert.Null(c.EncerradoEm);

            c.Status = StatusCampeonato.Rascunho;
            Placar(final, 3, 1);
            Assert.False(SituacaoCampeonatoHelper.AplicarStatus(c, agora));
            Assert.Equal(StatusCampeonato.Rascunho, c.Status);
        }

        [Fact]
        public void MataMata_ComTerceiroLugar_SoEncerraComOsDoisJogosDecididos()
        {
            var c = Montar((10, "MATA_MATA", ids => GeradorPartidasCampeonato.MataMata(ids, false, 1, terceiroLugar: true)));
            foreach (var semi in c.Partidas.Where(p => p.Grupo == "Semifinal")) Placar(semi, 1, 0);
            GeradorPartidasCampeonato.AvancarVencedores(c.Partidas.ToList());

            var final = c.Partidas.Single(p => p.Grupo == "Final");
            var terceiro = c.Partidas.Single(p => p.Grupo == GeradorPartidasCampeonato.NomeTerceiroLugar);

            Placar(final, 0, 2);
            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            Placar(terceiro, 3, 3);
            Assert.False(SituacaoCampeonatoHelper.Avaliar(c).Decidido);

            terceiro.PenaltisCasa = 4; terceiro.PenaltisVisitante = 2;
            Assert.Equal(new SituacaoCampeonato(true, final.ParticipanteVisitanteId), SituacaoCampeonatoHelper.Avaliar(c));
        }
    }
}
