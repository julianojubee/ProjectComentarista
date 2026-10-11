namespace ControleFutebolWeb.Models.ViewModels
{
    // Página /Jogadores/EstatisticasJogo: todas as ações e estatísticas importadas
    // de UM jogador em UM jogo específico, com o mapa de calor daquele jogo.
    // Complementa o histórico de /Jogadores/Estatisticas, que mostra só o resumo.
    public class JogadorEstatisticasJogoViewModel
    {
        public Jogador Jogador { get; set; } = null!;
        public Jogo Jogo { get; set; } = null!;

        // Lado do jogador NAQUELE jogo (da escalação da época — correto após transferência).
        public bool IsCasa { get; set; }

        // Posição escalada nesse jogo; null quando não há escalação registrada.
        public string? Posicao { get; set; }
        public bool Titular { get; set; }
        public bool TemEscalacao { get; set; }

        // Linha de estatísticas importadas para este jogo (pode não existir). A fonte
        // (Estatistica.Fonte) diz o que foi publicado — ver FonteEstatistica.Cobre.
        public EstatisticaJogador? Estatistica { get; set; }

        // Ações do jogador no jogo, em ordem de minuto.
        public List<EventoJogadorJogo> Eventos { get; set; } = new();

        // Posições registradas só deste jogo (fases salvas + destinos de seta).
        public List<PontoHeatmap> PontosHeatmap { get; set; } = new();

        // Nota do jogo na régua do site: a da análise do usuário (manual ou pelos
        // critérios) quando ele analisou; senão a automática sobre a estatística.
        public double? NotaSistema { get; set; }
        // "analise" ou "automatica" — de onde veio NotaSistema.
        public string? NotaOrigem { get; set; }
    }

    // Uma ação do jogador no jogo (gol, assistência, cartão, substituição, pênalti).
    public class EventoJogadorJogo
    {
        // Nulo em eventos sem minuto (ex.: cobrança da disputa de pênaltis).
        public int? Minuto { get; set; }
        // gol, gol-contra, assistencia, amarelo, vermelho, entrou, saiu,
        // penalti-perdido, penalti-convertido — vira ícone e cor na view.
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string? Detalhe { get; set; }
    }
}
