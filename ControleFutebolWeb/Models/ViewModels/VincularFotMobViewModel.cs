namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela /Jogadores/VincularFotMob/{id}: escolher a qual jogador do FotMob o nosso
    /// corresponde, para as estatísticas avançadas saberem o que buscar.
    ///
    /// É manual por necessidade, não por preguiça. O vínculo automático (que nasce na
    /// importação de um jogo, ver Jogador.IdFotMob) só alcança quem jogou uma partida
    /// importada de lá; para o resto, a busca por nome é insegura demais para decidir
    /// sozinha — o nome completo do cadastro costuma não achar nada e o curto traz
    /// homônimos.
    /// </summary>
    public class VincularFotMobViewModel
    {
        public int JogadorId { get; set; }
        public string JogadorNome { get; set; } = "";

        /// <summary>Vínculo já existente, se houver — a tela permite trocar ou remover.</summary>
        public long? IdFotMobAtual { get; set; }

        public string? Termo { get; set; }
        public List<CandidatoFotMob> Candidatos { get; set; } = new();
    }

    public class CandidatoFotMob
    {
        public long Id { get; set; }
        public string Nome { get; set; } = "";

        /// <summary>
        /// Clube atual no FotMob. É o desempate visual entre homônimos — "Edmilson" traz
        /// cinco jogadores, e o time é o que diz qual é o certo.
        /// </summary>
        public string? Time { get; set; }
    }
}
