using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Gerência dos links públicos de análise (/analise/{token}) pelo DONO da
    /// análise, a partir da tela /Jogos/Analisar. A leitura pública em si fica em
    /// AnalisePublicaController — aqui tudo exige login.
    ///
    /// Rota "Jogos/[action]/{id?}" segue o padrão dos outros controllers
    /// auxiliares da tela de análise, cujas URLs estão escritas em analisar.js.
    /// </summary>
    [Route("Jogos/[action]/{id?}")]
    public class JogosCompartilhamentoController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogosCompartilhamentoController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 24 bytes = 32 caracteres em base64url. Aleatório criptográfico: o token
        // é a única credencial de quem abre o link.
        private static string GerarToken() =>
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

        private string UrlPublica(string token) =>
            $"{Request.Scheme}://{Request.Host}/analise/{token}";

        // GET: Jogos/LinkAnalise/5 — link ativo do jogo para o usuário atual (se houver).
        [HttpGet]
        public async Task<IActionResult> LinkAnalise(int id)
        {
            var usuarioId = _userManager.GetUserId(User)!;
            var agora = DateTime.UtcNow;

            var link = await _context.AnalisesCompartilhadas
                .AsNoTracking()
                .Where(a => a.JogoId == id && a.UsuarioId == usuarioId
                         && a.RevogadoEm == null && (a.ExpiraEm == null || a.ExpiraEm > agora))
                .OrderByDescending(a => a.CriadoEm)
                .FirstOrDefaultAsync();

            if (link == null) return Json(new { existe = false });

            return Json(new
            {
                existe = true,
                url = UrlPublica(link.Token),
                criadoEm = link.CriadoEm,
                visualizacoes = link.Visualizacoes
            });
        }

        public class CompartilharRequest
        {
            public int JogoId { get; set; }
        }

        // POST: Jogos/CompartilharAnalise — cria (ou reaproveita) o link público.
        // Reaproveitar evita encher a tabela de tokens órfãos a cada clique no
        // botão; para invalidar o link antigo o usuário revoga antes.
        [HttpPost]
        public async Task<IActionResult> CompartilharAnalise([FromBody] CompartilharRequest req)
        {
            if (req == null || req.JogoId <= 0) return BadRequest("Jogo inválido.");

            var usuarioId = _userManager.GetUserId(User)!;
            var agora = DateTime.UtcNow;

            if (!await _context.Jogos.AnyAsync(j => j.Id == req.JogoId))
                return NotFound("Jogo não encontrado.");

            var existente = await _context.AnalisesCompartilhadas
                .Where(a => a.JogoId == req.JogoId && a.UsuarioId == usuarioId
                         && a.RevogadoEm == null && (a.ExpiraEm == null || a.ExpiraEm > agora))
                .OrderByDescending(a => a.CriadoEm)
                .FirstOrDefaultAsync();

            if (existente != null)
                return Json(new { url = UrlPublica(existente.Token), novo = false, visualizacoes = existente.Visualizacoes });

            var link = new AnaliseCompartilhada
            {
                JogoId = req.JogoId,
                UsuarioId = usuarioId,
                Token = GerarToken(),
                CriadoEm = agora
            };
            _context.AnalisesCompartilhadas.Add(link);
            await _context.SaveChangesAsync();

            return Json(new { url = UrlPublica(link.Token), novo = true, visualizacoes = 0 });
        }

        // POST: Jogos/RevogarAnaliseCompartilhada — derruba o link do jogo.
        // Filtra por UsuarioId: ninguém revoga o link de outra pessoa.
        [HttpPost]
        public async Task<IActionResult> RevogarAnaliseCompartilhada([FromBody] CompartilharRequest req)
        {
            if (req == null || req.JogoId <= 0) return BadRequest("Jogo inválido.");

            var usuarioId = _userManager.GetUserId(User)!;

            await _context.AnalisesCompartilhadas
                .Where(a => a.JogoId == req.JogoId && a.UsuarioId == usuarioId && a.RevogadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevogadoEm, DateTime.UtcNow));

            return Json(new { ok = true });
        }
    }
}
