namespace ControleFutebolWeb.Helpers
{
    public class TimesSulamericanaHelper
    {
        public static readonly Dictionary<string, string> mapaTimesNomes = 
            new (StringComparer.OrdinalIgnoreCase)
        {
            
        };
        /// Normaliza o nome vindo do Transfermarkt para o padrão do banco
        public static string NormalizarNome(string nomeTransfermarkt)
        {
            if (string.IsNullOrWhiteSpace(nomeTransfermarkt))
                return string.Empty;

            var nome = nomeTransfermarkt.Trim();

            // Primeiro tenta direto
            if (mapaTimesNomes.TryGetValue(nome, out var nomeBanco))
                return nomeBanco;

            // Se não achou, tenta normalizar (remover acentos, etc.)
            var norm = NormalizarTexto(nome);
            var encontrado = mapaTimesNomes
                .FirstOrDefault(kv => NormalizarTexto(kv.Key) == norm);

            return !string.IsNullOrEmpty(encontrado.Value)
                ? encontrado.Value
                : nome; // fallback: retorna o próprio nome
        }

        // Exemplo de normalização simples (acentos, espaços, etc.)
        private static string NormalizarTexto(string texto)
        {
            return texto
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Aggregate("", (s, c) => s + c)
                .ToLower()
                .Replace(" ", "");
        }
    }
}
