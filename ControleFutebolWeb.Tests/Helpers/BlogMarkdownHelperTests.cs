using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // O HTML gerado aqui vai direto para @Html.Raw na página pública do blog —
    // estes testes são a garantia de que nenhum script sobrevive ao pipeline.
    public class BlogMarkdownHelperTests
    {
        [Fact]
        public void Renderizar_MarkdownBasico_GeraHtml()
        {
            var html = BlogMarkdownHelper.RenderizarHtml("# Título\n\nTexto **negrito** e *itálico*.");
            Assert.Contains("Título</h1>", html);
            Assert.Contains("<strong>negrito</strong>", html);
            Assert.Contains("<em>itálico</em>", html);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("Texto <img src=x onerror=alert(1)> aqui")]
        [InlineData("<iframe src='https://mal.com'></iframe>")]
        [InlineData("<div onclick=\"roubar()\">clique</div>")]
        public void Renderizar_HtmlMalicioso_NaoVeraTagAtiva(string markdown)
        {
            // DisableHtml() escapa HTML cru para texto (&lt;script&gt; vira literal
            // exibido, não executável) — o que NÃO pode existir no resultado é a
            // tag/atributo em forma ativa, começando com '<' de verdade.
            var html = BlogMarkdownHelper.RenderizarHtml(markdown);
            Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<div", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Renderizar_LinkJavascript_TemHrefNeutralizado()
        {
            var html = BlogMarkdownHelper.RenderizarHtml("[clique](javascript:alert(1))");
            Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Renderizar_LinkExterno_GanhaNoopener()
        {
            var html = BlogMarkdownHelper.RenderizarHtml("[site](https://exemplo.com)");
            Assert.Contains("noopener", html);
            Assert.Contains("target=\"_blank\"", html);
        }

        [Fact]
        public void Renderizar_Vazio_RetornaVazio()
        {
            Assert.Equal("", BlogMarkdownHelper.RenderizarHtml(""));
            Assert.Equal("", BlogMarkdownHelper.RenderizarHtml("   "));
        }

        [Theory]
        [InlineData("", 1)]                 // vazio → mínimo 1
        [InlineData("uma frase curta", 1)]  // pouco texto → 1 min
        public void TempoLeitura_MinimoUmMinuto(string markdown, int esperado)
        {
            Assert.Equal(esperado, BlogMarkdownHelper.CalcularTempoLeituraMin(markdown));
        }

        [Fact]
        public void TempoLeitura_600Palavras_TresMinutos()
        {
            var texto = string.Join(" ", Enumerable.Repeat("palavra", 600));
            Assert.Equal(3, BlogMarkdownHelper.CalcularTempoLeituraMin(texto));
        }
    }
}
