using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    // Espelho enxuto de NotasController + parte de JogosController.MarcarAnalisado,
    // para o app mobile avaliar jogadores durante a partida. Mesma semântica de
    // salvamento (delete + recreate da nota do usuário) e mesma fórmula de nota
    // final (CriteriosNotaHelper.NotaBaseFixa/NotaMinima).
    [Route("api/v1/analises")]
    public class AnalisesApiController : ApiControllerBase
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnalisesApiController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET api/v1/analises/criterios
        [HttpGet("criterios")]
        public async Task<ActionResult<IEnumerable<CriterioNotaDto>>> Criterios()
        {
            var uid = _userManager.GetUserId(User);

            var compartilhados = await _context.CriteriosNota.AsNoTracking()
                .Where(c => c.UsuarioId == null).ToListAsync();
            var doUsuario = await _context.CriteriosNota.AsNoTracking()
                .Where(c => c.UsuarioId == uid).ToListAsync();

            var criterios = CriteriosNotaHelper.MergeCriterios(compartilhados, doUsuario)
                .Select(c => new CriterioNotaDto { AcaoId = c.AcaoId, Label = c.Label, Peso = c.Peso, Ordem = c.Ordem });

            return Ok(criterios);
        }

        // GET api/v1/analises/jogo/5
        [HttpGet("jogo/{jogoId:int}")]
        public async Task<ActionResult<AnaliseJogoDto>> ObterAnalise(int jogoId)
        {
            var jogoExiste = await _context.Jogos.AsNoTracking().AnyAsync(j => j.Id == jogoId);
            if (!jogoExiste) return NotFound();

            var uid = _userManager.GetUserId(User);

            var status = await _context.JogosAnalisadosUsuario.AsNoTracking()
                .FirstOrDefaultAsync(j => j.JogoId == jogoId && j.UsuarioId == uid);

            var notas = await _context.Notas.AsNoTracking()
                .Include(n => n.Detalhes)
                .Where(n => n.JogoId == jogoId && n.UsuarioId == uid)
                .ToListAsync();

            return Ok(new AnaliseJogoDto
            {
                JogoId = jogoId,
                AnalisadoPorMim = status?.Analisado ?? false,
                Observacoes = status?.Observacoes,
                Notas = notas.Select(ParaNotaDto).ToList()
            });
        }

        // PUT api/v1/analises/jogo/5/nota
        [HttpPut("jogo/{jogoId:int}/nota")]
        public async Task<ActionResult<NotaJogadorDto>> SalvarNota(int jogoId, [FromBody] SalvarNotaApiRequest request)
        {
            if (request.JogadorId <= 0) return BadRequest("JogadorId inválido.");
            if (request.NotaManual.HasValue && (request.NotaManual < 0 || request.NotaManual > 10))
                return BadRequest("NotaManual deve estar entre 0 e 10.");

            var jogoExiste = await _context.Jogos.AnyAsync(j => j.Id == jogoId);
            if (!jogoExiste) return NotFound("Jogo não encontrado.");
            var jogadorExiste = await _context.Jogadores.AnyAsync(j => j.Id == request.JogadorId);
            if (!jogadorExiste) return NotFound("Jogador não encontrado.");

            var uid = _userManager.GetUserId(User);

            // Mesma semântica do NotasController.Salvar da web: apaga a nota anterior
            // do usuário para esse jogador/jogo e recria do zero.
            var notaExistente = await _context.Notas
                .Include(n => n.Detalhes)
                .FirstOrDefaultAsync(n => n.JogadorId == request.JogadorId
                                       && n.JogoId == jogoId
                                       && n.UsuarioId == uid);

            if (notaExistente != null)
            {
                _context.NotaDetalhes.RemoveRange(notaExistente.Detalhes);
                _context.Notas.Remove(notaExistente);
            }

            var nota = new Nota
            {
                JogadorId = request.JogadorId,
                JogoId = jogoId,
                Valor = request.Total,
                Comentario = request.Observacao ?? "",
                NotaManual = request.NotaManual,
                UsuarioId = uid
            };

            _context.Notas.Add(nota);
            await _context.SaveChangesAsync();

            foreach (var d in request.Detalhes)
            {
                _context.NotaDetalhes.Add(new Notadetalhe
                {
                    NotaId = nota.Id,
                    AcaoId = d.AcaoId,
                    AcaoLabel = d.AcaoLabel,
                    Quantidade = d.Quantidade,
                    Peso = d.Peso
                });
            }

            await _context.SaveChangesAsync();

            // Recarrega com os detalhes para devolver o DTO completo
            var salva = await _context.Notas.AsNoTracking()
                .Include(n => n.Detalhes)
                .FirstAsync(n => n.Id == nota.Id);

            return Ok(ParaNotaDto(salva));
        }

        // DELETE api/v1/analises/jogo/5/nota/10
        [HttpDelete("jogo/{jogoId:int}/nota/{jogadorId:int}")]
        public async Task<IActionResult> ExcluirNota(int jogoId, int jogadorId)
        {
            var uid = _userManager.GetUserId(User);

            var nota = await _context.Notas
                .Include(n => n.Detalhes)
                .FirstOrDefaultAsync(n => n.JogoId == jogoId && n.JogadorId == jogadorId && n.UsuarioId == uid);

            if (nota == null) return NotFound();

            _context.NotaDetalhes.RemoveRange(nota.Detalhes);
            _context.Notas.Remove(nota);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT api/v1/analises/jogo/5/status
        [HttpPut("jogo/{jogoId:int}/status")]
        public async Task<ActionResult<AnaliseJogoDto>> AtualizarStatus(int jogoId, [FromBody] StatusAnaliseRequest request)
        {
            var jogoExiste = await _context.Jogos.AnyAsync(j => j.Id == jogoId);
            if (!jogoExiste) return NotFound();

            var uid = _userManager.GetUserId(User)!;

            var registro = await _context.JogosAnalisadosUsuario
                .FirstOrDefaultAsync(j => j.JogoId == jogoId && j.UsuarioId == uid);

            if (registro == null)
            {
                registro = new JogoAnalisadoUsuario
                {
                    JogoId = jogoId,
                    UsuarioId = uid,
                    Analisado = request.Analisado,
                    Observacoes = request.Observacoes
                };
                _context.JogosAnalisadosUsuario.Add(registro);
            }
            else
            {
                registro.Analisado = request.Analisado;
                if (request.Observacoes != null)
                    registro.Observacoes = request.Observacoes;
            }

            await _context.SaveChangesAsync();

            return Ok(new AnaliseJogoDto
            {
                JogoId = jogoId,
                AnalisadoPorMim = registro.Analisado,
                Observacoes = registro.Observacoes
            });
        }

        // GET api/v1/analises/jogo/5/preenchimento/10
        // Estatísticas importadas da api-football para pré-preencher a contagem de
        // ações no app — mesmo espírito de NotasController.BuscarEstatisticas, mas
        // já devolvendo só as ações com quantidade > 0.
        [HttpGet("jogo/{jogoId:int}/preenchimento/{jogadorId:int}")]
        public async Task<ActionResult<PreenchimentoDto>> Preenchimento(int jogoId, int jogadorId)
        {
            var e = await _context.EstatisticasJogador.AsNoTracking()
                .FirstOrDefaultAsync(x => x.JogoId == jogoId && x.JogadorId == jogadorId);

            if (e == null) return Ok(new PreenchimentoDto { Encontrado = false });

            var quantidades = new Dictionary<string, int>
            {
                ["offside"] = e.Offsides,
                ["finalizacao"] = e.FinalizacoesTotal,
                ["finalizacao_gol"] = e.FinalizacoesNoGol,
                ["gol"] = e.Gols,
                ["gol_sofrido"] = e.GolsSofridos,
                ["assistencia"] = e.Assistencias,
                ["defesa"] = e.Defesas,
                ["passe_chave"] = e.PassesChave,
                ["desarme"] = e.Desarmes,
                ["bloqueio"] = e.Bloqueios,
                ["interceptacao"] = e.Interceptacoes,
                ["duelo_vencido"] = e.DuelosVencidos,
                ["drible_certo"] = e.DriblesCertos,
                ["drible_sofrido"] = e.DriblesSofridos,
                ["falta_sofrida"] = e.FaltasSofridas,
                ["falta_cometida"] = e.FaltasCometidas,
                ["cartao_amarelo"] = e.CartoesAmarelos,
                ["cartao_vermelho"] = e.CartoesVermelhos,
                ["penalti_sofrido"] = e.PenaltiSofrido,
                ["penalti_cometido"] = e.PenaltiCometido,
                ["penalti_perdido"] = e.PenaltiPerdido,
                ["penalti_defendido"] = e.PenaltiDefendido,
            };

            return Ok(new PreenchimentoDto
            {
                Encontrado = true,
                Minutos = e.Minutos,
                Rating = e.Rating,
                QuantidadesPorAcao = quantidades.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value)
            });
        }

        private static NotaJogadorDto ParaNotaDto(Nota n)
        {
            double notaFinal = n.NotaManual.HasValue
                ? Math.Round(Math.Max(0, Math.Min(10, n.NotaManual.Value)), 2)
                : Math.Round(Math.Max(CriteriosNotaHelper.NotaMinima, Math.Min(10, CriteriosNotaHelper.NotaBaseFixa + n.Valor)), 2);

            return new NotaJogadorDto
            {
                JogadorId = n.JogadorId,
                Total = n.Valor,
                NotaManual = n.NotaManual,
                Comentario = n.Comentario,
                NotaFinal = notaFinal,
                Detalhes = n.Detalhes.Select(d => new NotaDetalheDto
                {
                    AcaoId = d.AcaoId,
                    AcaoLabel = d.AcaoLabel,
                    Quantidade = d.Quantidade,
                    Peso = d.Peso
                }).ToList()
            };
        }
    }
}
