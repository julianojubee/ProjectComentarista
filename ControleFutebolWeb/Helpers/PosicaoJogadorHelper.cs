using ControleFutebolWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    // Deriva a posição "real" do jogador a partir das escalações tituladas:
    // casa as coordenadas do slot (Escalacao.PosicaoX/Y) com o slot mais próximo
    // da formação usada no jogo (PosicaoFormacao) e usa o NomePosicao granular
    // ("Lateral Direito", "Ponta Esquerda"...). Quem atuou em mais de uma posição
    // fica com as duas mais frequentes ("Lateral Direito/Zagueiro").
    // Usado pelo botão de manutenção (todos os jogadores) e automaticamente ao
    // salvar a escalação de um jogo (só os jogadores envolvidos).
    public static class PosicaoJogadorHelper
    {
        /// <param name="jogadorIds">null = todos os jogadores com escalação.</param>
        /// <returns>(jogadores atualizados, jogadores com escalação avaliados)</returns>
        public static async Task<(int Atualizados, int ComEscalacao)> RecalcularAsync(
            FutebolContext context, IReadOnlyCollection<int>? jogadorIds = null, CancellationToken ct = default)
        {
            if (jogadorIds is { Count: 0 }) return (0, 0);

            var slotsPorFormacao = (await context.PosicoesFormacao.ToListAsync(ct))
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var query = context.Escalacoes.Where(e => e.Titular && e.JogadorId != null);
            if (jogadorIds != null)
                query = query.Where(e => jogadorIds.Contains(e.JogadorId!.Value));

            var escalacoes = await query
                .Select(e => new
                {
                    e.JogadorId,
                    e.JogoId,
                    e.FaseEscalacao,
                    e.PosicaoX,
                    e.PosicaoY,
                    e.IsTimeCasa,
                    FormacaoCasaId = e.Jogo.FormacaoCasaId,
                    FormacaoVisitanteId = e.Jogo.FormacaoVisitanteId
                })
                .ToListAsync(ct);

            // Por (jogador, jogo): vale a posição da fase INICIAL; se o jogador só
            // aparece na FINAL (entrou no decorrer), vale a FINAL.
            var porJogo = escalacoes
                .GroupBy(e => new { e.JogadorId, e.JogoId })
                .Select(g => g
                    .OrderBy(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null ? 0 : 1)
                    .First());

            var nomesPorJogador = new Dictionary<int, List<string>>();
            foreach (var e in porJogo)
            {
                var formacaoId = e.IsTimeCasa ? e.FormacaoCasaId : e.FormacaoVisitanteId;
                if (formacaoId == null ||
                    !slotsPorFormacao.TryGetValue(formacaoId.Value, out var slots) ||
                    slots.Count == 0)
                    continue;

                var slot = slots.MinBy(s =>
                    Math.Pow(s.PosicaoX - e.PosicaoX, 2) + Math.Pow(s.PosicaoY - e.PosicaoY, 2))!;

                var nome = NormalizarNomePosicao(slot.NomePosicao);
                if (string.IsNullOrWhiteSpace(nome)) continue;

                if (!nomesPorJogador.TryGetValue(e.JogadorId!.Value, out var lista))
                    nomesPorJogador[e.JogadorId.Value] = lista = new List<string>();
                lista.Add(nome);
            }

            var ids = nomesPorJogador.Keys.ToList();
            var jogadores = await context.Jogadores.Where(j => ids.Contains(j.Id)).ToListAsync(ct);

            int atualizados = 0;
            foreach (var j in jogadores)
            {
                var novo = string.Join("/", nomesPorJogador[j.Id]
                    .GroupBy(n => n)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .Take(2));

                if (!string.IsNullOrWhiteSpace(novo) && j.Posicao != novo)
                {
                    j.Posicao = novo;
                    atualizados++;
                }
            }

            await context.SaveChangesAsync(ct);
            return (atualizados, jogadores.Count);
        }

        // Unifica o gênero do sufixo de lado ("Meia Direito" ≡ "Meia Direita",
        // "Volante Direita" ≡ "Volante Direito") para não duplicar a mesma posição.
        public static string NormalizarNomePosicao(string nome)
        {
            var n = (nome ?? "").Trim();
            var partes = n.Split(' ', 2);
            if (partes.Length != 2) return n;

            bool feminino = partes[0] is "Meia" or "Ponta";
            var lado = partes[1].ToLowerInvariant() switch
            {
                "direito" or "direita" => feminino ? "Direita" : "Direito",
                "esquerdo" or "esquerda" => feminino ? "Esquerda" : "Esquerdo",
                "central" => "Central",
                "ofensivo" => "Ofensivo",
                _ => partes[1]
            };
            return $"{partes[0]} {lado}";
        }
    }
}
