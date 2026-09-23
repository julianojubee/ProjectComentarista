using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Preenche a escalação inicial dos lados que a api-football não trouxe, nesta
    /// ordem de fontes:
    ///
    ///   1. FotMob — cobre mais ligas e publica o id de cada atleta, que casa direto
    ///      com o vínculo gravado na tela do jogador (Jogador.IdFotMob). Nome + camisa
    ///      vêm logo depois, e só então nome ou camisa isolados
    ///      (ver EspnEscalacaoService.AplicarAtletasAsync).
    ///   2. ESPN — terceira opção, só para o lado que o FotMob também não tinha.
    ///
    /// Os lados que faltam são recalculados depois de cada fonte: se o FotMob preencher
    /// um lado só, a ESPN ainda é tentada para o outro. Antes a ordem era a inversa e a
    /// segunda fonte só entrava quando a primeira não preenchia NENHUM lado.
    ///
    /// Usado pelo botão "Reimportar dados" (com o usuário, que também recebe a cópia
    /// pessoal) e pelo ciclo de jogos ao vivo (sem usuário: só a compartilhada).
    /// Jogo importado da FIFA não passa por aqui — tem cascata própria.
    /// </summary>
    public class EscalacaoAlternativaService
    {
        private readonly FotMobService _fotmob;
        private readonly FotMobEscalacaoService _fotmobEscalacao;
        private readonly EspnEstatisticasService _espn;
        private readonly EspnEscalacaoService _espnEscalacao;
        private readonly ILogger<EscalacaoAlternativaService> _logger;

        public EscalacaoAlternativaService(
            FotMobService fotmob,
            FotMobEscalacaoService fotmobEscalacao,
            EspnEstatisticasService espn,
            EspnEscalacaoService espnEscalacao,
            ILogger<EscalacaoAlternativaService> logger)
        {
            _fotmob = fotmob;
            _fotmobEscalacao = fotmobEscalacao;
            _espn = espn;
            _espnEscalacao = espnEscalacao;
            _logger = logger;
        }

        /// <summary>Uma fonte que preencheu escalação.</summary>
        public record Preenchimento(string Fonte, string Mensagem);

        /// <param name="usuarioId">Null = só a escalação compartilhada.</param>
        /// <returns>As fontes que preencheram algum lado, na ordem em que entraram.</returns>
        public async Task<List<Preenchimento>> PreencherLadosFaltandoAsync(
            FutebolContext context, int jogoId, string? usuarioId, CancellationToken ct = default)
        {
            var preenchidos = new List<Preenchimento>();

            var (faltaCasa, faltaVis) = await LadosSemEscalacaoImportadaAsync(context, jogoId, ct);
            if (!faltaCasa && !faltaVis) return preenchidos;

            var idApiLiga = await context.Jogos.AsNoTracking()
                .Where(j => j.Id == jogoId)
                .Select(j => j.Competicao!.IdApi)
                .FirstOrDefaultAsync(ct);

            // ── 1º FotMob ──
            if (_fotmob.TemCobertura(idApiLiga))
            {
                var r = await _fotmobEscalacao.AplicarAsync(context, jogoId, usuarioId,
                    filtroLado: ehCasa => ehCasa ? faltaCasa : faltaVis, ct: ct);

                await RegistrarAsync(context, jogoId, LogFonteExterna.TipoFotMob, r.Ok, r.Mensagem, ct);
                if (r.Ok) preenchidos.Add(new Preenchimento(FonteEscalacao.FotMob, r.Mensagem));

                (faltaCasa, faltaVis) = await LadosSemEscalacaoImportadaAsync(context, jogoId, ct);
                if (!faltaCasa && !faltaVis) return preenchidos;
            }

            // ── 2º ESPN, para o que o FotMob não cobriu ──
            if (_espn.SlugDaLiga(idApiLiga) != null)
            {
                var r = await _espnEscalacao.AplicarAsync(context, jogoId, usuarioId,
                    filtroLado: ehCasa => ehCasa ? faltaCasa : faltaVis, ct: ct);

                await RegistrarAsync(context, jogoId, LogFonteExterna.TipoEspn, r.Ok, r.Mensagem, ct);
                if (r.Ok) preenchidos.Add(new Preenchimento(FonteEscalacao.Espn, r.Mensagem));
            }

            if (preenchidos.Count == 0)
                _logger.LogInformation(
                    "[EscalacaoAlternativa] Jogo {Id}: nem FotMob nem ESPN tinham o lado que faltava.", jogoId);

            return preenchidos;
        }

        /// <summary>
        /// Que lados do jogo NÃO têm escalação importada (linha compartilhada com
        /// jogador). É o mesmo critério do selo de origem da tela de análise.
        /// </summary>
        public static async Task<(bool Casa, bool Visitante)> LadosSemEscalacaoImportadaAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var lados = await context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.UsuarioId == null && e.JogadorId != null
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .Select(e => e.IsTimeCasa)
                .Distinct()
                .ToListAsync(ct);

            return (!lados.Contains(true), !lados.Contains(false));
        }

        private async Task RegistrarAsync(
            FutebolContext context, int jogoId, string tipo, bool ok, string mensagem, CancellationToken ct)
        {
            _logger.LogInformation("[EscalacaoAlternativa] Jogo {Id} › {Fonte}: {Ok} — {Msg}", jogoId, tipo, ok, mensagem);

            var jogo = await context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            await LogFonteExterna.RegistrarAsync(context, tipo, "Escalação (reimportação)", ok, jogo, mensagem, ct);
        }
    }
}
