using System.Globalization;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models.Nba;
using ControleFutebolWeb.Models.ViewModels;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Controllers
{
    /// <summary>
    /// Página /NBA: jogos do dia, classificação, estatísticas, jogo e time. Tudo vem
    /// ao vivo da ESPN (ver <see cref="EspnNbaService"/>); não há nada da NBA no banco.
    ///
    /// Página PÚBLICA: [AllowAnonymous] porque o AuthorizeFilter global exige login em
    /// tudo, e o controller é isento do AssinaturaFilter (vale para qualquer plano).
    /// Pode ser pública porque só lê fontes externas (ESPN e futnatv) — nenhum dado
    /// de usuário passa por aqui — e o cache é compartilhado, então visitante a mais
    /// não vira chamada a mais para a ESPN. Continua sem ação de escrita — a única
    /// coisa gravada é o contador agregado de aberturas (AcessoPublicoFilter, visto
    /// em /Admin/Acessos), igual ao de /creators.
    /// </summary>
    [AllowAnonymous]
    [ServiceFilter(typeof(ControleFutebolWeb.Filters.AcessoPublicoFilter))]
    [Route("NBA")]
    public class NbaController : Controller
    {
        // Temporadas oferecidas no seletor: a atual e as anteriores com dado na ESPN.
        private const int TemporadasNoSeletor = 5;

        private readonly EspnNbaService _nba;
        private readonly TransmissaoNbaService _transmissao;

        public NbaController(EspnNbaService nba, TransmissaoNbaService transmissao)
        {
            _nba = nba;
            _transmissao = transmissao;
        }

        private static DateTime HojeBrasil() =>
            DateHelper.ParaBrasilia(DateTime.UtcNow)?.Date ?? DateTime.Today;

        private static int TemporadaValida(int? temporada)
        {
            var atual = EspnNbaService.TemporadaAtual(HojeBrasil());
            return temporada is int t && t <= atual && t > atual - TemporadasNoSeletor ? t : atual;
        }

        private static IReadOnlyList<int> Temporadas()
        {
            var atual = EspnNbaService.TemporadaAtual(HojeBrasil());
            return Enumerable.Range(0, TemporadasNoSeletor).Select(i => atual - i).ToList();
        }

        // GET /NBA  ·  /NBA?data=2026-10-21
        [HttpGet("")]
        [HttpGet("Jogos")]
        public async Task<IActionResult> Index(string? data, CancellationToken ct)
        {
            var hoje = HojeBrasil();
            var dia = DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.Date : hoje;

            var jogos = await _nba.ObterJogosAsync(dia, ct);
            return View(new NbaJogosViewModel
            {
                Dia = dia,
                EhHoje = dia == hoje,
                Jogos = jogos,
                CanaisBrasil = jogos == null ? new Dictionary<string, IReadOnlyList<string>>()
                    : await _transmissao.CanaisAsync(jogos, ct),
            });
        }

        // GET /NBA/Classificacao?nivel=divisao&temporada=2026
        [HttpGet("Classificacao")]
        public async Task<IActionResult> Classificacao(string? nivel, int? temporada, CancellationToken ct)
        {
            nivel = nivel is "divisao" or "liga" ? nivel : "conferencia";
            var ano = TemporadaValida(temporada);

            return View(new NbaClassificacaoViewModel
            {
                Nivel = nivel,
                Temporada = ano,
                Temporadas = Temporadas(),
                Classificacao = await _nba.ObterClassificacaoAsync(nivel, ano, ct),
            });
        }

        // GET /NBA/Estatisticas?aba=jogadores&ordem=reb&pagina=2&time=8
        [HttpGet("Estatisticas")]
        public async Task<IActionResult> Estatisticas(string? aba, int? temporada, string? ordem, int pagina = 1, string? time = null, CancellationToken ct = default)
        {
            // Id de time da ESPN é numérico; qualquer outra coisa vira "todos".
            time = !string.IsNullOrEmpty(time) && time.All(char.IsDigit) ? time : null;
            aba = aba is "jogadores" or "times" ? aba : "lideres";
            ordem = ordem != null && EspnNbaService.OrdensJogadores.ContainsKey(ordem) ? ordem : "pts";
            var ano = TemporadaValida(temporada);

            var vm = new NbaEstatisticasViewModel
            {
                Aba = aba,
                Temporada = ano,
                Temporadas = Temporadas(),
                Ordem = ordem,
                TimeId = time,
            };

            switch (aba)
            {
                case "jogadores":
                    vm.Jogadores = await _nba.ObterJogadoresAsync(ano, ordem, pagina, time, ct);
                    vm.TimesFiltro = await _nba.ObterTimesAsync(ct) ?? Array.Empty<NbaTimeResumo>();
                    break;
                case "times":
                    vm.Times = await _nba.ObterTimesEstatisticasAsync(ano, ct);
                    break;
                default:
                    vm.Lideres = await _nba.ObterLideresAsync(ano, ct);
                    break;
            }

            return View(vm);
        }

        // GET /NBA/Jogo/401909088
        [HttpGet("Jogo/{id}")]
        public async Task<IActionResult> Jogo(string id, CancellationToken ct)
        {
            if (!id.All(char.IsDigit)) return NotFound();
            var detalhe = await _nba.ObterJogoAsync(id, ct);
            if (detalhe == null) return View("Indisponivel");
            var canais = await _transmissao.CanaisAsync(new[] { detalhe.Jogo }, ct);
            ViewData["CanaisBrasil"] = canais.GetValueOrDefault(detalhe.Jogo.Id);
            return View(detalhe);
        }

        // GET /NBA/Jogador/1966?aba=jogos&temporada=2026
        [HttpGet("Jogador/{id}")]
        public async Task<IActionResult> Jogador(string id, string? aba, int? temporada, bool playoffs = false, CancellationToken ct = default)
        {
            if (!id.All(char.IsDigit)) return NotFound();
            var perfil = await _nba.ObterPerfilJogadorAsync(id, ct);
            if (perfil == null) return View("Indisponivel");

            aba = aba is "jogos" or "carreira" or "recortes" ? aba : "geral";
            // Temporada livre aqui (o seletor vem da própria ESPN e vai até a estreia
            // do jogador), só limitada a um intervalo plausível.
            var atual = EspnNbaService.TemporadaAtual(HojeBrasil());
            var ano = temporada is int t && t >= 1947 && t <= atual ? t : atual;

            var vm = new NbaJogadorViewModel { Perfil = perfil, Aba = aba, Temporada = ano, Playoffs = playoffs };
            switch (aba)
            {
                case "jogos": vm.Jogos = await _nba.ObterGameLogAsync(id, ano, ct); break;
                case "carreira": vm.Carreira = await _nba.ObterCarreiraAsync(id, playoffs, ct); break;
                case "recortes": vm.Recortes = await _nba.ObterRecortesAsync(id, ano, ct); break;
                default: vm.Visao = await _nba.ObterVisaoGeralJogadorAsync(id, ct); break;
            }
            return View(vm);
        }

        // GET /NBA/Time/8
        [HttpGet("Time/{id}")]
        public async Task<IActionResult> Time(string id, int? temporada, CancellationToken ct)
        {
            if (!id.All(char.IsDigit)) return NotFound();
            var ano = TemporadaValida(temporada);
            var detalhe = await _nba.ObterTimeAsync(id, ano, ct);
            if (detalhe == null) return View("Indisponivel");
            return View(new NbaTimeViewModel { Temporada = ano, Detalhe = detalhe });
        }
    }
}
