using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Jogadores criados pelo usuário (o amigo da pelada, o atleta da liga amadora).
    // Em liga amadora é dado de pessoa real: só o nome é obrigatório.
    [Authorize]
    public class JogadoresPropriosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public JogadoresPropriosController(FutebolContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        private string Uid => _userManager.GetUserId(User)!;

        public async Task<IActionResult> Index()
        {
            var jogadores = await _context.JogadoresProprios
                .Where(j => j.UsuarioId == Uid)
                .Include(j => j.Nacionalidade)
                .OrderBy(j => j.Nome)
                .AsNoTracking()
                .ToListAsync();
            return View(jogadores);
        }

        public async Task<IActionResult> Criar(int? timeProprioId)
        {
            ViewBag.TimeProprioId = timeProprioId;
            await CarregarNacionalidadesAsync(null);
            return View("Form", new JogadorProprioInput());
        }

        public async Task<IActionResult> Editar(int id)
        {
            var j = await _context.JogadoresProprios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == Uid);
            if (j == null) return NotFound();
            await CarregarNacionalidadesAsync(j.NacionalidadeId);
            return View("Form", new JogadorProprioInput
            {
                Id = j.Id, Nome = j.Nome, Apelido = j.Apelido, Posicao = j.Posicao, NumeroCamisa = j.NumeroCamisa,
                DataNascimento = j.DataNascimento, FotoUrl = j.FotoUrl, NacionalidadeId = j.NacionalidadeId
            });
        }

        // timeProprioId: veio do "criar e já pôr no elenco" da tela do time.
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(ImagemUploadHelper.TamanhoMaximoBytes + 1024 * 1024)]
        public async Task<IActionResult> Salvar(JogadorProprioInput input, int? timeProprioId, IFormFile? foto, bool removerFoto)
        {
            string? novaFoto = null;
            if (foto is { Length: > 0 })
            {
                var (ok, erro, url) = await ImagemUploadHelper.SalvarAsync(foto, _env.WebRootPath, "campeonatos");
                if (ok) novaFoto = url;
                else ModelState.AddModelError("foto", erro!);
            }
            if (!ModelState.IsValid)
            {
                ViewBag.TimeProprioId = timeProprioId;
                await CarregarNacionalidadesAsync(input.NacionalidadeId);
                return View("Form", input);
            }

            JogadorProprio? jogador;
            if (input.Id == 0)
            {
                jogador = new JogadorProprio { UsuarioId = Uid, CriadoEm = DateTime.UtcNow };
                _context.JogadoresProprios.Add(jogador);
            }
            else
            {
                jogador = await _context.JogadoresProprios.FirstOrDefaultAsync(x => x.Id == input.Id && x.UsuarioId == Uid);
                if (jogador == null) return NotFound();
            }

            jogador.Nome = input.Nome.Trim();
            jogador.Apelido = string.IsNullOrWhiteSpace(input.Apelido) ? null : input.Apelido.Trim();
            jogador.Posicao = string.IsNullOrWhiteSpace(input.Posicao) ? null : input.Posicao.Trim();
            jogador.NumeroCamisa = input.NumeroCamisa;
            // Data pura ancorada ao meio-dia UTC (convenção do projeto): não vira o dia
            // anterior ao ser exibida no fuso do Brasil.
            jogador.DataNascimento = input.DataNascimento is DateTime d
                ? DateTime.SpecifyKind(d.Date.AddHours(12), DateTimeKind.Utc)
                : null;
            if (novaFoto != null) jogador.FotoUrl = novaFoto;
            else if (removerFoto) jogador.FotoUrl = null;
            jogador.NacionalidadeId = input.NacionalidadeId;

            if (input.Id == 0 && timeProprioId != null
                && await _context.TimesProprios.AnyAsync(t => t.Id == timeProprioId && t.UsuarioId == Uid))
            {
                var ordem = await _context.ItensElenco.CountAsync(e => e.TimeProprioId == timeProprioId);
                _context.ItensElenco.Add(new ElencoItem
                {
                    TimeProprioId = timeProprioId.Value,
                    JogadorProprio = jogador,
                    NomeSnapshot = jogador.NomeExibicao,
                    FotoUrlSnapshot = jogador.FotoUrl,
                    Posicao = jogador.Posicao,
                    NumeroCamisa = jogador.NumeroCamisa,
                    Ordem = ordem + 1,
                    IncluidoEm = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return timeProprioId != null
                ? RedirectToAction("Elenco", "TimesProprios", new { id = timeProprioId })
                : RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var jogador = await _context.JogadoresProprios.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == Uid);
            if (jogador == null) return NotFound();

            // Sai dos elencos (cascade); os gols dele nas súmulas ficam pelo nome.
            _context.JogadoresProprios.Remove(jogador);
            await _context.SaveChangesAsync();
            TempData["Mensagem"] = $"{jogador.Nome} excluído.";
            return RedirectToAction(nameof(Index));
        }

        private async Task CarregarNacionalidadesAsync(int? selecionada)
        {
            var nacionalidades = await _context.Nacionalidades.OrderBy(n => n.Nome).AsNoTracking().ToListAsync();
            ViewBag.Nacionalidades = new SelectList(nacionalidades, "Id", "Nome", selecionada);
        }
    }
}
