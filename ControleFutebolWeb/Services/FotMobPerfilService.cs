using System.Globalization;
using System.Text.Json;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Monta a tela de estatísticas avançadas de um jogador com o que o FotMob publica
    /// na página dele (fotmob.com/players/{id}).
    ///
    /// SÓ EXIBE — não grava nada. Toda a informação daqui vive na requisição e morre
    /// com ela. Isso não é economia de esquema: é o que garante que nenhum número do
    /// FotMob possa divergir de um número nosso guardado em outro lugar, porque não
    /// existe número nosso guardado a partir daqui. A única coisa persistida no fluxo
    /// todo é Jogador.IdFotMob, que é o endereço, não o dado.
    ///
    /// A chamada acontece quando o usuário clica, nunca antes — a tela do jogador
    /// carrega sem tocar no FotMob.
    ///
    /// O QUE FICA DE FORA, DE PROPÓSITO:
    ///
    /// 1. A nota do jogador (FotMob rating), tanto a da temporada quanto a de cada
    ///    jogo. É a mesma decisão da importação (ver FotMobService): é a opinião do
    ///    modelo deles, e exibi-la ao lado da nota do site significaria mostrar dois
    ///    julgamentos concorrentes sobre a mesma partida. No jogo Al-Duhail 2x0
    ///    Al-Sailiya o site dá 10,0 ao Edmílson e o FotMob dá 9,39 — os dois estão
    ///    "certos" nas suas réguas, e o produto aqui é a régua do site.
    ///
    /// 2. O cabeçalho de identificação (nome, altura, nacionalidade, clube). O cadastro
    ///    já diverge hoje — o nosso diz 182 cm e Brasil, o deles diz 181 cm e Catar — e
    ///    repeti-lo faria o mesmo jogador aparecer com dados diferentes em duas telas do
    ///    próprio sistema. Quem chega aqui veio da página do jogador e já sabe quem é.
    ///
    /// Em compensação entra tudo que é só deles e não temos como calcular: xG, xGOT,
    /// xA e o percentil de cada métrica contra os outros jogadores da liga.
    /// </summary>
    public class FotMobPerfilService
    {
        private readonly FotMobService _fotmob;
        private readonly ILogger<FotMobPerfilService> _logger;

        public FotMobPerfilService(FotMobService fotmob, ILogger<FotMobPerfilService> logger)
        {
            _fotmob = fotmob;
            _logger = logger;
        }

        /// <param name="temporadaId">
        /// O "entryId" da temporada (ex.: "1-1"), como vem em statSeasons. NÃO é o ano:
        /// é um identificador opaco do FotMob que combina temporada e competição, e é a
        /// única forma aceita por playerStats. Null carrega a primeira da lista, que é a
        /// mais recente.
        /// </param>
        public async Task<EstatisticasAvancadasViewModel> MontarAsync(
            long idFotMob, string? temporadaId, CancellationToken ct = default)
        {
            var vm = new EstatisticasAvancadasViewModel { IdFotMob = idFotMob };

            using var perfil = await _fotmob.BuscarPerfilJogadorAsync(idFotMob, ct);
            if (perfil == null)
            {
                vm.Erro = "O FotMob não respondeu os dados deste jogador.";
                return vm;
            }

            var raiz = perfil.RootElement;

            vm.NomeNoFotMob = Texto(raiz, "name");
            vm.PosicaoPrincipal = PosicaoPrincipal(raiz);
            vm.Temporadas = LerTemporadas(raiz).ToList();
            vm.ValorDeMercado = ValorAtual(raiz);
            vm.Carreira = LerCarreira(raiz).ToList();
            vm.TemporadasCarreira = LerTemporadasDaCarreira(raiz).ToList();
            vm.Titulos = LerTitulos(raiz).ToList();
            vm.Jogos = LerJogos(raiz).ToList();
            vm.Situacao = LerLesao(raiz);

            // Sem competição escolhida usa a primeira da temporada mais recente, que é
            // a ordem em que a fonte já devolve. Jogador sem nenhuma não tem o bloco de
            // desempenho, mas o resto da tela continua valendo.
            var escolhida = vm.Temporadas
                .SelectMany(t => t.Competicoes.Select(c => (Temporada: t, Competicao: c)))
                .FirstOrDefault(x => x.Competicao.Id == temporadaId);

            if (escolhida.Competicao == null)
                escolhida = vm.Temporadas
                    .SelectMany(t => t.Competicoes.Select(c => (Temporada: t, Competicao: c)))
                    .FirstOrDefault();

            if (escolhida.Competicao == null) return vm;

            vm.CompeticaoSelecionada = escolhida.Competicao.Id;
            vm.TemporadaSelecionada = escolhida.Temporada.Nome;

            using var stats = await _fotmob.BuscarEstatisticasJogadorAsync(
                idFotMob, escolhida.Competicao.Id, ct);
            if (stats == null)
            {
                _logger.LogInformation(
                    "[FotMobPerfil] Jogador {Id}: sem estatísticas para a competição {Competicao}.",
                    idFotMob, escolhida.Competicao.Id);
                return vm;
            }

            vm.Grupos = LerDesempenho(stats.RootElement).ToList();
            return vm;
        }

        // ── Leitura do perfil ─────────────────────────────────────────────────

        private static string? Texto(JsonElement e, string propriedade) =>
            e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        /// <summary>
        /// Sub-bloco que deveria ser um objeto.
        ///
        /// Existe porque o FotMob manda NULL no lugar do bloco inteiro quando o jogador
        /// nao tem aquele dado - um garoto de 19 anos vem com "trophies": null, e nao
        /// com uma lista vazia. E JsonElement.TryGetProperty num null LANCA em vez de
        /// devolver false, entao a leitura encadeada derrubava a tela justamente para
        /// quem tem menos historia. Toda descida de um nivel passa por aqui.
        /// </summary>
        private static bool Bloco(JsonElement pai, string propriedade, out JsonElement filho)
        {
            filho = default;
            return pai.ValueKind == JsonValueKind.Object &&
                   pai.TryGetProperty(propriedade, out filho) &&
                   filho.ValueKind == JsonValueKind.Object;
        }

        /// <summary>Sub-bloco que deveria ser uma lista - mesma armadilha do null.</summary>
        private static bool Lista(JsonElement pai, string propriedade, out JsonElement itens)
        {
            itens = default;
            return pai.ValueKind == JsonValueKind.Object &&
                   pai.TryGetProperty(propriedade, out itens) &&
                   itens.ValueKind == JsonValueKind.Array;
        }

        /// <summary>
        /// A posição que o FotMob considera principal. Serve de conferência contra a
        /// nossa: a tela do jogador já mostra a distribuição por jogo ("PD 75%, CA
        /// 25%"), e ver as duas concordando (ou não) é informação de verdade.
        /// </summary>
        internal static string? PosicaoPrincipal(JsonElement raiz)
        {
            if (!Bloco(raiz, "positionDescription", out var pd) ||
                !Lista(pd, "positions", out var posicoes)) return null;

            foreach (var p in posicoes.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object) continue;

                if (!p.TryGetProperty("isMainPosition", out var principal) ||
                    principal.ValueKind != JsonValueKind.True) continue;

                if (Bloco(p, "strPos", out var s))
                    return TraduzirPosicao(Texto(s, "key"), Texto(s, "label"));
            }

            // Nenhuma marcada como principal: a de mais ocorrências é o melhor palpite.
            return posicoes.EnumerateArray()
                .OrderByDescending(p => Inteiro(p, "occurences") ?? 0)
                .Select(p => Bloco(p, "strPos", out var s)
                    ? TraduzirPosicao(Texto(s, "key"), Texto(s, "label"))
                    : null)
                .FirstOrDefault();
        }

        /// <summary>
        /// Nome da posição em português. O de-para é pela key do FotMob
        /// ("rightmidfielder"), que não muda com o idioma da resposta — esta rota
        /// responde em inglês mesmo quando se pede pt-BR.
        /// </summary>
        private static string? TraduzirPosicao(string? key, string? original) => key switch
        {
            "keeper" or "goalkeeper" => "Goleiro",
            "rightback" => "Lateral direito",
            "leftback" => "Lateral esquerdo",
            "centerback" or "centreback" => "Zagueiro",
            "rightwingback" => "Ala direito",
            "leftwingback" => "Ala esquerdo",
            "defensivemidfielder" => "Volante",
            "centermidfielder" or "centremidfielder" => "Meia central",
            "centerattackingmidfielder" or "attackingmidfielder" => "Meia atacante",
            "centerdefensivemidfielder" => "Volante",
            "rightmidfielder" => "Meia direito",
            "leftmidfielder" => "Meia esquerdo",
            "rightwinger" => "Ponta direita",
            "leftwinger" => "Ponta esquerda",
            "striker" or "centerforward" or "centreforward" => "Centroavante",
            _ => original,
        };

        /// <summary>
        /// Temporadas com estatística, cada uma com as competições disputadas nela.
        ///
        /// A fonte já entrega nessa forma (temporada -> torneios) e a ordem que ela usa
        /// é da mais recente para a mais antiga, que é a útil aqui — por isso nada é
        /// reordenado.
        /// </summary>
        internal static IEnumerable<TemporadaAgrupada> LerTemporadas(JsonElement raiz)
        {
            if (!Lista(raiz, "statSeasons", out var temporadas)) yield break;

            foreach (var t in temporadas.EnumerateArray())
            {
                var nome = Texto(t, "seasonName");
                if (nome == null) continue;

                if (!Lista(t, "tournaments", out var torneios)) continue;

                var competicoes = new List<CompeticaoTemporada>();

                foreach (var c in torneios.EnumerateArray())
                {
                    // Sem entryId não dá para pedir as estatísticas: é o único
                    // identificador que a rota de desempenho aceita.
                    var id = Texto(c, "entryId");
                    if (id == null) continue;

                    competicoes.Add(new CompeticaoTemporada
                    {
                        Id = id,
                        Nome = Texto(c, "name") ?? "Competição",
                        TemDadosDetalhados = c.TryGetProperty("hasDeepStats", out var d) &&
                                             d.ValueKind == JsonValueKind.True,
                    });
                }

                // Temporada sem nenhuma competição utilizável vira um botão que não
                // leva a lugar nenhum; melhor não existir.
                if (competicoes.Count == 0) continue;

                yield return new TemporadaAgrupada { Nome = nome, Competicoes = competicoes };
            }
        }

        private static string? ValorAtual(JsonElement raiz)
        {
            if (!Lista(raiz, "playerInformation", out var info)) return null;

            foreach (var item in info.EnumerateArray())
            {
                if (Texto(item, "translationKey") != "transfer_value") continue;
                if (Bloco(item, "value", out var v)) return Texto(v, "fallback");
            }

            return null;
        }

        /// <summary>
        /// Clubes e seleção, com jogos/gols/assistências em cada um. Não conflita com a
        /// nossa base: aqui está a carreira inteira do jogador, enquanto o site só tem
        /// os jogos que alguém importou.
        /// </summary>
        internal static IEnumerable<PassagemFotMob> LerCarreira(JsonElement raiz)
        {
            if (!Bloco(raiz, "careerHistory", out var carreira) ||
                !Bloco(carreira, "careerItems", out var itens)) yield break;

            foreach (var bloco in itens.EnumerateObject())
            {
                if (!Lista(bloco.Value, "teamEntries", out var passagens)) continue;

                foreach (var p in passagens.EnumerateArray())
                {
                    var time = Texto(p, "team");
                    if (time == null) continue;

                    yield return new PassagemFotMob
                    {
                        // "senior" / "national team" — o nome do bloco é a categoria.
                        Categoria = bloco.Name == "national team" ? "Seleção" : "Clube",
                        Time = time,
                        TimeId = Id64(p, "teamId"),
                        Periodo = Periodo(p),
                        Jogos = Texto(p, "appearances"),
                        Gols = Texto(p, "goals"),
                        Assistencias = Texto(p, "assists"),
                        Atual = p.TryGetProperty("active", out var a) && a.ValueKind == JsonValueKind.True,
                    };
                }
            }
        }

        /// <summary>
        /// A mesma carreira, mas quebrada por temporada — é o bloco irmão de
        /// "teamEntries" dentro de careerItems, e traz os números de cada ano já somados,
        /// com o detalhe por competição.
        ///
        /// Sai da resposta que já foi buscada para montar a tela: alternar o totalizador
        /// entre "carreira toda" e uma temporada não custa chamada nenhuma.
        /// </summary>
        internal static IEnumerable<TemporadaCarreira> LerTemporadasDaCarreira(JsonElement raiz)
        {
            if (!Bloco(raiz, "careerHistory", out var carreira) ||
                !Bloco(carreira, "careerItems", out var itens)) yield break;

            foreach (var bloco in itens.EnumerateObject())
            {
                if (!Lista(bloco.Value, "seasonEntries", out var temporadas)) continue;

                foreach (var t in temporadas.EnumerateArray())
                {
                    var nome = Texto(t, "seasonName");
                    if (nome == null) continue;

                    var competicoes = new List<CompeticaoNaTemporada>();
                    if (Lista(t, "tournamentStats", out var torneios))
                    {
                        foreach (var c in torneios.EnumerateArray())
                        {
                            var nomeComp = Texto(c, "leagueName");
                            if (nomeComp == null) continue;

                            competicoes.Add(new CompeticaoNaTemporada
                            {
                                Nome = nomeComp,
                                Jogos = NumeroTexto(c, "appearances"),
                                Gols = NumeroTexto(c, "goals"),
                                Assistencias = NumeroTexto(c, "assists"),
                            });
                        }
                    }

                    yield return new TemporadaCarreira
                    {
                        Nome = nome,
                        Time = Texto(t, "team") ?? "",
                        Selecao = bloco.Name == "national team",
                        Jogos = NumeroTexto(t, "appearances"),
                        Gols = NumeroTexto(t, "goals"),
                        Assistencias = NumeroTexto(t, "assists"),
                        Competicoes = competicoes,
                    };
                }
            }
        }

        /// <summary>
        /// Contagem que vem como TEXTO neste bloco ("76"), e às vezes como a string
        /// literal "undefined" nas competições antigas — que aqui vale zero, não erro.
        /// </summary>
        private static int NumeroTexto(JsonElement e, string propriedade) =>
            int.TryParse(Texto(e, propriedade), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var n) ? n : 0;

        private static string Periodo(JsonElement passagem)
        {
            var inicio = Data(Texto(passagem, "startDate"));
            var fim = Data(Texto(passagem, "endDate")) ?? "hoje";
            return inicio == null ? fim : $"{inicio} — {fim}";
        }

        private static string? Data(string? iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal, out var d)
                ? d.ToString("MM/yyyy", CultureInfo.InvariantCulture) : null;

        internal static IEnumerable<TituloFotMob> LerTitulos(JsonElement raiz)
        {
            if (!Bloco(raiz, "trophies", out var trofeus) ||
                !Lista(trofeus, "playerTrophies", out var porTime)) yield break;

            foreach (var time in porTime.EnumerateArray())
            {
                if (!Lista(time, "tournaments", out var competicoes)) continue;

                foreach (var c in competicoes.EnumerateArray())
                {
                    var temporadas = Lista(c, "seasonsWon", out var w)
                        ? w.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => x.GetString()).ToList()
                        : new List<string?>();

                    // Vice não entra: a tela é de títulos, e o FotMob mistura os dois
                    // no mesmo bloco.
                    if (temporadas.Count == 0) continue;

                    yield return new TituloFotMob
                    {
                        Time = Texto(time, "teamName") ?? "",
                        TimeId = Id64(time, "teamId"),
                        Competicao = Texto(c, "leagueName") ?? "",
                        Temporadas = string.Join(" · ", temporadas),
                        Quantidade = temporadas.Count,
                    };
                }
            }
        }

        /// <summary>
        /// Últimos jogos segundo o FotMob — SEM a nota que ele dá a cada um.
        ///
        /// Vale a pena mesmo o site tendo o próprio histórico: aqui aparecem as partidas
        /// que ninguém importou (jogos de seleção, outras competições), então a lista é
        /// bem maior. Por isso ela é rotulada como sendo do FotMob na tela: ver "42
        /// jogos" aqui e "4 jogos" na página do jogador não é divergência, é recorte
        /// diferente — e o usuário precisa saber disso olhando.
        /// </summary>
        internal static IEnumerable<JogoFotMob> LerJogos(JsonElement raiz)
        {
            if (!Lista(raiz, "recentMatches", out var jogos)) yield break;

            foreach (var j in jogos.EnumerateArray())
            {
                if (j.ValueKind != JsonValueKind.Object) continue;

                if (j.TryGetProperty("playedInMatch", out var jogou) &&
                    jogou.ValueKind == JsonValueKind.False) continue;

                var casa = j.TryGetProperty("isHomeTeam", out var h) && h.ValueKind == JsonValueKind.True;
                var golsCasa = Inteiro(j, "homeScore");
                var golsFora = Inteiro(j, "awayScore");

                DateTime? data = null;
                if (Bloco(j, "matchDate", out var md) &&
                    DateTime.TryParse(Texto(md, "utcTime"), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out var d)) data = d;

                yield return new JogoFotMob
                {
                    Data = data,
                    Competicao = Texto(j, "leagueName"),
                    Adversario = Texto(j, "opponentTeamName"),
                    AdversarioId = Id64(j, "opponentTeamId"),
                    Mandante = casa,
                    Placar = golsCasa == null || golsFora == null ? null : $"{golsCasa} x {golsFora}",
                    Minutos = Inteiro(j, "minutesPlayed"),
                    Gols = Inteiro(j, "goals") ?? 0,
                    Assistencias = Inteiro(j, "assists") ?? 0,
                    CartoesAmarelos = Inteiro(j, "yellowCards") ?? 0,
                    CartoesVermelhos = Inteiro(j, "redCards") ?? 0,
                };
            }
        }

        /// <summary>
        /// Id de time. Vem ora como número, ora como texto conforme o bloco do payload,
        /// e é o que monta o escudo na tela (/MediaProxy/Escudo/{id}).
        /// </summary>
        private static long? Id64(JsonElement e, string propriedade)
        {
            if (e.ValueKind != JsonValueKind.Object ||
                !e.TryGetProperty(propriedade, out var v)) return null;

            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetInt64(),
                JsonValueKind.String => long.TryParse(v.GetString(), out var n) ? n : null,
                _ => null,
            };
        }

        private static int? Inteiro(JsonElement e, string propriedade) =>
            e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.Number &&
            v.TryGetInt32(out var n) ? n : null;

        // ── Leitura do desempenho na temporada ────────────────────────────────

        /// <summary>
        /// O bloco "Desempenho na temporada": grupos (finalização, passe, posse, defesa,
        /// disciplina) com valor total, média por 90 minutos e o percentil do jogador
        /// naquela métrica dentro da competição.
        ///
        /// O percentil é o que essa tela tem de mais valioso e é impossível de reproduzir
        /// com a nossa base: ele compara o jogador com todos os outros da liga. Por ser
        /// cálculo do FotMob (e não fato da partida), a tela o identifica como tal.
        /// </summary>
        internal static IEnumerable<GrupoMetricasFotMob> LerDesempenho(JsonElement raiz)
        {
            if (!Bloco(raiz, "statsSection", out var secao) ||
                !Lista(secao, "items", out var grupos)) yield break;

            foreach (var g in grupos.EnumerateArray())
            {
                if (!Lista(g, "items", out var metricas)) continue;

                var linhas = new List<MetricaFotMob>();

                foreach (var m in metricas.EnumerateArray())
                {
                    var titulo = Texto(m, "title");
                    if (titulo == null) continue;

                    // statFormat "percent" muda como a linha é lida: o valor já é uma
                    // taxa, então "84,8 por 90 minutos" não quer dizer nada — o FotMob
                    // repete a própria taxa no per90, e mostrá-la seria ruído.
                    var percentual = Texto(m, "statFormat") == "percent";

                    linhas.Add(new MetricaFotMob
                    {
                        Nome = TraduzirMetrica(Texto(m, "localizedTitleId"), titulo),
                        Valor = ComVirgula(Texto(m, "statValue")),
                        EhPercentual = percentual,
                        Por90 = percentual ? null : Numero(m, "per90"),
                        Percentil = Numero(m, "percentileRank"),
                    });
                }

                if (linhas.Count == 0) continue;

                yield return new GrupoMetricasFotMob
                {
                    Nome = TraduzirGrupo(Texto(g, "localizedTitleId")),
                    Metricas = linhas,
                };
            }
        }

        /// <summary>
        /// O statValue vem como texto já formatado pelo FotMob, com ponto decimal
        /// ("87.6"). A tela é em português: só o separador muda, o número não é
        /// reformatado — arredondar de novo poderia discordar do que eles mostram.
        /// </summary>
        private static string ComVirgula(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : valor.Replace('.', ',');

        private static double? Numero(JsonElement e, string propriedade) =>
            e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(propriedade, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble() : null;

        // O FotMob responde os títulos em inglês mesmo com lang=pt-BR nesta rota. O
        // de-para é por localizedTitleId (id estável do payload, conferido contra as
        // temporadas reais deste jogador), com o título original de reserva — métrica
        // nova aparece em inglês em vez de sumir da tela.
        private static string TraduzirMetrica(string? id, string original) => id switch
        {
            // Finalização
            "goals" => "Gols",
            "shots" => "Finalizações",
            "ShotsOnTarget" => "Finalizações no gol",
            "expected_goals" => "Gols esperados (xG)",
            "expected_goals_on_target" => "xG no alvo (xGOT)",
            "non_penalty_xg" => "xG sem pênaltis",

            // Passe e criação
            "assists" => "Assistências",
            "expected_assists" => "Assistências esperadas (xA)",
            "successful_passes" => "Passes certos",
            "successful_passes_accuracy" => "Precisão de passe",
            "long_balls_accurate" => "Bolas longas certas",
            "long_ball_succeeeded_accuracy" => "Precisão de bola longa",
            "chances_created" => "Chances criadas",
            "big_chance_created_team_title" => "Grandes chances criadas",
            "crosses_succeeeded" => "Cruzamentos certos",
            "crosses_succeeeded_accuracy" => "Precisão de cruzamento",

            // Posse
            "dribbles_succeeded" => "Dribles certos",
            "won_contest_subtitle" => "Sucesso em dribles",
            "duel_won" => "Duelos vencidos",
            "duel_won_percent" => "Aproveitamento em duelos",
            "aerials_won" => "Duelos aéreos vencidos",
            "aerials_won_percent" => "Aproveitamento aéreo",
            "touches" => "Toques",
            "touches_opp_box" => "Toques na área adversária",
            "dispossessed" => "Perdas de posse",
            "fouls_won" => "Faltas sofridas",
            "penalty_won_title" => "Pênaltis sofridos",

            // Defesa
            "defensive_actions" => "Ações defensivas",
            "matchstats.headers.tackles" => "Desarmes",
            "interceptions" => "Interceptações",
            "blocked_shots" => "Finalizações bloqueadas",
            "fouls" => "Faltas cometidas",
            "recoveries" => "Bolas recuperadas",
            "poss_won_att_3rd_team_title" => "Posse recuperada no último terço",
            "dribbled_past" => "Dribles sofridos",
            "clearances" => "Cortes",
            "clean_sheet_team_title" => "Jogos sem sofrer gol",
            "goals_conceded_while_on_pitch" => "Gols sofridos em campo",
            "expected_goals_against_while_on_pitch" => "xG sofrido em campo",

            // Disciplina
            "yellow_cards" => "Cartões amarelos",
            "red_cards" => "Cartões vermelhos",

            _ => original,
        };

        // ── Situação física ───────────────────────────────────────────────────

        /// <summary>
        /// Lesão atual do jogador, para o selo no cabeçalho de /Jogadores/Estatisticas.
        ///
        /// É a única informação do FotMob que aparece fora da tela de estatísticas
        /// avançadas, e entra por um motivo diferente do resto: não é um número que
        /// concorra com um número nosso — não temos cadastro de lesão nenhum —, é um
        /// fato sobre o jogador que muda como se lê a página inteira. Uma média que
        /// parou de subir em setembro se explica sozinha quando o cabeçalho diz que ele
        /// rompeu o cruzado em setembro.
        ///
        /// Continua sem gravar nada, pelo motivo explicado em SituacaoFisicaViewModel.
        /// Null = jogador sem lesão informada (o caso normal) ou fonte fora do ar; a
        /// tela simplesmente não mostra o selo, nunca um erro.
        /// </summary>
        public async Task<SituacaoFisicaViewModel?> SituacaoFisicaAsync(
            long idFotMob, CancellationToken ct = default)
        {
            using var perfil = await _fotmob.BuscarPerfilJogadorAsync(idFotMob, ct);
            return perfil == null ? null : LerLesao(perfil.RootElement);
        }

        /// <summary>
        /// A leitura em si, separada da busca porque as duas telas chegam aqui por
        /// caminhos diferentes: a de estatísticas avançadas já tem o perfil na mão
        /// (MontarAsync acabou de buscá-lo) e não pode gastar uma segunda chamada só
        /// para repetir o mesmo bloco.
        /// </summary>
        private static SituacaoFisicaViewModel? LerLesao(JsonElement raiz)
        {
            // Jogador sem lesão vem com "injuryInformation": null — daí Bloco(), que já
            // existe justamente porque TryGetProperty num null lança.
            if (!Bloco(raiz, "injuryInformation", out var lesao)) return null;

            var nome = Texto(lesao, "name");
            if (string.IsNullOrWhiteSpace(nome)) return null;

            return new SituacaoFisicaViewModel
            {
                Lesao = TraduzirLesao(nome),
                Retorno = Retorno(lesao),
                AtualizadoEm = AtualizadoEm(lesao),
            };
        }

        /// <summary>
        /// Previsão de retorno. O FotMob manda uma chave de tradução, uma data opcional
        /// e um texto pronto em inglês; a data, quando existe, é a informação mais útil
        /// e por isso vem antes da chave.
        /// </summary>
        private static string? Retorno(JsonElement lesao)
        {
            if (!Bloco(lesao, "expectedReturn", out var retorno)) return null;

            var data = Texto(retorno, "expectedReturnDateParam");
            if (!string.IsNullOrWhiteSpace(data) &&
                DateTime.TryParse(data, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando))
                return $"Retorno esperado: {quando.ToLocalTime():dd/MM/yyyy}";

            return Texto(retorno, "expectedReturnKey") switch
            {
                "expected_return_date_out_for_season" => "Fora por toda a temporada",
                "expected_return_date_unknown" => "Retorno sem previsão",
                "expected_return_date_doubtful" => "Presença em dúvida",
                // Chave nova da fonte: o texto pronto deles é em inglês, mas dizer algo
                // certo em inglês é melhor do que inventar uma tradução ou omitir.
                _ => Texto(retorno, "expectedReturnFallback"),
            };
        }

        private static DateTime? AtualizadoEm(JsonElement lesao)
        {
            if (!Bloco(lesao, "lastUpdated", out var bloco)) return null;
            var utc = Texto(bloco, "utcTime");
            return DateTime.TryParse(utc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var quando)
                ? quando.ToLocalTime() : null;
        }

        /// <summary>
        /// Nome da lesão em português.
        ///
        /// A rota de dados responde sempre em inglês (o site deles traduz no navegador,
        /// por uma chave que não é pública), então a tradução é nossa. A lista cobre o
        /// que aparece no dia a dia; o que faltar sai em inglês em vez de sumir, porque
        /// "Patellar tendon rupture" ainda informa mais do que um selo vazio.
        /// </summary>
        private static string TraduzirLesao(string nome) => nome.Trim().ToLowerInvariant() switch
        {
            "cruciate ligament injury" => "Lesão do ligamento cruzado",
            "cruciate ligament rupture" => "Ruptura do ligamento cruzado",
            "knee injury" => "Lesão no joelho",
            "meniscus injury" => "Lesão no menisco",
            "ankle injury" => "Lesão no tornozelo",
            "foot injury" => "Lesão no pé",
            "hamstring injury" => "Lesão na posterior da coxa",
            "thigh muscle strain" => "Estiramento na coxa",
            "muscle injury" => "Lesão muscular",
            "muscular problems" => "Problemas musculares",
            "calf injury" => "Lesão na panturrilha",
            "groin injury" or "groin strain" => "Lesão na virilha",
            "adductor problems" => "Problemas no adutor",
            "achilles tendon problems" => "Problemas no tendão de Aquiles",
            "achilles tendon rupture" => "Ruptura do tendão de Aquiles",
            "back injury" or "back problems" => "Lesão nas costas",
            "shoulder injury" => "Lesão no ombro",
            "hip injury" or "hip problems" => "Lesão no quadril",
            "head injury" => "Traumatismo craniano",
            "concussion" => "Concussão",
            "fracture" or "broken bone" => "Fratura",
            "illness" => "Doença",
            "knock" => "Pancada",
            "fitness" => "Condicionamento",
            "unknown injury" => "Lesão não especificada",
            _ => nome,
        };

        private static string TraduzirGrupo(string? id) => id switch
        {
            "shooting" => "Finalização",
            "passing" => "Passe e criação",
            "possession" => "Posse",
            "defending" => "Defesa",
            "discipline" => "Disciplina",
            _ => id ?? "Outras",
        };
    }
}
