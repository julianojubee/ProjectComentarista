using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogador
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;
        public string? PrimeiroNome { get; set; }
        public string? UltimoNome { get; set; }

        public string NomeExibicao =>
            (!string.IsNullOrWhiteSpace(PrimeiroNome) && !string.IsNullOrWhiteSpace(UltimoNome))
                ? $"{PrimeiroNome} {UltimoNome}"
                : Nome;

        public string Posicao { get; set; } = string.Empty;

        // Data de Nascimento (null = não informada)
        public DateTime? DataNascimento { get; set; }

        // Idade calculada dinamicamente (baseada na DataNascimento)
        public int Idade
        {
            get
            {
                if (!DataNascimento.HasValue) return 0;
                var hoje = DateTime.Today;
                var idade = hoje.Year - DataNascimento.Value.Year;
                if (DataNascimento.Value.Date > hoje.AddYears(-idade)) idade--;
                return idade;
            }
        }

        // 🔹 Novo campo: Idade extraída do Transfermarkt
        public int? IdadeTransfermarkt { get; set; }

        // 🔹 Novo campo: Flag para indicar se já foi atualizado
        public bool Atualizado { get; set; } = false;

        // 🔹 Novo campo: ID de origem da API/JSON

        public long? IdApi { get; set; }

        /// <summary>
        /// Id deste jogador no FotMob, quando conhecido. Só serve para abrir as
        /// estatísticas avançadas (ver FotMobPerfilService) — nenhum dado do FotMob é
        /// gravado a partir dele.
        ///
        /// NÃO é descoberto por busca: o nome completo do nosso cadastro não acha nada
        /// lá ("Edmilson Junior Paulo da Silva" devolve zero resultados) e o nome curto
        /// devolve homônimos. Ele nasce como subproduto da importação de estatísticas:
        /// FotMobService já casa jogador por nome E camisa dentro do elenco daquela
        /// partida — umas duas dezenas de candidatos, não o mundo inteiro —, e é esse
        /// casamento, já conferido, que fica guardado aqui.
        ///
        /// Null = jogador que ainda não apareceu em nenhum jogo importado do FotMob.
        /// </summary>
        public long? IdFotMob { get; set; }

        public int? NumeroCamisa { get; set; }

        public int? NacionalidadeId { get; set; }
        public Nacionalidade? Nacionalidade { get; set; }
        [ValidateNever]   // <- evita erro de ModelState

        public int TimeId { get; set; }
        [ValidateNever]   // <- evita erro de ModelState
        public Time Time { get; set; } = null!;

        // Seleção nacional (opcional) — permite que um jogador de clube
        // também seja utilizado em jogos de seleção sem criar cadastro duplicado.
        public int? SelecaoId { get; set; }
        [ValidateNever]
        public Time? Selecao { get; set; }

        public DateTime DtInc { get; set; }
        public DateTime? DtAlt { get; set; }

        // Altura em centímetros e peso em quilos (buscados via api-football).
        public int? Altura { get; set; }
        public int? Peso { get; set; }

        public string? FotoUrl { get; set; }
        [Column("linktransfermarket")]
        public string? LinkTransfermarket { get; set; }
        public string? Observacoes { get; set; }

        // Jogador que encerrou a carreira (marcado à mão em /Jogadores/Estatisticas →
        // "Transferências" → "Aposentou"). O TimeId continua apontando para o último
        // clube — o histórico de jogos e escalações depende dele —, essa flag é só o
        // estado atual, para não ficar parecendo que ele ainda defende aquele clube.
        public bool Aposentado { get; set; } = false;
        public DateTime? AposentadoEm { get; set; }

    }
}