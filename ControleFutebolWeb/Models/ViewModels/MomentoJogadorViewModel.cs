namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Como o jogador chega ao jogo, para a aba "Momento" do modal Pré-jogo.
    ///
    /// É o equivalente em português da aba "About" do perfil no FotMob — que não tem
    /// endpoint próprio: o texto deles é montado a partir do mesmo playerData que
    /// FotMobPerfilService já lê. Mesmas regras daquela tela: nada é gravado e a nota
    /// do FotMob fica de fora (média da temporada e nota de cada jogo).
    /// </summary>
    public class MomentoJogadorViewModel
    {
        public string? Posicao { get; set; }

        /// <summary>"Destro" / "Canhoto" / "Ambidestro".</summary>
        public string? Pe { get; set; }

        /// <summary>Clube atual segundo o FotMob, com "(emprestado)" quando for o caso.</summary>
        public string? Clube { get; set; }

        /// <summary>Id do clube, só para o escudo (/MediaProxy/Escudo/{id}).</summary>
        public long? ClubeId { get; set; }

        public SituacaoFisicaViewModel? Lesao { get; set; }

        /// <summary>Números na liga principal da temporada atual; null quando a fonte não tem.</summary>
        public TemporadaMomento? Temporada { get; set; }

        /// <summary>Métricas em que ele fica acima de 70% dos jogadores da mesma função.</summary>
        public List<DestaqueMomento> Destaques { get; set; } = new();

        /// <summary>Grupo de comparação dos destaques ("atacantes", "meio-campistas"...).</summary>
        public string? GrupoComparacao { get; set; }

        /// <summary>Últimos jogos, do mais recente para o mais antigo (até 10).</summary>
        public List<JogoMomento> Jogos { get; set; } = new();

        /// <summary>Resumo em uma frase dos últimos 5 jogos em que entrou em campo.</summary>
        public string? Sequencia { get; set; }

        public string? Erro { get; set; }
    }

    public class TemporadaMomento
    {
        public string Liga { get; set; } = "";

        /// <summary>Id da liga, só para o símbolo (/MediaProxy/Liga/{id}).</summary>
        public long? LigaId { get; set; }
        public string? Temporada { get; set; }
        public int Jogos { get; set; }
        public int Titular { get; set; }
        public int Minutos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Amarelos { get; set; }
        public int Vermelhos { get; set; }
    }

    public class DestaqueMomento
    {
        public string Nome { get; set; } = "";

        /// <summary>0-100: percentual de jogadores da mesma função que ele supera.</summary>
        public int Percentil { get; set; }
    }

    public class JogoMomento
    {
        public DateTime? Data { get; set; }
        public string? Competicao { get; set; }
        public long? CompeticaoId { get; set; }
        public string? Adversario { get; set; }
        public long? AdversarioId { get; set; }
        public bool Mandante { get; set; }

        /// <summary>Placar do ponto de vista do time dele ("4-1").</summary>
        public string? Placar { get; set; }

        /// <summary>"V", "E", "D" ou null quando o placar não veio.</summary>
        public string? Resultado { get; set; }

        public bool Jogou { get; set; }

        /// <summary>Ficou no banco (entrando ou não).</summary>
        public bool Reserva { get; set; }

        public int? Minutos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Amarelos { get; set; }
        public int Vermelhos { get; set; }
    }
}
