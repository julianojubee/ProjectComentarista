using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    [Authorize(Policy = "Admin")]
    public class AdminController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(FutebolContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: /Admin/NormalizarNacionalidades
        // Mescla nacionalidades duplicadas (ex.: "Brazil" + "Brasil") traduzindo tudo
        // para o nome canônico em português via CountryHelper. Não destrutiva:
        // nacionalidades fora do mapa são mantidas como estão (apenas deduplicadas
        // por igualdade case-insensitive); nenhum jogador/treinador perde o vínculo.
        [HttpPost]
        public async Task<IActionResult> NormalizarNacionalidades()
        {
            var todas = await _context.Nacionalidades.ToListAsync();

            var grupos = todas.GroupBy(
                n => CountryHelper.Traduzir(n.Nome.Trim()),
                StringComparer.OrdinalIgnoreCase);

            int renomeadas = 0, mescladas = 0, jogadoresMovidos = 0, treinadoresMovidos = 0;

            foreach (var grupo in grupos)
            {
                var lista = grupo.OrderBy(n => n.Id).ToList();
                var canonical = lista[0];
                var nomeCanonico = CountryHelper.Traduzir(canonical.Nome.Trim());

                if (canonical.Nome != nomeCanonico)
                {
                    canonical.Nome = nomeCanonico;
                    renomeadas++;
                }

                foreach (var duplicada in lista.Skip(1))
                {
                    jogadoresMovidos += await _context.Jogadores
                        .Where(j => j.NacionalidadeId == duplicada.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.NacionalidadeId, canonical.Id));

                    treinadoresMovidos += await _context.Treinadores
                        .Where(t => t.NacionalidadeId == duplicada.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.NacionalidadeId, canonical.Id));

                    _context.Nacionalidades.Remove(duplicada);
                    mescladas++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Admin] NormalizarNacionalidades: {R} renomeadas, {M} mescladas, {J} jogadores e {T} treinadores atualizados.",
                renomeadas, mescladas, jogadoresMovidos, treinadoresMovidos);

            TempData["Sucesso"] =
                $"Nacionalidades normalizadas: {renomeadas} renomeada(s), {mescladas} duplicata(s) mesclada(s), " +
                $"{jogadoresMovidos} jogador(es) e {treinadoresMovidos} treinador(es) atualizados.";
            return RedirectToAction("Index", "Servicos");
        }

        // POST: /Admin/RecalcularPosicoesJogadores
        // Deriva a posição real de cada jogador a partir das escalações: casa as
        // coordenadas do slot (Escalacao.PosicaoX/Y) com o slot mais próximo da
        // formação usada no jogo (PosicaoFormacao) e usa o NomePosicao granular
        // ("Lateral Direito", "Ponta Esquerda"...). Jogador que atuou em mais de
        // uma posição fica com as duas mais frequentes: "Lateral Direito/Zagueiro".
        // Jogadores sem escalação registrada mantêm a posição genérica da API.
        [HttpPost]
        public async Task<IActionResult> RecalcularPosicoesJogadores()
        {
            var slotsPorFormacao = (await _context.PosicoesFormacao.ToListAsync())
                .GroupBy(p => p.FormacaoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var escalacoes = await _context.Escalacoes
                .Where(e => e.Titular && e.JogadorId != null)
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
                .ToListAsync();

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
            var jogadores = await _context.Jogadores.Where(j => ids.Contains(j.Id)).ToListAsync();

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

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Admin] RecalcularPosicoesJogadores: {A} jogadores atualizados (de {T} com escalação).",
                atualizados, jogadores.Count);

            TempData["Sucesso"] =
                $"Posições recalculadas: {atualizados} jogador(es) atualizados a partir de {jogadores.Count} com escalações registradas.";
            return RedirectToAction("Index", "Servicos");
        }

        // Unifica o gênero do sufixo de lado ("Meia Direito" ≡ "Meia Direita",
        // "Volante Direita" ≡ "Volante Direito") para não duplicar a mesma posição.
        private static string NormalizarNomePosicao(string nome)
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
