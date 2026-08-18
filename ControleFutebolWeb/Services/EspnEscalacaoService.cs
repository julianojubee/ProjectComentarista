using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Confere a escalação inicial salva contra o XI que a ESPN publicou, e aplica a
    /// correção quando o usuário manda.
    ///
    /// Existe porque, quando a api-football devolve a partida sem lineup, a tela de
    /// Analisar monta a escalação a partir do último jogo do time (EscalacaoBaseHelper).
    /// O campo fica preenchido com gente que nem entrou, e não há nada indicando isso.
    ///
    /// Conferir e aplicar são passos separados de propósito: a escalação costuma ter
    /// ajuste manual do analista, e sobrescrever isso sem mostrar antes destruiria
    /// trabalho que não dá para recuperar.
    /// </summary>
    public class EspnEscalacaoService
    {
        private readonly EspnEstatisticasService _espn;
        private readonly ILogger<EspnEscalacaoService> _logger;

        public EspnEscalacaoService(EspnEstatisticasService espn, ILogger<EspnEscalacaoService> logger)
        {
            _espn = espn;
            _logger = logger;
        }

        // ── Conferência ───────────────────────────────────────────────────────

        /// <param name="usuarioAlvoId">
        /// De quem é a escalação sendo conferida. Pode não ser o usuário logado: a tela
        /// é de administração e serve para arrumar o jogo de qualquer analista.
        /// </param>
        public async Task<ConferenciaEscalacaoViewModel> ConferirAsync(
            FutebolContext context, int jogoId, string usuarioAlvoId, CancellationToken ct = default)
        {
            var vm = new ConferenciaEscalacaoViewModel
            {
                JogoId = jogoId,
                UsuarioSelecionadoId = usuarioAlvoId,
            };

            var (jogo, doc, _, erro) = await _espn.AbrirResumoAsync(context, jogoId, exigeIdApi: false, ct);
            if (jogo != null)
            {
                vm.Data = jogo.Data;
                vm.Partida = $"{jogo.TimeCasa?.Nome} x {jogo.TimeVisitante?.Nome}";
            }
            if (erro != null) { vm.Erro = erro.Mensagem; return vm; }

            using var _doc = doc!;
            if (!_doc.RootElement.TryGetProperty("rosters", out var rosters))
            {
                vm.Erro = "A ESPN não publicou a escalação desta partida.";
                return vm;
            }

            foreach (var roster in rosters.EnumerateArray())
            {
                var lado = await ConferirLadoAsync(context, jogo!, roster, usuarioAlvoId, ct);
                if (lado != null) vm.Lados.Add(lado);
            }

            if (vm.Lados.Count == 0)
            {
                vm.Erro = "Não foi possível casar os times da ESPN com os do jogo.";
                return vm;
            }

            vm.Lados = vm.Lados.OrderByDescending(l => l.IsTimeCasa).ToList();
            vm.Usuarios = await LevantarUsuariosAsync(context, jogo!, rosters, usuarioAlvoId, ct);

            // O que aplicar custa, para a tela poder avisar antes.
            vm.SetasAfetadas = await context.SetasEscalacao
                .CountAsync(s => s.Escalacao.JogoId == jogoId
                              && s.Escalacao.FaseEscalacao == "INICIAL"
                              && s.Escalacao.UsuarioId == usuarioAlvoId, ct);

            vm.TemFaseFinal = await context.Escalacoes
                .AnyAsync(e => e.JogoId == jogoId && e.FaseEscalacao == "FINAL"
                            && e.UsuarioId == usuarioAlvoId && e.JogadorId != null, ct);

            vm.NotasSuspeitas = await LevantarNotasSuspeitasAsync(context, jogo!, rosters, usuarioAlvoId, ct);

            vm.Ok = true;
            return vm;
        }

        /// <summary>
        /// Notas que o usuário deu a jogadores que a ESPN não registra como tendo
        /// entrado em campo. Aparecem quando a escalação errada levou o analista a
        /// avaliar quem nem jogou.
        ///
        /// Só lista — remover é decisão dele, um a um. A lista erra para o lado de
        /// mostrar demais: jogador que não casou com ninguém do roster entra aqui
        /// marcado, porque pode ser só divergência de nome.
        /// </summary>
        private async Task<List<NotaSuspeitaItem>> LevantarNotasSuspeitasAsync(
            FutebolContext context, Jogo jogo, JsonElement rosters, string usuarioId, CancellationToken ct)
        {
            var notas = await context.Notas
                .Where(n => n.JogoId == jogo.Id && n.UsuarioId == usuarioId)
                .Select(n => new
                {
                    n.Id, n.JogadorId, Jogador = n.Jogador.Nome,
                    n.Valor, n.NotaManual, n.Comentario, n.IsAutomatica,
                })
                .ToListAsync(ct);

            if (notas.Count == 0) return new();

            // Quem a ESPN diz que entrou em campo, resolvido para o nosso cadastro.
            var atuaram = new HashSet<int>();
            var listados = new HashSet<int>();

            foreach (var roster in rosters.EnumerateArray())
            {
                var nomeTime = roster.GetProperty("team").GetProperty("displayName").GetString();
                var time = TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeTime) ? jogo.TimeCasa
                         : TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeTime) ? jogo.TimeVisitante
                         : null;
                if (time == null) continue;

                var elenco = await ElencoAsync(context, time.Id, ct);
                foreach (var a in AtletasDaEspn(roster))
                {
                    var jogador = Casar(elenco, a.Nome, a.Numero)?.Jogador;
                    if (jogador == null) continue;
                    listados.Add(jogador.Id);
                    if (a.Atuou) atuaram.Add(jogador.Id);
                }
            }

            return notas
                .Where(n => !atuaram.Contains(n.JogadorId))
                .Select(n => new NotaSuspeitaItem
                {
                    NotaId = n.Id,
                    Jogador = n.Jogador,
                    Valor = n.Valor,
                    NotaManual = n.NotaManual,
                    Comentario = n.Comentario,
                    EhAutomatica = n.IsAutomatica,
                    JogadorNaoLocalizadoNaEspn = !listados.Contains(n.JogadorId),
                })
                .OrderBy(n => n.Jogador, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Todo mundo que tem escalação própria neste jogo (mais o usuário que está
        /// olhando, mesmo sem escalação), com quantas divergências cada um tem. É o que
        /// permite ver de cara quem precisa de ajuste em vez de abrir um por um.
        /// </summary>
        private async Task<List<UsuarioEscalacaoItem>> LevantarUsuariosAsync(
            FutebolContext context, Jogo jogo, JsonElement rosters, string usuarioAtualId, CancellationToken ct)
        {
            var comEscalacao = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.FaseEscalacao == "INICIAL" && e.UsuarioId != null)
                .Select(e => e.UsuarioId!)
                .Distinct()
                .ToListAsync(ct);

            var ids = comEscalacao.Append(usuarioAtualId).Distinct().ToList();

            var nomes = await context.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Id, ct);

            var notasPorUsuario = await context.Notas
                .Where(n => n.JogoId == jogo.Id && n.UsuarioId != null && ids.Contains(n.UsuarioId))
                .GroupBy(n => n.UsuarioId!)
                .Select(g => new { UsuarioId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.UsuarioId, x => x.Total, ct);

            var lista = new List<UsuarioEscalacaoItem>();

            foreach (var id in ids)
            {
                var divergencias = 0;
                foreach (var roster in rosters.EnumerateArray())
                {
                    var lado = await ConferirLadoAsync(context, jogo, roster, id, ct);
                    if (lado == null) continue;
                    divergencias += lado.Saem.Count
                                  + lado.Titulares.Count(t => t.Situacao != SituacaoEscalacao.Confere);
                }

                lista.Add(new UsuarioEscalacaoItem
                {
                    Id = id,
                    Nome = nomes.GetValueOrDefault(id, id),
                    Divergencias = divergencias,
                    SemEscalacaoPropria = !comEscalacao.Contains(id),
                    Notas = notasPorUsuario.GetValueOrDefault(id, 0),
                });
            }

            // Quem tem mais problema primeiro; sem divergência, ordem alfabética.
            return lista
                .OrderByDescending(u => u.Divergencias)
                .ThenBy(u => u.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<LadoEscalacaoEspn?> ConferirLadoAsync(
            FutebolContext context, Jogo jogo, JsonElement roster, string usuarioId, CancellationToken ct)
        {
            var nomeTime = roster.GetProperty("team").GetProperty("displayName").GetString();
            bool? ehCasa = TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeTime) ? true
                         : TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeTime) ? false
                         : null;
            if (ehCasa == null) return null;

            var time = ehCasa.Value ? jogo.TimeCasa! : jogo.TimeVisitante!;
            var elenco = await ElencoAsync(context, time.Id, ct);
            var escaladosTitulares = await TitularesSalvosAsync(context, jogo.Id, ehCasa.Value, usuarioId, ct);

            var formacaoEspn = roster.TryGetProperty("formation", out var f) ? f.GetString() : null;
            var formacaoSalvaId = ehCasa.Value ? jogo.FormacaoCasaId : jogo.FormacaoVisitanteId;
            var formacaoSalva = formacaoSalvaId == null ? null
                : await context.Formacoes.Where(x => x.Id == formacaoSalvaId).Select(x => x.Nome).FirstOrDefaultAsync(ct);

            var existeFormacaoEspn = formacaoEspn != null
                && await context.Formacoes.AnyAsync(x => x.Nome == formacaoEspn, ct);

            var lado = new LadoEscalacaoEspn
            {
                IsTimeCasa = ehCasa.Value,
                Time = time.Nome ?? "—",
                FormacaoSalva = formacaoSalva,
                FormacaoEspn = formacaoEspn,
                // Formação que não existe no cadastro não é divergência acionável.
                FormacaoDiverge = existeFormacaoEspn && !string.Equals(formacaoSalva, formacaoEspn, StringComparison.OrdinalIgnoreCase),
            };

            var casadosNoXi = new HashSet<int>();

            foreach (var a in TitularesDaEspn(roster))
            {
                var casado = Casar(elenco, a.Nome, a.Numero);
                var item = new ItemEscalacaoEspn
                {
                    Nome = a.Nome,
                    Numero = a.Numero,
                    Posicao = a.Posicao,
                };

                if (casado == null)
                {
                    item.Situacao = SituacaoEscalacao.Criar;
                }
                else
                {
                    if (!NomeJogadorHelper.Corresponde(casado.Jogador.Nome, a.Nome))
                        item.NomeNoCadastro = casado.Jogador.Nome;
                    item.CasadoPor = casado.Por;

                    if (escaladosTitulares.Contains(casado.Jogador.Id))
                    {
                        item.Situacao = SituacaoEscalacao.Confere;
                        casadosNoXi.Add(casado.Jogador.Id);
                    }
                    else
                    {
                        item.Situacao = SituacaoEscalacao.Entra;
                    }
                }

                lado.Titulares.Add(item);
            }

            // Quem está escalado como titular e a ESPN não confirmou.
            var sobrando = escaladosTitulares.Except(casadosNoXi).ToList();
            if (sobrando.Count > 0)
                lado.Saem = await context.Jogadores
                    .Where(j => sobrando.Contains(j.Id))
                    .Select(j => j.Nome)
                    .ToListAsync(ct);

            return lado;
        }

        // ── Aplicação ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grava a escalação inicial da ESPN para o usuário: cadastra quem falta no
        /// elenco, ajusta a formação e distribui os titulares nos slots dela. Só mexe
        /// na fase INICIAL e só nas linhas deste usuário.
        /// </summary>
        public async Task<ResultadoEspn> AplicarAsync(
            FutebolContext context, int jogoId, string usuarioId, CancellationToken ct = default)
        {
            var (jogo, doc, evento, erro) = await _espn.AbrirResumoAsync(context, jogoId, exigeIdApi: false, ct);
            if (erro != null) return erro;

            using var _doc = doc!;
            if (!_doc.RootElement.TryGetProperty("rosters", out var rosters))
                return new ResultadoEspn(false, "A ESPN não publicou a escalação desta partida.", evento);

            var lados = 0;
            var criados = 0;

            foreach (var roster in rosters.EnumerateArray())
            {
                var resultado = await AplicarLadoAsync(context, jogo!, roster, usuarioId, ct);
                if (resultado == null) continue;
                lados++;
                criados += resultado.Value;
            }

            if (lados == 0)
                return new ResultadoEspn(false, "Não foi possível casar os times da ESPN com os do jogo.", evento);

            await context.SaveChangesAsync(ct);

            var msg = $"Escalação inicial de {lados} time(s) atualizada pela ESPN";
            msg += criados > 0 ? $" ({criados} jogador(es) cadastrado(s) no elenco)." : ".";
            return new ResultadoEspn(true, msg, evento);
        }

        /// <returns>Quantos jogadores precisaram ser cadastrados, ou null se o time não casou.</returns>
        private async Task<int?> AplicarLadoAsync(
            FutebolContext context, Jogo jogo, JsonElement roster, string usuarioId, CancellationToken ct)
        {
            var nomeTime = roster.GetProperty("team").GetProperty("displayName").GetString();
            bool? ehCasa = TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeTime) ? true
                         : TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeTime) ? false
                         : null;
            if (ehCasa == null) return null;

            var time = ehCasa.Value ? jogo.TimeCasa! : jogo.TimeVisitante!;
            var elenco = await ElencoAsync(context, time.Id, ct);
            var criados = 0;

            // Cadastra quem a ESPN lista e o elenco não tem. Mesmo caminho do "+" da
            // tela de análise: entra sem IdApi, e a importação da api-football
            // reaproveita o cadastro pelo nome depois em vez de duplicar.
            async Task<Jogador> ResolverOuCriarAsync(EspnAtleta a)
            {
                var jogador = Casar(elenco, a.Nome, a.Numero)?.Jogador;
                if (jogador != null) return jogador;

                jogador = new Jogador
                {
                    Nome = a.Nome,
                    // A ESPN não diz a posição de quem começou no banco: manda
                    // "Substituto" para todo mundo. Isso não é posição e não pode ir
                    // para o cadastro — Jogador.Posicao alimenta filtro e régua de
                    // nota. Fica vazio, e Serviços › "Recalcular posições" preenche
                    // depois a partir das escalações reais.
                    Posicao = EhSubstituto(a.Posicao) ? "" : a.Posicao,
                    NumeroCamisa = a.Numero,
                    TimeId = time.Id,
                    SelecaoId = time.EhSelecao ? time.Id : null,
                    DtInc = DateTime.UtcNow,
                };
                context.Jogadores.Add(jogador);
                await context.SaveChangesAsync(ct);
                elenco.Add(jogador);
                criados++;
                _logger.LogInformation(
                    "[EspnEscalacao] {Nome} (camisa {Numero}) cadastrado em {Time} pelo jogo {Jogo}",
                    jogador.Nome, a.Numero, time.Nome, jogo.Id);
                return jogador;
            }

            // 1) Resolve (ou cadastra) cada titular do XI da ESPN.
            var titulares = new List<(Jogador Jogador, EspnAtleta Atleta)>();
            foreach (var a in TitularesDaEspn(roster))
                titulares.Add((await ResolverOuCriarAsync(a), a));

            if (titulares.Count == 0) return criados;

            // 2) Formação: usa a da ESPN quando existe no cadastro; senão mantém a atual.
            var formacaoEspn = roster.TryGetProperty("formation", out var f) ? f.GetString() : null;
            var formacaoId = ehCasa.Value ? jogo.FormacaoCasaId : jogo.FormacaoVisitanteId;
            if (formacaoEspn != null)
            {
                var achada = await context.Formacoes
                    .Where(x => x.Nome == formacaoEspn).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
                if (achada != null) formacaoId = achada;
            }

            var slots = formacaoId == null ? new List<PosicaoFormacao>()
                : await context.PosicoesFormacao.AsNoTracking()
                    .Where(p => p.FormacaoId == formacaoId).OrderBy(p => p.Ordem).ToListAsync(ct);

            // 3) Reservas: quem a ESPN lista e não é titular vai para o banco, para o
            // analista ter de onde puxar nas substituições. Também são cadastrados
            // quando faltam no elenco — o banco é parte da escalação, e um reserva que
            // entrou aos 60' precisa existir para receber nota.
            var reservas = new List<Jogador>();
            foreach (var a in AtletasDaEspn(roster).Where(x => !x.Titular))
            {
                var jogador = await ResolverOuCriarAsync(a);
                if (titulares.All(t => t.Jogador.Id != jogador.Id)
                    && reservas.All(r => r.Id != jogador.Id))
                    reservas.Add(jogador);
            }

            // 4) Substitui a escalação INICIAL deste usuário. As linhas dos outros
            //    usuários e a compartilhada (UsuarioId null) não são tocadas.
            var antigas = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.IsTimeCasa == ehCasa.Value
                         && e.FaseEscalacao == "INICIAL" && e.UsuarioId == usuarioId)
                .ToListAsync(ct);
            context.Escalacoes.RemoveRange(antigas);

            foreach (var (jogador, slot, atleta) in DistribuirNosSlots(titulares, slots))
                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogador.Id,
                    Posicao = slot?.NomePosicao ?? atleta.Posicao,
                    PosicaoX = slot?.PosicaoX ?? 0,
                    PosicaoY = slot?.PosicaoY ?? 0,
                    IsTimeCasa = ehCasa.Value,
                    Titular = true,
                    FaseEscalacao = "INICIAL",
                    UsuarioId = usuarioId,
                });

            foreach (var r in reservas)
                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = r.Id,
                    Posicao = "RES",
                    PosicaoX = 0,
                    PosicaoY = 0,
                    IsTimeCasa = ehCasa.Value,
                    Titular = false,
                    FaseEscalacao = "INICIAL",
                    UsuarioId = usuarioId,
                });

            if (formacaoId != null)
            {
                if (ehCasa.Value) jogo.FormacaoCasaId = formacaoId;
                else jogo.FormacaoVisitanteId = formacaoId;
            }

            return criados;
        }

        // ── Leitura do roster da ESPN ─────────────────────────────────────────

        /// <param name="Atuou">
        /// A ESPN marca em "appearances" quem de fato entrou em campo — o reserva que
        /// ficou no banco o jogo todo vem com 0. É esse campo que separa "não jogou"
        /// de "jogou", e não o simples fato de estar na lista.
        /// </param>
        public record EspnAtleta(string Nome, int? Numero, string Posicao, bool Titular, bool Atuou = true);

        private static IEnumerable<EspnAtleta> AtletasDaEspn(JsonElement roster)
        {
            if (!roster.TryGetProperty("roster", out var lista)) yield break;

            foreach (var a in lista.EnumerateArray())
            {
                var nome = a.GetProperty("athlete").GetProperty("displayName").GetString();
                if (string.IsNullOrWhiteSpace(nome)) continue;

                int? numero = a.TryGetProperty("jersey", out var j) && int.TryParse(j.GetString(), out var n) ? n : null;
                var posicao = a.TryGetProperty("position", out var p) && p.TryGetProperty("displayName", out var pd)
                    ? pd.GetString() ?? "" : "";
                var titular = a.TryGetProperty("starter", out var s) && s.GetBoolean();

                var atuou = titular;
                if (a.TryGetProperty("stats", out var stats))
                    foreach (var st in stats.EnumerateArray())
                        if (st.TryGetProperty("name", out var sn) && sn.GetString() == "appearances"
                            && st.TryGetProperty("value", out var sv) && sv.ValueKind == JsonValueKind.Number)
                            atuou = sv.GetDouble() > 0;

                yield return new EspnAtleta(nome, numero, posicao, titular, atuou);
            }
        }

        private static IEnumerable<EspnAtleta> TitularesDaEspn(JsonElement roster)
            => AtletasDaEspn(roster).Where(a => a.Titular);

        // ── Casamento e ordenação ─────────────────────────────────────────────

        private record Casado(Jogador Jogador, string Por);

        /// <summary>
        /// Acha o jogador do elenco correspondente ao atleta da ESPN: por nome
        /// primeiro, por número da camisa depois — é o que resolve "Joao Victor"
        /// (ESPN) x "Victor Sá" (cadastro).
        ///
        /// Vários candidatos NÃO é motivo para desistir. O cadastro já tem duplicatas
        /// vindas de importações antigas ("Keo Boets" e "K. Boets", os dois com a
        /// camisa 71), e exigir candidato único fazia o chamador cadastrar um terceiro
        /// registro do mesmo jogador. Desempata em vez de criar.
        /// </summary>
        private static Casado? Casar(List<Jogador> elenco, string nome, int? numero)
        {
            var porNome = elenco.Where(j => NomeJogadorHelper.Corresponde(j.Nome, nome)).ToList();
            if (porNome.Count == 1) return new Casado(porNome[0], "nome");
            if (porNome.Count > 1) return new Casado(Desempatar(porNome, nome, numero), "nome");

            if (numero != null)
            {
                var porCamisa = elenco.Where(j => j.NumeroCamisa == numero).ToList();
                if (porCamisa.Count == 1) return new Casado(porCamisa[0], "camisa");
                if (porCamisa.Count > 1) return new Casado(Desempatar(porCamisa, nome, numero), "camisa");
            }

            return null;
        }

        /// <summary>
        /// Entre cadastros do mesmo jogador, escolhe o melhor: nome idêntico ao da
        /// ESPN ganha da abreviação, depois a camisa que confere, depois quem já está
        /// vinculado à api-football (é o registro que a importação mantém atualizado),
        /// e por fim o mais antigo — que é o que os jogos anteriores referenciam.
        /// </summary>
        public static Jogador Desempatar(List<Jogador> candidatos, string nome, int? numero)
        {
            var alvo = NomeJogadorHelper.Normalizar(nome);
            return candidatos
                .OrderByDescending(j => NomeJogadorHelper.Normalizar(j.Nome) == alvo)
                .ThenByDescending(j => numero != null && j.NumeroCamisa == numero)
                .ThenByDescending(j => j.IdApi != null)
                .ThenBy(j => j.Id)
                .First();
        }

        private static Task<List<Jogador>> ElencoAsync(FutebolContext context, int timeId, CancellationToken ct)
            => context.Jogadores.Where(j => j.TimeId == timeId || j.SelecaoId == timeId).ToListAsync(ct);

        private static async Task<HashSet<int>> TitularesSalvosAsync(
            FutebolContext context, int jogoId, bool isTimeCasa, string usuarioId, CancellationToken ct)
        {
            var doUsuario = await context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.IsTimeCasa == isTimeCasa && e.Titular
                         && e.FaseEscalacao == "INICIAL" && e.UsuarioId == usuarioId && e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (doUsuario.Count > 0) return doUsuario.ToHashSet();

            // Sem escalação própria, o que a tela mostra é a compartilhada — é contra
            // ela que a conferência tem de comparar.
            var compartilhada = await context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.IsTimeCasa == isTimeCasa && e.Titular
                         && e.FaseEscalacao == "INICIAL" && e.UsuarioId == null && e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Distinct()
                .ToListAsync(ct);

            return compartilhada.ToHashSet();
        }

        /// <summary>
        /// Encaixa cada titular no slot da formação que corresponde à posição dele.
        ///
        /// Emparelhar por ordem não funciona: os slots ficam gravados como goleiro,
        /// zagueiros e só então os laterais, enquanto qualquer ordenação natural do
        /// XI põe o lateral-direito ao lado dos zagueiros. Isso trocava o lateral pelo
        /// zagueiro no campo. O formationPlace da ESPN também não serve — é a numeração
        /// posicional clássica (4 = volante, 5 e 6 = zagueiros), que não sobe em linha
        /// reta da defesa para o ataque.
        ///
        /// Então casa por LINHA (goleiro/defesa/meio/ataque) e, dentro da linha, por
        /// lateralidade: o X do slot é a posição real dele no campo, e o X estimado do
        /// atleta sai do texto da posição da ESPN. Quem sobra de uma linha cai nos
        /// slots que ficaram livres, para nenhum titular ficar de fora.
        /// </summary>
        public static List<(Jogador Jogador, PosicaoFormacao? Slot, EspnAtleta Atleta)> DistribuirNosSlots(
            List<(Jogador Jogador, EspnAtleta Atleta)> titulares, List<PosicaoFormacao> slots)
        {
            var resultado = new List<(Jogador, PosicaoFormacao?, EspnAtleta)>();
            var livres = slots.ToList();

            foreach (var linha in new[] { 0, 1, 2, 3 })
            {
                var daLinha = titulares
                    .Where(t => RankLinha(t.Atleta.Posicao) == linha)
                    .OrderBy(t => XEstimado(t.Atleta.Posicao))
                    .ToList();
                if (daLinha.Count == 0) continue;

                var slotsDaLinha = livres
                    .Where(s => RankLinha(s.NomePosicao) == linha)
                    .OrderBy(s => s.PosicaoX)
                    .ToList();

                for (var i = 0; i < daLinha.Count; i++)
                {
                    var slot = i < slotsDaLinha.Count ? slotsDaLinha[i] : null;
                    if (slot != null) livres.Remove(slot);
                    resultado.Add((daLinha[i].Jogador, slot, daLinha[i].Atleta));
                }
            }

            // A ESPN classificou alguém numa linha que a formação não tem (ex.: três
            // zagueiros num 4-4-2). Esses ficaram sem slot; aproveita o que sobrou.
            for (var i = 0; i < resultado.Count; i++)
            {
                if (resultado[i].Item2 != null || livres.Count == 0) continue;
                resultado[i] = (resultado[i].Item1, livres[0], resultado[i].Item3);
                livres.RemoveAt(0);
            }

            return resultado;
        }

        /// <summary>
        /// "Substituto" / "SUB" é o que a ESPN devolve no lugar da posição de quem
        /// começou no banco — é estado no jogo, não posição do jogador.
        /// </summary>
        public static bool EhSubstituto(string posicao)
        {
            var p = posicao.Trim().ToLowerInvariant();
            return p is "sub" || p.Contains("substitut");
        }

        // Onde o jogador fica no eixo horizontal do campo, na mesma escala do X dos
        // slots (0 = esquerda, 100 = direita). "Center Right Defender" é zagueiro pela
        // direita, e tem de ficar mais central que um "Right Back".
        public static double XEstimado(string posicao)
        {
            var p = posicao.ToLowerInvariant();
            // "Zagueiro esquerdo" (ESPN em pt-BR) é central, não é lateral — sem isso
            // ele empatava com "Lateral esquerdo" e os dois trocavam de lugar no campo.
            var central = p.Contains("center") || p.Contains("central")
                       || p.Contains("centro") || p.Contains("zagueiro");
            var direita = p.Contains("direit") || p.Contains("right");
            var esquerda = p.Contains("esquerd") || p.Contains("left");

            if (direita) return central ? 62 : 82;
            if (esquerda) return central ? 38 : 18;
            return 50;
        }

        // Linha do jogador no campo, do próprio gol para a frente. Pedimos o resumo em
        // pt-BR ("Zagueiro Central Direito", "Meia-atacante"), mas a ESPN às vezes
        // devolve em inglês mesmo assim — e uma posição não reconhecida jogaria todo
        // mundo na linha de ataque, embaralhando os slots na hora de aplicar.
        public static int RankLinha(string posicao)
        {
            var p = posicao.ToLowerInvariant();
            if (p.Contains("goleiro") || p.Contains("goalkeeper")) return 0;
            if (p.Contains("zagueiro") || p.Contains("lateral") || p.Contains("defensor")
                || p.Contains("defender") || p.Contains("back")) return 1;
            if (p.Contains("volante") || p.Contains("meia") || p.Contains("meio")
                || p.Contains("midfield")) return 2;
            return 3;   // atacante, ponta, centroavante, forward, striker, winger
        }

        // Direita antes de esquerda, para acompanhar o X crescente dos slots.
        public static int RankLado(string posicao)
        {
            var p = posicao.ToLowerInvariant();
            if (p.Contains("direit") || p.Contains("right")) return 0;
            if (p.Contains("esquerd") || p.Contains("left")) return 2;
            return 1;
        }
    }
}
