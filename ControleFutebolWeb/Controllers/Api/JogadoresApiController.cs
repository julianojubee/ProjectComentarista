using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/jogadores")]
    public class JogadoresApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;

        public JogadoresApiController(FutebolContext context)
        {
            _context = context;
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
                query = query.Where(j => j.Posicao == posicao);
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
