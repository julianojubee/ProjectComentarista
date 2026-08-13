using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Janela de Transferências: histórico das trocas de clube detectadas
    // automaticamente na importação (jogador cadastrado num time aparece na
    // escalação de outro clube). Filtros por time, competição e jogador.
    [Authorize]
    public class TransferenciasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Services.ApiFootballService _apiFootball;
        private readonly IWebHostEnvironment _env;

        // Temporada usada no catálogo de ligas (wwwroot/data/competicoes-api-2026.json)
        // e nas consultas de clubes na api-football.
        private const int TemporadaCatalogo = 2026;

        public TransferenciasController(
            FutebolContext context,
            UserManager<ApplicationUser> userManager,
            Services.ApiFootballService apiFootball,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _apiFootball = apiFootball;
            _env = env;
        }

        // Teto de linhas da tela. A lista é longa (milhares de registros) e a
        // página monta tudo de uma vez; quando corta, a view avisa.
        private const int LimiteLista = 300;

        // GET: /Transferencias?timeId=1&competicaoId=2&jogador=borre&ordem=data
        // ordem=registro (padrão) → ordem de gravação, para ver no topo o que uma
        // sincronização acabou de trazer: a data do anúncio costuma ser semanas
        // atrás e jogaria o registro para o meio da lista, dando a impressão de
        // que nada foi gravado. ordem=data → data da transferência.
        public async Task<IActionResult> Index(int? timeId, int? competicaoId, string? jogador, string? ordem)
        {
            var query = _context.Transferencias
                .AsNoTracking()
                .Include(t => t.Jogador)
                .Include(t => t.TimeOrigem)
                .Include(t => t.TimeDestino)
                .Include(t => t.Jogo).ThenInclude(j => j!.Competicao)
                .AsQueryable();

            // Time participa como origem OU destino
            if (timeId.HasValue)
                query = query.Where(t => t.TimeOrigemId == timeId || t.TimeDestinoId == timeId);

            if (competicaoId.HasValue)
                query = query.Where(t => t.Jogo != null && t.Jogo.CompeticaoId == competicaoId);

            if (!string.IsNullOrWhiteSpace(jogador))
                query = query.Where(t => t.Jogador.Nome.ToLower().Contains(jogador.ToLower()));

            var porRegistro = !string.Equals(ordem, "data", StringComparison.OrdinalIgnoreCase);

            var totalFiltrado = await query.CountAsync();

            var ordenada = porRegistro
                ? query.OrderByDescending(t => t.Id)
                : query.OrderByDescending(t => t.Data).ThenByDescending(t => t.Id);

            var transferencias = await ordenada
                .Take(LimiteLista)
                .ToListAsync();

            // Combos dos filtros: só times/competições que aparecem no histórico
            var timeIdsUsados = await _context.Transferencias
                .Where(t => t.TimeDestinoId != null)
                .Select(t => t.TimeDestinoId!.Value)
                .Union(_context.Transferencias
                    .Where(t => t.TimeOrigemId != null)
                    .Select(t => t.TimeOrigemId!.Value))
                .Distinct()
                .ToListAsync();

            var times = await _context.Times
                .Where(t => timeIdsUsados.Contains(t.Id))
                .OrderBy(t => t.Nome)
                .ToListAsync();

            var competicaoIdsUsadas = await _context.Transferencias
                .Where(t => t.JogoId != null)
                .Select(t => t.Jogo!.CompeticaoId)
                .Distinct()
                .ToListAsync();

            var competicoes = await _context.Competicoes
                .Where(c => competicaoIdsUsadas.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();

            ViewBag.Times = new SelectList(times, "Id", "Nome", timeId);
            ViewBag.Competicoes = new SelectList(competicoes, "Id", "Nome", competicaoId);
            ViewBag.FiltroTimeId = timeId;
            ViewBag.FiltroCompeticaoId = competicaoId;
            ViewBag.FiltroJogador = jogador;
            ViewBag.Ordem = porRegistro ? "registro" : "data";
            ViewBag.TotalFiltrado = totalFiltrado;
            ViewBag.LimiteLista = LimiteLista;

            // Dados do formulário de transferência manual: todos os jogadores (com o
            // clube atual no rótulo, para conferência) e os clubes de destino possíveis.
            ViewBag.JogadoresManual = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .OrderBy(j => j.Nome)
                .Select(j => new { j.Id, Rotulo = j.Nome + " — " + (j.Time != null ? j.Time.Nome : "sem clube") })
                .ToListAsync();

            ViewBag.TimesDestino = new SelectList(
                await _context.Times.Where(t => !t.EhSelecao).OrderBy(t => t.Nome).ToListAsync(),
                "Id", "Nome");

            ViewBag.UsuarioAtualId = _userManager.GetUserId(User);

            return View(transferencias);
        }

        // POST: /Transferencias/TransferirManual
        // Transferência manual: o usuário escolhe o jogador e o clube de destino,
        // sem esperar o jogador aparecer escalado num jogo importado. Troca o clube
        // e registra no histórico, igual à detecção automática (JogoId fica null).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirManual(int jogadorId, int timeDestinoId)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);
            var destino = await _context.Times.FirstOrDefaultAsync(t => t.Id == timeDestinoId);

            if (jogador == null || destino == null)
            {
                TempData["Erro"] = "Jogador ou clube de destino não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (destino.EhSelecao)
            {
                TempData["Erro"] = "O destino precisa ser um clube — seleções não contam como transferência.";
                return RedirectToAction(nameof(Index));
            }

            if (jogador.TimeId == destino.Id)
            {
                TempData["Erro"] = $"{jogador.NomeExibicao} já pertence a {destino.Nome}.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioId = _userManager.GetUserId(User);

            _context.Transferencias.Add(new Transferencia
            {
                JogadorId = jogador.Id,
                TimeOrigemId = jogador.TimeId,
                TimeDestinoId = destino.Id,
                JogoId = null,
                Data = DateTime.UtcNow,
                UsuarioId = usuarioId
            });

            var origemNome = jogador.Time?.Nome ?? "sem clube";
            jogador.TimeId = destino.Id;
            // Voltou a ter clube: se estava marcado como aposentado, deixa de estar.
            jogador.Aposentado = false;
            jogador.AposentadoEm = null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{jogador.NomeExibicao} transferido: {origemNome} → {destino.Nome}.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Transferência para clube de fora do sistema (botão "Transferências"
        // em /Jogadores/Estatisticas). Quando o jogador vai para um clube de uma
        // liga que ninguém cadastrou, ele nunca aparece numa escalação importada
        // e a detecção automática não acontece. Aqui o usuário escolhe país →
        // liga (mesmo catálogo de /Competicoes/CompeticoesApi) → clube (consulta
        // /teams na api-football) e o clube escolhido é criado no banco, já com
        // a liga de origem gravada, para reaproveitamento nas próximas vezes.
        // ─────────────────────────────────────────────────────────────────────

        // Catálogo estático das ligas da api-football (mesmo arquivo da tela
        // /Competicoes/CompeticoesApi).
        private async Task<List<CompeticaoApiLiga>> CarregarCatalogoLigasAsync()
        {
            var caminho = Path.Combine(_env.WebRootPath, "data", $"competicoes-api-{TemporadaCatalogo}.json");
            if (!System.IO.File.Exists(caminho)) return new();

            var json = await System.IO.File.ReadAllTextAsync(caminho);
            return System.Text.Json.JsonSerializer.Deserialize<List<CompeticaoApiLiga>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        // GET: /Transferencias/ApiPaises — países que têm liga no catálogo
        [HttpGet]
        public async Task<IActionResult> ApiPaises()
        {
            var paises = (await CarregarCatalogoLigasAsync())
                .GroupBy(l => l.Pais)
                .Select(g => new
                {
                    nome = g.Key,
                    bandeira = g.First().Bandeira,
                    ligas = g.Count()
                })
                // "World" (internacionais) primeiro, igual em /Competicoes/CompeticoesApi
                .OrderBy(p => p.nome == "World" ? 0 : 1)
                .ThenBy(p => p.nome)
                .ToList();

            return Json(paises);
        }

        // GET: /Transferencias/ApiLigas?pais=Qatar — ligas do país escolhido
        [HttpGet]
        public async Task<IActionResult> ApiLigas(string? pais)
        {
            if (string.IsNullOrWhiteSpace(pais))
                return Json(Array.Empty<object>());

            var ligas = (await CarregarCatalogoLigasAsync())
                .Where(l => string.Equals(l.Pais, pais, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.Nome)
                .Select(l => new { id = l.Id, nome = l.Nome, tipo = l.Tipo, logo = l.Logo })
                .ToList();

            return Json(ligas);
        }

        // GET: /Transferencias/ApiClubes?leagueId=824 — clubes da liga na api-football.
        // Marca os que já existem no banco (timeId preenchido) para o usuário saber
        // que a transferência não vai criar clube novo.
        [HttpGet]
        public async Task<IActionResult> ApiClubes(int leagueId, int? season, CancellationToken ct)
        {
            if (leagueId <= 0)
                return Json(new { erro = "Liga inválida." });

            List<Services.AfTeamCatalogo> clubes;
            try
            {
                clubes = await _apiFootball.BuscarClubesDaLigaAsync(leagueId, season ?? TemporadaCatalogo, ct);
            }
            catch (Exception ex)
            {
                return Json(new { erro = "Não foi possível consultar os clubes na api-football: " + ex.Message });
            }

            var idsApi = clubes.Select(c => c.Id).ToList();
            var existentes = await _context.Times
                .AsNoTracking()
                .Where(t => idsApi.Contains(t.IdApi))
                .ToDictionaryAsync(t => t.IdApi, t => t.Id, ct);

            var itens = clubes.Select(c => new
            {
                idApi = c.Id,
                nome = c.Name,
                pais = c.Country,
                logo = c.Logo,
                timeId = existentes.TryGetValue(c.Id, out var id) ? id : (int?)null
            }).ToList();

            return Json(new { clubes = itens });
        }

        // POST: /Transferencias/TransferirParaClubeApi
        // Transfere o jogador para um clube vindo da api-football, criando o Time
        // caso ainda não exista. Responde JSON (chamada via fetch pelo modal).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirParaClubeApi(
            int jogadorId, int idApi, string nome, string? logo, int leagueId, string? pais)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

            if (jogador == null)
                return Json(new { ok = false, mensagem = "Jogador não encontrado." });

            if (idApi <= 0 || string.IsNullOrWhiteSpace(nome))
                return Json(new { ok = false, mensagem = "Clube de destino inválido." });

            var destino = await _context.Times.FirstOrDefaultAsync(t => t.IdApi == idApi);

            if (destino == null)
            {
                // Clube ainda não existe no sistema: cria com os dados da API,
                // guardando a liga/país de origem para reaproveitar depois.
                var formacaoPadrao = await _context.Formacoes.FirstOrDefaultAsync();
                if (formacaoPadrao == null)
                    return Json(new { ok = false, mensagem = "Nenhuma formação cadastrada para servir de padrão do clube." });

                destino = new Time
                {
                    Nome             = Services.ApiFootballService.TraduzirNomeClube(nome.Trim()),
                    IdApi            = idApi,
                    EscudoUrl        = logo ?? "",
                    Cidade           = string.IsNullOrWhiteSpace(pais) ? "Importado" : pais,
                    CorPrincipal     = "#000000",
                    CorSecundaria    = "#FFFFFF",
                    FormacaoPadraoId = formacaoPadrao.Id,
                    EhSelecao        = false,
                    LigaIdApi        = leagueId,
                    PaisApi          = pais
                };
                _context.Times.Add(destino);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Já existia (importado de algum jogo): completa a liga de origem
                // e o escudo, se ainda estiverem em branco.
                if (destino.LigaIdApi == null && leagueId > 0) destino.LigaIdApi = leagueId;
                if (string.IsNullOrWhiteSpace(destino.PaisApi)) destino.PaisApi = pais;
                if (string.IsNullOrWhiteSpace(destino.EscudoUrl)) destino.EscudoUrl = logo ?? "";
            }

            // Estádio do clube: a lista de clubes da liga já veio com o "venue" e
            // está em cache, então preencher aqui não custa requisição nova.
            if (string.IsNullOrWhiteSpace(destino.EstadioNome) && leagueId > 0)
            {
                try
                {
                    var entradas = await _apiFootball.BuscarEntradasClubesDaLigaAsync(leagueId, TemporadaCatalogo);
                    var entrada = entradas.FirstOrDefault(e => e.Team.Id == idApi);
                    Services.ApiFootballService.AplicarEstadio(destino, entrada?.Venue);
                }
                catch
                {
                    // Estádio é acessório: falha na API não pode impedir a transferência.
                }
            }

            if (destino.EhSelecao)
                return Json(new { ok = false, mensagem = "O destino precisa ser um clube — seleções não contam como transferência." });

            if (jogador.TimeId == destino.Id)
                return Json(new { ok = false, mensagem = $"{jogador.NomeExibicao} já pertence a {destino.Nome}." });

            _context.Transferencias.Add(new Transferencia
            {
                JogadorId     = jogador.Id,
                TimeOrigemId  = jogador.TimeId,
                TimeDestinoId = destino.Id,
                JogoId        = null,
                Data          = DateTime.UtcNow,
                UsuarioId     = _userManager.GetUserId(User)
            });

            var origemNome = jogador.Time?.Nome ?? "sem clube";
            jogador.TimeId = destino.Id;
            // Voltou a ter clube: se estava marcado como aposentado, deixa de estar.
            jogador.Aposentado = false;
            jogador.AposentadoEm = null;
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                mensagem = $"{jogador.NomeExibicao} transferido: {origemNome} → {destino.Nome}."
            });
        }

        // POST: /Transferencias/Aposentar
        // Jogador que parou de jogar: não há clube de destino, então o registro
        // entra no histórico com TimeDestinoId null e o jogador ganha a marca de
        // aposentado. O TimeId continua no último clube — jogos, escalações e
        // estatísticas antigas dependem dele. Responde JSON (fetch pelo modal).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aposentar(int jogadorId)
        {
            var jogador = await _context.Jogadores
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

            if (jogador == null)
                return Json(new { ok = false, mensagem = "Jogador não encontrado." });

            if (jogador.Aposentado)
                return Json(new { ok = false, mensagem = $"{jogador.NomeExibicao} já está marcado como aposentado." });

            _context.Transferencias.Add(new Transferencia
            {
                JogadorId     = jogador.Id,
                TimeOrigemId  = jogador.TimeId,
                TimeDestinoId = null,   // sem destino = aposentadoria
                JogoId        = null,
                Data          = DateTime.UtcNow,
                UsuarioId     = _userManager.GetUserId(User)
            });

            jogador.Aposentado = true;
            jogador.AposentadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var origemNome = jogador.Time?.Nome ?? "sem clube";
            return Json(new
            {
                ok = true,
                mensagem = $"{jogador.NomeExibicao} marcado como aposentado (último clube: {origemNome})."
            });
        }

        // POST: /Transferencias/Excluir
        // Só quem criou a transferência manual pode excluí-la — um engano de um
        // usuário não deve afetar o histórico/dados dos outros. Se o jogador ainda
        // estiver no clube de destino (ninguém o transferiu de novo depois), o clube
        // volta pro de origem; se já houve outra transferência por cima, só remove
        // o registro do histórico (reverter o time seria incorreto).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            var transferencia = await _context.Transferencias
                .Include(t => t.Jogador)
                .Include(t => t.TimeOrigem)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transferencia == null)
            {
                TempData["Erro"] = "Transferência não encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (transferencia.JogoId != null || transferencia.UsuarioId != usuarioId)
            {
                TempData["Erro"] = "Só é possível excluir transferências manuais criadas por você.";
                return RedirectToAction(nameof(Index));
            }

            // Sem destino = aposentadoria: desfazer significa tirar a marca de
            // aposentado (o clube nunca mudou, então não há o que reverter nele).
            var ehAposentadoria = transferencia.TimeDestinoId == null;

            if (ehAposentadoria)
            {
                transferencia.Jogador.Aposentado = false;
                transferencia.Jogador.AposentadoEm = null;
            }
            else if (transferencia.Jogador.TimeId == transferencia.TimeDestinoId && transferencia.TimeOrigemId != null)
            {
                transferencia.Jogador.TimeId = transferencia.TimeOrigemId.Value;
            }

            _context.Transferencias.Remove(transferencia);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = ehAposentadoria
                ? $"{transferencia.Jogador.NomeExibicao} não está mais marcado como aposentado."
                : $"Transferência de {transferencia.Jogador.NomeExibicao} excluída.";
            return RedirectToAction(nameof(Index));
        }
    }
}
