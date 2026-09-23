using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class GeradorPartidasCampeonatoTests
    {
        private static List<int> Ids(int n) => Enumerable.Range(1, n).ToList();

        private static (int, int) Par(PartidaCampeonato p) =>
            (Math.Min(p.ParticipanteCasaId!.Value, p.ParticipanteVisitanteId!.Value),
             Math.Max(p.ParticipanteCasaId!.Value, p.ParticipanteVisitanteId!.Value));

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(8)]
        [InlineData(11)]
        public void TodosContraTodos_CadaParUmaVez_NinguemJogaDuasVezesNaRodada(int n)
        {
            var partidas = GeradorPartidasCampeonato.TodosContraTodos(Ids(n), idaEVolta: false, rodadaInicial: 1);

            Assert.Equal(n * (n - 1) / 2, partidas.Count);
            Assert.Equal(partidas.Count, partidas.Select(Par).Distinct().Count());

            var rodadas = n % 2 == 0 ? n - 1 : n;
            Assert.Equal(Enumerable.Range(1, rodadas), partidas.Select(p => p.Rodada).Distinct().OrderBy(r => r));

            foreach (var rodada in partidas.GroupBy(p => p.Rodada))
            {
                var ids = rodada.SelectMany(p => new[] { p.ParticipanteCasaId, p.ParticipanteVisitanteId }).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(10)]
        public void TodosContraTodos_MandoEquilibrado(int n)
        {
            var partidas = GeradorPartidasCampeonato.TodosContraTodos(Ids(n), idaEVolta: false, rodadaInicial: 1);

            foreach (var id in Ids(n))
            {
                var jogos = partidas.Count(p => p.ParticipanteCasaId == id || p.ParticipanteVisitanteId == id);
                var emCasa = partidas.Count(p => p.ParticipanteCasaId == id);
                Assert.InRange(emCasa, jogos / 2, (jogos + 1) / 2);
            }
        }

        [Fact]
        public void TodosContraTodos_IdaEVolta_ReturnoInverteMandoNasRodadasSeguintes()
        {
            var partidas = GeradorPartidasCampeonato.TodosContraTodos(Ids(4), idaEVolta: true, rodadaInicial: 3);

            Assert.Equal(12, partidas.Count);
            Assert.Equal(3, partidas.Min(p => p.Rodada));
            Assert.Equal(8, partidas.Max(p => p.Rodada));

            // Todo mando aparece exatamente uma vez: A x B no turno, B x A no returno.
            var mandos = partidas.Select(p => (p.ParticipanteCasaId, p.ParticipanteVisitanteId)).ToList();
            Assert.Equal(12, mandos.Distinct().Count());
        }

        [Fact]
        public void Grupos_RotulaEAlinhaRodadas()
        {
            var grupos = GeradorPartidasCampeonato.DistribuirEmGrupos(Ids(8), 2);
            var partidas = GeradorPartidasCampeonato.Grupos(grupos, idaEVolta: false, rodadaInicial: 1);

            Assert.Equal(new[] { "Grupo A", "Grupo B" }, partidas.Select(p => p.Grupo).Distinct().OrderBy(g => g));
            Assert.Equal(12, partidas.Count);
            Assert.All(partidas, p => Assert.InRange(p.Rodada, 1, 3));
        }

        [Fact]
        public void DistribuirEmGrupos_CabecasDeChaveEmGruposDiferentes()
        {
            var grupos = GeradorPartidasCampeonato.DistribuirEmGrupos(Ids(8), 4);

            Assert.Equal(new[] { 1, 5 }, grupos["A"]);
            Assert.Equal(new[] { 2, 6 }, grupos["B"]);
            Assert.Equal(new[] { 4, 8 }, grupos["D"]);
        }

        [Fact]
        public void OrdemDaChave_FavoritosSoSeCruzamNoFim()
        {
            Assert.Equal(new[] { 1, 8, 4, 5, 2, 7, 3, 6 }, GeradorPartidasCampeonato.OrdemDaChave(8));
        }

        [Fact]
        public void MataMata_Oito_MontaChaveInteiraComVagasEmAberto()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(8), idaEVolta: false, rodadaInicial: 1);

            Assert.Equal(7, partidas.Count);
            Assert.Equal(4, partidas.Count(p => p.Grupo == "Quartas"));
            Assert.Equal(2, partidas.Count(p => p.Grupo == "Semifinal"));
            Assert.Single(partidas, p => p.Grupo == "Final");

            var quartas = partidas.Where(p => p.Grupo == "Quartas").OrderBy(p => p.ChaveOrdem).ToList();
            Assert.Equal((1, 8), (quartas[0].ParticipanteCasaId!.Value, quartas[0].ParticipanteVisitanteId!.Value));
            Assert.All(partidas.Where(p => p.Grupo != "Quartas"),
                p => Assert.True(p.ParticipanteCasaId == null && p.ParticipanteVisitanteId == null));
        }

        [Fact]
        public void MataMata_Cinco_FavoritosPassamDiretoParaSemifinal()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(5), idaEVolta: false, rodadaInicial: 1);

            // 8 vagas, 3 byes: só 4 x 5 joga a primeira etapa.
            var quartas = Assert.Single(partidas, p => p.Grupo == "Quartas");
            Assert.Equal((4, 5), Par(quartas));

            var semis = partidas.Where(p => p.Grupo == "Semifinal").OrderBy(p => p.ChaveOrdem).ToList();
            Assert.Equal(1, semis[0].ParticipanteCasaId);
            Assert.Null(semis[0].ParticipanteVisitanteId);       // vencedor de 4 x 5
            Assert.Equal((2, 3), Par(semis[1]));
        }

        [Fact]
        public void MataMata_IdaEVolta_VoltaInverteMando()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(4), idaEVolta: true, rodadaInicial: 1);

            Assert.Equal(6, partidas.Count);
            var semi1 = partidas.Where(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 1).OrderBy(p => p.Rodada).ToList();
            Assert.Equal(new[] { 1, 2 }, semi1.Select(p => p.Rodada));
            Assert.Equal(semi1[0].ParticipanteCasaId, semi1[1].ParticipanteVisitanteId);
            Assert.All(partidas.Where(p => p.Grupo == "Final"), p => Assert.InRange(p.Rodada, 3, 4));
        }

        [Fact]
        public void Vencedor_AgregadoEPenaltisDoUltimoJogo()
        {
            var ida = new PartidaCampeonato { Id = 1, Rodada = 1, ParticipanteCasaId = 1, ParticipanteVisitanteId = 2, PlacarCasa = 2, PlacarVisitante = 1 };
            var volta = new PartidaCampeonato { Id = 2, Rodada = 2, ParticipanteCasaId = 2, ParticipanteVisitanteId = 1, PlacarCasa = 1, PlacarVisitante = 0 };

            // 2 x 2 no agregado, sem pênaltis: ainda não decidido.
            Assert.Null(GeradorPartidasCampeonato.Vencedor(new[] { ida, volta }));

            volta.PenaltisCasa = 4; volta.PenaltisVisitante = 5;
            Assert.Equal(1, GeradorPartidasCampeonato.Vencedor(new[] { ida, volta }));

            volta.PlacarCasa = 3;
            Assert.Equal(2, GeradorPartidasCampeonato.Vencedor(new[] { ida, volta }));
        }

        [Fact]
        public void AvancarVencedores_PreencheProximaEtapaEDesfazQuandoPlacarSome()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(4), idaEVolta: false, rodadaInicial: 1);
            for (var i = 0; i < partidas.Count; i++) partidas[i].Id = i + 1;

            var semi1 = partidas.Single(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 1); // 1 x 4
            var semi2 = partidas.Single(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 2); // 2 x 3
            var final = partidas.Single(p => p.Grupo == "Final");

            semi1.PlacarCasa = 0; semi1.PlacarVisitante = 1;
            semi2.PlacarCasa = 3; semi2.PlacarVisitante = 0;

            Assert.Equal(2, GeradorPartidasCampeonato.AvancarVencedores(partidas));
            Assert.Equal(4, final.ParticipanteCasaId);
            Assert.Equal(2, final.ParticipanteVisitanteId);

            semi1.PlacarCasa = null; semi1.PlacarVisitante = null;
            GeradorPartidasCampeonato.AvancarVencedores(partidas);
            Assert.Null(final.ParticipanteCasaId);
            Assert.Equal(2, final.ParticipanteVisitanteId);
        }

        [Fact]
        public void AvancarVencedores_NaoMexeEmConfrontoQueJaTemPlacar()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(4), idaEVolta: false, rodadaInicial: 1);
            var semi1 = partidas.Single(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 1);
            var final = partidas.Single(p => p.Grupo == "Final");

            final.ParticipanteCasaId = 1; final.ParticipanteVisitanteId = 2;
            final.PlacarCasa = 1; final.PlacarVisitante = 0;
            semi1.PlacarCasa = 0; semi1.PlacarVisitante = 1; // 4 venceria

            Assert.Equal(0, GeradorPartidasCampeonato.AvancarVencedores(partidas));
            Assert.Equal(1, final.ParticipanteCasaId);
        }

        [Fact]
        public void AvancarVencedores_ComBye_NaoApagaQuemPassouDireto()
        {
            var partidas = GeradorPartidasCampeonato.MataMata(Ids(5), idaEVolta: false, rodadaInicial: 1);
            var quartas = partidas.Single(p => p.Grupo == "Quartas"); // 4 x 5
            var semi1 = partidas.Single(p => p.Grupo == "Semifinal" && p.ChaveOrdem == 1);

            quartas.PlacarCasa = 2; quartas.PlacarVisitante = 0;
            GeradorPartidasCampeonato.AvancarVencedores(partidas);

            Assert.Equal(1, semi1.ParticipanteCasaId);
            Assert.Equal(4, semi1.ParticipanteVisitanteId);
        }
    }
}
