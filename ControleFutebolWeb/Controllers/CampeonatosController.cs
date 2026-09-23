using System.Security.Cryptography;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Filters;
using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    // Módulo de campeonatos próprios: campeonato entre amigos, liga amadora.
    // Tudo aqui é do usuário logado — nenhuma action enxerga campeonato de outro
    // dono (o link público é o CampeonatoPublicoController, só leitura).
    [Authorize]
    [ModuloRequerido(ModuloSistema.Campeonatos)]
    public class CampeonatosController : Controller
    {
        private readonly FutebolContext _context;
        private readonly CampeonatoService _servico;
        private readonly UserManager<ApplicationUser> _userManager;

        public CampeonatosController(FutebolContext context, CampeonatoService servico, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _servico = servico;
            _userManager = userManager;
        }

        private string Uid => _userManager.GetUserId(User)!;

        // GET: /Campeonatos
        public async Task<IActionResult> Index()
        {
            var campeonatos = await _context.Campeonatos
                .Where(c => c.UsuarioId == Uid)
                .Include(c => c.Participantes)
                .OrderBy(c => c.Status == StatusCampeonato.Encerrado)
                .ThenByDescending(c => c.CriadoEm)
                .AsNoTracking()
                .ToListAsync();
            return View(campeonatos);
        }

        // GET: /Campeonatos/Criar
        public IActionResult Criar() => View(new CampeonatoCriarInput());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(CampeonatoCriarInput input)
        {
            if (input.Formato is not ("PONTOS_CORRIDOS" or "GRUPOS" or "MATA_MATA"))
                ModelState.AddModelError(nameof(input.Formato), "Escolha um formato.");
            if (!ModelState.IsValid) return View(input);

            var campeonato = new Campeonato
            {
                UsuarioId = Uid,
                Nome = input.Nome.Trim(),
                Modalidade = input.Modalidade,
                Plataforma = string.IsNullOrWhiteSpace(input.Plataforma) ? null : input.Plataforma.Trim(),
                Tipo = input.Formato,
                CriadoEm = DateTime.UtcNow
            };
            foreach (var fase in FasesDoFormato(input)) campeonato.Fases.Add(fase);

            _context.Campeonatos.Add(campeonato);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Detalhes), new { id = campeonato.Id, aba = "participantes" });
        }

        // Todo campeonato tem ao menos uma fase: é nela que ficam "ida e volta" e
        // quantos se classificam, e é por fase que as partidas são geradas.
        private static IEnumerable<CampeonatoFase> FasesDoFormato(CampeonatoCriarInput input) => input.Formato switch
        {
            "GRUPOS" => new[]
            {
                new CampeonatoFase { Nome = "Fase de grupos", Tipo = "GRUPOS", Ordem = 1, IdaEVolta = input.IdaEVolta, Classificados = input.ClassificadosPorGrupo },
                new CampeonatoFase { Nome = "Mata-mata", Tipo = "MATA_MATA", Ordem = 2, IdaEVolta = input.MataMataIdaEVolta }
            },
            "MATA_MATA" => new[] { new CampeonatoFase { Nome = "Mata-mata", Tipo = "MATA_MATA", Ordem = 1, IdaEVolta = input.IdaEVolta } },
            _ => new[] { new CampeonatoFase { Nome = "Pontos corridos", Tipo = "PONTOS_CORRIDOS", Ordem = 1, IdaEVolta = input.IdaEVolta } }
        };

        // GET: /Campeonatos/Detalhes/5?aba=partidas
        public async Task<IActionResult> Detalhes(int id, string? aba)
        {
            var campeonato = await _servico.CarregarAsync(id, Uid);
            if (campeonato == null) return NotFound();

            var vm = new CampeonatoDetalhesViewModel
            {
                Campeonato = campeonato,
                Aba = aba is "partidas" or "participantes" or "artilharia" ? aba : "tabela",
                Painel = CampeonatoPainelAdapter.MontarPainel(campeonato),
                Participantes = campeonato.Participantes.ToDictionary(p => p.Id),
                Artilharia = EstatisticasCampeonatoHelper.PorAtleta(campeonato.Partidas.SelectMany(p => p.Eventos)),
                LinkPublico = campeonato.TokenPublico == null ? null
                    : Url.Action("Index", "CampeonatoPublico", new { token = campeonato.TokenPublico }, Request.Scheme)
            };

            if (vm.Aba == "participantes" && !vm.PrimeiraFaseGerada)
            {
                vm.MeusTimes = await _context.TimesProprios
                    .Where(t => t.UsuarioId == Uid && t.ArquivadoEm == null)
                    .OrderBy(t => t.Nome).AsNoTracking().ToListAsync();
                vm.TimesReais = await _context.Times
                    .OrderBy(t => t.Nome).AsNoTracking().ToListAsync();
            }

            return View(vm);
        }

        // ── Participantes ───────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarParticipante(int id, string? nome, string origem, int? timeProprioId, int? timeId, string? grupo, int? semente)
        {
            var campeonato = await _servico.CarregarAsync(id, Uid);
            if (campeonato == null) return NotFound();
            if (campeonato.Partidas.Any())
                return Voltar(id, "participantes", "As partidas já foram geradas: não dá mais para mudar os participantes.");

            var participante = new CampeonatoParticipante
            {
                CampeonatoId = id,
                Nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim(),
                Grupo = string.IsNullOrWhiteSpace(grupo) ? null : grupo.Trim().ToUpperInvariant(),
                Semente = semente,
                Ordem = campeonato.Participantes.Count + 1
            };

            if (origem == "proprio" && timeProprioId != null)
            {
                var time = await _context.TimesProprios.FirstOrDefaultAsync(t => t.Id == timeProprioId && t.UsuarioId == Uid);
                if (time == null) return Voltar(id, "participantes", "Time não encontrado.");
                participante.TimeProprioId = time.Id;
                participante.NomeTimeSnapshot = time.Nome;
                participante.EscudoUrlSnapshot = time.EscudoUrl;
            }
            else if (origem == "real" && timeId != null)
            {
                var time = await _context.Times.AsNoTracking().FirstOrDefaultAsync(t => t.Id == timeId);
                if (time == null) return Voltar(id, "participantes", "Time não encontrado.");
                participante.TimeId = time.Id;
                participante.NomeTimeSnapshot = time.Nome;
                participante.EscudoUrlSnapshot = time.EscudoUrl;
            }

            if (participante.Nome == null && participante.NomeTimeSnapshot == null)
                return Voltar(id, "participantes", "Informe um nome ou escolha um time.");

            campeonato.Participantes.Add(participante);
            await _context.SaveChangesAsync();
            return Voltar(id, "participantes");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverParticipante(int id, int participanteId)
        {
            var campeonato = await _servico.CarregarAsync(id, Uid);
            if (campeonato == null) return NotFound();
            if (campeonato.Partidas.Any())
                return Voltar(id, "participantes", "As partidas já foram geradas: não dá mais para mudar os participantes.");

            var participante = campeonato.Participantes.FirstOrDefault(p => p.Id == participanteId);
            if (participante != null)
            {
                _context.CampeonatoParticipantes.Remove(participante);
                await _context.SaveChangesAsync();
            }
            return Voltar(id, "participantes");
        }

        // ── Partidas ────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarFase(int id, int faseId, int? quantidadeGrupos)
        {
            var resultado = await _servico.GerarFaseAsync(id, faseId, Uid, quantidadeGrupos);
            return resultado.Ok
                ? Voltar(id, "partidas", mensagem: $"{resultado.Partidas} partidas geradas.")
                : Voltar(id, "tabela", resultado.Erro);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarPlacar(int id, int partidaId, int? placarCasa, int? placarVisitante,
            int? penaltisCasa, int? penaltisVisitante, bool wo, string? voltarPara)
        {
            var partida = await _context.PartidasCampeonato
                .FirstOrDefaultAsync(p => p.Id == partidaId && p.CampeonatoId == id && p.Campeonato.UsuarioId == Uid);
            if (partida == null) return NotFound();
            if (partida.ParticipanteCasaId == null || partida.ParticipanteVisitanteId == null)
                return Voltar(id, "partidas", "Esta partida ainda não tem os dois participantes.");

            // Placar pela metade não vale: ou os dois lados, ou nenhum (desfaz o resultado).
            var temPlacar = placarCasa != null && placarVisitante != null;
            partida.PlacarCasa = temPlacar ? Math.Max(0, placarCasa!.Value) : null;
            partida.PlacarVisitante = temPlacar ? Math.Max(0, placarVisitante!.Value) : null;
            var temPenaltis = temPlacar && penaltisCasa != null && penaltisVisitante != null;
            partida.PenaltisCasa = temPenaltis ? Math.Max(0, penaltisCasa!.Value) : null;
            partida.PenaltisVisitante = temPenaltis ? Math.Max(0, penaltisVisitante!.Value) : null;
            partida.WO = temPlacar && wo;
            await _context.SaveChangesAsync();

            await _servico.AtualizarChaveamentoAsync(id, Uid);

            if (voltarPara == "sumula") return RedirectToAction(nameof(Sumula), new { id, partidaId });
            return Redirect(Url.Action(nameof(Detalhes), new { id, aba = "partidas" }) + $"#partida-{partidaId}");
        }

        // GET: /Campeonatos/Sumula/5?partidaId=12
        public async Task<IActionResult> Sumula(int id, int partidaId)
        {
            var campeonato = await _servico.CarregarAsync(id, Uid);
            var partida = campeonato?.Partidas.FirstOrDefault(p => p.Id == partidaId);
            if (campeonato == null || partida == null) return NotFound();

            var casa = campeonato.Participantes.FirstOrDefault(p => p.Id == partida.ParticipanteCasaId);
            var visitante = campeonato.Participantes.FirstOrDefault(p => p.Id == partida.ParticipanteVisitanteId);

            return View(new SumulaViewModel
            {
                Campeonato = campeonato,
                Partida = partida,
                Casa = casa,
                Visitante = visitante,
                ElencoCasa = await ElencoAsync(casa, partida.TimeCasaUsadoId),
                ElencoVisitante = await ElencoAsync(visitante, partida.TimeVisitanteUsadoId)
            });
        }

        // Quem pode aparecer na súmula por aquele lado: o elenco do time próprio, ou
        // o elenco atual do time real (o usado na partida, se o videogame trocou).
        private async Task<List<OpcaoAtleta>> ElencoAsync(CampeonatoParticipante? participante, int? timeUsadoId)
        {
            if (participante == null) return new();

            var timeRealId = timeUsadoId ?? participante.TimeId;
            if (timeRealId != null)
            {
                return await _context.Jogadores
                    .Where(j => j.TimeId == timeRealId && !j.Aposentado)
                    .OrderBy(j => j.Nome)
                    .Select(j => new OpcaoAtleta("r:" + j.Id, j.Nome))
                    .ToListAsync();
            }

            if (participante.TimeProprioId != null)
            {
                return await _context.ItensElenco
                    .Where(e => e.TimeProprioId == participante.TimeProprioId)
                    .OrderBy(e => e.Ordem).ThenBy(e => e.NomeSnapshot)
                    .Select(e => new OpcaoAtleta(
                        e.JogadorId != null ? "r:" + e.JogadorId : "p:" + e.JogadorProprioId,
                        e.NomeSnapshot))
                    .ToListAsync();
            }

            return new();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarEvento(int id, int partidaId, TipoEventoPartida tipo, int participanteId,
            string? atleta, string? nomeLivre, int? minuto, string? assistente, string? nomeLivreAssistente)
        {
            var partida = await _context.PartidasCampeonato
                .FirstOrDefaultAsync(p => p.Id == partidaId && p.CampeonatoId == id && p.Campeonato.UsuarioId == Uid);
            if (partida == null) return NotFound();
            if (participanteId != partida.ParticipanteCasaId && participanteId != partida.ParticipanteVisitanteId)
                return BadRequest();

            var evento = await MontarEventoAsync(partidaId, tipo, participanteId, atleta, nomeLivre, minuto);
            if (evento == null)
            {
                TempData["Erro"] = "Escolha o jogador ou digite o nome.";
                return RedirectToAction(nameof(Sumula), new { id, partidaId });
            }
            _context.EventosPartidaCampeonato.Add(evento);

            if (tipo == TipoEventoPartida.Gol && (!string.IsNullOrWhiteSpace(assistente) || !string.IsNullOrWhiteSpace(nomeLivreAssistente)))
            {
                var assistencia = await MontarEventoAsync(partidaId, TipoEventoPartida.Assistencia, participanteId, assistente, nomeLivreAssistente, minuto);
                if (assistencia != null)
                {
                    assistencia.Gol = evento;
                    _context.EventosPartidaCampeonato.Add(assistencia);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Sumula), new { id, partidaId });
        }

        // "r:12" = Jogador 12, "p:3" = JogadorProprio 3 (só do usuário); nenhum dos
        // dois = nome digitado. O nome sempre fica gravado no NomeSnapshot.
        private async Task<EventoPartidaCampeonato?> MontarEventoAsync(int partidaId, TipoEventoPartida tipo, int participanteId,
            string? atleta, string? nomeLivre, int? minuto)
        {
            var evento = new EventoPartidaCampeonato
            {
                PartidaId = partidaId,
                ParticipanteId = participanteId,
                Tipo = tipo,
                Minuto = minuto is >= 0 and <= 200 ? minuto : null
            };

            if (atleta?.StartsWith("r:") == true && int.TryParse(atleta[2..], out var jogadorId))
            {
                var nome = await _context.Jogadores.Where(j => j.Id == jogadorId).Select(j => j.Nome).FirstOrDefaultAsync();
                if (nome == null) return null;
                evento.JogadorId = jogadorId;
                evento.NomeSnapshot = nome;
            }
            else if (atleta?.StartsWith("p:") == true && int.TryParse(atleta[2..], out var proprioId))
            {
                var jogador = await _context.JogadoresProprios.FirstOrDefaultAsync(j => j.Id == proprioId && j.UsuarioId == Uid);
                if (jogador == null) return null;
                evento.JogadorProprioId = proprioId;
                evento.NomeSnapshot = jogador.NomeExibicao;
            }
            else if (!string.IsNullOrWhiteSpace(nomeLivre))
            {
                evento.NomeSnapshot = nomeLivre.Trim()[..Math.Min(80, nomeLivre.Trim().Length)];
            }
            else
            {
                return null;
            }

            return evento;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverEvento(int id, int partidaId, int eventoId)
        {
            var evento = await _context.EventosPartidaCampeonato
                .FirstOrDefaultAsync(e => e.Id == eventoId && e.PartidaId == partidaId
                                          && e.Partida.CampeonatoId == id && e.Partida.Campeonato.UsuarioId == Uid);
            if (evento != null)
            {
                _context.EventosPartidaCampeonato.Remove(evento);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Sumula), new { id, partidaId });
        }

        // ── Link público e exclusão ─────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Compartilhar(int id, bool revogar = false)
        {
            var campeonato = await _context.Campeonatos.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == Uid);
            if (campeonato == null) return NotFound();

            // Token opaco (24 bytes aleatórios), como o da análise compartilhada: é a
            // única credencial de quem abre o link.
            campeonato.TokenPublico = revogar ? null
                : Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            await _context.SaveChangesAsync();
            return Voltar(id, "tabela", mensagem: revogar ? "Link público desativado." : "Link público criado.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            var campeonato = await _context.Campeonatos.FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == Uid);
            if (campeonato == null) return NotFound();

            // Partidas, participantes, fases e súmula vão juntos pelo cascade do banco.
            _context.Campeonatos.Remove(campeonato);
            await _context.SaveChangesAsync();
            TempData["Mensagem"] = $"Campeonato \"{campeonato.Nome}\" excluído.";
            return RedirectToAction(nameof(Index));
        }

        private IActionResult Voltar(int id, string aba, string? erro = null, string? mensagem = null)
        {
            if (erro != null) TempData["Erro"] = erro;
            if (mensagem != null) TempData["Mensagem"] = mensagem;
            return RedirectToAction(nameof(Detalhes), new { id, aba });
        }
    }
}
