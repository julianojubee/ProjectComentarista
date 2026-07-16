using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/relatorios")]
    public class RelatoriosApiController : ApiControllerBase
    {
        private readonly RelatoriosService _relatorios;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Data.FutebolContext _context;

        public RelatoriosApiController(RelatoriosService relatorios, UserManager<ApplicationUser> userManager, Data.FutebolContext context)
        {
            _relatorios = relatorios;
            _userManager = userManager;
            _context = context;
        }

        // GET api/v1/relatorios/resumo?competicaoIds=&timeIds=&temporada=&incluirNaoAnalisados=&apenas10Jogos=
        // Mesmos filtros e mesma regra de negócio da página /Relatorios (via
        // RelatoriosService); devolve só os rankings mais usados.
        [HttpGet("resumo")]
        public async Task<ActionResult<RelatorioResumoDto>> Resumo(
            // [FromQuery] explícito: com [ApiController], arrays seriam inferidos
            // como FromBody e a aplicação nem sobe (InvalidOperationException).
            [FromQuery] int[]? competicaoIds, [FromQuery] int[]? timeIds, int? temporada,
            bool incluirNaoAnalisados = false, bool apenas10Jogos = false)
        {
            var usuarioId = _userManager.GetUserId(User)!;

            var vm = await _relatorios.MontarAsync(
                competicaoIds, timeIds, temporada, incluirNaoAnalisados, apenas10Jogos ? 10 : 1, usuarioId,
                incluirEstatisticasCompeticoes: false, incluirMatchUp: false);

            // Sem jogos analisados o VM devolve zero temporadas e o filtro do app
            // ficaria vazio/desabilitado — cai para todas as temporadas do banco.
            var temporadasDisponiveis = vm.TemporadasDisponiveis;
            if (temporadasDisponiveis.Count == 0)
                temporadasDisponiveis = await _context.Jogos
                    .Select(j => j.Temporada).Distinct()
                    .OrderByDescending(t => t)
                    .ToListAsync();

            var dto = new RelatorioResumoDto
            {
                Temporada = vm.TemporadaFiltro,
                TemporadasDisponiveis = temporadasDisponiveis,
                Competicoes = vm.Competicoes
                    .Select(c => new CompeticaoRefDto { Id = c.Id, Nome = c.Nome })
                    .ToList(),
                ExibirSelecao = vm.ExibirSelecao,
                TotalJogos = vm.TotalJogos,
                TotalGols = vm.TotalGols,
                TotalGolsContra = vm.TotalGolsContra,
                TotalCartaoAmarelo = vm.TotalCartaoAmarelo,
                TotalCartaoVermelho = vm.TotalCartaoVermelho,

                RankingNotas = vm.RankingNotas.Select(r => ParaRankingDto(r, vm.ExibirSelecao)).ToList(),
                RankingGoleiros = vm.RankingGoleiros.Select(r => ParaRankingDto(r, vm.ExibirSelecao)).ToList(),
                RankingDefensores = vm.RankingDefensores.Select(r => ParaRankingDto(r, vm.ExibirSelecao)).ToList(),
                RankingMeias = vm.RankingMeias.Select(r => ParaRankingDto(r, vm.ExibirSelecao)).ToList(),
                RankingAtacantes = vm.RankingAtacantes.Select(r => ParaRankingDto(r, vm.ExibirSelecao)).ToList(),

                Artilheiros = vm.Artilheiros.Select(e => ParaJogadorValorDto(e, vm.ExibirSelecao)).ToList(),
                Assistencias = vm.Assistencias.Select(e => ParaJogadorValorDto(e, vm.ExibirSelecao)).ToList(),
                MaisPartidas = vm.MaisPartidas.Select(e => ParaJogadorValorDto(e, vm.ExibirSelecao)).ToList(),
                MaisCartoesAmarelos = vm.MaisCartoesAmarelos.Select(e => ParaJogadorValorDto(e, vm.ExibirSelecao)).ToList(),
                MaisCartoesVermelhos = vm.MaisCartoesVermelhos.Select(e => ParaJogadorValorDto(e, vm.ExibirSelecao)).ToList(),

                TimesAproveitamento = vm.TimesAproveitamento.Select(ParaTimeDto).ToList(),
                TimesMaisPontos = vm.TimesMaisPontos.Select(ParaTimeDto).ToList(),
                TimesGols = vm.TimesGols.Select(ParaTimeDto).ToList(),
                TimesMenosGolsSofridos = vm.TimesMenosGolsSofridos.Select(ParaTimeDto).ToList(),

                MediasPorPosicao = vm.MediasPorPosicao.Select(m => new MediaPosicaoDto
                {
                    Posicao = m.Posicao,
                    Media = m.Media,
                    TotalJogadores = m.TotalJogadores
                }).ToList(),
            };

            return Ok(dto);
        }

        // Nome do time exibido: seleção quando o filtro é só de competições de
        // seleções (mesma regra da view web).
        private static string? NomeTimeExibido(Jogador j, bool exibirSelecao) =>
            exibirSelecao && j.Selecao != null ? j.Selecao.Nome : j.Time?.Nome;

        private RankingNotaDto ParaRankingDto(RankingNotaItem r, bool exibirSelecao) => new()
        {
            JogadorId = r.Jogador.Id,
            Nome = r.Jogador.Nome,
            NomeExibicao = r.Jogador.NomeExibicao,
            Posicao = r.Jogador.Posicao,
            TimeNome = NomeTimeExibido(r.Jogador, exibirSelecao),
            FotoUrl = Url.FotoSrcAbsoluto(Request, r.Jogador.FotoUrl),
            NotaFinal = r.NotaFinal,
            NotaLabel = r.NotaLabel,
            NotaColor = r.NotaColor,
            Partidas = r.Partidas,
            Vitorias = r.Vitorias,
            Derrotas = r.Derrotas,
            Empates = r.Empates
        };

        private JogadorValorDto ParaJogadorValorDto(JogadorEstatistica e, bool exibirSelecao) => new()
        {
            JogadorId = e.Jogador.Id,
            Nome = e.Jogador.Nome,
            NomeExibicao = e.Jogador.NomeExibicao,
            Posicao = e.Jogador.Posicao,
            TimeNome = NomeTimeExibido(e.Jogador, exibirSelecao),
            FotoUrl = Url.FotoSrcAbsoluto(Request, e.Jogador.FotoUrl),
            Valor = e.Valor,
            Detalhe = e.Detalhe
        };

        private TimeEstatisticaDto ParaTimeDto(TimeEstatistica t) => new()
        {
            TimeId = t.Time.Id,
            Nome = t.Time.Nome,
            EscudoUrl = Url.FotoSrcAbsoluto(Request, t.Time.EscudoUrl),
            Jogos = t.Jogos,
            Vitorias = t.Vitorias,
            Empates = t.Empates,
            Derrotas = t.Derrotas,
            GolsPro = t.GolsPro,
            GolsContra = t.GolsContra,
            SaldoGols = t.SaldoGols,
            Pontos = t.Pontos,
            Aproveitamento = t.Aproveitamento
        };
    }
}
