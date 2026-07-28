using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Leitura PÚBLICA do blog — a única área do site acessível sem login.
    // [AllowAnonymous] é obrigatório aqui: o AuthorizeFilter global (Program.cs)
    // exige autenticação em tudo por padrão. Este controller também é isento do
    // AssinaturaFilter (usuário inadimplente pode ler como qualquer visitante).
    //
    // REGRA DE OURO: toda query passa por PostsPublicados() — nunca montar o
    // filtro de publicação na mão em uma ação (risco de vazar rascunho).
    [AllowAnonymous]
    [Route("blog")]
    public class BlogController : Controller
    {
        private const int PostsPorPagina = 12;

        private readonly FutebolContext _context;

        public BlogController(FutebolContext context)
        {
            _context = context;
        }

        // Filtro central de visibilidade pública: publicado, com data no passado
        // e não excluído (soft delete).
        private IQueryable<BlogPost> PostsPublicados()
        {
            var agora = DateTime.UtcNow;
            return _context.BlogPosts.AsNoTracking()
                .Where(p => p.Status == BlogPostStatus.Publicado
                         && p.PublicadoEm != null && p.PublicadoEm <= agora
                         && p.ExcluidoEm == null);
        }

        // GET /blog e /blog/pagina/2
        [HttpGet("")]
        [HttpGet("pagina/{pagina:int:min(1)}")]
        public async Task<IActionResult> Index(int pagina = 1)
        {
            var query = PostsPublicados().OrderByDescending(p => p.PublicadoEm);

            var total = await query.CountAsync();
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)PostsPorPagina));
            if (pagina > totalPaginas) return NotFound();

            // Projeção do card: nunca carrega ConteudoMarkdown/ConteudoHtml na listagem.
            var posts = await query
                .Skip((pagina - 1) * PostsPorPagina)
                .Take(PostsPorPagina)
                .Select(p => new BlogPostCardViewModel
                {
                    Titulo = p.Titulo,
                    Slug = p.Slug,
                    Resumo = p.Resumo,
                    ImagemCapaUrl = p.ImagemCapaUrl,
                    ImagemCapaAlt = p.ImagemCapaAlt,
                    PublicadoEm = p.PublicadoEm!.Value,
                    AutorNome = p.Autor.Nome,
                    TempoLeituraMin = p.TempoLeituraMin
                })
                .ToListAsync();

            var vm = new BlogIndexViewModel
            {
                Posts = posts,
                PaginaAtual = pagina,
                TotalPaginas = totalPaginas
            };
            return View(vm);
        }

        // GET /blog/{slug}
        // Post não publicado devolve 404 (não 403): não revela que o rascunho existe.
        [HttpGet("{slug}")]
        public async Task<IActionResult> Post(string slug)
        {
            var post = await PostsPublicados()
                .Include(p => p.Autor)
                .FirstOrDefaultAsync(p => p.Slug == slug);
            if (post == null) return NotFound();

            // Contador simples de views (fire-and-forget dentro do request; sem
            // tracking do post carregado acima).
            await _context.BlogPosts
                .Where(p => p.Id == post.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Visualizacoes, p => p.Visualizacoes + 1));

            return View(post);
        }
    }
}
