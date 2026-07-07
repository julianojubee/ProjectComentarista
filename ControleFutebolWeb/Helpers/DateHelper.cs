using System;

namespace ControleFutebolWeb.Helpers
{
    public static class DateHelper
    {
        private static readonly TimeZoneInfo _brt =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

        /// <summary>
        /// Converte a data de UTC para horário de Brasília (BRT/BRST) e formata.
        /// Se for nula, retorna "—".
        /// </summary>
        public static string FormatarData(DateTime? data, string formato = "dd/MM/yyyy")
        {
            if (!data.HasValue) return "—";

            var utc = data.Value.Kind == DateTimeKind.Utc
                ? data.Value
                : DateTime.SpecifyKind(data.Value, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, _brt).ToString(formato);
        }

        public static DateTime? ParaBrasilia(DateTime? data)
        {
            if (!data.HasValue) return null;
            var utc = data.Value.Kind == DateTimeKind.Utc
                ? data.Value
                : DateTime.SpecifyKind(data.Value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _brt);
        }

        /// <summary>
        /// Converte um horário informado em Brasília (ex.: digitado num formulário) para
        /// UTC, pra salvar no banco — inverso do <see cref="ParaBrasilia"/>.
        /// </summary>
        public static DateTime? DeBrasiliaParaUtc(DateTime? dataBrasilia)
        {
            if (!dataBrasilia.HasValue) return null;
            var local = DateTime.SpecifyKind(dataBrasilia.Value, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, _brt);
        }
    }
}
