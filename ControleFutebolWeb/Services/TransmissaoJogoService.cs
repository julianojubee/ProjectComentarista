using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    // Lógica compartilhada de "buscar no futnatv.net e gravar em Jogo.TransmissaoTv" —
    // usada tanto pelo ciclo periódico (AtualizarTransmissoesService) quanto por
    // chamadas pontuais (ex.: ao cadastrar um jogo de hoje em JogosController).
    public class TransmissaoJogoService
    {
        private readonly FutebolContext _context;
        private readonly FutnatvService _futnatv;
        private readonly ILogger<TransmissaoJogoService> _logger;

        private static readonly TimeZoneInfo FusoBrasil =
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");

        public TransmissaoJogoService(FutebolContext context, FutnatvService futnatv, ILogger<TransmissaoJogoService> logger)
        {
            _context = context;
            _futnatv = futnatv;
            _logger = logger;
        }

        public static DateOnly HojeBrasil()
            => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasil));

        // Converte um Jogo.Data (UTC) pro dia civil em Brasília — usado para decidir
        // se vale a pena buscar transmissão agora (só faz sentido para jogos de hoje).
        public static DateOnly? DiaBrasilDoJogo(DateTime? dataUtc)
        {
            if (!dataUtc.HasValue) return null;
            var utc = dataUtc.Value.Kind == DateTimeKind.Utc
                ? dataUtc.Value : DateTime.SpecifyKind(dataUtc.Value, DateTimeKind.Utc);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, FusoBrasil));
        }

        // Busca as transmissões do dia informado (Brasília) no futnatv e atualiza os
        // jogos daquele dia que já estão no banco. Retorna quantos jogos foram atualizados.
        public async Task<int> AtualizarTransmissoesDoDiaAsync(DateOnly diaBrasilia, CancellationToken ct = default)
        {
            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(diaBrasilia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), FusoBrasil);
            var fimUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(diaBrasilia.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), FusoBrasil);

            var jogosDoDia = await _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j => j.Data >= inicioUtc && j.Data < fimUtc)
                .ToListAsync(ct);

            if (jogosDoDia.Count == 0) return 0;

            var cicloId = Guid.NewGuid();
            var resultado = await _futnatv.BuscarJogosDoDiaAsync(diaBrasilia, ct);

            _context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId = cicloId,
                Data = DateTime.UtcNow,
                Tipo = "Futnatv",
                Acao = resultado.Sucesso ? "Requisicao" : "RequisicaoErro",
                Detalhes = resultado.Sucesso
                    ? $"GET {resultado.Url} -> {resultado.Jogos.Count} jogo(s) no dia {diaBrasilia:yyyy-MM-dd}"
                    : $"GET {resultado.Url} falhou: {resultado.Erro}",
            });

            if (!resultado.Sucesso || resultado.Jogos.Count == 0)
            {
                await _context.SaveChangesAsync(ct);
                return 0;
            }

            int atualizados = 0;
            foreach (var jogo in jogosDoDia)
            {
                var correspondente = resultado.Jogos.FirstOrDefault(f =>
                    TimeNomeMatcher.SaoMesmaCompeticao(jogo.Competicao?.Nome, f.Competition) &&
                    TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, f.Home) &&
                    TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, f.Away));

                if (correspondente == null || string.IsNullOrWhiteSpace(correspondente.Broadcast))
                    continue;

                if (jogo.TransmissaoTv == correspondente.Broadcast) continue;

                jogo.TransmissaoTv = correspondente.Broadcast;
                jogo.TransmissaoAtualizadaEm = DateTime.UtcNow;
                atualizados++;

                _context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
                {
                    CicloId = cicloId,
                    Data = DateTime.UtcNow,
                    Tipo = "Futnatv",
                    Acao = "Transmissao",
                    CompeticaoNome = jogo.Competicao?.Nome,
                    JogoDescricao = $"{jogo.TimeCasa?.Nome} x {jogo.TimeVisitante?.Nome}",
                    Detalhes = correspondente.Broadcast,
                });
            }

            await _context.SaveChangesAsync(ct);
            return atualizados;
        }
    }
}
