using System.Globalization;
using System.Text;

namespace ControleFutebolWeb.Helpers
{
    // Gera o payload de PIX estático no padrão BR Code (EMV-MPM, especificação do
    // Bacen) — o mesmo texto do "copia e cola", que também vira QR code. Não
    // depende de banco/PSP: qualquer app de banco lê. TxId fixo "***" (padrão
    // para QR estático sem conciliação por identificador).
    public static class PixHelper
    {
        public static string GerarPayload(string chave, string nomeRecebedor, string cidade, decimal valor)
        {
            // Nome (máx. 25) e cidade (máx. 15) sem acentos, como pede a especificação.
            var nome = Truncar(RemoverAcentos(nomeRecebedor).ToUpperInvariant(), 25);
            var cidadeLimpa = Truncar(RemoverAcentos(cidade).ToUpperInvariant(), 15);

            var merchantAccount = Campo("00", "br.gov.bcb.pix") + Campo("01", chave.Trim());

            var payload = new StringBuilder()
                .Append(Campo("00", "01"))                    // Payload Format Indicator
                .Append(Campo("26", merchantAccount))         // Merchant Account Info (PIX)
                .Append(Campo("52", "0000"))                  // Merchant Category Code
                .Append(Campo("53", "986"))                   // Moeda: BRL
                .Append(Campo("54", valor.ToString("0.00", CultureInfo.InvariantCulture)))
                .Append(Campo("58", "BR"))
                .Append(Campo("59", nome))
                .Append(Campo("60", cidadeLimpa))
                .Append(Campo("62", Campo("05", "***")))      // TxId estático
                .Append("6304")                               // CRC16 (ID + tamanho; valor vem abaixo)
                .ToString();

            return payload + Crc16Ccitt(payload);
        }

        private static string Campo(string id, string valor) =>
            id + valor.Length.ToString("00", CultureInfo.InvariantCulture) + valor;

        private static string Truncar(string s, int max) =>
            s.Length <= max ? s : s[..max];

        private static string RemoverAcentos(string texto)
        {
            var decomposto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            foreach (var c in decomposto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // CRC16-CCITT (polinômio 0x1021, inicial 0xFFFF), como definido no BR Code.
        private static string Crc16Ccitt(string dados)
        {
            ushort crc = 0xFFFF;
            foreach (var b in Encoding.UTF8.GetBytes(dados))
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc.ToString("X4", CultureInfo.InvariantCulture);
        }
    }
}
