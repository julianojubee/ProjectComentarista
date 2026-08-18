using System.Diagnostics;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Buscar(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new object[0]);

            q = q.Trim();

            // EscudoUrl/FotoUrl/LogoUrl às vezes apontam para hosts da api-sports.io,
            // que precisam passar pelo MediaProxy — por isso o Url.FotoSrc só entra
            // depois do ToListAsync (não é traduzível para SQL).
            var timesRaw = await _context.Times
                .Where(t => t.Nome != null && EF.Functions.ILike(t.Nome, $"%{q}%"))
                .Select(t => new { t.Nome, t.Id, t.Cidade, t.EscudoUrl })
                .Take(5).ToListAsync();
            var times = timesRaw
                .Select(t => new { tipo = "Time", nome = t.Nome, id = t.Id, extra = t.Cidade, imagem = Url.FotoSrc(t.EscudoUrl) })
                .ToList();

            var jogadoresRaw = await _context.Jogadores
                .Where(j => j.Nome != null && EF.Functions.ILike(j.Nome, $"%{q}%"))
                .Select(j => new { j.Nome, j.Id, j.FotoUrl })
                .Take(5).ToListAsync();
            var jogadores = jogadoresRaw
                .Select(j => new { tipo = "Jogador", nome = j.Nome, id = j.Id, extra = (string?)null, imagem = Url.FotoSrc(j.FotoUrl) })
                .ToList();

            var competicoesRaw = await _context.Competicoes
                .Where(c => EF.Functions.ILike(c.Nome, $"%{q}%"))
                .Select(c => new { c.Nome, c.Id, c.Regiao, c.LogoUrl })
                .Take(5).ToListAsync();
            var competicoes = competicoesRaw
                .Select(c => new { tipo = "Competição", nome = c.Nome, id = c.Id, extra = c.Regiao, imagem = Url.FotoSrc(c.LogoUrl) })
                .ToList();

            var resultados = times.Cast<object>()
                .Concat(jogadores.Cast<object>())
                .Concat(competicoes.Cast<object>())
                .ToList();

            return Json(resultados);
        }

        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User);

            // Competições que o usuário marcou para compor a tabela da home.
            var competicoesHome = await _context.CompeticoesHomeUsuario
                .AsNoTracking()
                .Where(t => t.UsuarioId == uid)
                .Select(t => t.CompeticaoId)
                .ToListAsync();

            // Últimos 6 jogos finalizados (com placar)
            var jogosRecentes = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .OrderByDescending(j => j.Data)
                .Take(6)
                .ToListAsync();

            // Classificação: só as competições marcadas como "Home" na tela de competições
            // e, dentro de cada uma, só a temporada mais recente — senão o Brasileirão 2025
            // somaria pontos na mesma linha do 2026.
            var jogosFinalizados = new List<JogoResumo>();
            if (competicoesHome.Count > 0)
            {
                var temporadaAtual = await _context.Jogos
                    .AsNoTracking()
                    .Where(j => competicoesHome.Contains(j.CompeticaoId))
                    .GroupBy(j => j.CompeticaoId)
                    .Select(g => new { CompeticaoId = g.Key, Temporada = g.Max(j => j.Temporada) })
                    .ToListAsync();

                foreach (var ct in temporadaAtual)
                {
                    var jogosCompeticao = await _context.Jogos
                        .AsNoTracking()
                        .Where(j => j.CompeticaoId == ct.CompeticaoId
                                 && j.Temporada == ct.Temporada
                                 && j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                        .Select(j => new JogoResumo(j.TimeCasaId, j.TimeVisitanteId, j.PlacarCasa, j.PlacarVisitante))
                        .ToListAsync();
                    jogosFinalizados.AddRange(jogosCompeticao);
                }
            }

            // Agrega numa única passada O(jogos) — antes era O(times × jogos), pois
            // refiltrava a lista inteira de jogos para cada time.
            var agregado = new Dictionary<int, (int V, int E, int D, int GP, int GC)>();
            foreach (var j in jogosFinalizados)
            {
                int pc = j.PlacarCasa ?? 0, pv = j.PlacarVisitante ?? 0;
                var casa = agregado.GetValueOrDefault(j.TimeCasaId);
                var vis  = agregado.GetValueOrDefault(j.TimeVisitanteId);

                casa.GP += pc; casa.GC += pv;
                vis.GP  += pv; vis.GC  += pc;
                if (pc > pv)      { casa.V++; vis.D++; }
                else if (pc < pv) { casa.D++; vis.V++; }
                else              { casa.E++; vis.E++; }

                agregado[j.TimeCasaId] = casa;
                agregado[j.TimeVisitanteId] = vis;
            }

            // Carrega apenas os times que disputaram jogos (não a tabela inteira).
            var idsComJogos = agregado.Keys.ToList();
            var timesById = await _context.Times.AsNoTracking()
                .Where(t => idsComJogos.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            var classificacao = agregado
                .Where(kv => timesById.ContainsKey(kv.Key))
                .Select(kv => new ClassificacaoResumo
                {
                    Time = timesById[kv.Key],
                    Vitorias = kv.Value.V,
                    Empates = kv.Value.E,
                    Derrotas = kv.Value.D,
                    GolsPro = kv.Value.GP,
                    GolsContra = kv.Value.GC,
                    Pontos = kv.Value.V * 3 + kv.Value.E,
                    Jogos = kv.Value.V + kv.Value.E + kv.Value.D
                })
                .OrderByDescending(c => c.Pontos)
                .ThenByDescending(c => c.SaldoGols)
                .ThenByDescending(c => c.GolsPro)
                .Take(8)
                .ToList();

            for (int i = 0; i < classificacao.Count; i++)
                classificacao[i].Posicao = i + 1;

            var vm = new HomeViewModel
            {
                JogosRecentes = jogosRecentes,
                Classificacao = classificacao,
                TotalTimes = await _context.Times.CountAsync(),
                TotalJogadores = await _context.Jogadores.CountAsync(),
                TotalJogos = await _context.Jogos.CountAsync(),
                TotalCompeticoes = await _context.Competicoes.CountAsync(),
                CompeticoesHomeSelecionadas = competicoesHome.Count
            };

            return View(vm);
        }

        // Projeção mínima usada só para agregar a classificação da home.
        private sealed record JogoResumo(int TimeCasaId, int TimeVisitanteId, int? PlacarCasa, int? PlacarVisitante);

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
