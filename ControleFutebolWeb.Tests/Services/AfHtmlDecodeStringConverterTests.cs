using System.Text.Json;
using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Tests.Services
{
    public class AfHtmlDecodeStringConverterTests
    {
        private record Jogador(string? Name, string? Lastname);

        private static readonly JsonSerializerOptions Opcoes = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new AfHtmlDecodeStringConverter() },
        };

        [Theory]
        [InlineData("N. O&apos;Reilly", "N. O'Reilly")]
        [InlineData("Stadio Renato Dall&apos;Ara", "Stadio Renato Dall'Ara")]
        [InlineData("Brighton &amp; Hove", "Brighton & Hove")]
        [InlineData("Guéhi", "Guéhi")]
        [InlineData("O'Brien", "O'Brien")]
        public void DecodificaEntidadesHtml(string daApi, string esperado)
        {
            var json = JsonSerializer.Serialize(new { name = daApi });
            var j = JsonSerializer.Deserialize<Jogador>(json, Opcoes)!;
            Assert.Equal(esperado, j.Name);
        }

        [Fact]
        public void NullContinuaNull()
        {
            var j = JsonSerializer.Deserialize<Jogador>("""{"name":null,"lastname":"O&apos;Nien"}""", Opcoes)!;
            Assert.Null(j.Name);
            Assert.Equal("O'Nien", j.Lastname);
        }
    }
}
