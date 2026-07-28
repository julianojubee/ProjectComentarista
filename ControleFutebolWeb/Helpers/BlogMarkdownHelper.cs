using Ganss.Xss;
using Markdig;

namespace ControleFutebolWeb.Helpers
{
    // Renderiza o Markdown dos posts do blog para HTML e sanitiza o resultado.
    // SEMPRE no servidor e SEMPRE no save (o HTML pronto fica em BlogPost.ConteudoHtml);
    // a view pública só imprime com @Html.Raw, sem processar nada por request.
    public static class BlogMarkdownHelper
    {
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()   // tabelas, listas de tarefas, auto-links etc.
            .DisableHtml()             // HTML cru no Markdown vira texto escapado
            .Build();

        private static readonly HtmlSanitizer _sanitizer = CriarSanitizer();

        private static HtmlSanitizer CriarSanitizer()
        {
            // Defense in depth: mesmo com DisableHtml() no Markdig, o HTML gerado
            // passa pela allowlist do sanitizer antes de ir ao banco.
            var s = new HtmlSanitizer();
            // Permite âncoras de heading e classes de code fence (```csharp).
            s.AllowedAttributes.Add("id");
            s.AllowedAttributes.Add("class");
            // Links externos não herdam a reputação do site nem ganham window.opener.
            s.PostProcessNode += (_, e) =>
            {
                if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement a &&
                    !string.IsNullOrEmpty(a.Href) &&
                    a.Href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    a.SetAttribute("rel", "noopener noreferrer");
                    a.SetAttribute("target", "_blank");
                }
            };
            return s;
        }

        public static string RenderizarHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";
            var html = Markdown.ToHtml(markdown, _pipeline);
            return _sanitizer.Sanitize(html);
        }

        // Tempo de leitura estimado: ~200 palavras/min, mínimo 1.
        public static int CalcularTempoLeituraMin(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return 1;
            var palavras = markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(1, (int)Math.Round(palavras / 200.0));
        }
    }
}
