using Microsoft.AspNetCore.Http;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>Resultado de um upload de imagem.</summary>
    public record ResultadoUpload(bool Sucesso, string? UrlRelativa, string? Erro);

    /// <summary>
    /// Salvamento seguro de imagens enviadas por upload: valida extensão,
    /// content-type e tamanho, e grava com um nome gerado (nunca o nome enviado
    /// pelo cliente) dentro de wwwroot — impedindo path traversal e upload de
    /// arquivos executáveis/SVG com script.
    /// </summary>
    public static class UploadHelper
    {
        private static readonly HashSet<string> ExtensoesImagem = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

        private static readonly HashSet<string> ContentTypesImagem = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/webp", "image/gif" };

        public const long TamanhoMaximoBytes = 5L * 1024 * 1024; // 5 MB

        public static bool ValidarImagem(IFormFile? arquivo, out string erro)
        {
            erro = "";
            if (arquivo == null || arquivo.Length == 0) { erro = "Arquivo vazio."; return false; }
            if (arquivo.Length > TamanhoMaximoBytes) { erro = "Arquivo excede o limite de 5 MB."; return false; }

            var ext = Path.GetExtension(arquivo.FileName);
            if (!ExtensoesImagem.Contains(ext)) { erro = "Extensão não permitida (use png, jpg, webp ou gif)."; return false; }
            if (!ContentTypesImagem.Contains(arquivo.ContentType)) { erro = "Tipo de conteúdo inválido."; return false; }
            return true;
        }

        /// <summary>
        /// Valida e salva a imagem em <paramref name="pastaRelativa"/> (dentro de
        /// wwwroot). Retorna a URL relativa (ex.: /images/escudos/flamengo-ab12.png).
        /// </summary>
        public static async Task<ResultadoUpload> SalvarImagemAsync(
            IFormFile? arquivo, string webRootPath, string pastaRelativa, string? nomeBase = null)
        {
            if (!ValidarImagem(arquivo, out var erro))
                return new ResultadoUpload(false, null, erro);

            var ext = Path.GetExtension(arquivo!.FileName).ToLowerInvariant();
            var nomeArquivo = $"{Slug(nomeBase)}{Guid.NewGuid():N}{ext}";

            // Resolve a pasta física e garante que está DENTRO de wwwroot (anti path traversal).
            var rootFull = Path.GetFullPath(webRootPath);
            var pastaFisica = Path.GetFullPath(Path.Combine(rootFull, pastaRelativa));
            if (!pastaFisica.StartsWith(rootFull, StringComparison.Ordinal))
                return new ResultadoUpload(false, null, "Caminho de destino inválido.");

            Directory.CreateDirectory(pastaFisica);
            var caminhoFisico = Path.Combine(pastaFisica, nomeArquivo);

            using (var stream = new FileStream(caminhoFisico, FileMode.Create))
                await arquivo.CopyToAsync(stream);

            var url = "/" + pastaRelativa.Replace('\\', '/').Trim('/') + "/" + nomeArquivo;
            return new ResultadoUpload(true, url, null);
        }

        // Gera um prefixo seguro a partir de um nome (só letras/dígitos/hífen).
        private static string Slug(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var chars = s.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var slug = new string(chars).Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (slug.Length > 40) slug = slug[..40];
            return slug.Length > 0 ? slug + "-" : "";
        }
    }
}
