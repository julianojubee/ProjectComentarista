namespace ControleFutebolWeb.Models.Nba
{
    // Página /NBA/Jogador/{id}. Mesma regra dos outros modelos da NBA: montado da
    // resposta da ESPN, só em cache, nada no banco.

    public record NbaEstatResumo(string Sigla, string Nome, string Valor, string? Ranking);

    public record NbaPerfilJogador(
        string Id,
        string Nome,
        string? Camisa,
        string? Posicao,
        NbaTimeResumo? Time,
        string? Foto,
        string? Altura,
        string? Peso,
        int? Idade,
        string? Nascimento,
        string? LocalNascimento,
        string? Draft,
        string? Experiencia,
        bool Ativo,
        string? TituloResumo,
        IReadOnlyList<NbaEstatResumo> Resumo);

    public record NbaOpcaoTemporada(int Ano, string Rotulo);

    /// <summary>Linha genérica: rótulo ("Carreira", "Em casa"…) e os valores na ordem das colunas.</summary>
    public record NbaLinhaRotulada(string Rotulo, IReadOnlyList<string> Valores);

    public record NbaPremio(string Nome, string Quantidade, IReadOnlyList<string> Temporadas);

    public record NbaPassagem(string? TimeId, string Nome, string? Logo, string Periodo);

    /// <summary>Um jogo no game log, visto do lado do jogador.</summary>
    public record NbaJogoAtleta(
        string EventoId,
        DateTime DataUtc,
        bool EmCasa,
        string? AdversarioId,
        string AdversarioSigla,
        string? AdversarioLogo,
        string? Resultado,
        string? Placar,
        IReadOnlyList<string> Valores);

    public record NbaVisaoGeralJogador(
        IReadOnlyList<string> ColunasMedias,
        IReadOnlyList<NbaLinhaRotulada> Medias,
        IReadOnlyList<string> ColunasUltimos,
        IReadOnlyList<NbaJogoAtleta> UltimosJogos,
        IReadOnlyList<NbaPremio> Premios,
        IReadOnlyList<NbaPassagem> Times);

    public record NbaMesJogos(string Nome, IReadOnlyList<NbaJogoAtleta> Jogos, IReadOnlyList<string> Medias);

    public record NbaGameLog(
        string Temporada,
        IReadOnlyList<string> Colunas,
        IReadOnlyList<NbaMesJogos> Meses,
        IReadOnlyList<NbaLinhaRotulada> ResumoTemporada,
        IReadOnlyList<NbaLinhaRotulada> ResumoPlayoffs,
        IReadOnlyList<NbaOpcaoTemporada> Temporadas);

    public record NbaLinhaCarreira(string Temporada, NbaTimeResumo? Time, IReadOnlyList<string> Valores);

    public record NbaTabelaCarreira(
        string Chave,
        string Titulo,
        IReadOnlyList<string> Colunas,
        IReadOnlyList<NbaLinhaCarreira> Linhas,
        IReadOnlyList<string> Totais);

    public record NbaGrupoRecorte(string Nome, IReadOnlyList<NbaLinhaRotulada> Linhas);

    public record NbaRecortes(
        string Temporada,
        IReadOnlyList<string> Colunas,
        IReadOnlyList<NbaGrupoRecorte> Grupos,
        IReadOnlyList<NbaOpcaoTemporada> Temporadas);
}
