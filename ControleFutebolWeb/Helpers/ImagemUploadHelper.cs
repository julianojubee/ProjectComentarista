namespace ControleFutebolWeb.Helpers
{
    // Validação de imagens enviadas pelo editor do blog.
    //
    // O formato é decidido pelos MAGIC BYTES do conteúdo, nunca pela extensão
    // do nome enviado: renomear "payload.exe" para "foto.jpg" não engana esta
    // checagem, e o arquivo gravado sempre recebe nome novo (GUID) + a extensão
    // derivada do conteúdo real.
    //
    // SVG é recusado de propósito: é XML e pode carregar <script>, o que viraria
    // XSS servido do próprio domínio (mesma origem do cookie de sessão).
    public static class ImagemUploadHelper
    {
        public const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB

        // Bytes suficientes para identificar todos os formatos aceitos
        // (WebP precisa de 12: "RIFF" + tamanho + "WEBP").
        public const int BytesCabecalho = 12;

        // Retorna a extensão canônica ("jpg", "png", "gif", "webp") ou null se o
        // conteúdo não for uma imagem de formato aceito.
        public static string? DetectarFormato(ReadOnlySpan<byte> cabecalho)
        {
            if (cabecalho.Length >= 3 &&
                cabecalho[0] == 0xFF && cabecalho[1] == 0xD8 && cabecalho[2] == 0xFF)
                return "jpg";

            if (cabecalho.Length >= 8 &&
                cabecalho[0] == 0x89 && cabecalho[1] == 0x50 && cabecalho[2] == 0x4E && cabecalho[3] == 0x47 &&
                cabecalho[4] == 0x0D && cabecalho[5] == 0x0A && cabecalho[6] == 0x1A && cabecalho[7] == 0x0A)
                return "png";

            // "GIF87a" / "GIF89a"
            if (cabecalho.Length >= 6 &&
                cabecalho[0] == (byte)'G' && cabecalho[1] == (byte)'I' && cabecalho[2] == (byte)'F' &&
                cabecalho[3] == (byte)'8' && (cabecalho[4] == (byte)'7' || cabecalho[4] == (byte)'9') &&
                cabecalho[5] == (byte)'a')
                return "gif";

            // "RIFF" (0-3) + tamanho (4-7) + "WEBP" (8-11)
            if (cabecalho.Length >= 12 &&
                cabecalho[0] == (byte)'R' && cabecalho[1] == (byte)'I' && cabecalho[2] == (byte)'F' && cabecalho[3] == (byte)'F' &&
                cabecalho[8] == (byte)'W' && cabecalho[9] == (byte)'E' && cabecalho[10] == (byte)'B' && cabecalho[11] == (byte)'P')
                return "webp";

            return null;
        }

        // Valida tamanho + formato. Devolve a extensão canônica quando aceita.
        public static (bool Ok, string? Erro, string? Extensao) Validar(ReadOnlySpan<byte> cabecalho, long tamanhoBytes)
        {
            if (tamanhoBytes <= 0)
                return (false, "Arquivo vazio.", null);

            if (tamanhoBytes > TamanhoMaximoBytes)
                return (false, $"Imagem muito grande ({tamanhoBytes / 1024 / 1024} MB). O limite é {TamanhoMaximoBytes / 1024 / 1024} MB.", null);

            var extensao = DetectarFormato(cabecalho);
            if (extensao == null)
                return (false, "Formato não suportado. Envie JPG, PNG, GIF ou WebP (SVG não é aceito).", null);

            return (true, null, extensao);
        }

        // Caminho relativo dentro de wwwroot, particionado por ano/mês para a
        // pasta não virar um diretório único com milhares de arquivos.
        // O nome é sempre gerado (GUID) — o nome enviado pelo usuário é descartado,
        // o que elimina de saída path traversal e colisão.
        public static string GerarCaminhoRelativo(string extensao, DateTime agora)
        {
            return $"uploads/blog/{agora:yyyy}/{agora:MM}/{Guid.NewGuid():N}.{extensao}";
        }
    }
}
