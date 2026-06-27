using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class NotasController : Controller
    {
        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotasController(FutebolContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<IActionResult> Salvar([FromBody] SalvarNotaRequest request)
        {
            if (request == null || request.JogadorId <= 0 || request.JogoId <= 0)
                return BadRequest("Dados inválidos.");

            var usuarioId = _userManager.GetUserId(User);

            // Remove nota anterior do mesmo jogador/jogo/usuário se existir
            var notaExistente = await _context.Notas
                .Include(n => n.Detalhes)
                .FirstOrDefaultAsync(n => n.JogadorId == request.JogadorId
                                       && n.JogoId == request.JogoId
                                       && n.UsuarioId == usuarioId);

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
                Comentario = request.Observacao ?? "",
                NotaManual = request.NotaManual,
                UsuarioId = usuarioId
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

        // Estatísticas importadas da api-football para este jogador nesta partida —
        // usadas para pré-preencher o formulário de avaliação manual.
        [HttpGet]
        public async Task<IActionResult> BuscarEstatisticas(int jogoId, int jogadorId)
        {
            var e = await _context.EstatisticasJogador
                .FirstOrDefaultAsync(x => x.JogoId == jogoId && x.JogadorId == jogadorId);

            if (e == null) return Ok(new { encontrado = false });

            return Ok(new
            {
                encontrado = true,
                minutos = e.Minutos,
                rating = e.Rating,
                offside = e.Offsides,
                finalizacao = e.FinalizacoesTotal,
                finalizacao_gol = e.FinalizacoesNoGol,
                gol = e.Gols,
                gol_sofrido = e.GolsSofridos,
                assistencia = e.Assistencias,
                defesa = e.Defesas,
                passe_chave = e.PassesChave,
                desarme = e.Desarmes,
                bloqueio = e.Bloqueios,
                interceptacao = e.Interceptacoes,
                duelo_vencido = e.DuelosVencidos,
                drible_certo = e.DriblesCertos,
                drible_sofrido = e.DriblesSofridos,
                falta_sofrida = e.FaltasSofridas,
                falta_cometida = e.FaltasCometidas,
                cartao_amarelo = e.CartoesAmarelos,
                cartao_vermelho = e.CartoesVermelhos,
                penalti_sofrido = e.PenaltiSofrido,
                penalti_cometido = e.PenaltiCometido,
                penalti_perdido = e.PenaltiPerdido,
                penalti_defendido = e.PenaltiDefendido
            });
        }

        // Critérios ativos do banco — usados pelo modal de avaliação manual em Analisar.cshtml
        [HttpGet]
        public async Task<IActionResult> BuscarCriterios()
        {
            var uid = _userManager.GetUserId(User);
            var compartilhados = await _context.CriteriosNota
                .Where(c => c.UsuarioId == null).ToListAsync();
            var doUsuario = await _context.CriteriosNota
                .Where(c => c.UsuarioId == uid).ToListAsync();
            var criterios = CriteriosNotaHelper.MergeCriterios(compartilhados, doUsuario)
                .Select(c => new { id = c.AcaoId, label = c.Label, peso = c.Peso })
                .ToList();
            return Ok(criterios);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarPorJogo(int jogoId)
        {
            var usuarioId = _userManager.GetUserId(User);
            var notas = await _context.Notas
                .Include(n => n.Jogador)
                .Include(n => n.Detalhes)
                .Where(n => n.JogoId == jogoId && n.UsuarioId == usuarioId)
                .ToListAsync();

            var resultado = notas.Select(n => new
            {
                jogadorId = n.JogadorId,
                jogadorNome = n.Jogador.Nome,
                total = n.Valor,
                observacao = n.Comentario,
                notaManual = n.NotaManual,
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
        public double Total { get; set; }
        public string? Observacao { get; set; }
        public double? NotaManual { get; set; }
        public List<DetalheRequest> Detalhes { get; set; } = new();
    }

    public class DetalheRequest
    {
        public string AcaoId { get; set; } = "";
        public string AcaoLabel { get; set; } = "";
        public int Quantidade { get; set; }
        public double Peso { get; set; }
    }
}