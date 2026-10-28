using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Carrega o grupo de comparação da aba Similares do Scout e devolve o JSON da
    /// tela. A conta em si é de <see cref="SimilaridadeJogadorHelper"/>; aqui fica só
    /// o recorte (temporada, competição, função, minutos) e a formatação.
    ///
    /// EstatisticaJogador é global, então o resultado não depende do usuário — a nota
    /// fica de fora de propósito: ela muda com os critérios de cada um e o "parecido"
    /// tem que ser o mesmo para todo mundo.
    /// </summary>
    public class SimilaridadeJogadorService
    {
        private readonly FutebolContext _context;

        public SimilaridadeJogadorService(FutebolContext context)
        {
            _context = context;
        }

        /// <summary>Minutos mínimos para alguém entrar no grupo de comparação (5 jogos).</summary>
        public const int MinMinutosPadrao = 450;

        /// <param name="temporada">Null = a temporada mais recente do jogador; 0 = todas.</param>
        public async Task<object?> SimilaresAsync(
            int jogadorId, int? temporada, int? competicaoId, int? idadeMax, int? minMinutos,
            Func<string, string?> imagemUrl, CancellationToken ct = default)
        {
            var alvo = await _context.Jogadores.AsNoTracking()
                .Include(j => j.Time)
                .FirstOrDefaultAsync(j => j.Id == jogadorId, ct);
            if (alvo == null) return null;

            if (temporada == null)
                temporada = await _context.EstatisticasJogador
                    .Where(e => e.JogadorId == jogadorId && e.Minutos > 0)
                    .MaxAsync(e => (int?)e.Jogo.Temporada, ct);
            var todas = temporada is null or 0;

            var funcao = PerfilJogadorService.RolesJogador(alvo.Posicao)[0];
            var metricas = SimilaridadeJogadorHelper.MetricasDaFuncao(funcao);
            var piso = Math.Max(SimilaridadeJogadorHelper.MinMinutosMetrica, minMinutos ?? MinMinutosPadrao);

            var query = _context.EstatisticasJogador.AsNoTracking().Where(e => e.Minutos > 0);
            if (!todas) query = query.Where(e => e.Jogo.Temporada == temporada);
            if (competicaoId is > 0) query = query.Where(e => e.Jogo.CompeticaoId == competicaoId);

            // Corte de minutos no banco: quem não chega ao piso nem é carregado. O
            // alvo entra de qualquer jeito — o aviso de amostra curta é dele.
            var elegiveis = await query
                .GroupBy(e => e.JogadorId)
                .Where(g => g.Sum(e => e.Minutos!.Value) >= piso || g.Key == jogadorId)
                .Select(g => g.Key)
                .ToListAsync(ct);

            var cadastros = await _context.Jogadores.AsNoTracking()
                .Include(j => j.Time)
                .Where(j => elegiveis.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, ct);

            // Mesmo grupo de função do alvo ("Lateral Direito/Zagueiro" serve para LAT
            // e para ZAG). Idade só filtra candidatos, nunca o alvo.
            var grupo = cadastros.Values
                .Where(j => j.Id == jogadorId || PerfilJogadorService.RolesJogador(j.Posicao).Contains(funcao))
                .Select(j => j.Id)
                .ToHashSet();

            var linhas = await query.Where(e => grupo.Contains(e.JogadorId)).ToListAsync(ct);
            var porJogador = linhas.GroupBy(e => e.JogadorId).ToDictionary(g => g.Key, g => g.ToList());

            var perfis = porJogador.ToDictionary(
                kv => kv.Key, kv => SimilaridadeJogadorHelper.Perfil(kv.Value, metricas));
            var percentis = SimilaridadeJogadorHelper.Percentis(perfis, metricas);

            // Filtro de idade depois dos percentis: o grupo de referência não muda
            // conforme quem o usuário quer ver no resultado.
            var candidatos = idadeMax is > 0
                ? percentis.Where(kv => kv.Key == jogadorId
                        || (cadastros[kv.Key].Idade > 0 && cadastros[kv.Key].Idade <= idadeMax))
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
                : percentis;
            var similares = SimilaridadeJogadorHelper.Ranquear(jogadorId, candidatos);

            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            var rotulos = metricas.ToDictionary(m => m.Id, m => m.Rotulo);
            var minutosAlvo = porJogador.TryGetValue(jogadorId, out var la) ? la.Sum(e => e.Minutos!.Value) : 0;
            var perfilAlvo = perfis.GetValueOrDefault(jogadorId) ?? new();
            var percAlvo = percentis.GetValueOrDefault(jogadorId) ?? new();

            string Valor(SimilaridadeJogadorHelper.Metrica m, double v) =>
                m.EhTaxa ? v.ToString("0", ptBr) + "%" : v.ToString("0.00", ptBr);

            object Cabecalho(Jogador j, int minutos) => new
            {
                id = j.Id,
                nome = j.Nome,
                foto = string.IsNullOrEmpty(j.FotoUrl) ? null : imagemUrl(j.FotoUrl),
                posicao = j.Posicao,
                sigla = PosicaoJogadorHelper.Sigla(j.Posicao),
                idade = j.Idade > 0 ? j.Idade : (int?)null,
                clube = j.Time?.Nome,
                escudo = string.IsNullOrEmpty(j.Time?.EscudoUrl) ? null : imagemUrl(j.Time.EscudoUrl),
                minutos,
            };

            string? aviso = null;
            if (perfilAlvo.Count == 0)
                aviso = minutosAlvo == 0
                    ? "O jogador não tem estatística importada nesse recorte."
                    : $"Só {minutosAlvo} minutos com estatística nesse recorte — pouco para montar o perfil.";
            else if (minutosAlvo < piso)
                aviso = $"Amostra curta: {minutosAlvo} minutos. O perfil pode mudar bastante com mais jogos.";
            else if (similares.Count == 0)
                aviso = "Ninguém no grupo divide métricas suficientes com o jogador. Tente outra temporada ou tire o filtro de competição.";

            return new
            {
                alvo = Cabecalho(alvo, minutosAlvo),
                funcao = PerfilJogadorService.RoleInfo(funcao).Nome,
                temporada = todas ? null : temporada,
                grupo = percentis.Count - (percentis.ContainsKey(jogadorId) ? 1 : 0),
                minMinutos = piso,
                aviso,
                metricas = metricas
                    .Where(m => perfilAlvo.ContainsKey(m.Id))
                    .Select(m => new
                    {
                        id = m.Id,
                        rotulo = m.Rotulo,
                        valor = Valor(m, perfilAlvo[m.Id]),
                        percentil = (int)Math.Round(percAlvo[m.Id]),
                    }).ToList(),
                similares = similares.Select(s =>
                {
                    var j = cadastros[s.JogadorId];
                    var perfil = perfis[s.JogadorId];
                    return new
                    {
                        jogador = Cabecalho(j, porJogador[s.JogadorId].Sum(e => e.Minutos!.Value)),
                        similaridade = s.Similaridade,
                        similaridadeFmt = s.Similaridade.ToString("0", ptBr) + "%",
                        comparadas = s.Diferencas.Count,
                        parecidos = s.Diferencas.Take(3).Select(d => rotulos[d.Id]).ToList(),
                        // Só diferença que separa de verdade (20 pontos de percentil);
                        // abaixo disso "mais desarmes" seria ruído da amostra.
                        diferencas = s.Diferencas.AsEnumerable().Reverse()
                            .Where(d => Math.Abs(d.Delta) >= 20)
                            .Take(2)
                            .Select(d => new { rotulo = rotulos[d.Id], mais = d.Delta > 0 })
                            .ToList(),
                        percentis = s.Diferencas.ToDictionary(d => d.Id, d => (int)Math.Round(percAlvo[d.Id] + d.Delta)),
                        valores = metricas.Where(m => perfil.ContainsKey(m.Id))
                            .ToDictionary(m => m.Id, m => Valor(m, perfil[m.Id])),
                    };
                }).ToList(),
            };
        }
    }
}
