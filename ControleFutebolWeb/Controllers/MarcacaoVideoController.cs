using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Marcação de estatística assistindo ao vídeo da partida: o usuário abre o jogo
    /// ao lado da lista de jogadoras, digita o número da camisa e aperta a tecla da
    /// ação ("10" + P = passe certo da camisa 10). Cada tecla vira um
    /// <see cref="MarcacaoVideo"/>; no fim, "Consolidar" soma tudo numa linha de
    /// EstatisticaJogador (MarcacaoVideoService) e a nota automática passa a existir.
    ///
    /// Existe pelas competições que fonte nenhuma cobre — o Mundial Sub-20 feminino
    /// é o caso que motivou a tela. Para essas, marcar a mão é a ÚNICA forma de ter
    /// estatística; para as demais, o import continua sendo o caminho e a
    /// consolidação preserva o que veio da API por padrão.
    /// </summary>
    // Restrito ao admin enquanto é protótipo: a consolidação escreve em
    // EstatisticasJogador, que é dado do JOGO e não do usuário — a linha gravada
    // aqui vale para todo mundo que abrir a análise daquela partida.
    [Authorize(Policy = "Admin")]
    public class MarcacaoVideoController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MarcacaoVideoService _marcacoes;
        private readonly ILogger<MarcacaoVideoController> _log;

        public MarcacaoVideoController(FutebolContext context, UserManager<ApplicationUser> userManager,
            MarcacaoVideoService marcacoes, ILogger<MarcacaoVideoController> log)
        {
            _context = context;
            _userManager = userManager;
            _marcacoes = marcacoes;
            _log = log;
        }

        // Teto de marcações por jogo. Um jogo marcado com afinco fica na casa de 1.500
        // a 2.500 eventos; o limite existe só para um POST em loop não encher a tabela.
        private const int MaxMarcacoesPorJogo = 20000;

        private string UsuarioId => _userManager.GetUserId(User)!;

        // ── Escolha do jogo ──────────────────────────────────────────────────

        // GET: /MarcacaoVideo — lista os jogos mais recentes para escolher qual marcar.
        public async Task<IActionResult> Index(string? q)
        {
            var consulta = _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = q.Trim();
                consulta = consulta.Where(j =>
                    EF.Functions.ILike(j.TimeCasa.Nome, $"%{termo}%") ||
                    EF.Functions.ILike(j.TimeVisitante.Nome, $"%{termo}%") ||
                    (j.Competicao != null && EF.Functions.ILike(j.Competicao.Nome, $"%{termo}%")));
            }

            ViewBag.Busca = q;

            // Quantas marcações o usuário já tem em cada jogo da lista, para a tela
            // mostrar de cara onde ele parou.
            var jogos = await consulta
                .OrderByDescending(j => j.Data)
                .Take(60)
                .ToListAsync();

            var ids = jogos.Select(j => j.Id).ToList();
            var usuarioId = UsuarioId;
            ViewBag.MarcacoesPorJogo = await _context.MarcacoesVideo.AsNoTracking()
                .Where(m => m.UsuarioId == usuarioId && ids.Contains(m.JogoId))
                .GroupBy(m => m.JogoId)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Total);

            return View(jogos);
        }

        // ── Tela de marcação ─────────────────────────────────────────────────

        // GET: /MarcacaoVideo/Marcar/5
        public async Task<IActionResult> Marcar(int id)
        {
            var jogo = await _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var usuarioId = UsuarioId;
            var escalacoes = await _context.Escalacoes.AsNoTracking()
                .Where(e => e.JogoId == id && e.UsuarioId == usuarioId && e.FaseEscalacao == "INICIAL"
                         && e.JogadorId != null)
                .Select(e => new { JogadorId = e.JogadorId!.Value, e.Titular, e.IsTimeCasa })
                .ToListAsync();

            var vm = new MarcacaoVideoViewModel
            {
                Jogo = jogo,
                ElencoCasa = await ElencoAsync(jogo.TimeCasaId,
                    escalacoes.Where(e => e.IsTimeCasa).ToDictionary(e => e.JogadorId, e => e.Titular)),
                ElencoVisitante = await ElencoAsync(jogo.TimeVisitanteId,
                    escalacoes.Where(e => !e.IsTimeCasa).ToDictionary(e => e.JogadorId, e => e.Titular)),
                Acoes = AcoesMarcacaoVideo.Todas
                    .Select(a => new AcaoMarcacaoVm(a.Id, a.Rotulo, a.Grupo, a.Tecla, a.Shift, a.EhEstatistica))
                    .ToList(),
                Marcacoes = (await _marcacoes.ListarAsync(id, usuarioId))
                    .Select(m => new MarcacaoVm(m.Id, m.JogadorId, m.AcaoId, m.SegundoVideo, m.MinutoJogo))
                    .ToList(),
                TemEstatisticaDeOutraFonte = await _context.EstatisticasJogador.AsNoTracking()
                    .AnyAsync(e => e.JogoId == id && e.Fonte != FonteEstatistica.Video),
            };

            return View(vm);
        }

        /// <summary>
        /// Quem pode ser marcada por um time: o elenco cadastrado (clube ou seleção)
        /// mais quem o usuário escalou. Vai o elenco inteiro, e não só a escalação,
        /// porque na competição sem cobertura a escalação frequentemente não foi
        /// importada — e sem lista não há o que marcar.
        /// </summary>
        private async Task<List<JogadoraMarcacao>> ElencoAsync(int timeId, Dictionary<int, bool> escalados)
        {
            var jogadores = await _context.Jogadores.AsNoTracking()
                .Where(j => j.TimeId == timeId || j.SelecaoId == timeId)
                .ToListAsync();

            // Quem foi escalado mas não está no elenco do time (empréstimo, cadastro
            // desencontrado) entraria de fora da lista e ficaria sem como ser marcada.
            var faltantes = escalados.Keys.Except(jogadores.Select(j => j.Id)).ToList();
            if (faltantes.Count > 0)
                jogadores.AddRange(await _context.Jogadores.AsNoTracking()
                    .Where(j => faltantes.Contains(j.Id)).ToListAsync());

            return jogadores
                .Select(j => new JogadoraMarcacao(
                    j.Id, j.Nome, j.NumeroCamisa, j.Posicao,
                    Titular: escalados.GetValueOrDefault(j.Id),
                    Reserva: escalados.ContainsKey(j.Id) && !escalados[j.Id]))
                // Sem número por último: o marcador procura pelo número, e quem não
                // tem só é alcançável no clique.
                .OrderBy(j => j.Numero == null)
                .ThenBy(j => j.Numero)
                .ThenBy(j => j.Nome)
                .ToList();
        }

        // ── API da tela ──────────────────────────────────────────────────────

        public class RegistrarRequest
        {
            public int JogoId { get; set; }
            public int JogadorId { get; set; }
            public string AcaoId { get; set; } = "";
            public int SegundoVideo { get; set; }
            public int? MinutoJogo { get; set; }
        }

        // POST: /MarcacaoVideo/Registrar — uma tecla apertada.
        // Devolve o id para o "desfazer" da tela poder apagar exatamente esta linha.
        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] RegistrarRequest req)
        {
            if (!AcoesMarcacaoVideo.Existe(req.AcaoId))
                return BadRequest("Ação desconhecida.");

            var usuarioId = UsuarioId;

            // O jogador tem que existir e o jogo também — o POST vem de teclado e um
            // número digitado errado não pode virar linha órfã.
            if (!await _context.Jogos.AnyAsync(j => j.Id == req.JogoId))
                return NotFound("Jogo não encontrado.");
            if (!await _context.Jogadores.AnyAsync(j => j.Id == req.JogadorId))
                return NotFound("Jogadora não encontrada.");

            var total = await _context.MarcacoesVideo
                .CountAsync(m => m.JogoId == req.JogoId && m.UsuarioId == usuarioId);
            if (total >= MaxMarcacoesPorJogo)
                return BadRequest("Limite de marcações deste jogo atingido.");

            var marcacao = new MarcacaoVideo
            {
                JogoId = req.JogoId,
                JogadorId = req.JogadorId,
                UsuarioId = usuarioId,
                AcaoId = req.AcaoId,
                SegundoVideo = Math.Max(0, req.SegundoVideo),
                // Minuto negativo ou absurdo vira "não sei": melhor consolidar com o
                // padrão de 90 do que com uma janela de minutos inventada.
                MinutoJogo = req.MinutoJogo is >= 0 and <= 150 ? req.MinutoJogo : null,
                CriadoEm = DateTime.UtcNow,
            };

            _context.MarcacoesVideo.Add(marcacao);
            await _context.SaveChangesAsync();

            return Json(new { id = marcacao.Id });
        }

        public class ExcluirRequest
        {
            public int Id { get; set; }
        }

        // POST: /MarcacaoVideo/Excluir — o "desfazer" (Backspace) e o X da linha do tempo.
        [HttpPost]
        public async Task<IActionResult> Excluir([FromBody] ExcluirRequest req)
        {
            var usuarioId = UsuarioId;
            var marcacao = await _context.MarcacoesVideo
                .FirstOrDefaultAsync(m => m.Id == req.Id && m.UsuarioId == usuarioId);

            // Já não existe: o desfazer é idempotente de propósito, porque a tela
            // dispara em rajada e o usuário não tem o que fazer com esse erro.
            if (marcacao == null) return Json(new { ok = true });

            _context.MarcacoesVideo.Remove(marcacao);
            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        // GET: /MarcacaoVideo/DoJogo/5 — a linha do tempo, para recarregar sem F5.
        [HttpGet]
        public async Task<IActionResult> DoJogo(int id)
        {
            var lista = await _marcacoes.ListarAsync(id, UsuarioId);
            return Json(lista.Select(m => new MarcacaoVm(m.Id, m.JogadorId, m.AcaoId, m.SegundoVideo, m.MinutoJogo)));
        }

        // GET: /MarcacaoVideo/Previa/5 — o que a consolidação gravaria, sem gravar.
        [HttpGet]
        public async Task<IActionResult> Previa(int id)
        {
            var linhas = await _marcacoes.PreviaAsync(id, UsuarioId);
            var ids = linhas.Select(l => l.Estatistica.JogadorId).ToList();
            var nomes = await _context.Jogadores.AsNoTracking()
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => new { j.Nome, j.NumeroCamisa });

            return Json(linhas.Select(l =>
            {
                var e = l.Estatistica;
                var j = nomes.GetValueOrDefault(e.JogadorId);
                return new
                {
                    jogadorId = e.JogadorId,
                    nome = j?.Nome ?? $"#{e.JogadorId}",
                    numero = j?.NumeroCamisa,
                    minutos = e.Minutos,
                    minutosAssumidos = l.MinutosAssumidos,
                    passes = e.PassesTotal,
                    passesCertos = e.PassesCertos,
                    precisaoPasses = e.PrecisaoPasses,
                    passesChave = e.PassesChave,
                    finalizacoes = e.FinalizacoesTotal,
                    finalizacoesNoGol = e.FinalizacoesNoGol,
                    gols = e.Gols,
                    assistencias = e.Assistencias,
                    desarmes = e.Desarmes,
                    interceptacoes = e.Interceptacoes,
                    bloqueios = e.Bloqueios,
                    duelos = e.DuelosTotal,
                    duelosVencidos = e.DuelosVencidos,
                    dribles = e.DriblesTentados,
                    driblesCertos = e.DriblesCertos,
                    defesas = e.Defesas,
                    faltasCometidas = e.FaltasCometidas,
                    cartoesAmarelos = e.CartoesAmarelos,
                    cartoesVermelhos = e.CartoesVermelhos,
                };
            }).OrderByDescending(x => x.minutos));
        }

        public class ConsolidarRequest
        {
            public int JogoId { get; set; }
            public bool SobrescreverOutrasFontes { get; set; }
        }

        // POST: /MarcacaoVideo/Consolidar — marcações viram EstatisticaJogador.
        [HttpPost]
        public async Task<IActionResult> Consolidar([FromBody] ConsolidarRequest req)
        {
            var usuarioId = UsuarioId;
            var resultado = await _marcacoes.ConsolidarAsync(
                req.JogoId, usuarioId, req.SobrescreverOutrasFontes);

            _log.LogInformation(
                "Marcação por vídeo consolidada: jogo {JogoId}, {Gravadas} linha(s), {Puladas} preservada(s).",
                req.JogoId, resultado.Gravadas, resultado.Puladas.Count);

            return Json(new
            {
                gravadas = resultado.Gravadas,
                puladas = resultado.Puladas,
                minutosAssumidos = resultado.MinutosAssumidos,
            });
        }
    }
}
