using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ControleFutebolWeb.Controllers
{
    public class BrasileiraoController : Controller
    {
        private readonly ApiFootballDataService _apiFootballDataService;
        private readonly FutebolContext _context;

        public BrasileiraoController(ApiFootballDataService apiFootballDataService, FutebolContext context)
        {
            _apiFootballDataService = apiFootballDataService;
            _context = context;
        }

        public async Task<IActionResult> ImportarTodosJogadores(string competitionCode = "BSA")
        {
            // Busca todos os times já salvos no banco
            var times = _context.Times.ToList();

            if (times == null || times.Count == 0)
            {
                TempData["MensagemErro"] = "Nenhum time encontrado no banco. Importe os times primeiro.";
                return RedirectToAction("Times");
            }

            foreach (var time in times)
            {
                // Busca detalhes do time na API (inclui elenco)
                var detalhe = await _apiFootballDataService.GetTeamDetailAsync(time.IdApi);

                if (detalhe?.Elenco == null) continue;

                foreach (var jogadorApi in detalhe.Elenco)
                {
                    // Verifica se a nacionalidade já existe
                    var nacionalidade = _context.Nacionalidades
                        .FirstOrDefault(n => n.Nome == jogadorApi.Nacionalidade);

                    if (nacionalidade == null)
                    {
                        nacionalidade = new Nacionalidade { Nome = jogadorApi.Nacionalidade };
                        _context.Nacionalidades.Add(nacionalidade);
                    }

                    // Evita duplicar jogadores
                    if (!_context.Jogadores.Any(j => j.Nome == jogadorApi.Nome && j.TimeId == time.Id))
                    {
                        _context.Jogadores.Add(new Jogador
                        {
                            Nome = jogadorApi.Nome,
                            Posicao = jogadorApi.Posicao,
                            DataNascimento = jogadorApi.Nascimento ?? DateTime.MinValue,
                            Nacionalidade = nacionalidade,
                            Time = time,

                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Jogadores de todos os times importados com sucesso!";
            return RedirectToAction("Index", "Times");
        }

        public async Task<IActionResult> ImportarJogadores(int teamId)
        {
            // Busca o time no banco pelo IdApi
            var time = _context.Times.FirstOrDefault(t => t.IdApi == teamId);
            if (time == null)
            {
                TempData["MensagemErro"] = "Time não encontrado no banco. Importe os times primeiro.";
                return RedirectToAction("Times");
            }

            // Busca detalhes do time na API
            var detalhe = await _apiFootballDataService.GetTeamDetailAsync(teamId);
            if (detalhe?.Elenco == null)
            {
                TempData["MensagemErro"] = "Não foi possível obter o elenco do time.";
                return RedirectToAction("Times");
            }

            foreach (var jogadorApi in detalhe.Elenco)
            {
                var nacionalidade = _context.Nacionalidades
                    .FirstOrDefault(n => n.Nome == jogadorApi.Nacionalidade);

                if (nacionalidade == null)
                {
                    nacionalidade = new Nacionalidade { Nome = jogadorApi.Nacionalidade };
                    _context.Nacionalidades.Add(nacionalidade);
                }

                if (!_context.Jogadores.Any(j => j.Nome == jogadorApi.Nome && j.TimeId == time.Id))
                {
                    _context.Jogadores.Add(new Jogador
                    {
                        Nome = jogadorApi.Nome,
                        Posicao = jogadorApi.Posicao,
                        DataNascimento = jogadorApi.Nascimento ?? DateTime.MinValue,
                        Nacionalidade = nacionalidade,
                        Time = time
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"Jogadores do {time.Nome} importados com sucesso!";
            return RedirectToAction("Index", "Times");
        }

        // Listar partidas
        public async Task<IActionResult> Index(string competitionCode = "BSA")
        {
            var partidas = await _apiFootballDataService.GetMatchesAsync(competitionCode);

            if (partidas == null || partidas.Count == 0)
            {
                TempData["MensagemErro"] = "Nenhuma partida encontrada para esta competição.";
                return View(new List<Partida>());
            }

            ViewBag.CampeonatoId = competitionCode;
            return View(partidas);
        }

        //Listar times
        public async Task<IActionResult> Times(string competitionCode = "BSA")
        {
            var clubes = await _apiFootballDataService.GetTeamsAsync(competitionCode);

            if (clubes == null || clubes.Count == 0)
            {
                TempData["MensagemErro"] = "Nenhum time encontrado para esta competição.";
                return View(new List<ClubeInfo>());
            }

            return View(clubes);

        }
        public IActionResult Times()
        {
            var times = _context.Times.ToList();

            if (times == null || times.Count == 0)
            {
                TempData["MensagemErro"] = "Nenhum time encontrado no banco. Importe os times primeiro.";
                return View(new List<Time>());
            }

            return View(times);
        }


        public async Task<IActionResult> SalvarTimes(string competitionCode = "BSA")
        {
            var clubes = await _apiFootballDataService.GetTeamsAsync(competitionCode);

            foreach (var clube in clubes)
            {
                // Verifica se já existe no banco
                var timeExistente = _context.Times.FirstOrDefault(t => t.Nome == clube.Nome);
                if (timeExistente == null)
                {
                    var time = new Time
                    {
                        Nome = clube.Nome,
                        EscudoUrl = clube.Escudo,
                        IdApi = clube.Id, // salva o id da API
                        Cidade = "Desconhecida" // Football-Data não retorna cidade, você pode preencher manualmente ou deixar default
                    };

                    _context.Times.Add(time);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Times salvos no banco com sucesso!";
            return RedirectToAction("Times");
        }

        public IActionResult Detalhes(int id)
        {
            var time = _context.Times
                .Include(t => t.Jogadores)
                .ThenInclude(j => j.Nacionalidade)
                .FirstOrDefault(t => t.Id == id);

            if (time == null)
            {
                TempData["MensagemErro"] = "Time não encontrado no banco.";
                return RedirectToAction("Times");
            }

            var jogosDoTime = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.TimeCasaId == time.Id || j.TimeVisitanteId == time.Id)
                .ToList();

            var viewModel = new TimeDetalhesViewModel
            {
                Time = time,
                Jogos = jogosDoTime
            };

            return View(viewModel);
        }

        //Importar Jogos Do Campeonato Brasileiro

        public async Task<IActionResult> ImportarJogos(string competitionCode = "BSA")
        {
            var partidas = await _apiFootballDataService.GetMatchesAsync(competitionCode);

            foreach (var partida in partidas)
            {
                // Verifica se já existe no banco (evita duplicar)
                var jogoExistente = _context.Jogos.FirstOrDefault(j => j.PartidaApiId == partida.Id);
                if (jogoExistente == null)
                {
                    // Busca os times no banco pelo IdApi
                    var timeCasa = _context.Times.FirstOrDefault(t => t.IdApi == partida.HomeTeam.Id);
                    var timeVisitante = _context.Times.FirstOrDefault(t => t.IdApi == partida.AwayTeam.Id);

                    if (timeCasa != null && timeVisitante != null)
                    {
                        var jogo = new Jogo
                        {
                            Rodada = partida.Matchday, // salva a rodada
                            PartidaApiId = partida.Id,
                            Data = partida.UtcDate,
                            TimeCasaId = timeCasa.Id,
                            PlacarCasa = partida.Score.FullTime.Home,
                            PlacarVisitante = partida.Score.FullTime.Away,
                            TimeVisitanteId = timeVisitante.Id,
                            FormacaoCasaId = 20, // placeholder
                            FormacaoVisitanteId = 20, // placeholder
                            CompeticaoId = 1,
                            Grupo = null


                        };

                        _context.Jogos.Add(jogo);
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = "Jogos importados com sucesso!";
            return RedirectToAction("Index");
        }

    }
}