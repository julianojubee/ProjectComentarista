namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// "Seleção da Copa" montada pelo usuário: uma formação + os jogadores que ele
    /// escolheu como os melhores por posição. Uma por usuário + competição + temporada.
    /// Os slots (posição no campo + jogador) são guardados como JSON para evitar uma
    /// tabela-filha — não há jogo associado (diferente de Escalacao).
    /// </summary>
    public class SelecaoCopaUsuario
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = "";

        public int CompeticaoId { get; set; }
        public int? Temporada { get; set; }

        // Nome da seleção (ex.: "1ª fase", "Final"). Permite várias por usuário+temporada.
        public string? Nome { get; set; }

        public int? FormacaoId { get; set; }

        // JSON: [{ "x": 50.0, "y": 90.0, "jogadorId": 123 }, ...]
        public string? SlotsJson { get; set; }
    }
}
