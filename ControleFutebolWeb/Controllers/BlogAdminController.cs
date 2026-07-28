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
    // Área de ESCRITA do blog (/blog/admin) — separada de propósito do
    // BlogController público: aqui tudo exige a política "BlogEscrever"
    // (admin ou flag EhAutorBlog). Autor comum só enxerga/edita os próprios
    // posts; admin enxerga/edita todos.
    [Authorize(Policy = "BlogEscrever")]
    [Route("blog/admin")]
    public class BlogAdminController : Controller
    {
        // Slugs que colidiriam com rotas do BlogController/BlogAdminController.
        private static readonly string[] SlugsReservados = { "admin", "pagina", "categoria", "tag", "busca", "feed", "sitemap", "preview" };

        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BlogAdminController> _logger;

        public BlogAdminController(
            FutebolContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            ILogger<BlogAdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _logger = logger;
        }

        private async Task<bool> EhAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.IsAdmin == true;
        }

        // Posts visíveis no painel: os do próprio autor, ou todos se admin.
        private async Task<IQueryable<BlogPost>> PostsDoUsuarioAsync()
        {
            var query = _context.BlogPosts.Where(p => p.ExcluidoEm == null);
            if (!await EhAdminAsync())
            {
                var uid = _userManager.GetUserId(User);
                query = query.Where(p => p.AutorId == uid);
            }
            return query;
        }

        // GET /blog/admin
        [HttpGet("")]
        public async Task<IActionResult> Index(string? status)
        {
            var query = await PostsDoUsuarioAsync();

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<BlogPostStatus>(status, ignoreCase: true, out var st))
                query = query.Where(p => p.Status == st);

            var posts = await query
                .OrderByDescending(p => p.PublicadoEm ?? p.CriadoEm)
                .Select(p => new BlogAdminListaItemViewModel
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    Slug = p.Slug,
                    Status = p.Status,
                    PublicadoEm = p.PublicadoEm,
                    CriadoEm = p.CriadoEm,
                    AutorNome = p.Autor.Nome,
                    Visualizacoes = p.Visualizacoes
                })
                .ToListAsync();

            ViewBag.FiltroStatus = status;
            return View(posts);
        }

        // GET /blog/admin/novo
        [HttpGet("novo")]
        public IActionResult Novo() => View("Editar", new BlogPostEditarViewModel());

        // GET /blog/admin/editar/5
        [HttpGet("editar/{id:int}")]
        public async Task<IActionResult> Editar(int id)
        {
            var post = await (await PostsDoUsuarioAsync()).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            return View(new BlogPostEditarViewModel
            {
                Id = post.Id,
                Titulo = post.Titulo,
                Resumo = post.Resumo,
                ConteudoMarkdown = post.ConteudoMarkdown,
                ImagemCapaUrl = post.ImagemCapaUrl,
                ImagemCapaAlt = post.ImagemCapaAlt,
                Slug = post.Slug,
                Status = post.Status
            });
        }

        // POST /blog/admin/salvar — cria ou atualiza (rascunho ou publicado).
        // O antiforgery global (AutoValidateAntiforgeryToken) cobre este POST.
        [HttpPost("salvar")]
        public async Task<IActionResult> Salvar(BlogPostEditarViewModel model)
        {
            if (!ModelState.IsValid) return View("Editar", model);

            BlogPost post;
            if (model.Id.HasValue)
            {
                var existente = await (await PostsDoUsuarioAsync())
                    .FirstOrDefaultAsync(p => p.Id == model.Id.Value);
                if (existente == null) return NotFound();
                post = existente;
                post.AtualizadoEm = DateTime.UtcNow;
            }
            else
            {
                post = new BlogPost
                {
                    AutorId = _userManager.GetUserId(User)!,
                    CriadoEm = DateTime.UtcNow
                };
                _context.BlogPosts.Add(post);
            }

            post.Titulo = model.Titulo.Trim();
            post.Resumo = model.Resumo?.Trim() ?? "";
            post.ConteudoMarkdown = model.ConteudoMarkdown ?? "";
            post.ImagemCapaUrl = string.IsNullOrWhiteSpace(model.ImagemCapaUrl) ? null : model.ImagemCapaUrl.Trim();
            post.ImagemCapaAlt = string.IsNullOrWhiteSpace(model.ImagemCapaAlt) ? null : model.ImagemCapaAlt.Trim();

            // Markdown → HTML sanitizado, gravado pronto para a view pública.
            post.ConteudoHtml = BlogMarkdownHelper.RenderizarHtml(post.ConteudoMarkdown);
            post.TempoLeituraMin = BlogMarkdownHelper.CalcularTempoLeituraMin(post.ConteudoMarkdown);

            // Slug: gerado no primeiro save; regerado ao renomear SÓ enquanto
            // rascunho (após publicado é imutável para não quebrar links).
            if (post.Id == 0 || post.Status == BlogPostStatus.Rascunho)
            {
                post.Slug = await SlugHelper.GerarUnicoAsync(post.Titulo, async s =>
                    SlugsReservados.Contains(s) ||
                    await _context.BlogPosts.AnyAsync(p => p.Slug == s && p.Id != post.Id));
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Post salvo.";
            return RedirectToAction(nameof(Editar), new { id = post.Id });
        }

        // POST /blog/admin/publicar/5
        [HttpPost("publicar/{id:int}")]
        public async Task<IActionResult> Publicar(int id)
        {
            var post = await (await PostsDoUsuarioAsync()).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            post.Status = BlogPostStatus.Publicado;
            // Mantém a data original em republicações (despublicar → publicar).
            post.PublicadoEm ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Post publicado em /blog/{post.Slug}.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        // POST /blog/admin/despublicar/5 — volta para rascunho (some do público).
        [HttpPost("despublicar/{id:int}")]
        public async Task<IActionResult> Despublicar(int id)
        {
            var post = await (await PostsDoUsuarioAsync()).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            post.Status = BlogPostStatus.Rascunho;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Post voltou para rascunho.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        // POST /blog/admin/excluir/5 — soft delete.
        [HttpPost("excluir/{id:int}")]
        public async Task<IActionResult> Excluir(int id)
        {
            var post = await (await PostsDoUsuarioAsync()).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            post.ExcluidoEm = DateTime.UtcNow;
            post.Status = BlogPostStatus.Arquivado;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Post excluído.";
            return RedirectToAction(nameof(Index));
        }

        // POST /blog/admin/imagem — upload de imagem do editor.
        // Devolve JSON { url } para o JS inserir o Markdown no texto.
        // O RequestSizeLimit corta a requisição no servidor antes de bufferizar
        // o arquivo inteiro; a checagem de tamanho no helper cobre o resto.
        [HttpPost("imagem")]
        [RequestSizeLimit(ImagemUploadHelper.TamanhoMaximoBytes + 1024 * 1024)]
        public async Task<IActionResult> Imagem(IFormFile? arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { erro = "Nenhum arquivo enviado." });

            // Lê só o cabeçalho para identificar o formato pelo conteúdo real.
            var cabecalho = new byte[ImagemUploadHelper.BytesCabecalho];
            await using (var leitura = arquivo.OpenReadStream())
                await leitura.ReadExactlyAsync(cabecalho.AsMemory(0, Math.Min(cabecalho.Length, (int)arquivo.Length)));

            var (ok, erro, extensao) = ImagemUploadHelper.Validar(cabecalho, arquivo.Length);
            if (!ok)
                return BadRequest(new { erro });

            var caminhoRelativo = ImagemUploadHelper.GerarCaminhoRelativo(extensao!, DateTime.UtcNow);
            var caminhoFisico = Path.Combine(_env.WebRootPath, caminhoRelativo.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(caminhoFisico)!);

            await using (var destino = System.IO.File.Create(caminhoFisico))
                await arquivo.CopyToAsync(destino);

            _logger.LogInformation("[Blog] Imagem enviada por {Usuario}: {Caminho} ({Bytes} bytes).",
                User.Identity?.Name, caminhoRelativo, arquivo.Length);

            return Json(new { url = "/" + caminhoRelativo });
        }

        // POST /blog/admin/preview-md — preview server-side do editor (a CSP do
        // site proíbe libs de CDN; o preview renderiza no servidor com o MESMO
        // pipeline usado no save, então o que se vê é o que será publicado).
        [HttpPost("preview-md")]
        public IActionResult PreviewMarkdown([FromForm] string? markdown)
        {
            return Content(BlogMarkdownHelper.RenderizarHtml(markdown ?? ""), "text/html");
        }
    }
}
