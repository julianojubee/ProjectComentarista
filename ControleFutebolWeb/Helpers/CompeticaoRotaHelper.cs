using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Página de uma competição: as que têm tela própria (Brasileirão, Copa do Brasil,
    /// Copa do Mundo, Libertadores, Sul-Americana, Champions) vão para ela; o resto, para
    /// Competicoes/Detalhes. Mesma regra do menu do _Layout e de Competicoes/Index.
    /// </summary>
    public static class CompeticaoRotaHelper
    {
        public static string Url(IUrlHelper url, int id, string? nome)
        {
            var n = nome?.ToLowerInvariant() ?? "";
            // Só o Brasileirão masculino (Id 1) tem a tabela dedicada
            if (id == 1) return url.Action("Brasileirao", "Tabela")!;
            if (n.Contains("copa do brasil")) return url.Action("Index", "CopaBrasil")!;
            if (n.Contains("copa do mundo")) return url.Action("Index", "CopaDoMundo")!;
            if (n.Contains("libertadores")) return url.Action("Index", "Libertadores")!;
            if (n.Contains("sul-americana") || n.Contains("sudamericana") || n.Contains("sul americana"))
                return url.Action("Index", "SulAmericana")!;
            if (n.Contains("champions")) return url.Action("Index", "ChampionsLeague")!;
            return url.Action("Detalhes", "Competicoes", new { id })!;
        }
    }
}
