using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ControleFutebolWeb.Filters
{
    // Conta a abertura das páginas públicas de /creators (ver AcessoPublicoService
    // e /Admin/Acessos). Aplicado no CreatorsController, não globalmente.
    //
    // Conta PÁGINA, não requisição: as chamadas de bastidor da mesma tela
    // (escalacao/dados, simulador/tabela, selecao/elenco…) ficam de fora, senão
    // um creator arrastando jogadores viraria dezenas de "acessos".
    //
    // Roda DEPOIS da ação e só se ela deu certo: 404 e erro não são visita.
    public class AcessoPublicoFilter : IAsyncActionFilter
    {
        private readonly AcessoPublicoService _acessos;

        public AcessoPublicoFilter(AcessoPublicoService acessos)
        {
            _acessos = acessos;
        }

        // Ação → nome curto da ferramenta no relatório. O que não está aqui não conta.
        private static readonly Dictionary<string, string> Paginas = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Index"] = "hub",
            ["Escalacao"] = "escalacao",
            ["Selecao"] = "selecao",
            ["Tabelas"] = "tabelas",
            ["JogosHoje"] = "jogos-hoje",
            ["Simulador"] = "simulador",
        };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executado = await next();

            if (executado.Exception != null && !executado.ExceptionHandled) return;
            if (context.ActionDescriptor is not ControllerActionDescriptor descritor) return;
            if (!Paginas.TryGetValue(descritor.ActionName, out var ferramenta)) return;

            // Só a página que realmente apareceu: NotFound/Redirect não é visita.
            if (executado.Result is not Microsoft.AspNetCore.Mvc.ViewResult) return;

            await _acessos.RegistrarAsync(ferramenta, context.HttpContext);
        }
    }
}
