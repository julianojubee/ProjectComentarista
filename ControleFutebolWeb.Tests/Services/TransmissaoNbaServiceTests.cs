using System.Net;
using System.Text;
using ControleFutebolWeb.Models.Nba;
using ControleFutebolWeb.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFutebolWeb.Tests.Services
{
    /// <summary>
    /// Casamento dos jogos da ESPN com a grade do futnatv (/api/basquete). Os erros
    /// que importam aqui são silenciosos: buscar pelo dia errado (a rodada dos EUA em
    /// vez do dia de Brasília) ou exigir a ordem mandante x visitante, que o futnatv
    /// não segue — nos dois casos o canal só some da tela.
    ///
    /// Os JSONs reproduzem a estrutura da API com jogos inventados.
    /// </summary>
    public class TransmissaoNbaServiceTests
    {
        private const string Dia21 = """
        {"schedule":[{"key":"2026-10-21","games":[
          {"sport":"basquete","time":"20h00","competition":"NBA","home":"Hawks","away":"Magic","broadcast":"NBA League Pass"},
          {"sport":"basquete","time":"21h00","competition":"WNBA","home":"Lakers","away":"Warriors","broadcast":"ESPN 3"},
          {"sport":"basquete","time":"19h30","competition":"NBB","home":"Franca","away":"Mogi","broadcast":"YouTube (NBB)"}
        ]}]}
        """;

        // O jogo das 00h em Brasília está no dia 28 do futnatv, mas na rodada de 27 da ESPN.
        private const string Dia28 = """
        {"schedule":[{"key":"2026-10-28","games":[
          {"sport":"basquete","time":"00h00","competition":"NBA","home":"Trail Blazers","away":"Lakers","broadcast":"ESPN 2 e Disney+"}
        ]}]}
        """;

        private sealed class FutnatvFalso : HttpMessageHandler
        {
            public List<string> Pedidos { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var url = request.RequestUri!.PathAndQuery;
                Pedidos.Add(url);
                var corpo = url.Contains("2026-10-21") ? Dia21
                          : url.Contains("2026-10-28") ? Dia28
                          : """{"schedule":[]}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
                });
            }
        }

        private static (TransmissaoNbaService Servico, FutnatvFalso Http) Criar()
        {
            var http = new FutnatvFalso();
            var futnatv = new FutnatvService(new HttpClient(http), NullLogger<FutnatvService>.Instance);
            var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000_000 });
            return (new TransmissaoNbaService(futnatv, cache), http);
        }

        private static NbaJogo Jogo(string id, DateTime inicioUtc, string casa, string fora)
        {
            NbaLadoJogo Lado(string apelido, bool ehCasa) => new(
                new NbaTimeResumo(apelido, apelido, apelido, apelido[..3].ToUpperInvariant(), null, null),
                ehCasa, null, false, null, Array.Empty<int>(), Array.Empty<NbaLider>());
            return new NbaJogo(id, inicioUtc, NbaSituacao.Agendado, "", Lado(casa, true), Lado(fora, false),
                null, null, Array.Empty<string>(), null, null);
        }

        [Fact]
        public async Task Casa_o_par_sem_depender_do_mando()
        {
            var (servico, _) = Criar();
            // Na ESPN o Magic é o mandante; no futnatv aparece "Hawks x Magic".
            var jogo = Jogo("1", new DateTime(2026, 10, 21, 23, 0, 0, DateTimeKind.Utc), "Magic", "Hawks");

            var canais = await servico.CanaisAsync(new[] { jogo });

            Assert.Equal(new[] { "NBA League Pass" }, canais["1"]);
        }

        [Fact]
        public async Task Jogo_da_meia_noite_e_buscado_no_dia_de_Brasilia()
        {
            var (servico, http) = Criar();
            // 03h UTC do dia 28 = 00h de 28 em Brasília (rodada de 27 nos EUA).
            var jogo = Jogo("2", new DateTime(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc), "Lakers", "Trail Blazers");

            var canais = await servico.CanaisAsync(new[] { jogo });

            Assert.Contains(http.Pedidos, p => p.Contains("api/basquete?data=2026-10-28"));
            Assert.Equal(new[] { "ESPN 2", "Disney+" }, canais["2"]);
        }

        [Fact]
        public async Task Ignora_outras_ligas_com_os_mesmos_apelidos()
        {
            var (servico, _) = Criar();
            // "Lakers x Warriors" do dia 21 está como WNBA — não pode virar canal da NBA.
            var jogo = Jogo("3", new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), "Warriors", "Lakers");

            var canais = await servico.CanaisAsync(new[] { jogo });

            Assert.False(canais.ContainsKey("3"));
        }

        [Fact]
        public async Task Um_pedido_por_dia_mesmo_com_varios_jogos()
        {
            var (servico, http) = Criar();
            var jogos = new[]
            {
                Jogo("1", new DateTime(2026, 10, 21, 23, 0, 0, DateTimeKind.Utc), "Magic", "Hawks"),
                Jogo("4", new DateTime(2026, 10, 21, 23, 30, 0, DateTimeKind.Utc), "Heat", "Timberwolves"),
            };

            await servico.CanaisAsync(jogos);
            await servico.CanaisAsync(jogos);

            Assert.Single(http.Pedidos);
        }

        [Theory]
        [InlineData("ESPN 2 e Disney+", new[] { "ESPN 2", "Disney+" })]
        [InlineData("Prime Video", new[] { "Prime Video" })]
        [InlineData("YT", new[] { "YouTube" })]
        [InlineData("-", new string[0])]
        public void Separa_os_canais(string texto, string[] esperado)
        {
            Assert.Equal(esperado, TransmissaoNbaService.Canais(texto));
        }
    }
}
