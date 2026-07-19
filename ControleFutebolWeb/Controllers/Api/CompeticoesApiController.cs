using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/competicoes")]
    public class CompeticoesApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;

        public CompeticoesApiController(FutebolContext context)
        {
            _context = context;
        }

        // GET api/v1/competicoes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CompeticaoDto>>> Listar()
        {
            var competicoes = await _context.Competicoes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();
            return Ok(competicoes.Select(ParaDto));
        }

        // GET api/v1/competicoes/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CompeticaoDto>> Detalhe(int id)
        {
            var competicao = await _context.Competicoes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (competicao == null) return NotFound();

            return Ok(ParaDto(competicao));
        }

        // GET api/v1/competicoes/5/classificacao?temporada=
        // Tabela de pontos corridos calculada dos jogos com placar — mesma regra e
        // ordem de desempate da /Tabela/Brasileirao (pontos > vitórias > saldo >
        // gols pró), genérica para qualquer competição. Em mata-mata funciona como
        // um quadro de campanha.
        [HttpGet("{id:int}/classificacao")]
        public async Task<ActionResult<ClassificacaoDto>> Classificacao(int id, int? temporada)
        {
            var competicao = await _context.Competicoes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (competicao == null) return NotFound();

            var temporadasDisponiveis = await _context.Jogos
                .Where(j => j.CompeticaoId == id)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t)
                .ToListAsync();

            int? temporadaSel = temporada
                ?? (temporadasDisponiveis.Any() ? temporadasDisponiveis.First() : (int?)null);

            // WithIdentityResolution: o GroupBy da tabela agrupa por instância de Time —
            // com AsNoTracking puro cada Include cria objetos distintos para o mesmo
            // clube e cada linha viraria um "time" separado na classificação.
            var jogos = await _context.Jogos
                .AsNoTrackingWithIdentityResolution()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == id
                         && (temporadaSel == null || j.Temporada == temporadaSel)
                         && j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToListAsync();

            // Jogos de mata-mata/playoffs não entram na tabela de pontos corridos
            // (mesma regra da tela /Competicoes/Detalhes). Em competição MATA_MATA pura
            // (por tipo ou porque todas as fases declaradas são MATA_MATA) mantém tudo:
            // a tabela funciona como quadro de campanha.
            var fasesDeclaradas = await _context.CompeticaoFases.AsNoTracking()
                .Where(f => f.CompeticaoId == id)
                .OrderBy(f => f.Ordem).ThenBy(f => f.Id)
                .ToListAsync();

            if (fasesDeclaradas.Any(f => f.Tipo != "MATA_MATA"))
            {
                var jogosPorFase = FaseJogoClassifier.DistribuirPorFases(fasesDeclaradas, jogos);
                var faseTabela = fasesDeclaradas.First(f => f.Tipo != "MATA_MATA");
                jogos = jogosPorFase[faseTabela.Id];
            }
            else if (!fasesDeclaradas.Any() && competicao.Tipo != "MATA_MATA")
            {
                jogos = jogos
                    .Where(j => FaseJogoClassifier.Classificar(j.Grupo) != FaseCategoria.MataMata)
                    .ToList();
            }

            var tabela = jogos
                .SelectMany(j => new[]
                {
                    new { Time = j.TimeCasa, GolsPro = j.PlacarCasa!.Value, GolsContra = j.PlacarVisitante!.Value },
                    new { Time = j.TimeVisitante, GolsPro = j.PlacarVisitante!.Value, GolsContra = j.PlacarCasa!.Value }
                })
                .Where(x => x.Time != null)
                .GroupBy(x => x.Time!)
                .Select(g => new ClassificacaoLinhaDto
                {
                    TimeId = g.Key.Id,
                    TimeNome = g.Key.Nome,
                    EscudoUrl = Url.FotoSrcAbsoluto(Request, g.Key.EscudoUrl),
                    Jogos = g.Count(),
                    Pontos = g.Sum(x => x.GolsPro > x.GolsContra ? 3 : x.GolsPro == x.GolsContra ? 1 : 0),
                    Vitorias = g.Count(x => x.GolsPro > x.GolsContra),
                    Empates = g.Count(x => x.GolsPro == x.GolsContra),
                    Derrotas = g.Count(x => x.GolsPro < x.GolsContra),
                    GolsPro = g.Sum(x => x.GolsPro),
                    GolsContra = g.Sum(x => x.GolsContra),
                    SaldoGols = g.Sum(x => x.GolsPro) - g.Sum(x => x.GolsContra)
                })
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.Vitorias)
                .ThenByDescending(t => t.SaldoGols)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            for (int i = 0; i < tabela.Count; i++)
                tabela[i].Posicao = i + 1;

            // Artilheiros (top 5), como na página da tabela
            var topScorers = await _context.Gols
                .Where(g => !g.Contra && g.Jogo.CompeticaoId == id
                         && (temporadaSel == null || g.Jogo.Temporada == temporadaSel))
                .GroupBy(g => g.JogadorId)
                .Select(gr => new { JogadorId = gr.Key, Gols = gr.Count() })
                .OrderByDescending(x => x.Gols)
                .Take(5)
                .ToListAsync();

            var idsArtilheiros = topScorers.Select(t => t.JogadorId).ToList();
            var jogadores = await _context.Jogadores
                .AsNoTracking()
                .Include(j => j.Time)
                .Where(j => idsArtilheiros.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id);

            var artilheiros = topScorers
                .Where(t => jogadores.ContainsKey(t.JogadorId))
                .Select(t => new ArtilheiroDto
                {
                    JogadorId = t.JogadorId,
                    Nome = jogadores[t.JogadorId].Nome,
                    TimeNome = jogadores[t.JogadorId].Time?.Nome,
                    FotoUrl = Url.FotoSrcAbsoluto(Request, jogadores[t.JogadorId].FotoUrl),
                    Gols = t.Gols
                })
                .ToList();

            return Ok(new ClassificacaoDto
            {
                CompeticaoId = competicao.Id,
                CompeticaoNome = competicao.Nome,
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis,
                Tabela = tabela,
                Artilheiros = artilheiros
            });
        }

        private static CompeticaoDto ParaDto(Competicao c) => new()
        {
            Id = c.Id,
            Nome = c.Nome,
            Regiao = c.Regiao,
            Tipo = c.Tipo,
            EhSelecaoNacional = c.EhSelecaoNacional,
            TopTier = c.TopTier,
            LogoUrl = c.LogoUrl
        };
    }
}
