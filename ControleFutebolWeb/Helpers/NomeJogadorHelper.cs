using System.Globalization;
using System.Text;

namespace ControleFutebolWeb.Helpers
{
    // Comparação de nomes de jogador entre fontes que escrevem diferente: a API
    // ("K. Prates"), o cadastro manual ("Kauã Prates") e o feed de eventos
    // ("L. E. Jales do Nascimento"). Usado para reaproveitar o jogador que já existe
    // em vez de criar um duplicado.
    public static class NomeJogadorHelper
    {
        // Minúsculas, sem acentos e com espaços colapsados.
        public static string Normalizar(string nome)
        {
            var decomposto = (nome ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            foreach (var c in decomposto)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // Compara nomes tolerando abreviação por iniciais: "L. E. Jales do Nascimento"
        // ↔ "Lucas Emanuel". Cada token é comparado na ordem por igualdade ou por
        // inicial ("l." ↔ "lucas"); sobrenomes excedentes do nome mais longo são
        // ignorados. Usado só entre candidatos restritos (jogadores do mesmo time),
        // nunca como busca geral.
        public static bool Corresponde(string a, string b)
        {
            var ta = Normalizar(a).Split(' ');
            var tb = Normalizar(b).Split(' ');
            if (ta.Length == 0 || tb.Length == 0) return false;

            var n = Math.Min(ta.Length, tb.Length);
            var exatos = 0;
            for (var i = 0; i < n; i++)
            {
                var x = ta[i].TrimEnd('.');
                var y = tb[i].TrimEnd('.');
                if (x.Length == 0 || y.Length == 0) return false;
                if (x == y) { exatos++; continue; }
                if (x.Length == 1 && y[0] == x[0]) continue;
                if (y.Length == 1 && x[0] == y[0]) continue;
                return false;
            }

            // Exige dois tokens casando (ou um token exato razoável) para não
            // vincular por coincidência de uma única inicial.
            return n >= 2 || (exatos == 1 && ta[0].Length >= 4);
        }
    }
}
