using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Página de uma competição: as que têm tela própria (Brasileirão, Copa do Brasil,
    /// Copa do Mundo, Libertadores, Sul-Americana, Champions) vão para ela; o resto, para
    /// Competicoes/Detalhes. Usado pelo menu do _Layout, por Competicoes/Index, Jogos/Index
    /// e pelos cards de Jogos de hoje (_JogosDoDia), que passam a temporada do jogo.
    /// </summary>
    public static class CompeticaoRotaHelper
    {
        // A regra é pelo Id, não pelo nome: cada tela dedicada lê os jogos de um
        // CompeticaoId fixo no controller. Casar por nome mandava "Libertadores
        // Feminina", "Champions League Women" etc. para a tela da masculina, que
        // mostrava a tabela da masculina.
        // temporada: abre a tela já na temporada pedida (todas aceitam ?temporada=);
        // sem ela, cada tela escolhe a mais recente.
        public static string Url(IUrlHelper url, int id, string? nome = null, int? temporada = null) => id switch
        {
            1 => url.Action("Brasileirao", "Tabela", new { temporada })!,
            2 => url.Action("Index", "Libertadores", new { temporada })!,
            3 => url.Action("Index", "CopaBrasil", new { temporada })!,
            4 => url.Action("Index", "SulAmericana", new { temporada })!,
            6 => url.Action("Index", "ChampionsLeague", new { temporada })!,
            7 => url.Action("Index", "CopaDoMundo", new { temporada })!,
            _ => url.Action("Detalhes", "Competicoes", new { id, temporada })!
        };

        private static readonly int[] ComTelaPropria = { 1, 2, 3, 4, 6, 7 };

        // Mesma regra para o JavaScript: {"1":"/Tabela/Brasileirao",...} só com as de
        // tela própria. O JS usa o mapa e cai em /Competicoes/Detalhes/{id} no resto.
        public static string MapaTelasPropriasJson(IUrlHelper url) =>
            System.Text.Json.JsonSerializer.Serialize(ComTelaPropria.ToDictionary(id => id.ToString(), id => Url(url, id)));
    }
}
