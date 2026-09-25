namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Time criado pelo usuário no módulo de campeonatos ("Real Pelada FC", o time
    /// dos sonhos do videogame). Vive FORA da tabela Times de propósito: aquela é
    /// lida por dezenas de telas e pelos serviços de importação (FotMob, ESPN,
    /// Transfermarkt), e um time fictício ali vazaria para a home, buscas e
    /// sincronizações.
    ///
    /// Um time próprio é do usuário, não de um campeonato: o mesmo time pode
    /// disputar várias edições (ver CampeonatoParticipante).
    /// </summary>
    public class TimeProprio
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        public string Nome { get; set; } = "";
        // Abreviação para placar e chaveamento (ex.: "RPF"). Null = gerada do nome.
        public string? Sigla { get; set; }
        public string? Cidade { get; set; }
        public string? EscudoUrl { get; set; }
        public string? CorPrincipal { get; set; }
        public string? CorSecundaria { get; set; }

        public DateTime CriadoEm { get; set; }
        // Arquivar tira o time das listas sem apagar o histórico dos campeonatos
        // em que ele jogou.
        public DateTime? ArquivadoEm { get; set; }

        public ICollection<ElencoItem> Elenco { get; set; } = new List<ElencoItem>();
    }
}
