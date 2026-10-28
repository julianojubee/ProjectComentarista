namespace ControleFutebolWeb.Models.Nba
{
    // Modelos da página /NBA. Nada disso vai para o banco: tudo é montado a partir
    // da API JSON da ESPN (ver EspnNbaService) e vive só no cache em memória.

    /// <summary>Situação do jogo, como a ESPN informa em status.type.state.</summary>
    public enum NbaSituacao { Agendado, AoVivo, Encerrado, Adiado }

    public record NbaTimeResumo(
        string Id,
        string Nome,
        string NomeCurto,
        string Sigla,
        string? Logo,
        string? Cor);

    public record NbaLider(string Categoria, string AtletaId, string Nome, string Valor, string? Foto);

    public record NbaLadoJogo(
        NbaTimeResumo Time,
        bool Casa,
        int? Placar,
        bool Vencedor,
        string? Campanha,
        IReadOnlyList<int> Parciais,
        IReadOnlyList<NbaLider> Lideres);

    public record NbaJogo(
        string Id,
        DateTime InicioUtc,
        NbaSituacao Situacao,
        string Status,
        NbaLadoJogo Casa,
        NbaLadoJogo Visitante,
        string? Ginasio,
        string? Cidade,
        IReadOnlyList<string> Transmissao,
        string? Manchete,
        string? Nota);

    // ── Classificação ─────────────────────────────────────────────────────

    public record NbaLinhaClassificacao(
        NbaTimeResumo Time,
        int Seed,
        int Vitorias,
        int Derrotas,
        string Aproveitamento,
        string JogosAtras,
        string Casa,
        string Fora,
        string Divisao,
        string Conferencia,
        string PontosFeitos,
        string PontosSofridos,
        string Saldo,
        string Sequencia,
        string Ultimos10,
        string? Clincher);

    public record NbaGrupoClassificacao(
        string Nome,
        string? Conferencia,
        IReadOnlyList<NbaLinhaClassificacao> Linhas);

    public record NbaClassificacao(string Temporada, IReadOnlyList<NbaGrupoClassificacao> Grupos);

    // ── Estatísticas ──────────────────────────────────────────────────────

    public record NbaLiderCategoria(
        string Chave,
        string Nome,
        string Sigla,
        IReadOnlyList<NbaLiderLinha> Top);

    public record NbaLiderLinha(
        string AtletaId,
        string Nome,
        string? Foto,
        NbaTimeResumo? Time,
        string Valor);

    public record NbaLideres(string Temporada, IReadOnlyList<NbaLiderCategoria> Categorias);

    public record NbaLinhaJogador(
        string AtletaId,
        string Nome,
        string? Foto,
        string? Posicao,
        string? TimeId,
        string? TimeSigla,
        string? TimeLogo,
        IReadOnlyDictionary<string, string> Valores);

    public record NbaTabelaJogadores(
        string Temporada,
        int Pagina,
        int TotalPaginas,
        int Total,
        IReadOnlyList<NbaLinhaJogador> Linhas);

    public record NbaLinhaTime(NbaTimeResumo Time, IReadOnlyDictionary<string, string> Valores);

    public record NbaTabelaTimes(string Temporada, IReadOnlyList<NbaLinhaTime> Linhas);

    // ── Jogo (summary) ────────────────────────────────────────────────────

    public record NbaAtletaBox(
        string AtletaId,
        string Nome,
        string? Posicao,
        string? Camisa,
        string? Foto,
        bool Titular,
        bool NaoJogou,
        string? Motivo,
        IReadOnlyList<string> Valores);

    public record NbaBoxTime(
        NbaTimeResumo Time,
        IReadOnlyList<string> Colunas,
        IReadOnlyList<NbaAtletaBox> Atletas,
        IReadOnlyList<string> Totais);

    public record NbaEstatTime(string Nome, string Casa, string Visitante);

    public record NbaLance(
        int Periodo,
        string Relogio,
        string Texto,
        int PlacarCasa,
        int PlacarVisitante,
        string? TimeId,
        bool Pontuou);

    public record NbaLesao(string TimeId, string Nome, string Status, string? Detalhe);

    public record NbaDetalheJogo(
        NbaJogo Jogo,
        IReadOnlyList<NbaBoxTime> Box,
        IReadOnlyList<NbaEstatTime> EstatisticasTimes,
        IReadOnlyList<NbaLance> Lances,
        IReadOnlyList<NbaLesao> Lesoes,
        string? Confronto,
        int? Publico,
        IReadOnlyList<string> Arbitros);

    // ── Time ──────────────────────────────────────────────────────────────

    public record NbaAtletaElenco(
        string Id,
        string Nome,
        string? Camisa,
        string? Posicao,
        string? Altura,
        string? Peso,
        int? Idade,
        int? Experiencia,
        string? Universidade,
        string? Foto,
        string? Lesao);

    public record NbaDetalheTime(
        NbaTimeResumo Time,
        string? Campanha,
        string? Posicao,
        string? Treinador,
        IReadOnlyList<NbaAtletaElenco> Elenco,
        IReadOnlyList<NbaJogo> Calendario);
}
