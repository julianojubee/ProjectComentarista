using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    // Calcula a pontuação a partir das estatísticas importadas da api-football.
    // Os pesos vêm do banco (CriterioNota); este helper mantém apenas o mapeamento
    // de AcaoId → propriedade da EstatisticaJogador.
    public static class CriteriosNotaHelper
    {
        public const double NotaBaseFixa = 4.0;
        public const double NotaMinima = 4.0;
        public const double BonusVitoria = +2.0;
        public const double BonusDerrota = -1.0;
        public const double BonusGoleiroSemSofrerGol = +2.0;

        // Mapeamento fixo: AcaoId → extrator de quantidade da EstatisticaJogador
        private static readonly Dictionary<string, Func<EstatisticaJogador, int>> Extratores = new()
        {
            ["offside"]           = e => e.Offsides,
            ["finalizacao"]       = e => e.FinalizacoesTotal,
            ["finalizacao_gol"]   = e => e.FinalizacoesNoGol,
            ["gol"]               = e => e.Gols,
            ["gol_sofrido"]       = e => e.GolsSofridos,
            ["assistencia"]       = e => e.Assistencias,
            ["defesa"]            = e => e.Defesas,
            ["passe_chave"]       = e => e.PassesChave,
            ["desarme"]           = e => e.Desarmes,
            ["bloqueio"]          = e => e.Bloqueios,
            ["interceptacao"]     = e => e.Interceptacoes,
            ["duelo_vencido"]     = e => e.DuelosVencidos,
            ["drible_certo"]      = e => e.DriblesCertos,
            ["drible_sofrido"]    = e => e.DriblesSofridos,
            ["falta_sofrida"]     = e => e.FaltasSofridas,
            ["falta_cometida"]    = e => e.FaltasCometidas,
            ["cartao_amarelo"]    = e => e.CartoesAmarelos,
            ["cartao_vermelho"]   = e => e.CartoesVermelhos,
            ["penalti_sofrido"]   = e => e.PenaltiSofrido,
            ["penalti_cometido"]  = e => e.PenaltiCometido,
            ["penalti_perdido"]   = e => e.PenaltiPerdido,
            ["penalti_defendido"] = e => e.PenaltiDefendido,
        };

        // Pesos padrão usados como fallback quando o banco não tem registros
        private static readonly Dictionary<string, (string Label, double Peso)> PadroesDefault = new()
        {
            ["offside"]           = ("Impedimento",         -0.1),
            ["finalizacao"]       = ("Finalização",          0.1),
            ["finalizacao_gol"]   = ("Finalização no alvo",  0.2),
            ["gol"]               = ("Gol",                  2.0),
            ["gol_sofrido"]       = ("Gol sofrido",         -1.0),
            ["assistencia"]       = ("Assistência",          1.0),
            ["defesa"]            = ("Defesa (goleiro)",     0.5),
            ["passe_chave"]       = ("Passe-chave",          0.5),
            ["desarme"]           = ("Desarme",              0.1),
            ["bloqueio"]          = ("Bloqueio",             0.1),
            ["interceptacao"]     = ("Interceptação",        0.1),
            ["duelo_vencido"]     = ("Duelo vencido",        0.1),
            ["drible_certo"]      = ("Drible certo",         0.1),
            ["drible_sofrido"]    = ("Drible sofrido",      -0.1),
            ["falta_sofrida"]     = ("Falta sofrida",        0.1),
            ["falta_cometida"]    = ("Falta cometida",      -0.1),
            ["cartao_amarelo"]    = ("Cartão amarelo",      -0.5),
            ["cartao_vermelho"]   = ("Cartão vermelho",     -1.0),
            ["penalti_sofrido"]   = ("Pênalti sofrido",      0.5),
            ["penalti_cometido"]  = ("Pênalti cometido",    -0.5),
            ["penalti_perdido"]   = ("Pênalti perdido",     -0.5),
            ["penalti_defendido"] = ("Pênalti defendido",    0.5),
        };

        // Bônus de jogo sem sofrer gol vale para goleiros e defensores que jogaram
        // e cujo time não sofreu gol na partida.
        private static bool TemJogoSemSofrerGol(EstatisticaJogador e)
        {
            var pos = e.Jogador?.Posicao;
            bool defensivo = pos != null &&
                (pos.Equals("Goleiro", StringComparison.OrdinalIgnoreCase)
                 || pos.Equals("Defensor", StringComparison.OrdinalIgnoreCase));

            if (!defensivo || e.Minutos <= 0) return false;

            // Gols sofridos pelo TIME do jogador, a partir do placar da partida.
            var jogo = e.Jogo;
            var jogador = e.Jogador;
            if (jogo?.PlacarCasa != null && jogo.PlacarVisitante != null && jogador != null)
            {
                bool isCasa = jogo.TimeCasaId == jogador.TimeId
                           || jogo.TimeCasaId == jogador.SelecaoId;
                int golsSofridosTime = isCasa ? jogo.PlacarVisitante.Value : jogo.PlacarCasa.Value;
                return golsSofridosTime == 0;
            }

            // Fallback quando e.Jogo não veio carregado na consulta: a estatística
            // individual GolsSofridos só é confiável para goleiros — para jogadores
            // de linha ela é sempre 0 e daria o bônus indevidamente. Nesse caso é
            // melhor NÃO dar o bônus do que inflar a nota.
            bool goleiro = pos!.Equals("Goleiro", StringComparison.OrdinalIgnoreCase);
            return goleiro && e.GolsSofridos == 0;
        }

        // Calcula a pontuação usando os critérios do banco (ou padrões se lista vazia)
        public static double CalcularPontuacao(EstatisticaJogador e, IEnumerable<CriterioNota>? criteriosBanco = null)
        {
            var criterios = ResolverCriterios(criteriosBanco);
            var total = criterios.Sum(c =>
                Extratores.TryGetValue(c.AcaoId, out var extrator)
                    ? extrator(e) * c.Peso
                    : 0);
            if (TemJogoSemSofrerGol(e)) total += BonusGoleiroSemSofrerGol;
            return total;
        }

        public static List<Notadetalhe> ConstruirDetalhes(EstatisticaJogador e, IEnumerable<CriterioNota>? criteriosBanco = null)
        {
            var criterios = ResolverCriterios(criteriosBanco);
            var detalhes = criterios
                .Where(c => Extratores.ContainsKey(c.AcaoId))
                .Select(c => new Notadetalhe
                {
                    AcaoId    = c.AcaoId,
                    AcaoLabel = c.Label,
                    Quantidade = Extratores[c.AcaoId](e),
                    Peso      = c.Peso
                })
                .Where(d => d.Quantidade > 0)
                .ToList();

            if (TemJogoSemSofrerGol(e))
                detalhes.Add(new Notadetalhe
                {
                    AcaoId    = "sem_sofrer_gol",
                    AcaoLabel = "Não sofreu gol",
                    Quantidade = 1,
                    Peso      = BonusGoleiroSemSofrerGol
                });

            return detalhes;
        }

        // Retorna critérios efetivos: override do usuário se existir, senão o compartilhado (usuarioid = null).
        // Chame com todos os registros do usuário + todos os compartilhados.
        public static List<CriterioNota> MergeCriterios(
            IEnumerable<CriterioNota> compartilhados,
            IEnumerable<CriterioNota> doUsuario)
        {
            var overrides = doUsuario.ToDictionary(c => c.AcaoId);
            return compartilhados
                .Select(c => overrides.TryGetValue(c.AcaoId, out var u) ? u : c)
                .Concat(doUsuario.Where(u => !compartilhados.Any(c => c.AcaoId == u.AcaoId)))
                .Where(c => c.Ativo)
                .OrderBy(c => c.Ordem)
                .ToList();
        }

        // Retorna os critérios do banco ou cria a lista padrão se o banco estiver vazio
        private static List<CriterioNota> ResolverCriterios(IEnumerable<CriterioNota>? criteriosBanco)
        {
            var lista = criteriosBanco?.Where(c => c.Ativo).ToList();
            if (lista != null && lista.Count > 0) return lista;

            return PadroesDefault
                .Select((kv, i) => new CriterioNota
                {
                    AcaoId = kv.Key,
                    Label  = kv.Value.Label,
                    Peso   = kv.Value.Peso,
                    Ativo  = true,
                    Ordem  = i + 1
                })
                .ToList();
        }
    }
}
