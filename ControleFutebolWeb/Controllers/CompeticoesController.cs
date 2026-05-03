using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControleFutebolWeb.Controllers
{
    public class CompeticoesController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<CompeticoesController> _logger;

        public CompeticoesController(FutebolContext context, ILogger<CompeticoesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var competicoes = _context.Competicoes.ToList();
            return View(competicoes);
        }

        public IActionResult Detalhes(int id)
        {
            var competicao = _context.Competicoes
                .Include(c => c.Jogos)
                .FirstOrDefault(c => c.Id == id);

            if (competicao == null) return NotFound();

            var vm = new CompeticaoDetalhesViewModel
            {
                Competicao = competicao,
                Tipo = competicao.Tipo,
                Classificacao = CalcularTabela(competicao.Jogos),
                Grupos = competicao.Tipo == "GRUPOS" ? MontarGrupos(competicao.Jogos) : null
            };

            return View(vm);
        }

        // GET: Competicoes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Competicoes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Regiao,Tipo")] Competicao competicao)
        {
            _logger.LogInformation("POST Create chamado: Nome={Nome}, Regiao={Regiao}, Tipo={Tipo}",
                competicao.Nome, competicao.Regiao, competicao.Tipo);

            if (!ModelState.IsValid)
            {
                foreach (var erro in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Erro de validação: {Erro}", erro.ErrorMessage);
                }
                return View(competicao);
            }

            _context.Add(competicao);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Competição salva com sucesso no banco: Id={Id}", competicao.Id);

            return RedirectToAction(nameof(Index));
        }
        // Ação Index e Detalhes...

        private List<Classificacao> CalcularTabela(ICollection<Jogo> jogos)
        {
            var tabela = new Dictionary<int, Classificacao>();

            foreach (var jogo in jogos)
            {
                // Garantir que os times existam na tabela
                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa };

                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante };
                var casa = tabela[jogo.TimeCasaId];
                var visitante = tabela[jogo.TimeVisitanteId];

                casa.Jogos++;
                visitante.Jogos++;

                casa.GolsPro += (int)jogo.PlacarCasa;
                casa.GolsContra += (int)jogo.PlacarVisitante;
                visitante.GolsPro += (int)jogo.PlacarVisitante;
                visitante.GolsContra += (int)jogo.PlacarCasa;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                {
                    casa.Vitorias++;
                    casa.Pontos += 3;
                    visitante.Derrotas++;
                }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                {
                    visitante.Vitorias++;
                    visitante.Pontos += 3;
                    casa.Derrotas++;
                }
                else
                {
                    casa.Empates++;
                    visitante.Empates++;
                    casa.Pontos++;
                    visitante.Pontos++;
                }
            }

            foreach (var item in tabela.Values)
            {
                item.Saldo = item.GolsPro - item.GolsContra;
            }

            var lista = tabela.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.Saldo)
                .ThenByDescending(t => t.GolsPro)
                .ToList();

            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].Posicao = i + 1;
            }

            return lista;
        }

        private List<GrupoViewModel> MontarGrupos(ICollection<Jogo> jogos)
        {
            var grupos = new List<GrupoViewModel>();

            // supondo que cada jogo tenha uma propriedade "Grupo" (string)
            var nomesGrupos = jogos
                .Where(j => !string.IsNullOrEmpty(j.Grupo)) // só pega jogos com grupo definido
                .Select(j => j.Grupo)
                .Distinct()
                .ToList();

            foreach (var nome in nomesGrupos)
            {
                var jogosDoGrupo = jogos.Where(j => j.Grupo == nome).ToList();
                var classificacao = CalcularTabela(jogosDoGrupo);

                grupos.Add(new GrupoViewModel
                {
                    Nome = nome,
                    Times = classificacao
                });
            }

            return grupos;
        }
    }
}
