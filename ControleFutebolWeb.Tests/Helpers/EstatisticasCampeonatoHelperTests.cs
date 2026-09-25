using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Tests.Helpers
{
    public class EstatisticasCampeonatoHelperTests
    {
        private static EventoPartidaCampeonato E(TipoEventoPartida tipo, int participante, string nome,
            int? jogadorId = null, int? proprioId = null) => new()
        {
            Tipo = tipo, ParticipanteId = participante, NomeSnapshot = nome,
            JogadorId = jogadorId, JogadorProprioId = proprioId
        };

        [Fact]
        public void SomaPorCadastro_E_IgnoraGolContra()
        {
            var linhas = EstatisticasCampeonatoHelper.PorAtleta(new[]
            {
                E(TipoEventoPartida.Gol, 1, "Neymar", jogadorId: 10),
                E(TipoEventoPartida.Gol, 1, "Neymar Jr", jogadorId: 10),
                E(TipoEventoPartida.Assistencia, 1, "Neymar", jogadorId: 10),
                E(TipoEventoPartida.Gol, 2, "Zé", proprioId: 5),
                E(TipoEventoPartida.GolContra, 2, "Zé", proprioId: 5),
            });

            Assert.Equal(2, linhas.Count);
            Assert.Equal("Neymar", linhas[0].Nome);
            Assert.Equal(2, linhas[0].Gols);
            Assert.Equal(1, linhas[0].Assistencias);
            Assert.Equal(1, linhas[1].Gols);
        }

        [Fact]
        public void NomeDigitado_MesmoNomeEmTimesDiferentesSaoPessoasDiferentes()
        {
            var linhas = EstatisticasCampeonatoHelper.PorAtleta(new[]
            {
                E(TipoEventoPartida.Gol, 1, "Zé"),
                E(TipoEventoPartida.Gol, 1, "zé "),
                E(TipoEventoPartida.Gol, 2, "Zé"),
                E(TipoEventoPartida.CartaoAmarelo, 2, "Zé"),
            });

            Assert.Equal(2, linhas.Count);
            Assert.Equal(2, linhas.Single(l => l.ParticipanteId == 1).Gols);
            Assert.Equal(1, linhas.Single(l => l.ParticipanteId == 2).Amarelos);
        }
    }
}
