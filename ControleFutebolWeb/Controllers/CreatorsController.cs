using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Filters;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Área PÚBLICA para criadores de conteúdo (/creators) — a terceira parte do
    // site acessível sem login, junto do blog e de /analise/{token}.
    //
    // São cinco ferramentas de tela, todas de leitura:
    //   /creators/escalacao   — Match Up: escolhe dois times, o campo vem montado
    //                           com a última escalação registrada de cada um e o
    //                           creator arrasta à vontade;
    //   /creators/selecao     — os 11 da competição escolhidos à mão pelo creator;
    //   /creators/tabelas     — classificação de uma competição/temporada;
    //   /creators/jogos-hoje  — os jogos de um dia, com placar ao vivo e gols;
    //   /creators/simulador   — tabela recalculada com os placares que o visitante
    //                           imagina (que ficam no navegador dele).
    //
    // [AllowAnonymous] é obrigatório: o AuthorizeFilter global (Program.cs) exige
    // autenticação em tudo por padrão. O controller também é isento do
    // AssinaturaFilter — não há conta por trás do visitante.
    //
    // REGRA DE OURO (mesma do AnalisePublicaController): este controller NÃO TEM
    // AÇÃO DE ESCRITA e nunca pode ganhar uma. O que o creator monta na tela vive
    // só no navegador dele — nada é salvo.
    //
    // SEGUNDA REGRA: todo dado sai do acervo COMPARTILHADO (UsuarioId == null,
    // o que veio das importações). Nada que dependa do usuário — nota, critério,
    // anotação, escalação ajustada à mão — pode aparecer aqui.
    //
    // O contador de acessos (AcessoPublicoFilter) é a única coisa que esta área
    // grava, e não é dado de ninguém: só "a ferramenta X foi aberta N vezes no
    // dia Y", para o dono do site saber se alguém está usando. Ver /Admin/Acessos.
    [AllowAnonymous]
    [ServiceFilter(typeof(AcessoPublicoFilter))]
    [Route("creators")]
    public class CreatorsController : Controller
    {
        private readonly FutebolContext _context;
        private readonly TooltipJogadorService _tooltip;
        private readonly SimuladorService _simulador;

        public CreatorsController(FutebolContext context, TooltipJogadorService tooltip,
            SimuladorService simulador)
        {
            _context = context;
            _tooltip = tooltip;
            _simulador = simulador;
        }

        private string? ImagemUrl(string? url) =>
            string.IsNullOrEmpty(url) ? null : Url.Action("Imagem", "MediaProxy", new { url });

        // GET /creators
        [HttpGet("")]
        public IActionResult Index() => View();

        // ── Match Up ──────────────────────────────────────────────────────────

        // GET /creators/escalacao?competicao=82 — a tela em si só traz as listas
        // de competição e time; o campo é montado no navegador a partir de
        // /creators/escalacao/dados.
        [HttpGet("escalacao")]
        public async Task<IActionResult> Escalacao(int? competicao = null)
        {
            return View(new CreatorsEscalacaoViewModel
            {
                Competicoes = await CompeticoesComEscalacaoAsync(),
                CompeticaoId = competicao,
                Times = await TimesComEscalacaoAsync(competicao),
            });
        }

        // GET /creators/escalacao/times?competicao=82 — repovoa os dois seletores
        // de time quando o creator troca a competição, no mesmo formato que
        // /Jogos/TimesPorCompeticao já usa. Sem competição, devolve a lista toda.
        [HttpGet("escalacao/times")]
        public async Task<IActionResult> EscalacaoTimes(int? competicao = null)
        {
            var times = await TimesComEscalacaoAsync(competicao);
            return Json(times.Select(t => new { id = t.Id, nome = t.Nome }));
        }

        // GET /creators/escalacao/dados?casa=1&fora=2 — mesmo JSON que o modal
        // Pré-jogo entrega para o js/matchup.js, montado por MatchUpHelper.
        //
        // usuarioId: null de propósito — o helper passa a enxergar só as escalações
        // compartilhadas. O card do jogador não traz nota: a média do sistema é a
        // avaliação de quem analisou, dado privado que não cabe numa página pública.
        // O tooltip ℹ, sim, traz os números importados (gols, assistências, médias
        // por jogo), que são iguais para todo mundo.
        [HttpGet("escalacao/dados")]
        public async Task<IActionResult> EscalacaoDados(int casa, int fora)
        {
            if (casa <= 0 || fora <= 0 || casa == fora) return BadRequest();

            var nomes = await _context.Times.AsNoTracking()
                .Where(t => t.Id == casa || t.Id == fora)
                .ToDictionaryAsync(t => t.Id, t => t.Nome);

            if (!nomes.ContainsKey(casa) || !nomes.ContainsKey(fora)) return NotFound();

            var t1 = await MatchUpHelper.MontarTimeAsync(_context, casa, esquerda: true, usuarioId: null);
            var t2 = await MatchUpHelper.MontarTimeAsync(_context, fora, esquerda: false, usuarioId: null);

            object? Map(MatchUpTimeViewModel? t) => t == null ? null : new
            {
                nome = t.Time.Nome,
                escudo = ImagemUrl(t.Time.EscudoUrl),
                adversario = t.JogoOrigemEhCasa ? t.JogoOrigem?.TimeVisitante?.Nome : t.JogoOrigem?.TimeCasa?.Nome,
                data = t.JogoOrigem?.Data?.ToString("dd/MM/yyyy"),
                escalacao = t.Escalacao.Select(e => new
                {
                    id = e.Jogador.Id,
                    numero = e.Jogador.NumeroCamisa?.ToString() ?? "",
                    nome = e.Jogador.Nome,
                    sigla = PosicaoJogadorHelper.Sigla(e.Posicao),
                    foto = ImagemUrl(e.Jogador.FotoUrl),
                    x = Math.Round(e.PosicaoX, 2),
                    y = Math.Round(e.PosicaoY, 2),
                }),
                elenco = t.Elenco.Select(j => new
                {
                    id = j.Id,
                    numero = j.NumeroCamisa?.ToString() ?? "",
                    nome = j.Nome,
                    sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                    foto = ImagemUrl(j.FotoUrl),
                }),
            };

            // Ficha e números do ℹ. Cada time é calculado no escopo do PRÓPRIO jogo
            // de origem (os dois podem vir de competições diferentes) — como um
            // jogador só aparece de um lado, os mapas se somam sem se atropelar.
            // usuarioId vazio: aqui não há login, e só a escalação compartilhada conta.
            var tooltip = await MontarTooltipAsync(t1, t2);

            return Json(new
            {
                casa = Map(t1),
                visitante = Map(t2),
                nomeCasa = nomes[casa],
                nomeVisitante = nomes[fora],
                tooltip,
            });
        }

        // GET /creators/escalacao/buscar-jogador?q=estevao — busca no acervo todo,
        // para trazer ao elenco quem não estava na última escalação (convocado
        // novo, reforço recém-contratado). Devolve o mesmo formato do elenco, mais
        // o clube e a seleção para o creator saber quem é quem.
        //
        // Sem acento de propósito: o creator digita "estevao" e o banco tem
        // "Estêvão". A regex (~* no Postgres) troca cada letra que costuma vir
        // acentuada por uma classe com as variantes; o filtro fino, já sem
        // acento, é feito aqui depois.
        [HttpGet("escalacao/buscar-jogador")]
        public async Task<IActionResult> EscalacaoBuscarJogador(string? q)
        {
            var termo = NomeJogadorHelper.Normalizar(q ?? string.Empty);
            if (termo.Length < 3) return Json(Array.Empty<object>());

            var padrao = RegexSemAcento(termo);

            var candidatos = await _context.Jogadores.AsNoTracking()
                .Where(j => !j.Aposentado && Regex.IsMatch(j.Nome, padrao, RegexOptions.IgnoreCase))
                .OrderBy(j => j.Nome.Length)
                .Take(200)
                .Select(j => new
                {
                    j.Id, j.Nome, j.NumeroCamisa, j.Posicao, j.FotoUrl,
                    Time = j.Time.Nome,
                    Selecao = j.Selecao != null ? j.Selecao.Nome : null,
                })
                .ToListAsync();

            var achados = candidatos
                .Select(j => new { j, nome = NomeJogadorHelper.Normalizar(j.Nome) })
                .Where(x => x.nome.Contains(termo))
                // Quem começa com o termo vem antes ("Estevão" antes de "Paulo Estevão")
                .OrderBy(x => x.nome.StartsWith(termo) ? 0 : 1)
                .ThenBy(x => x.j.Nome.Length)
                .Take(15)
                .Select(x => new
                {
                    id = x.j.Id,
                    nome = x.j.Nome,
                    numero = x.j.NumeroCamisa?.ToString() ?? "",
                    sigla = PosicaoJogadorHelper.Sigla(x.j.Posicao),
                    foto = ImagemUrl(x.j.FotoUrl),
                    time = x.j.Time,
                    selecao = x.j.Selecao,
                });

            return Json(achados);
        }

        // "estevao" → "[eéêèë]st[eéêèë]v[aáâãàä][oóôõòö]". Só letras, dígitos e
        // espaço passam; o resto vira "." — nada do que o visitante digita chega
        // à regex como metacaractere.
        private static readonly Dictionary<char, string> VariantesAcento = new()
        {
            ['a'] = "aáâãàäAÁÂÃÀÄ", ['e'] = "eéêèëEÉÊÈË", ['i'] = "iíîìïIÍÎÌÏ",
            ['o'] = "oóôõòöOÓÔÕÒÖ", ['u'] = "uúûùüUÚÛÙÜ", ['c'] = "cçCÇ", ['n'] = "nñNÑ",
        };

        private static string RegexSemAcento(string termo) =>
            string.Concat(termo.Select(c =>
                VariantesAcento.TryGetValue(c, out var v) ? "[" + v + "]"
                : (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or ' ') ? c.ToString()
                : "."));

        // Junta o pacote do tooltip dos dois lados do Match Up. Sem jogo de origem
        // (time sem escalação registrada) o lado simplesmente não entra.
        private async Task<TooltipJogadorPacote> MontarTooltipAsync(params MatchUpTimeViewModel?[] times)
        {
            var junto = new TooltipJogadorPacote();

            foreach (var t in times)
            {
                if (t?.JogoOrigem == null) continue;

                var ids = t.Escalacao.Select(e => e.Jogador.Id)
                    .Concat(t.Elenco.Select(j => j.Id))
                    .Distinct()
                    .ToList();

                var pacote = await _tooltip.MontarPacoteAsync(
                    ids, t.JogoOrigem, usuarioId: string.Empty, imagemUrl: ImagemUrl,
                    incluirObservacoes: false);

                foreach (var (id, info) in pacote.Dados) junto.Dados[id] = info;
                foreach (var (id, m) in pacote.Medias) junto.Medias[id] = m;
                foreach (var (id, v) in pacote.TitularCompeticao) junto.TitularCompeticao[id] = v;
                foreach (var (id, v) in pacote.GolsTemporada) junto.GolsTemporada[id] = v;
                foreach (var (id, v) in pacote.AssistsTemporada) junto.AssistsTemporada[id] = v;
                foreach (var (id, v) in pacote.TitularTemporada) junto.TitularTemporada[id] = v;
                foreach (var (id, v) in pacote.TimeAnterior) junto.TimeAnterior[id] = v;

                // Rótulo da linha "Temporada": os dois lados costumam ser da mesma;
                // se divergirem, fica a do primeiro time que tiver uma.
                if (junto.Temporada == 0) junto.Temporada = pacote.Temporada;
            }

            return junto;
        }

        // ── Seleção da competição ─────────────────────────────────────────────

        // GET /creators/selecao?competicao=1&temporada=2025&formacao=3
        // Campo vazio na formação escolhida; o creator preenche arrastando os
        // jogadores dos times que disputaram a competição.
        [HttpGet("selecao")]
        public async Task<IActionResult> Selecao(int? competicao = null, int? temporada = null, int? formacao = null)
        {
            var vm = new CreatorsSelecaoViewModel
            {
                Competicoes = await CompeticoesComJogosAsync(),
                Formacoes = await _context.Formacoes.AsNoTracking()
                    .Include(f => f.Posicoes)
                    .OrderBy(f => f.Nome)
                    .ToListAsync(),
            };

            vm.CompeticaoId = competicao ?? vm.Competicoes.FirstOrDefault()?.Id;
            if (vm.CompeticaoId == null) return View(vm);

            vm.Temporadas = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == vm.CompeticaoId)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToListAsync();
            vm.Temporada = temporada ?? vm.Temporadas.FirstOrDefault();

            vm.Times = await TimesDaCompeticaoAsync(vm.CompeticaoId.Value, vm.Temporada);

            // Formação escolhida, senão a primeira com 11 posições cadastradas —
            // uma formação incompleta deixaria o campo com buracos sem explicação.
            vm.FormacaoId = formacao ?? vm.Formacoes
                .FirstOrDefault(f => f.Posicoes != null && f.Posicoes.Count == 11)?.Id
                ?? vm.Formacoes.FirstOrDefault()?.Id;

            return View(vm);
        }

        // GET /creators/selecao/elenco?time=5 — jogadores do time para a barra
        // lateral da seleção. Seleção nacional vincula por SelecaoId; clube, por
        // TimeId (o mesmo par que o resto do sistema usa).
        [HttpGet("selecao/elenco")]
        public async Task<IActionResult> SelecaoElenco(int time)
        {
            var t = await _context.Times.AsNoTracking().FirstOrDefaultAsync(x => x.Id == time);
            if (t == null) return NotFound();

            var jogadores = await _context.Jogadores.AsNoTracking()
                .Where(j => !j.Aposentado && (t.EhSelecao ? j.SelecaoId == time : j.TimeId == time))
                .OrderBy(j => j.Nome)
                .ToListAsync();

            return Json(new
            {
                time = t.Nome,
                escudo = ImagemUrl(t.EscudoUrl),
                jogadores = jogadores.Select(j => new
                {
                    id = j.Id,
                    nome = j.Nome,
                    numero = j.NumeroCamisa?.ToString() ?? "",
                    sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                    foto = ImagemUrl(j.FotoUrl),
                    time = t.Nome,
                }),
            });
        }

        // ── Jogos do dia ──────────────────────────────────────────────────────

        // GET /creators/jogos-hoje?data=2026-09-02 — mesmos cards de /Jogos/Hoje
        // (Shared/_JogosDoDia), sem o atalho de análise e sem a marcação de
        // "analisado", que é de quem está logado.
        [HttpGet("jogos-hoje")]
        public async Task<IActionResult> JogosHoje(DateTime? data = null)
        {
            // Jogos ficam em UTC no banco. Converte o dia escolhido (no fuso do Brasil,
            // UTC-3) para UTC para não perder os jogos das primeiras horas da manhã.
            var fusoBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var agoraBrasil = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fusoBrasil);

            var diaBrasil = (data?.Date) ?? agoraBrasil.Date;
            var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(diaBrasil, DateTimeKind.Unspecified), fusoBrasil);
            var fimUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(diaBrasil.AddDays(1), DateTimeKind.Unspecified), fusoBrasil);

            var jogos = await _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .Where(j => j.Data >= inicioUtc && j.Data < fimUtc)
                .OrderBy(j => j.Data)
                .ToListAsync();

            ViewBag.GolsPorJogo = await GolsDosJogosAsync(jogos);
            ViewBag.JogosAnalisadosIds = new HashSet<int>(); // ninguém logado: nada analisado
            ViewBag.DiaAtual = diaBrasil;
            ViewBag.DiaAnterior = diaBrasil.AddDays(-1);
            ViewBag.DiaSeguinte = diaBrasil.AddDays(1);
            ViewBag.EhHoje = diaBrasil == agoraBrasil.Date;
            return View(jogos);
        }

        // Gols do dia agrupados por jogo (mesmo GolResumoHelper da timeline da
        // análise). Duas consultas para o dia inteiro, e não uma por jogo: numa
        // rodada cheia o N+1 apareceria no tempo de resposta.
        private async Task<Dictionary<int, List<GolResumo>>> GolsDosJogosAsync(List<Jogo> jogos)
        {
            var ids = jogos.Select(j => j.Id).ToList();
            if (ids.Count == 0) return new();

            var gols = await _context.Gols.AsNoTracking()
                .Include(g => g.Jogador)
                .Where(g => ids.Contains(g.JogoId))
                .ToListAsync();

            if (gols.Count == 0) return new();

            var assistencias = await _context.Assistencias.AsNoTracking()
                .Include(a => a.Jogador)
                .Where(a => ids.Contains(a.JogoId))
                .ToListAsync();

            // Escalações só de quem marcou ou deu assistência: é delas que sai o lado do
            // gol, e carregar as escalações inteiras da rodada seriam milhares de linhas.
            var envolvidos = gols.Select(g => g.JogadorId)
                .Concat(assistencias.Select(a => a.JogadorId))
                .Distinct()
                .ToList();

            var escalacoes = await LadoJogadorHelper
                .EscalacoesDosEventos(_context, ids, envolvidos)
                .ToListAsync();

            return jogos.ToDictionary(
                j => j.Id,
                j => GolResumoHelper.Montar(j,
                        gols.Where(g => g.JogoId == j.Id),
                        assistencias.Where(a => a.JogoId == j.Id),
                        escalacoes.Where(e => e.JogoId == j.Id)));
        }

        // ── Simulador ─────────────────────────────────────────────────────────

        // GET /creators/simulador?competicao=82&temporada=2026&rodada=12
        // Mesmo simulador da tela logada, com uma diferença: os palpites do
        // visitante ficam no navegador dele e são reenviados a cada recálculo —
        // aqui nada é gravado.
        [HttpGet("simulador")]
        public async Task<IActionResult> Simulador(int? competicao = null, int? temporada = null, int? rodada = null)
        {
            var vm = new SimuladorViewModel
            {
                CompeticoesDisponiveis = await _simulador.CompeticoesElegiveisAsync()
            };

            var escolhida = competicao == null
                ? null
                : vm.CompeticoesDisponiveis.FirstOrDefault(c => c.Id == competicao);

            if (escolhida == null) return View(vm);

            await _simulador.PreencherSimulacaoAsync(
                vm, escolhida, temporada, rodada, SemPalpites);

            return View(vm);
        }

        // POST /creators/simulador/tabela — recebe todos os palpites da tela e
        // devolve a classificação recalculada. É uma leitura: o corpo do POST só
        // existe porque a lista de palpites não caberia numa querystring.
        // [IgnoreAntiforgeryToken]: o AutoValidateAntiforgeryToken global (Program.cs)
        // exige token em todo POST, e aqui não há token para pedir a um visitante sem
        // sessão. É seguro porque esta ação não muda nada — nem no banco, nem em conta
        // nenhuma: ela recebe placares imaginados e devolve uma tabela calculada. Não
        // existe ação a ser forjada. Se um dia isto passar a gravar algo, o atributo
        // sai junto (e a ação não pertence mais a este controller — ver REGRA DE OURO).
        [HttpPost("simulador/tabela")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SimuladorTabela([FromBody] SimuladorPalpitesRequest req)
        {
            var competicao = await _simulador.CompeticaoElegivelAsync(req?.Competicao ?? 0);
            if (competicao == null) return BadRequest("Competição não simulável.");

            var vm = new SimuladorViewModel();
            await _simulador.PreencherSimulacaoAsync(
                vm, competicao, req!.Temporada, rodada: null, PalpitesDoRequest(req), montarRodada: false);

            return PartialView("../Simulador/_TabelaSimulada", vm);
        }

        // GET /creators/simulador/rodada — troca o painel de jogos pelas setas.
        // Os placares digitados não vão nem voltam daqui: quem repõe os inputs é
        // o próprio JS da tela, a partir do que ele guardou.
        [HttpGet("simulador/rodada")]
        public async Task<IActionResult> SimuladorRodada(int competicao, int? temporada, int rodada)
        {
            var elegivel = await _simulador.CompeticaoElegivelAsync(competicao);
            if (elegivel == null) return BadRequest("Competição não simulável.");

            var vm = new SimuladorViewModel();
            await _simulador.PreencherSimulacaoAsync(vm, elegivel, temporada, rodada, SemPalpites);

            return PartialView("../Simulador/_JogosRodada", vm.Rodada);
        }

        private static readonly Dictionary<int, PalpiteSimulado> SemPalpites = new();

        // Placares vindos da tela, com a mesma faixa da versão logada (0–99) e
        // sem confiar em nada do corpo além disso.
        private static Dictionary<int, PalpiteSimulado> PalpitesDoRequest(SimuladorPalpitesRequest req) =>
            (req.Palpites ?? new List<SimuladorPalpiteDto>())
                .Where(p => p.JogoId > 0 && p.Casa != null && p.Visitante != null)
                .GroupBy(p => p.JogoId)
                .ToDictionary(
                    g => g.Key,
                    g => new PalpiteSimulado(
                        Math.Clamp(g.First().Casa!.Value, 0, 99),
                        Math.Clamp(g.First().Visitante!.Value, 0, 99)));

        // ── Tabelas ───────────────────────────────────────────────────────────

        // GET /creators/tabelas?competicao=1&temporada=2025
        [HttpGet("tabelas")]
        public async Task<IActionResult> Tabelas(int? competicao = null, int? temporada = null)
        {
            var vm = new CreatorsTabelaViewModel { Competicoes = await CompeticoesComJogosAsync() };

            vm.CompeticaoId = competicao ?? vm.Competicoes.FirstOrDefault()?.Id;
            if (vm.CompeticaoId == null) return View(vm);

            vm.Competicao = vm.Competicoes.FirstOrDefault(c => c.Id == vm.CompeticaoId);
            if (vm.Competicao == null) return NotFound();

            vm.Temporadas = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == vm.CompeticaoId)
                .Select(j => j.Temporada).Distinct()
                .OrderByDescending(t => t).ToListAsync();
            vm.Temporada = temporada ?? vm.Temporadas.FirstOrDefault();

            // Todos os jogos da temporada, terminados ou não: o builder precisa dos
            // que ainda não rolaram para saber quem está em cada grupo/chave.
            var jogos = await _context.Jogos.AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == vm.CompeticaoId && j.Temporada == vm.Temporada)
                .ToListAsync();

            var fases = await _context.CompeticaoFases.AsNoTracking()
                .Where(f => f.CompeticaoId == vm.CompeticaoId)
                .OrderBy(f => f.Ordem).ThenBy(f => f.Id)
                .ToListAsync();

            // Cartões dos jogos encerrados: alguns critérios de desempate da
            // competição (menos vermelhos/amarelos, fair play) dependem deles.
            var jogoIds = jogos.Where(j => j.PlacarCasa != null && j.PlacarVisitante != null)
                .Select(j => j.Id).ToHashSet();
            var cartoes = await _context.Cartoes.AsNoTracking()
                .Where(c => jogoIds.Contains(c.JogoId))
                .ToListAsync();

            // Respeita o formato: pontos corridos vira tabela, fase de grupos vira
            // uma tabela por grupo, eliminatória vira chaveamento — e competição
            // com fases declaradas monta cada fase pelo tipo dela.
            var criterios = CriteriosDesempateHelper.Parse(vm.Competicao.CriteriosDesempate);
            // O cartão pertence ao time pelo qual o jogador entrou em campo naquele jogo.
            var escalacoesCartoes = await LadoJogadorHelper.EscalacoesDosEventos(
                _context, jogoIds, cartoes.Select(c => c.JogadorId)).ToListAsync();

            vm.Painel = CompeticaoPainelBuilder.Montar(
                vm.Competicao, fases, jogos, criterios, cartoes, escalacoesCartoes);
            vm.TotalJogos = jogoIds.Count;

            return View(vm);
        }

        // ── Consultas compartilhadas ──────────────────────────────────────────

        // Só times que têm escalação titular COMPARTILHADA registrada: oferecer um
        // time sem escalação devolveria um campo vazio sem explicar o motivo.
        // Com competicaoId, só os que a têm naquela competição.
        private async Task<List<Time>> TimesComEscalacaoAsync(int? competicaoId = null)
        {
            var escalacoes = EscalacoesCompartilhadas();
            if (competicaoId != null)
                escalacoes = escalacoes.Where(e => e.Jogo.CompeticaoId == competicaoId);

            var ids = await escalacoes
                .Select(e => e.IsTimeCasa ? e.Jogo.TimeCasaId : e.Jogo.TimeVisitanteId)
                .Distinct()
                .ToListAsync();

            return await _context.Times.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .OrderBy(t => t.Nome)
                .ToListAsync();
        }

        // Competições que aparecem no filtro do Match Up: as que têm escalação
        // registrada — escolher uma sem escalação esvaziaria a lista de times.
        private async Task<List<Competicao>> CompeticoesComEscalacaoAsync()
        {
            var ids = await EscalacoesCompartilhadas()
                .Select(e => e.Jogo.CompeticaoId)
                .Distinct()
                .ToListAsync();

            return await _context.Competicoes.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        // Titulares vindos das importações (UsuarioId null) — a base de tudo que
        // esta área pública pode mostrar.
        private IQueryable<Escalacao> EscalacoesCompartilhadas() =>
            _context.Escalacoes.AsNoTracking()
                .Where(e => e.Titular && e.JogadorId != null && e.UsuarioId == null);

        private async Task<List<Competicao>> CompeticoesComJogosAsync()
        {
            var ids = await _context.Jogos.AsNoTracking()
                .Select(j => j.CompeticaoId).Distinct().ToListAsync();

            return await _context.Competicoes.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        private async Task<List<Time>> TimesDaCompeticaoAsync(int competicaoId, int temporada)
        {
            var jogos = await _context.Jogos.AsNoTracking()
                .Where(j => j.CompeticaoId == competicaoId && j.Temporada == temporada)
                .Select(j => new { j.TimeCasaId, j.TimeVisitanteId })
                .ToListAsync();

            var ids = jogos.Select(j => j.TimeCasaId)
                .Concat(jogos.Select(j => j.TimeVisitanteId))
                .Distinct().ToList();

            return await _context.Times.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .OrderBy(t => t.Nome)
                .ToListAsync();
        }
    }
}
