using System.Globalization;
using ControleFutebolWeb.Models.Nba;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>Formatação compartilhada pelas views de /NBA.</summary>
    public static class NbaViewHelper
    {
        public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static DateTime Brasilia(DateTime utc) =>
            DateHelper.ParaBrasilia(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) ?? utc;

        /// <summary>
        /// "20:30", ou "00:30 (qua)" quando o jogo já cai no dia seguinte no horário
        /// de Brasília — a rodada é agrupada pela data dos EUA.
        /// </summary>
        public static string Horario(DateTime inicioUtc, DateTime diaRodada)
        {
            var local = Brasilia(inicioUtc);
            var hora = local.ToString("HH:mm", PtBr);
            return local.Date == diaRodada.Date ? hora : $"{hora} ({local.ToString("ddd", PtBr).TrimEnd('.')})";
        }

        public static string DataHora(DateTime inicioUtc) =>
            Brasilia(inicioUtc).ToString("ddd, dd/MM 'às' HH:mm", PtBr);

        /// <summary>"ter 20/10" — data curta das tabelas de jogos.</summary>
        public static string DataCurta(DateTime utc) =>
            utc == DateTime.MinValue ? "" : Brasilia(utc).ToString("ddd dd/MM", PtBr).Replace(".", "");

        public static string Capitalizar(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

        public static string SiglaLider(string categoria) => categoria switch
        {
            "points" => "PTS",
            "rebounds" => "REB",
            "assists" => "AST",
            _ => categoria.ToUpperInvariant(),
        };

        public static string ClasseEstado(NbaSituacao s) => s switch
        {
            NbaSituacao.AoVivo => "vivo",
            NbaSituacao.Encerrado => "enc",
            _ => "ag",
        };

        public static string ClasseSequencia(string seq) =>
            seq.StartsWith('V') ? "nba-seq-v" : seq.StartsWith('D') ? "nba-seq-d" : "";
    }
}
