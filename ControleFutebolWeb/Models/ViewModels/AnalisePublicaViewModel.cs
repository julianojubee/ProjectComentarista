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
    }
}
