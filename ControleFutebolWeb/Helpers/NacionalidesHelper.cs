namespace ControleFutebolWeb.Helpers
{
    public static class NacionalidadesHelper
    {
        // Dicionário de nacionalidades: inglês/alemão → português
        private static readonly Dictionary<string, string> _mapaFlags =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "Brazil", "Brasil" }, { "Brasil", "Brasil" },
            { "Argentina", "Argentina" },
            { "Uruguay", "Uruguai" },
            { "Chile", "Chile" },
            { "Paraguay", "Paraguai" },
            { "Bolivia", "Bolívia" }, { "Bolivien", "Bolívia" },
            { "Peru", "Peru" },
            { "Ecuador", "Equador" },
            { "Colombia", "Colômbia" }, { "Kolumbien", "Colômbia" },
            { "Venezuela", "Venezuela" },
            { "Portugal", "Portugal" },
            { "Spain", "Espanha" }, { "Spanien", "Espanha" },
            { "France", "França" }, { "Frankreich", "França" },
            { "Germany", "Alemanha" }, { "Deutschland", "Alemanha" },
            { "Italy", "Itália" }, { "Italien", "Itália" },
            { "England", "Inglaterra" },
            { "Netherlands", "Holanda" }, { "Niederlande", "Holanda" },
            { "Belgium", "Bélgica" }, { "Belgien", "Bélgica" },
            { "Switzerland", "Suíça" }, { "Schweiz", "Suíça" },
            { "Croatia", "Croácia" }, { "Kroatien", "Croácia" },
            { "Mexico", "México" }, { "Mexiko", "México" },
            { "United States", "Estados Unidos" }, { "USA", "Estados Unidos" },
            { "Canada", "Canadá" }, { "Kanada", "Canadá" },
            { "Morocco", "Marrocos" }, { "Marokko", "Marrocos" },
            { "Senegal", "Senegal" },
            { "Ghana", "Gana" },
            { "Ivory Coast", "Costa do Marfim" },
            { "Nigeria", "Nigéria" },
            { "Cameroon", "Camarões" }, { "Kamerun", "Camarões" },
            { "Democratic Republic of Congo", "República Democrática do Congo" },
            { "Angola", "Angola" },
            { "Ukraine", "Ucrânia" },
            { "Serbia", "Sérvia" }, { "Serbien", "Sérvia" },
            { "Denmark", "Dinamarca" }, { "Dänemark", "Dinamarca" },
            { "Greece", "Grécia" }, { "Griechenland", "Grécia" },
            { "Panama", "Panamá" },
            { "Guinea", "Guiné" },
        };

        /// <summary>
        /// Normaliza a nacionalidade para português.
        /// </summary>
        public static string Normalizar(string nacionalidade)
        {
            if (string.IsNullOrWhiteSpace(nacionalidade))
                return string.Empty;

            var nome = nacionalidade.Trim();

            return _mapaFlags.TryGetValue(nome, out var nomePt)
                ? nomePt
                : nome; // fallback: retorna o próprio nome se não achar
        }

        /// <summary>
        /// Verifica se a nacionalidade existe no mapa.
        /// </summary>
        public static bool ExisteNoMapa(string nacionalidade)
        {
            return !string.IsNullOrWhiteSpace(nacionalidade)
                   && _mapaFlags.ContainsKey(nacionalidade.Trim());
        }
    }
}
