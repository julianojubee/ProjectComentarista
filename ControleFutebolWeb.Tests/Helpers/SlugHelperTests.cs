using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // O slug é a URL pública do post (/blog/{slug}) — precisa sair limpo de
    // qualquer título em português e nunca colidir com outro post.
    public class SlugHelperTests
    {
        [Theory]
        [InlineData("Análise: Flamengo x Palmeiras!", "analise-flamengo-x-palmeiras")]
        [InlineData("São Paulo é campeão", "sao-paulo-e-campeao")]
        [InlineData("  Espaços   em  excesso  ", "espacos-em-excesso")]
        [InlineData("100% aproveitamento no returno", "100-aproveitamento-no-returno")]
        [InlineData("UPPERCASE vira minúsculas", "uppercase-vira-minusculas")]
        [InlineData("çãõéêíóú", "caoeeiou")]
        public void Gerar_NormalizaTitulos(string titulo, string esperado)
        {
            Assert.Equal(esperado, SlugHelper.Gerar(titulo));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("!!!???")] // só pontuação → não pode gerar slug vazio
        public void Gerar_EntradaSemLetras_UsaFallback(string titulo)
        {
            Assert.Equal("post", SlugHelper.Gerar(titulo));
        }

        [Fact]
        public void Gerar_TituloLongo_RespeitaLimiteSemHifenSolto()
        {
            var titulo = string.Join(" ", Enumerable.Repeat("palavra", 100));
            var slug = SlugHelper.Gerar(titulo);
            Assert.True(slug.Length <= 200);
            Assert.False(slug.EndsWith('-'));
        }

        [Fact]
        public async Task GerarUnico_ComColisao_AdicionaSufixoNumerico()
        {
            var existentes = new HashSet<string> { "analise-do-jogo", "analise-do-jogo-2" };
            var slug = await SlugHelper.GerarUnicoAsync("Análise do jogo",
                s => Task.FromResult(existentes.Contains(s)));
            Assert.Equal("analise-do-jogo-3", slug);
        }

        [Fact]
        public async Task GerarUnico_SemColisao_MantemSlugBase()
        {
            var slug = await SlugHelper.GerarUnicoAsync("Título inédito",
                _ => Task.FromResult(false));
            Assert.Equal("titulo-inedito", slug);
        }
    }
}
