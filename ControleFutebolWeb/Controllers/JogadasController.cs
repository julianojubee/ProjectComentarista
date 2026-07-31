using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Prancheta tática de jogadas: o usuário desenha, passo a passo, como o time
    /// constrói um lance (goleiro → lateral → cruzamento → finalização) e o player
    /// da tela anima a sequência interpolando as posições entre os passos.
    ///
    /// Cada jogada nasce dentro da análise de um jogo (modal "Jogadas" de
    /// /Jogos/Analisar) mas guarda também o TimeId, o que a devolve no arsenal de
    /// /Times/Details — o acervo de jogadas daquele time em todas as análises.
    ///
    /// Só JSON: o desenho e a animação são inteiramente client-side (jogadas.js).
    /// </summary>
    public class JogadasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JogadasController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Tetos de tamanho do storyboard. Não são regra de negócio: são o limite do
        // que faz sentido animar (e o que impede um POST forjado de gravar um blob
        // gigante numa coluna text).
        private const int MaxPassos = 40;
        private const int MaxPecas = 36;   // os 22 em campo, com folga para reservas
        private const int MaxSetas = 20;
        private const int MaxLegenda = 120;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // ── DTOs do storyboard (formato documentado em Models/Jogada.cs) ──────

        public class PontoDto
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class PecaDto : PontoDto
        {
            public int Id { get; set; }
        }

        public class SetaDto
        {
            public double X1 { get; set; }
            public double Y1 { get; set; }
            public double X2 { get; set; }
            public double Y2 { get; set; }
        }

        public class PassoDto
        {
            public string? Legenda { get; set; }
            public PontoDto? Bola { get; set; }
            public List<PecaDto> Pecas { get; set; } = new();
            public List<SetaDto> Setas { get; set; } = new();
        }

        public class ElencoDto
        {
            public int Id { get; set; }
            public string? Num { get; set; }
            public string? Nome { get; set; }
            public string? Sigla { get; set; }

            // Peça da marcação adversária (o outro time da partida). Ausente nas
            // jogadas gravadas antes disso existir, quando só o time atacante
            // entrava em campo — false é a leitura certa para elas.
            public bool Adv { get; set; }
        }

        public class StoryboardDto
        {
            public int V { get; set; } = 1;
            public int Ms { get; set; } = 900;
            public List<ElencoDto> Elenco { get; set; } = new();
            public List<PassoDto> Passos { get; set; } = new();
        }

        public class SalvarJogadaRequest
        {
            public int? Id { get; set; }
            public int JogoId { get; set; }
            public int TimeId { get; set; }
            public string? Nome { get; set; }
            public string? Descricao { get; set; }
            public StoryboardDto? Storyboard { get; set; }
        }

        public class ExcluirJogadaRequest
        {
            public int Id { get; set; }
        }

        // ── Leitura ──────────────────────────────────────────────────────────

        // GET: /Jogadas/DoJogo/5 — abre o modal de jogadas da análise: os dois times
        // com elenco arrastável, a escalação atual (semente do primeiro passo) e as
        // jogadas já salvas.
        [HttpGet]
        public async Task<IActionResult> DoJogo(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (jogo == null) return NotFound();

            var jogadas = await _context.Jogadas
                .AsNoTracking()
                .Where(j => j.JogoId == id && j.UsuarioId == uid)
                .OrderBy(j => j.Ordem).ThenBy(j => j.Id)
                .ToListAsync();

            return Json(new
            {
                casa = await MontarLadoAsync(jogo, ehCasa: true, uid, jogadas),
                visitante = await MontarLadoAsync(jogo, ehCasa: false, uid, jogadas)
            });
        }

        // GET: /Jogadas/DoTime/5 — arsenal do time em /Times/Details: todas as jogadas
        // que o usuário desenhou para ele, em qualquer análise. Só leitura (o storyboard
        // carrega o próprio elenco, ver Models/Jogada.cs).
        [HttpGet]
        public async Task<IActionResult> DoTime(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogadas = await _context.Jogadas
                .AsNoTracking()
                .Include(j => j.Jogo).ThenInclude(g => g.TimeCasa)
                .Include(j => j.Jogo).ThenInclude(g => g.TimeVisitante)
                .Where(j => j.TimeId == id && j.UsuarioId == uid)
                .OrderByDescending(j => j.AtualizadoEm)
                .ToListAsync();

            return Json(jogadas.Select(j =>
            {
                var adversario = j.Jogo.TimeCasaId == id ? j.Jogo.TimeVisitante?.Nome : j.Jogo.TimeCasa?.Nome;
                return new
                {
                    id = j.Id,
                    nome = j.Nome,
                    descricao = j.Descricao,
                    jogoId = j.JogoId,
                    adversario,
                    data = j.Jogo.Data?.ToString("dd/MM/yyyy"),
                    atualizadoEm = j.AtualizadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    storyboard = LerStoryboard(j.PassosJson)
                };
            }));
        }

        // ── Escrita ──────────────────────────────────────────────────────────

        // POST: /Jogadas/Salvar — cria ou sobrescreve a jogada inteira (o editor
        // manda sempre o storyboard completo, não um diff).
        [HttpPost]
        public async Task<IActionResult> Salvar([FromBody] SalvarJogadaRequest req)
        {
            if (req?.Storyboard == null) return BadRequest("Dados inválidos.");

            var uid = _userManager.GetUserId(User)!;

            var jogo = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.Id == req.JogoId);
            if (jogo == null) return NotFound();

            // O time tem que ser um dos dois da partida: a jogada é o lance de quem
            // está em campo, e é isso que faz o arsenal do time bater com o jogo.
            if (req.TimeId != jogo.TimeCasaId && req.TimeId != jogo.TimeVisitanteId)
                return BadRequest("O time não participa deste jogo.");

            var storyboard = Normalizar(req.Storyboard);
            if (storyboard.Passos.Count == 0)
                return BadRequest("A jogada precisa de pelo menos um passo.");

            var nome = string.IsNullOrWhiteSpace(req.Nome) ? "Jogada sem nome" : req.Nome.Trim();
            if (nome.Length > 80) nome = nome[..80];
            var descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
            if (descricao?.Length > 400) descricao = descricao[..400];

            var agora = DateTime.UtcNow;
            Jogada jogada;

            if (req.Id is > 0)
            {
                jogada = (await _context.Jogadas
                    .FirstOrDefaultAsync(j => j.Id == req.Id && j.UsuarioId == uid))!;
                if (jogada == null) return NotFound();
            }
            else
            {
                var ordemMax = await _context.Jogadas
                    .Where(j => j.JogoId == req.JogoId && j.TimeId == req.TimeId && j.UsuarioId == uid)
                    .Select(j => (int?)j.Ordem).MaxAsync() ?? 0;

                jogada = new Jogada
                {
                    JogoId = req.JogoId,
                    TimeId = req.TimeId,
                    UsuarioId = uid,
                    Ordem = ordemMax + 1,
                    CriadoEm = agora
                };
                _context.Jogadas.Add(jogada);
            }

            jogada.Nome = nome;
            jogada.Descricao = descricao;
            jogada.PassosJson = JsonSerializer.Serialize(storyboard, JsonOpts);
            jogada.AtualizadoEm = agora;

            await _context.SaveChangesAsync();

            return Ok(new { id = jogada.Id, nome = jogada.Nome, descricao = jogada.Descricao, storyboard });
        }

        // POST: /Jogadas/Excluir
        [HttpPost]
        public async Task<IActionResult> Excluir([FromBody] ExcluirJogadaRequest req)
        {
            var uid = _userManager.GetUserId(User)!;

            var jogada = await _context.Jogadas
                .FirstOrDefaultAsync(j => j.Id == req.Id && j.UsuarioId == uid);
            if (jogada == null) return NotFound();

            _context.Jogadas.Remove(jogada);
            await _context.SaveChangesAsync();
            return Ok(new { sucesso = true });
        }

        // ── Montagem do payload por lado ─────────────────────────────────────

        private async Task<object> MontarLadoAsync(Jogo jogo, bool ehCasa, string uid, List<Jogada> jogadas)
        {
            var time = ehCasa ? jogo.TimeCasa : jogo.TimeVisitante;
            var timeId = ehCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;

            // Elenco arrastável: todo mundo do time (em seleção o vínculo é SelecaoId,
            // TimeId aponta pro clube — mesmo critério do Match-up).
            bool ehSelecao = time?.EhSelecao == true;
            var elenco = await _context.Jogadores
                .AsNoTracking()
                .Where(j => j.TimeId == timeId || (ehSelecao && j.SelecaoId == timeId))
                .OrderBy(j => j.NumeroCamisa == null).ThenBy(j => j.NumeroCamisa).ThenBy(j => j.Nome)
                .Select(j => new
                {
                    id = j.Id,
                    num = j.NumeroCamisa != null ? j.NumeroCamisa.ToString() : "",
                    nome = j.Nome,
                    sigla = j.Posicao
                })
                .ToListAsync();

            // Escalação titular deste jogo (a do usuário vence a importada da API),
            // convertida para o campo horizontal: é a semente do primeiro passo, para
            // a jogada já começar com o time posicionado.
            var escalacoes = await _context.Escalacoes
                .AsNoTracking()
                .Include(e => e.Jogador)
                .Where(e => e.JogoId == jogo.Id && e.Titular && e.IsTimeCasa == ehCasa
                         && e.JogadorId != null
                         && (e.UsuarioId == uid || e.UsuarioId == null)
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null))
                .ToListAsync();

            var escalacao = escalacoes
                .GroupBy(e => e.JogadorId)
                .Select(g => g.OrderBy(e => e.UsuarioId == null ? 1 : 0).First())
                .Select(e =>
                {
                    var (x, y) = ParaCampoHorizontal(e.PosicaoX, e.PosicaoY);
                    return new
                    {
                        id = e.Jogador!.Id,
                        num = e.Jogador.NumeroCamisa?.ToString() ?? "",
                        nome = e.Jogador.Nome,
                        sigla = PosicaoJogadorHelper.Sigla(e.Posicao),
                        x,
                        y
                    };
                })
                .OrderBy(e => e.x)
                .ToList();

            return new
            {
                timeId,
                nome = time?.Nome ?? "",
                escudo = string.IsNullOrEmpty(time?.EscudoUrl)
                    ? null
                    : Url.Action("Imagem", "MediaProxy", new { url = time!.EscudoUrl }),
                elenco = elenco.Select(j => new
                {
                    j.id,
                    j.num,
                    j.nome,
                    sigla = PosicaoJogadorHelper.Sigla(j.sigla)
                }),
                escalacao,
                jogadas = jogadas.Where(j => j.TimeId == timeId).Select(j => new
                {
                    id = j.Id,
                    nome = j.Nome,
                    descricao = j.Descricao,
                    storyboard = LerStoryboard(j.PassosJson)
                })
            };
        }

        // Campo da tela Analisar (vertical: X é a lateral, Y vai de 0 no ataque a 100
        // no próprio gol — ver MatchUpHelper) → campo da prancheta (horizontal, o time
        // sempre atacando da esquerda para a direita).
        //
        // As duas faixas são comprimidas de propósito: a peça é ancorada pelo centro e
        // leva sigla acima e nome abaixo do círculo, então encostar na borda cortaria o
        // texto no overflow do campo.
        private static (double X, double Y) ParaCampoHorizontal(double posicaoX, double posicaoY)
        {
            double x = 5 + (100 - Math.Clamp(posicaoY, 0, 100)) * 0.90;
            double y = 8 + Math.Clamp(posicaoX, 0, 100) * 0.84;
            return (Math.Round(x, 2), Math.Round(y, 2));
        }

        // ── Storyboard: leitura tolerante e normalização na gravação ─────────

        // Jogada gravada por uma versão anterior (ou linha corrompida na mão) não pode
        // derrubar a listagem inteira — devolve um storyboard vazio e a jogada aparece
        // sem passos em vez de estourar 500.
        private static StoryboardDto LerStoryboard(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new StoryboardDto();
            try
            {
                return JsonSerializer.Deserialize<StoryboardDto>(json, JsonOpts) ?? new StoryboardDto();
            }
            catch (JsonException)
            {
                return new StoryboardDto();
            }
        }

        private static StoryboardDto Normalizar(StoryboardDto s)
        {
            static double C(double v) => Math.Round(Math.Clamp(v, 0, 100), 2);

            return new StoryboardDto
            {
                V = 1,
                Ms = Math.Clamp(s.Ms == 0 ? 900 : s.Ms, 200, 5000),
                Elenco = s.Elenco.Take(MaxPecas).Select(e => new ElencoDto
                {
                    Id = e.Id,
                    Num = Cortar(e.Num, 3),
                    Nome = Cortar(e.Nome, 60),
                    Sigla = Cortar(e.Sigla, 6),
                    Adv = e.Adv
                }).ToList(),
                Passos = s.Passos.Take(MaxPassos).Select(p => new PassoDto
                {
                    Legenda = Cortar(p.Legenda, MaxLegenda),
                    Bola = p.Bola == null ? null : new PontoDto { X = C(p.Bola.X), Y = C(p.Bola.Y) },
                    Pecas = p.Pecas.Take(MaxPecas)
                        .Select(c => new PecaDto { Id = c.Id, X = C(c.X), Y = C(c.Y) }).ToList(),
                    Setas = p.Setas.Take(MaxSetas)
                        .Select(t => new SetaDto { X1 = C(t.X1), Y1 = C(t.Y1), X2 = C(t.X2), Y2 = C(t.Y2) }).ToList()
                }).ToList()
            };
        }

        private static string? Cortar(string? v, int max)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            v = v.Trim();
            return v.Length > max ? v[..max] : v;
        }
    }
}
