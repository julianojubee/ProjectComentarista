using System.Globalization;
using System.Text;

namespace ControleFutebolWeb.Helpers
{
    // Gera slugs de URL a partir de títulos: minúsculas, sem acento, hífens.
    // Ex.: "Análise: Flamengo x Palmeiras!" → "analise-flamengo-x-palmeiras".
    public static class SlugHelper
    {
        public static string Gerar(string titulo, int maxLength = 200)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return "post";

            // Remove acentos: decompõe (é → e + ´) e descarta os diacríticos.
            var decomposto = titulo.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            foreach (var c in decomposto)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria == UnicodeCategory.NonSpacingMark) continue;

                var minuscula = char.ToLowerInvariant(c);
                if (minuscula >= 'a' && minuscula <= 'z' || minuscula >= '0' && minuscula <= '9')
                    sb.Append(minuscula);
                else
                    sb.Append('-'); // qualquer separador/pontuação vira hífen
            }

            // Colapsa hífens repetidos e apara as pontas.
            var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');

            if (slug.Length == 0) return "post";
            if (slug.Length > maxLength)
                slug = slug[..maxLength].TrimEnd('-');

            return slug;
        }

        // Resolve colisão de slug: se "analise-x" já existe, tenta "analise-x-2",
        // "analise-x-3"... `existe` verifica no banco (ignorando o próprio post).
        public static async Task<string> GerarUnicoAsync(string titulo, Func<string, Task<bool>> existe)
        {
            // -8 reserva espaço para o sufixo numérico sem estourar o limite da coluna.
            var baseSlug = Gerar(titulo, maxLength: 192);
            var slug = baseSlug;
            for (int i = 2; await existe(slug); i++)
                slug = $"{baseSlug}-{i}";
            return slug;
        }
    }
}
