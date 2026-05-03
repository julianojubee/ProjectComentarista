using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class LibertadoresController : Controller
    {
        private readonly FutebolContext _context;

        public LibertadoresController(FutebolContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Busca jogos da Libertadores (CompeticaoId = 2) que tenham grupo definido
            var jogos = _context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == 2 && !string.IsNullOrEmpty(j.Grupo))
                .OrderBy(j => j.Data)
                .ToList();

            // Jogos já realizados: tem placar
            var jogosRealizados = jogos
                .Where(j => j.PlacarCasa.HasValue && j.PlacarVisitante.HasValue)
                .ToList();

            // Monta grupos baseado nos jogos realizados (para classificação)
            var nomesGrupos = jogosRealizados
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            // Inclui também grupos de jogos agendados (sem placar) para exibir todos
            var todosGrupos = jogos
                .Select(j => j.Grupo!)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            var grupos = new List<GrupoViewModel>();

            foreach (var nomeGrupo in todosGrupos)
            {
                var jogosDoGrupo = jogosRealizados
                    .Where(j => j.Grupo == nomeGrupo)
                    .ToList();

                var classificacao = CalcularClassificacaoGrupo(jogosDoGrupo);

                grupos.Add(new GrupoViewModel
                {
                    Nome = nomeGrupo,
                    Times = classificacao
                });
            }

            // Próximos jogos: sem placar, ordenados por data
            var proximosJogos = jogos
                .Where(j => !j.PlacarCasa.HasValue || !j.PlacarVisitante.HasValue)
                .OrderBy(j => j.Data)
                .Take(20)
                .ToList();

            // Se não houver futuros, pega os mais recentes realizados
            if (!proximosJogos.Any())
            {
                proximosJogos = jogos
                    .OrderByDescending(j => j.Data)
                    .Take(10)
                    .ToList();
            }

            // Rodada atual
            var rodadaAtual = proximosJogos.Any()
                ? proximosJogos.Min(j => j.Rodada)
                : (jogos.Any() ? jogos.Max(j => j.Rodada) : 0);

            ViewBag.Grupos = grupos;
            ViewBag.ProximosJogos = proximosJogos;
            ViewBag.RodadaAtual = rodadaAtual;

            return View();
        }

        private List<Classificacao> CalcularClassificacaoGrupo(List<Jogo> jogos)
        {
            var tabela = new Dictionary<int, Classificacao>();

            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                if (!jogo.PlacarCasa.HasValue || !jogo.PlacarVisitante.HasValue) continue;

                if (!tabela.ContainsKey(jogo.TimeCasaId))
                    tabela[jogo.TimeCasaId] = new Classificacao { TimeId = jogo.TimeCasaId, Time = jogo.TimeCasa };

                if (!tabela.ContainsKey(jogo.TimeVisitanteId))
                    tabela[jogo.TimeVisitanteId] = new Classificacao { TimeId = jogo.TimeVisitanteId, Time = jogo.TimeVisitante };

                var casa = tabela[jogo.TimeCasaId];
                var vis  = tabela[jogo.TimeVisitanteId];

                casa.Jogos++;
                vis.Jogos++;
                casa.GolsPro    += jogo.PlacarCasa.Value;
                casa.GolsContra += jogo.PlacarVisitante.Value;
                vis.GolsPro     += jogo.PlacarVisitante.Value;
                vis.GolsContra  += jogo.PlacarCasa.Value;

                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                {
                    casa.Vitorias++; casa.Pontos += 3; vis.Derrotas++;
                }
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                {
                    vis.Vitorias++; vis.Pontos += 3; casa.Derrotas++;
                }
                else
                {
                    casa.Empates++; vis.Empates++;
                    casa.Pontos++; vis.Pontos++;
                }
            }

            var lista = tabela.Values
                .OrderByDescending(t => t.Pontos)
                .ThenByDescending(t => t.GolsPro - t.GolsContra)
                .ThenByDescending(t => t.GolsPro)
                .ThenByDescending(t => t.Vitorias)
                .ToList();

            for (int i = 0; i < lista.Count; i++)
                lista[i].Posicao = i + 1;

            return lista;
        }
    }
}
