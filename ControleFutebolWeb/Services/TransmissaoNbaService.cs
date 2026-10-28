using System.Globalization;
using System.Text;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models.Nba;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Onde cada jogo da NBA passa no Brasil, lido do futnatv.net (/api/basquete) e
    /// casado com os jogos da ESPN na hora de montar a tela. Nada é gravado: o
    /// resultado de cada dia fica em cache e a página /NBA consulta quando abre.
    ///
    /// Casamento:
    /// - o dia do futnatv é o dia civil em Brasília, não a data da rodada nos EUA
    ///   (o jogo das 00h BRT está no dia seguinte lá);
    /// - os times vêm só pelo apelido em inglês ("Lakers", "Trail Blazers"), que é o
    ///   shortDisplayName da ESPN; o par é comparado sem ordem porque o futnatv não
    ///   segue o mando ("Hawks x Magic" com o Magic em casa);
    /// - o horário só desempata se o mesmo par aparecer duas vezes no dia.
    /// </summary>
    public class TransmissaoNbaService
    {
        private static readonly TimeSpan CacheSucesso = TimeSpan.FromHours(2);
        // Falha também vai para o cache, por pouco tempo, para um site fora do ar não
        // ser consultado a cada F5.
        private static readonly TimeSpan CacheFalha = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ToleranciaHorario = TimeSpan.FromMinutes(90);

        // Apelidos alternativos que o site pode usar no lugar do oficial.
        private static readonly Dictionary<string, string> Apelidos = new()
        {
            ["blazers"] = "trailblazers",
            ["sixers"] = "76ers",
            ["wolves"] = "timberwolves",
            ["cavs"] = "cavaliers",
            ["mavs"] = "mavericks",
        };

        private readonly FutnatvService _futnatv;
        private readonly IMemoryCache _cache;

        public TransmissaoNbaService(FutnatvService futnatv, IMemoryCache cache)
        {
            _futnatv = futnatv;
            _cache = cache;
        }

        /// <summary>
        /// Canais por id de evento da ESPN. Jogo que o futnatv não lista (fora da janela
        /// de ~7 dias, ou site fora do ar) simplesmente não entra no dicionário.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> CanaisAsync(
            IEnumerable<NbaJogo> jogos, CancellationToken ct = default)
        {
            var resultado = new Dictionary<string, IReadOnlyList<string>>();

            foreach (var porDia in jogos.Where(j => j.InicioUtc != DateTime.MinValue)
                                        .GroupBy(j => DateOnly.FromDateTime(NbaViewHelper.Brasilia(j.InicioUtc))))
            {
                var doDia = await JogosNbaDoDiaAsync(porDia.Key, ct);
                if (doDia.Count == 0) continue;

                foreach (var jogo in porDia)
                {
                    var par = Par(jogo.Casa.Time.NomeCurto, jogo.Visitante.Time.NomeCurto);
                    var candidatos = doDia.Where(f => f.Par == par).ToList();
                    if (candidatos.Count > 1)
                    {
                        var inicio = TimeOnly.FromDateTime(NbaViewHelper.Brasilia(jogo.InicioUtc));
                        candidatos = candidatos
                            .Where(f => f.Horario is TimeOnly h && Distancia(h, inicio) <= ToleranciaHorario)
                            .OrderBy(f => Distancia(f.Horario!.Value, inicio))
                            .ToList();
                    }

                    var achado = candidatos.FirstOrDefault();
                    if (achado.Canais is { Count: > 0 })
                        resultado[jogo.Id] = achado.Canais;
                }
            }

            return resultado;
        }

        private record struct JogoFutnatv(string Par, TimeOnly? Horario, IReadOnlyList<string> Canais);

        private async Task<List<JogoFutnatv>> JogosNbaDoDiaAsync(DateOnly dia, CancellationToken ct)
        {
            var chave = $"futnatv:basquete:{dia:yyyy-MM-dd}";
            if (_cache.TryGetValue<List<JogoFutnatv>>(chave, out var cacheado) && cacheado != null)
                return cacheado;

            var r = await _futnatv.BuscarJogosDoDiaAsync(dia, ct, FutnatvService.Basquete);
            var jogos = r.Jogos
                // A lista mistura NBB, EuroLiga e WNBA. "NBA Cup" e afins também contam.
                .Where(j => j.Competition?.StartsWith("NBA", StringComparison.OrdinalIgnoreCase) == true)
                .Select(j => new JogoFutnatv(Par(j.Home, j.Away), Horario(j.Time), Canais(j.Broadcast)))
                .ToList();

            _cache.Set(chave, jogos, new MemoryCacheEntryOptions
            {
                Size = 1 + jogos.Count * 200,
                AbsoluteExpirationRelativeToNow = r.Sucesso ? CacheSucesso : CacheFalha,
            });
            return jogos;
        }

        private static TimeSpan Distancia(TimeOnly a, TimeOnly b)
        {
            var d = (a - b).Duration();
            // 23h30 x 00h15 é 45 min, não 23h15.
            return d > TimeSpan.FromHours(12) ? TimeSpan.FromHours(24) - d : d;
        }

        // "23h00" → 23:00
        private static TimeOnly? Horario(string? texto) =>
            TimeOnly.TryParseExact(texto?.Trim(), "H'h'mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

        /// <summary>"ESPN 2 e Disney+" → ["ESPN 2", "Disney+"]. "YT" vira "YouTube", como o site exibe.</summary>
        public static IReadOnlyList<string> Canais(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Trim() == "-") return Array.Empty<string>();
            return texto.Split(new[] { " e ", ",", "/" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.Equals("YT", StringComparison.OrdinalIgnoreCase) ? "YouTube" : c)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string Par(string a, string b)
        {
            var x = Normalizar(a);
            var y = Normalizar(b);
            return string.CompareOrdinal(x, y) <= 0 ? $"{x}|{y}" : $"{y}|{x}";
        }

        // Minúsculas, sem acento e só letras/dígitos: "Trail Blazers" e "trail-blazers"
        // dão "trailblazers".
        private static string Normalizar(string nome)
        {
            var sb = new StringBuilder();
            foreach (var c in nome.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            var n = sb.ToString();
            return Apelidos.GetValueOrDefault(n, n);
        }
    }
}
