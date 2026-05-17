namespace ControleFutebolWeb.Helpers
{
    public static class DataHelper
    {
        private static readonly Dictionary<string, int> _meses =
            new(StringComparer.OrdinalIgnoreCase)
        {
        { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 },
        { "mai", 5 }, { "jun", 6 }, { "jul", 7 }, { "ago", 8 },
        { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 },
        { "ene", 1 }, { "feb", 2 }, { "may", 5 }, { "sep", 9 }, { "oct", 10 }, { "dic", 12 },
        { "jan.", 1 }, { "feb.", 2 }, { "mar.", 3 }, { "apr.", 4 },
        { "may.", 5 }, { "jun.", 6 }, { "jul.", 7 }, { "aug.", 8 },
        { "sep.", 9 }, { "oct.", 10 }, { "nov.", 11 }, { "dec.", 12 },
        };

        public static int? GetMes(string abreviacao)
        {
            return _meses.TryGetValue(abreviacao.Trim(), out var numero)
                ? numero
                : null; // retorna null se não encontrar
        }
    }

}
