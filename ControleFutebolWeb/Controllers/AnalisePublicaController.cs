using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Leitura PÚBLICA de uma análise de jogo (/analise/{token}) — a segunda e
    // última área do site acessível sem login, junto do blog.
    //
    // [AllowAnonymous] é obrigatório: o AuthorizeFilter global (Program.cs) exige
    // autenticação em tudo por padrão. O controller também é isento do
    // AssinaturaFilter — o visitante não tem conta, e a análise já foi paga por
    // quem gerou o link.
    //
    // REGRA DE OURO: este controller NÃO TEM AÇÃO DE ESCRITA e nunca pode ganhar
    // uma. A única coisa que o visitante pode fazer é ler. Todo dado sai
    // filtrado pelo par (JogoId, UsuarioId) que veio do token — nunca por
    // parâmetro da URL, senão o link viraria uma porta para qualquer jogo.
    [AllowAnonymous]
    [Route("analise")]
    public class AnalisePublicaController : Controller
    {
        private readonly FutebolContext _context;
        private readonly PainelJogoService _paineis;

        public AnalisePublicaController(FutebolContext context, PainelJogoService paineis)
        {
            _context = context;
            _paineis = paineis;
        }

        private string? ImagemUrl(string url) => Url.Action("Imagem", "MediaProxy", new { url });

        // Resolve o token em (jogo, dono). Link revogado, expirado ou inexistente
        // devolve null — e o chamador responde 404, nunca 403: não confirma para
        // um curioso que aquele token um dia existiu.
        private async Task<AnaliseCompartilhada?> BuscarLinkAsync(string? token)
        {
            // Token é base64url de 24 bytes (32 chars). O teto evita mandar para o
            // banco string gigante vinda de varredura de URL.
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;

            var link = await _context.AnalisesCompartilhadas
                .AsNoTracking()
                .Include(a => a.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(a => a.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(a => a.Usuario)
                .FirstOrDefaultAsync(a => a.Token == token);

            return link != null && link.EstaAtivo(DateTime.UtcNow) ? link : null;
        }

        // ?tv=1|16x9|h → apresentação 16:9; ?tv=9x16|v → 9:16 (Shorts).
        // Qualquer outro valor cai fora: modo desligado, página normal.
        private static string? NormalizarModoTv(string? tv) => tv switch
        {
            "1" or "16x9" or "h" => "h",
            "9x16" or "v" => "v",
            _ => null
        };

        // ?tab=... só aceita as quatro abas do painel — o valor entra no HTML e
        // vira chamada de JS, então nada de string arbitrária da URL.
        private static string NormalizarAba(string? tab) =>
            tab is "notas" or "campo" or "stats" or "obs" ? tab : "notas";

        // GET /analise/{token} — a página em si (casca; o conteúdo vem por JSON).
        [HttpGet("{token}")]
        public async Task<IActionResult> Index(string token, [FromQuery] string? tv, [FromQuery] string? tab)
        {
            var link = await BuscarLinkAsync(token);
            if (link == null) return NotFound();

            var modoTv = NormalizarModoTv(tv);

            // Contador de acessos: update direto, sem tracking, para não pesar
            // numa página que pode ser aberta por muita gente. O modo
            // apresentação não conta — quem abre é o próprio autor gravando, e
            // isso inflaria a métrica de quantas pessoas viram a análise.
            if (modoTv == null)
            {
                await _context.AnalisesCompartilhadas
                    .Where(a => a.Id == link.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Visualizacoes, a => a.Visualizacoes + 1)
                        .SetProperty(a => a.UltimoAcessoEm, DateTime.UtcNow));
            }

            static string Cor(string? valor, string padrao) =>
                string.IsNullOrWhiteSpace(valor) ? padrao : valor;

            return View(new AnalisePublicaViewModel
            {
                Token = link.Token,
                Confronto = $"{link.Jogo.TimeCasa?.Nome} x {link.Jogo.TimeVisitante?.Nome}",
                AutorNome = link.Usuario?.Nome,
                CorCamisaCasa = Cor(link.Jogo.CorCamisaCasa, "dc3545"),
                CorNumeroCasa = Cor(link.Jogo.CorNumeroCasa, "ffffff"),
                CorCamisaVisitante = Cor(link.Jogo.CorCamisaVisitante, "0d6efd"),
                CorNumeroVisitante = Cor(link.Jogo.CorNumeroVisitante, "ffffff"),
                ModoTv = modoTv,
                AbaInicial = NormalizarAba(tab)
            });
        }

        // GET /analise/{token}/dados — o pós-jogo completo (placar, notas, campo,
        // estatísticas, observações) do dono do link.
        [HttpGet("{token}/dados")]
        public async Task<IActionResult> Dados(string token)
        {
            var link = await BuscarLinkAsync(token);
            if (link == null) return NotFound();

            var dados = await _paineis.PosJogoAsync(link.JogoId, link.UsuarioId, ImagemUrl);
            return dados == null ? NotFound() : Json(dados);
        }

        // GET /analise/{token}/prejogo — resumo pré-jogo dos dois times.
        // Só dados do banco local: o H2H (que consome a API externa paga) NÃO é
        // exposto publicamente de propósito.
        [HttpGet("{token}/prejogo")]
        public async Task<IActionResult> PreJogo(string token)
        {
            var link = await BuscarLinkAsync(token);
            if (link == null) return NotFound();

            var dados = await _paineis.PreJogoAsync(link.JogoId, ImagemUrl);
            return dados == null ? NotFound() : Json(dados);
        }
    }
}
