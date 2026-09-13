using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
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

        // GET: /Admin/Acessos?dias=30
        // Movimento das páginas públicas de /creators: quantas aberturas e quantos
        // visitantes por ferramenta, dia a dia. É o contador agregado do
        // AcessoPublicoService — não há registro de visita individual para mostrar.
        [HttpGet]
        public async Task<IActionResult> Acessos(int dias = 30)
        {
            // Teto de dois anos: a URL é do admin, mas ?dias=999999 varreria a
            // tabela inteira sem necessidade.
            dias = Math.Clamp(dias, 7, 730);

            var hoje = AcessoPublicoService.HojeNoBrasil();
            var desde = hoje.AddDays(-(dias - 1));

            var linhas = await _context.AcessosPublicos.AsNoTracking()
                .Where(a => a.Dia >= desde)
                .OrderByDescending(a => a.Dia)
                .ToListAsync();

            var vm = new AcessosPublicosViewModel
            {
                Dias = dias,
                De = desde,
                Ate = hoje,
                PorFerramenta = linhas
                    .GroupBy(a => a.Ferramenta)
                    .Select(g => new AcessoFerramentaViewModel
                    {
                        Ferramenta = g.Key,
                        Visitas = g.Sum(a => a.Visitas),
                        Visitantes = g.Sum(a => a.Visitantes),
                    })
                    .OrderByDescending(f => f.Visitas)
                    .ToList(),
            };

            // Série diária completa: dia sem acesso entra zerado, senão o gráfico
            // "pula" os dias parados e dá impressão de movimento contínuo.
            var porDia = linhas.GroupBy(a => a.Dia)
                .ToDictionary(g => g.Key, g => (Visitas: g.Sum(a => a.Visitas), Visitantes: g.Sum(a => a.Visitantes)));

            for (var dia = desde; dia <= hoje; dia = dia.AddDays(1))
            {
                porDia.TryGetValue(dia, out var totais);
                vm.PorDia.Add(new AcessoDiaViewModel
                {
                    Dia = dia,
                    Visitas = totais.Visitas,
                    Visitantes = totais.Visitantes,
                    Detalhe = linhas.Where(a => a.Dia == dia)
                        .OrderByDescending(a => a.Visitas)
                        .Select(a => (a.Ferramenta, a.Visitas))
                        .ToList(),
                });
            }

            return View(vm);
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
        // Recalcula a posição de TODOS os jogadores com escalação registrada
        // (lógica em PosicaoJogadorHelper — a mesma roda automaticamente ao
        // salvar a escalação de um jogo, só para os jogadores envolvidos).
        [HttpPost]
        public async Task<IActionResult> RecalcularPosicoesJogadores()
        {
            var (atualizados, comEscalacao) = await PosicaoJogadorHelper.RecalcularAsync(_context);

            _logger.LogInformation(
                "[Admin] RecalcularPosicoesJogadores: {A} jogadores atualizados (de {T} com escalação).",
                atualizados, comEscalacao);

            TempData["Sucesso"] =
                $"Posições recalculadas: {atualizados} jogador(es) atualizados a partir de {comEscalacao} com escalações registradas.";
            return RedirectToAction("Index", "Servicos");
        }

        // POST: /Admin/AtualizarPlacarPenaltis
        // Backfill de jogos importados antes da correção que fazia a api-football
        // gravar cada cobrança em PenaltisDisputa mas nunca atualizava
        // Jogo.PenaltisCasa/PenaltisVisitante — por isso a tela de Jogos, o
        // chaveamento da Copa e o resumo do placar em Analisar mostravam empate em
        // vez de quem avançou/venceu nos pênaltis. Deriva o placar (cobranças
        // convertidas por lado) a partir de PenaltisDisputa para todo jogo que já
        // tem disputa registrada.
        [HttpPost]
        public async Task<IActionResult> AtualizarPlacarPenaltis()
        {
            var porJogo = await _context.PenaltisDisputa
                .GroupBy(p => p.JogoId)
                .Select(g => new
                {
                    JogoId = g.Key,
                    Casa = g.Count(p => p.IsTimeCasa && p.Convertido),
                    Visitante = g.Count(p => !p.IsTimeCasa && p.Convertido)
                })
                .ToListAsync();

            var jogoIds = porJogo.Select(g => g.JogoId).ToList();
            var jogos = await _context.Jogos
                .Where(j => jogoIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id);

            int atualizados = 0;
            foreach (var g in porJogo)
            {
                if (!jogos.TryGetValue(g.JogoId, out var jogo)) continue;
                if (jogo.PenaltisCasa == g.Casa && jogo.PenaltisVisitante == g.Visitante) continue;

                jogo.PenaltisCasa = g.Casa;
                jogo.PenaltisVisitante = g.Visitante;
                atualizados++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Admin] AtualizarPlacarPenaltis: {A} jogo(s) atualizados (de {T} com disputa registrada).",
                atualizados, porJogo.Count);

            TempData["Sucesso"] =
                $"Placar de pênaltis atualizado: {atualizados} jogo(s) de {porJogo.Count} com disputa registrada.";
            return RedirectToAction("Index", "Servicos");
        }
    }
}
