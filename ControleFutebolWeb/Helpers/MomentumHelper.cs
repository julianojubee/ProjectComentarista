using System.Text.Json;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Curva de momentum da partida (aba Momentum do Pós-jogo): quem estava em cima
    /// a cada minuto, de -100 (visitante pressionando) a +100 (mandante).
    ///
    /// Duas origens, na ordem em que o MomentumJogoService tenta:
    ///  1. o FotMob, que publica a própria curva no matchDetails
    ///     (content.momentum.main.data) — modelo deles, sobre todos os lances;
    ///  2. a marcação por vídeo do usuário (/MarcacaoVideo/Marcar), para os jogos
    ///     que fonte nenhuma cobre. Aí a curva é montada aqui: cada ação ofensiva
    ///     vale um peso que decai com o tempo, e a diferença entre os dois lados é
    ///     normalizada pelo pico do jogo — mesma leitura da curva do FotMob, que
    ///     também é relativa à partida e não absoluta.
    /// </summary>
    public static class MomentumHelper
    {
        /// <param name="Minuto">Pode ser fracionário: o FotMob põe acréscimo em 45,5 / 90,5.</param>
        public sealed record Ponto(double Minuto, int Valor);

        public sealed record Acao(int Minuto, bool Casa, double Peso);

        /// <summary>
        /// Peso de cada ação da marcação por vídeo (AcoesMarcacaoVideo). Só entra o
        /// que é ameaça de quem ataca; ação defensiva fica de fora porque desarme no
        /// próprio campo não é sinal de que o time estava por cima.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, double> PesoAcao = new Dictionary<string, double>
        {
            ["gol"] = 5,
            ["finalizacao_gol"] = 3,
            ["finalizacao_fora"] = 2,
            ["penalti_sofrido"] = 3,
            ["passe_chave"] = 1.5,
            ["drible_certo"] = 1,
            ["falta_sofrida"] = 0.5,
            ["impedimento"] = 0.5,
        };

        /// <summary>Minutos para o peso de uma ação cair pela metade.</summary>
        public const double MeiaVida = 3;

        /// <summary>
        /// Curva minuto a minuto a partir das ações. Vazia quando não há ação que
        /// pese — "jogo sem lance marcado" não é "jogo equilibrado".
        /// </summary>
        public static List<Ponto> DeAcoes(IEnumerable<Acao> acoes, int duracao = 90)
        {
            var lista = acoes.Where(a => a.Peso > 0 && a.Minuto >= 0).ToList();
            if (lista.Count == 0) return new();
            duracao = Math.Max(duracao, lista.Max(a => a.Minuto));

            var diferencas = new double[duracao + 1];
            for (var m = 0; m <= duracao; m++)
            {
                double casa = 0, visitante = 0;
                foreach (var a in lista)
                {
                    if (a.Minuto > m) continue;
                    var peso = a.Peso * Math.Pow(0.5, (m - a.Minuto) / MeiaVida);
                    if (a.Casa) casa += peso; else visitante += peso;
                }
                diferencas[m] = casa - visitante;
            }

            var pico = diferencas.Max(Math.Abs);
            if (pico <= 0) return new();
            return diferencas
                .Select((d, m) => new Ponto(m, (int)Math.Round(100 * d / pico)))
                .ToList();
        }

        /// <summary>
        /// Curva do matchDetails do FotMob, ou null quando a partida não tem (jogo
        /// sem cobertura ao vivo, ou ainda não começou).
        /// </summary>
        public static List<Ponto>? LerFotMob(JsonElement raiz)
        {
            if (!raiz.TryGetProperty("content", out var conteudo) ||
                conteudo.ValueKind != JsonValueKind.Object ||
                !conteudo.TryGetProperty("momentum", out var momentum) ||
                momentum.ValueKind != JsonValueKind.Object ||
                !momentum.TryGetProperty("main", out var principal) ||
                principal.ValueKind != JsonValueKind.Object ||
                !principal.TryGetProperty("data", out var dados) ||
                dados.ValueKind != JsonValueKind.Array) return null;

            var pontos = new List<Ponto>();
            foreach (var p in dados.EnumerateArray())
            {
                if (!p.TryGetProperty("minute", out var mEl) || mEl.ValueKind != JsonValueKind.Number ||
                    !p.TryGetProperty("value", out var vEl) || vEl.ValueKind != JsonValueKind.Number) continue;
                pontos.Add(new Ponto(mEl.GetDouble(), (int)Math.Clamp(Math.Round(vEl.GetDouble()), -100, 100)));
            }

            // Só o ponto zero (ou nada) é curva vazia: o FotMob publica o bloco antes
            // do apito e só depois vai preenchendo.
            return pontos.Count(p => p.Valor != 0) == 0 ? null : pontos.OrderBy(p => p.Minuto).ToList();
        }

        /// <param name="Casa">Ponto acima da linha: o mandante estava em cima.</param>
        public sealed record Periodo(double Inicio, double Fim, bool Casa);

        /// <summary>
        /// Fatia do domínio de cada lado: área acima da linha sobre a área total.
        /// Devolve o percentual do mandante (0–100), ou null sem curva.
        /// </summary>
        public static int? DominioCasa(IReadOnlyList<Ponto> pontos)
        {
            double casa = pontos.Where(p => p.Valor > 0).Sum(p => p.Valor);
            double total = pontos.Sum(p => Math.Abs(p.Valor));
            return total > 0 ? (int)Math.Round(100 * casa / total) : null;
        }

        /// <summary>
        /// Trechos em que um lado ficou em cima com força (|valor| ≥ <paramref name="limiar"/>)
        /// por pelo menos <paramref name="minimo"/> pontos seguidos — os "momentos de
        /// pressão" que a tela lista embaixo do gráfico. Os mais intensos primeiro.
        /// </summary>
        public static List<Periodo> Periodos(IReadOnlyList<Ponto> pontos, int limiar = 50, int minimo = 3, int maximo = 4)
        {
            var trechos = new List<(Periodo P, double Intensidade)>();
            var i = 0;
            while (i < pontos.Count)
            {
                if (Math.Abs(pontos[i].Valor) < limiar) { i++; continue; }
                var casa = pontos[i].Valor > 0;
                var j = i;
                while (j + 1 < pontos.Count && Math.Abs(pontos[j + 1].Valor) >= limiar && pontos[j + 1].Valor > 0 == casa) j++;
                if (j - i + 1 >= minimo)
                    trechos.Add((new Periodo(pontos[i].Minuto, pontos[j].Minuto, casa),
                        pontos.Skip(i).Take(j - i + 1).Sum(p => Math.Abs(p.Valor))));
                i = j + 1;
            }
            return trechos.OrderByDescending(t => t.Intensidade).Take(maximo)
                .Select(t => t.P).OrderBy(p => p.Inicio).ToList();
        }
    }
}
