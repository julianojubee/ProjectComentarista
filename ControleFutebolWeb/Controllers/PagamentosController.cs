using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Controle manual de pagamentos (PIX etc.): o admin registra cada pagamento
    // recebido e o sistema calcula o vencimento (ApplicationUser.AcessoPagoAte).
    // Usuário não-admin com vencimento no passado é bloqueado pelo AssinaturaFilter.
    [Authorize(Policy = "Admin")]
    public class PagamentosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<PagamentosController> _logger;
        private readonly PixOptions _pixOptions;

        public PagamentosController(
            FutebolContext context,
            ILogger<PagamentosController> logger,
            Microsoft.Extensions.Options.IOptions<PixOptions> pixOptions)
        {
            _context = context;
            _logger = logger;
            _pixOptions = pixOptions.Value;
        }

        // GET: /Pagamentos
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Users.AsNoTracking()
                .OrderBy(u => u.IsAdmin)
                .ThenBy(u => u.Nome)
                .ToListAsync();

            var pagamentos = await _context.PagamentosUsuario.AsNoTracking()
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.DataPagamento)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var porUsuario = pagamentos.GroupBy(p => p.UsuarioId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var model = new PagamentosIndexViewModel
            {
                Usuarios = usuarios.Select(u => new UsuarioPagamentoViewModel
                {
                    Usuario = u,
                    UltimoPagamento = porUsuario.TryGetValue(u.Id, out var lista) ? lista.First() : null,
                    TotalPago = porUsuario.TryGetValue(u.Id, out var lista2) ? lista2.Sum(p => p.Valor) : 0m
                }).ToList(),
                Lancamentos = pagamentos
            };

            ViewBag.ValorPadrao = _pixOptions.ValorPadrao;
            ViewBag.PixConfigurado = !string.IsNullOrWhiteSpace(_pixOptions.Chave);
            return View(model);
        }

        // POST: /Pagamentos/Registrar
        // Grava o lançamento e atualiza o vencimento do usuário para pagoAte.
        [HttpPost]
        public async Task<IActionResult> Registrar(string usuarioId, DateTime dataPagamento, decimal valor, DateTime pagoAte, string? observacao)
        {
            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == usuarioId);
            if (usuario == null)
            {
                TempData["Erro"] = "Usuário não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            if (valor <= 0)
            {
                TempData["Erro"] = "O valor do pagamento deve ser maior que zero.";
                return RedirectToAction(nameof(Index));
            }

            var pagamento = new PagamentoUsuario
            {
                UsuarioId = usuario.Id,
                DataPagamento = AncorarMeioDiaUtc(dataPagamento),
                PagoAte = AncorarMeioDiaUtc(pagoAte),
                Valor = valor,
                Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
                DataRegistro = DateTime.UtcNow
            };

            usuario.AcessoPagoAte = pagamento.PagoAte;
            _context.PagamentosUsuario.Add(pagamento);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Pagamentos] Registrado: {Valor:0.00} de {Usuario} em {Data:yyyy-MM-dd}, pago até {PagoAte:yyyy-MM-dd}.",
                valor, usuario.UserName, pagamento.DataPagamento, pagamento.PagoAte);

            TempData["Sucesso"] = $"Pagamento de {usuario.Nome} registrado — acesso liberado até {pagamento.PagoAte:dd/MM/yyyy}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Pagamentos/Excluir
        // Remove um lançamento errado e recalcula o vencimento do usuário a partir
        // dos lançamentos restantes (maior PagoAte; sem lançamentos → sem cobrança).
        [HttpPost]
        public async Task<IActionResult> Excluir(int id)
        {
            var pagamento = await _context.PagamentosUsuario
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pagamento == null)
            {
                TempData["Erro"] = "Lançamento não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            _context.PagamentosUsuario.Remove(pagamento);

            var novoVencimento = await _context.PagamentosUsuario
                .Where(p => p.UsuarioId == pagamento.UsuarioId && p.Id != id)
                .MaxAsync(p => (DateTime?)p.PagoAte);
            pagamento.Usuario.AcessoPagoAte = novoVencimento;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Pagamentos] Excluído lançamento {Id} de {Usuario}; vencimento recalculado para {Venc}.",
                id, pagamento.Usuario.UserName, novoVencimento?.ToString("yyyy-MM-dd") ?? "(sem cobrança)");

            TempData["Sucesso"] = novoVencimento.HasValue
                ? $"Lançamento excluído — vencimento de {pagamento.Usuario.Nome} recalculado para {novoVencimento:dd/MM/yyyy}."
                : $"Lançamento excluído — {pagamento.Usuario.Nome} ficou sem cobrança (acesso livre).";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Pagamentos/DefinirVencimento
        // Ajuste manual do vencimento e da mensalidade, sem lançamento de pagamento
        // (ex.: iniciar a cobrança de um usuário, dar dias de cortesia). vencimento
        // vazio = remover a cobrança (acesso livre); mensalidade vazia = usar o
        // valor padrão (Pix:ValorPadrao) no QR da tela de bloqueio.
        [HttpPost]
        public async Task<IActionResult> DefinirVencimento(string usuarioId, DateTime? vencimento, decimal? valorMensalidade)
        {
            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == usuarioId);
            if (usuario == null)
            {
                TempData["Erro"] = "Usuário não encontrado.";
                return RedirectToAction(nameof(Index));
            }

            usuario.AcessoPagoAte = vencimento.HasValue ? AncorarMeioDiaUtc(vencimento.Value) : null;
            usuario.ValorMensalidade = valorMensalidade > 0 ? valorMensalidade : null;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Pagamentos] Vencimento de {Usuario} definido manualmente para {Venc}.",
                usuario.UserName, usuario.AcessoPagoAte?.ToString("yyyy-MM-dd") ?? "(sem cobrança)");

            TempData["Sucesso"] = usuario.AcessoPagoAte.HasValue
                ? $"Vencimento de {usuario.Nome} definido para {usuario.AcessoPagoAte:dd/MM/yyyy}."
                : $"Cobrança de {usuario.Nome} removida — acesso livre.";
            return RedirectToAction(nameof(Index));
        }

        // Datas puras ancoradas ao meio-dia UTC (meia-noite desloca o dia no fuso -3).
        private static DateTime AncorarMeioDiaUtc(DateTime data) =>
            DateTime.SpecifyKind(data.Date.AddHours(12), DateTimeKind.Utc);
    }
}
