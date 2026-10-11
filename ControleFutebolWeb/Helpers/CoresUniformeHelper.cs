using System.Globalization;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Cor do uniforme quando a fonte não manda. A api-football trazia a cor da camisa
    /// e do número em lineups[].team.colors, mas desde set/2026 devolve colors: null —
    /// e sem cor o campinho cai no vermelho/azul padrão para qualquer jogo. As cores
    /// cadastradas no Time também não servem (são o #000000 do cadastro automático).
    ///
    /// O que sobra de confiável é o histórico: os jogos importados antes disso guardam
    /// a cor real que cada time usou. Mandante usa o último uniforme que vestiu em casa;
    /// visitante, o último que vestiu fora — e, se ele ficar parecido com o do
    /// mandante, o outro uniforme do time, que é o que acontece em campo.
    /// </summary>
    public static class CoresUniformeHelper
    {
        public record Uniforme(string Camisa, string? Numero);

        /// <summary>
        /// Distância RGB abaixo da qual duas camisas são "da mesma cor" para quem olha o
        /// campinho (vermelho × vinho passa; branco × off-white não).
        /// </summary>
        private const double DistanciaMinima = 90;

        /// <summary>
        /// Completa só o lado que está sem cor — a que veio da fonte (ou que o usuário
        /// escolheu) nunca é sobrescrita. Devolve true se mexeu em alguma coisa.
        /// </summary>
        public static async Task<bool> PreencherFaltantesAsync(
            FutebolContext context, Jogo jogo, CancellationToken ct = default)
        {
            var faltaCasa = string.IsNullOrWhiteSpace(jogo.CorCamisaCasa);
            var faltaVis = string.IsNullOrWhiteSpace(jogo.CorCamisaVisitante);
            if (!faltaCasa && !faltaVis) return false;

            Uniforme? casa = faltaCasa
                ? null
                : new Uniforme(jogo.CorCamisaCasa!, jogo.CorNumeroCasa);
            Uniforme? vis = faltaVis
                ? null
                : new Uniforme(jogo.CorCamisaVisitante!, jogo.CorNumeroVisitante);

            if (faltaCasa)
            {
                var emCasa = await UltimoAsync(context, jogo, jogo.TimeCasaId, comoMandante: true, ct);
                var fora = await UltimoAsync(context, jogo, jogo.TimeCasaId, comoMandante: false, ct);
                casa = EscolherMandante(emCasa, fora);
            }

            if (faltaVis)
            {
                var fora = await UltimoAsync(context, jogo, jogo.TimeVisitanteId, comoMandante: false, ct);
                var emCasa = await UltimoAsync(context, jogo, jogo.TimeVisitanteId, comoMandante: true, ct);
                vis = EscolherVisitante(casa?.Camisa, fora, emCasa);
            }

            var mudou = false;
            if (faltaCasa && casa != null)
            {
                jogo.CorCamisaCasa = casa.Camisa;
                jogo.CorNumeroCasa = casa.Numero ?? NumeroLegivel(casa.Camisa);
                mudou = true;
            }
            if (faltaVis && vis != null)
            {
                jogo.CorCamisaVisitante = vis.Camisa;
                jogo.CorNumeroVisitante = vis.Numero ?? NumeroLegivel(vis.Camisa);
                mudou = true;
            }
            return mudou;
        }

        public static Uniforme? EscolherMandante(Uniforme? emCasa, Uniforme? fora) => emCasa ?? fora;

        /// <summary>
        /// O uniforme de visitante primeiro; se ele bate com a camisa do mandante, o
        /// outro. Se os dois batem, fica o de visitante mesmo — melhor do que nada.
        /// </summary>
        public static Uniforme? EscolherVisitante(string? camisaMandante, Uniforme? fora, Uniforme? emCasa)
        {
            var candidatos = new[] { fora, emCasa }.Where(u => u != null).ToList();
            if (candidatos.Count == 0) return null;
            return candidatos.FirstOrDefault(u => !Parecidas(u!.Camisa, camisaMandante)) ?? candidatos[0];
        }

        public static bool Parecidas(string? a, string? b)
        {
            if (!TentarRgb(a, out var ra) || !TentarRgb(b, out var rb)) return false;
            var dr = ra.R - rb.R; var dg = ra.G - rb.G; var db = ra.B - rb.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db) < DistanciaMinima;
        }

        /// <summary>Branco em camisa escura, preto em camisa clara.</summary>
        public static string NumeroLegivel(string camisa)
        {
            if (!TentarRgb(camisa, out var c)) return "ffffff";
            var luminancia = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return luminancia > 160 ? "000000" : "ffffff";
        }

        private static bool TentarRgb(string? hex, out (int R, int G, int B) rgb)
        {
            rgb = default;
            var h = hex?.Trim().TrimStart('#');
            if (h is not { Length: 6 } ||
                !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                return false;
            rgb = ((v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff);
            return true;
        }

        private static async Task<Uniforme?> UltimoAsync(
            FutebolContext context, Jogo jogo, int timeId, bool comoMandante, CancellationToken ct)
        {
            // Data != null: no Postgres o DESC põe os nulos primeiro.
            var q = context.Jogos.AsNoTracking().Where(j => j.Id != jogo.Id && j.Data != null);

            var linha = comoMandante
                ? await q.Where(j => j.TimeCasaId == timeId && j.CorCamisaCasa != null && j.CorCamisaCasa != "")
                    .OrderByDescending(j => j.Data)
                    .Select(j => new { Camisa = j.CorCamisaCasa!, Numero = j.CorNumeroCasa })
                    .FirstOrDefaultAsync(ct)
                : await q.Where(j => j.TimeVisitanteId == timeId && j.CorCamisaVisitante != null && j.CorCamisaVisitante != "")
                    .OrderByDescending(j => j.Data)
                    .Select(j => new { Camisa = j.CorCamisaVisitante!, Numero = j.CorNumeroVisitante })
                    .FirstOrDefaultAsync(ct);

            return linha == null ? null : new Uniforme(linha.Camisa, linha.Numero);
        }
    }
}
