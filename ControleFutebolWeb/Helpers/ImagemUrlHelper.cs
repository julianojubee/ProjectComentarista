using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Helpers
{
    public static class ImagemUrlHelper
    {
        /// <summary>
        /// Resolve a URL de exibição de uma foto/imagem.
        /// Hosts do api-sports passam pelo MediaProxy (que injeta a chave da API e
        /// está protegido por allowlist); URLs manuais de qualquer outro host são
        /// carregadas direto pelo navegador — o MediaProxy bloquearia hosts fora da
        /// allowlist (proteção contra SSRF), o que faria a imagem não aparecer.
        /// </summary>
        public static string? FotoSrc(this IUrlHelper url, string? foto)
        {
            if (string.IsNullOrWhiteSpace(foto)) return null;

            if (Uri.TryCreate(foto, UriKind.Absolute, out var uri) &&
                uri.Host.EndsWith("api-sports.io", StringComparison.OrdinalIgnoreCase))
                return url.Action("Imagem", "MediaProxy", new { url = foto });

            return foto;
        }
    }
}
