using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Serviço em background que busca periodicamente jogadores com data de nascimento
    /// inválida (-infinity / MinValue) e tenta atualizar via Transfermarkt.
    /// </summary>
    public class AtualizarJogadoresSemDataService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AtualizarJogadoresSemDataService> _logger;

        // Intervalo entre cada ciclo completo (padrão: 6 horas)
        private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromHours(6);

        // Intervalo entre cada jogador dentro do ciclo (evita bloqueio do Transfermarkt)
        private static readonly TimeSpan IntervaloEntreJogadores = TimeSpan.FromSeconds(3);

        public AtualizarJogadoresSemDataService(
            IServiceProvider serviceProvider,
            ILogger<AtualizarJogadoresSemDataService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[AtualizarJogadores] Serviço iniciado.");

            // Aguarda 30 segundos após o start da aplicação antes do primeiro ciclo
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutarCiclo(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro inesperado no ciclo.");
                }

                _logger.LogInformation(
                    "[AtualizarJogadores] Próximo ciclo em {Horas}h.",
                    IntervaloEntreCiclos.TotalHours);

                await Task.Delay(IntervaloEntreCiclos, stoppingToken);
            }

            _logger.LogInformation("[AtualizarJogadores] Serviço encerrado.");
        }

        private async Task ExecutarCiclo(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FutebolContext>();
            var transfermarkt = scope.ServiceProvider.GetRequiredService<TransfermarktService>();

            await SincronizarCompeticoesTimesJogosEElencos(context, transfermarkt, ct);

            var jogadores = await context.Jogadores
                .Include(j => j.Time)
                .Include(j => j.Nacionalidade)
                .Where(j => !string.IsNullOrEmpty(j.linktransfermarket) && !j.Atualizado)
                .OrderBy(j => j.Id)
                .ToListAsync(ct);

            if (!jogadores.Any())
            {
                _logger.LogInformation("[AtualizarJogadores] Nenhum jogador pendente encontrado.");
                return;
            }

            _logger.LogInformation("[AtualizarJogadores] Iniciando ciclo: {Total} jogadores com link Transfermarkt.", jogadores.Count);

            int atualizados = 0;
            int falhas = 0;

            foreach (var jogador in jogadores)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("[AtualizarJogadores] Verificando jogador: {Nome}", jogador.Nome);

                    var info = await transfermarkt.BuscarJogadorPorLink(jogador.linktransfermarket);

                    if (info == null)
                    {
                        _logger.LogWarning("[AtualizarJogadores] Não foi possível obter dados do Transfermarkt: {Nome}", jogador.Nome);
                        falhas++;
                    }
                    else
                    {
                        bool alterado = false;

                        // Atualiza data de nascimento
                        if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year > 1900 &&
                            info.DataNascimento.Value.Date != jogador.DataNascimento.Date)
                        {
                            
                            jogador.DataNascimento = DateTime.SpecifyKind(info.DataNascimento.Value, DateTimeKind.Utc);

                            alterado = true;
                        }

                        // Atualiza nacionalidade
                        if (!string.IsNullOrWhiteSpace(info.Nacionalidade))
                        {
                            var nacionalidade = await context.Nacionalidades
                                .FirstOrDefaultAsync(n => n.Nome.ToLower() == info.Nacionalidade.ToLower(), ct);

                            if (nacionalidade == null)
                            {
                                nacionalidade = new Nacionalidade { Nome = info.Nacionalidade };
                                context.Nacionalidades.Add(nacionalidade);
                                await context.SaveChangesAsync(ct);
                                _logger.LogInformation("[AtualizarJogadores] Nova nacionalidade criada: {Nac}", info.Nacionalidade);
                            }

                            if (jogador.NacionalidadeId != nacionalidade.Id)
                            {
                                jogador.NacionalidadeId = nacionalidade.Id;
                                alterado = true;
                            }
                        }

                        // Atualiza foto
                        var fotoUrl = await transfermarkt.BuscarFotoJogador(jogador);
                        if (!string.IsNullOrEmpty(fotoUrl) && fotoUrl != jogador.FotoUrl)
                        {
                            jogador.FotoUrl = fotoUrl;
                            alterado = true;
                        }

                        // Marca como atualizado
                        jogador.Atualizado = true;
                        jogador.DtAlt = DateTime.UtcNow;

                        if (alterado)
                        {
                            await context.SaveChangesAsync(ct);
                            atualizados++;
                            _logger.LogInformation("[AtualizarJogadores] ✅ Atualizado: {Nome}", jogador.Nome);
                        }
                        else
                        {
                            await context.SaveChangesAsync(ct); // mesmo sem alteração, marca Atualizado=true
                            _logger.LogInformation("[AtualizarJogadores] ⚠️ Nenhuma alteração necessária: {Nome}", jogador.Nome);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AtualizarJogadores] Erro ao processar jogador {Nome}", jogador.Nome);
                    falhas++;
                }

                await Task.Delay(IntervaloEntreJogadores, ct);
            }

            _logger.LogInformation("[AtualizarJogadores] Ciclo concluído. Atualizados: {Ok} | Falhas: {Fail}", atualizados, falhas);
        }

        private async Task SincronizarCompeticoesTimesJogosEElencos(
            FutebolContext context,
            TransfermarktService transfermarkt,
            CancellationToken ct)
        {
            var cicloId = Guid.NewGuid();
            RegistrarLog(context, cicloId, "Ciclo", "Iniciado", detalhes: "Sincronização Transfermarkt iniciada.");

            var competicoes = await context.Competicoes
                .Where(c => !string.IsNullOrWhiteSpace(c.linktransfermarket))
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            foreach (var competicao in competicoes)
            {
                if (ct.IsCancellationRequested) break;

                _logger.LogInformation("[TransfermarktSync] Verificando competição: {Nome}", competicao.Nome);
                RegistrarLog(context, cicloId, "Competicao", "Verificando", competicaoNome: competicao.Nome);

                var jogosWeb = await transfermarkt.BuscarJogosCompeticaoPorLink(competicao.linktransfermarket!, ct);
                _logger.LogInformation("[TransfermarktSync] {Total} jogos encontrados para {Nome}.", jogosWeb.Count, competicao.Nome);
                RegistrarLog(
                    context,
                    cicloId,
                    "Competicao",
                    "JogosEncontrados",
                    competicaoNome: competicao.Nome,
                    detalhes: $"{jogosWeb.Count} jogo(s) encontrado(s) no Transfermarkt.");

                foreach (var jogoWeb in jogosWeb)
                {
                    var timeCasa = await ResolverOuCriarTime(context, transfermarkt, jogoWeb.NomeTimeCasa, jogoWeb.LinkTimeCasa, cicloId, ct);
                    var timeVisitante = await ResolverOuCriarTime(context, transfermarkt, jogoWeb.NomeTimeVisitante, jogoWeb.LinkTimeVisitante, cicloId, ct);

                    await IncluirOuAtualizarJogo(context, competicao, jogoWeb, timeCasa, timeVisitante, cicloId, ct);
                    await context.SaveChangesAsync(ct);

                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }

            var times = await context.Times
                .Where(t => !string.IsNullOrWhiteSpace(t.linktransfermarket))
                .OrderBy(t => t.Id)
                .ToListAsync(ct);

            foreach (var time in times)
            {
                if (ct.IsCancellationRequested) break;

                await SincronizarElencoTime(context, transfermarkt, time, cicloId, ct);
                await context.SaveChangesAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }

            RegistrarLog(context, cicloId, "Ciclo", "Concluido", detalhes: "Sincronização Transfermarkt concluída.");
            await context.SaveChangesAsync(ct);
        }

        private async Task<Time> ResolverOuCriarTime(
    FutebolContext context,
    TransfermarktService transfermarkt,
    string nome,
    string? linkTransfermarkt,
    Guid cicloId,
    CancellationToken ct)
        {
            var nomeNormalizado = NormalizarTexto(nome);
            var urlNormalizada = !string.IsNullOrWhiteSpace(linkTransfermarkt)
                ? transfermarkt.NormalizarLinkTransfermarkt(linkTransfermarkt)
                : null;

            // 1️⃣ Carrega todos os times em memória para comparar com NormalizarTexto
            var timesBanco = await context.Times.ToListAsync(ct);
            var time = timesBanco.FirstOrDefault(t => NormalizarTexto(t.Nome) == nomeNormalizado);

            // 2️⃣ Se não achou pelo nome, tenta achar pelo link
            if (time == null && !string.IsNullOrWhiteSpace(urlNormalizada))
            {
                time = await context.Times.FirstOrDefaultAsync(t => t.linktransfermarket == urlNormalizada, ct);
                if (time != null)
                {
                    _logger.LogInformation("[TransfermarktSync] Time encontrado pelo link: {Nome}", time.Nome);
                    return time;
                }
            }

            // 3️⃣ Se achou pelo nome mas a URL diverge
            if (time != null && !string.IsNullOrWhiteSpace(urlNormalizada))
            {
                if (time.linktransfermarket != urlNormalizada)
                {
                    _logger.LogWarning("[TransfermarktSync] Divergência de URL para {Nome}: banco={Banco}, web={Web}",
                        time.Nome, time.linktransfermarket, urlNormalizada);
                }
                return time;
            }

            // 4️⃣ Se não achou nada, cria novo
            if (time == null)
            {
                time = new Time
                {
                    Nome = nome,
                    Cidade = string.Empty,
                    linktransfermarket = urlNormalizada,
                    FormacaoPadraoId = await ObterFormacaoPadraoId(context, ct)
                };

                // 🔹 Só adiciona se realmente não existe
                await context.Times.AddAsync(time, ct);
                _logger.LogInformation("[TransfermarktSync] Time criado: {Nome}", nome);
            }

            return time;
        }




        private async Task IncluirOuAtualizarJogo(
        FutebolContext context,
        Competicao competicao,
        TransfermarktJogoInfo jogoWeb,
        Time timeCasa,
        Time timeVisitante,
        Guid cicloId,
        CancellationToken ct)
        {
            var jogosBanco = await context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id &&
                            j.TimeCasaId == timeCasa.Id &&
                            j.TimeVisitanteId == timeVisitante.Id)
                .ToListAsync(ct);

            var jogo = jogoWeb.Data.HasValue
            ? jogosBanco.FirstOrDefault(j => j.Data.HasValue &&
                Math.Abs((j.Data.Value.Date - jogoWeb.Data.Value.Date).TotalDays) <= 2)
            : jogosBanco.FirstOrDefault();

            // 🔹 Buscar/criar formação da casa
            var formacaoCasa = await ObterOuCriarFormacao(context, jogoWeb.FormacaoCasa, ct);
            var formacaoVisitante = await ObterOuCriarFormacao(context, jogoWeb.FormacaoVisitante, ct);

            if (jogo == null)
            {
                jogo = new Jogo
                {
                    CompeticaoId = competicao.Id,
                    TimeCasa = timeCasa,
                    TimeVisitante = timeVisitante,
                    Data = jogoWeb.Data.HasValue
                        ? DateTime.SpecifyKind(jogoWeb.Data.Value, DateTimeKind.Utc)
                        : DateTime.UtcNow,
                    Rodada = jogoWeb.Rodada,
                    PlacarCasa = jogoWeb.PlacarCasa,
                    PlacarVisitante = jogoWeb.PlacarVisitante,
                    Grupo = jogoWeb.Grupo,
                    Status = jogoWeb.PlacarCasa.HasValue ? "Finalizado" : "Agendado",
                    Atualizado = jogoWeb.PlacarCasa.HasValue ? 1 : 0,
                    FormacaoCasaId = formacaoCasa.Id,
                    FormacaoVisitanteId = formacaoVisitante.Id
                };

                context.Jogos.Add(jogo);

                // 🔹 Escalações com posições reais
                AdicionarEscalacaoComPosicoes(context, jogo, formacaoCasa, true);
                AdicionarEscalacaoComPosicoes(context, jogo, formacaoVisitante, false);

                // 🔹 Eventos do jogo
                foreach (var evento in jogoWeb.Eventos)
                {
                    if (evento.Tipo == "Gol")
                    {
                        context.Gols.Add(new Gol
                        {
                            JogoId = jogo.Id,
                            JogadorId = evento.JogadorId,
                            Minuto = evento.Minuto,
                            Contra = evento.Contra
                        });

                        // Se houver assistência vinculada ao gol
                        if (evento.AssistenteId.HasValue)
                        {
                            context.Assistencias.Add(new Assistencia
                            {
                                JogoId = jogo.Id,
                                JogadorId = evento.AssistenteId.Value,
                                Minuto = evento.Minuto
                            });
                        }
                    }
                    else if (evento.Tipo == "Assistencia")
                    {
                        // Caso venha como evento separado
                        context.Assistencias.Add(new Assistencia
                        {
                            JogoId = jogo.Id,
                            JogadorId = evento.JogadorId,
                            Minuto = evento.Minuto
                        });
                    }
                    else if (evento.Tipo == "Cartao")
                    {
                        context.Cartoes.Add(new Cartao
                        {
                            JogoId = jogo.Id,
                            JogadorId = evento.JogadorId,
                            Tipo = evento.Detalhe,
                            Minuto = evento.Minuto
                        });
                    }
                }


                _logger.LogInformation("[TransfermarktSync] Jogo incluído com formações e eventos: {Casa} x {Visitante}", timeCasa.Nome, timeVisitante.Nome);
                return;
            }

            // 🔹 Atualização de placar
            var alterouPlacar =
                jogoWeb.PlacarCasa.HasValue &&
                (jogo.PlacarCasa != jogoWeb.PlacarCasa || jogo.PlacarVisitante != jogoWeb.PlacarVisitante);

            if (alterouPlacar)
            {
                jogo.PlacarCasa = jogoWeb.PlacarCasa;
                jogo.PlacarVisitante = jogoWeb.PlacarVisitante;
                jogo.Status = "Finalizado";
                jogo.Atualizado = 1;

                _logger.LogInformation("[TransfermarktSync] Placar atualizado: {Casa} {PC}-{PV} {Visitante}",
                    timeCasa.Nome, jogo.PlacarCasa, jogo.PlacarVisitante, timeVisitante.Nome);
            }
        }

        private async Task<Formacao> ObterOuCriarFormacao(FutebolContext context, string? nomeFormacao, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeFormacao))
                nomeFormacao = "4-3-3"; // fallback

            var formacao = await context.Formacoes
                .Include(f => f.Posicoes)
                .FirstOrDefaultAsync(f => f.Nome == nomeFormacao, ct);

            if (formacao == null)
            {
                formacao = new Formacao { Nome = nomeFormacao, Posicoes = new List<PosicaoFormacao>() };
                context.Formacoes.Add(formacao);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[TransfermarktSync] Nova formação criada: {Formacao}", nomeFormacao);
            }

            return formacao;
        }

        private void AdicionarEscalacaoComPosicoes(FutebolContext context, Jogo jogo, Formacao formacao, bool isTimeCasa)
        {
            var posicoes = formacao.Posicoes.OrderBy(p => p.Ordem).ToList();

            foreach (var pos in posicoes)
            {
                context.Escalacoes.Add(new Escalacao
                {
                    Jogo = jogo,
                    IsTimeCasa = isTimeCasa,
                    Titular = true,
                    Posicao = pos.NomePosicao,
                    PosicaoX = pos.PosicaoX,
                    PosicaoY = pos.PosicaoY,
                    FaseEscalacao = "INICIAL"
                });
            }
        }


        private async Task SincronizarElencoTime(
            FutebolContext context,
            TransfermarktService transfermarkt,
            Time time,
            Guid cicloId,
            CancellationToken ct)
        {
            var elencoWeb = await transfermarkt.BuscarElencoTimePorLink(time.linktransfermarket!, ct);
            if (!elencoWeb.Any()) return;

            var jogadoresBanco = await context.Jogadores
                .Where(j => j.TimeId == time.Id)
                .ToListAsync(ct);

            foreach (var jogadorWeb in elencoWeb)
            {
                if (string.IsNullOrWhiteSpace(jogadorWeb.NomeCompleto)) continue;

                var nomeNorm = NormalizarTexto(jogadorWeb.NomeCompleto);
                var jogador = jogadoresBanco.FirstOrDefault(j =>
                    NormalizarTexto(j.Nome) == nomeNorm ||
                    NormalizarTexto(j.Nome).Contains(nomeNorm) ||
                    nomeNorm.Contains(NormalizarTexto(j.Nome)));

                if (jogador != null)
                {
                    if (string.IsNullOrWhiteSpace(jogador.linktransfermarket) &&
                        !string.IsNullOrWhiteSpace(jogadorWeb.LinkPerfil))
                    {
                        jogador.linktransfermarket = jogadorWeb.LinkPerfil;
                        jogador.DtAlt = DateTime.UtcNow;
                        RegistrarLog(
                            context,
                            cicloId,
                            "Elenco",
                            "LinkJogadorAtualizado",
                            timeNome: time.Nome,
                            detalhes: $"Link salvo para {jogador.Nome}: {jogador.linktransfermarket}");
                    }

                    continue;
                }

                jogador = new Jogador
                {
                    Nome = jogadorWeb.NomeCompleto,
                    Posicao = jogadorWeb.Posicao ?? "Meio",
                    TimeId = time.Id,
                    NumeroCamisa = jogadorWeb.NumeroCamisa,
                    linktransfermarket = jogadorWeb.LinkPerfil,
                    DataNascimento = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),

                    DtInc = DateTime.UtcNow,
                    Atualizado = false
                };

                context.Jogadores.Add(jogador);
                jogadoresBanco.Add(jogador);
                _logger.LogInformation("[TransfermarktSync] Jogador incluído no elenco de {Time}: {Jogador}", time.Nome, jogador.Nome);
                RegistrarLog(
                    context,
                    cicloId,
                    "Elenco",
                    "JogadorCriado",
                    timeNome: time.Nome,
                    detalhes: $"Jogador incluído: {jogador.Nome}");
            }
        }

        private static void RegistrarLog(
            FutebolContext context,
            Guid cicloId,
            string tipo,
            string acao,
            string? competicaoNome = null,
            string? timeNome = null,
            string? jogoDescricao = null,
            string? detalhes = null)
        {
            context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId = cicloId,
                Data = DateTime.UtcNow,
                Tipo = tipo,
                Acao = acao,
                CompeticaoNome = competicaoNome,
                TimeNome = timeNome,
                JogoDescricao = jogoDescricao,
                Detalhes = detalhes
            });
        }

        private static string MontarDescricaoJogo(Time timeCasa, Time timeVisitante, TransfermarktJogoInfo jogoWeb)
        {
            var placar = jogoWeb.PlacarCasa.HasValue
                ? $" {jogoWeb.PlacarCasa} x {jogoWeb.PlacarVisitante}"
                : string.Empty;

            var data = jogoWeb.Data.HasValue
                ? $" em {jogoWeb.Data.Value:dd/MM/yyyy}"
                : string.Empty;

            return $"{timeCasa.Nome}{placar} {timeVisitante.Nome}{data}";
        }

        private static async Task<int> ObterFormacaoPadraoId(FutebolContext context, CancellationToken ct)
        {
            var formacao = await context.Formacoes.OrderBy(f => f.Id).FirstOrDefaultAsync(ct);
            return formacao?.Id ?? 1;
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var s = texto.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("õ", "o").Replace("ê", "e").Replace("â", "a")
                .Replace("ô", "o").Replace("ç", "c").Replace("ñ", "n");

            return Regex.Replace(s, @"\b(cr|fc|sc|ec|ac|se|cf|cd|club|clube|futebol|football|de|do|da|dos|das)\b|\s+", "");
        }

    }
}
