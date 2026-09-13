using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    // Rankings de jogador de uma competição (artilharia e assistências). Ficam num só
    // lugar porque as telas fixas — Brasileirão, Libertadores, Copa do Brasil,
    // Sul-Americana, Champions, Copa do Mundo — e a tela genérica da competição
    // mostram exatamente os mesmos números, mudando só o id e a temporada.
    public static class RankingCompeticaoHelper
    {
        // Gol contra não conta para o artilheiro (é gol do adversário).
        public static Task<List<RankingJogadorViewModel>> ArtilheirosAsync(
            FutebolContext ctx, int competicaoId, int? temporada, int top = 5) =>
            RankearAsync(
                ctx,
                ctx.Gols.Where(g => !g.Contra
                                 && g.Jogo.CompeticaoId == competicaoId
                                 && (temporada == null || g.Jogo.Temporada == temporada))
                        .Select(g => g.JogadorId),
                top);

        public static Task<List<RankingJogadorViewModel>> AssistentesAsync(
            FutebolContext ctx, int competicaoId, int? temporada, int top = 5) =>
            RankearAsync(
                ctx,
                ctx.Assistencias.Where(a => a.Jogo.CompeticaoId == competicaoId
                                         && (temporada == null || a.Jogo.Temporada == temporada))
                                .Select(a => a.JogadorId),
                top);

        // Conta as ocorrências por jogador e traz o atleta (com o clube) de uma vez só.
        private static async Task<List<RankingJogadorViewModel>> RankearAsync(
            FutebolContext ctx, IQueryable<int> jogadorIds, int top)
        {
            var contagem = await jogadorIds
                .GroupBy(id => id)
                .Select(g => new { JogadorId = g.Key, Valor = g.Count() })
                .OrderByDescending(x => x.Valor)
                .Take(top)
                .ToListAsync();

            if (contagem.Count == 0) return new List<RankingJogadorViewModel>();

            var ids = contagem.Select(c => c.JogadorId).ToList();
            var jogadores = await ctx.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id);

            return contagem
                .Where(c => jogadores.ContainsKey(c.JogadorId))
                .Select(c => new RankingJogadorViewModel { Jogador = jogadores[c.JogadorId], Valor = c.Valor })
                .ToList();
        }
    }
}
