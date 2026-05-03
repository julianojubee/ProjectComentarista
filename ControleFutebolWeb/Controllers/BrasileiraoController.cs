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


        private static readonly Dictionary<string, string> _mapaNavionalidades = new(StringComparer.OrdinalIgnoreCase)
        {
            { "uruguay", "Uruguai" },
            { "argentina", "Argentina" },
            { "brazil", "Brasil" },
            { "brasil", "Brasil" },
            { "colombia", "Colômbia" },
            { "chile", "Chile" },
            { "paraguay", "Paraguai" },
            { "peru", "Peru" },
            { "bolivia", "Bolívia" },
            { "venezuela", "Venezuela" },
            { "ecuador", "Equador" },
            { "england", "Inglaterra" },
            { "france", "França" },
            { "germany", "Alemanha" },
            { "spain", "Espanha" },
            { "portugal", "Portugal" },
            { "italy", "Itália" },
            // adicione outros conforme necessário
        };

        private Nacionalidade ObterOuCriarNacionalidade(string nomeApi)
        {
            // Normaliza o nome vindo da API
            var nomeNormalizado = _mapaNavionalidades.TryGetValue(nomeApi.Trim(), out var nomeMapeado)
                ? nomeMapeado
                : nomeApi.Trim();

            // Busca ignorando maiúsculas/minúsculas
            var nacionalidade = _context.Nacionalidades
                .FirstOrDefault(n => n.Nome.ToLower() == nomeNormalizado.ToLower());

            if (nacionalidade == null)
            {
                nacionalidade = new Nacionalidade { Nome = nomeNormalizado };
                _context.Nacionalidades.Add(nacionalidade);
            }

            return nacionalidade;
        }

        private static readonly Dictionary<string, string> _mapaPosicoes = new(StringComparer.OrdinalIgnoreCase)
{
            // Goleiros
            { "goalkeeper", "Goleiro" },

            // Defensores
            { "defence", "Zagueiro" },
            { "center-back", "Zagueiro" },
            { "centre-back", "Zagueiro" },
            { "left-back", "Lateral Esquerdo" },
            { "right-back", "Lateral Direito" },

            // Meio-campo
            { "midfield", "Meio-campo" },
            { "central midfield", "Meio-campo" },
            { "defensive midfield", "Volante" },
            { "attacking midfield", "Meia Ofensivo" },
            { "left midfield", "Ponta Esquerda" },
            { "right midfield", "Ponta Direita" },

            // Atacantes
            { "offence", "Atacante" },
            { "centre-forward", "Centroavante" },
            { "center-forward", "Centroavante" },
            { "left winger", "Ponta Esquerda" },
            { "right winger", "Ponta Direita" },
         };

        private string NormalizarPosicao(string? posicaoApi)
        {
            if (string.IsNullOrWhiteSpace(posicaoApi)) return "Desconhecida";

            return _mapaPosicoes.TryGetValue(posicaoApi.Trim(), out var posicaoMapeada)
                ? posicaoMapeada
                : posicaoApi.Trim(); // mantém o original se não encontrar mapeamento
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
                var nacionalidade = ObterOuCriarNacionalidade(jogadorApi.Nacionalidade ?? "Desconhecida");

                if (!_context.Jogadores.Any(j => j.Nome == jogadorApi.Nome && j.TimeId == time.Id))
                {
                    _context.Jogadores.Add(new Jogador
                    {
                        Nome = jogadorApi.Nome,
                        Posicao = NormalizarPosicao(jogadorApi.Posicao),  // ← 
                        DataNascimento = jogadorApi.Nascimento ?? DateTime.MinValue,
                        Nacionalidade = nacionalidade,
                        DtInc = DateTime.UtcNow,
                        DtAlt = null,
                        Time = time
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"Jogadores do {time.Nome} importados com sucesso!";
            return RedirectToAction("Index", "Times");
        }

        public IActionResult CorrigirPosicoes()
        {
            var jogadores = _context.Jogadores.ToList();
            foreach (var jogador in jogadores)
            {
                jogador.Posicao = NormalizarPosicao(jogador.Posicao);
            }
            _context.SaveChanges();
            TempData["Mensagem"] = "Posições corrigidas com sucesso!";
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

        public async Task<IActionResult> ImportarJogos(string competitionCode, int competicaoId)
        {
            var partidas = await _apiFootballDataService.GetMatchesAsync(competitionCode);

            // Coleta todos os IDs de times que aparecem nas partidas
            var idsApiNasPartidas = partidas
                .Where(p => p.HomeTeam?.Id != null && p.AwayTeam?.Id != null)
                .SelectMany(p => new[] { p.HomeTeam.Id!.Value, p.AwayTeam.Id!.Value })
                .Distinct()
                .ToList(); ;

            // Verifica quais ainda não existem no banco
            var idsJaNoBank = _context.Times
                .Where(t => idsApiNasPartidas.Contains(t.IdApi))
                .Select(t => t.IdApi)
                .ToList();

            var idsFaltando = idsApiNasPartidas.Except(idsJaNoBank).ToList();

            // Se houver times faltando, busca e salva antes de continuar
            if (idsFaltando.Any())
            {
                var clubes = await _apiFootballDataService.GetTeamsAsync(competitionCode);

                foreach (var clube in clubes.Where(c => idsFaltando.Contains(c.Id)))
                {
                    var timeExistente = _context.Times.FirstOrDefault(t => t.IdApi == clube.Id);
                    if (timeExistente == null)
                    {
                        _context.Times.Add(new Time
                        {
                            Nome = clube.Nome,
                            EscudoUrl = clube.Escudo,
                            IdApi = clube.Id,
                            Cidade = "Desconhecida",
                            FormacaoPadraoId = 20,  // ← adicionar esta linha
                            CorPrincipal = "#000000",   // ← obrigatório se não for nullable
                            CorSecundaria = "#FFFFFF"   // ← obrigatório se não for nullable
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            // Agora importa as partidas normalmente
            foreach (var partida in partidas)
            {
                if(partida.HomeTeam?.Id == null || partida.AwayTeam?.Id == null)
                    continue;

                var jogoExistente = _context.Jogos.FirstOrDefault(j => j.PartidaApiId == partida.Id);
                if (jogoExistente == null)
                {
                    
                    var timeCasa = _context.Times.FirstOrDefault(t => t.IdApi == partida.HomeTeam.Id);
                    var timeVisitante = _context.Times.FirstOrDefault(t => t.IdApi == partida.AwayTeam.Id);

                    if (timeCasa != null && timeVisitante != null)
                    {
                        _context.Jogos.Add(new Jogo
                        {
                            Rodada = partida.Matchday.GetValueOrDefault(),  // ← 
                            PartidaApiId = partida.Id,
                            Data = partida.UtcDate,
                            TimeCasaId = timeCasa.Id,
                            PlacarCasa = partida.Score.FullTime.Home,
                            PlacarVisitante = partida.Score.FullTime.Away,
                            TimeVisitanteId = timeVisitante.Id,
                            FormacaoCasaId = 20,
                            FormacaoVisitanteId = 20,
                            CompeticaoId = competicaoId,
                            Grupo = partida.Group
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Mensagem"] = $"Jogos da competição {competitionCode} importados com sucesso!";
            return RedirectToAction("Index");
        }


    }
}