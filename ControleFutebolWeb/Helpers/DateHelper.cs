using System;

namespace ControleFutebolWeb.Helpers
{
    public static class DateHelper
    {
        /// <summary>
        /// Formata uma data opcional (DateTime?) no padrão desejado.
        /// Se for nula, retorna "—".
        /// </summary>
        public static string FormatarData(DateTime? data, string formato = "dd/MM/yyyy")
        {
            return data.HasValue
                ? data.Value.ToLocalTime().ToString(formato)
                : "—";
        }
    }
}
