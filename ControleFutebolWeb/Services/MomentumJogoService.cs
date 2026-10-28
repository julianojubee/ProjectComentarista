using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Aba Momentum do Pós-jogo: a curva de pressão minuto a minuto, com gols e
    /// expulsões marcados por cima. Serve as duas telas do pós-jogo (o modal de
    /// /Jogos/Analisar e a página pública /analise/{token}) — o usuário entra por
    /// parâmetro, como no PainelJogoService.
    ///
    /// Fica fora do PosJogoAsync de propósito: buscar no FotMob pode levar segundos
    /// na primeira vez, e o pós-jogo não pode esperar por uma aba que talvez nem
    /// seja aberta. O JS carrega esta parte só quando a aba é clicada.
    /// </summary>
    public class MomentumJogoService
    {
        private readonly FutebolContext _context;
        private readonly FotMobService _fotmob;
        private readonly ILogger<MomentumJogoService> _logger;

        public MomentumJogoService(FutebolContext context, FotMobService fotmob, ILogger<MomentumJogoService> logger)
        {
            _context = context;
            _fotmob = fotmob;
            _logger = logger;
        }

        /// <summary>
        /// Abaixo disso a marcação por vídeo é teste ou começo de trabalho, e a curva
        /// mostraria picos de um lance só como se fossem domínio.
        /// </summary>
        public const int MinAcoesVideo = 15;

        public async Task<object?> MontarAsync(int jogoId, string? usuarioId, CancellationToken ct = default)
        {
            var jogo = await _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);
            if (jogo == null) return null;

            // Lado de cada jogador: escalação do dono da análise (ou a importada),
            // senão o cadastro — o mesmo critério do pós-jogo.
            var ladoEscalacao = (await _context.Escalacoes.AsNoTracking()
                    .Where(e => e.JogoId == jogoId && e.JogadorId != null && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                    .Select(e => new { JogadorId = e.JogadorId!.Value, e.IsTimeCasa, Dono = e.UsuarioId != null })
                    .ToListAsync(ct))
                .GroupBy(e => e.JogadorId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Dono).First().IsTimeCasa);
            bool EhCasa(Jogador j) => ladoEscalacao.TryGetValue(j.Id, out var casa)
                ? casa
                : j.TimeId == jogo.TimeCasaId || j.SelecaoId == jogo.TimeCasaId;

            List<MomentumHelper.Ponto>? pontos = null;
            string? fonte = null;

            pontos = await FotMobAsync(jogoId, ct);
            if (pontos != null) fonte = "fotmob";

            if (pontos == null && usuarioId != null)
            {
                var marcacoes = await _context.MarcacoesVideo.AsNoTracking()
                    .Include(m => m.Jogador)
                    .Where(m => m.JogoId == jogoId && m.UsuarioId == usuarioId && m.MinutoJogo != null)
                    .ToListAsync(ct);
                var acoes = marcacoes
                    .Where(m => MomentumHelper.PesoAcao.ContainsKey(m.AcaoId))
                    .Select(m => new MomentumHelper.Acao(m.MinutoJogo!.Value, EhCasa(m.Jogador), MomentumHelper.PesoAcao[m.AcaoId]))
                    .ToList();
                if (acoes.Count >= MinAcoesVideo)
                {
                    pontos = MomentumHelper.DeAcoes(acoes);
                    fonte = "video";
                }
            }

            if (pontos == null || pontos.Count == 0)
                return new
                {
                    vazio = "Sem curva de momentum para este jogo. Ela vem do FotMob quando a competição é coberta por lá, "
                          + "ou da marcação por vídeo (Marcar vídeo) com os minutos do jogo ancorados.",
                };

            var gols = await _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => g.JogoId == jogoId)
                .ToListAsync(ct);
            var vermelhos = await _context.Cartoes.AsNoTracking()
                .Include(c => c.Jogador)
                .Where(c => c.JogoId == jogoId && c.Tipo == "Vermelho")
                .ToListAsync(ct);

            var eventos = gols
                // Gol contra conta para o outro lado.
                .Select(g => new { minuto = g.Minuto, casa = EhCasa(g.Jogador) != g.Contra, tipo = "gol",
                                   texto = $"{g.Minuto}' Gol de {g.Jogador.Nome}{(g.Contra ? " (contra)" : "")}" })
                .Concat(vermelhos.Select(c => new { minuto = c.Minuto, casa = EhCasa(c.Jogador), tipo = "vermelho",
                                   texto = $"{c.Minuto}' Expulsão de {c.Jogador.Nome}" }))
                .OrderBy(e => e.minuto)
                .ToList();

            var dominio = MomentumHelper.DominioCasa(pontos);
            return new
            {
                fonte,
                fonteTxt = fonte == "fotmob" ? "Curva do FotMob" : "Calculada da sua marcação por vídeo",
                casa = jogo.TimeCasa?.Nome,
                visitante = jogo.TimeVisitante?.Nome,
                pontos = pontos.Select(p => new { m = p.Minuto, v = p.Valor }).ToList(),
                eventos,
                dominioCasa = dominio,
                dominioVisitante = dominio.HasValue ? 100 - dominio : null,
                periodos = MomentumHelper.Periodos(pontos)
                    .Select(p => new { inicio = (int)Math.Floor(p.Inicio), fim = (int)Math.Ceiling(p.Fim), casa = p.Casa })
                    .ToList(),
            };
        }

        /// <summary>
        /// Curva publicada pelo FotMob, ou null quando a competição não está lá, a
        /// partida não foi achada ou não tem momentum. Falha de rede não derruba a aba:
        /// a marcação por vídeo ainda pode responder.
        /// </summary>
        private async Task<List<MomentumHelper.Ponto>?> FotMobAsync(int jogoId, CancellationToken ct)
        {
            try
            {
                var (_, doc, _, erro) = await _fotmob.AbrirPartidaAsync(_context, jogoId, exigeIdApi: false, ct);
                if (erro != null || doc == null) return null;
                using (doc) return MomentumHelper.LerFotMob(doc.RootElement);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _logger.LogWarning(ex, "[Momentum] FotMob indisponível para o jogo {JogoId}.", jogoId);
                return null;
            }
        }
    }
}
