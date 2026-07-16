using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/jogadores")]
    public class JogadoresApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogadoresApiController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET api/v1/jogadores?timeId=&posicao=&nome=&page=&pageSize=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<JogadorResumoDto>>> Listar(
            int? timeId, string? posicao, string? nome, int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .AsQueryable();

            if (timeId.HasValue)
                query = query.Where(j => j.TimeId == timeId.Value);
            if (!string.IsNullOrWhiteSpace(posicao))
                // Contains: posição pode ser composta ("Lateral Direito/Zagueiro")
                query = query.Where(j => j.Posicao.Contains(posicao));
            if (!string.IsNullOrWhiteSpace(nome))
                query = query.Where(j => j.Nome.ToLower().Contains(nome.ToLower()));

            var jogadores = await query
                .OrderBy(j => j.Nome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(jogadores.Select(ParaResumoDto));
        }

        // GET api/v1/jogadores/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<JogadorDetalheDto>> Detalhe(int id)
        {
            var jogador = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Include(j => j.Selecao)
                .Include(j => j.Nacionalidade)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogador == null) return NotFound();

            return Ok(new JogadorDetalheDto
            {
                Id = jogador.Id,
                Nome = jogador.Nome,
                NomeExibicao = jogador.NomeExibicao,
                Posicao = jogador.Posicao,
                Idade = jogador.Idade,
                NumeroCamisa = jogador.NumeroCamisa,
                NacionalidadeNome = jogador.Nacionalidade?.Nome,
                TimeId = jogador.TimeId,
                TimeNome = jogador.Time?.Nome,
                FotoUrl = Url.FotoSrcAbsoluto(Request, jogador.FotoUrl),
                DataNascimento = jogador.DataNascimento,
                SelecaoId = jogador.SelecaoId,
                SelecaoNome = jogador.Selecao?.Nome,
                LinkTransfermarket = jogador.LinkTransfermarket,
                Observacoes = jogador.Observacoes
            });
        }

        // GET api/v1/jogadores/5/estatisticas?competicaoId=
        // Versão enxuta da página /Jogadores/Estatisticas: totais + histórico por
        // jogo. Nota final por jogo segue a mesma regra de toda parte: manual é
        // override absoluto; senão base fixa + pontuação das estatísticas
        // importadas, clampada entre a mínima e 10.
        [HttpGet("{id:int}/estatisticas")]
        public async Task<ActionResult<JogadorEstatisticasDto>> Estatisticas(int id, int? competicaoId)
        {
            var uid = _userManager.GetUserId(User);

            var jogador = await _context.Jogadores.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
            if (jogador == null) return NotFound();

            // Competições em que o jogador aparece (dropdown de filtro no app)
            var competicaoIds = await _context.Notas
                .Where(n => n.JogadorId == id)
                .Select(n => n.Jogo.CompeticaoId)
                .Union(_context.EstatisticasJogador
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Union(_context.Escalacoes
                    .Where(e => e.JogadorId == id)
                    .Select(e => e.Jogo.CompeticaoId))
                .Distinct()
                .ToListAsync();

            var competicoes = await _context.Competicoes
                .AsNoTracking()
                .Where(c => competicaoIds.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .Select(c => new CompeticaoRefDto { Id = c.Id, Nome = c.Nome })
                .ToListAsync();

            // ── Fontes (mesmos filtros da página web) ─────────────────────
            var notasQuery = _context.Notas
                .AsNoTracking()
                .Include(n => n.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(n => n.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(n => n.Jogo).ThenInclude(j => j.Competicao)
                .Where(n => n.JogadorId == id && n.UsuarioId == uid);
            if (competicaoId.HasValue)
                notasQuery = notasQuery.Where(n => n.Jogo.CompeticaoId == competicaoId);
            var notas = await notasQuery.ToListAsync();

            // Minutos 0/null = reserva não utilizado (linha criada pela api-football
            // para o elenco inteiro) — fora, senão o jogo contaria sem ele ter jogado.
            var estatisticasQuery = _context.EstatisticasJogador
                .AsNoTracking()
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(e => e.Jogo).ThenInclude(j => j.Competicao)
                .Where(e => e.JogadorId == id && e.Minutos != null && e.Minutos > 0);
            if (competicaoId.HasValue)
                estatisticasQuery = estatisticasQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);
            var estatisticas = await estatisticasQuery.ToListAsync();

            var escalacoesQuery = _context.Escalacoes
                .AsNoTracking()
                .Include(e => e.Jogo).ThenInclude(j => j.TimeCasa)
                .Include(e => e.Jogo).ThenInclude(j => j.TimeVisitante)
                .Include(e => e.Jogo).ThenInclude(j => j.Competicao)
                .Where(e => e.JogadorId == id && (e.UsuarioId == uid || e.UsuarioId == null));
            if (competicaoId.HasValue)
                escalacoesQuery = escalacoesQuery.Where(e => e.Jogo.CompeticaoId == competicaoId);
            var escalacoes = await escalacoesQuery.ToListAsync();

            var golsQuery = _context.Gols.AsNoTracking().Where(g => g.JogadorId == id && !g.Contra);
            if (competicaoId.HasValue) golsQuery = golsQuery.Where(g => g.Jogo.CompeticaoId == competicaoId);
            var gols = await golsQuery.ToListAsync();

            var assistenciasQuery = _context.Assistencias.AsNoTracking().Where(a => a.JogadorId == id);
            if (competicaoId.HasValue) assistenciasQuery = assistenciasQuery.Where(a => a.Jogo.CompeticaoId == competicaoId);
            var assistencias = await assistenciasQuery.ToListAsync();

            var cartoesQuery = _context.Cartoes.AsNoTracking().Where(c => c.JogadorId == id);
            if (competicaoId.HasValue) cartoesQuery = cartoesQuery.Where(c => c.Jogo.CompeticaoId == competicaoId);
            var cartoes = await cartoesQuery.ToListAsync();

            var criteriosBanco = CriteriosNotaHelper.MergeCriterios(
                await _context.CriteriosNota.Where(c => c.UsuarioId == null).ToListAsync(),
                await _context.CriteriosNota.Where(c => c.UsuarioId == uid).ToListAsync());

            // ── Índices auxiliares ────────────────────────────────────────
            // Lado do jogador em cada jogo pela escalação da época (não o time atual,
            // que inverteria o histórico após transferência).
            var ladoPorJogoId = escalacoes
                .GroupBy(e => e.JogoId)
                .ToDictionary(g => g.Key, g => g.First().IsTimeCasa);

            var posicaoPorJogoId = escalacoes
                .Where(e => e.Titular && !string.IsNullOrWhiteSpace(e.Posicao) && e.Posicao != "RES")
                .GroupBy(e => e.JogoId)
                .Select(g => g
                    .OrderBy(e => e.UsuarioId == uid ? 0 : 1)
                    .ThenBy(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null ? 0 : 1)
                    .First())
                .ToDictionary(e => e.JogoId, e => e.Posicao);

            var minutosPorJogoId = estatisticas
                .Where(e => e.Minutos.HasValue)
                .ToDictionary(e => e.JogoId, e => e.Minutos!.Value);

            var jogosComNotaManualIds = notas.Select(n => n.JogoId).ToHashSet();
            var jogosComEstatisticaIds = estatisticas.Select(e => e.JogoId).ToHashSet();

            JogadorJogoItemDto MontarItem(Jogo jogo, bool analisado, double? notaFinal, bool origemManual)
            {
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;
                bool isCasa = ladoPorJogoId.TryGetValue(jogo.Id, out var ladoCasa)
                    ? ladoCasa
                    : (jogo.TimeCasaId == jogador.TimeId || jogo.TimeCasaId == jogador.SelecaoId);

                string resultado;
                if (!jogo.PlacarCasa.HasValue) resultado = "?";
                else if (pc == pv) resultado = "E";
                else if ((isCasa && pc > pv) || (!isCasa && pv > pc)) resultado = "V";
                else resultado = "D";

                var adversario = isCasa ? jogo.TimeVisitante : jogo.TimeCasa;

                return new JogadorJogoItemDto
                {
                    JogoId = jogo.Id,
                    Data = jogo.Data,
                    CompeticaoNome = jogo.Competicao?.Nome,
                    IsCasa = isCasa,
                    AdversarioNome = adversario?.Nome ?? string.Empty,
                    AdversarioEscudoUrl = Url.FotoSrcAbsoluto(Request, adversario?.EscudoUrl),
                    GolsPro = isCasa ? pc : pv,
                    GolsContra = isCasa ? pv : pc,
                    Resultado = resultado,
                    Posicao = posicaoPorJogoId.GetValueOrDefault(jogo.Id),
                    Minutos = minutosPorJogoId.TryGetValue(jogo.Id, out var min) ? min : null,
                    Gols = gols.Count(g => g.JogoId == jogo.Id),
                    Assistencias = assistencias.Count(a => a.JogoId == jogo.Id),
                    CartoesAmarelos = cartoes.Count(c => c.JogoId == jogo.Id && c.Tipo == "Amarelo"),
                    CartoesVermelhos = cartoes.Count(c => c.JogoId == jogo.Id && c.Tipo == "Vermelho"),
                    Analisado = analisado,
                    NotaFinal = notaFinal,
                    OrigemManual = origemManual,
                };
            }

            double ClampNota(double valorAcoes) => Math.Round(
                Math.Max(CriteriosNotaHelper.NotaMinima,
                    Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + valorAcoes)), 2);

            var itens = new List<JogadorJogoItemDto>();

            // Jogos com nota manual (override absoluto quando NotaManual preenchida)
            foreach (var n in notas)
            {
                double notaFinal = n.NotaManual.HasValue
                    ? Math.Round(Math.Max(0, Math.Min(10, n.NotaManual.Value)), 2)
                    : ClampNota(n.Valor);
                itens.Add(MontarItem(n.Jogo, analisado: true, notaFinal, origemManual: true));
            }

            // Jogos só com estatísticas importadas → nota automática
            foreach (var e in estatisticas.Where(e => !jogosComNotaManualIds.Contains(e.JogoId)))
                itens.Add(MontarItem(e.Jogo, analisado: true,
                    ClampNota(Math.Round(CriteriosNotaHelper.CalcularPontuacao(e, criteriosBanco), 2)),
                    origemManual: false));

            // Jogos em que só há escalação (não analisados)
            foreach (var e in escalacoes
                .Where(e => !jogosComNotaManualIds.Contains(e.JogoId) && !jogosComEstatisticaIds.Contains(e.JogoId))
                .GroupBy(e => e.JogoId)
                .Select(g => g.First()))
                itens.Add(MontarItem(e.Jogo, analisado: false, notaFinal: null, origemManual: false));

            itens = itens.OrderByDescending(i => i.Data).ToList();

            var analisadosComNota = itens.Where(i => i.Analisado && i.NotaFinal.HasValue).ToList();

            return Ok(new JogadorEstatisticasDto
            {
                JogadorId = jogador.Id,
                Nome = jogador.Nome,
                CompeticaoIdFiltro = competicaoId,
                Competicoes = competicoes,
                Partidas = itens.Count,
                Gols = gols.Count,
                Assistencias = assistencias.Count,
                CartoesAmarelos = cartoes.Count(c => c.Tipo == "Amarelo"),
                CartoesVermelhos = cartoes.Count(c => c.Tipo == "Vermelho"),
                Vitorias = itens.Count(i => i.Resultado == "V"),
                Empates = itens.Count(i => i.Resultado == "E"),
                Derrotas = itens.Count(i => i.Resultado == "D"),
                NotaMedia = analisadosComNota.Any()
                    ? Math.Round(analisadosComNota.Average(i => i.NotaFinal!.Value), 2)
                    : null,
                Jogos = itens,
            });
        }

        private JogadorResumoDto ParaResumoDto(Jogador j) => new()
        {
            Id = j.Id,
            Nome = j.Nome,
            NomeExibicao = j.NomeExibicao,
            Posicao = j.Posicao,
            Idade = j.Idade,
            NumeroCamisa = j.NumeroCamisa,
            NacionalidadeNome = j.Nacionalidade?.Nome,
            TimeId = j.TimeId,
            TimeNome = j.Time?.Nome,
            FotoUrl = Url.FotoSrcAbsoluto(Request, j.FotoUrl)
        };
    }
}
