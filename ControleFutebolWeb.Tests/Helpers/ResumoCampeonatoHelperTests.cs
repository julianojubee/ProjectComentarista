using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class ResumoCampeonatoHelperTests
    {
        private static Campeonato Montar(int participantes, params (int Id, string Tipo, string Nome, Func<List<int>, List<PartidaCampeonato>> Gerar)[] fases)
        {
            var c = new Campeonato { Id = 1, Nome = "Teste", Tipo = fases.Length > 0 ? fases[0].Tipo : "PONTOS_CORRIDOS", Status = StatusCampeonato.EmAndamento, CriadoEm = DateTime.UtcNow };
            for (var i = 1; i <= participantes; i++)
                c.Participantes.Add(new CampeonatoParticipante { Id = i, CampeonatoId = 1, Nome = $"P{i}", Ordem = i });

            var proximoId = 100;
            var ordem = 0;
            foreach (var (id, tipo, nome, gerar) in fases)
            {
                c.Fases.Add(new CampeonatoFase { Id = id, CampeonatoId = 1, Nome = nome, Tipo = tipo, Ordem = ++ordem });
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
        public void Rascunho_SemParticipantesSuficientes_PedeParticipantes()
        {
            var c = Montar(1);
            c.Status = StatusCampeonato.Rascunho;

            var r = ResumoCampeonatoHelper.Montar(c);

            Assert.Equal("Adicione os participantes (1 de no mínimo 2)", r.ProximoPasso);
            Assert.Null(r.Etapa);
        }

        [Fact]
        public void Rascunho_ComParticipantes_ProntoParaGerar()
        {
            var c = Montar(4);
            c.Status = StatusCampeonato.Rascunho;

            Assert.Equal("Pronto para gerar as partidas", ResumoCampeonatoHelper.Montar(c).ProximoPasso);
        }

        [Fact]
        public void PontosCorridos_ContaPartidasERodadaELider()
        {
            var c = Montar(4, (10, "PONTOS_CORRIDOS", "Pontos corridos", ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1)));
            // Rodada 1 inteira jogada, com P3 vencendo
            foreach (var p in c.Partidas.Where(p => p.Rodada == 1))
            {
                if (p.ParticipanteCasaId == 3) Placar(p, 2, 0);
                else if (p.ParticipanteVisitanteId == 3) Placar(p, 0, 2);
                else Placar(p, 1, 1);
            }

            var r = ResumoCampeonatoHelper.Montar(c);

            Assert.Equal(2, r.PartidasJogadas);
            Assert.Equal(6, r.PartidasTotal);
            Assert.Equal("Rodada 2", r.Etapa);
            Assert.Equal(3, r.Lider?.Id);
            Assert.Null(r.ProximoPasso);
        }

        [Fact]
        public void PontosCorridos_SemJogo_SemLider()
        {
            var c = Montar(4, (10, "PONTOS_CORRIDOS", "Pontos corridos", ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1)));

            var r = ResumoCampeonatoHelper.Montar(c);

            Assert.Null(r.Lider);
            Assert.Equal("Rodada 1", r.Etapa);
        }

        [Fact]
        public void MataMata_EtapaEhAPrimeiraComPartidaPendente()
        {
            var c = Montar(4, (10, "MATA_MATA", "Mata-mata", ids => GeradorPartidasCampeonato.MataMata(ids, false, 1)));
            Assert.Equal("Semifinal", ResumoCampeonatoHelper.Montar(c).Etapa);

            foreach (var s in c.Partidas.Where(p => p.Grupo == "Semifinal")) Placar(s, 1, 0);
            GeradorPartidasCampeonato.AvancarVencedores(c.Partidas.ToList());

            var r = ResumoCampeonatoHelper.Montar(c);
            Assert.Equal("Final", r.Etapa);
            Assert.Null(r.Lider);
        }

        [Fact]
        public void Grupos_MostraFaseERodada()
        {
            var c = Montar(4,
                (10, "GRUPOS", "Fase de grupos", ids => GeradorPartidasCampeonato.Grupos(GeradorPartidasCampeonato.DistribuirEmGrupos(ids, 2), false, 1)),
                (11, "MATA_MATA", "Mata-mata", _ => new List<PartidaCampeonato>()));

            Assert.Equal("Fase de grupos, rodada 1", ResumoCampeonatoHelper.Montar(c).Etapa);
        }
    }
}
