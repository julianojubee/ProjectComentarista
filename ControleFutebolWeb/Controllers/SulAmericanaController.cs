// ControleFutebolWeb/Controllers/SulAmericanaController.cs
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class SulAmericanaController : Controller
    {
        private readonly FutebolContext _context;
        private readonly TransfermarktSulAmericanaService _tmService;
        private readonly ILogger<SulAmericanaController> _logger;

        // ID fixo da competição Sul-Americana no banco — ajuste se necessário
        private const int COMPETICAO_ID = 4;

        public SulAmericanaController(
            FutebolContext context,
            TransfermarktSulAmericanaService tmService,
            ILogger<SulAmericanaController> logger)
        {
            _context = context;
            _tmService = tmService;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> DiagRaw()
        {
            var http = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient();

            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9");

            var urls = new[]
            {
        "https://www.transfermarkt.com.br/copa-sudamericana/ergebnisse/pokalwettbewerb/CS/saison_id/2025",
        "https://www.transfermarkt.com.br/copa-sudamericana/ergebnisse/pokalwettbewerb/CS/saison_id/2026",
        "https://www.transfermarkt.com.br/copa-sudamericana/spieltag/pokalwettbewerb/CS/saison_id/2025/spieltag/1",
        "https://www.transfermarkt.com.br/copa-sudamericana/spieltag/pokalwettbewerb/CS/saison_id/2026/spieltag/1",
        "https://www.transfermarkt.com.br/copa-sudamericana/gesamtspielplan/pokalwettbewerb/CS/saison_id/2025",
        "https://www.transfermarkt.com.br/copa-sudamericana/gesamtspielplan/pokalwettbewerb/CS/saison_id/2026",
    };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<style>table{border-collapse:collapse} td,th{border:1px solid #ccc;padding:6px;font-size:12px}</style>");
            sb.AppendLine("<table><tr><th>URL</th><th>Status</th><th>Chars</th><th>Título</th><th>Tem /spielbericht/</th><th>Primeiros 800 chars</th></tr>");

            foreach (var url in urls)
            {
                try
                {
                    var resp = await http.GetAsync(url);
                    var html = await resp.Content.ReadAsStringAsync();

                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);
                    var titulo = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";

                    bool temJogo = html.Contains("/spielbericht/") || html.Contains("/begegnung_detail/");

                    // Primeiros 800 chars (texto visível, sem tags)
                    var textoLimpo = System.Text.RegularExpressions.Regex
                        .Replace(html, "<[^>]+>", " ");
                    textoLimpo = System.Text.RegularExpressions.Regex
                        .Replace(textoLimpo, @"\s+", " ").Trim();
                    if (textoLimpo.Length > 800) textoLimpo = textoLimpo[..800];

                    sb.AppendLine($"<tr>" +
                        $"<td style='max-width:300px;word-break:break-all'>{url}</td>" +
                        $"<td>{(int)resp.StatusCode}</td>" +
                        $"<td>{html.Length}</td>" +
                        $"<td>{System.Web.HttpUtility.HtmlEncode(titulo)}</td>" +
                        $"<td style='color:{(temJogo ? "green" : "red")}'>{(temJogo ? "SIM" : "NÃO")}</td>" +
                        $"<td style='font-size:10px;max-width:400px'>{System.Web.HttpUtility.HtmlEncode(textoLimpo)}</td>" +
                        $"</tr>");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"<tr><td>{url}</td><td colspan='5' style='color:red'>ERRO: {ex.Message}</td></tr>");
                }
            }

            sb.AppendLine("</table>");
            return Content(sb.ToString(), "text/html");
        }

        // Teste rápido para validar extração dos jogos
        public async Task<IActionResult> TesteBrasileirao()
        {
            var tmService = HttpContext.RequestServices.GetRequiredService<OgolService>();

            var jogos = await tmService.BuscarJogosCompeticaoPorLink(
                "https://www.ogol.com.br/edicao/campeonato-brasileiro-serie-a/2025");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<h2>Primeiros 5 jogos extraídos</h2>");
            sb.AppendLine("<table border='1' cellpadding='6' style='font-size:12px'>");
            sb.AppendLine("<tr><th>#</th><th>Casa</th><th>Placar</th><th>Visitante</th><th>Data</th><th>Rodada</th></tr>");

            int i = 0;
            foreach (var j in jogos.Take(5))
            {
                var placar = j.PlacarCasa.HasValue
                    ? $"{j.PlacarCasa} × {j.PlacarVisitante}"
                    : "—";

                var dataStr = j.Data.HasValue
                    ? j.Data.Value.ToString("dd/MM/yyyy HH:mm")
                    : "—";

                sb.AppendLine($"<tr><td>{++i}</td><td>{j.NomeTimeCasa}</td>" +
                              $"<td style='text-align:center'>{placar}</td>" +
                              $"<td>{j.NomeTimeVisitante}</td>" +
                              $"<td>{dataStr}</td>" +
                              $"<td>{j.Rodada}</td></tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine($"<p>Total extraído: {jogos.Count} jogos</p>");

            return Content(sb.ToString(), "text/html");
        }


        [HttpGet]
        public async Task<IActionResult> DiagBrasileiraoRaw()
        {
            var http = HttpContext.RequestServices
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient();

            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9");

            var urls = new[]
            {
        "https://www.transfermarkt.com.br/campeonato-brasileiro-serie-a/gesamtspielplan/wettbewerb/BRA1/saison_id/2025",
        "https://www.transfermarkt.com.br/campeonato-brasileiro-serie-a/spieltag/wettbewerb/BRA1/saison_id/2025/spieltag/1",
        "https://www.transfermarkt.com.br/campeonato-brasileiro-serie-a/spieltag/wettbewerb/BRA1/saison_id/2025/spieltag/2",
        };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<style>table{border-collapse:collapse} td,th{border:1px solid #ccc;padding:6px;font-size:12px}</style>");
            sb.AppendLine("<table><tr><th>URL</th><th>Status</th><th>Chars</th><th>Tem /spielbericht/</th><th>Boxes encontrados</th><th>Primeiros 1000 chars</th></tr>");

            foreach (var url in urls)
            {
                try
                {
                    var resp = await http.GetAsync(url);
                    var html = await resp.Content.ReadAsStringAsync();

                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);

                    bool temJogo = html.Contains("/spielbericht/") || html.Contains("/begegnung_detail/");

                    var boxes = doc.DocumentNode.SelectNodes(
                        "//div[contains(@class,'box')] | //div[contains(@class,'content-box')]");

                    // Pega primeiros 1000 chars do texto visível
                    var textoLimpo = System.Text.RegularExpressions.Regex
                        .Replace(html, "<[^>]+>", " ");
                    textoLimpo = System.Text.RegularExpressions.Regex
                        .Replace(textoLimpo, @"\s+", " ").Trim();
                    if (textoLimpo.Length > 1000) textoLimpo = textoLimpo[..1000];

                    sb.AppendLine($"<tr>" +
                        $"<td style='max-width:200px;word-break:break-all'>{url}</td>" +
                        $"<td>{(int)resp.StatusCode}</td>" +
                        $"<td>{html.Length}</td>" +
                        $"<td style='color:{(temJogo ? "green" : "red")}'>{(temJogo ? "SIM" : "NÃO")}</td>" +
                        $"<td>{boxes?.Count ?? 0}</td>" +
                        $"<td style='font-size:10px;max-width:400px'>{System.Web.HttpUtility.HtmlEncode(textoLimpo)}</td>" +
                        $"</tr>");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"<tr><td>{url}</td><td colspan='5' style='color:red'>ERRO: {ex.Message}</td></tr>");
                }
            }

            sb.AppendLine("</table>");
            return Content(sb.ToString(), "text/html");
        }


        [HttpGet]
        public async Task<IActionResult> DiagSincronizar(int ano = 2026, bool importarEscalacoes = false)
        {
            const int COMPETICAO_ID = 4;

            var resultado = await _tmService.SincronizarAsync(
                _context, COMPETICAO_ID, ano, importarEscalacoes);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"<style>
        body{font-family:sans-serif;font-size:13px;padding:20px;background:#f5f5f5}
        h2{margin-bottom:4px}
        .resumo{display:flex;gap:12px;flex-wrap:wrap;margin:16px 0}
        .card{background:#fff;border:1px solid #ddd;border-radius:8px;padding:12px 18px;text-align:center;min-width:110px}
        .card .num{font-size:28px;font-weight:700;color:#1d4ed8}
        .card .label{font-size:11px;color:#666;margin-top:2px}
        table{border-collapse:collapse;width:100%;background:#fff;border-radius:8px;
              overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,.08);margin-bottom:8px}
        th{background:#1e293b;color:#fff;padding:8px 10px;font-size:11px;text-align:left}
        td{padding:7px 10px;border-bottom:1px solid #f0f0f0;font-size:12px}
        tr:last-child td{border-bottom:none}
        tr:hover td{background:#f8faff}
        .badge{display:inline-block;padding:2px 8px;border-radius:10px;font-size:11px;font-weight:600}
        .bg{background:#dcfce7;color:#15803d}
        .by{background:#fef9c3;color:#854d0e}
        .br{background:#fee2e2;color:#b91c1c}
        .bb{background:#f1f5f9;color:#475569}
        h3{margin:24px 0 8px;color:#1e293b}
        .empty{color:#94a3b8;font-style:italic;padding:12px}
    </style>");

            sb.AppendLine($"<h2>Diagnóstico de Sincronização — Sul-Americana {ano}</h2>");
            sb.AppendLine($"<p style='color:#64748b'>Executado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss} &nbsp;|&nbsp; " +
                          $"Escalações: {(importarEscalacoes ? "Sim" : "Não")} &nbsp;|&nbsp; " +
                          $"<a href='?ano={ano}&importarEscalacoes=true'>▶ Rodar com escalações</a> &nbsp;|&nbsp; " +
                          $"<a href='?ano={ano}'>↺ Rodar sem escalações</a></p>");

            // Cards
            sb.AppendLine("<div class='resumo'>");
            void Card(string num, string label) =>
                sb.AppendLine($"<div class='card'><div class='num'>{num}</div><div class='label'>{label}</div></div>");
            Card(resultado.JogosEncontradosNaSite.ToString(), "Jogos no site");
            Card(resultado.JogosAtualizados.ToString(), "Atualizados");
            Card(resultado.PlacaresAtualizados.ToString(), "Placares novos");
            Card(resultado.EscalacoesImportadas.ToString(), "Escalações");
            Card(resultado.GolsImportados.ToString(), "Gols");
            Card(resultado.JogosNaoEncontrados.ToString(), "Não encontrados");
            Card(resultado.Avisos.Count.ToString(), "Avisos");
            sb.AppendLine("</div>");

            // Avisos
            sb.AppendLine("<h3>⚠️ Avisos</h3>");
            if (!resultado.Avisos.Any())
            {
                sb.AppendLine("<p class='empty'>Nenhum aviso.</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>#</th><th>Mensagem</th><th>Tipo</th></tr>");
                int n = 0;
                foreach (var aviso in resultado.Avisos)
                {
                    string tipo, cls;
                    if (aviso.Contains("não mapeado") || aviso.Contains("NÃO ACHADO") || aviso.Contains("Times não"))
                    { tipo = "Time não mapeado"; cls = "br"; }
                    else if (aviso.Contains("não encontrado no banco") || aviso.Contains("Jogo não"))
                    { tipo = "Jogo não encontrado"; cls = "by"; }
                    else if (aviso.ToLower().Contains("jogador"))
                    { tipo = "Jogador"; cls = "bb"; }
                    else if (aviso.Contains("Erro") || aviso.Contains("erro"))
                    { tipo = "Erro"; cls = "br"; }
                    else
                    { tipo = "Info"; cls = "bb"; }

                    sb.AppendLine($"<tr><td style='color:#94a3b8;width:30px'>{++n}</td>" +
                                  $"<td>{System.Web.HttpUtility.HtmlEncode(aviso)}</td>" +
                                  $"<td><span class='badge {cls}'>{tipo}</span></td></tr>");
                }
                sb.AppendLine("</table>");
            }

            // Jogos atualizados
            sb.AppendLine("<h3>✅ Jogos atualizados (Atualizado = 1)</h3>");
            var atualizados = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID && j.Atualizado == 1)
                .OrderByDescending(j => j.Data)
                .ToListAsync();

            if (!atualizados.Any())
            {
                sb.AppendLine("<p class='empty'>Nenhum jogo com Atualizado=1.</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>Data</th><th>R</th><th>Grupo</th>" +
                              "<th>Casa</th><th style='text-align:center'>Placar</th>" +
                              "<th>Visitante</th></tr>");
                foreach (var j in atualizados)
                {
                    // 🔹 Placar
                    var placar = j.PlacarCasa.HasValue
                        ? $"<b>{j.PlacarCasa} × {j.PlacarVisitante}</b>"
                        : "<span class='badge bb'>—</span>";

                    // 🔹 Data (se for nula, mostra "—")
                    var dataStr = j.Data.HasValue
                        ? j.Data.Value.ToLocalTime().ToString("dd/MM/yy")
                        : "—";

                    // 🔹 Grupo (se for nulo, mostra "—")
                    var grupoStr = string.IsNullOrEmpty(j.Grupo) ? "—" : j.Grupo;

                    sb.AppendLine(
                        $"<tr>" +
                        $"<td>{dataStr}</td>" +
                        $"<td>{j.Rodada}</td>" +
                        $"<td>{grupoStr}</td>" +
                        $"<td>{j.TimeCasa?.Nome}</td>" +
                        $"<td style='text-align:center'>{placar}</td>" +
                        $"<td>{j.TimeVisitante?.Nome}</td>" +
                        $"</tr>"
                    );
                }

                sb.AppendLine("</table>");
            }

            // Jogos pendentes
            sb.AppendLine("<h3>⏳ Pendentes (Atualizado ≠ 1)</h3>");
            var pendentes = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID && j.Atualizado != 1)
                .OrderBy(j => j.Rodada).ThenBy(j => j.Data)
                .ToListAsync();

            if (!pendentes.Any())
            {
                sb.AppendLine("<p class='empty'>Todos sincronizados!</p>");
            }
            else
            {
                sb.AppendLine("<table><tr><th>Data</th><th>R</th><th>Grupo</th>" +
                              "<th>Casa</th><th style='text-align:center'>Placar banco</th>" +
                              "<th>Visitante</th><th>Situação</th></tr>");
                foreach (var j in pendentes)
                {
                    // 🔹 Placar
                    var placar = j.PlacarCasa.HasValue
                        ? $"{j.PlacarCasa} × {j.PlacarVisitante}"
                        : "—";

                    // 🔹 Situação
                    var sit = j.PlacarCasa.HasValue
                        ? "<span class='badge by'>Tem placar, não sincronizado</span>"
                        : "<span class='badge bb'>Sem placar</span>";

                    // 🔹 Data (se for nula, mostra "—")
                    var dataStr = j.Data.HasValue
                        ? j.Data.Value.ToLocalTime().ToString("dd/MM/yy")
                        : "—";

                    // 🔹 Grupo (se for nulo, mostra "—")
                    var grupoStr = string.IsNullOrEmpty(j.Grupo) ? "—" : j.Grupo;

                    sb.AppendLine(
                        $"<tr>" +
                        $"<td>{dataStr}</td>" +
                        $"<td>{j.Rodada}</td>" +
                        $"<td>{grupoStr}</td>" +
                        $"<td>{j.TimeCasa?.Nome}</td>" +
                        $"<td style='text-align:center'>{placar}</td>" +
                        $"<td>{j.TimeVisitante?.Nome}</td>" +
                        $"<td>{sit}</td>" +
                        $"</tr>"
                    );
                }

                sb.AppendLine("</table>");
            }

            return Content(sb.ToString(), "text/html");
        }

        [HttpGet]
        public async Task<IActionResult> DiagGrupos(int ano = 2026)
        {
            await _tmService.AtualizarGruposAsync(_context, COMPETICAO_ID, ano);

            var jogos = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID)
                .OrderBy(j => j.Grupo).ThenBy(j => j.Rodada)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<style>body{font-family:sans-serif;font-size:13px;padding:20px}" +
                "table{border-collapse:collapse;width:100%}" +
                "th{background:#1e293b;color:#fff;padding:7px 10px;font-size:11px;text-align:left}" +
                "td{padding:6px 10px;border-bottom:1px solid #f0f0f0;font-size:12px}" +
                "tr:hover td{background:#f8faff}</style>");
            sb.AppendLine($"<h2>Grupos — Sul-Americana {ano}</h2>");
            sb.AppendLine("<table><tr><th>Grupo</th><th>Rodada</th><th>Casa</th><th>Placar</th><th>Visitante</th></tr>");
            foreach (var j in jogos)
            {
                var placar = j.PlacarCasa.HasValue ? $"{j.PlacarCasa} x {j.PlacarVisitante}" : "—";
                sb.AppendLine($"<tr><td>{j.Grupo ?? "—"}</td><td>{j.Rodada}</td>" +
                    $"<td>{j.TimeCasa?.Nome}</td><td>{placar}</td><td>{j.TimeVisitante?.Nome}</td></tr>");
            }
            sb.AppendLine("</table>");
            return Content(sb.ToString(), "text/html");
        }

        [HttpGet]
        public async Task<IActionResult> DiagCalendario(int ano = 2026)
        {
            var jogos = await _tmService.BuscarCalendarioAsync(ano);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<h3>Total: {jogos.Count} | Com placar: {jogos.Count(j => j.PlacarCasa.HasValue)}</h3>");
            sb.AppendLine("<table border='1' cellpadding='4' style='font-size:12px'>");
            sb.AppendLine("<tr><th>#</th><th>Casa</th><th>Placar</th><th>Visitante</th><th>Data</th><th>Grupo</th><th>Link</th></tr>");
            int i = 0;
            foreach (var j in jogos)
                sb.AppendLine($"<tr><td>{++i}</td><td>{j.NomeTimeCasa}</td>" +
                    $"<td>{j.PlacarCasa?.ToString() ?? "—"}-{j.PlacarVisitante?.ToString() ?? "—"}</td>" +
                    $"<td>{j.NomeTimeVisitante}</td>" +
                    $"<td>{j.Data?.ToString("dd/MM/yyyy") ?? "?"}</td>" +
                    $"<td>{j.Grupo}</td>" +
                    $"<td>{(string.IsNullOrEmpty(j.LinkDetalhes) ? "—" : $"<a href='{j.LinkDetalhes}' target='_blank'>ver</a>")}</td></tr>");
            sb.AppendLine("</table>");
            return Content(sb.ToString(), "text/html");
        }
        // GET: /SulAmericana
        public IActionResult Index()
        {
            var grupos = MontarGrupos();
            var proximosJogos = BuscarProximosJogos();
            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (_context.Jogos.Any(j => j.CompeticaoId == COMPETICAO_ID)
                    ? _context.Jogos.Where(j => j.CompeticaoId == COMPETICAO_ID).Max(j => j.Rodada)
                    : 0);

            ViewBag.Grupos = grupos;
            ViewBag.ProximosJogos = proximosJogos;
            ViewBag.RodadaAtual = rodadaAtual;

            return View();
        }

        // ── POST: aciona a sincronização manual ──────────────────────────────
        // POST: /SulAmericana/Sincronizar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sincronizar(
            int ano = 2025,
            bool importarEscalacoes = true)
        {
            _logger.LogInformation(
                "[SulAmericana] Sincronização manual iniciada. Ano={Ano}, " +
                "ImportarEscalacoes={Esc}", ano, importarEscalacoes);

            var resultado = await _tmService.SincronizarAsync(
                _context, COMPETICAO_ID, ano, importarEscalacoes);

            _logger.LogInformation("[SulAmericana] {Resultado}", resultado.ToString());

            TempData["Sucesso"] =
                $"Sincronização concluída! " +
                $"{resultado.JogosAtualizados} jogo(s) atualizado(s), " +
                $"{resultado.PlacaresAtualizados} placar(es) atualizado(s), " +
                $"{resultado.EscalacoesImportadas} escalação(ões) importada(s), " +
                $"{resultado.GolsImportados} gol(s) importado(s).";

            if (resultado.Avisos.Any())
                TempData["Avisos"] = string.Join(" | ", resultado.Avisos.Take(10));

            return RedirectToAction(nameof(Index));
        }

        // ── GET: retorna o status de um jogo específico (AJAX) ───────────────
        // GET: /SulAmericana/StatusJogo/5
        [HttpGet]
        public async Task<IActionResult> StatusJogo(int id)
        {
            var jogo = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Gols).ThenInclude(g => g.Jogador)
                .Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            return Ok(new
            {
                id = jogo.Id,
                placarCasa = jogo.PlacarCasa,
                placarVisitante = jogo.PlacarVisitante,
                atualizado = jogo.Atualizado,
                gols = jogo.Gols?.Count ?? 0,
                escalacoes = jogo.Escalacoes?.Count ?? 0
            });
        }

        // ── POST: reinicia a flag Atualizado de um jogo ───────────────────────
        // POST: /SulAmericana/ResetarJogo/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetarJogo(int id)
        {
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return NotFound();

            jogo.Atualizado = 0;

            // Remove escalações e gols para reimportar na próxima sincronização
            var escalacoes = await _context.Escalacoes
                .Where(e => e.JogoId == id).ToListAsync();
            _context.Escalacoes.RemoveRange(escalacoes);

            var gols = await _context.Gols
                .Where(g => g.JogoId == id).ToListAsync();
            _context.Gols.RemoveRange(gols);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] =
                $"Jogo {id} resetado. Será reprocessado na próxima sincronização.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS PRIVADOS
        // ─────────────────────────────────────────────────────────────────────

        private List<GrupoViewModel> MontarGrupos()
        {
            var jogosRealizados = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue &&
                            !string.IsNullOrEmpty(j.Grupo))
                .OrderBy(j => j.Data)
                .ToList();

            return jogosRealizados
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .Select(nomeGrupo =>
                {
                    var jogosGrupo = jogosRealizados
                        .Where(j => j.Grupo == nomeGrupo).ToList();
                    return new GrupoViewModel
                    {
                        Nome = nomeGrupo,
                        Times = CalcularClassificacaoGrupo(jogosGrupo)
                    };
                })
                .ToList();
        }

        private List<Jogo> BuscarProximosJogos()
        {
            // Jogos sem placar (agendados) ou com placar mas recentes
            var sem = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID &&
                            (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue))
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            if (sem.Any()) return sem;

            return _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == COMPETICAO_ID)
                .OrderByDescending(j => j.Data)
                .Take(10)
                .ToList();
        }

        private static List<Classificacao> CalcularClassificacaoGrupo(List<Jogo> jogos)
        {
            var tab = new Dictionary<int, Classificacao>();

            foreach (var j in jogos)
            {
                if (j.TimeCasa == null || j.TimeVisitante == null) continue;
                if (!j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue) continue;

                if (!tab.ContainsKey(j.TimeCasaId))
                    tab[j.TimeCasaId] = new Classificacao
                    { TimeId = j.TimeCasaId, Time = j.TimeCasa };
                if (!tab.ContainsKey(j.TimeVisitanteId))
                    tab[j.TimeVisitanteId] = new Classificacao
                    { TimeId = j.TimeVisitanteId, Time = j.TimeVisitante };

                var c = tab[j.TimeCasaId];
                var v = tab[j.TimeVisitanteId];

                c.Jogos++; v.Jogos++;
                c.GolsPro += j.PlacarCasa.Value;
                c.GolsContra += j.PlacarVisitante.Value;
                v.GolsPro += j.PlacarVisitante.Value;
                v.GolsContra += j.PlacarCasa.Value;

                if (j.PlacarCasa > j.PlacarVisitante)
                { c.Vitorias++; c.Pontos += 3; v.Derrotas++; }
                else if (j.PlacarCasa < j.PlacarVisitante)
                { v.Vitorias++; v.Pontos += 3; c.Derrotas++; }
                else
                { c.Empates++; v.Empates++; c.Pontos++; v.Pontos++; }
            }

            var lista = tab.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.GolsPro - t.GolsContra)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            for (int i = 0; i < lista.Count; i++) lista[i].Posicao = i + 1;
            return lista;
        }
    }
}