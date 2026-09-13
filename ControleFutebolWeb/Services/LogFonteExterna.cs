using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Registra na tela de logs (/TransfermarktLogs) o que as fontes de reserva
    /// tentaram e o que responderam.
    ///
    /// Existe porque o resultado dessas importações vivia só no TempData — sumia no
    /// primeiro clique seguinte — e no log do processo, que ninguém abre. O caso que
    /// mais importa é justamente o silencioso: a partida NÃO foi localizada. Sem
    /// registro, "o jogo continua sem estatística" e "a fonte não tem o jogo" ficam
    /// indistinguíveis, e não dá para saber se falta mapear a liga, se o nome do time
    /// não casou ou se a fonte realmente não cobre aquela partida.
    ///
    /// Grava na mesma tabela do Transfermarkt/api-football porque a tela já lê dela e
    /// já filtra por Tipo e Ação — as fontes novas aparecem sozinhas nos filtros.
    /// </summary>
    public static class LogFonteExterna
    {
        public const string TipoEspn = "ESPN";
        public const string TipoFotMob = "FotMob";
        public const string TipoFifa = "FIFA";
        public const string TipoOgol = "ogol";

        /// <summary>
        /// Uma linha por tentativa. Salva na hora (e não junto com a importação) para
        /// que a tentativa FRACASSADA também fique registrada — é ela que interessa,
        /// e nesse caminho não há SaveChanges nenhum para pegar carona.
        /// </summary>
        /// <param name="ok">
        /// Vira o prefixo da ação, para a tela separar de relance o que funcionou do
        /// que não funcionou sem precisar ler os detalhes.
        /// </param>
        public static async Task RegistrarAsync(
            FutebolContext context,
            string tipo,
            string operacao,
            bool ok,
            Jogo? jogo,
            string? detalhes,
            CancellationToken ct = default)
        {
            context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId        = Guid.NewGuid(),
                Data           = DateTime.UtcNow,
                Tipo           = tipo,
                Acao           = (ok ? "OK — " : "Falhou — ") + operacao,
                CompeticaoNome = jogo?.Competicao?.Nome,
                JogoDescricao  = Descrever(jogo),
                Detalhes       = detalhes,
            });

            await context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// "1234: Al Sadd x Al Ahli Doha (22/08/2026)". O id vem junto porque é por ele
        /// que se reimporta o jogo na tela de Serviços.
        /// </summary>
        private static string? Descrever(Jogo? jogo)
        {
            if (jogo == null) return null;

            var data = jogo.Data?.ToLocalTime().ToString("dd/MM/yyyy");
            var confronto = $"{jogo.TimeCasa?.Nome} x {jogo.TimeVisitante?.Nome}";

            return data == null
                ? $"{jogo.Id}: {confronto}"
                : $"{jogo.Id}: {confronto} ({data})";
        }
    }
}
