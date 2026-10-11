using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    // Desarmes de cada time em cada jogo. As estatísticas de time da partida
    // (Jogo.EstatisticasJson) não trazem esse número, então ele sai da soma das
    // estatísticas por jogador (EstatisticaJogador.Desarmes).
    //
    // Usado pela aba Estatísticas da Análise (Jogos/Analisar) e pelo painel de
    // estatísticas das competições (EstatisticaTimeCalculator).
    public static class DesarmesPorTimeHelper
    {
        /// <summary>
        /// (JogoId, TimeId) → desarmes do time naquele jogo. O lado do jogador vem da
        /// escalação da partida (o cadastro é só fallback — ver LadoJogadorHelper);
        /// jogador sem lado conhecido fica fora da soma.
        ///
        /// Sem entrada = sem dado: o time não tem estatística por jogador no jogo, ou
        /// alguma linha dele veio de uma fonte que não publica desarme (ESPN, FIFA).
        /// Ali o 0 gravado não é zero, e somá-lo puxaria a média do time para baixo.
        /// </summary>
        public static Dictionary<(int JogoId, int TimeId), int> Carregar(
            FutebolContext context, IReadOnlyCollection<Jogo> jogos)
        {
            if (jogos.Count == 0) return new();

            var jogoIds = jogos.Select(j => j.Id).ToList();
            var estats = context.EstatisticasJogador.AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId))
                .Select(e => new { e.JogoId, e.JogadorId, e.Fonte, e.Desarmes, e.Jogador.TimeId, e.Jogador.SelecaoId })
                .ToList();
            if (estats.Count == 0) return new();

            var lados = LadoJogadorHelper.Montar(LadoJogadorHelper.EscalacoesDosEventos(
                context, jogoIds, estats.Select(e => e.JogadorId)).ToList());

            var jogoPorId = jogos.ToDictionary(j => j.Id);
            var resultado = new Dictionary<(int JogoId, int TimeId), int>();
            var semCobertura = new HashSet<(int JogoId, int TimeId)>();

            foreach (var e in estats)
            {
                var jogador = new Jogador { Id = e.JogadorId, TimeId = e.TimeId, SelecaoId = e.SelecaoId };
                var timeId = LadoJogadorHelper.TimeDoAutor(jogoPorId[e.JogoId], jogador, lados);
                if (timeId == null) continue;

                var chave = (e.JogoId, timeId.Value);
                if (!FonteEstatistica.Cobre(e.Fonte, "desarme")) { semCobertura.Add(chave); continue; }
                resultado[chave] = resultado.GetValueOrDefault(chave) + e.Desarmes;
            }

            foreach (var chave in semCobertura) resultado.Remove(chave);
            return resultado;
        }
    }
}
