namespace ControleFutebolWeb.Models.ViewModels
{
    // Dados mínimos da página pública /analise/{token}. Só o suficiente para o
    // cabeçalho e para o JS saber de onde puxar o resto — placar, notas e
    // observações vêm dos endpoints do próprio token, nunca do HTML inicial.
    public class AnalisePublicaViewModel
    {
        public string Token { get; set; } = "";

        // "Bolívar x São Paulo" — usado no <title> e no cabeçalho.
        public string Confronto { get; set; } = "";

        public string? AutorNome { get; set; }

        // Cores de camisa do jogo: alimentam as variáveis CSS que pintam os
        // números dos jogadores na lista e no campinho (mesmas de /Jogos/Analisar).
        public string CorCamisaCasa { get; set; } = "dc3545";
        public string CorNumeroCasa { get; set; } = "ffffff";
        public string CorCamisaVisitante { get; set; } = "0d6efd";
        public string CorNumeroVisitante { get; set; } = "ffffff";

        // Modo apresentação (?tv=...): layout sem moldura e tipografia grande,
        // para gravar vídeo da tela. "h" = 16:9, "v" = 9:16, null = página normal.
        public string? ModoTv { get; set; }
        public bool EhTv => ModoTv != null;
        public bool TvVertical => ModoTv == "v";

        // Aba já aberta ao carregar (?tab=): evita gravar o clique inicial e
        // permite mandar o link direto para o trecho que interessa.
        public string AbaInicial { get; set; } = "notas";
    }
}
