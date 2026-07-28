using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Aceita https://… (imagem hospedada fora) ou /caminho/relativo (imagem do
    // próprio site, incluindo as enviadas pelo upload do editor). Recusa http://
    // porque a CSP do site (img-src … https:) bloquearia a imagem no navegador —
    // melhor avisar no formulário do que deixar o post com imagem invisível.
    public class ImagemUrlValidaAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            var url = value as string;
            if (string.IsNullOrWhiteSpace(url)) return true; // campo opcional

            url = url.Trim();
            if (url.StartsWith('/') && !url.StartsWith("//")) return true;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttps;
        }

        public override string FormatErrorMessage(string name) =>
            "Informe uma URL começando com https:// ou um caminho do próprio site (/uploads/...).";
    }

    // Card da listagem pública — projeção sem o conteúdo do post.
    public class BlogPostCardViewModel
    {
        public string Titulo { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Resumo { get; set; } = "";
        public string? ImagemCapaUrl { get; set; }
        public string? ImagemCapaAlt { get; set; }
        public DateTime PublicadoEm { get; set; }
        public string AutorNome { get; set; } = "";
        public int TempoLeituraMin { get; set; }
    }

    public class BlogIndexViewModel
    {
        public List<BlogPostCardViewModel> Posts { get; set; } = new();
        public int PaginaAtual { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
    }

    // Formulário do editor (/blog/admin/novo e /editar).
    public class BlogPostEditarViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Informe o título."), StringLength(200)]
        public string Titulo { get; set; } = "";

        [StringLength(300, ErrorMessage = "O resumo pode ter no máximo 300 caracteres.")]
        public string Resumo { get; set; } = "";

        public string ConteudoMarkdown { get; set; } = "";

        // Aceita URL absoluta (https://...) OU caminho relativo do próprio site
        // (/uploads/blog/...), que é o formato devolvido pelo upload — por isso
        // não usa [Url], que rejeitaria o caminho relativo.
        [StringLength(500)]
        [ImagemUrlValida]
        public string? ImagemCapaUrl { get; set; }

        [StringLength(200)]
        public string? ImagemCapaAlt { get; set; }

        // Só leitura no form (o slug é gerado do título e imutável após publicado).
        public string? Slug { get; set; }
        public BlogPostStatus Status { get; set; } = BlogPostStatus.Rascunho;
    }

    // Linha da lista do painel do autor.
    public class BlogAdminListaItemViewModel
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string Slug { get; set; } = "";
        public BlogPostStatus Status { get; set; }
        public DateTime? PublicadoEm { get; set; }
        public DateTime CriadoEm { get; set; }
        public string AutorNome { get; set; } = "";
        public int Visualizacoes { get; set; }
    }
}
