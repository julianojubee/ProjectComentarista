using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    // Números do tooltip ℹ do jogador: o que ele fez na competição e na temporada,
    // as médias por jogo das estatísticas importadas e o clube de onde veio.
    //
    // Saiu do JogosController quando o campo do Match Up passou a mostrar o mesmo
    // tooltip — inclusive na área pública /creators/escalacao, que não tem usuário
    // logado (aí usuarioId entra vazio e só as escalações compartilhadas contam).
    //
    // Só leitura: o serviço consulta e agrega, nunca grava.
    public class TooltipJogadorService
    {
        private readonly FutebolContext _context;

        public TooltipJogadorService(FutebolContext context)
        {
            _context = context;
        }


        // Temporadas que o seletor do tooltip oferece: todas em que os jogadores da
        // tela têm estatística importada, mais a do próprio jogo (que pode ainda não
        // ter estatística nenhuma — caso do primeiro jogo da temporada).
        /// <summary>
        /// Pacote pronto para o tooltip ℹ de um conjunto de jogadores: a ficha de
        /// cada um (nome, foto, físico, nacionalidade) mais os números calculados
        /// no escopo de <paramref name="jogoReferencia"/> — a competição e a
        /// temporada daquele jogo.
        ///
        /// É o que o campo do Match Up manda para o navegador, tanto no modal
        /// Pré-jogo quanto na página pública /creators/escalacao. Na pública
        /// <paramref name="usuarioId"/> vem vazio (só escalação compartilhada
        /// conta) e <paramref name="incluirObservacoes"/> fica false: a
        /// observação do cadastro é nota interna, não vai para visitante.
        /// </summary>
        public async Task<TooltipJogadorPacote> MontarPacoteAsync(
            IReadOnlyCollection<int> ids,
            Jogo jogoReferencia,
            string usuarioId,
            Func<string?, string?> imagemUrl,
            bool incluirObservacoes = true)
        {
            var pacote = new TooltipJogadorPacote();
            if (ids.Count == 0) return pacote;

            var numeros = await CalcularTooltipAsync(
                ids, jogoReferencia, usuarioId, jogoReferencia.Temporada, imagemUrl);

            var jogadores = await _context.Jogadores.AsNoTracking()
                .Include(j => j.Nacionalidade)
                .Include(j => j.Time)
                .Where(j => ids.Contains(j.Id))
                .ToListAsync();

            // Em jogo de seleção o tooltip mostra também o clube do jogador
            // (Jogador.Time é o clube; a seleção fica em Jogador.SelecaoId).
            var jogoDeSelecao = jogoReferencia.TimeCasa?.EhSelecao == true
                             || jogoReferencia.TimeVisitante?.EhSelecao == true;

            foreach (var j in jogadores)
            {
                var clube = jogoDeSelecao && j.Time != null && !j.Time.EhSelecao ? j.Time : null;
                pacote.Dados[j.Id] = new TooltipJogadorInfo
                {
                    Id = j.Id,
                    Nome = j.NomeExibicao,
                    Foto = j.FotoUrl ?? "",
                    Posicao = j.Posicao ?? "",
                    Sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                    Numero = j.NumeroCamisa,
                    Idade = j.Idade > 0 ? j.Idade : null,
                    Altura = j.Altura,
                    Peso = j.Peso,
                    Nac = j.Nacionalidade?.Nome ?? "",
                    // Imagem da bandeira (flagcdn) em vez do emoji: no Windows os
                    // navegadores mostram só as letras do código do país.
                    NacFlag = j.Nacionalidade != null ? (FlagHelper.GetFlagImageUrl(j.Nacionalidade.Nome) ?? "") : "",
                    Time = clube?.Nome ?? "",
                    TimeEscudo = clube != null ? (imagemUrl(clube.EscudoUrl) ?? "") : "",
                    Gols = numeros.Gols.TryGetValue(j.Id, out var g) ? g : 0,
                    Assists = numeros.Assists.TryGetValue(j.Id, out var a) ? a : 0,
                    Obs = incluirObservacoes ? (j.Observacoes ?? "") : "",
                };
            }

            pacote.Temporada = numeros.Temporada;
            pacote.Medias = numeros.Medias;
            pacote.TitularCompeticao = numeros.TitularCompeticao;
            pacote.GolsTemporada = numeros.GolsTemporada;
            pacote.AssistsTemporada = numeros.AssistsTemporada;
            pacote.TitularTemporada = numeros.TitularTemporada;
            pacote.TimeAnterior = numeros.TimeAnterior;
            return pacote;
        }

        public async Task<List<int>> TemporadasDoTooltipAsync(
            IReadOnlyCollection<int> ids, int temporadaJogo)
        {
            var temporadas = await _context.EstatisticasJogador
                .Where(e => ids.Contains(e.JogadorId) && e.Jogo.Temporada > 0)
                .Select(e => e.Jogo.Temporada)
                .Distinct()
                .ToListAsync();

            if (temporadaJogo > 0 && !temporadas.Contains(temporadaJogo))
                temporadas.Add(temporadaJogo);

            return temporadas.OrderByDescending(t => t).ToList();
        }

        // Calcula as três seções do tooltip (Competição, Temporada e médias por jogo)
        // com o mesmo recorte de temporada. temporada = rótulo (Jogo.Temporada);
        // 0 = sem recorte (carreira inteira).
        public async Task<TooltipJogadorDados> CalcularTooltipAsync(
            IReadOnlyCollection<int> ids, Jogo jogo, string usuarioId, int temporada,
            Func<string?, string?> imagemUrl)
        {
            var dados = new TooltipJogadorDados { Temporada = temporada };

            // O rótulo Temporada segue a api-football: ligas européias usam o ano de
            // INÍCIO (2025 = 2025/26) e competições de ano civil (Brasileirão, Copa do
            // Mundo) o próprio ano. Comparar o int direto deixava de fora os jogos de
            // clube do jogador (Bayern Temporada 2025 vs Copa Temporada 2026) e a linha
            // "Temporada" ficava igual à "Competição". Normaliza pelo ANO DE TÉRMINO:
            // uma (competição, temporada) "cruza o ano" quando tem jogos em ano civil
            // maior que o rótulo — nesse caso termina em Temporada+1. Dois jogos são da
            // mesma temporada quando terminam no mesmo ano.
            int? anoTermino = null;
            var compsCruzadasAnterior = new List<int>();
            var compsCruzadasAtual = new List<int>();
            var cruzaAno = false;

            if (temporada > 0)
            {
                cruzaAno = await _context.Jogos.AnyAsync(j =>
                    j.CompeticaoId == jogo.CompeticaoId && j.Temporada == temporada &&
                    j.Data != null && j.Data.Value.Year > j.Temporada);
                anoTermino = temporada + (cruzaAno ? 1 : 0);

                // Competições cujo rótulo (anoTermino-1) cruza o ano → terminam em anoTermino (entram)
                compsCruzadasAnterior = await _context.Jogos
                    .Where(j => j.Temporada == anoTermino - 1 && j.Data != null && j.Data.Value.Year > j.Temporada)
                    .Select(j => j.CompeticaoId).Distinct().ToListAsync();

                // Competições cujo rótulo anoTermino cruza o ano → terminam em anoTermino+1 (saem)
                compsCruzadasAtual = await _context.Jogos
                    .Where(j => j.Temporada == anoTermino && j.Data != null && j.Data.Value.Year > j.Temporada)
                    .Select(j => j.CompeticaoId).Distinct().ToListAsync();
            }

            // Recorte de temporada repetido nas consultas abaixo — inline porque o
            // EF não traduz chamada de função local dentro da árvore de expressão.
            // semFiltro = "Todas as temporadas".
            var semFiltro = anoTermino == null;
            var ano = anoTermino ?? 0;

            // ── Linha "Competição": só esta competição, dentro da temporada ────
            dados.Gols = await _context.Gols
                .Where(g => g.Jogo.CompeticaoId == jogo.CompeticaoId && !g.Contra
                         && ids.Contains(g.JogadorId)
                         && (semFiltro
                             || (g.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(g.Jogo.CompeticaoId))
                             || (g.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(g.Jogo.CompeticaoId))))
                .GroupBy(g => g.JogadorId)
                .Select(g => new { JogadorId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            dados.Assists = await _context.Assistencias
                .Where(a => a.Jogo.CompeticaoId == jogo.CompeticaoId
                         && ids.Contains(a.JogadorId)
                         && (semFiltro
                             || (a.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(a.Jogo.CompeticaoId))
                             || (a.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(a.Jogo.CompeticaoId))))
                .GroupBy(a => a.JogadorId)
                .Select(a => new { JogadorId = a.Key, Total = a.Count() })
                .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

            dados.TitularCompeticao = await CalcularTitularesPorJogadorAsync(
                ids, usuarioId, competicaoId: jogo.CompeticaoId,
                temporadaAnoTermino: anoTermino,
                compsCruzadasAnterior: compsCruzadasAnterior,
                compsCruzadasAtual: compsCruzadasAtual);

            // ── Linha "Temporada": todas as competições do mesmo ano ──────────
            // Temporada 0 (jogo sem temporada, ou "Todas") esconde a linha na view.
            if (anoTermino != null)
            {
                dados.GolsTemporada = await _context.Gols
                    .Where(g => !g.Contra && ids.Contains(g.JogadorId)
                             && ((g.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(g.Jogo.CompeticaoId))
                              || (g.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(g.Jogo.CompeticaoId))))
                    .GroupBy(g => g.JogadorId)
                    .Select(g => new { JogadorId = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

                dados.AssistsTemporada = await _context.Assistencias
                    .Where(a => ids.Contains(a.JogadorId)
                             && ((a.Jogo.Temporada == ano && !compsCruzadasAtual.Contains(a.Jogo.CompeticaoId))
                              || (a.Jogo.Temporada == ano - 1 && compsCruzadasAnterior.Contains(a.Jogo.CompeticaoId))))
                    .GroupBy(a => a.JogadorId)
                    .Select(a => new { JogadorId = a.Key, Total = a.Count() })
                    .ToDictionaryAsync(x => x.JogadorId, x => x.Total);

                dados.TitularTemporada = await CalcularTitularesPorJogadorAsync(
                    ids, usuarioId,
                    temporadaAnoTermino: anoTermino,
                    compsCruzadasAnterior: compsCruzadasAnterior,
                    compsCruzadasAtual: compsCruzadasAtual);
            }

            dados.Medias = await CalcularMediasPorJogadorAsync(
                ids, anoTermino, compsCruzadasAnterior, compsCruzadasAtual);

            // "Vinha do": só faz sentido com uma temporada escolhida (a anterior é
            // sempre o rótulo - 1). Em "Todas as temporadas" não há referência.
            if (temporada > 0)
                dados.TimeAnterior = await CalcularTimeAnteriorAsync(ids, usuarioId, temporada - 1, cruzaAno, imagemUrl);

            return dados;
        }

        // Clube pelo qual cada jogador atuou na temporada anterior, quando ele NÃO
        // atuou pelo clube atual naquela temporada — ou seja, chegou depois. É o que
        // o tooltip mostra como "Vinha do", para o analista reconhecer os reforços.
        //
        // Duas fontes, nesta ordem:
        //  1. Escalação dos jogos daquela temporada (o lado do jogo diz o clube) —
        //     é a mais confiável e ainda dá quantos jogos ele fez lá;
        //  2. Janela de Transferências (tabela Transferencia, alimentada por
        //     /Times → Transferências da API e pelas trocas manuais), para o reforço
        //     de liga que não tem jogo importado: aí só o clube de origem é conhecido,
        //     sem contagem de jogos.
        // Jogos de seleção ficam de fora — a comparação é entre clubes.
        public async Task<Dictionary<int, TimeAnteriorJogador>> CalcularTimeAnteriorAsync(
            IReadOnlyCollection<int> ids, string usuarioId, int temporadaAnterior, bool cruzaAno,
            Func<string?, string?> imagemUrl)
        {
            if (ids.Count == 0 || temporadaAnterior <= 0) return new();

            var timeAtual = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .Select(j => new { j.Id, j.TimeId })
                .ToDictionaryAsync(j => j.Id, j => j.TimeId);

            // Distinct por (jogador, jogo, time): o mesmo jogo tem escalação INICIAL e
            // FINAL, e ainda a do import junto com a do usuário — sem isso um jogo
            // valeria três na contagem.
            var participacoes = await _context.Escalacoes
                .Where(e => e.JogadorId != null && ids.Contains(e.JogadorId!.Value)
                         && e.Jogo.Temporada == temporadaAnterior
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null))
                .Select(e => new
                {
                    JogadorId = e.JogadorId!.Value,
                    e.JogoId,
                    TimeId = e.IsTimeCasa ? e.Jogo.TimeCasaId : e.Jogo.TimeVisitanteId,
                    EhSelecao = e.IsTimeCasa ? e.Jogo.TimeCasa.EhSelecao : e.Jogo.TimeVisitante.EhSelecao,
                })
                .Distinct()
                .ToListAsync();

            var anteriores = new Dictionary<int, (int TimeId, int Jogos)>();
            foreach (var grupo in participacoes.Where(p => !p.EhSelecao).GroupBy(p => p.JogadorId))
            {
                if (!timeAtual.TryGetValue(grupo.Key, out var atual)) continue;

                var porTime = grupo.GroupBy(p => p.TimeId)
                    .Select(g => new { TimeId = g.Key, Jogos = g.Count() })
                    .ToList();

                // Jogou pelo clube de hoje na temporada passada → não é reforço.
                if (porTime.Any(t => t.TimeId == atual)) continue;

                var principal = porTime.OrderByDescending(t => t.Jogos).FirstOrDefault();
                if (principal != null) anteriores[grupo.Key] = (principal.TimeId, principal.Jogos);
            }

            // ── Fonte 2: janela de transferências ─────────────────────────────
            // Só para quem a escalação não resolveu: sem jogo pelo clube atual e sem
            // jogo por clube nenhum na temporada passada. Vale a chegada MAIS RECENTE
            // ao clube de hoje, e só a partir do início da temporada atual — uma
            // transferência de anos atrás não diz nada sobre a temporada passada.
            var jogouNaTemporada = participacoes.Where(p => !p.EhSelecao)
                .Select(p => p.JogadorId).ToHashSet();
            var semJogos = ids.Where(id => !anteriores.ContainsKey(id) && !jogouNaTemporada.Contains(id))
                .ToList();

            if (semJogos.Count > 0)
            {
                // Início da temporada atual: julho quando a competição cruza o ano
                // civil (Europa), janeiro quando é de ano civil (Brasil, MLS…).
                var inicioTemporada = new DateTime(
                    temporadaAnterior + 1, cruzaAno ? 7 : 1, 1, 0, 0, 0, DateTimeKind.Utc);

                var chegadas = await _context.Transferencias
                    .Where(t => semJogos.Contains(t.JogadorId)
                             && t.TimeOrigemId != null && t.TimeDestinoId != null
                             && t.Data >= inicioTemporada)
                    .Select(t => new { t.JogadorId, TimeOrigemId = t.TimeOrigemId!.Value, TimeDestinoId = t.TimeDestinoId!.Value, t.Data, t.Id })
                    .ToListAsync();

                foreach (var grupo in chegadas.GroupBy(t => t.JogadorId))
                {
                    if (!timeAtual.TryGetValue(grupo.Key, out var atual)) continue;

                    var chegada = grupo
                        .Where(t => t.TimeDestinoId == atual && t.TimeOrigemId != atual)
                        .OrderByDescending(t => t.Data).ThenByDescending(t => t.Id)
                        .FirstOrDefault();

                    // Jogos = 0: a transferência não conta partidas, e o tooltip
                    // omite o "· N jogos" nesse caso.
                    if (chegada != null) anteriores[grupo.Key] = (chegada.TimeOrigemId, 0);
                }
            }

            if (anteriores.Count == 0) return new();

            var idsTimes = anteriores.Values.Select(a => a.TimeId).Distinct().ToList();
            var times = await _context.Times
                .Where(t => idsTimes.Contains(t.Id))
                .Select(t => new { t.Id, t.Nome, t.EscudoUrl })
                .ToDictionaryAsync(t => t.Id, t => t);

            // Rótulo igual ao da tela: "2024/25" quando a competição cruza o ano civil.
            var rotulo = cruzaAno
                ? $"{temporadaAnterior}/{(temporadaAnterior + 1) % 100:00}"
                : temporadaAnterior.ToString();

            return anteriores
                .Where(a => times.ContainsKey(a.Value.TimeId))
                .ToDictionary(a => a.Key, a =>
                {
                    var t = times[a.Value.TimeId];
                    return new TimeAnteriorJogador
                    {
                        Nome = t.Nome,
                        Escudo = string.IsNullOrEmpty(t.EscudoUrl)
                            ? ""
                            : imagemUrl(t.EscudoUrl) ?? "",
                        Temporada = rotulo,
                        Jogos = a.Value.Jogos,
                    };
                });
        }

        // Médias por jogo das estatísticas importadas, em lote, para os jogadores
        // exibidos na tela — mesmas fórmulas de /Jogadores/Estatisticas (inclusive
        // o filtro Minutos > 0, que exclui reservas não utilizados). Alimenta o
        // tooltip de info do jogador em /Jogos/Analisar.
        // anoTermino recorta as médias na temporada escolhida no tooltip (null =
        // carreira inteira, que era o comportamento antigo e fixo).
        public async Task<Dictionary<int, MediasPorJogo>> CalcularMediasPorJogadorAsync(
            IReadOnlyCollection<int> ids, int? anoTermino = null,
            List<int>? compsCruzadasAnterior = null, List<int>? compsCruzadasAtual = null)
        {
            if (ids.Count == 0) return new();

            var semFiltro = anoTermino == null;
            var ano = anoTermino ?? 0;
            var cruzAnt = compsCruzadasAnterior ?? new List<int>();
            var cruzAtu = compsCruzadasAtual ?? new List<int>();

            var agregados = await _context.EstatisticasJogador
                .Where(e => ids.Contains(e.JogadorId) && e.Minutos != null && e.Minutos > 0
                         && (semFiltro
                             || (e.Jogo.Temporada == ano && !cruzAtu.Contains(e.Jogo.CompeticaoId))
                             || (e.Jogo.Temporada == ano - 1 && cruzAnt.Contains(e.Jogo.CompeticaoId))))
                .GroupBy(e => e.JogadorId)
                .Select(g => new
                {
                    JogadorId = g.Key,
                    Jogos = g.Count(),
                    Passes = g.Average(e => (double)e.PassesTotal),
                    PassesChave = g.Average(e => (double)e.PassesChave),
                    Finalizacoes = g.Average(e => (double)e.FinalizacoesTotal),
                    FinalizacoesNoGolSum = g.Sum(e => e.FinalizacoesNoGol),
                    FinalizacoesSum = g.Sum(e => e.FinalizacoesTotal),
                    Dribles = g.Average(e => (double)e.DriblesTentados),
                    DriblesCertosSum = g.Sum(e => e.DriblesCertos),
                    DriblesSum = g.Sum(e => e.DriblesTentados),
                    Duelos = g.Average(e => (double)e.DuelosTotal),
                    DuelosVencidosSum = g.Sum(e => e.DuelosVencidos),
                    DuelosSum = g.Sum(e => e.DuelosTotal),
                    Desarmes = g.Average(e => (double)e.Desarmes),
                    Interceptacoes = g.Average(e => (double)e.Interceptacoes),
                    Bloqueios = g.Average(e => (double)e.Bloqueios),
                    Defesas = g.Average(e => (double)e.Defesas),
                    FaltasSofridas = g.Average(e => (double)e.FaltasSofridas),
                    FaltasCometidas = g.Average(e => (double)e.FaltasCometidas),
                })
                .ToListAsync();

            static int Pct(int certos, int total) =>
                total > 0 ? (int)Math.Round(100.0 * certos / total) : 0;

            return agregados.ToDictionary(a => a.JogadorId, a => new MediasPorJogo
            {
                Jogos = a.Jogos,
                Passes = Math.Round(a.Passes, 1),
                PassesChave = Math.Round(a.PassesChave, 1),
                Finalizacoes = Math.Round(a.Finalizacoes, 1),
                FinalizacoesPct = Pct(a.FinalizacoesNoGolSum, a.FinalizacoesSum),
                Dribles = Math.Round(a.Dribles, 1),
                DriblesPct = Pct(a.DriblesCertosSum, a.DriblesSum),
                Duelos = Math.Round(a.Duelos, 1),
                DuelosPct = Pct(a.DuelosVencidosSum, a.DuelosSum),
                Desarmes = Math.Round(a.Desarmes, 1),
                Interceptacoes = Math.Round(a.Interceptacoes, 1),
                Bloqueios = Math.Round(a.Bloqueios, 1),
                Defesas = Math.Round(a.Defesas, 1),
                FaltasSofridas = Math.Round(a.FaltasSofridas, 1),
                FaltasCometidas = Math.Round(a.FaltasCometidas, 1),
            });
        }

        // Total de jogos como titular por jogador — mesmo critério de dedupe usado
        // em /Jogadores/Estatisticas: por jogo, prefere a escalação do próprio
        // usuário sobre a compartilhada (importada, UsuarioId null), e conta só a
        // fase INICIAL (a FINAL é a mesma partida, não um jogo a mais).
        // competicaoId/temporada limitam o escopo (linhas Competição/Temporada do
        // tooltip); sem filtro, conta a carreira toda.
        public async Task<Dictionary<int, int>> CalcularTitularesPorJogadorAsync(
            IReadOnlyCollection<int> ids, string usuarioId, int? competicaoId = null,
            int? temporadaAnoTermino = null,
            List<int>? compsCruzadasAnterior = null, List<int>? compsCruzadasAtual = null)
        {
            if (ids.Count == 0) return new();

            var query = _context.Escalacoes
                .Where(e => e.JogadorId != null && ids.Contains(e.JogadorId!.Value)
                         && e.Titular && e.Posicao != null && e.Posicao != "RES"
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null));
            if (competicaoId != null) query = query.Where(e => e.Jogo.CompeticaoId == competicaoId);
            if (temporadaAnoTermino != null)
            {
                // Mesma normalização por ano de término usada em PreencherDadosTooltipAsync.
                var cruzAnt = compsCruzadasAnterior ?? new List<int>();
                var cruzAtu = compsCruzadasAtual ?? new List<int>();
                query = query.Where(e =>
                    (e.Jogo.Temporada == temporadaAnoTermino && !cruzAtu.Contains(e.Jogo.CompeticaoId)) ||
                    (e.Jogo.Temporada == temporadaAnoTermino - 1 && cruzAnt.Contains(e.Jogo.CompeticaoId)));
            }

            var candidatas = await query
                .Select(e => new { e.JogadorId, e.JogoId, e.UsuarioId })
                .ToListAsync();

            return candidatas
                .GroupBy(e => e.JogadorId!.Value)
                .ToDictionary(g => g.Key, g => g
                    .GroupBy(e => e.JogoId)
                    .Select(gj => gj.OrderBy(e => e.UsuarioId == usuarioId ? 0 : 1).First())
                    .Count());
        }
    }
}
