using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Times criados pelo usuário para os campeonatos próprios, e o elenco de cada
    // um (jogadores reais do sistema e/ou jogadores criados pelo usuário).
    [Authorize]
    public class TimesPropriosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public TimesPropriosController(FutebolContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        private string Uid => _userManager.GetUserId(User)!;

        private Task<TimeProprio?> DoUsuarioAsync(int id) =>
            _context.TimesProprios.FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == Uid);

        public async Task<IActionResult> Index(bool arquivados = false)
        {
            ViewBag.Arquivados = arquivados;
            var times = await _context.TimesProprios
                .Where(t => t.UsuarioId == Uid && (t.ArquivadoEm != null) == arquivados)
                .Include(t => t.Elenco)
                .OrderBy(t => t.Nome)
                .AsNoTracking()
                .ToListAsync();
            return View(times);
        }

        public IActionResult Criar() => View("Form", new TimeProprioInput());

        public async Task<IActionResult> Editar(int id)
        {
            var t = await DoUsuarioAsync(id);
            if (t == null) return NotFound();
            return View("Form", new TimeProprioInput
            {
                Id = t.Id, Nome = t.Nome, Sigla = t.Sigla, Cidade = t.Cidade,
                EscudoUrl = t.EscudoUrl, CorPrincipal = t.CorPrincipal, CorSecundaria = t.CorSecundaria
            });
        }

        // Escudo por upload, e não por URL digitada: o MediaProxy só busca hosts da
        // lista dele, então um endereço qualquer nunca carregaria nas tabelas.
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(ImagemUploadHelper.TamanhoMaximoBytes + 1024 * 1024)]
        public async Task<IActionResult> Salvar(TimeProprioInput input, IFormFile? escudo, bool removerEscudo)
        {
            string? novoEscudo = null;
            if (escudo is { Length: > 0 })
            {
                var (ok, erro, url) = await ImagemUploadHelper.SalvarAsync(escudo, _env.WebRootPath, "campeonatos");
                if (ok) novoEscudo = url;
                else ModelState.AddModelError("escudo", erro!);
            }
            if (!ModelState.IsValid) return View("Form", input);

            TimeProprio? time;
            if (input.Id == 0)
            {
                time = new TimeProprio { UsuarioId = Uid, CriadoEm = DateTime.UtcNow };
                _context.TimesProprios.Add(time);
            }
            else
            {
                time = await DoUsuarioAsync(input.Id);
                if (time == null) return NotFound();
            }

            time.Nome = input.Nome.Trim();
            time.Sigla = string.IsNullOrWhiteSpace(input.Sigla) ? null : input.Sigla.Trim().ToUpperInvariant();
            time.Cidade = string.IsNullOrWhiteSpace(input.Cidade) ? null : input.Cidade.Trim();
            if (novoEscudo != null) time.EscudoUrl = novoEscudo;
            else if (removerEscudo) time.EscudoUrl = null;
            time.CorPrincipal = input.CorPrincipal;
            time.CorSecundaria = input.CorSecundaria;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Elenco), new { id = time.Id });
        }

        // Arquivar, e não excluir: o time pode ter disputado campeonatos, e sumir com
        // ele tiraria o elenco das súmulas antigas.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Arquivar(int id, bool desarquivar = false)
        {
            var time = await DoUsuarioAsync(id);
            if (time == null) return NotFound();
            time.ArquivadoEm = desarquivar ? null : DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /TimesProprios/Elenco/5
        public async Task<IActionResult> Elenco(int id)
        {
            var time = await _context.TimesProprios
                .Include(t => t.Elenco)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == Uid);
            if (time == null) return NotFound();

            var noElenco = time.Elenco.Where(e => e.JogadorProprioId != null).Select(e => e.JogadorProprioId).ToHashSet();
            ViewBag.MeusJogadores = await _context.JogadoresProprios
                .Where(j => j.UsuarioId == Uid && !noElenco.Contains(j.Id))
                .OrderBy(j => j.Nome)
                .AsNoTracking()
                .ToListAsync();
            return View(time);
        }

        // GET: /TimesProprios/BuscarJogadores?q=neym — jogadores reais para o elenco.
        [HttpGet]
        public async Task<IActionResult> BuscarJogadores(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3) return Json(Array.Empty<object>());
            var termo = $"%{q.Trim()}%";

            var jogadores = await _context.Jogadores
                .Where(j => EF.Functions.ILike(j.Nome, termo))
                .OrderBy(j => j.Aposentado).ThenBy(j => j.Nome)
                .Take(20)
                .Select(j => new { id = j.Id, nome = j.Nome, posicao = j.Posicao, time = j.Time.Nome, foto = j.FotoUrl })
                .ToListAsync();
            return Json(jogadores);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAoElenco(int id, int? jogadorId, int? jogadorProprioId, int? numeroCamisa)
        {
            var time = await _context.TimesProprios.Include(t => t.Elenco)
                .FirstOrDefaultAsync(t => t.Id == id && t.UsuarioId == Uid);
            if (time == null) return NotFound();

            var item = new ElencoItem { TimeProprioId = id, IncluidoEm = DateTime.UtcNow, Ordem = time.Elenco.Count + 1 };

            if (jogadorId != null)
            {
                if (time.Elenco.Any(e => e.JogadorId == jogadorId)) return RedirectToAction(nameof(Elenco), new { id });
                var j = await _context.Jogadores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jogadorId);
                if (j == null) return NotFound();
                item.JogadorId = j.Id;
                item.NomeSnapshot = j.Nome;
                item.FotoUrlSnapshot = j.FotoUrl;
                item.Posicao = j.Posicao;
                item.NumeroCamisa = numeroCamisa ?? j.NumeroCamisa;
            }
            else if (jogadorProprioId != null)
            {
                if (time.Elenco.Any(e => e.JogadorProprioId == jogadorProprioId)) return RedirectToAction(nameof(Elenco), new { id });
                var j = await _context.JogadoresProprios.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == jogadorProprioId && x.UsuarioId == Uid);
                if (j == null) return NotFound();
                item.JogadorProprioId = j.Id;
                item.NomeSnapshot = j.NomeExibicao;
                item.FotoUrlSnapshot = j.FotoUrl;
                item.Posicao = j.Posicao;
                item.NumeroCamisa = numeroCamisa ?? j.NumeroCamisa;
            }
            else
            {
                return RedirectToAction(nameof(Elenco), new { id });
            }

            time.Elenco.Add(item);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Elenco), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverDoElenco(int id, int itemId)
        {
            var item = await _context.ItensElenco
                .FirstOrDefaultAsync(e => e.Id == itemId && e.TimeProprioId == id && e.TimeProprio.UsuarioId == Uid);
            if (item != null)
            {
                _context.ItensElenco.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Elenco), new { id });
        }
    }
}
