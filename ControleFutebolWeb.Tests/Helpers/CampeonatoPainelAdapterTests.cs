using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class CampeonatoPainelAdapterTests
    {
        private static Campeonato Montar(string tipoFase, int participantes, Func<List<int>, List<PartidaCampeonato>> gerar)
        {
            var campeonato = new Campeonato { Id = 1, Nome = "Copa da Firma", Tipo = tipoFase, CriadoEm = new DateTime(2026, 9, 1) };
            campeonato.Fases.Add(new CampeonatoFase { Id = 10, CampeonatoId = 1, Nome = "Fase única", Tipo = tipoFase, Ordem = 1 });

            for (var i = 1; i <= participantes; i++)
                campeonato.Participantes.Add(new CampeonatoParticipante
                {
                    Id = i, CampeonatoId = 1, Nome = $"Amigo {i}", NomeTimeSnapshot = $"Time {i}", Ordem = i
                });

            var partidas = gerar(campeonato.Participantes.Select(p => p.Id).ToList());
            for (var i = 0; i < partidas.Count; i++)
            {
                partidas[i].Id = 100 + i;
                partidas[i].CampeonatoId = 1;
                partidas[i].FaseId = 10;
                campeonato.Partidas.Add(partidas[i]);
            }
            return campeonato;
        }

        [Fact]
        public void PontosCorridos_SemJogo_MostraTodosZerados()
        {
            var campeonato = Montar("PONTOS_CORRIDOS", 4,
                ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1));

            var painel = CampeonatoPainelAdapter.MontarPainel(campeonato);

            Assert.Empty(painel.Fases);
            Assert.Equal(4, painel.Classificacao.Count);
            Assert.All(painel.Classificacao, c => Assert.Equal(0, c.Pontos));
            Assert.Contains(painel.Classificacao, c => c.Time.Nome == "Amigo 1 (Time 1)");
        }

        [Fact]
        public void PontosCorridos_ContaPontosDoParticipante()
        {
            var campeonato = Montar("PONTOS_CORRIDOS", 3,
                ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1));

            var jogo = campeonato.Partidas.First(p => p.ParticipanteCasaId == 2 || p.ParticipanteVisitanteId == 2);
            var ladoCasa = jogo.ParticipanteCasaId == 2;
            jogo.PlacarCasa = ladoCasa ? 3 : 0;
            jogo.PlacarVisitante = ladoCasa ? 0 : 3;

            var painel = CampeonatoPainelAdapter.MontarPainel(campeonato);

            var lider = painel.Classificacao[0];
            Assert.Equal(2, lider.TimeId);
            Assert.Equal(3, lider.Pontos);
            Assert.Equal(3, painel.Classificacao.Count);
        }

        [Fact]
        public void Cartoes_ContamParaOParticipanteDoEvento()
        {
            var campeonato = Montar("PONTOS_CORRIDOS", 2,
                ids => GeradorPartidasCampeonato.TodosContraTodos(ids, false, 1));
            var partida = campeonato.Partidas.Single();
            partida.PlacarCasa = 1; partida.PlacarVisitante = 1;
            partida.Eventos.Add(new EventoPartidaCampeonato
            {
                Id = 7, PartidaId = partida.Id, ParticipanteId = 1, Tipo = TipoEventoPartida.CartaoVermelho, NomeSnapshot = "Zé"
            });

            var conv = CampeonatoPainelAdapter.Converter(campeonato);
            var dados = ControleFutebolWeb.Helpers.DadosDesempate.Construir(conv.Jogos, conv.Cartoes);

            Assert.Equal(1, dados.Vermelhos[1]);
            Assert.False(dados.Vermelhos.ContainsKey(2));
        }

        [Fact]
        public void MataMata_VagasEmAbertoFormamConfrontosSeparados()
        {
            var campeonato = Montar("MATA_MATA", 4,
                ids => GeradorPartidasCampeonato.MataMata(ids, true, 1));

            var painel = CampeonatoPainelAdapter.MontarPainel(campeonato);

            var semi = Assert.Single(painel.FasesMataMata, f => f.Nome == "Semifinal");
            Assert.Equal(2, semi.Confrontos.Count);
            Assert.All(semi.Confrontos, c => Assert.NotNull(c.JogoVolta));

            // A final ainda sem ninguém: ida e volta caem no MESMO confronto.
            var final = Assert.Single(painel.FasesMataMata, f => f.Nome == "Final");
            var confronto = Assert.Single(final.Confrontos);
            Assert.NotNull(confronto.JogoVolta);
            Assert.Equal(CampeonatoPainelAdapter.NomeVagaEmAberto, confronto.TimeA!.Nome);
        }

        [Fact]
        public void DuasFases_UmaAbaPorFase()
        {
            var campeonato = Montar("GRUPOS", 4, ids => GeradorPartidasCampeonato.Grupos(
                GeradorPartidasCampeonato.DistribuirEmGrupos(ids, 2), false, 1));
            campeonato.Fases.Add(new CampeonatoFase { Id = 11, CampeonatoId = 1, Nome = "Final", Tipo = "MATA_MATA", Ordem = 2 });

            var painel = CampeonatoPainelAdapter.MontarPainel(campeonato);

            Assert.Equal(2, painel.Fases.Count);
            Assert.Equal(2, painel.Fases[0].Grupos.Count);
            Assert.Empty(painel.Fases[1].FasesMataMata);
        }
    }
}
