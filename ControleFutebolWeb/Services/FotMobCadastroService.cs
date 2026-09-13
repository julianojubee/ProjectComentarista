using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Completa o cadastro do jogador (altura, nascimento, nacionalidade e foto) com o
    /// que o FotMob publica no perfil dele, quando a api-football não trouxe.
    ///
    /// Roda ao abrir /Jogadores/EstatisticasAvancadas, porque é ali que o perfil do
    /// FotMob já vai ser buscado de qualquer jeito: o serviço reaproveita a mesma
    /// resposta em cache (FotMobService.BuscarPerfilJogadorAsync guarda 6h), então
    /// completar o cadastro não custa uma visita a mais à fonte.
    ///
    /// POR QUE ESTE SERVIÇO É SEPARADO DO FotMobPerfilService
    ///
    /// Aquele serviço não grava nada, de propósito — é o que garante que a tela de
    /// estatísticas avançadas não possa divergir do resto do sistema. Aqui é o
    /// contrário: o objetivo é justamente gravar. Manter os dois no mesmo lugar
    /// apagaria a fronteira que faz aquela garantia valer.
    ///
    /// SÓ PREENCHE O QUE ESTÁ VAZIO. Nunca sobrescreve — nem para "corrigir". Um dado
    /// já preenchido veio da api-football ou da mão do usuário, e os dois valem mais
    /// que o palpite de uma terceira fonte (o cadastro diz 182 cm onde o FotMob diz
    /// 181; trocar isso a cada visita faria o número dançar sem ninguém pedir).
    ///
    /// O nome é o outro caso: quando a altura é completada, o nome do FotMob também
    /// substitui o do cadastro se forem diferentes (ver ComplementarAsync).
    ///
    /// A outra exceção é a foto, e ela não é bem uma exceção: a silhueta cinza que a
    /// api-sports devolve para quem ela não tem retrato É a ausência de foto, só que
    /// gravada como se fosse uma — ver EhSilhuetaAsync.
    ///
    /// PESO NÃO ENTRA: o FotMob simplesmente não publica. O bloco playerInformation
    /// traz altura, camisa, idade, pé preferido, país, valor de mercado e fim de
    /// contrato — peso não está lá para nenhum jogador. Quem tem peso no cadastro tem
    /// porque veio da api-football.
    /// </summary>
    public class FotMobCadastroService
    {
        private readonly FutebolContext _context;
        private readonly FotMobService _fotmob;
        private readonly IHttpClientFactory _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FotMobCadastroService> _logger;

        public FotMobCadastroService(
            FutebolContext context, FotMobService fotmob, IHttpClientFactory http,
            IMemoryCache cache, ILogger<FotMobCadastroService> logger)
        {
            _context = context;
            _fotmob = fotmob;
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Preenche o que falta no cadastro do jogador e devolve os campos completados,
        /// em português, para a tela poder avisar o usuário do que mudou.
        ///
        /// Nunca lança: um cadastro incompleto é o estado normal de quem chega aqui, e
        /// falhar a fonte não pode impedir a tela de estatísticas avançadas de abrir —
        /// ela é o que o usuário pediu, isto é um efeito colateral bem-vindo.
        /// </summary>
        public async Task<IReadOnlyList<string>> ComplementarAsync(
            int jogadorId, long idFotMob, CancellationToken ct = default)
        {
            var vazio = Array.Empty<string>();

            try
            {
                var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == jogadorId, ct);
                if (jogador == null) return vazio;

                // Cadastro completo não pede nada à fonte. Peso fica fora da conta de
                // propósito: como o FotMob não o publica, incluí-lo faria todo jogador
                // sem peso buscar o perfil à toa, para sempre.
                var faltaFoto = string.IsNullOrWhiteSpace(jogador.FotoUrl) ||
                                await EhSilhuetaAsync(jogador.FotoUrl!, ct);
                if (jogador.Altura != null && jogador.DataNascimento != null &&
                    jogador.NacionalidadeId != null && !faltaFoto)
                    return vazio;

                using var perfil = await _fotmob.BuscarPerfilJogadorAsync(idFotMob, ct);
                if (perfil == null) return vazio;

                var raiz = perfil.RootElement;
                var completados = new List<string>();

                if (jogador.Altura == null && Altura(raiz) is { } altura)
                {
                    jogador.Altura = altura;
                    completados.Add("altura");

                    // Junto com a altura, o nome passa a ser o do FotMob quando difere.
                    // Quem chega sem altura costuma ser o estreante que a fonte do jogo
                    // só conhecia abreviado ("I. Machado"), enquanto o perfil já tem o
                    // nome completo ("Iago Machado"). Fica preso à altura de propósito:
                    // é o momento em que o cadastro está sendo completado pela primeira
                    // vez, então um nome já acertado à mão depois disso não é desfeito.
                    var nomeFotMob = Nome(raiz);
                    if (nomeFotMob != null &&
                        !string.Equals(jogador.Nome?.Trim(), nomeFotMob, StringComparison.Ordinal))
                    {
                        _logger.LogInformation(
                            "[FotMobCadastro] Nome do jogador {Id} trocado de \"{Antigo}\" para \"{Novo}\" pelo FotMob {IdFotMob}.",
                            jogador.Id, jogador.Nome, nomeFotMob, idFotMob);
                        jogador.Nome = nomeFotMob!;
                        completados.Add("nome");
                    }
                }

                if (jogador.DataNascimento == null && Nascimento(raiz) is { } nascimento)
                {
                    jogador.DataNascimento = nascimento;
                    completados.Add("data de nascimento");
                }

                if (jogador.NacionalidadeId == null && Pais(raiz) is { } pais)
                {
                    var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(
                        _context, pais, ct);
                    if (nac != null)
                    {
                        jogador.NacionalidadeId = nac.Id;
                        completados.Add("nacionalidade");
                    }
                }

                // A foto é a única que exige uma segunda chamada (a URL não vem no
                // perfil), então fica por último: só é perguntada a quem realmente
                // não tem retrato nenhum.
                if (faltaFoto && await _fotmob.TemFotoJogadorAsync(idFotMob, ct))
                {
                    jogador.FotoUrl = $"/MediaProxy/FotoJogador/{idFotMob}";
                    completados.Add("foto");
                }

                if (completados.Count == 0) return vazio;

                jogador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[FotMobCadastro] {Nome} (id {Id}): completado {Campos} pelo FotMob {IdFotMob}.",
                    jogador.Nome, jogador.Id, string.Join(", ", completados), idFotMob);

                return completados;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "[FotMobCadastro] Não foi possível completar o cadastro do jogador {Id}.", jogadorId);
                return vazio;
            }
        }

        /// <summary>
        /// Sincronização pedida pelo usuário (botão da tela de estatísticas avançadas):
        /// ao contrário de ComplementarAsync, SOBRESCREVE foto, nome, altura, data de
        /// nascimento (idade) e nacionalidade com o que o FotMob publica, campo a
        /// campo e só quando o valor é diferente. Campo que o FotMob não traz fica
        /// como está — ausência na fonte não apaga o cadastro.
        ///
        /// Devolve os campos alterados (vazio = já estava tudo igual) ou nulo quando
        /// não foi possível ler o perfil.
        /// </summary>
        public async Task<IReadOnlyList<string>?> SincronizarAsync(
            int jogadorId, long idFotMob, CancellationToken ct = default)
        {
            try
            {
                var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == jogadorId, ct);
                if (jogador == null) return null;

                using var perfil = await _fotmob.BuscarPerfilJogadorAsync(idFotMob, ct);
                if (perfil == null) return null;

                var raiz = perfil.RootElement;
                var alterados = new List<string>();

                if (Nome(raiz) is { } nome && !string.Equals(jogador.Nome?.Trim(), nome, StringComparison.Ordinal))
                {
                    jogador.Nome = nome;
                    alterados.Add("nome");
                }

                if (Altura(raiz) is { } altura && jogador.Altura != altura)
                {
                    jogador.Altura = altura;
                    alterados.Add("altura");
                }

                if (Nascimento(raiz) is { } nascimento && jogador.DataNascimento?.Date != nascimento.Date)
                {
                    jogador.DataNascimento = nascimento;
                    alterados.Add("idade");
                }

                if (Pais(raiz) is { } pais)
                {
                    var nac = await ApiFootballService.ResolverOuCriarNacionalidadePublicAsync(
                        _context, pais, ct);
                    if (nac != null && jogador.NacionalidadeId != nac.Id)
                    {
                        jogador.NacionalidadeId = nac.Id;
                        alterados.Add("nacionalidade");
                    }
                }

                var fotoFotMob = $"/MediaProxy/FotoJogador/{idFotMob}";
                if (jogador.FotoUrl != fotoFotMob && await _fotmob.TemFotoJogadorAsync(idFotMob, ct))
                {
                    jogador.FotoUrl = fotoFotMob;
                    alterados.Add("foto");
                }

                if (alterados.Count == 0) return alterados;

                jogador.DtAlt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[FotMobCadastro] {Nome} (id {Id}): sincronizado {Campos} pelo FotMob {IdFotMob}.",
                    jogador.Nome, jogador.Id, string.Join(", ", alterados), idFotMob);

                return alterados;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "[FotMobCadastro] Não foi possível sincronizar o jogador {Id}.", jogadorId);
                return null;
            }
        }

        /// <summary>
        /// Diz se a foto que está no cadastro é, na verdade, a silhueta cinza que a
        /// api-sports devolve para quem ela não tem retrato.
        ///
        /// Existe porque "tem FotoUrl" não é o mesmo que "tem foto": a api-sports
        /// responde 200 com a mesma imagem genérica para todo jogador sem retrato, e o
        /// cadastro guarda essa URL como se fosse o rosto dele. Sem esta conferência,
        /// justamente os jogadores que precisam da foto do FotMob — os que a
        /// api-football mal conhece — seriam os únicos a nunca recebê-la.
        ///
        /// O reconhecimento é pelo conteúdo (SHA-256 dos bytes), porque a silhueta não
        /// tem URL própria: ela vem no endereço do jogador. Se um dia a api-sports
        /// trocar a imagem, o hash deixa de bater e a resposta vira "é foto de verdade"
        /// — o cadastro fica como está, que é a falha certa para se ter aqui.
        /// </summary>
        private async Task<bool> EhSilhuetaAsync(string fotoUrl, CancellationToken ct)
        {
            // Só a api-sports serve silhueta no lugar da foto. Foto posta à mão, do
            // FotMob ou de qualquer outro lugar é tratada como boa.
            if (!Uri.TryCreate(fotoUrl, UriKind.Absolute, out var uri) ||
                !uri.Host.EndsWith("api-sports.io", StringComparison.OrdinalIgnoreCase))
                return false;

            var chave = $"foto-silhueta:{fotoUrl}";
            if (_cache.TryGetValue<bool>(chave, out var conhecido)) return conhecido;

            bool silhueta;
            try
            {
                var http = _http.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                var bytes = await http.GetByteArrayAsync(fotoUrl, ct);
                silhueta = Convert.ToHexString(SHA256.HashData(bytes))
                    .Equals(SilhuetaApiSports, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Não deu para conferir: mantém a foto que está lá. Guardar "não é
                // silhueta" aqui seria transformar uma instabilidade de rede numa
                // decisão de um dia, então esta resposta não vai para o cache.
                _logger.LogWarning(ex, "[FotMobCadastro] Falha ao conferir a foto {Url}.", fotoUrl);
                return false;
            }

            _cache.Set(chave, silhueta, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
            });

            return silhueta;
        }

        /// <summary>
        /// SHA-256 da silhueta cinza da api-sports (media.api-sports.io, 5.192 bytes) —
        /// a mesma imagem para todo jogador sem retrato lá.
        /// </summary>
        private const string SilhuetaApiSports =
            "2FF7D52A628FCE5D954C58480DDE4E47396DB4BB405B7B7D6A6567134BF86422";

        // ── Leitura do perfil ─────────────────────────────────────────────────

        /// <summary>Nome do jogador no perfil ("Iago Machado"), ou nulo se não veio.</summary>
        private static string? Nome(JsonElement raiz)
        {
            if (raiz.ValueKind != JsonValueKind.Object ||
                !raiz.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) return null;

            var nome = n.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(nome) ? null : nome;
        }

        /// <summary>
        /// Altura em centímetros. Vem em playerInformation, a mesma lista de onde o
        /// perfil já tira o valor de mercado — cada item se identifica pela
        /// translationKey, que não muda com o idioma da resposta.
        /// </summary>
        private static int? Altura(JsonElement raiz)
        {
            if (Valor(raiz, "height_sentencecase") is not { } v) return null;

            // "numberValue": 175 é o campo bom; o fallback é texto ("175 cm").
            if (v.TryGetProperty("numberValue", out var n) && n.ValueKind == JsonValueKind.Number &&
                n.TryGetInt32(out var cm) && cm is > 100 and < 250)
                return cm;

            return null;
        }

        /// <summary>
        /// Data de nascimento. A idade não é lida: o cadastro guarda o nascimento e
        /// calcula a idade sozinho (Jogador.Idade), então gravar a idade de hoje seria
        /// gravar um número que envelhece errado.
        ///
        /// Vem como instante UTC; a coluna é "timestamp without time zone", como no
        /// caminho da api-football, por isso o Kind é zerado antes de gravar.
        /// </summary>
        private static DateTime? Nascimento(JsonElement raiz)
        {
            if (raiz.ValueKind != JsonValueKind.Object ||
                !raiz.TryGetProperty("birthDate", out var bloco) ||
                bloco.ValueKind != JsonValueKind.Object ||
                !bloco.TryGetProperty("utcTime", out var iso) ||
                iso.ValueKind != JsonValueKind.String) return null;

            if (!DateTime.TryParse(iso.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal, out var data)) return null;

            return data.Year > 1900
                ? DateTime.SpecifyKind(data.Date, DateTimeKind.Unspecified)
                : null;
        }

        /// <summary>
        /// País, em inglês ("Spain") — quem traduz e evita duplicar "Brazil"/"Brasil" é
        /// o mesmo ResolverOuCriarNacionalidade usado pela importação.
        /// </summary>
        private static string? Pais(JsonElement raiz)
        {
            if (Valor(raiz, "country_sentencecase") is not { } v) return null;

            return v.TryGetProperty("fallback", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString() : null;
        }

        /// <summary>
        /// O bloco "value" do item de playerInformation com aquela translationKey.
        ///
        /// Devolve nulo em vez de lançar quando a lista não veio: jogador sem ficha
        /// completa é comum na fonte, e é justamente esse jogador que chega aqui.
        /// </summary>
        private static JsonElement? Valor(JsonElement raiz, string chave)
        {
            if (raiz.ValueKind != JsonValueKind.Object ||
                !raiz.TryGetProperty("playerInformation", out var info) ||
                info.ValueKind != JsonValueKind.Array) return null;

            foreach (var item in info.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("translationKey", out var k) ||
                    k.ValueKind != JsonValueKind.String || k.GetString() != chave) continue;

                if (item.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object)
                    return v;
            }

            return null;
        }
    }
}
