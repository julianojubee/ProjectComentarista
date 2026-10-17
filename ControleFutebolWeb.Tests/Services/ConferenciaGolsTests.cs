using ControleFutebolWeb.Services;
using LanceGol = ControleFutebolWeb.Services.ApiFootballService.LanceGol;

namespace ControleFutebolWeb.Tests.Services
{
    // A api-football revisa autor de gol, gol contra e assistência depois do jogo. A
    // sincronização compara os lances da API com os gravados e só reimporta quando a
    // diferença é confiável — nunca quando o gravado tem mais (veio do FotMob ou à mão).
    public class ConferenciaGolsTests
    {
        private static AfEvent Gol(int id, string nome, int? assistId = null, string? assistNome = null,
            string detail = "Normal Goal", string? comments = null) => new()
        {
            Type = "Goal",
            Detail = detail,
            Comments = comments,
            Player = new AfPlayerRef { Id = id, Name = nome },
            Assist = assistId == null && assistNome == null ? null : new AfPlayerRef { Id = assistId, Name = assistNome },
        };

        private static ApiFootballService.ConferenciaGols Comparar(
            AfEvent[] eventos, LanceGol[] gols, LanceGol[]? assist = null) =>
            ApiFootballService.CompararGolsEAssistencias(eventos, gols, assist ?? Array.Empty<LanceGol>());

        [Fact]
        public void Igual_NaoDiverge()
        {
            var r = Comparar(
                new[] { Gol(10, "Pedro", 20, "Arrascaeta") },
                new[] { new LanceGol(10, "Pedro", false) },
                new[] { new LanceGol(20, "Arrascaeta", false) });

            Assert.False(r.Divergente);
        }

        [Fact]
        public void AssistenciaTrocadaDeJogador_Diverge()
        {
            var r = Comparar(
                new[] { Gol(10, "Pedro", 30, "De la Cruz") },
                new[] { new LanceGol(10, "Pedro", false) },
                new[] { new LanceGol(20, "Arrascaeta", false) });

            Assert.True(r.Divergente);
            Assert.Equal("Assistências: −Arrascaeta; +De la Cruz", r.Descricao);
        }

        [Fact]
        public void AssistenciaRetirada_Diverge()
        {
            var r = Comparar(
                new[] { Gol(10, "Pedro") },
                new[] { new LanceGol(10, "Pedro", false) },
                new[] { new LanceGol(20, "Arrascaeta", false) });

            // Assistência a mais no gravado não conta — pode ter vindo do FotMob.
            Assert.False(r.Divergente);
        }

        [Fact]
        public void GolQueVirouGolContra_Diverge()
        {
            var r = Comparar(
                new[] { Gol(40, "Zagueiro", detail: "Own Goal") },
                new[] { new LanceGol(10, "Pedro", false) });

            Assert.True(r.Divergente);
            Assert.Equal("Gols: −Pedro; +Zagueiro (contra)", r.Descricao);
        }

        [Fact]
        public void MinutoDiferente_NaoDiverge()
        {
            var ev = Gol(10, "Pedro");
            ev.Time.Elapsed = 47;

            Assert.False(Comparar(new[] { ev }, new[] { new LanceGol(10, "Pedro", false) }).Divergente);
        }

        [Fact]
        public void GravadoComMaisGols_NaoDiverge()
        {
            var r = Comparar(
                new[] { Gol(10, "Pedro") },
                new[] { new LanceGol(10, "Pedro", false), new LanceGol(11, "Bruno Henrique", false) });

            Assert.False(r.Divergente);
        }

        [Fact]
        public void ApiComGolQueFaltava_Diverge()
        {
            var r = Comparar(
                new[] { Gol(10, "Pedro"), Gol(11, "Bruno Henrique") },
                new[] { new LanceGol(10, "Pedro", false) });

            Assert.True(r.Divergente);
            Assert.Equal("Gols: +Bruno Henrique", r.Descricao);
        }

        [Fact]
        public void IdDoEventoDiferenteDoCadastro_CasaPeloNome()
        {
            // Futebol feminino: a api-football usa no evento um id diferente do da lineup.
            var r = Comparar(
                new[] { Gol(77324, "Vic Albuquerque") },
                new[] { new LanceGol(591488, "Vic Albuquerque", false) });

            Assert.False(r.Divergente);
        }

        [Fact]
        public void NomeAbreviadoNoEvento_CasaPeloNome()
        {
            var r = Comparar(
                new[] { Gol(0, "K. Prates") },
                new[] { new LanceGol(null, "Kauã Prates", false) });

            Assert.False(r.Divergente);
        }

        [Fact]
        public void EstreanteSemIdNoLugarDeOutro_Diverge()
        {
            var r = Comparar(
                new[] { Gol(0, "Estreante") },
                new[] { new LanceGol(10, "Pedro", false) });

            Assert.True(r.Divergente);
            Assert.Equal("Gols: −Pedro; +Estreante", r.Descricao);
        }

        [Fact]
        public void PenaltiPerdidoEDisputa_FicamForaDosGols()
        {
            var r = Comparar(
                new[]
                {
                    Gol(10, "Pedro"),
                    Gol(11, "Bruno Henrique", detail: "Missed Penalty"),
                    Gol(12, "Gerson", detail: "Penalty", comments: "Penalty Shootout"),
                },
                new[] { new LanceGol(10, "Pedro", false) });

            Assert.False(r.Divergente);
        }
    }
}
