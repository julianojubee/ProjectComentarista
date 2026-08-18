using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    // Mapa (jogador, jogo) → lado e posição naquele jogo. Montado pelo LadoJogadorHelper.
    using LadoPorJogadorJogo = IReadOnlyDictionary<(int JogadorId, int JogoId), AtuacaoNoJogo>;

    // Calcula a pontuação a partir das estatísticas importadas da api-football.
    // Os pesos vêm do banco (CriterioNota); este helper mantém apenas o mapeamento
    // de AcaoId → propriedade da EstatisticaJogador.
    public static class CriteriosNotaHelper
    {
        // Peso inicial (nota base) padrão. O usuário pode trocar o seu na tela
        // /CriteriosNota — ver AcaoPesoInicial e NotaBase.
        public const double NotaBasePadrao = 4.0;

        // Critério especial guardado junto dos demais (criterionotas) só para
        // carregar o peso inicial do usuário: não tem extrator em Extratores, então
        // nunca soma pontos por ação — quem o lê é o NotaBase.
        public const string AcaoPesoInicial = "peso_inicial";
        public const string LabelPesoInicial = "Peso inicial (nota base)";

        // Segundo critério especial: o bônus de jogo sem sofrer gol. O peso é quanto
        // vale o bônus e o Config lista as posições que o recebem. Também não tem
        // extrator — quem o aplica é o TemJogoSemSofrerGol.
        public const string AcaoSemSofrerGol = "sem_sofrer_gol";
        public const string LabelSemSofrerGol = "Não sofreu gol";

        // Terceiro critério especial: qual motor calcula a nota dos jogos que o
        // usuário não avaliou à mão. O modo fica no Config; Peso não é usado.
        // Quem lê é o MotorNota; quem aplica é o NotaAutomaticaHelper.
        public const string AcaoMotorNota = "motor_nota";
        public const string LabelMotorNota = "Motor da nota automática";

        public const double BonusVitoria = +2.0;

        public const double BonusDerrota = -1.0;
        public const double BonusGoleiroSemSofrerGol = +2.0;

        // Grupos de posição oferecidos na tela, na ordem de exibição.
        public static readonly IReadOnlyList<(string Codigo, string Rotulo)> GruposPosicao = new[]
        {
            ("GOLEIRO",  "Goleiro"),
            ("ZAGUEIRO", "Zagueiro"),
            ("LATERAL",  "Lateral"),
            ("ALA",      "Ala"),
            ("VOLANTE",  "Volante"),
            ("MEIA",     "Meia"),
            ("PONTA",    "Ponta"),
            ("ATACANTE", "Atacante"),
        };

        // Comportamento histórico do bônus: goleiros, zagueiros, laterais e alas.
        public static readonly IReadOnlyList<string> PosicoesSemSofrerGolPadrao =
            new[] { "GOLEIRO", "ZAGUEIRO", "LATERAL", "ALA" };

        public static string RotuloGrupoPosicao(string codigo)
        {
            foreach (var g in GruposPosicao)
                if (g.Codigo == codigo) return g.Rotulo;
            return codigo;
        }

        // Grupo da posição jogada, casando por conteúdo: serve tanto para o nome
        // granular derivado da escalação ("Lateral Esquerdo", "Centroavante Esquerdo")
        // quanto para o rótulo genérico da api-football ("Defensor", "Meia").
        // Null = posição não reconhecida.
        public static string? GrupoDaPosicao(string? posicao)
        {
            if (string.IsNullOrWhiteSpace(posicao)) return null;
            bool C(string s) => posicao.Contains(s, StringComparison.OrdinalIgnoreCase);

            if (C("Goleiro")) return "GOLEIRO";
            if (C("Zagueiro") || C("Defensor")) return "ZAGUEIRO";
            if (C("Lateral")) return "LATERAL";
            if (C("Ala")) return "ALA";
            if (C("Volante")) return "VOLANTE";
            if (C("Meia") || C("Meio")) return "MEIA";
            if (C("Ponta")) return "PONTA";
            if (C("Centroavante") || C("Atacante")) return "ATACANTE";
            return null;
        }

        // Lê o Config do critério "sem_sofrer_gol" (códigos separados por ';'),
        // descartando inválidos. Sem critério salvo = comportamento padrão.
        public static List<string> PosicoesSemSofrerGol(IEnumerable<CriterioNota>? criterios)
        {
            var registro = CriterioSemSofrerGol(criterios);
            if (registro == null) return PosicoesSemSofrerGolPadrao.ToList();

            // Config vazio é uma escolha explícita: nenhuma posição recebe o bônus.
            return ParsePosicoes(registro.Config);
        }

        public static List<string> ParsePosicoes(string? valor)
        {
            var validos = GruposPosicao.Select(g => g.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (valor ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Where(validos.Contains)
                .Distinct()
                .ToList();
        }

        public static string SerializarPosicoes(IEnumerable<string>? posicoes)
            => string.Join(';', ParsePosicoes(string.Join(';', posicoes ?? Enumerable.Empty<string>())));

        // Motor escolhido pelo usuário para os jogos que ele não avaliou à mão.
        // Valor desconhecido (ou ausente) = Classico: a régua que já existia continua
        // valendo para quem nunca abriu a tela, e nenhuma nota muda sem escolha.
        public static MotorNota MotorDaNota(IEnumerable<CriterioNota>? criterios)
        {
            var registro = criterios?.FirstOrDefault(c => c.AcaoId == AcaoMotorNota && c.Ativo);
            return ParseMotor(registro?.Config);
        }

        public static MotorNota ParseMotor(string? valor)
            => string.Equals(valor?.Trim(), nameof(MotorNota.Automatico), StringComparison.OrdinalIgnoreCase)
                ? MotorNota.Automatico
                : MotorNota.Classico;

        // Quanto vale o bônus de não sofrer gol para este usuário.
        public static double BonusSemSofrerGol(IEnumerable<CriterioNota>? criterios)
            => CriterioSemSofrerGol(criterios)?.Peso ?? BonusGoleiroSemSofrerGol;

        private static CriterioNota? CriterioSemSofrerGol(IEnumerable<CriterioNota>? criterios)
            => criterios?.FirstOrDefault(c => c.AcaoId == AcaoSemSofrerGol && c.Ativo);

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

        // Peso inicial do usuário (critério "peso_inicial") ou o padrão 4,0. Valores
        // fora de 0–10 são ignorados — a nota final nunca poderia respeitá-los.
        public static double NotaBase(IEnumerable<CriterioNota>? criterios)
        {
            var registro = criterios?.FirstOrDefault(c => c.AcaoId == AcaoPesoInicial && c.Ativo);
            if (registro == null) return NotaBasePadrao;
            return registro.Peso >= 0 && registro.Peso <= 10 ? registro.Peso : NotaBasePadrao;
        }

        // Nota de um jogo "ok": quem fez mais tipos de ação verde do que vermelha
        // chega pelo menos aqui, mesmo que a soma dos pesos tenha dado pouco.
        public const double PisoJogoPositivo = 6.0;

        // Jogo empatado (mesma quantidade de tipos verdes e vermelhos): nem bom nem
        // ruim, vale a nota do meio.
        public const double PisoJogoEquilibrado = 5.0;

        // Quem entrou no decorrer do jogo não teve tempo de construir nota: abaixo
        // deste tanto de minutos vale o piso de participação curta.
        public const int MinutosParticipacaoCurta = 45;
        public const double PisoParticipacaoCurta = 5.0;

        // Goleiro que defendeu 100% do que foi no alvo: bônus por ter sido decisivo,
        // só enquanto a nota não chega ao teto abaixo (quem já tirou nota alta não
        // precisa do empurrão).
        public const double BonusGoleiroDecisivo = +2.0;
        public const double TetoGoleiroDecisivo = 7.0;

        // Quem marcou o gol que decidiu a partida ganha um ponto por cima do gol em si.
        // Sem teto: ao contrário do bônus do goleiro, este não é um empurrão para quem
        // ficou com nota baixa — é o prêmio por ter decidido o jogo.
        public const double BonusGolDaVitoria = +1.0;

        // Quantos TIPOS de ação de cada cor o jogador registrou na partida — os chips
        // da tela. Quantidade e peso não entram: "Falta cometida ×4" é uma vermelha só,
        // e um gol vale o mesmo que um desarme nesta contagem. É ela que decide o piso
        // de merecimento, não a soma dos pontos.
        public readonly record struct ContagemAcoes(int Verdes, int Vermelhas);

        // O que a partida diz sobre o jogador além do valor somado das ações: minutos
        // em campo, se ele foi o goleiro que pegou tudo o que foi no gol e a contagem
        // de ações por cor. Montado pelo ContextoNotaHelper; Vazio = informação
        // indisponível, e aí os ajustes que dependem dela simplesmente não entram.
        /// <param name="Acoes">
        /// Null = detalhes indisponíveis (ou jogo sem nenhuma ação marcada), e aí o
        /// gatilho do merecimento cai no valor somado das ações.
        /// </param>
        public readonly record struct ContextoNota(
            int? Minutos = null, bool GoleiroDecisivo = false, ContagemAcoes? Acoes = null,
            bool GolDaVitoria = false)
        {
            public static readonly ContextoNota Vazio = new();
        }

        // Conta os chips por cor. Null quando não há nenhum: sem ação marcada não dá
        // para dizer se o jogo foi bom, empatado ou ruim.
        public static ContagemAcoes? ContarAcoes(IEnumerable<Notadetalhe>? detalhes)
        {
            if (detalhes == null) return null;
            var marcados = detalhes.Where(d => d.Quantidade > 0).ToList();
            if (marcados.Count == 0) return null;
            return new ContagemAcoes(marcados.Count(d => d.Peso > 0), marcados.Count(d => d.Peso < 0));
        }

        // Nota final exibida: peso inicial + valor das ações, com os ajustes de
        // merecimento e teto 10. Régua única de todo o sistema — web, API e relatórios.
        public static double NotaFinal(double valorAcoes, IEnumerable<CriterioNota>? criterios, int casas = 2)
            => NotaFinal(valorAcoes, criterios, ContextoNota.Vazio, casas);

        public static double NotaFinal(double valorAcoes, IEnumerable<CriterioNota>? criterios,
            ContextoNota contexto, int casas = 2)
            => NotaFinalComBase(valorAcoes, NotaBase(criterios), contexto, casas);

        public static double NotaFinalComBase(double valorAcoes, double notaBase, int casas = 2)
            => NotaFinalComBase(valorAcoes, notaBase, ContextoNota.Vazio, casas);

        // Nota por merecimento. Parte de "peso inicial + ações" e aplica, nesta ordem:
        //
        //   1. Piso de merecimento, pela contagem de chips verdes x vermelhos:
        //        • mais verdes  → 6,0 (um jogo de ações de peso baixo — desarmes,
        //          passes-chave — não pode virar nota 1);
        //        • empate       → 5,0 (nem bom nem ruim);
        //        • mais vermelhas → nada, vale a nota calculada. Por isso o peso
        //          inicial deixou de ser piso: senão quem jogou mal ficaria inflado.
        //   2. Piso de participação curta (5,0): quem jogou menos de 45 minutos não teve
        //      jogo suficiente para justificar nota baixa. Já acima de 5, fica como está.
        //   3. Bônus de goleiro decisivo (+2,0): pegou 100% do que foi no alvo e ainda
        //      assim está abaixo de 7. Vem DEPOIS dos pisos de propósito — antes deles,
        //      o piso de 6 engoliria o bônus e o goleiro decisivo terminaria empatado
        //      com quem só fez um jogo correto.
        //   4. Bônus de gol da vitória (+1,0): marcou o gol que decidiu a partida. Sem
        //      teto — só o de 10, no fim.
        public static double NotaFinalComBase(double valorAcoes, double notaBase,
            ContextoNota contexto, int casas = 2)
            => Compor(valorAcoes, notaBase, contexto, casas).Nota;

        // As parcelas da nota, para a tela conseguir explicar a conta.
        public readonly record struct ComposicaoNota(
            double NotaBase,
            double ValorAcoes,
            double Merecimento,
            double ParticipacaoCurta,
            double GoleiroDecisivo,
            double GolDaVitoria,
            double Nota,
            // Até onde o merecimento levaria a nota (6,0 jogo positivo; 5,0 empatado;
            // 0 quando não houve piso) — a tela usa para explicar a parcela.
            double PisoMerecimento = 0)
        {
            public double TotalAjustes => Merecimento + ParticipacaoCurta + GoleiroDecisivo + GolDaVitoria;
        }

        // Até onde a nota sobe pelo merecimento. 0 = sem piso (jogo com mais vermelhas
        // que verdes, ou sem ação nenhuma).
        public static double PisoDeMerecimento(double valorAcoes, ContagemAcoes? acoes)
        {
            // Sem os detalhes do jogo não dá para contar os chips: aí o gatilho é o
            // valor somado, que é a melhor aproximação disponível.
            if (acoes == null) return valorAcoes > 0 ? PisoJogoPositivo : 0;

            var (verdes, vermelhas) = (acoes.Value.Verdes, acoes.Value.Vermelhas);
            if (verdes > vermelhas) return PisoJogoPositivo;
            if (verdes == vermelhas && verdes > 0) return PisoJogoEquilibrado;
            return 0;
        }

        public static ComposicaoNota Compor(double valorAcoes, double notaBase, ContextoNota contexto, int casas = 2)
        {
            var bruta = notaBase + valorAcoes;
            double merecimento = 0, participacao = 0, goleiro = 0;

            var piso = PisoDeMerecimento(valorAcoes, contexto.Acoes);
            if (bruta < piso)
            {
                merecimento = piso - bruta;
                bruta = piso;
            }

            if (contexto.Minutos is > 0 and < MinutosParticipacaoCurta && bruta < PisoParticipacaoCurta)
            {
                participacao = PisoParticipacaoCurta - bruta;
                bruta = PisoParticipacaoCurta;
            }

            if (contexto.GoleiroDecisivo && bruta < TetoGoleiroDecisivo)
            {
                goleiro = BonusGoleiroDecisivo;
                bruta += BonusGoleiroDecisivo;
            }

            double golVitoria = 0;
            if (contexto.GolDaVitoria)
            {
                golVitoria = BonusGolDaVitoria;
                bruta += BonusGolDaVitoria;
            }

            return new ComposicaoNota(
                notaBase, valorAcoes,
                Math.Round(merecimento, casas), Math.Round(participacao, casas), Math.Round(goleiro, casas),
                Math.Round(golVitoria, casas),
                Math.Round(Math.Clamp(bruta, 0, 10), casas), piso);
        }

        public static ComposicaoNota Compor(double valorAcoes, IEnumerable<CriterioNota>? criterios,
            ContextoNota contexto, int casas = 2)
            => Compor(valorAcoes, NotaBase(criterios), contexto, casas);

        // Critérios que ocupam a tabela criterionotas sem serem ações marcáveis por
        // quantidade: os dois especiais acima e a régua do rating automático, que só
        // usa o registro como lugar para guardar o JSON da calibração.
        private static readonly HashSet<string> AcoesReservadas = new()
        {
            AcaoPesoInicial, AcaoSemSofrerGol, AcaoMotorNota, Rating.BaselineRating.AcaoId,
        };

        public static bool EhAcaoReservada(string acaoId) => AcoesReservadas.Contains(acaoId);

        // Lista sem os critérios especiais — para telas/APIs que mostram as ações
        // avaliáveis (avaliação manual, app Android), onde nenhum deles é uma ação
        // que se marca por quantidade.
        public static List<CriterioNota> SomenteAcoes(IEnumerable<CriterioNota> criterios)
            => criterios.Where(c => !EhAcaoReservada(c.AcaoId)).ToList();

        // Bônus de jogo sem sofrer gol: vale para quem jogou, atuou numa das posições
        // configuradas em /CriteriosNota (padrão: goleiro, zagueiro, lateral e ala) e
        // cujo time não sofreu gol na partida.
        //
        // A posição considerada é a DAQUELA PARTIDA, vinda da escalação (`lados`) —
        // nunca Jogador.Posicao, que é o agregado das posições em que ele mais atua:
        // um lateral escalado de meia naquele jogo não é defensor ali. Sem escalação
        // do jogo não há como saber a posição, e aí o bônus não é dado.
        private static bool TemJogoSemSofrerGol(
            EstatisticaJogador e, LadoPorJogadorJogo? lados, IEnumerable<CriterioNota>? criterios)
        {
            if (e.Minutos <= 0) return false;
            if (lados == null || !lados.TryGetValue((e.JogadorId, e.JogoId), out var atuacao)) return false;

            var posicoes = PosicoesSemSofrerGol(criterios);
            var grupo = GrupoDaPosicao(atuacao.Posicao);
            if (grupo == null || !posicoes.Contains(grupo)) return false;

            // Gols sofridos pelo TIME do jogador, a partir do placar da partida. O lado
            // vem da escalação da época: o time ATUAL do jogador inverteria o placar
            // nos jogos anteriores a uma transferência.
            var jogo = e.Jogo;
            if (jogo?.PlacarCasa != null && jogo.PlacarVisitante != null)
            {
                int golsSofridosTime = atuacao.IsTimeCasa ? jogo.PlacarVisitante.Value : jogo.PlacarCasa.Value;
                return golsSofridosTime == 0;
            }

            // Fallback quando e.Jogo não veio carregado na consulta: a estatística
            // individual GolsSofridos só é confiável para goleiros — para jogadores
            // de linha ela é sempre 0 e daria o bônus indevidamente. Nesse caso é
            // melhor NÃO dar o bônus do que inflar a nota.
            return grupo == "GOLEIRO" && e.GolsSofridos == 0;
        }

        // Calcula a pontuação usando os critérios do banco (ou padrões se lista vazia).
        // `lados` (LadoJogadorHelper) diz de que lado o jogador atuou em cada jogo —
        // sem ele o bônus "não sofreu gol" cai no time atual, que erra após transferência.
        public static double CalcularPontuacao(EstatisticaJogador e, IEnumerable<CriterioNota>? criteriosBanco = null,
            LadoPorJogadorJogo? lados = null)
        {
            var criterios = ResolverCriterios(criteriosBanco);
            // Critério que a fonte não informa fica de fora em vez de somar zero. Sem
            // isso a nota da ESPN cairia de um jeito enviesado: os critérios ausentes
            // são quase todos positivos (desarme, interceptação, passe-chave, drible),
            // enquanto cartão e falta, que puxam para baixo, vêm todos.
            var total = criterios.Sum(c =>
                Extratores.TryGetValue(c.AcaoId, out var extrator) && FonteEstatistica.Cobre(e.Fonte, c.AcaoId)
                    ? extrator(e) * c.Peso
                    : 0);
            if (TemJogoSemSofrerGol(e, lados, criterios)) total += BonusSemSofrerGol(criterios);
            return total;
        }

        public static List<Notadetalhe> ConstruirDetalhes(EstatisticaJogador e, IEnumerable<CriterioNota>? criteriosBanco = null,
            LadoPorJogadorJogo? lados = null)
        {
            var criterios = ResolverCriterios(criteriosBanco);
            // Mesmo filtro de CalcularPontuacao, para o detalhamento na tela bater com
            // o total: o que a fonte não informa não vira linha.
            var detalhes = criterios
                .Where(c => Extratores.ContainsKey(c.AcaoId) && FonteEstatistica.Cobre(e.Fonte, c.AcaoId))
                .Select(c => new Notadetalhe
                {
                    AcaoId    = c.AcaoId,
                    AcaoLabel = c.Label,
                    Quantidade = Extratores[c.AcaoId](e),
                    Peso      = c.Peso
                })
                .Where(d => d.Quantidade > 0)
                .ToList();

            if (TemJogoSemSofrerGol(e, lados, criterios))
                detalhes.Add(new Notadetalhe
                {
                    AcaoId    = AcaoSemSofrerGol,
                    AcaoLabel = LabelSemSofrerGol,
                    Quantidade = 1,
                    Peso      = BonusSemSofrerGol(criterios)
                });

            return detalhes;
        }

        // Retorna critérios efetivos: override do usuário se existir, senão o compartilhado (usuarioid = null).
        // Chame com todos os registros do usuário + todos os compartilhados.
        public static List<CriterioNota> MergeCriterios(
            IEnumerable<CriterioNota> compartilhados,
            IEnumerable<CriterioNota> doUsuario)
        {
            // Dedup defensivo por AcaoId: registros duplicados no banco (compartilhados
            // ou do usuário) não podem somar peso em dobro/triplo no cálculo da nota —
            // mantém o de menor Id (primeiro cadastrado) de cada AcaoId.
            var compartilhadosUnicos = compartilhados
                .GroupBy(c => c.AcaoId)
                .Select(g => g.OrderBy(c => c.Id).First())
                .ToList();
            var doUsuarioUnico = doUsuario
                .GroupBy(c => c.AcaoId)
                .Select(g => g.OrderBy(c => c.Id).First())
                .ToList();

            var overrides = doUsuarioUnico.ToDictionary(c => c.AcaoId);
            return compartilhadosUnicos
                .Select(c => overrides.TryGetValue(c.AcaoId, out var u) ? u : c)
                .Concat(doUsuarioUnico.Where(u => !compartilhadosUnicos.Any(c => c.AcaoId == u.AcaoId)))
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
