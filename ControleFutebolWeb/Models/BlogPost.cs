using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models
{
    // Status editorial do post. Gravado como string no banco (legível em queries
    // manuais). AGENDADO/ARQUIVADO já existem no enum para a fase 2, mas a UI da
    // fase 1 só usa Rascunho e Publicado.
    public enum BlogPostStatus
    {
        Rascunho,
        Agendado,
        Publicado,
        Arquivado
    }

    // Post do blog público (/blog). O Markdown é a fonte da verdade; ConteudoHtml
    // guarda o HTML já renderizado (Markdig) e sanitizado (HtmlSanitizer) no save,
    // para a view pública só imprimir sem processar nada por request.
    public class BlogPost
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Titulo { get; set; } = "";

        // Gerado do título no primeiro save (SlugHelper); imutável após publicado
        // para não quebrar links externos. Índice único.
        [Required, StringLength(200)]
        public string Slug { get; set; } = "";

        // Chamada do card na listagem e meta description padrão.
        [StringLength(300)]
        public string Resumo { get; set; } = "";

        public string ConteudoMarkdown { get; set; } = "";
        public string ConteudoHtml { get; set; } = "";

        // Fase 1: URL externa colada pelo autor (upload de arquivo é fase 2).
        [StringLength(500)]
        public string? ImagemCapaUrl { get; set; }

        [StringLength(200)]
        public string? ImagemCapaAlt { get; set; }

        public BlogPostStatus Status { get; set; } = BlogPostStatus.Rascunho;

        // Definida ao publicar; posts só aparecem no público com
        // Status == Publicado && PublicadoEm <= agora.
        public DateTime? PublicadoEm { get; set; }
        public DateTime? AtualizadoEm { get; set; }
        public DateTime CriadoEm { get; set; }

        public string AutorId { get; set; } = "";
        public ApplicationUser Autor { get; set; } = null!;

        public int? CategoriaId { get; set; }
        public BlogCategoria? Categoria { get; set; }

        // Calculado no save: palavras do Markdown / 200 (mínimo 1).
        public int TempoLeituraMin { get; set; } = 1;

        public int Visualizacoes { get; set; }

        // Overrides de SEO (fase 3 usa; colunas já criadas para evitar migration).
        [StringLength(70)]
        public string? MetaTitulo { get; set; }

        [StringLength(160)]
        public string? MetaDescricao { get; set; }

        // Soft delete: post excluído some de todas as listagens mas fica no banco.
        public DateTime? ExcluidoEm { get; set; }

        // Vínculos opcionais com o domínio do site (exibição nas telas de
        // jogo/time/jogador é fase futura; colunas prontas desde já).
        public int? JogoId { get; set; }
        public Jogo? Jogo { get; set; }
        public int? TimeId { get; set; }
        public Time? Time { get; set; }
        public int? JogadorId { get; set; }
        public Jogador? Jogador { get; set; }

        public ICollection<BlogPostTag> Tags { get; set; } = new List<BlogPostTag>();
    }
}
