using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    using Linha = ContextoNotaHelper.LinhaEstatistica;
    using Gol = ContextoNotaHelper.LinhaGol;
    using Placar = ContextoNotaHelper.LinhaPlacar;

    public class ContextoNotaHelperTests
    {
        private const int Jogo = 1;

        // Elenco mínimo do jogo 1: goleiro da casa (id 10) e dois atacantes
        // visitantes (20 e 21) que finalizaram no alvo.
        private static Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> Lados(string posicaoGoleiro = "Goleiro")
            => new()
            {
                [(10, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: true,  Posicao: posicaoGoleiro),
                [(20, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: false, Posicao: "Centroavante"),
                [(21, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: false, Posicao: "Ponta Direita"),
            };

        private static bool Decisivo(IEnumerable<Linha> linhas, Dictionary<(int, int), AtuacaoNoJogo>? lados = null)
            => ContextoNotaHelper.Montar(linhas, lados ?? Lados())[(10, Jogo)].GoleiroDecisivo;

        private static Linha Goleiro(int defesas, int? minutos = 90) => new(10, Jogo, minutos, defesas, 0);
        private static Linha Atacante(int id, int noAlvo) => new(id, Jogo, 90, 0, noAlvo);

        [Fact]
        public void PegouTudoQueFoiNoAlvo_EhDecisivo()
        {
            // Adversário finalizou 2 no gol (1 + 1) e o goleiro fez as 2 defesas.
            Assert.True(Decisivo(new[] { Goleiro(defesas: 2), Atacante(20, 1), Atacante(21, 1) }));
        }

        [Fact]
        public void SofreuGol_NaoEhDecisivo()
        {
            // 3 no alvo, só 2 defesas: passou uma.
            Assert.False(Decisivo(new[] { Goleiro(defesas: 2), Atacante(20, 2), Atacante(21, 1) }));
        }

        [Fact]
        public void SemChuteNoAlvoDoAdversario_NaoEhDecisivo()
        {
            // Nada para defender — e um jogo com o dado zerado no banco não pode
            // distribuir o bônus de graça.
            Assert.False(Decisivo(new[] { Goleiro(defesas: 0), Atacante(20, 0) }));
            Assert.False(Decisivo(new[] { Goleiro(defesas: 3), Atacante(20, 0) }));
        }

        [Fact]
        public void FinalizacoesDoProprioTime_NaoContam()
        {
            // O goleiro da casa é medido pelo que o VISITANTE mandou no alvo.
            var lados = Lados();
            lados[(11, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: true, Posicao: "Centroavante");

            var linhas = new[] { Goleiro(defesas: 1), Atacante(20, 1), new Linha(11, Jogo, 90, 0, 5) };

            Assert.True(ContextoNotaHelper.Montar(linhas, lados)[(10, Jogo)].GoleiroDecisivo);
        }

        [Fact]
        public void JogadorDeLinha_NaoRecebeOBonusDeGoleiro()
        {
            Assert.False(Decisivo(new[] { Goleiro(defesas: 2), Atacante(20, 2) }, Lados(posicaoGoleiro: "Zagueiro")));
        }

        [Fact]
        public void GoleiroQueNaoEntrou_NaoEhDecisivo()
        {
            Assert.False(Decisivo(new[] { Goleiro(defesas: 2, minutos: 0), Atacante(20, 2) }));
            Assert.False(Decisivo(new[] { Goleiro(defesas: 2, minutos: null), Atacante(20, 2) }));
        }

        [Fact]
        public void SemEscalacao_NaoDaParaSaberOLado_ENaoEhDecisivo()
        {
            var mapa = ContextoNotaHelper.Montar(new[] { Goleiro(defesas: 2), Atacante(20, 2) }, lados: null);

            Assert.False(mapa[(10, Jogo)].GoleiroDecisivo);
            Assert.Equal(90, mapa[(10, Jogo)].Minutos); // os minutos continuam valendo
        }

        [Fact]
        public void Montar_LevaOsMinutosDeCadaJogador()
        {
            var mapa = ContextoNotaHelper.Montar(new[] { new Linha(20, Jogo, 31, 0, 0) }, Lados());

            Assert.Equal(31, mapa[(20, Jogo)].Minutos);
        }

        [Fact]
        public void De_SemEntradaNoMapa_DevolveContextoVazio()
        {
            Assert.Equal(CriteriosNotaHelper.ContextoNota.Vazio, ContextoNotaHelper.De(null, 10, Jogo));
            Assert.Equal(CriteriosNotaHelper.ContextoNota.Vazio,
                ContextoNotaHelper.De(ContextoNotaHelper.Montar(Array.Empty<Linha>(), Lados()), 99, Jogo));
        }

        // ── Gol da vitória ───────────────────────────────────────────────────
        //
        // Elenco do jogo: 10 é goleiro da casa; 11 e 12 são atacantes da casa;
        // 20 e 21 são visitantes.

        private static Dictionary<(int JogadorId, int JogoId), AtuacaoNoJogo> LadosCompletos()
        {
            var lados = Lados();
            lados[(11, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: true, Posicao: "Centroavante");
            lados[(12, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: true, Posicao: "Ponta Esquerda");
            return lados;
        }

        private static Gol Gol(int id, int jogadorId, int minuto, bool contra = false)
            => new(id, jogadorId, Jogo, minuto, contra);

        private static Placar Placar(int casa, int visitante) => new(Jogo, casa, visitante);

        // Elenco inteiro com estatística, para todos aparecerem no mapa.
        private static IEnumerable<Linha> TodosJogaram()
            => new[] { 10, 11, 12, 20, 21 }.Select(id => new Linha(id, Jogo, 90, 0, 0));

        private static HashSet<int> AutoresDoGolDaVitoria(IEnumerable<Gol> gols, Placar placar,
            Dictionary<(int, int), AtuacaoNoJogo>? lados = null)
            => ContextoNotaHelper
                .Montar(TodosJogaram(), lados ?? LadosCompletos(), gols, new[] { placar })
                .Where(kv => kv.Value.GolDaVitoria)
                .Select(kv => kv.Key.JogadorId)
                .ToHashSet();

        [Fact]
        public void UmAZero_OGolDaVitoriaEhOUnico()
        {
            Assert.Equal(new[] { 11 }, AutoresDoGolDaVitoria(new[] { Gol(1, 11, 30) }, Placar(1, 0)));
        }

        [Fact]
        public void TresAUm_OGolDaVitoriaEhOSegundoDoVencedor()
        {
            var gols = new[]
            {
                Gol(1, 11, 10),   // 1x0
                Gol(2, 20, 20),   // 1x1
                Gol(3, 12, 30),   // 2x1 ← decidiu
                Gol(4, 11, 80),   // 3x1
            };

            Assert.Equal(new[] { 12 }, AutoresDoGolDaVitoria(gols, Placar(3, 1)));
        }

        [Fact]
        public void EmpateNaoTemGolDaVitoria()
        {
            var gols = new[] { Gol(1, 11, 10), Gol(2, 20, 70) };

            Assert.Empty(AutoresDoGolDaVitoria(gols, Placar(1, 1)));
        }

        [Fact]
        public void GolContraDoAdversario_ContaNaSerieMasNaoPremiaNinguem()
        {
            // O visitante 20 fez contra aos 30 e isso decidiu o 2x1 da casa.
            var gols = new[]
            {
                Gol(1, 11, 10),                 // 1x0
                Gol(2, 21, 20),                 // 1x1
                Gol(3, 20, 30, contra: true),   // 2x1 ← decidiu, mas é gol contra
            };

            Assert.Empty(AutoresDoGolDaVitoria(gols, Placar(2, 1)));
        }

        [Fact]
        public void GolContraDoProprioTime_ContaParaOAdversario()
        {
            // A casa venceu 1x0; o gol foi contra do visitante 20 — sem premiação.
            // Já o 11, que fez contra, não pode virar autor de nada.
            var gols = new[] { Gol(1, 20, 40, contra: true) };

            Assert.Empty(AutoresDoGolDaVitoria(gols, Placar(1, 0)));
        }

        [Fact]
        public void SerieIncompleta_NaoArriscaApontarOAutorErrado()
        {
            // Placar diz 3x1 mas só há 2 gols do vencedor registrados: o segundo
            // pode não ser o decisivo de verdade.
            var gols = new[] { Gol(1, 11, 10), Gol(2, 20, 20), Gol(3, 12, 30) };

            Assert.Empty(AutoresDoGolDaVitoria(gols, Placar(3, 1)));
        }

        [Fact]
        public void SemEscalacaoDoAutor_NaoDaParaSaberOLado()
        {
            // 12 não está no mapa de lados: a série do vencedor não fecha.
            var lados = Lados();
            lados[(11, Jogo)] = new AtuacaoNoJogo(IsTimeCasa: true, Posicao: "Centroavante");

            Assert.Empty(AutoresDoGolDaVitoria(new[] { Gol(1, 11, 10), Gol(2, 12, 50) }, Placar(2, 0), lados));
        }

        [Fact]
        public void MesmoMinuto_DesempataPelaOrdemDeInsercao()
        {
            var gols = new[] { Gol(7, 12, 45), Gol(3, 11, 45) };

            // Ambos aos 45: o de Id menor (registrado antes) é o primeiro da série,
            // então em 2x1 o decisivo é o outro.
            Assert.Equal(new[] { 12 }, AutoresDoGolDaVitoria(
                new[] { gols[1], gols[0], Gol(9, 20, 80) }, Placar(2, 1)));
        }

        [Fact]
        public void AutorSemEstatisticaImportada_AindaAssimEntraNoMapa()
        {
            // Só o goleiro tem linha de estatística; quem marcou não pode perder o bônus.
            var mapa = ContextoNotaHelper.Montar(
                new[] { Goleiro(defesas: 0) }, LadosCompletos(), new[] { Gol(1, 11, 30) }, new[] { Placar(1, 0) });

            Assert.True(mapa[(11, Jogo)].GolDaVitoria);
        }

        [Fact]
        public void SemGolsOuPlacar_NinguemGanhaOBonus()
        {
            var mapa = ContextoNotaHelper.Montar(TodosJogaram(), LadosCompletos());

            Assert.DoesNotContain(mapa, kv => kv.Value.GolDaVitoria);
        }
    }
}
