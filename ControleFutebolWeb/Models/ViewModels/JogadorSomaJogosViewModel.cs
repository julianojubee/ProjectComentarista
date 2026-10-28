namespace ControleFutebolWeb.Models.ViewModels
{
    // Página /Jogadores/SomaJogos: o usuário escolhe jogos em que o jogador atuou
    // (os últimos N, os contra um adversário, os de uma competição...) e vê a soma
    // das estatísticas dele nesse recorte. É a /Jogadores/EstatisticasJogo para
    // vários jogos de uma vez.
    public class JogadorSomaJogosViewModel
    {
        public Jogador Jogador { get; set; } = null!;

        // Todos os jogos em que ele entrou em campo, do mais recente ao mais antigo.
        public List<JogoSomaItem> Jogos { get; set; } = new();

        // Os marcados para a soma (subconjunto de Jogos, mesma ordem).
        public List<JogoSomaItem> Selecionados { get; set; } = new();

        // Linhas de estatística importada dos jogos selecionados (uma por jogo, no máximo).
        public List<EstatisticaJogador> Estatisticas { get; set; } = new();

        // Posições salvas nos jogos selecionados (fases + destinos de seta).
        public List<PontoHeatmap> PontosHeatmap { get; set; } = new();

        // Média da nota do site nos jogos selecionados que têm nota.
        public double? MediaNota { get; set; }
        public int JogosComNota { get; set; }
    }

    // Um jogo da lista de escolha, já do ponto de vista do jogador.
    public class JogoSomaItem
    {
        public Jogo Jogo { get; set; } = null!;
        public bool IsCasa { get; set; }
        public Time? Adversario => IsCasa ? Jogo.TimeVisitante : Jogo.TimeCasa;

        public int? GolsPro => IsCasa ? Jogo.PlacarCasa : Jogo.PlacarVisitante;
        public int? GolsContra => IsCasa ? Jogo.PlacarVisitante : Jogo.PlacarCasa;
        // "V", "E", "D" ou "?" (sem placar)
        public string Resultado => GolsPro is not int p || GolsContra is not int c ? "?"
            : p > c ? "V" : p < c ? "D" : "E";

        public int? Minutos { get; set; }
        public bool Titular { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public double? Nota { get; set; }
    }
}
