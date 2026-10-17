using System.Text;
using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Conserta texto UTF-8 que alguém leu como Latin-1 no caminho ("JoÃ£o" no lugar
    /// de "João"). A api-football devolve alguns nomes assim, já estragados na fonte.
    ///
    /// Só mexe quando a volta é certa: os bytes precisam formar UTF-8 válido. Um nome
    /// que de fato tenha "Ã" seguido de letra comum ("Ãngel") não casa com o padrão
    /// de caractere estragado e fica como está.
    /// </summary>
    public static class MojibakeHelper
    {
        private static readonly Encoding Utf8Estrito = new UTF8Encoding(false, throwOnInvalidBytes: true);

        // Ã/Â + byte de continuação: um caractere de 2 bytes lido como dois
        private static readonly Regex Par = new("[ÂÃ][\u0080-¿]", RegexOptions.Compiled);

        public static string? Corrigir(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            if (texto.IndexOf('Ã') < 0 && texto.IndexOf('Â') < 0) return texto;

            // Texto todo estragado: a volta inteira é a mais segura (pega também
            // caracteres de 3 bytes, que o par a par não cobre)
            if (texto.All(c => c <= 'ÿ'))
            {
                try { return Utf8Estrito.GetString(Encoding.Latin1.GetBytes(texto)); }
                catch (DecoderFallbackException) { /* misturado: segue para o par a par */ }
            }

            // Misturado ("SnÃ¦hólm": o æ estragado, o ó certo): conserta só os pares
            return Par.Replace(texto, m =>
            {
                try { return Utf8Estrito.GetString(Encoding.Latin1.GetBytes(m.Value)); }
                catch (DecoderFallbackException) { return m.Value; }
            });
        }
    }
}
