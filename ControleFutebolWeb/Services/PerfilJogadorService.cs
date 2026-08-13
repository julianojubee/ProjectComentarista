// ControleFutebolWeb/Services/PerfilJogadorService.cs
// Agregação de estatísticas de temporada de um jogador (jogos, gols, passes,
// duelos, desarmes etc., com a mesma régua de nota manual/automática usada em
// /Jogadores/Estatisticas). Extraído de JogadoresController para ser
// reaproveitado também pela comparação de jogadores em /Relatorios/Scout.
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    // Números agregados de um jogador usados nas telas de comparação.
    public sealed class PerfilJogador
    {
        public Jogador J = null!;
        public string Grupo = "ATA";
        public List<string> Roles = new(); // funções de Jogador.Posicao ("Lateral Direito/Zagueiro" → LAT, ZAG)
        public int JogosTotal;     // escalações distintas ou jogos com estatística (o maior)
        public int JogosStats;     // jogos com estatística importada (denominador das médias)
        public int JogosTitular;   // titular com placar definido (denominador dos jogos sem sofrer gols)
        public int JogosSemSofrerGols;
        public int Gols;
        public int Assistencias;
        public double? NotaMedia;  // mesma régua da tela (nota manual ou base + ações)
        public double? Rating;     // média do rating api-football
        public double MinutosMedio;

        // Somatórios das estatísticas importadas (Minutos > 0)
        public int Finalizacoes, FinNoGol, Passes, PassesChave, PassesCertos;
        public int DriblesTentados, DriblesCertos, DuelosTotal, DuelosVencidos;
        public int Desarmes, Interceptacoes, Bloqueios, Defesas, GolsSofridos;
        public int FaltasSofridas, FaltasCometidas, Cartoes;

        public double PJ(int total) => JogosStats > 0 ? (double)total / JogosStats : 0;
        public double PJTotal(int total) => JogosTotal > 0 ? (double)total / JogosTotal : 0;
        public int Pct(int certos, int total) => total > 0 ? (int)Math.Round(100.0 * certos / total) : 0;
        public double AcoesDefensivasPJ => PJ(Desarmes + Interceptacoes + Bloqueios);
        public int PctCleanSheets => Pct(JogosSemSofrerGols, JogosTitular);
    }

    public class PerfilJogadorService
    {
        private readonly FutebolContext _context;

        public PerfilJogadorService(FutebolContext context)
        {
            _context = context;
        }

        // ── Grupo de posição → define quais métricas comparar ─────────────────
        public static string GrupoPosicao(string? pos)
        {
            if (string.IsNullOrEmpty(pos)) return "ATA";
            bool C(string s) => pos.Contains(s, StringComparison.OrdinalIgnoreCase);
            if (C("Goleiro")) return "GOL";
            if (C("Defensor") || C("Zagueiro") || C("Lateral") || C("Ala")) return "DEF";
            if (C("Meia") || C("Meio") || C("Volante")) return "MEI";
            return "ATA"; // Atacante, Ponta, Centroavante e demais
        }

        // ── Funções (roles) para a comparação por posição ──────────────────────
        // Jogador.Posicao guarda até duas posições granulares separadas por "/".
        public static readonly string[] OrdemRoles = { "GOL", "ZAG", "LAT", "VOL", "MEI", "PON", "ATA" };

        public static (string Nome, string Emoji) RoleInfo(string role) => role switch
        {
            "GOL" => ("goleiro", "🧤"),
            "ZAG" => ("zagueiro", "🛡️"),
            "LAT" => ("lateral/ala", "🏃"),
            "VOL" => ("volante", "⚙️"),
            "MEI" => ("meia armador", "🎯"),
            "PON" => ("ponta", "🌀"),
            _ => ("centroavante", "⚽"),
        };

        private static string? RolePosicao(string parte)
        {
            bool C(string s) => parte.Contains(s, StringComparison.OrdinalIgnoreCase);
            if (C("Goleiro")) return "GOL";
            if (C("Zagueiro") || C("Defensor")) return "ZAG";
            if (C("Lateral") || C("Ala")) return "LAT";
            if (C("Volante")) return "VOL";
            if (C("Ponta")) return "PON";
            if (C("Meia") || C("Meio")) return "MEI";
            if (C("Centroavante") || C("Atacante")) return "ATA";
            return null;
        }

        public static List<string> RolesJogador(string? posicao)
        {
            var roles = (posicao ?? "").Split('/')
                .Select(RolePosicao)
                .Where(r => r != null)
                .Select(r => r!)
                .Distinct()
                .ToList();
            if (roles.Count == 0) roles.Add("ATA"); // mesmo fallback do GrupoPosicao
            return roles;
        }

        public async Task<PerfilJogador?> MontarAsync(int jogadorId, string? uid, List<CriterioNota> criterios, int? temporada = null)
        {
            var j = await _context.Jogadores
                .AsNoTracking()
                .Include(x => x.Time)
                .Include(x => x.Nacionalidade)
                .FirstOrDefaultAsync(x => x.Id == jogadorId);
            if (j == null) return null;

            // Exclui reservas não utilizados (Minutos 0/null), mesmo critério da tela.
            var estatisticas = await _context.EstatisticasJogador
                .AsNoTracking()
                // Jogo e Jogador precisam vir juntos: o bônus "não sofreu gol"
                // (CriteriosNotaHelper) lê o placar da partida e a posição/time do
                // jogador. Sem tracking não há fixup, então sem Include sai errado.
                .Include(e => e.Jogo)
                .Include(e => e.Jogador)
                .Where(e => e.JogadorId == jogadorId && e.Minutos != null && e.Minutos > 0
                         && (!temporada.HasValue || e.Jogo.Temporada == temporada.Value))
                .ToListAsync();

            // Lado (casa/visitante) por jogo, da escalação da época — usado pelo bônus
            // "não sofreu gol"; o time atual erraria os jogos pré-transferência.
            var lados = await LadoJogadorHelper.CarregarAsync(
                _context, estatisticas.Select(e => e.JogoId).Distinct().ToList(), uid, new[] { jogadorId });

            var jogosEscalado = await _context.Escalacoes
                .Where(e => e.JogadorId == jogadorId && (e.UsuarioId == uid || e.UsuarioId == null)
                         && (!temporada.HasValue || e.Jogo.Temporada == temporada.Value))
                .Select(e => e.JogoId)
                .Distinct()
                .CountAsync();

            var gols = await _context.Gols.CountAsync(g => g.JogadorId == jogadorId && !g.Contra
                         && (!temporada.HasValue || g.Jogo.Temporada == temporada.Value));
            var assistencias = await _context.Assistencias.CountAsync(x => x.JogadorId == jogadorId
                         && (!temporada.HasValue || x.Jogo.Temporada == temporada.Value));

            // Jogos como titular com placar definido → "jogos sem sofrer gols"
            var titularidades = await _context.Escalacoes
                .AsNoTracking()
                .Where(e => e.JogadorId == jogadorId && e.Titular
                         && (e.UsuarioId == uid || e.UsuarioId == null)
                         && e.Jogo.PlacarCasa != null && e.Jogo.PlacarVisitante != null
                         && (!temporada.HasValue || e.Jogo.Temporada == temporada.Value))
                .Select(e => new { e.JogoId, e.IsTimeCasa, e.Jogo.PlacarCasa, e.Jogo.PlacarVisitante })
                .Distinct()
                .ToListAsync();

            // Nota média com a mesma régua de /Jogadores/Estatisticas: nota manual
            // quando existe; senão base fixa + ações calculadas sobre a estatística.
            var notas = await _context.Notas
                .AsNoTracking()
                // Detalhes: o piso de merecimento conta os chips verdes x vermelhos.
                .Include(n => n.Detalhes)
                .Where(n => n.JogadorId == jogadorId && n.UsuarioId == uid
                         && (!temporada.HasValue || n.Jogo.Temporada == temporada.Value))
                .ToListAsync();

            var jogosComNotaManual = notas.Select(n => n.JogoId).ToHashSet();

            // Minutos e goleiro decisivo por jogo. Carrega os próprios lados: `lados`
            // acima está filtrado neste jogador e não fecharia as finalizações no alvo
            // do elenco adversário.
            var jogoIdsDoPerfil = notas.Select(n => n.JogoId)
                .Concat(estatisticas.Select(e => e.JogoId)).Distinct().ToList();
            var contextos = await ContextoNotaHelper.CarregarAsync(_context, jogoIdsDoPerfil, uid);

            // Motor da nota automática escolhido em /CriteriosNota — só para os jogos
            // sem avaliação manual.
            var calculadora = await NotaAutomaticaHelper.CarregarAsync(
                _context, jogoIdsDoPerfil, uid, criterios, lados, contextos);

            var notasFinais = notas
                .Select(n => n.NotaManual.HasValue
                    ? Math.Round(Math.Max(0, Math.Min(10, n.NotaManual.Value)), 2)
                    : CriteriosNotaHelper.NotaFinal(n.Valor, criterios,
                        ContextoNotaHelper.De(contextos, jogadorId, n.JogoId) with
                        {
                            Acoes = CriteriosNotaHelper.ContarAcoes(n.Detalhes)
                        }))
                .Concat(estatisticas
                    .Where(e => !jogosComNotaManual.Contains(e.JogoId))
                    .Select(e => calculadora.De(e).Nota))
                .ToList();

            var ratings = estatisticas.Where(e => e.Rating.HasValue).Select(e => e.Rating!.Value).ToList();

            return new PerfilJogador
            {
                J = j,
                Grupo = GrupoPosicao(j.Posicao),
                Roles = RolesJogador(j.Posicao),
                JogosTotal = Math.Max(jogosEscalado, estatisticas.Count),
                JogosStats = estatisticas.Count,
                JogosTitular = titularidades.Count,
                JogosSemSofrerGols = titularidades.Count(t => (t.IsTimeCasa ? t.PlacarVisitante : t.PlacarCasa) == 0),
                Gols = gols,
                Assistencias = assistencias,
                NotaMedia = notasFinais.Count > 0 ? Math.Round(notasFinais.Average(), 2) : null,
                Rating = ratings.Count > 0 ? Math.Round(ratings.Average(), 2) : null,
                MinutosMedio = estatisticas.Count > 0 ? Math.Round(estatisticas.Average(e => (double)e.Minutos!.Value)) : 0,
                Finalizacoes = estatisticas.Sum(e => e.FinalizacoesTotal),
                FinNoGol = estatisticas.Sum(e => e.FinalizacoesNoGol),
                Passes = estatisticas.Sum(e => e.PassesTotal),
                PassesChave = estatisticas.Sum(e => e.PassesChave),
                PassesCertos = estatisticas.Sum(e => e.PassesCertos),
                DriblesTentados = estatisticas.Sum(e => e.DriblesTentados),
                DriblesCertos = estatisticas.Sum(e => e.DriblesCertos),
                DuelosTotal = estatisticas.Sum(e => e.DuelosTotal),
                DuelosVencidos = estatisticas.Sum(e => e.DuelosVencidos),
                Desarmes = estatisticas.Sum(e => e.Desarmes),
                Interceptacoes = estatisticas.Sum(e => e.Interceptacoes),
                Bloqueios = estatisticas.Sum(e => e.Bloqueios),
                Defesas = estatisticas.Sum(e => e.Defesas),
                GolsSofridos = estatisticas.Sum(e => e.GolsSofridos),
                FaltasSofridas = estatisticas.Sum(e => e.FaltasSofridas),
                FaltasCometidas = estatisticas.Sum(e => e.FaltasCometidas),
                Cartoes = estatisticas.Sum(e => e.CartoesAmarelos + e.CartoesVermelhos),
            };
        }
    }
}
