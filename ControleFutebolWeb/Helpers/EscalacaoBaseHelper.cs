using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Helpers
{
    // Monta a escalação de partida de um time quando não há (ou não veio) importação:
    // 1) última escalação real do time em jogos anteriores, 2) escalação padrão do time,
    // 3) slots vazios de uma formação. Usado pelo fallback automático da tela Analisar e
    // pelo botão "Última escalação" (JogosController.AplicarUltimaEscalacao).
    public static class EscalacaoBaseHelper
    {
        public enum Origem
        {
            UltimoJogo,
            PadraoDoTime,
            FormacaoVazia,
            Nenhuma
        }

        public class Resultado
        {
            public Origem Origem { get; init; }
            public List<Escalacao> Escalacoes { get; init; } = new();
            public int FormacaoId { get; init; }
            public Jogo? JogoOrigem { get; init; }

            // Texto curto pra mostrar ao usuário de onde veio a escalação.
            public string Descrever(string nomeTime) => Origem switch
            {
                Origem.UltimoJogo => $"{nomeTime}: escalação do último jogo{DescreverJogoOrigem()}.",
                Origem.PadraoDoTime => $"{nomeTime}: sem jogo anterior com escalação — usada a escalação padrão do time.",
                Origem.FormacaoVazia => $"{nomeTime}: sem jogo anterior nem escalação padrão — campo montado com a formação vazia (arraste os jogadores).",
                _ => $"{nomeTime}: não foi possível montar a escalação (cadastre uma formação padrão para o time)."
            };

            private string DescreverJogoOrigem()
            {
                if (JogoOrigem == null) return string.Empty;
                var adversario = JogoOrigem.TimeCasa?.Nome != null && JogoOrigem.TimeVisitante?.Nome != null
                    ? $" {JogoOrigem.TimeCasa.Nome} x {JogoOrigem.TimeVisitante.Nome}"
                    : string.Empty;
                var data = JogoOrigem.Data.HasValue ? $" em {JogoOrigem.Data.Value:dd/MM/yyyy}" : string.Empty;
                return $"{adversario}{data}";
            }
        }

        // Quantos jogos anteriores do time são varridos em busca de uma escalação
        // preenchida. O fallback antigo olhava só o jogo mais recente — se aquele
        // ainda não tinha escalação importada, caía direto nos slots vazios.
        private const int MaxJogosAnteriores = 30;

        public static async Task<Resultado> MontarAsync(
            FutebolContext ctx,
            Jogo jogo,
            bool paraTimeCasa,
            string? usuarioId,
            string fase,
            int formacaoPreferida)
        {
            var timeId = paraTimeCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;

            // 1) Última escalação real do time em jogos anteriores.
            var doUltimoJogo = await MontarDoUltimoJogoAsync(ctx, jogo, timeId, paraTimeCasa, usuarioId, fase);
            if (doUltimoJogo != null) return doUltimoJogo;

            // 2) Escalação padrão cadastrada no time.
            var padrao = await ctx.TimeEscalacaoPadrao
                .AsNoTracking()
                .Where(t => t.TimeId == timeId)
                .ToListAsync();

            if (padrao.Count > 0)
            {
                return new Resultado
                {
                    Origem = Origem.PadraoDoTime,
                    FormacaoId = formacaoPreferida > 0 ? formacaoPreferida : padrao[0].FormacaoId,
                    Escalacoes = padrao.Select(p => new Escalacao
                    {
                        JogoId = jogo.Id,
                        JogadorId = p.JogadorId,
                        Posicao = p.Posicao,
                        PosicaoX = p.PosicaoX,
                        PosicaoY = p.PosicaoY,
                        IsTimeCasa = paraTimeCasa,
                        Titular = true,
                        FaseEscalacao = fase,
                        UsuarioId = usuarioId
                    }).ToList()
                };
            }

            // 3) Slots vazios de uma formação.
            return await MontarDaFormacaoAsync(ctx, jogo, paraTimeCasa, usuarioId, fase, formacaoPreferida);
        }

        // Só os slots vazios de uma formação — usado quando o usuário troca a formação
        // na tela Analisar. Sempre resolve alguma formação (a escolhida, a padrão do time
        // ou a primeira cadastrada): sem formação definida o campo ficava sem nenhum slot
        // e não dava pra montar a escalação na mão.
        public static async Task<Resultado> MontarDaFormacaoAsync(
            FutebolContext ctx,
            Jogo jogo,
            bool paraTimeCasa,
            string? usuarioId,
            string fase,
            int formacaoPreferida)
        {
            var timeId = paraTimeCasa ? jogo.TimeCasaId : jogo.TimeVisitanteId;
            var formacaoId = await ResolverFormacaoAsync(ctx, timeId, formacaoPreferida);

            if (formacaoId > 0)
            {
                var posicoes = await ctx.PosicoesFormacao
                    .AsNoTracking()
                    .Where(p => p.FormacaoId == formacaoId)
                    .ToListAsync();

                if (posicoes.Count > 0)
                    return new Resultado
                    {
                        Origem = Origem.FormacaoVazia,
                        FormacaoId = formacaoId,
                        Escalacoes = posicoes.Select(pos => new Escalacao
                        {
                            JogoId = jogo.Id,
                            Posicao = pos.NomePosicao,
                            PosicaoX = pos.PosicaoX,
                            PosicaoY = pos.PosicaoY,
                            IsTimeCasa = paraTimeCasa,
                            Titular = true,
                            FaseEscalacao = fase,
                            UsuarioId = usuarioId
                        }).ToList()
                    };
            }

            return new Resultado { Origem = Origem.Nenhuma, FormacaoId = formacaoPreferida };
        }

        // Reposiciona a escalação ATUAL nos slots de uma nova formação, PRESERVANDO os
        // jogadores — eles só mudam de lugar, não são apagados. Usado quando o usuário
        // troca a formação na tela Analisar (ex.: veio 4-3-3 da última escalação e o jogo
        // será no 4-4-2). Os titulares atuais são distribuídos pelos slots da nova formação
        // na mesma ordem de campo (goleiro mais recuado → atacantes à frente); as reservas
        // são mantidas. Se a nova formação tiver menos slots que titulares atuais, o
        // excedente vai para o banco (nunca some).
        public static async Task<Resultado> RemapearFormacaoAsync(
            FutebolContext ctx,
            Jogo jogo,
            bool paraTimeCasa,
            string? usuarioId,
            string fase,
            int novaFormacaoId,
            List<Escalacao> escalacoesAtuais)
        {
            var slots = await ctx.PosicoesFormacao
                .AsNoTracking()
                .Where(p => p.FormacaoId == novaFormacaoId)
                .OrderBy(p => p.Ordem)   // Ordem 1 = goleiro; cresce até os atacantes
                .ToListAsync();

            if (slots.Count == 0)
                return new Resultado { Origem = Origem.Nenhuma, FormacaoId = novaFormacaoId };

            // Titulares atuais na mesma ordem de campo dos slots (Y do próprio gol para a
            // frente; empata pela lateralidade). Assim o goleiro cai no slot de goleiro, a
            // defesa nos slots de defesa, e assim por diante.
            var titulares = escalacoesAtuais
                .Where(e => e.Titular)
                .OrderByDescending(e => e.PosicaoY)
                .ThenBy(e => e.PosicaoX)
                .ToList();

            var novas = new List<Escalacao>();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var origem = i < titulares.Count ? titulares[i] : null;
                novas.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = origem?.JogadorId,   // slot pode ficar vazio se faltar titular
                    Posicao = slot.NomePosicao,
                    PosicaoX = slot.PosicaoX,
                    PosicaoY = slot.PosicaoY,
                    IsTimeCasa = paraTimeCasa,
                    Titular = true,
                    FaseEscalacao = fase,
                    UsuarioId = usuarioId
                });
            }

            // Reservas atuais + titulares que sobraram (nova formação com menos slots) vão
            // para o banco, para nenhum jogador desaparecer na troca.
            var paraOBanco = escalacoesAtuais
                .Where(e => !e.Titular && e.JogadorId != null)
                .Concat(titulares.Skip(slots.Count).Where(e => e.JogadorId != null));

            foreach (var e in paraOBanco)
                novas.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = e.JogadorId,
                    Posicao = "RES",
                    PosicaoX = 0,
                    PosicaoY = 0,
                    IsTimeCasa = paraTimeCasa,
                    Titular = false,
                    FaseEscalacao = fase,
                    UsuarioId = usuarioId
                });

            return new Resultado
            {
                Origem = Origem.UltimoJogo,   // reaproveitou a escalação atual
                FormacaoId = novaFormacaoId,
                Escalacoes = novas
            };
        }

        private static async Task<Resultado?> MontarDoUltimoJogoAsync(
            FutebolContext ctx, Jogo jogo, int timeId, bool paraTimeCasa, string? usuarioId, string fase)
        {
            var anteriores = await ctx.Jogos
                .AsNoTracking()
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.Id != jogo.Id
                         && (j.TimeCasaId == timeId || j.TimeVisitanteId == timeId)
                         // Jogo sem data não tem como ser ordenado — entra pelo Id.
                         && (jogo.Data == null || j.Data == null || j.Data <= jogo.Data))
                .OrderByDescending(j => j.Data == null)
                .ThenByDescending(j => j.Data)
                .ThenByDescending(j => j.Id)
                .Take(MaxJogosAnteriores)
                .ToListAsync();

            if (anteriores.Count == 0) return null;

            var idsAnteriores = anteriores.Select(j => j.Id).ToList();

            // Só interessam linhas com jogador atribuído: escalação com slots vazios
            // (o caso "a API não trouxe nada") não serve como última escalação.
            var escalacoes = await ctx.Escalacoes
                .AsNoTracking()
                .Where(e => idsAnteriores.Contains(e.JogoId)
                         && e.JogadorId != null
                         && (e.UsuarioId == usuarioId || e.UsuarioId == null)
                         && (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == "FINAL" || e.FaseEscalacao == null))
                .ToListAsync();

            if (escalacoes.Count == 0) return null;

            foreach (var anterior in anteriores)
            {
                bool eraCasa = anterior.TimeCasaId == timeId;

                var doJogo = escalacoes
                    .Where(e => e.JogoId == anterior.Id && e.IsTimeCasa == eraCasa)
                    .ToList();

                if (doJogo.Count == 0) continue;

                // A escalação ajustada pelo usuário ganha da importada (UsuarioId null).
                if (doJogo.Any(e => e.UsuarioId != null))
                    doJogo = doJogo.Where(e => e.UsuarioId != null).ToList();

                // Prefere a INICIAL; só usa a FINAL se o jogo não tiver INICIAL.
                var temInicial = doJogo.Any(e => e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null);
                doJogo = doJogo
                    .Where(e => temInicial
                        ? (e.FaseEscalacao == "INICIAL" || e.FaseEscalacao == null)
                        : e.FaseEscalacao == "FINAL")
                    .ToList();

                if (!doJogo.Any(e => e.Titular)) continue;

                var formacaoOrigem = (eraCasa ? anterior.FormacaoCasaId : anterior.FormacaoVisitanteId) ?? 0;

                return new Resultado
                {
                    Origem = Origem.UltimoJogo,
                    JogoOrigem = anterior,
                    FormacaoId = formacaoOrigem,
                    Escalacoes = doJogo.Select(e => new Escalacao
                    {
                        JogoId = jogo.Id,
                        JogadorId = e.JogadorId,
                        Posicao = e.Posicao,
                        PosicaoX = e.PosicaoX,
                        PosicaoY = e.PosicaoY,
                        IsTimeCasa = paraTimeCasa,
                        Titular = e.Titular,
                        FaseEscalacao = fase,
                        UsuarioId = usuarioId
                    }).ToList()
                };
            }

            return null;
        }

        // Formação a usar quando não há escalação nenhuma: a já escolhida no jogo,
        // senão a padrão do time, senão a primeira cadastrada no sistema.
        private static async Task<int> ResolverFormacaoAsync(FutebolContext ctx, int timeId, int formacaoPreferida)
        {
            if (formacaoPreferida > 0) return formacaoPreferida;

            var doTime = await ctx.Times
                .AsNoTracking()
                .Where(t => t.Id == timeId)
                .Select(t => t.FormacaoPadraoId)
                .FirstOrDefaultAsync();

            if (doTime > 0) return doTime;

            return await ctx.Formacoes
                .AsNoTracking()
                .OrderBy(f => f.Id)
                .Select(f => f.Id)
                .FirstOrDefaultAsync();
        }
    }
}
