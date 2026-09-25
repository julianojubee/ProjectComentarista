namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Jogador criado pelo usuário: o amigo da pelada, o atleta da liga amadora,
    /// o "regen" do modo carreira. Jogadores reais NÃO são copiados para cá — o
    /// elenco aponta direto para Jogador (ver ElencoItem).
    ///
    /// Em liga amadora isto é dado de pessoa real, às vezes menor de idade: tudo
    /// além do nome é opcional, e a tela deve deixar isso claro (LGPD).
    /// </summary>
    public class JogadorProprio
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        public string Nome { get; set; } = "";
        // Nome de camisa / como é chamado ("Zé do Gol"). Null = usa Nome.
        public string? Apelido { get; set; }
        public string? Posicao { get; set; }
        public int? NumeroCamisa { get; set; }
        public DateTime? DataNascimento { get; set; }
        public string? FotoUrl { get; set; }

        public int? NacionalidadeId { get; set; }
        public Nacionalidade? Nacionalidade { get; set; }

        public DateTime CriadoEm { get; set; }

        public string NomeExibicao => string.IsNullOrWhiteSpace(Apelido) ? Nome : Apelido;
    }
}
