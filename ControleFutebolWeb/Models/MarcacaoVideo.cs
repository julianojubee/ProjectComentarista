namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Uma ação marcada a mão sobre o vídeo da partida, em /MarcacaoVideo/Marcar:
    /// o usuário assiste ao jogo, digita o número da camisa e aperta a tecla da
    /// ação ("10" + P = passe certo da camisa 10). Cada tecla vira uma linha destas.
    ///
    /// É evento cru, não estatística: a tabela guarda o lance individual (com o
    /// segundo do vídeo, para poder voltar e conferir) e a consolidação
    /// (MarcacaoVideoService) é que soma tudo numa linha de
    /// <see cref="EstatisticaJogador"/> com Fonte = <see cref="FonteEstatistica.Video"/>.
    /// Guardar o evento é o que permite desfazer, revisar e reconsolidar sem
    /// remarcar o jogo inteiro.
    ///
    /// Existe porque há competição sem estatística em fonte nenhuma — o Mundial
    /// Sub-20 feminino é o caso que motivou a tela. Sem linha em EstatisticasJogador
    /// não há nota automática (ver RatingAutomaticoService), e a marcação manual é
    /// o único caminho que produz um dado confiável para esses jogos.
    ///
    /// Por usuário: duas pessoas podem marcar o mesmo jogo sem misturar as
    /// contagens, e cada uma consolida a sua.
    /// </summary>
    public class MarcacaoVideo
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser? Usuario { get; set; }

        // Id da ação no catálogo — ver AcoesMarcacaoVideo.Todas. É ele que diz
        // quais campos de EstatisticaJogador a marcação incrementa.
        public string AcaoId { get; set; } = "";

        // Onde a ação acontece no vídeo, em segundos desde o começo do arquivo.
        // Serve para o revisor pular direto para o lance e conferir a marcação.
        public int SegundoVideo { get; set; }

        // Minuto de JOGO (não de vídeo), quando a tela conseguiu calcular: o
        // marcador ancora o início de cada tempo e o resto sai por diferença.
        // Null quando as âncoras não foram definidas — aí a consolidação cai no
        // padrão de 90 minutos em vez de inventar entrada e saída.
        public int? MinutoJogo { get; set; }

        public DateTime CriadoEm { get; set; }
    }
}
