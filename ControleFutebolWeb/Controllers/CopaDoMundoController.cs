using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ControleFutebolWeb.Controllers
{
    public class CopaDoMundoController : Controller
    {
        private const int CopaCompeticaoId = 7;

        private readonly FutebolContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CopaDoMundoController(FutebolContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index(int? temporada = null, int? selFormacaoId = null,
            int? selId = null, bool nova = false)
        {
            var gruposPermitidos = new[] { "A","B","C","D","E","F","G","H","I","J","K","L" };

            var competicao = _context.Competicoes.FirstOrDefault(c => c.Id == 7);
            if (competicao == null)
            {
                return View(new CopaDoMundoIndexViewModel
                {
                    Chaveamento = new ChaveamentoCopaViewModel()
                });
            }

            // Temporadas disponíveis; padrão = a mais recente
            var (temporadasDisponiveis, temporadaSel) =
                TemporadaHelper.Resolver(_context, competicao.Id, temporada);
            var vm = new CopaDoMundoIndexViewModel
            {
                Temporada = temporadaSel,
                TemporadasDisponiveis = temporadasDisponiveis
            };

            static string Normalize(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var s = raw.Trim().ToUpperInvariant();
                var tokens = s.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
                var last = tokens[tokens.Length - 1];
                return last.Length == 1 && char.IsLetter(last[0]) ? last : s;
            }

            var jogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicao.Id && !string.IsNullOrEmpty(j.Grupo)
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .OrderBy(j => j.Data)
                .ToList();

            jogos = jogos.Where(j => gruposPermitidos.Contains(Normalize(j.Grupo))).ToList();

            var jogosRealizados = jogos.Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue).ToList();

            // Índice disciplinar (Fair Play) por time: carrega cartões + escalações de uma vez
            var fairPlayPorTime = CalcularFairPlay(jogosRealizados);

            var todosGrupos = jogos.Select(j => Normalize(j.Grupo)).Distinct().OrderBy(g => g).ToList();

            var grupos = new List<GrupoViewModel>();

            foreach (var nomeGrupo in todosGrupos)
            {
                var jogosDoGrupo = jogosRealizados.Where(j => Normalize(j.Grupo) == nomeGrupo).ToList();
                var classificacao = CalcularClassificacaoGrupo(jogosDoGrupo, nomeGrupo, fairPlayPorTime);
                grupos.Add(new GrupoViewModel { Nome = nomeGrupo, Times = classificacao });
            }

            // Tabela de terceiros colocados (critérios FIFA para 8 melhores de 12 grupos)
            var terceiros = grupos
                .Where(g => g.Times.Count >= 3)
                .Select(g => { var t = g.Times[2]; t.Grupo = g.Nome; return t; })
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.SaldoGols)
                .ThenByDescending(t => t.GolsPro)
                .ThenBy(t => t.FairPlay)
                .ToList();

            for (int i = 0; i < terceiros.Count; i++)
                terceiros[i].Posicao = i + 1;

            var proximosJogos = jogos
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            if (!proximosJogos.Any())
                proximosJogos = jogos.OrderByDescending(j => j.Data).Take(10).ToList();

            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (jogos.Any() ? jogos.Max(j => j.Rodada) : 0);

            // ── Chaveamento (mata-mata) ────────────────────────────────────────
            // Grupo "completo" = todos os 6 jogos do round-robin de 4 seleções realizados.
            var gruposCompletos = jogosRealizados
                .GroupBy(j => Normalize(j.Grupo))
                .ToDictionary(g => g.Key, g => g.Count() >= 6);

            // Jogos de mata-mata já importados (grupo = "Round of 16", "Quarter-finals", etc.).
            var jogosMataMata = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicao.Id && !string.IsNullOrEmpty(j.Grupo)
                         && (temporadaSel == null || j.Temporada == temporadaSel))
                .ToList()
                .Where(j => ChaveamentoCopaBuilder.NormalizarFase(j.Grupo) != null)
                .ToList();

            var chaveamento = ChaveamentoCopaBuilder.Construir(
                grupos, terceiros, gruposCompletos, jogosMataMata);

            vm.Grupos = grupos;
            vm.ProximosJogos = proximosJogos;
            vm.RodadaAtual = rodadaAtual;
            vm.TerceirosColocados = terceiros;
            vm.Chaveamento = chaveamento;

            PreencherAbaSelecao(vm, competicao.Id, temporadaSel, selFormacaoId, selId, nova);

            return View(vm);
        }

        // ── Aba "Seleção": campo + formação + melhores por posição ───────────
        private void PreencherAbaSelecao(
            CopaDoMundoIndexViewModel vm, int competicaoId, int? temporada,
            int? selFormacaoId, int? selId, bool nova)
        {
            var formacoes = _context.Formacoes
                .Include(f => f.Posicoes)
                .OrderBy(f => f.Nome)
                .ToList();
            vm.Formacoes = formacoes;
            if (!formacoes.Any()) return;

            var uid = _userManager.GetUserId(User);
            var selecoes = uid == null ? new List<SelecaoCopaUsuario>() : _context.SelecoesCopaUsuario
                .Where(s => s.UsuarioId == uid && s.CompeticaoId == competicaoId && s.Temporada == temporada)
                .OrderBy(s => s.Id)
                .ToList();
            vm.Selecoes = selecoes;

            // Seleção atual: por selId, senão a primeira; "nova" abre o editor em branco.
            SelecaoCopaUsuario? atual = null;
            if (!nova)
                atual = (selId.HasValue ? selecoes.FirstOrDefault(s => s.Id == selId.Value) : null)
                        ?? selecoes.FirstOrDefault();
            vm.ModoNovaSelecao = nova || atual == null;
            vm.SelecaoAtualId = atual?.Id;
            vm.SelecaoAtualNome = atual?.Nome;

            var formacaoId = selFormacaoId ?? atual?.FormacaoId ?? formacoes.First().Id;
            var formacao = formacoes.FirstOrDefault(f => f.Id == formacaoId) ?? formacoes.First();
            vm.SelecaoFormacaoId = formacao.Id;

            // Slots salvos só se aplicam quando a formação escolhida é a mesma da salva.
            var slotsSalvos = new List<SelecaoSlotSalvo>();
            if (atual?.SlotsJson != null && atual.FormacaoId == formacao.Id)
                slotsSalvos = JsonSerializer.Deserialize<List<SelecaoSlotSalvo>>(atual.SlotsJson) ?? new();

            foreach (var p in (formacao.Posicoes ?? new List<PosicaoFormacao>())
                         .OrderByDescending(p => p.PosicaoY).ThenBy(p => p.PosicaoX))
            {
                var salvo = slotsSalvos.FirstOrDefault(s =>
                    Math.Abs(s.X - p.PosicaoX) < 0.5 && Math.Abs(s.Y - p.PosicaoY) < 0.5);
                vm.SelecaoSlots.Add(new SelecaoSlotVM
                {
                    X = p.PosicaoX,
                    Y = p.PosicaoY,
                    JogadorId = salvo?.JogadorId
                });
            }

            // Pool: jogadores que atuaram na Copa (escalações de jogos da competição).
            var poolIds = (from e in _context.Escalacoes
                           join j in _context.Jogos on e.JogoId equals j.Id
                           where e.JogadorId != null && j.CompeticaoId == competicaoId
                                 && (temporada == null || j.Temporada == temporada)
                           select e.JogadorId!.Value).Distinct().ToList();

            vm.PoolJogadores = _context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Selecao)
                .Where(j => poolIds.Contains(j.Id))
                .OrderBy(j => j.Nome)
                .ToList();

            vm.JogadoresPorId = vm.PoolJogadores.ToDictionary(j => j.Id);

            // Jogadores já escalados que (por algum motivo) não estejam no pool atual.
            var faltantes = vm.SelecaoSlots
                .Where(s => s.JogadorId.HasValue && !vm.JogadoresPorId.ContainsKey(s.JogadorId.Value))
                .Select(s => s.JogadorId!.Value).Distinct().ToList();
            if (faltantes.Any())
                foreach (var j in _context.Jogadores
                             .Include(j => j.Time).Include(j => j.Selecao)
                             .Where(j => faltantes.Contains(j.Id)).ToList())
                    vm.JogadoresPorId[j.Id] = j;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarSelecao(
            int? selId, int? temporada, string? nome, int formacaoId, List<SelecaoSlotInput> slots)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Challenge();

            var atribuidos = (slots ?? new())
                .Where(s => s.JogadorId.HasValue)
                .Select(s => new SelecaoSlotSalvo { X = s.X, Y = s.Y, JogadorId = s.JogadorId!.Value })
                .ToList();
            var json = JsonSerializer.Serialize(atribuidos);
            var nomeFinal = string.IsNullOrWhiteSpace(nome) ? "Seleção" : nome.Trim();

            var sel = selId.HasValue
                ? await _context.SelecoesCopaUsuario
                    .FirstOrDefaultAsync(s => s.Id == selId.Value && s.UsuarioId == uid)
                : null;

            if (sel == null)
            {
                sel = new SelecaoCopaUsuario
                {
                    UsuarioId = uid,
                    CompeticaoId = CopaCompeticaoId,
                    Temporada = temporada,
                    Nome = nomeFinal,
                    FormacaoId = formacaoId,
                    SlotsJson = json
                };
                _context.SelecoesCopaUsuario.Add(sel);
            }
            else
            {
                sel.Nome = nomeFinal;
                sel.FormacaoId = formacaoId;
                sel.SlotsJson = json;
            }

            await _context.SaveChangesAsync();
            TempData["SelecaoSalva"] = "Seleção salva com sucesso!";
            return RedirectToAction("Index", new { temporada, selId = sel.Id, aba = "selecao" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirSelecao(int selId, int? temporada)
        {
            var uid = _userManager.GetUserId(User);
            if (uid == null) return Challenge();

            var sel = await _context.SelecoesCopaUsuario
                .FirstOrDefaultAsync(s => s.Id == selId && s.UsuarioId == uid);
            if (sel != null)
            {
                _context.SelecoesCopaUsuario.Remove(sel);
                await _context.SaveChangesAsync();
                TempData["SelecaoSalva"] = "Seleção excluída.";
            }
            return RedirectToAction("Index", new { temporada, aba = "selecao" });
        }

        // Calcula o índice disciplinar (Fair Play) por TimeId.
        // Limitação do modelo: Cartao só guarda "Amarelo"/"Vermelho" → amarelo=1pt, vermelho=3pt.
        private Dictionary<int, int> CalcularFairPlay(List<Jogo> jogos)
        {
            var resultado = new Dictionary<int, int>();
            if (!jogos.Any()) return resultado;

            var jogoIds = jogos.Select(j => j.Id).ToHashSet();

            var cartoes = _context.Cartoes
                .AsNoTracking()
                .Where(c => jogoIds.Contains(c.JogoId))
                .Select(c => new { c.JogoId, c.JogadorId, c.Tipo })
                .ToList();

            if (!cartoes.Any()) return resultado;

            // Mapa (JogoId, JogadorId) → IsTimeCasa, para atribuir o cartão ao time correto
            var escalacoes = _context.Escalacoes
                .AsNoTracking()
                .Where(e => jogoIds.Contains(e.JogoId) && e.JogadorId != null)
                .Select(e => new { e.JogoId, e.JogadorId, e.IsTimeCasa })
                .ToList();

            var ladoPorJogador = escalacoes
                .GroupBy(e => (e.JogoId, e.JogadorId))
                .ToDictionary(g => g.Key, g => g.First().IsTimeCasa);

            var jogoPorId = jogos.ToDictionary(j => j.Id);

            foreach (var c in cartoes)
            {
                if (!jogoPorId.TryGetValue(c.JogoId, out var jogo)) continue;
                if (!ladoPorJogador.TryGetValue((c.JogoId, (int?)c.JogadorId), out var isCasa)) continue;

                int timeId = isCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;
                int pontos = string.Equals(c.Tipo, "Vermelho", StringComparison.OrdinalIgnoreCase) ? 3 : 1;

                resultado[timeId] = resultado.GetValueOrDefault(timeId) + pontos;
            }

            return resultado;
        }

        private List<Classificacao> CalcularClassificacaoGrupo(List<Jogo> jogos, string nomeGrupo, Dictionary<int, int> fairPlayPorTime)
        {
            var tabela = new Dictionary<int, Classificacao>();

            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                if (!jogo.PlacarCasa.HasValue || !jogo.PlacarVisitante.HasValue) continue;

                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa, Grupo = nomeGrupo };
                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante, Grupo = nomeGrupo };

                var casa = tabela[jogo.TimeCasaId];
                var vis  = tabela[jogo.TimeVisitanteId];

                casa.Jogos++; vis.Jogos++;
                casa.GolsPro     += jogo.PlacarCasa.Value;
                casa.GolsContra  += jogo.PlacarVisitante.Value;
                vis.GolsPro      += jogo.PlacarVisitante.Value;
                vis.GolsContra   += jogo.PlacarCasa.Value;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                    { casa.Vitorias++; casa.Pontos += 3; vis.Derrotas++; }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                    { vis.Vitorias++; vis.Pontos += 3; casa.Derrotas++; }
                else
                    { casa.Empates++; vis.Empates++; casa.Pontos++; vis.Pontos++; }
            }

            // Atribui o índice disciplinar a cada time
            foreach (var c in tabela.Values)
                c.FairPlay = fairPlayPorTime.GetValueOrDefault(c.TimeId);

            var lista = tabela.Values.ToList();

            // Ordenação com critérios de desempate FIFA
            lista = OrdenarComDesempateFifa(lista, jogos);

            for (int i = 0; i < lista.Count; i++)
                lista[i].Posicao = i + 1;

            return lista;
        }

        private static List<Classificacao> OrdenarComDesempateFifa(List<Classificacao> lista, List<Jogo> jogosGrupo)
        {
            // Pré-ordena por pontos, saldo, gols pró
            lista = lista
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.SaldoGols)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            // Resolve empates ponto-a-ponto aplicando confronto direto e fair play
            var resultado = new List<Classificacao>();
            int i = 0;
            while (i < lista.Count)
            {
                // Encontra o bloco de times com os mesmos pts/saldo/golspro
                int j = i + 1;
                while (j < lista.Count
                    && lista[j].Pontos == lista[i].Pontos
                    && lista[j].SaldoGols == lista[i].SaldoGols
                    && lista[j].GolsPro == lista[i].GolsPro)
                    j++;

                var bloco = lista.Skip(i).Take(j - i).ToList();

                if (bloco.Count > 1)
                    bloco = DesempatarPorConfrontoDireto(bloco, jogosGrupo);

                resultado.AddRange(bloco);
                i = j;
            }

            return resultado;
        }

        private static List<Classificacao> DesempatarPorConfrontoDireto(List<Classificacao> bloco, List<Jogo> jogosGrupo)
        {
            var ids = bloco.Select(t => t.TimeId).ToHashSet();
            var jogosDiretos = jogosGrupo
                .Where(j => ids.Contains(j.TimeCasaId) && ids.Contains(j.TimeVisitanteId)
                         && j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            if (!jogosDiretos.Any())
                return bloco.OrderBy(t => t.FairPlay).ToList();

            var cd = bloco.ToDictionary(t => t.TimeId, t => new { Pts = 0, Saldo = 0, Gols = 0 });
            foreach (var j in jogosDiretos)
            {
                var casa = cd[j.TimeCasaId];
                var vis  = cd[j.TimeVisitanteId];
                int golsCasa = j.PlacarCasa!.Value, golsVis = j.PlacarVisitante!.Value;

                if (golsCasa > golsVis)
                    cd[j.TimeCasaId]      = new { Pts = casa.Pts + 3, Saldo = casa.Saldo + (golsCasa - golsVis), Gols = casa.Gols + golsCasa };
                else if (golsVis > golsCasa)
                    cd[j.TimeVisitanteId] = new { Pts = vis.Pts  + 3, Saldo = vis.Saldo  + (golsVis - golsCasa),  Gols = vis.Gols  + golsVis };
                else
                {
                    cd[j.TimeCasaId]      = new { Pts = casa.Pts + 1, Saldo = casa.Saldo + 0, Gols = casa.Gols + golsCasa };
                    cd[j.TimeVisitanteId] = new { Pts = vis.Pts  + 1, Saldo = vis.Saldo  + 0, Gols = vis.Gols  + golsVis };
                }
            }

            return bloco
                .OrderByDescending(t => cd[t.TimeId].Pts)
                .ThenByDescending(t => cd[t.TimeId].Saldo)
                .ThenByDescending(t => cd[t.TimeId].Gols)
                .ThenBy(t => t.FairPlay)
                .ToList();
        }
    }
}