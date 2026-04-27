using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class NotasController : Controller
    {
        private readonly FutebolContext _context;

        public NotasController(FutebolContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Salvar([FromBody] SalvarNotaRequest request)
        {
            if (request == null || request.JogadorId <= 0 || request.JogoId <= 0)
                return BadRequest("Dados inválidos.");

            // Remove nota anterior do mesmo jogador/jogo se existir
            var notaExistente = await _context.Notas
                .Include(n => n.Detalhes)
                .FirstOrDefaultAsync(n => n.JogadorId == request.JogadorId && n.JogoId == request.JogoId);

            if (notaExistente != null)
            {
                _context.NotaDetalhes.RemoveRange(notaExistente.Detalhes);
                _context.Notas.Remove(notaExistente);
            }

            var nota = new Nota
            {
                JogadorId = request.JogadorId,
                JogoId = request.JogoId,
                Valor = request.Total,
                Comentario = request.Observacao ?? ""
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

            return Ok(new { sucesso = true, notaId = nota.Id });
        }

        [HttpGet]
        public async Task<IActionResult> BuscarPorJogo(int jogoId)
        {
            var notas = await _context.Notas
                .Include(n => n.Jogador)
                .Include(n => n.Detalhes)
                .Where(n => n.JogoId == jogoId)
                .ToListAsync();

            var resultado = notas.Select(n => new
            {
                jogadorId = n.JogadorId,
                jogadorNome = n.Jogador.Nome,
                total = n.Valor,
                observacao = n.Comentario,
                detalhes = n.Detalhes.Select(d => new
                {
                    acaoId = d.AcaoId,
                    acaoLabel = d.AcaoLabel,
                    quantidade = d.Quantidade,
                    peso = d.Peso
                })
            });

            return Ok(resultado);
        }
    }

    // DTO para receber o POST
    public class SalvarNotaRequest
    {
        public int JogadorId { get; set; }
        public int JogoId { get; set; }
        public int Total { get; set; }
        public string? Observacao { get; set; }
        public List<DetalheRequest> Detalhes { get; set; } = new();
    }

    public class DetalheRequest
    {
        public string AcaoId { get; set; } = "";
        public string AcaoLabel { get; set; } = "";
        public int Quantidade { get; set; }
        public int Peso { get; set; }
    }
}