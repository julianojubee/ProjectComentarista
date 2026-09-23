namespace ControleFutebolWeb.Models.Campeonatos
{
    // Módulos que um usuário pode assinar (ApplicationUser.Modulos). Flags porque
    // o plano "completo" é a soma dos dois — o filtro de assinatura pergunta
    // "tem o módulo X?" e não "qual é o plano?".
    [Flags]
    public enum ModuloSistema
    {
        Nenhum = 0,
        Analise = 1,       // o sistema atual: jogos reais, análise, notas, relatórios
        Campeonatos = 2,   // campeonatos, times e jogadores próprios
        Completo = Analise | Campeonatos
    }

    // Só muda textos e padrões da tela (ex.: videogame pergunta "qual jogo?" e
    // mostra o time usado em cada partida). As regras de tabela são as mesmas.
    public enum ModalidadeCampeonato
    {
        VideoGame,
        Amador,
        Outro
    }

    public enum StatusCampeonato
    {
        Rascunho,     // montando participantes; ainda sem partidas geradas
        EmAndamento,  // partidas geradas — participantes ficam travados
        Encerrado
    }

    public enum TipoEventoPartida
    {
        Gol,
        GolContra,
        Assistencia,
        CartaoAmarelo,
        CartaoVermelho
    }
}
