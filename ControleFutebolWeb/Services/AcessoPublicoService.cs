using System.Security.Cryptography;
using System.Text;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Marcas de "este visitante já abriu esta ferramenta hoje", em memória.
    ///
    /// Cache PRÓPRIO (singleton), e não o IMemoryCache do app: aquele é dividido
    /// com o proxy de imagens e tem SizeLimit em bytes — uma galeria pesada
    /// despejaria as marcas e a mesma pessoa seria contada de novo. Aqui cada
    /// marca é minúscula e o teto é de quantidade.
    /// </summary>
    public sealed class VisitantesDoDiaCache : IDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 200_000 });

        public bool JaVisto(string chave) => _cache.TryGetValue(chave, out _);

        public void Marcar(string chave, TimeSpan validade) =>
            _cache.Set(chave, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = validade,
                Size = 1,
            });

        public void Dispose() => _cache.Dispose();
    }

    /// <summary>
    /// Conta as aberturas das páginas públicas de /creators.
    ///
    /// Só agregado: soma +1 na linha (ferramenta, dia) e nada mais é gravado —
    /// nem IP, nem user-agent, nem horário da visita. O IP e o user-agent são
    /// usados de passagem, para decidir se aquela abertura é uma pessoa NOVA no
    /// dia, e viram um hash que vive apenas na memória do processo.
    ///
    /// Robô não conta: buscador e prévia de link (WhatsApp, Telegram, redes)
    /// batem na página o tempo todo e inflariam o número que se quer olhar.
    /// </summary>
    public class AcessoPublicoService
    {
        private readonly FutebolContext _context;
        private readonly VisitantesDoDiaCache _visitantes;
        private readonly ILogger<AcessoPublicoService> _logger;

        public AcessoPublicoService(FutebolContext context, VisitantesDoDiaCache visitantes,
            ILogger<AcessoPublicoService> logger)
        {
            _context = context;
            _visitantes = visitantes;
            _logger = logger;
        }

        private static readonly TimeZoneInfo FusoBrasil =
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        // Trechos de user-agent de robô. Minúsculo; a comparação é por "contém".
        private static readonly string[] Robos =
        {
            "bot", "crawl", "spider", "slurp", "preview", "fetch", "monitor", "uptime",
            "curl", "wget", "python", "httpclient", "okhttp", "java/", "go-http",
            "headless", "lighthouse", "pingdom", "semrush", "ahrefs", "facebookexternalhit",
            "whatsapp", "telegram", "discord", "slackbot", "embedly", "skypeuripreview",
        };

        public static bool EhRobo(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return true; // sem UA: quase sempre script
            var ua = userAgent.ToLowerInvariant();
            return Robos.Any(r => ua.Contains(r, StringComparison.Ordinal));
        }

        /// <summary>Dia no calendário do Brasil — o "hoje" de quem vai ler o relatório.</summary>
        public static DateOnly HojeNoBrasil() =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasil));

        /// <summary>
        /// Registra uma abertura. Nunca lança: contador quebrado não pode derrubar
        /// a página que ele está apenas observando.
        /// </summary>
        public async Task RegistrarAsync(string ferramenta, HttpContext http)
        {
            try
            {
                var userAgent = http.Request.Headers.UserAgent.ToString();
                if (EhRobo(userAgent)) return;

                var dia = HojeNoBrasil();
                var novoVisitante = MarcarVisitante(ferramenta, dia, http, userAgent);

                await IncrementarAsync(ferramenta, dia, novoVisitante ? 1 : 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AcessoPublico] Falha ao contar acesso de {Ferramenta}", ferramenta);
            }
        }

        /// <summary>
        /// true na primeira vez que este visitante abre esta ferramenta hoje.
        ///
        /// A marca é um hash (IP + user-agent + dia) guardado só em memória, que
        /// expira à meia-noite: nada disso chega ao banco. Reiniciar o app apaga
        /// as marcas do dia, então "Visitantes" é piso, não número exato.
        /// </summary>
        private bool MarcarVisitante(string ferramenta, DateOnly dia, HttpContext http, string userAgent)
        {
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{dia:yyyy-MM-dd}|{ip}|{userAgent}"));
            var chave = $"acesso-publico:{ferramenta}:{Convert.ToHexString(digest)}";

            if (_visitantes.JaVisto(chave)) return false;

            // Expira quando o dia vira no Brasil (com um teto de 24h por segurança).
            var agoraBrasil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasil);
            var faltaParaAmanha = agoraBrasil.Date.AddDays(1) - agoraBrasil;

            _visitantes.Marcar(chave, faltaParaAmanha);

            return true;
        }

        // Upsert do contador. Tenta somar na linha do dia; se ela ainda não existe,
        // cria — e se outra requisição criou primeiro (índice único), soma na dela.
        private async Task IncrementarAsync(string ferramenta, DateOnly dia, int visitantes)
        {
            var atualizadas = await SomarAsync(ferramenta, dia, visitantes);
            if (atualizadas > 0) return;

            try
            {
                _context.AcessosPublicos.Add(new AcessoPublico
                {
                    Ferramenta = ferramenta,
                    Dia = dia,
                    Visitas = 1,
                    Visitantes = visitantes,
                });
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Corrida com outra requisição do mesmo dia: a linha passou a
                // existir entre o UPDATE e o INSERT. Desfaz a inserção pendente
                // (o contexto é por requisição, mas a entidade ficaria marcada) e
                // soma na linha que venceu.
                _context.ChangeTracker.Clear();
                await SomarAsync(ferramenta, dia, visitantes);
            }
        }

        private Task<int> SomarAsync(string ferramenta, DateOnly dia, int visitantes) =>
            _context.AcessosPublicos
                .Where(a => a.Ferramenta == ferramenta && a.Dia == dia)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Visitas, a => a.Visitas + 1)
                    .SetProperty(a => a.Visitantes, a => a.Visitantes + visitantes));
    }
}
