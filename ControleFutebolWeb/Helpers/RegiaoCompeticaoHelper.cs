using System.Globalization;
using System.Text;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Normaliza o campo livre <c>Competicao.Regiao</c> para exibição e agrupa as
    /// competições por bloco geográfico na lista de Competições. O cadastro tem
    /// variações do mesmo lugar ("World" e "Mundo", "Italia" sem acento) que, sem isso,
    /// apareciam como regiões diferentes.
    /// </summary>
    public static class RegiaoCompeticaoHelper
    {
        // Chave sem acento e minúscula → nome de exibição
        private static readonly Dictionary<string, string> _nomes = new()
        {
            ["world"] = "Mundo",
            ["mundo"] = "Mundo",
            ["internacional"] = "Mundo",
            ["international"] = "Mundo",
            ["america do sul"] = "América do Sul",
            ["south america"] = "América do Sul",
            ["conmebol"] = "América do Sul",
            ["europe"] = "Europa",
            ["europa"] = "Europa",
            ["uefa"] = "Europa",
            ["italia"] = "Itália",
            ["italy"] = "Itália",
            ["arabia saudita"] = "Arábia Saudita",
            ["franca"] = "França",
            ["alemanha"] = "Alemanha",
            ["espanha"] = "Espanha",
            ["inglaterra"] = "Inglaterra",
            ["asia"] = "Ásia",
        };

        // Nome normalizado → bloco onde a competição aparece na lista
        private static readonly Dictionary<string, string> _blocos = new()
        {
            ["Brasil"] = "Brasil",
            ["América do Sul"] = "América do Sul",
            ["Argentina"] = "América do Sul",
            ["Uruguai"] = "América do Sul",
            ["Chile"] = "América do Sul",
            ["Colômbia"] = "América do Sul",
            ["Paraguai"] = "América do Sul",
            ["Peru"] = "América do Sul",
            ["Equador"] = "América do Sul",
            ["Bolívia"] = "América do Sul",
            ["Venezuela"] = "América do Sul",
            ["Europa"] = "Europa",
            ["Inglaterra"] = "Europa",
            ["Espanha"] = "Europa",
            ["Itália"] = "Europa",
            ["Alemanha"] = "Europa",
            ["França"] = "Europa",
            ["Portugal"] = "Europa",
            ["Holanda"] = "Europa",
            ["Países Baixos"] = "Europa",
            ["Bélgica"] = "Europa",
            ["Turquia"] = "Europa",
            ["Escócia"] = "Europa",
            ["Ásia"] = "Ásia e Oriente Médio",
            ["Arábia Saudita"] = "Ásia e Oriente Médio",
            ["Qatar"] = "Ásia e Oriente Médio",
            ["Catar"] = "Ásia e Oriente Médio",
            ["Emirados Árabes Unidos"] = "Ásia e Oriente Médio",
            ["Japão"] = "Ásia e Oriente Médio",
            ["Mundo"] = "Mundo",
        };

        /// <summary>Ordem dos blocos na tela; os não listados vêm depois, em ordem alfabética.</summary>
        public static readonly string[] OrdemBlocos =
            { "Brasil", "América do Sul", "Europa", "Mundo", "Ásia e Oriente Médio" };

        public static string Normalizar(string? regiao)
        {
            if (string.IsNullOrWhiteSpace(regiao)) return "";
            var limpo = regiao.Trim();
            if (_nomes.TryGetValue(SemAcento(limpo).ToLowerInvariant(), out var nome)) return nome;
            return CountryHelper.TraduzirOuNull(limpo) ?? limpo;
        }

        /// <summary>Bloco da lista para a região; sem mapeamento, a própria região (ou "Outras").</summary>
        public static string Bloco(string? regiao)
        {
            var nome = Normalizar(regiao);
            if (nome == "") return "Outras";
            return _blocos.TryGetValue(nome, out var bloco) ? bloco : nome;
        }

        /// <summary>Posição do bloco para ordenar (os desconhecidos ficam no fim).</summary>
        public static int OrdemBloco(string bloco)
        {
            var i = Array.IndexOf(OrdemBlocos, bloco);
            return i >= 0 ? i : OrdemBlocos.Length;
        }

        private static string SemAcento(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
