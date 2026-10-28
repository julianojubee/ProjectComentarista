using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ControleFutebolWeb.Filters
{
    // Conta a abertura das páginas públicas de /creators e de /NBA (ver
    // AcessoPublicoService e /Admin/Acessos). Aplicado nesses controllers, não
    // globalmente.
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

        // "Controller.Ação" → nome curto da ferramenta no relatório. O que não está
        // aqui não conta. A chave leva o controller porque os dois têm um Index.
        // As da NBA ganham o prefixo "nba-" para a tela agrupar por área.
        private static readonly Dictionary<string, string> Paginas = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Creators.Index"] = "hub",
            ["Creators.Escalacao"] = "escalacao",
            ["Creators.Selecao"] = "selecao",
            ["Creators.Tabelas"] = "tabelas",
            ["Creators.JogosHoje"] = "jogos-hoje",
            ["Creators.Simulador"] = "simulador",
            ["Nba.Index"] = "nba-jogos",
            ["Nba.Classificacao"] = "nba-classificacao",
            ["Nba.Estatisticas"] = "nba-estatisticas",
            ["Nba.Jogo"] = "nba-jogo",
            ["Nba.Time"] = "nba-time",
            ["Nba.Jogador"] = "nba-jogador",
        };

        public static string? Ferramenta(string controller, string acao) =>
            Paginas.GetValueOrDefault($"{controller}.{acao}");

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executado = await next();

            if (executado.Exception != null && !executado.ExceptionHandled) return;
            if (context.ActionDescriptor is not ControllerActionDescriptor descritor) return;
            if (Ferramenta(descritor.ControllerName, descritor.ActionName) is not string ferramenta) return;

            // Só a página que realmente apareceu: NotFound/Redirect não é visita, nem
            // o "dados indisponíveis" da NBA quando a ESPN não responde.
            if (executado.Result is not Microsoft.AspNetCore.Mvc.ViewResult view || view.ViewName == "Indisponivel") return;

            await _acessos.RegistrarAsync(ferramenta, context.HttpContext);
        }
    }
}
