// ── Prancheta de jogadas ────────────────────────────────────────────────────
//
// Uma jogada é uma sequência de PASSOS (keyframes). Cada passo guarda onde está
// cada peça — os jogadores e a bola — e as setas de anotação daquele momento. O
// player anima interpolando as posições entre passos consecutivos, e é isso que
// transforma "goleiro, lateral, cruzamento, finalização" num lance que se
// desenrola na tela.
//
// Todo passo repete a posição de TODAS as peças, inclusive de quem não se moveu.
// Guardar só o delta economizaria bytes e obrigaria a caçar a última posição
// conhecida a cada frame — do jeito atual a interpolação é uma linha de código.
//
// Duas telas usam este arquivo:
//   • /Jogos/Analisar — modal com o editor completo (Jogadas.abrirModal)
//   • /Times/Details  — arsenal do time, só reprodução (Jogadas.montarArsenal)
// O que os dois compartilham é o "palco" (campo + peças + player); o editor
// acrescenta arrasto, setas e a barra de ferramentas por cima do mesmo palco.

(function () {
    'use strict';

    // Posição inicial da bola numa jogada nova: perto do gol do próprio time,
    // que é de onde quase toda construção começa.
    var BOLA_PADRAO = { x: 12, y: 50 };

    // Perfil de cada tipo de bola: em que fração do trecho ela chega, quanto
    // sobe (multiplicador do arco) e quanto gira por unidade de distância. É o
    // que separa um toque rasteiro de um cruzamento — antes os dois tinham
    // exatamente a mesma leitura na tela.
    var PERFIL = {
        toque:      { frac: 0.42, arco: 0.00, giro: 11 },
        passe:      { frac: 0.62, arco: 0.30, giro: 9 },
        conducao:   { frac: 1.00, arco: 0.00, giro: 7 },
        cruzamento: { frac: 0.82, arco: 0.75, giro: 6 },
        lancamento: { frac: 0.88, arco: 1.00, giro: 6 },
        chute:      { frac: 0.30, arco: 0.18, giro: 16 }
    };

    // Multiplicador de tempo do deslocamento: sprint cobre a mesma distância em
    // menos tempo, andar leva mais.
    var RITMO = { andar: 1.35, trote: 1, sprint: 0.7 };

    // Motor antigo: uma bola só, que sobe se o lance for longo.
    function perfilV1(d) { return { frac: 0.62, arco: d > 18 ? 1 : 0, giro: 9 }; }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function limitar(v, min, max) { return Math.max(min, Math.min(max, v)); }

    function suave(t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2; }
    function desacelera(t) { return 1 - Math.pow(1 - t, 3); }

    function distancia(a, b) { var dx = b.x - a.x, dy = b.y - a.y; return Math.sqrt(dx * dx + dy * dy); }

    // ── Caminho curvo ───────────────────────────────────────────────────────
    //
    // Uma diagonal montada em três passos, interpolada em linha reta, vira um
    // zigue-zague com bicos. Catmull-Rom passa exatamente pelos keyframes (o
    // usuário continua vendo a peça onde a colocou) e arredonda o que há entre
    // eles, que é como um jogador de verdade muda de direção.
    function catmull(p0, p1, p2, p3, t) {
        var t2 = t * t, t3 = t2 * t;
        return {
            x: 0.5 * ((2 * p1.x) + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3),
            y: 0.5 * ((2 * p1.y) + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3)
        };
    }

    // Polyline amostrada de pts[de] até pts[ate] com o comprimento acumulado em
    // cada vértice. É o acumulado que permite andar pelo caminho em velocidade
    // constante: sem ele, os trechos curtos da curva seriam percorridos no mesmo
    // tempo dos longos. Corrida de um único trecho fica RETA, sem influência dos
    // vizinhos — a peça vai direto para onde foi arrastada.
    function spline(pts, de, ate) {
        var trecho = pts.slice(de, ate + 1);
        var out = [trecho[0]];
        var sub = trecho.length > 2 ? 14 : 1;
        for (var s = 0; s < trecho.length - 1; s++) {
            var p0 = trecho[s - 1] || trecho[s], p1 = trecho[s];
            var p2 = trecho[s + 1], p3 = trecho[s + 2] || trecho[s + 1];
            for (var k = 1; k <= sub; k++) out.push(sub === 1 ? p2 : catmull(p0, p1, p2, p3, k / sub));
        }
        var cum = [0];
        for (var i = 1; i < out.length; i++) cum.push(cum[i - 1] + distancia(out[i - 1], out[i]));
        return { pts: out, cum: cum, len: cum[cum.length - 1] || 0 };
    }

    // Ponto a "d" unidades do início do caminho.
    function emCaminho(path, d) {
        var c = path.cum;
        if (d <= 0) return path.pts[0];
        if (d >= path.len) return path.pts[path.pts.length - 1];
        var i = 1;
        while (i < c.length && c[i] < d) i++;
        var f = (d - c[i - 1]) / Math.max(0.0001, c[i] - c[i - 1]);
        return {
            x: path.pts[i - 1].x + (path.pts[i].x - path.pts[i - 1].x) * f,
            y: path.pts[i - 1].y + (path.pts[i].y - path.pts[i - 1].y) * f
        };
    }

    function clonarPasso(p) {
        return {
            legenda: p.legenda || '',
            passe: p.passe || 'passe',
            bola: { x: p.bola.x, y: p.bola.y },
            pecas: p.pecas.map(function (c) {
                // O ritmo é característica do jogador naquele lance e costuma se
                // manter no passo seguinte; o atraso é do momento e zera.
                return { id: c.id, x: c.x, y: c.y, modo: c.modo || 'trote', atraso: 0 };
            }),
            setas: []   // anotação descreve UM momento; copiá-la poluiria o passo novo
        };
    }

    function storyboardVazio(ms) {
        return {
            v: 2,
            ms: ms || 900,
            elenco: [],
            // Cor da camisa de cada lado na prancheta. Fica no storyboard (e não
            // numa preferência do usuário) porque é parte do desenho: o arsenal do
            // time precisa reproduzir a jogada com as mesmas cores em que ela foi
            // montada, como o match-up dos creators faz.
            cores: { nos: COR_PADRAO.nos, adv: COR_PADRAO.adv },
            // Blocos: conjuntos de jogadores que se movem juntos (a linha de quatro,
            // o triângulo do meio). Ficam na raiz, e não no passo, porque o vínculo
            // é entre as PEÇAS e vale a jogada inteira — o que muda de passo para
            // passo é a posição do bloco, não quem faz parte dele.
            grupos: [],
            passos: [{ legenda: '', passe: 'passe', bola: { x: BOLA_PADRAO.x, y: BOLA_PADRAO.y }, pecas: [], setas: [] }]
        };
    }

    // Cores padrão das camisas: as mesmas que a prancheta sempre teve (amarelo
    // para quem executa a jogada, azul para a marcação).
    var COR_PADRAO = { nos: '#facc15', adv: '#60a5fa' };

    function corHex(v, alt) { return /^#[0-9a-fA-F]{6}$/.test(v || '') ? v : alt; }

    // Paleta dos blocos: cores que não são as dos times (amarelo/azul) nem a da
    // posse (branco), para o contorno do setor não competir com a leitura tática.
    var COR_BLOCO = ['#f472b6', '#a78bfa', '#34d399', '#fb923c', '#38bdf8', '#e879f9'];

    var MAX_GRUPOS = 6;

    // Storyboard vindo do servidor pode ter sido gravado por uma versão anterior:
    // completa o que faltar para o resto do arquivo não precisar checar nada.
    function normalizar(sb) {
        var out = storyboardVazio(sb && sb.ms);
        if (!sb) return out;
        out.elenco = (sb.elenco || []).map(function (e) {
            // adv ausente = jogada gravada antes dos adversários existirem: era só
            // o time atacante, então todo mundo é do nosso lado.
            return {
                id: e.id, num: e.num || '', nome: e.nome || '', sigla: e.sigla || '',
                adv: !!e.adv, foto: e.foto || ''
            };
        });
        out.cores = {
            nos: corHex(sb.cores && sb.cores.nos, COR_PADRAO.nos),
            adv: corHex(sb.cores && sb.cores.adv, COR_PADRAO.adv)
        };
        var passos = (sb.passos || []).map(function (p) {
            return {
                legenda: p.legenda || '',
                // Jogada gravada na v1 não tinha tipo de bola nem ritmo por peça:
                // o default reproduz exatamente o que ela fazia antes.
                passe: PERFIL[p.passe] ? p.passe : 'passe',
                bola: p.bola ? { x: p.bola.x, y: p.bola.y } : { x: BOLA_PADRAO.x, y: BOLA_PADRAO.y },
                pecas: (p.pecas || []).map(function (c) {
                    return {
                        id: c.id, x: c.x, y: c.y,
                        modo: RITMO[c.modo] ? c.modo : 'trote',
                        atraso: typeof c.atraso === 'number' ? limitar(c.atraso, 0, 0.6) : 0
                    };
                }),
                setas: (p.setas || []).map(function (s) { return { x1: s.x1, y1: s.y1, x2: s.x2, y2: s.y2 }; })
            };
        });
        if (passos.length) out.passos = passos;

        // Um jogador entra em no máximo um bloco: com blocos sobrepostos, arrastar
        // uma peça compartilhada não teria resposta certa. Grupos de menos de dois
        // membros (o outro saiu da jogada) deixam de existir.
        var noElenco = {};
        out.elenco.forEach(function (e) { noElenco[e.id] = true; });
        var usado = {};
        out.grupos = (sb.grupos || []).slice(0, MAX_GRUPOS).map(function (g, i) {
            var ids = (g.ids || []).filter(function (id) {
                if (!noElenco[id] || usado[id]) return false;
                usado[id] = true;
                return true;
            });
            return { id: g.id || ('g' + i), nome: g.nome || '', ids: ids };
        }).filter(function (g) { return g.ids.length >= 2; });

        return out;
    }

    // ══ Palco ═══════════════════════════════════════════════════════════════
    // Campo + peças + player. Dono do storyboard: quem edita chama os métodos
    // daqui em vez de mexer no objeto por fora, para o DOM nunca ficar defasado.
    function criarPalco(host, opts) {
        opts = opts || {};
        var editavel = !!opts.editavel;
        var aoMudar = opts.aoMudar || function () { };
        var aoTrocarPasso = opts.aoTrocarPasso || function () { };
        var aoParar = opts.aoParar || function () { };
        // Entrou/saiu jogador do campo: a lista do elenco marca quem já está em
        // campo, e a peça pode sair pelo × dentro do palco (fora do alcance do
        // editor), então quem avisa é o palco.
        var aoMudarElenco = opts.aoMudarElenco || function () { };
        // O relógio agora é em milissegundos absolutos e a barra de scrub precisa
        // acompanhá-lo quadro a quadro.
        var aoTempo = opts.aoTempo || function () { };
        var aoSelecionar = opts.aoSelecionar || function () { };

        var sb = storyboardVazio();
        var passoAtual = 0;
        var tocando = false;
        var raf = null;
        var velocidade = 1;
        var loop = true;
        var modoSeta = false;
        var setaOrigem = null;
        var pecasDom = {};   // id do jogador → elemento
        var bolaDom = null;

        // 'v2' é o motor novo (corrida contínua, caminho curvo, tempo por
        // distância); 'v1' reproduz o comportamento antigo para comparação lado a
        // lado na mesma jogada.
        var motor = opts.motor === 'v1' ? 'v1' : 'v2';
        var plano = null;     // { durs, ini, total, pecas, bola } — ver planejar()
        var tempo = 0;        // posição do player, em ms desde o início da jogada
        // Fotos dos jogadores no disco da peça. Ligadas por padrão; quem prefere só
        // números desliga no botão da barra (a preferência é do palco, não da
        // jogada — não muda o que está salvo).
        var verFotos = opts.fotos !== false;
        var sel = null;       // peça selecionada no editor (a última clicada)
        // Seleção múltipla: Ctrl/Shift+clique ou laço no gramado. É o que vira um
        // bloco, e enquanto ela existe o arrasto move todas as peças juntas.
        var selecao = [];
        var verCaminhos = false;
        var snap = false;
        var hist = [];        // pilha de desfazer (snapshots JSON do storyboard)
        var fut = [];         // pilha de refazer
        var setasDesenhadas = -1;   // último passo cujas setas foram para o SVG

        // As traves ficam FORA de .jgd-campo, na margem do wrapper. O campo é o
        // sistema de coordenadas da jogada (0–100% = a caixa verde): desenhar o gol
        // dentro dele invadiria a área, e encolher o verde para abrir espaço moveria
        // todas as jogadas já salvas.
        host.innerHTML =
            '<div class="jgd-trave jgd-trave-esq"></div>' +
            '<div class="jgd-trave jgd-trave-dir"></div>' +
            '<div class="jgd-campo theme-dark-zone">' +
                '<div class="jgd-linha-meio"></div><div class="jgd-circulo"></div>' +
                '<div class="jgd-area-esq"></div><div class="jgd-area-dir"></div>' +
                '<div class="jgd-gol-esq"></div><div class="jgd-gol-dir"></div>' +
                '<div class="jgd-sentido">ATAQUE →</div>' +
                '<svg class="jgd-svg"><defs>' +
                    '<marker class="jgd-marker" markerWidth="11" markerHeight="9" refX="9" refY="4.5"' +
                    ' orient="auto" markerUnits="userSpaceOnUse">' +
                    '<path d="M0,0 L11,4.5 L0,9 z" fill="#facc15"></path>' +
                    '</marker>' +
                '</defs><g class="jgd-g-blocos"></g><g class="jgd-g-caminhos"></g><g class="jgd-g-rastro"></g><g class="jgd-g-setas"></g></svg>' +
                '<div class="jgd-legenda-palco"></div>' +
            '</div>';

        var campo = host.querySelector('.jgd-campo');
        var svg = host.querySelector('.jgd-svg');
        var gBlocos = host.querySelector('.jgd-g-blocos');
        var gCaminhos = host.querySelector('.jgd-g-caminhos');
        var gRastro = host.querySelector('.jgd-g-rastro');
        var gSetas = host.querySelector('.jgd-g-setas');
        var legendaPalco = host.querySelector('.jgd-legenda-palco');

        // O marker do SVG é referenciado por id e o modal pode conviver com vários
        // palcos na mesma página (arsenal) — id fixo faria todos apontarem para o
        // primeiro, que some quando aquele card é removido.
        var markerId = 'jgdSeta' + Math.random().toString(36).slice(2, 9);
        host.querySelector('.jgd-marker').id = markerId;

        // A sombra é um elemento à parte porque fica no CHÃO: quando a bola sobe
        // no cruzamento, ela se afasta da sombra — é isso que dá a leitura de
        // altura num campo visto de cima.
        var bolaSombraDom = document.createElement('div');
        bolaSombraDom.className = 'jgd-bola-sombra';
        campo.appendChild(bolaSombraDom);

        bolaDom = document.createElement('div');
        bolaDom.className = 'jgd-bola';
        bolaDom.title = editavel ? 'Arraste a bola para onde ela chega neste passo' : '';
        campo.appendChild(bolaDom);

        // Distância percorrida por cada peça, em % do campo. A cadência da passada
        // e o giro da bola derivam dela (e não do tempo): quem corre mais rápido
        // dá mais passadas por segundo sozinho, sem precisar de fator de correção.
        var giroBola = 0;

        // ── Coordenadas ─────────────────────────────────────────────────────
        function pctDoEvento(clientX, clientY) {
            var r = campo.getBoundingClientRect();
            return {
                x: limitar(((clientX - r.left) / r.width) * 100, 0, 100),
                y: limitar(((clientY - r.top) / r.height) * 100, 0, 100)
            };
        }

        function passo() { return sb.passos[passoAtual]; }

        function pecaNoPasso(p, id) {
            for (var i = 0; i < p.pecas.length; i++) if (p.pecas[i].id === id) return p.pecas[i];
            return null;
        }

        // ── Peças ───────────────────────────────────────────────────────────
        function sincronizarPecas() {
            var vistos = {};

            sb.elenco.forEach(function (j) {
                vistos[j.id] = true;
                var el = pecasDom[j.id];
                if (!el) {
                    el = document.createElement('div');
                    el.dataset.id = j.id;
                    campo.appendChild(el);
                    pecasDom[j.id] = el;
                }
                var comFoto = verFotos && !!j.foto;
                el.className = 'jgd-peca' + (j.adv ? ' jgd-adv' : '') + (comFoto ? ' jgd-com-foto' : '');
                el.innerHTML =
                    '<div class="jgd-peca-sigla">' + esc(j.sigla) + '</div>' +
                    '<div class="jgd-peca-disco">' +
                        '<span class="jgd-peca-sombra"></span>' +
                        '<span class="jgd-peca-facing"></span>' +
                        // Com foto o círculo vira moldura da imagem e o número vai
                        // para um badge no canto — mesma leitura do match-up, onde
                        // o rosto identifica mais rápido que o número.
                        '<div class="jgd-peca-circulo">' +
                            (comFoto ? '<img class="jgd-peca-foto" src="' + esc(j.foto) + '" alt="" loading="lazy">' : '') +
                            '<span class="jgd-peca-num">' + esc(j.num || '') + '</span>' +
                        '</div>' +
                    '</div>' +
                    '<div class="jgd-peca-nome">' + esc(j.nome) + '</div>' +
                    (editavel ? '<button type="button" class="jgd-peca-remover" title="Tirar da jogada">×</button>' : '');
                el.title = j.nome;

                // Cacheadas: a pose é recalculada a cada quadro para as 22 peças e
                // não vale pagar querySelector nisso.
                el._facing = el.querySelector('.jgd-peca-facing');
                el._circulo = el.querySelector('.jgd-peca-circulo');
                el._sombra = el.querySelector('.jgd-peca-sombra');
                el._andado = 0;
                el._px = null;
                el._py = null;
                el._ox = null;
                el._oy = null;
            });

            Object.keys(pecasDom).forEach(function (id) {
                if (!vistos[id]) {
                    pecasDom[id].remove();
                    delete pecasDom[id];
                }
            });

            // Peça que saiu de campo não pode continuar selecionada nem valendo
            // como membro de um bloco.
            aplicarCores();
            selecao = selecao.filter(function (id) { return !!pecasDom[id]; });
            if (sel != null && !pecasDom[sel]) sel = selecao.length ? selecao[selecao.length - 1] : null;
            pintarSelecao();
        }

        // ── Blocos (setores que andam juntos) ───────────────────────────────
        function grupoDe(id) {
            for (var i = 0; i < sb.grupos.length; i++) {
                if (sb.grupos[i].ids.indexOf(id) >= 0) return sb.grupos[i];
            }
            return null;
        }

        function indiceDoGrupo(g) { return sb.grupos.indexOf(g); }

        // Quem se move junto com esta peça: a seleção múltipla manda (é o que o
        // usuário está segurando na tela); fora dela, vale o bloco a que a peça
        // pertence. Sem nenhum dos dois, a peça anda sozinha, como sempre andou.
        function bloco(id) {
            if (selecao.length > 1 && selecao.indexOf(id) >= 0) return selecao.slice();
            var g = grupoDe(id);
            return g ? g.ids.slice() : [id];
        }

        // Peça selecionada: alvo dos controles de ritmo/atraso e das setas do
        // teclado. Fica com anel ciano, cor que ainda não é usada em campo (o
        // branco já é posse de bola e o amarelo/azul são os times).
        //
        // `aditivo` (Ctrl/Shift+clique) alterna a peça dentro da seleção múltipla
        // em vez de trocar de peça: é assim que se junta a linha de zaga.
        function selecionar(id, aditivo) {
            if (id == null) {
                selecao = [];
            } else if (aditivo) {
                var i = selecao.indexOf(id);
                if (i >= 0) {
                    selecao.splice(i, 1);
                    // Tirou a principal: a próxima da lista assume os controles.
                    if (sel === id) sel = selecao.length ? selecao[selecao.length - 1] : null;
                    pintarSelecao();
                    aoSelecionar(sel);
                    return;
                }
                selecao.push(id);
            } else if (selecao.length !== 1 || selecao[0] !== id) {
                selecao = [id];
            }

            if (sel === id && !aditivo) { pintarSelecao(); return; }
            sel = id;
            pintarSelecao();
            aoSelecionar(sel);
        }

        // Seleciona um conjunto de uma vez (laço, ou o bloco inteiro ao clicar
        // numa peça agrupada).
        function selecionarVarios(ids) {
            selecao = ids.slice();
            sel = selecao.length ? selecao[selecao.length - 1] : null;
            pintarSelecao();
            aoSelecionar(sel);
        }

        function pintarSelecao() {
            Object.keys(pecasDom).forEach(function (k) {
                var id = parseInt(k, 10);
                var el = pecasDom[k];
                el.classList.toggle('jgd-selecionada', selecao.indexOf(id) >= 0);
                el.classList.toggle('jgd-principal', id === sel && selecao.length > 1);
                var g = grupoDe(id);
                el.classList.toggle('jgd-em-bloco', !!g);
                el.style.setProperty('--jgd-bloco-cor', g ? COR_BLOCO[indiceDoGrupo(g) % COR_BLOCO.length] : 'transparent');
            });
        }

        // As cores da camisa entram como variáveis no host: o CSS das peças (e o do
        // fantasma, do rastro) lê delas, então trocar a cor é uma atribuição só.
        function aplicarCores() {
            host.style.setProperty('--jgd-cor-nos', corHex(sb.cores && sb.cores.nos, COR_PADRAO.nos));
            host.style.setProperty('--jgd-cor-adv', corHex(sb.cores && sb.cores.adv, COR_PADRAO.adv));
        }

        function posicionar(el, x, y) {
            el.style.left = x + '%';
            el.style.top = y + '%';
        }

        // ── Pose das peças ──────────────────────────────────────────────────
        //
        // O círculo é simétrico: girá-lo não comunicaria nada. Quem indica para
        // onde o jogador vai é o "nariz" (.jgd-peca-facing), que só aparece com a
        // peça em movimento. O resto da vida vem da passada — um sobe-e-desce cuja
        // fase é função da DISTÂNCIA percorrida, não do relógio, de modo que correr
        // mais rápido dá mais passadas por segundo naturalmente.

        function orientar(el, dx, dy) {
            if (Math.abs(dx) < 0.01 && Math.abs(dy) < 0.01) return;   // parado mantém a última direção
            // +90° porque o nariz é desenhado apontando para cima.
            el._facing.style.transform = 'rotate(' + (Math.atan2(dy, dx) * 180 / Math.PI + 90) + 'deg)';
        }

        function pousar(el) {
            el.classList.remove('jgd-movendo', 'jgd-com-bola');
            el._circulo.style.transform = '';
            el._sombra.style.transform = '';
            el._andado = 0;
            el._px = null;
            el._py = null;
            el._ox = null;
            el._oy = null;
        }

        // Aplica a passada a partir do quanto a peça andou desde o quadro anterior.
        function passada(el, x, y) {
            var dpx = el._px === null ? 0 : x - el._px;
            var dpy = el._py === null ? 0 : y - el._py;
            var avanco = Math.sqrt(dpx * dpx + dpy * dpy);
            el._px = x;
            el._py = y;
            el._andado += avanco;

            var movendo = avanco > 0.015;
            el.classList.toggle('jgd-movendo', movendo);

            if (!movendo) {
                el._circulo.style.transform = '';
                el._sombra.style.transform = '';
                return;
            }

            var fase = el._andado * 2.6;
            var altura = Math.abs(Math.sin(fase)) * 2.4;          // sobe nos dois "pés"
            var esticar = 1 + Math.min(avanco * 5, 0.55);         // sombra alonga na corrida

            el._circulo.style.transform = 'translateY(' + (-altura) + 'px)';
            el._sombra.style.transform = 'translateX(-50%) scaleX(' + esticar + ') scaleY(' + (1 - altura * 0.12) + ')';
        }

        // Bola: gira conforme roda e sobe num arco cuja altura vem do tipo de
        // lance — o afastamento da própria sombra é o que lê como bola no alto.
        // `arco` é o multiplicador do PERFIL: 0 no toque rasteiro, 1 no lançamento.
        function poseBola(x, y, distanciaDoLance, f, arco) {
            posicionar(bolaSombraDom, x, y);

            var alto = arco > 0 ? Math.sin(Math.PI * limitar(f, 0, 1)) * Math.min(distanciaDoLance * 0.5, 18) * arco : 0;
            var escala = 1 + alto / 46;

            bolaDom.style.transform = 'translate(-50%,-50%) translateY(' + (-alto) + 'px) rotate(' + giroBola + 'deg) scale(' + escala + ')';
            bolaSombraDom.style.opacity = alto > 0.5 ? String(limitar(0.5 - alto / 55, 0.12, 0.5)) : '0.42';
            bolaSombraDom.style.transform = 'translate(-50%,-50%) scale(' + limitar(1 - alto / 40, 0.55, 1) + ')';
        }

        function bolaParada(x, y) {
            posicionar(bolaSombraDom, x, y);
            bolaDom.style.transform = 'translate(-50%,-50%) rotate(' + giroBola + 'deg)';
            bolaSombraDom.style.opacity = '0.42';
            bolaSombraDom.style.transform = 'translate(-50%,-50%)';
        }

        // ── Desenho ─────────────────────────────────────────────────────────
        function linha(g, x1, y1, x2, y2, classe, comPonta) {
            var r = campo.getBoundingClientRect();
            var el = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            el.setAttribute('x1', (x1 / 100) * r.width);
            el.setAttribute('y1', (y1 / 100) * r.height);
            el.setAttribute('x2', (x2 / 100) * r.width);
            el.setAttribute('y2', (y2 / 100) * r.height);
            el.setAttribute('class', classe);
            if (comPonta) el.setAttribute('marker-end', 'url(#' + markerId + ')');
            g.appendChild(el);
            return el;
        }

        function poli(g, pts, classe) {
            var r = campo.getBoundingClientRect();
            var el = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
            el.setAttribute('points', pts.map(function (p) {
                return ((p.x / 100) * r.width) + ',' + ((p.y / 100) * r.height);
            }).join(' '));
            el.setAttribute('class', classe);
            g.appendChild(el);
            return el;
        }

        // Contorno do bloco: o casco convexo das peças do grupo. Com dois é uma
        // linha, com três um triângulo, com quatro em fila uma faixa fina — a
        // mesma regra desenha qualquer setor sem o usuário escolher o formato.
        function casco(pts) {
            if (pts.length < 3) return pts.slice();
            var ps = pts.slice().sort(function (a, b) { return a.x - b.x || a.y - b.y; });
            function cruz(o, a, b) { return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x); }
            var baixo = [], i;
            for (i = 0; i < ps.length; i++) {
                while (baixo.length >= 2 && cruz(baixo[baixo.length - 2], baixo[baixo.length - 1], ps[i]) <= 0) baixo.pop();
                baixo.push(ps[i]);
            }
            var cima = [];
            for (i = ps.length - 1; i >= 0; i--) {
                while (cima.length >= 2 && cruz(cima[cima.length - 2], cima[cima.length - 1], ps[i]) <= 0) cima.pop();
                cima.push(ps[i]);
            }
            baixo.pop(); cima.pop();
            var h = baixo.concat(cima);
            return h.length >= 2 ? h : ps;
        }

        function desenharBlocos() {
            gBlocos.innerHTML = '';
            if (!editavel || tocando || !sb.grupos.length) return;
            var p = passo();
            if (!p) return;

            sb.grupos.forEach(function (g, i) {
                var pts = g.ids.map(function (id) { return pecaNoPasso(p, id); })
                    .filter(function (c) { return !!c; })
                    .map(function (c) { return { x: c.x, y: c.y }; });
                if (pts.length < 2) return;

                var h = casco(pts);
                if (h.length > 2) h = h.concat([h[0]]);   // fecha o polígono
                var el = poli(gBlocos, h, 'jgd-bloco');
                el.style.stroke = COR_BLOCO[i % COR_BLOCO.length];
            });
        }

        function desenharSetas(p) {
            gSetas.innerHTML = '';
            if (!p) return;
            p.setas.forEach(function (s, idx) {
                var el = linha(gSetas, s.x1, s.y1, s.x2, s.y2, 'jgd-seta', true);
                if (editavel) {
                    el.addEventListener('dblclick', function (ev) {
                        ev.stopPropagation();
                        snapshot();
                        p.setas.splice(idx, 1);
                        desenharSetas(p);
                        aoMudar();
                    });
                }
            });
        }

        function limparFantasmas() {
            gRastro.innerHTML = '';
            host.querySelectorAll('.jgd-fantasma').forEach(function (f) { f.remove(); });
        }

        // Fantasma do passo anterior: mostra de onde cada peça veio, para o usuário
        // montar o passo novo enxergando o movimento que está criando.
        function desenharRastro() {
            limparFantasmas();
            if (!editavel || tocando || passoAtual === 0) return;

            var ant = sb.passos[passoAtual - 1];
            var atu = passo();

            atu.pecas.forEach(function (c) {
                var a = pecaNoPasso(ant, c.id);
                if (!a || (Math.abs(a.x - c.x) < 0.5 && Math.abs(a.y - c.y) < 0.5)) return;
                var f = document.createElement('div');
                f.className = 'jgd-fantasma';
                posicionar(f, a.x, a.y);
                campo.appendChild(f);
                linha(gRastro, a.x, a.y, c.x, c.y, 'jgd-rastro', false);
            });

            if (Math.abs(ant.bola.x - atu.bola.x) > 0.5 || Math.abs(ant.bola.y - atu.bola.y) > 0.5) {
                linha(gRastro, ant.bola.x, ant.bola.y, atu.bola.x, atu.bola.y, 'jgd-rastro', false);
            }
        }

        // Onion skin da jogada inteira: cada corrida planejada vira uma polyline
        // tracejada e o trajeto da bola uma linha cheia. Como sai do próprio
        // plano, é a pré-visualização exata do que o motor vai percorrer — o
        // rastro fantasma só mostra o passo anterior.
        function desenharCaminhos() {
            gCaminhos.innerHTML = '';
            if (!verCaminhos || !plano || tocando) return;

            var porId = {};
            sb.elenco.forEach(function (j) { porId[j.id] = j; });

            Object.keys(plano.pecas).forEach(function (id) {
                var info = plano.pecas[id];
                var j = porId[id];
                var classe = 'jgd-caminho' + (j && j.adv ? ' jgd-caminho-adv' : '');
                info.runs.forEach(function (r) {
                    if (r.path) poli(gCaminhos, r.path.pts, classe);
                    else poli(gCaminhos, [info.pts[r.de], info.pts[r.ate + 1]], classe);
                });
            });

            if (sb.passos.length > 1) {
                poli(gCaminhos, sb.passos.map(function (p) { return p.bola; }), 'jgd-caminho-bola');
            }
        }

        // Quem está com a bola num passo: o mais próximo dela, desde que perto o
        // bastante para ser dele (bola solta no campo não tem dono).
        function donoDaBola(p) {
            var dono = null, melhor = 7;   // em % do campo
            p.pecas.forEach(function (c) {
                var dx = c.x - p.bola.x, dy = c.y - p.bola.y;
                var d = Math.sqrt(dx * dx + dy * dy);
                if (d < melhor) { melhor = d; dono = c.id; }
            });
            return dono;
        }

        function mostrarLegenda(texto) {
            legendaPalco.textContent = texto || '';
            legendaPalco.classList.toggle('jgd-visivel', !!texto);
        }

        // ── Plano de tempo ──────────────────────────────────────────────────
        //
        // O player trabalha em milissegundos absolutos sobre um plano
        // pré-calculado, e não mais num relógio em "unidades de passo". É esse
        // plano que resolve os três defeitos do motor antigo:
        //
        //  • a duração de cada trecho vem da MAIOR distância percorrida nele —
        //    quem anda 2% e quem cruza 40% do campo não gastam mais o mesmo tempo;
        //  • cada peça recebe "corridas" (sequências de trechos consecutivos em
        //    que ela se move) com um caminho curvo único, percorrido em
        //    velocidade constante — o easing passa a valer na corrida inteira, e
        //    não em cada trecho, então a peça não desacelera nem para em cada
        //    keyframe que atravessa;
        //  • a bola sabe se está sendo conduzida ou em voo.
        function planejar() {
            var P = sb.passos, n = P.length, durs = [], i;
            var v2 = motor === 'v2';

            for (i = 0; i < n - 1; i++) {
                if (!v2) { durs.push(sb.ms); continue; }
                // A bola pesa menos que as pernas: um passe longo não deve esticar
                // o trecho tanto quanto uma corrida longa.
                var maior = distancia(P[i].bola, P[i + 1].bola) * 0.6;
                P[i].pecas.forEach(function (c) {
                    var b = pecaNoPasso(P[i + 1], c.id);
                    if (b) maior = Math.max(maior, distancia(c, b));
                });
                durs.push(limitar(sb.ms * (0.42 + maior / 24), 380, 2600));
            }

            var ini = [0];
            durs.forEach(function (d, k) { ini.push(ini[k] + d); });
            var total = ini[n - 1] || 0;

            var pecas = {};
            sb.elenco.forEach(function (j) {
                var pts = P.map(function (p) {
                    var c = pecaNoPasso(p, j.id);
                    return c ? { x: c.x, y: c.y } : null;
                });
                // Peça ausente de algum passo não tem trajetória contínua: fica
                // fora do plano e o quadro a esconde.
                if (pts.some(function (p) { return !p; })) return;

                var info = { pts: pts, runs: [] }, k = 0;
                while (k < n - 1) {
                    if (distancia(pts[k], pts[k + 1]) < 0.6) { k++; continue; }   // parada
                    var fim = k;
                    while (fim + 1 < n - 1 && distancia(pts[fim + 1], pts[fim + 2]) >= 0.6) fim++;

                    var c0 = pecaNoPasso(P[k], j.id);
                    var atraso = v2 ? (c0.atraso || 0) : 0;
                    var ritmo = v2 ? (RITMO[c0.modo] || 1) : 1;
                    var t0 = ini[k] + durs[k] * atraso;
                    var bruto = ini[fim + 1] - t0;
                    var t1 = t0 + bruto * ritmo;

                    // Quem anda termina depois do trecho terminar. Em vez de cortar
                    // a corrida (a peça pararia antes de chegar onde foi colocada),
                    // a jogada inteira espera por ela.
                    if (t1 > total) total = t1;

                    info.runs.push({
                        de: k, ate: fim, t0: t0, t1: t1,
                        path: v2 ? spline(pts, k, fim + 1) : null
                    });
                    k = fim + 1;
                }
                pecas[j.id] = info;
            });

            var bola = [];
            for (i = 0; i < n - 1; i++) {
                var a = P[i], b = P[i + 1];
                var tipo = a.passe || 'passe';
                var dA = donoDaBola(a), dB = donoDaBola(b);
                // Condução: ou o usuário marcou o trecho assim, ou a bola termina
                // com o mesmo jogador que a tinha — nos dois casos ela vai colada
                // nele, em vez de escorregar sozinha até o destino.
                var conduz = v2 && (tipo === 'conducao' ||
                    (dA != null && dA === dB && distancia(a.bola, b.bola) > 0.6));
                bola.push({ tipo: tipo, conduz: conduz, carregador: dA, d: distancia(a.bola, b.bola) });
            }

            plano = { durs: durs, ini: ini, total: total, pecas: pecas, bola: bola };
        }

        // Render estático de um passo (modo edição / jogada parada).
        function renderizarPasso() {
            var p = passo();
            if (!p) return;
            planejar();
            var ant = passoAtual > 0 ? sb.passos[passoAtual - 1] : null;
            var dono = donoDaBola(p);

            sb.elenco.forEach(function (j) {
                var c = pecaNoPasso(p, j.id);
                var el = pecasDom[j.id];
                if (!el) return;
                if (!c) { el.style.display = 'none'; return; }
                el.style.display = '';
                posicionar(el, c.x, c.y);
                pousar(el);
                el.classList.toggle('jgd-com-bola', j.id === dono);
                // Parado, o nariz aponta de onde a peça veio no passo anterior:
                // reforça o rastro fantasma que o editor já desenha.
                var a = ant && pecaNoPasso(ant, j.id);
                if (a) {
                    orientar(el, c.x - a.x, c.y - a.y);
                    el.classList.toggle('jgd-movendo', Math.abs(c.x - a.x) > 0.5 || Math.abs(c.y - a.y) > 0.5);
                }
            });
            bolaParada(p.bola.x, p.bola.y);
            posicionar(bolaDom, p.bola.x, p.bola.y);
            desenharSetas(p);
            setasDesenhadas = passoAtual;
            desenharRastro();
            desenharBlocos();
            desenharCaminhos();
            mostrarLegenda(tocando ? p.legenda : (editavel ? '' : p.legenda));
        }

        // Render do instante t (ms desde o início da jogada). Devolve o passo
        // corrente, para a timeline e o contador acompanharem.
        // Onde está cada peça e a bola no instante t (ms desde o início da jogada).
        // Puro: não toca no DOM. Quem pinta é quadro() na tela e o exportador de
        // vídeo no canvas — os dois têm de mostrar exatamente a mesma jogada, e a
        // única forma de garantir isso é um cálculo só.
        //
        // `ant` carrega o que depende do quadro anterior (a bola conduzida precisa
        // saber de onde o jogador veio, e o giro é acumulado): quem chama guarda o
        // objeto devolvido em `bola.mem` e passa de volta no quadro seguinte.
        function estadoEm(t, ant) {
            var P = sb.passos, n = P.length, i = 0;
            var v2 = motor === 'v2';
            while (i < n - 2 && plano.ini[i + 1] <= t) i++;
            var f = limitar((t - plano.ini[i]) / Math.max(1, plano.durs[i]), 0, 1);
            var a = P[i], b = P[i + 1];
            var bp = plano.bola[i];
            var prof = v2 ? PERFIL[bp.tipo] : perfilV1(bp.d);
            var mem = ant || {};

            var posic = {};
            sb.elenco.forEach(function (j) {
                var info = plano.pecas[j.id], x, y;
                if (!info) { posic[j.id] = null; return; }

                if (v2) {
                    var run = null;
                    for (var k = 0; k < info.runs.length; k++) {
                        var r = info.runs[k];
                        if (t >= r.t0 && t <= r.t1) { run = r; break; }
                    }
                    if (run) {
                        // O easing vale sobre a CORRIDA inteira: no meio dela a
                        // velocidade é constante, e a aceleração e a frenagem ficam
                        // só no começo e no fim do deslocamento de verdade.
                        var u = suave(limitar((t - run.t0) / Math.max(1, run.t1 - run.t0), 0, 1));
                        var pt = emCaminho(run.path, u * run.path.len);
                        x = pt.x; y = pt.y;
                    } else {
                        // Parada: no fim da última corrida concluída — ou, antes da
                        // primeira, no ponto de onde ela vai sair.
                        var idx = info.runs.length ? info.runs[0].de : 0;
                        info.runs.forEach(function (r) { if (t > r.t1) idx = r.ate + 1; });
                        var pp = info.pts[limitar(idx, 0, n - 1)];
                        x = pp.x; y = pp.y;
                    }
                } else {
                    var ca = pecaNoPasso(a, j.id), cb = pecaNoPasso(b, j.id);
                    var de = ca || cb, para = cb || ca;
                    if (!de) { posic[j.id] = null; return; }
                    var fp = suave(f);
                    x = de.x + (para.x - de.x) * fp;
                    y = de.y + (para.y - de.y) * fp;
                }

                posic[j.id] = { x: x, y: y };
            });

            // ── bola
            var bx, by, fb, giroInc, cx = null, cy = null;
            if (v2 && bp.conduz && posic[bp.carregador]) {
                // Colada em quem conduz: um pouco à frente do jogador, com a
                // oscilação lateral de quem toca a bola de um pé para o outro.
                var c = posic[bp.carregador];
                var dx = c.x - (mem.cx == null ? c.x : mem.cx);
                var dy = c.y - (mem.cy == null ? c.y : mem.cy);
                var m = Math.sqrt(dx * dx + dy * dy) || 1;
                var bal = Math.sin(t / 90) * 0.7;
                bx = c.x + (dx / m) * 2.4 - (dy / m) * bal;
                by = c.y + (dy / m) * 2.4 + (dx / m) * bal;
                cx = c.x; cy = c.y;
                fb = 0;
                giroInc = m * 12;
            } else {
                fb = desacelera(limitar(f / prof.frac, 0, 1));
                bx = a.bola.x + (b.bola.x - a.bola.x) * fb;
                by = a.bola.y + (b.bola.y - a.bola.y) * fb;
                giroInc = bp.d * (fb - (mem.fb == null ? fb : mem.fb)) * prof.giro;
            }

            // Posse é decidida nos PASSOS, não quadro a quadro: medir a distância
            // até a bola em pleno voo faz cada jogador por quem ela passa piscar,
            // como se tivesse tocado nela. Aqui o dono da bola só troca no instante
            // em que ela chega ao destino — antes disso ela ainda é de quem tocou.
            var dono = fb >= 1 ? donoDaBola(b) : (bp.conduz ? bp.carregador : donoDaBola(a));

            return {
                i: i, f: f, passo: a, legenda: a.legenda, dono: dono,
                pecas: posic,
                bola: {
                    x: bx, y: by, fb: fb, giroInc: giroInc,
                    dist: bp.d, prog: f / prof.frac, arco: prof.arco,
                    mem: { cx: cx, cy: cy, fb: fb }
                }
            };
        }

        function quadro(t) {
            var est = estadoEm(t, { cx: bolaDom._cx, cy: bolaDom._cy, fb: bolaDom._fb });

            sb.elenco.forEach(function (j) {
                var el = pecasDom[j.id], pos = est.pecas[j.id];
                if (!el) return;
                if (!pos) { el.style.display = 'none'; return; }
                el.style.display = '';
                posicionar(el, pos.x, pos.y);
                passada(el, pos.x, pos.y);
                // O caminho é curvo: a direção do nariz vem do deslocamento do
                // quadro, não do vetor entre keyframes.
                orientar(el, pos.x - (el._ox == null ? pos.x : el._ox), pos.y - (el._oy == null ? pos.y : el._oy));
                el._ox = pos.x; el._oy = pos.y;
                el.classList.toggle('jgd-com-bola', j.id === est.dono);
            });

            giroBola += est.bola.giroInc;
            bolaDom._cx = est.bola.mem.cx;
            bolaDom._cy = est.bola.mem.cy;
            bolaDom._fb = est.bola.fb;
            posicionar(bolaDom, est.bola.x, est.bola.y);
            poseBola(est.bola.x, est.bola.y, est.bola.dist, est.bola.prog, est.bola.arco);

            // Setas são elementos SVG recriados do zero: redesenhar a cada quadro
            // custaria 60 rebuilds por segundo sem mudar nada na tela.
            if (est.i !== setasDesenhadas) { desenharSetas(est.passo); setasDesenhadas = est.i; }
            mostrarLegenda(est.legenda);
            return est.i;
        }

        // ── Player ──────────────────────────────────────────────────────────
        //
        // O relógio é um acumulador em milissegundos avançado pelo delta entre
        // quadros. Duas razões para não usar um instante de origem fixo:
        //
        //  • trocar a velocidade passa a valer no quadro seguinte, sem reiniciar a
        //    animação — antes ela só era aplicada por um parar()/tocar();
        //  • o timestamp do rAF é o início do quadro e pode ser anterior ao
        //    performance.now() lido no clique. Com origem fixa isso dava um delta
        //    negativo no primeiro quadro, que caía num tempo negativo.
        function tocar() {
            if (tocando || sb.passos.length < 2) return;
            planejar();
            tocando = true;
            host.classList.add('jgd-tocando');
            limparFantasmas();
            gCaminhos.innerHTML = '';   // a pré-visualização atrapalha o lance rodando

            // Cadência começa do zero a cada reprodução, senão a primeira passada
            // herdaria a fase da execução anterior e a peça sairia "no meio do passo".
            Object.keys(pecasDom).forEach(function (id) { pousar(pecasDom[id]); });
            bolaDom._fb = null;
            bolaDom._cx = null;
            bolaDom._cy = null;
            setasDesenhadas = -1;

            // Retoma de onde o scrub parou; se está no fim, recomeça.
            var t = tempo >= plano.total - 20 ? 0 : tempo;
            var anterior = null;

            function frame(agora) {
                if (!tocando) return;
                if (anterior === null) anterior = agora;   // 1º quadro só acerta o relógio

                t += Math.max(0, agora - anterior) * velocidade;
                anterior = agora;

                if (t >= plano.total) {
                    if (loop) t = 0;
                    else {
                        tempo = plano.total;
                        passoAtual = sb.passos.length - 1;
                        parar();
                        aoTrocarPasso(passoAtual);
                        return;
                    }
                }

                tempo = t;
                var i = quadro(t);
                if (i !== passoAtual) { passoAtual = i; aoTrocarPasso(i); }
                aoTempo(t, plano.total);
                raf = requestAnimationFrame(frame);
            }

            raf = requestAnimationFrame(frame);
        }

        function parar() {
            if (raf) cancelAnimationFrame(raf);
            raf = null;
            if (!tocando) return;
            tocando = false;
            host.classList.remove('jgd-tocando');
            setasDesenhadas = -1;
            renderizarPasso();
            aoTempo(tempo, plano ? plano.total : 0);
            aoParar();
        }

        // ── Desfazer / refazer ──────────────────────────────────────────────
        //
        // Snapshot do storyboard inteiro em JSON. É grosseiro, mas o storyboard
        // de uma jogada tem alguns KB e a alternativa — um diff por mutação —
        // custaria um tipo de comando para cada ação do editor.
        function snapshot() {
            if (!editavel) return;
            hist.push(JSON.stringify(sb));
            if (hist.length > 40) hist.shift();
            fut = [];
        }

        function restaurar(json) {
            parar();
            sb = normalizar(JSON.parse(json));
            passoAtual = limitar(passoAtual, 0, sb.passos.length - 1);
            setaOrigem = null;
            tempo = 0;
            sincronizarPecas();   // repinta seleção e blocos: o desfazer pode ter
                                  // criado ou desfeito um deles
            renderizarPasso();
            aoTrocarPasso(passoAtual);
            aoMudar();
        }

        function desfazer() {
            if (!hist.length) return;
            fut.push(JSON.stringify(sb));
            restaurar(hist.pop());
        }

        function refazer() {
            if (!fut.length) return;
            hist.push(JSON.stringify(sb));
            restaurar(fut.pop());
        }

        // ── Arrasto de peças e da bola (só no editor) ───────────────────────
        function iniciarArrasto(ev) {
            if (!editavel || tocando || modoSeta) return;
            if (ev.button !== 0 && ev.pointerType === 'mouse') return;

            var alvo = ev.target.closest('.jgd-peca, .jgd-bola');
            if (!alvo || !campo.contains(alvo)) return;
            if (ev.target.classList.contains('jgd-peca-remover')) return;

            ev.preventDefault();
            var p = passo();
            var ehBola = alvo.classList.contains('jgd-bola');
            var id = ehBola ? null : parseInt(alvo.dataset.id, 10);
            var dado = ehBola ? p.bola : pecaNoPasso(p, id);
            if (!dado) return;

            // Peça de um bloco entra na tela já com o setor inteiro selecionado: o
            // usuário vê o que vai andar antes de começar a arrastar.
            if (!ehBola) {
                if (ev.ctrlKey || ev.metaKey || ev.shiftKey) selecionar(id, true);
                else if (selecao.indexOf(id) < 0) {
                    var g = grupoDe(id);
                    if (g) selecionarVarios(g.ids); else selecionar(id);
                } else if (sel !== id) { sel = id; pintarSelecao(); aoSelecionar(sel); }
            }

            snapshot();

            // Arrasto em bloco: as outras peças acompanham o MESMO deslocamento,
            // então o setor (a linha de quatro, o triângulo do meio) chega inteiro
            // do outro lado — mover uma a uma sempre deformava o desenho.
            var juntos = ehBola ? [] : bloco(id).filter(function (i) { return i !== id; });
            var base = { x: dado.x, y: dado.y };
            var outros = juntos.map(function (i) {
                var c = pecaNoPasso(p, i);
                return c ? { c: c, x: c.x, y: c.y, el: pecasDom[i] } : null;
            }).filter(function (o) { return !!o; });

            // O bloco anda junto ou não anda: o limite de cada eixo é o da peça que
            // chega primeiro na linha de fundo, senão o setor se amassaria contra
            // a borda do campo.
            var minX = base.x, maxX = base.x, minY = base.y, maxY = base.y;
            outros.forEach(function (o) {
                minX = Math.min(minX, o.x); maxX = Math.max(maxX, o.x);
                minY = Math.min(minY, o.y); maxY = Math.max(maxY, o.y);
            });

            alvo.classList.add('jgd-arrastando');
            alvo.setPointerCapture(ev.pointerId);

            function mover(e) {
                var pos = pctDoEvento(e.clientX, e.clientY);
                var x = pos.x, y = pos.y;
                if (snap) { x = Math.round(x / 2.5) * 2.5; y = Math.round(y / 2.5) * 2.5; }
                var dx = limitar(x - base.x, -minX, 100 - maxX);
                var dy = limitar(y - base.y, -minY, 100 - maxY);

                dado.x = Math.round((base.x + dx) * 100) / 100;
                dado.y = Math.round((base.y + dy) * 100) / 100;
                posicionar(alvo, dado.x, dado.y);

                outros.forEach(function (o) {
                    o.c.x = Math.round((o.x + dx) * 100) / 100;
                    o.c.y = Math.round((o.y + dy) * 100) / 100;
                    if (o.el) posicionar(o.el, o.c.x, o.c.y);
                });

                desenharRastro();
                desenharBlocos();
            }

            function soltar(e) {
                alvo.classList.remove('jgd-arrastando');
                alvo.releasePointerCapture(e.pointerId);
                alvo.removeEventListener('pointermove', mover);
                alvo.removeEventListener('pointerup', soltar);
                alvo.removeEventListener('pointercancel', soltar);
                // Render completo, e não só o rastro: mover a bola (ou o jogador)
                // muda quem está com ela, e o anel de posse ficaria no dono antigo.
                renderizarPasso();
                aoMudar();
            }

            alvo.addEventListener('pointermove', mover);
            alvo.addEventListener('pointerup', soltar);
            alvo.addEventListener('pointercancel', soltar);
        }

        campo.addEventListener('pointerdown', iniciarArrasto);

        // ── Laço: selecionar várias peças arrastando no gramado vazio ────────
        //
        // É o caminho natural para pegar "a linha de zaga": em vez de Ctrl+clique
        // quatro vezes, cerca-se o setor. Clique seco no vazio limpa a seleção.
        function iniciarLaco(ev) {
            if (!editavel || tocando || modoSeta) return;
            if (ev.button !== 0 && ev.pointerType === 'mouse') return;
            if (ev.target.closest('.jgd-peca, .jgd-bola')) return;

            ev.preventDefault();
            var ini = pctDoEvento(ev.clientX, ev.clientY);
            var cx = null;

            function ret(a, b) {
                return {
                    x1: Math.min(a.x, b.x), y1: Math.min(a.y, b.y),
                    x2: Math.max(a.x, b.x), y2: Math.max(a.y, b.y)
                };
            }

            function mover(e) {
                var r = ret(ini, pctDoEvento(e.clientX, e.clientY));
                if (!cx && (r.x2 - r.x1 > 1 || r.y2 - r.y1 > 1)) {
                    cx = document.createElement('div');
                    cx.className = 'jgd-laco';
                    campo.appendChild(cx);
                }
                if (!cx) return;
                cx.style.left = r.x1 + '%';
                cx.style.top = r.y1 + '%';
                cx.style.width = (r.x2 - r.x1) + '%';
                cx.style.height = (r.y2 - r.y1) + '%';
            }

            function soltar(e) {
                campo.removeEventListener('pointermove', mover);
                campo.removeEventListener('pointerup', soltar);
                campo.removeEventListener('pointercancel', soltar);
                campo.releasePointerCapture(e.pointerId);

                if (!cx) { selecionar(null); return; }   // clique seco: limpa
                cx.remove();

                var r = ret(ini, pctDoEvento(e.clientX, e.clientY));
                var dentro = passo().pecas.filter(function (c) {
                    return c.x >= r.x1 && c.x <= r.x2 && c.y >= r.y1 && c.y <= r.y2;
                }).map(function (c) { return c.id; });

                if (dentro.length) selecionarVarios(dentro); else selecionar(null);
            }

            campo.setPointerCapture(ev.pointerId);
            campo.addEventListener('pointermove', mover);
            campo.addEventListener('pointerup', soltar);
            campo.addEventListener('pointercancel', soltar);
        }

        campo.addEventListener('pointerdown', iniciarLaco);

        campo.addEventListener('click', function (ev) {
            if (!editavel) return;

            if (ev.target.classList.contains('jgd-peca-remover')) {
                var el = ev.target.closest('.jgd-peca');
                if (el) api.removerJogador(parseInt(el.dataset.id, 10));
                return;
            }

            if (!modoSeta || tocando) return;
            if (ev.target.tagName === 'line') return;   // duplo clique na seta remove

            var pos = pctDoEvento(ev.clientX, ev.clientY);
            if (!setaOrigem) {
                setaOrigem = pos;
                return;
            }
            snapshot();
            passo().setas.push({ x1: setaOrigem.x, y1: setaOrigem.y, x2: pos.x, y2: pos.y });
            setaOrigem = null;
            desenharSetas(passo());
            aoMudar();
        });

        // Setas e rastros são desenhados em pixels a partir de %: refazer no resize
        // (o modal abre com o campo em tamanho zero até o layout assentar).
        var ro = new ResizeObserver(function () { if (!tocando) renderizarPasso(); });
        ro.observe(campo);

        // ── Teclado ─────────────────────────────────────────────────────────
        //
        // O arrasto acerta a posição no olho; as setas fazem o ajuste fino que o
        // mouse não alcança. O listener é global porque o campo não é focável —
        // daí as guardas: só quando este palco está visível e o foco não está num
        // campo de texto, senão as setas roubariam o cursor da legenda.
        function aoTeclado(ev) {
            if (!editavel || tocando) return;
            if (host.offsetParent === null) return;
            var alvo = ev.target;
            var tag = alvo && alvo.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || (alvo && alvo.isContentEditable)) {
                // Ctrl+Z num input é o desfazer do próprio campo de texto.
                return;
            }

            if ((ev.ctrlKey || ev.metaKey) && (ev.key === 'z' || ev.key === 'Z')) {
                ev.preventDefault();
                if (ev.shiftKey) refazer(); else desfazer();
                return;
            }
            if ((ev.ctrlKey || ev.metaKey) && (ev.key === 'y' || ev.key === 'Y')) {
                ev.preventDefault();
                refazer();
                return;
            }

            // Ctrl+G junta a seleção num bloco; Ctrl+Shift+G desfaz o bloco de
            // quem está selecionado. Mesmo par de atalhos de qualquer editor de
            // desenho, que é o que a prancheta é.
            if ((ev.ctrlKey || ev.metaKey) && (ev.key === 'g' || ev.key === 'G')) {
                ev.preventDefault();
                if (ev.shiftKey) api.desagrupar(); else api.agrupar();
                return;
            }

            if (ev.key === 'Escape' && selecao.length) { selecionar(null); return; }

            if (sel == null) return;
            if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].indexOf(ev.key) < 0) return;

            var p = passo();
            var ids = bloco(sel).filter(function (i) { return !!pecaNoPasso(p, i); });
            if (!ids.length) return;

            ev.preventDefault();
            var d = ev.shiftKey ? 2 : 0.5;
            var dx = (ev.key === 'ArrowLeft' ? -d : 0) + (ev.key === 'ArrowRight' ? d : 0);
            var dy = (ev.key === 'ArrowUp' ? -d : 0) + (ev.key === 'ArrowDown' ? d : 0);

            // Mesma regra do arrasto: o bloco anda junto, e para quando a peça
            // mais adiantada alcança a borda.
            var cs = ids.map(function (i) { return pecaNoPasso(p, i); });
            var xs = cs.map(function (c) { return c.x; }), ys = cs.map(function (c) { return c.y; });
            dx = limitar(dx, -Math.min.apply(null, xs), 100 - Math.max.apply(null, xs));
            dy = limitar(dy, -Math.min.apply(null, ys), 100 - Math.max.apply(null, ys));
            if (!dx && !dy) return;

            snapshot();
            cs.forEach(function (c) {
                c.x = Math.round((c.x + dx) * 100) / 100;
                c.y = Math.round((c.y + dy) * 100) / 100;
            });
            renderizarPasso();
            aoMudar();
        }

        if (editavel) document.addEventListener('keydown', aoTeclado);

        // ── API ─────────────────────────────────────────────────────────────
        var api = {
            campo: campo,

            storyboard: function () { return sb; },
            passos: function () { return sb.passos; },
            passoAtual: function () { return passoAtual; },
            tocando: function () { return tocando; },
            elenco: function () { return sb.elenco; },

            carregar: function (novo) {
                parar();
                sb = normalizar(novo);
                passoAtual = 0;
                setaOrigem = null;
                tempo = 0;
                sel = null;
                selecao = [];
                hist = [];
                fut = [];
                sincronizarPecas();
                renderizarPasso();
                aoTrocarPasso(passoAtual);
                aoTempo(0, plano.total);
                aoSelecionar(null);
            },

            irParaPasso: function (i) {
                parar();
                passoAtual = limitar(i, 0, sb.passos.length - 1);
                setaOrigem = null;
                renderizarPasso();
                tempo = plano.ini[passoAtual] || 0;
                aoTrocarPasso(passoAtual);
                aoTempo(tempo, plano.total);
            },

            // Passo novo herda as posições do atual: o usuário só move quem andou.
            adicionarPasso: function () {
                parar();
                snapshot();
                sb.passos.splice(passoAtual + 1, 0, clonarPasso(passo()));
                passoAtual++;
                renderizarPasso();
                aoTrocarPasso(passoAtual);
                aoMudar();
            },

            removerPasso: function () {
                if (sb.passos.length <= 1) return;
                parar();
                snapshot();
                sb.passos.splice(passoAtual, 1);
                passoAtual = limitar(passoAtual, 0, sb.passos.length - 1);
                renderizarPasso();
                aoTrocarPasso(passoAtual);
                aoMudar();
            },

            definirLegenda: function (texto) {
                passo().legenda = texto;
                aoMudar();
            },

            // ── Motor v2: tipo de bola do trecho e ritmo/atraso da peça ─────
            definirTipoPasse: function (tipo) {
                if (!PERFIL[tipo]) return;
                snapshot();
                passo().passe = tipo;
                renderizarPasso();
                aoMudar();
            },

            // O ritmo acompanha a peça pelos passos seguintes: quem entrou em
            // sprint num lance costuma seguir em sprint até parar.
            // Aceita um id ou uma lista: com um bloco selecionado, o ritmo vale
            // para o setor inteiro (uma linha de quatro sobe toda em sprint).
            definirModo: function (id, modo) {
                if (id == null || !RITMO[modo]) return;
                var ids = Array.isArray(id) ? id : [id];
                snapshot();
                for (var i = passoAtual; i < sb.passos.length; i++) {
                    for (var k = 0; k < ids.length; k++) {
                        var c = pecaNoPasso(sb.passos[i], ids[k]);
                        if (c) c.modo = modo;
                    }
                }
                renderizarPasso();
                aoMudar();
            },

            // `registrar` separa o arrasto do slider (input contínuo) do valor
            // final (change): sem isso cada pixel do slider viraria um snapshot.
            definirAtraso: function (id, atraso, registrar) {
                var ids = Array.isArray(id) ? id : [id];
                var cs = ids.map(function (i) { return pecaNoPasso(passo(), i); })
                    .filter(function (c) { return !!c; });
                if (!cs.length) return;
                if (registrar) snapshot();
                cs.forEach(function (c) { c.atraso = limitar(atraso, 0, 0.6); });
                renderizarPasso();
                aoMudar();
            },

            selecionar: selecionar,
            selecionarVarios: selecionarVarios,
            selecionada: function () { return sel; },
            selecao: function () { return selecao.slice(); },
            pecaSelecionada: function () { return sel == null ? null : pecaNoPasso(passo(), sel); },

            // ── Blocos ──────────────────────────────────────────────────────
            grupos: function () { return sb.grupos; },
            grupoDe: grupoDe,
            corDoGrupo: function (g) { return COR_BLOCO[indiceDoGrupo(g) % COR_BLOCO.length]; },

            // Vira bloco quem está selecionado agora. Como cada jogador só pode
            // estar num bloco, os membros saem dos blocos antigos — agrupar de novo
            // é a forma de remontar um setor sem precisar desagrupar antes.
            agrupar: function () {
                if (selecao.length < 2) return null;
                snapshot();
                var ids = selecao.slice();
                sb.grupos.forEach(function (g) {
                    g.ids = g.ids.filter(function (i) { return ids.indexOf(i) < 0; });
                });
                sb.grupos = sb.grupos.filter(function (g) { return g.ids.length >= 2; });
                if (sb.grupos.length >= MAX_GRUPOS) sb.grupos.shift();
                var g = { id: 'g' + Date.now().toString(36), nome: '', ids: ids };
                sb.grupos.push(g);
                pintarSelecao();
                renderizarPasso();
                aoMudar();
                return g;
            },

            // Desfaz os blocos que tocam a seleção (ou o da peça selecionada).
            desagrupar: function () {
                var alvos = selecao.length ? selecao : (sel == null ? [] : [sel]);
                var mexeu = false;
                sb.grupos.forEach(function (g) {
                    if (g.ids.some(function (i) { return alvos.indexOf(i) >= 0; })) { g.ids = []; mexeu = true; }
                });
                if (!mexeu) return false;
                snapshot();
                sb.grupos = sb.grupos.filter(function (g) { return g.ids.length >= 2; });
                pintarSelecao();
                renderizarPasso();
                aoMudar();
                return true;
            },

            definirMotor: function (v) {
                motor = v === 'v1' ? 'v1' : 'v2';
                parar();
                tempo = 0;
                renderizarPasso();
                aoTempo(0, plano.total);
            },

            motor: function () { return motor; },

            alternarCaminhos: function () {
                verCaminhos = !verCaminhos;
                desenharCaminhos();
                return verCaminhos;
            },

            alternarSnap: function () { snap = !snap; return snap; },

            // ── Cores das camisas e fotos ───────────────────────────────────
            cores: function () { return { nos: sb.cores.nos, adv: sb.cores.adv }; },

            definirCor: function (lado, cor, registrar) {
                if (!corHex(cor, null)) return;
                if (registrar) snapshot();
                sb.cores[lado === 'adv' ? 'adv' : 'nos'] = cor;
                aplicarCores();
                if (registrar) aoMudar();
            },

            alternarFotos: function () {
                verFotos = !verFotos;
                sincronizarPecas();
                renderizarPasso();
                return verFotos;
            },

            fotos: function () { return verFotos; },

            desfazer: desfazer,
            refazer: refazer,
            podeDesfazer: function () { return hist.length > 0; },
            podeRefazer: function () { return fut.length > 0; },

            // Scrub: pausa e pinta o instante t sem mexer no storyboard.
            irParaTempo: function (t) {
                if (tocando) parar();
                if (!plano || sb.passos.length < 2) return;
                tempo = limitar(t, 0, plano.total);
                var i = quadro(tempo);
                if (i !== passoAtual) { passoAtual = i; aoTrocarPasso(i); }
            },

            duracaoTotal: function () { return plano ? plano.total : 0; },

            // Cálculo puro de um instante da jogada — o exportador de vídeo pinta
            // no canvas exatamente o que estadoEm() devolve para a tela.
            estadoEm: function (t, ant) { planejar(); return estadoEm(t, ant); },
            duracaoDoPasso: function (i) { return plano && plano.durs[i] != null ? plano.durs[i] : null; },
            tempo: function () { return tempo; },

            // Jogador entra em TODOS os passos na mesma posição: ele passa a existir
            // na jogada inteira e o usuário reposiciona onde precisar. Sem isso a
            // peça surgiria do nada no meio da animação.
            adicionarJogador: function (jogador, x, y, adv) {
                if (sb.elenco.some(function (j) { return j.id === jogador.id; })) return;
                snapshot();
                sb.elenco.push({
                    id: jogador.id, num: jogador.num || '',
                    nome: jogador.nome || '', sigla: jogador.sigla || '', adv: !!adv,
                    foto: jogador.foto || ''
                });
                sb.passos.forEach(function (p) {
                    p.pecas.push({ id: jogador.id, x: x, y: y, modo: 'trote', atraso: 0 });
                });
                sincronizarPecas();
                renderizarPasso();
                aoMudar();
                aoMudarElenco();
            },

            removerJogador: function (id) {
                snapshot();
                if (sel === id) selecionar(null);
                sb.elenco = sb.elenco.filter(function (j) { return j.id !== id; });
                // Bloco que fica com um membro só deixa de ser bloco.
                sb.grupos.forEach(function (g) {
                    g.ids = g.ids.filter(function (i) { return i !== id; });
                });
                sb.grupos = sb.grupos.filter(function (g) { return g.ids.length >= 2; });
                sb.passos.forEach(function (p) {
                    p.pecas = p.pecas.filter(function (c) { return c.id !== id; });
                });
                sincronizarPecas();
                renderizarPasso();
                aoMudar();
                aoMudarElenco();
            },

            // O loop lê a velocidade a cada quadro: não precisa reiniciar nada.
            definirVelocidade: function (v) { velocidade = v || 1; },

            definirLoop: function (v) { loop = v; },

            definirDuracao: function (ms) { sb.ms = ms; renderizarPasso(); aoMudar(); },

            alternarModoSeta: function () {
                modoSeta = !modoSeta;
                setaOrigem = null;
                campo.classList.toggle('jgd-modo-seta', modoSeta);
                return modoSeta;
            },

            modoSeta: function () { return modoSeta; },
            cancelarSeta: function () { setaOrigem = null; },

            tocar: tocar,
            parar: parar,
            alternarPlay: function () { if (tocando) parar(); else tocar(); },

            destruir: function () {
                parar();
                ro.disconnect();
                if (editavel) document.removeEventListener('keydown', aoTeclado);
                host.innerHTML = '';
            }
        };

        return api;
    }

    // ══ Exportar a jogada em vídeo ══════════════════════════════════════════
    //
    // O palco anima com DOM (divs em % dentro do gramado), e DOM não vira arquivo
    // de vídeo. Então a jogada é REDESENHADA num canvas quadro a quadro e o
    // MediaRecorder grava o stream desse canvas. O que garante que o vídeo é a
    // mesma jogada da tela é o motor: as posições saem de palco.estadoEm(), o
    // mesmo cálculo que o player usa — aqui só muda quem pinta.
    //
    // A gravação roda em tempo real (o MediaRecorder carimba os quadros pelo
    // relógio da máquina): uma jogada de 6s leva 6s para exportar. Por isso o
    // canvas aparece numa janela na frente do usuário, com o progresso — trocar
    // de aba no meio pausa o requestAnimationFrame e engasgaria o vídeo.

    var VID_LARGURA = 1280;                                  // 720p-ish na proporção do campo
    var VID_ALTURA = Math.round(VID_LARGURA / 1.62);
    var VID_FPS = 30;

    // Preferência do navegador, em ordem: MP4 abre em qualquer lugar (inclusive
    // no celular e nos editores de vídeo), WebM é o fallback universal do Chrome.
    var VID_TIPOS = [
        { mime: 'video/mp4;codecs=avc1.42E01E', ext: 'mp4' },
        { mime: 'video/mp4', ext: 'mp4' },
        { mime: 'video/webm;codecs=vp9', ext: 'webm' },
        { mime: 'video/webm;codecs=vp8', ext: 'webm' },
        { mime: 'video/webm', ext: 'webm' }
    ];

    function tipoDeVideo() {
        if (typeof MediaRecorder === 'undefined') return null;
        for (var i = 0; i < VID_TIPOS.length; i++) {
            if (MediaRecorder.isTypeSupported(VID_TIPOS[i].mime)) return VID_TIPOS[i];
        }
        return null;
    }

    // As fotos são <img> do MediaProxy (mesma origem), então o canvas não fica
    // "tainted" e o vídeo sai com os rostos. Quem falhar simplesmente volta a ser
    // o número — uma foto quebrada não pode derrubar a exportação inteira.
    function carregarFotos(elenco) {
        return Promise.all(elenco.map(function (j) {
            return new Promise(function (ok) {
                if (!j.foto) return ok(null);
                var img = new Image();
                img.onload = function () { ok({ id: j.id, img: img }); };
                img.onerror = function () { ok(null); };
                img.src = j.foto;
            });
        })).then(function (rs) {
            var m = {};
            rs.forEach(function (r) { if (r) m[r.id] = r.img; });
            return m;
        });
    }

    // ── Desenho do campo e das peças em canvas ──────────────────────────────
    // Reproduz o gramado do CSS (.jgd-campo e filhos) nas mesmas proporções, para
    // o vídeo não parecer outra prancheta.
    function vidCampo(ctx, W, H) {
        var g = ctx.createLinearGradient(0, 0, W, 0);
        g.addColorStop(0, '#14532d'); g.addColorStop(0.5, '#166534'); g.addColorStop(1, '#14532d');
        ctx.fillStyle = g;
        ctx.fillRect(0, 0, W, H);

        // Faixas do corte da grama: só existem no vídeo, onde o gramado é o fundo
        // inteiro do quadro e uma cor chapada fica sem escala.
        ctx.fillStyle = 'rgba(255,255,255,.022)';
        for (var i = 0; i < 10; i += 2) ctx.fillRect((W / 10) * i, 0, W / 10, H);

        ctx.strokeStyle = 'rgba(255,255,255,.22)';
        ctx.lineWidth = Math.max(1.5, W / 640);

        ctx.strokeRect(ctx.lineWidth, ctx.lineWidth, W - ctx.lineWidth * 2, H - ctx.lineWidth * 2);

        ctx.beginPath();
        ctx.moveTo(W / 2, 0); ctx.lineTo(W / 2, H);
        ctx.stroke();

        ctx.beginPath();
        ctx.arc(W / 2, H / 2, H * 0.15, 0, Math.PI * 2);
        ctx.stroke();

        // Grandes áreas (13% da largura, de 20% a 80% da altura) e as pequenas
        ctx.strokeRect(0, H * 0.2, W * 0.13, H * 0.6);
        ctx.strokeRect(W - W * 0.13, H * 0.2, W * 0.13, H * 0.6);
        ctx.fillStyle = 'rgba(255,255,255,.05)';
        ctx.fillRect(0, H * 0.38, W * 0.045, H * 0.24);
        ctx.fillRect(W - W * 0.045, H * 0.38, W * 0.045, H * 0.24);
        ctx.strokeRect(0, H * 0.38, W * 0.045, H * 0.24);
        ctx.strokeRect(W - W * 0.045, H * 0.38, W * 0.045, H * 0.24);

        ctx.fillStyle = 'rgba(255,255,255,.4)';
        ctx.font = '700 ' + Math.round(H / 45) + 'px system-ui, Arial';
        ctx.textAlign = 'right';
        ctx.fillText('ATAQUE →', W - 14, H / 32);
        ctx.textAlign = 'center';
    }

    function vidPeca(ctx, W, H, j, pos, opts) {
        var x = (pos.x / 100) * W, y = (pos.y / 100) * H;
        var r = opts.raio;
        var cor = j.adv ? opts.corAdv : opts.corNos;
        var foto = opts.fotos[j.id];

        ctx.save();

        // Sombra no chão, como a da peça na tela
        ctx.fillStyle = 'rgba(0,0,0,.4)';
        ctx.beginPath();
        ctx.ellipse(x, y + r * 0.92, r * 0.72, r * 0.24, 0, 0, Math.PI * 2);
        ctx.fill();

        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.closePath();

        if (foto) {
            // Com foto o disco é a moldura: a imagem preenche o círculo e a cor do
            // time fica na borda (mesma leitura do match-up e da prancheta).
            ctx.save();
            ctx.clip();
            var lado = r * 2;
            ctx.drawImage(foto, x - r, y - r, lado, lado);
            ctx.restore();
            ctx.lineWidth = r * 0.17;
            ctx.strokeStyle = cor;
            ctx.stroke();
        } else {
            ctx.fillStyle = cor;
            ctx.fill();
            ctx.lineWidth = r * 0.13;
            ctx.strokeStyle = 'rgba(255,255,255,.5)';
            ctx.stroke();
        }

        // Anel de posse
        if (opts.comBola) {
            ctx.lineWidth = r * 0.16;
            ctx.strokeStyle = 'rgba(255,255,255,.95)';
            ctx.beginPath();
            ctx.arc(x, y, r * 1.16, 0, Math.PI * 2);
            ctx.stroke();
        }

        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        if (foto) {
            // Número no badge, no canto de cima
            var bw = r * 0.95, bh = r * 0.62, bx = x - r * 1.05, by = y - r * 1.05;
            ctx.fillStyle = 'rgba(0,0,0,.82)';
            ctx.beginPath();
            ctx.roundRect ? ctx.roundRect(bx, by, bw, bh, bh / 2) : ctx.rect(bx, by, bw, bh);
            ctx.fill();
            ctx.fillStyle = '#fff';
            ctx.font = '800 ' + Math.round(bh * 0.78) + 'px system-ui, Arial';
            ctx.fillText(j.num || '', bx + bw / 2, by + bh / 2 + 0.5);
        } else {
            ctx.fillStyle = '#0b0f16';
            ctx.font = '800 ' + Math.round(r * 0.95) + 'px system-ui, Arial';
            ctx.fillText(j.num || '', x, y + 0.5);
        }

        // Sigla acima, nome abaixo — com sombra, porque o gramado é claro em partes
        ctx.shadowColor = 'rgba(0,0,0,.9)';
        ctx.shadowBlur = 3;
        ctx.shadowOffsetY = 1;
        ctx.fillStyle = j.adv ? 'rgba(191,219,254,.85)' : 'rgba(255,255,255,.7)';
        ctx.font = '700 ' + Math.round(r * 0.62) + 'px system-ui, Arial';
        ctx.fillText((j.sigla || '').toUpperCase(), x, y - r * 1.55);
        ctx.fillStyle = '#fff';
        ctx.font = '600 ' + Math.round(r * 0.66) + 'px system-ui, Arial';
        ctx.fillText(j.nome || '', x, y + r * 1.62);

        ctx.restore();
    }

    function vidBola(ctx, W, H, bola) {
        var x = (bola.x / 100) * W, y = (bola.y / 100) * H;
        var r = Math.max(5, W / 145);
        // Altura do lance: a mesma curva da tela (poseBola), em px do vídeo
        var alto = bola.arco > 0
            ? Math.sin(Math.PI * Math.max(0, Math.min(1, bola.prog))) * Math.min(bola.dist * 0.5, 18) * bola.arco
            : 0;
        var altoPx = (alto / 100) * H * 1.6;

        ctx.save();
        ctx.fillStyle = 'rgba(0,0,0,' + (alto > 0.5 ? 0.22 : 0.42) + ')';
        ctx.beginPath();
        ctx.ellipse(x, y, r * 0.9, r * 0.42, 0, 0, Math.PI * 2);
        ctx.fill();

        var cy = y - altoPx;
        var esc = 1 + altoPx / (H * 0.6);
        var g = ctx.createRadialGradient(x - r * 0.35, cy - r * 0.4, r * 0.1, x, cy, r * esc);
        g.addColorStop(0, '#fff'); g.addColorStop(0.55, '#e5e7eb'); g.addColorStop(1, '#9ca3af');
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(x, cy, r * esc, 0, Math.PI * 2);
        ctx.fill();
        ctx.lineWidth = 1;
        ctx.strokeStyle = 'rgba(0,0,0,.25)';
        ctx.stroke();
        ctx.restore();
    }

    function vidSetas(ctx, W, H, setas) {
        ctx.save();
        ctx.strokeStyle = '#facc15';
        ctx.fillStyle = '#facc15';
        ctx.lineWidth = Math.max(2, W / 420);
        setas.forEach(function (s) {
            var x1 = (s.x1 / 100) * W, y1 = (s.y1 / 100) * H;
            var x2 = (s.x2 / 100) * W, y2 = (s.y2 / 100) * H;
            var ang = Math.atan2(y2 - y1, x2 - x1);
            var p = Math.max(9, W / 110);   // ponta
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2 - Math.cos(ang) * p * 0.8, y2 - Math.sin(ang) * p * 0.8);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(x2, y2);
            ctx.lineTo(x2 - Math.cos(ang - 0.42) * p, y2 - Math.sin(ang - 0.42) * p);
            ctx.lineTo(x2 - Math.cos(ang + 0.42) * p, y2 - Math.sin(ang + 0.42) * p);
            ctx.closePath();
            ctx.fill();
        });
        ctx.restore();
    }

    function vidLegenda(ctx, W, H, texto) {
        if (!texto) return;
        ctx.save();
        ctx.font = '600 ' + Math.round(H / 26) + 'px system-ui, Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        var larg = Math.min(W * 0.9, ctx.measureText(texto).width + H / 14);
        var alt = H / 15, x = (W - larg) / 2, y = H - alt - H / 40;
        ctx.fillStyle = 'rgba(0,0,0,.72)';
        ctx.beginPath();
        ctx.roundRect ? ctx.roundRect(x, y, larg, alt, alt / 3) : ctx.rect(x, y, larg, alt);
        ctx.fill();
        ctx.strokeStyle = 'rgba(255,255,255,.14)';
        ctx.lineWidth = 1;
        ctx.stroke();
        ctx.fillStyle = '#facc15';
        ctx.fillText(texto, W / 2, y + alt / 2 + 1);
        ctx.restore();
    }

    // Marca d'água com o nome da jogada e os times: o vídeo sai do sistema e vai
    // para um story ou uma reunião, onde ninguém tem o contexto da tela.
    function vidTitulo(ctx, W, H, meta) {
        if (!meta || !meta.titulo) return;
        ctx.save();
        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';
        ctx.shadowColor = 'rgba(0,0,0,.85)';
        ctx.shadowBlur = 4;
        ctx.fillStyle = 'rgba(255,255,255,.92)';
        ctx.font = '800 ' + Math.round(H / 30) + 'px system-ui, Arial';
        ctx.fillText(meta.titulo, 16, 12);
        if (meta.sub) {
            ctx.fillStyle = 'rgba(255,255,255,.62)';
            ctx.font = '600 ' + Math.round(H / 44) + 'px system-ui, Arial';
            ctx.fillText(meta.sub, 16, 12 + H / 26);
        }
        ctx.restore();
    }

    function vidQuadro(ctx, W, H, palco, est, opts) {
        vidCampo(ctx, W, H);
        vidSetas(ctx, W, H, (est.passo && est.passo.setas) || []);

        var elenco = palco.storyboard().elenco;
        // A bola fica sob as peças quando alguém a conduz e sobre elas em voo:
        // desenhar sempre por cima faria a bola conduzida cobrir o rosto.
        var noAr = est.bola.arco > 0 && est.bola.prog > 0 && est.bola.prog < 1;
        if (!noAr) vidBola(ctx, W, H, est.bola);

        elenco.forEach(function (j) {
            var pos = est.pecas[j.id];
            if (!pos) return;
            vidPeca(ctx, W, H, j, pos, {
                raio: opts.raio, corNos: opts.corNos, corAdv: opts.corAdv,
                fotos: opts.fotos, comBola: est.dono === j.id
            });
        });

        if (noAr) vidBola(ctx, W, H, est.bola);
        vidLegenda(ctx, W, H, est.legenda);
        vidTitulo(ctx, W, H, opts.meta);
    }

    // O MediaRecorder do Firefox só grava WebM, e WebM é recusado no upload do
    // Instagram, do TikTok e do WhatsApp — justamente para onde este vídeo vai.
    // Então o arquivo passa pelo ffmpeg do servidor e volta MP4, e o formato final
    // deixa de depender de qual navegador o usuário abriu.
    //
    // Se o servidor não tiver ffmpeg (503), fica o WebM: um arquivo que toca no
    // computador é melhor que exportação nenhuma.
    async function paraMp4(r, aoAviso) {
        if (r.ext === 'mp4') return r;
        if (aoAviso) aoAviso('convertendo');

        var fd = new FormData();
        fd.append('arquivo', r.blob, 'jogada.webm');

        var resp;
        try {
            resp = await fetch('/Jogadas/ConverterMp4', { method: 'POST', body: fd });
        } catch (e) {
            return Object.assign({}, r, { aviso: 'Sem resposta do servidor na conversão — o arquivo saiu em WebM.' });
        }

        if (!resp.ok) {
            var motivo = resp.status === 503
                ? 'Este servidor não tem ffmpeg instalado'
                : 'A conversão falhou no servidor';
            return Object.assign({}, r, { aviso: motivo + ' — o arquivo saiu em WebM.' });
        }

        var mp4 = await resp.blob();
        return { blob: mp4, ext: 'mp4', canvas: r.canvas };
    }

    // Grava a jogada inteira e devolve { blob, ext }. `aoProgresso(0..1)` é
    // chamado a cada quadro, e `aoQuadro` recebe o canvas para quem quiser
    // mostrá-lo enquanto grava.
    function gravarVideo(palco, meta, aoProgresso) {
        var tipo = tipoDeVideo();
        if (!tipo) return Promise.reject(new Error('Este navegador não grava vídeo (MediaRecorder indisponível).'));

        var total = palco.duracaoTotal();
        if (!total) return Promise.reject(new Error('A jogada tem um passo só: não há movimento para gravar.'));

        var sb = palco.storyboard();
        var canvas = document.createElement('canvas');
        canvas.width = VID_LARGURA;
        canvas.height = VID_ALTURA;
        var ctx = canvas.getContext('2d');

        return carregarFotos(sb.elenco).then(function (fotos) {
            var opts = {
                raio: VID_LARGURA / 62,
                corNos: (sb.cores && sb.cores.nos) || COR_PADRAO.nos,
                corAdv: (sb.cores && sb.cores.adv) || COR_PADRAO.adv,
                fotos: fotos,
                meta: meta
            };

            var stream = canvas.captureStream(VID_FPS);
            var rec = new MediaRecorder(stream, { mimeType: tipo.mime, videoBitsPerSecond: 6000000 });
            var pedacos = [];
            rec.ondataavailable = function (e) { if (e.data && e.data.size) pedacos.push(e.data); };

            return new Promise(function (resolve, reject) {
                rec.onerror = function (e) { reject(e.error || new Error('Falha ao gravar.')); };
                rec.onstop = function () {
                    resolve({ blob: new Blob(pedacos, { type: tipo.mime }), ext: tipo.ext, canvas: canvas });
                };

                // Um respiro no fim: sem ele o vídeo corta no instante em que a
                // bola chega, e quem assiste não vê o desfecho do lance.
                var CAUDA = 700;
                var ini = performance.now(), mem = null, parado = false;

                // O relógio é o de parede, e não a contagem de quadros: se a
                // máquina engasgar (ou a janela sair da frente, onde o navegador
                // estrangula os timers), a jogada continua no tempo certo e o
                // MediaRecorder repete o último quadro — o vídeo fica com um
                // engasgo em vez de sair em câmera lenta.
                //
                // setTimeout, e não requestAnimationFrame: rAF PARA de disparar
                // com a aba em segundo plano, o que deixaria a gravação pendurada
                // para sempre em vez de apenas perder qualidade.
                function passo() {
                    if (parado) return;
                    var t = performance.now() - ini;
                    var est = palco.estadoEm(Math.min(t, total), mem);
                    mem = est.bola.mem;

                    vidQuadro(ctx, VID_LARGURA, VID_ALTURA, palco, est, opts);
                    if (aoProgresso) aoProgresso(Math.min(1, t / (total + CAUDA)), canvas);

                    if (t < total + CAUDA) setTimeout(passo, 1000 / VID_FPS);
                    else { parado = true; rec.stop(); }
                }

                rec.start();
                passo();
            });
        });
    }

    // ══ Modal do editor (/Jogos/Analisar) ═══════════════════════════════════
    var M = {
        jogoId: null,
        dados: null,        // { casa: {...}, visitante: {...} } vindo de /Jogadas/DoJogo
        lado: 'casa',
        palco: null,
        jogadaId: null,
        sujo: false,
        carregado: false
    };

    function ladoAtual() { return M.dados ? M.dados[M.lado === 'casa' ? 'casa' : 'visitante'] : null; }

    // O outro time da partida — o elenco de marcação da jogada.
    function adversario() { return M.dados ? M.dados[M.lado === 'casa' ? 'visitante' : 'casa'] : null; }

    // Lista que o painel do elenco está mostrando no momento.
    function elencoAlvo() {
        return M.elencoAlvo === 'adv'
            ? { time: adversario(), adv: true }
            : { time: ladoAtual(), adv: false };
    }

    // A escalação vem do servidor no referencial de quem ataca para a direita.
    // O adversário defende do outro lado, então entra girado 180° — é assim que
    // os dois times se encaram, como numa transmissão.
    function posicaoDaEscalacao(e, adv) {
        return adv ? { x: 100 - e.x, y: 100 - e.y } : { x: e.x, y: e.y };
    }

    function el(id) { return document.getElementById(id); }

    async function abrirModal(jogoId) {
        M.jogoId = jogoId;
        var modal = el('modal-jogadas');
        if (!modal) return;
        modal.style.display = 'flex';
        aplicarMaximizado(maximizadoSalvo());

        if (M.carregado) return;

        var corpo = el('jgd-corpo');
        corpo.innerHTML = '<div class="jgd-vazio" style="grid-column:1/-1;">Carregando…</div>';

        try {
            var resp = await fetch('/Jogadas/DoJogo/' + jogoId);
            if (!resp.ok) throw new Error();
            M.dados = await resp.json();
            M.carregado = true;
            montarModal();
        } catch (e) {
            corpo.innerHTML = '<div class="jgd-vazio" style="grid-column:1/-1;color:#f87171;">' +
                'Erro ao carregar as jogadas deste jogo.</div>';
        }
    }

    // ── Maximizar o modal ───────────────────────────────────────────────────
    //
    // A prancheta é a tela onde o tamanho do campo importa mais: quanto maior o
    // gramado, mais preciso fica o arrasto das peças. A preferência fica gravada
    // porque quem monta jogada costuma trabalhar sempre do mesmo jeito.
    var CHAVE_MAX = 'jgd-maximizado';

    // Ver fotos é preferência de quem desenha (não faz parte da jogada), então
    // fica no localStorage junto com a de maximizar.
    var CHAVE_FOTOS = 'jgd-fotos';

    function fotosSalvas() {
        try { return localStorage.getItem(CHAVE_FOTOS) !== '0'; } catch (e) { return true; }
    }

    function aplicarMaximizado(v) {
        var box = document.querySelector('#modal-jogadas .jgd-box');
        var btn = el('jgd-maximizar');
        if (!box) return;
        box.classList.toggle('jgd-maximizado', v);
        if (btn) {
            btn.innerHTML = v ? '&#10529;' : '&#9974;';
            btn.title = v ? 'Restaurar o tamanho normal' : 'Maximizar (ocupa a tela inteira)';
        }
    }

    function maximizadoSalvo() {
        try { return localStorage.getItem(CHAVE_MAX) === '1'; } catch (e) { return false; }
    }

    function alternarMaximizar() {
        var box = document.querySelector('#modal-jogadas .jgd-box');
        if (!box) return;
        var v = !box.classList.contains('jgd-maximizado');
        aplicarMaximizado(v);
        try { localStorage.setItem(CHAVE_MAX, v ? '1' : '0'); } catch (e) { /* modo privado */ }
        // O campo mudou de tamanho: setas, rastros e caminhos são desenhados em
        // pixels e precisam ser refeitos. O ResizeObserver do palco já cuida disso.
    }

    function fecharModal() {
        if (M.sujo && !confirm('A jogada tem alterações não salvas. Fechar mesmo assim?')) return;
        if (M.palco) M.palco.parar();
        var modal = el('modal-jogadas');
        if (modal) modal.style.display = 'none';
    }

    function montarModal() {
        var abas = el('jgd-abas');
        abas.innerHTML = ['casa', 'visitante'].map(function (k) {
            var t = M.dados[k];
            return '<button type="button" class="jgd-aba' + (k === M.lado ? ' ativa' : '') + '" data-lado="' + k + '">' +
                (t.escudo ? '<img src="' + esc(t.escudo) + '" alt="">' : '') +
                '<span>' + esc(t.nome) + '</span>' +
                '<span class="jgd-aba-contagem">' + t.jogadas.length + '</span>' +
                '</button>';
        }).join('');

        abas.querySelectorAll('.jgd-aba').forEach(function (b) {
            b.addEventListener('click', function () { trocarLado(b.dataset.lado); });
        });

        el('jgd-corpo').innerHTML =
            '<div class="jgd-lista" id="jgd-lista"></div>' +
            '<div class="jgd-editor" id="jgd-editor"></div>';

        renderizarLista();
        mostrarEditorVazio();
    }

    function trocarLado(lado) {
        if (lado === M.lado) return;
        if (M.sujo && !confirm('A jogada tem alterações não salvas. Trocar de time mesmo assim?')) return;
        M.lado = lado;
        M.sujo = false;
        M.jogadaId = null;
        el('jgd-abas').querySelectorAll('.jgd-aba').forEach(function (b) {
            b.classList.toggle('ativa', b.dataset.lado === lado);
        });
        renderizarLista();
        mostrarEditorVazio();
    }

    function atualizarContagens() {
        el('jgd-abas').querySelectorAll('.jgd-aba').forEach(function (b) {
            var t = M.dados[b.dataset.lado];
            b.querySelector('.jgd-aba-contagem').textContent = t.jogadas.length;
        });
    }

    function renderizarLista() {
        var t = ladoAtual();
        var lista = el('jgd-lista');

        var itens = t.jogadas.map(function (j) {
            var n = (j.storyboard && j.storyboard.passos) ? j.storyboard.passos.length : 0;
            return '<div class="jgd-item' + (j.id === M.jogadaId ? ' ativa' : '') + '" data-id="' + j.id + '">' +
                '<div class="jgd-item-nome">' + esc(j.nome) + '</div>' +
                '<div class="jgd-item-meta">' + n + (n === 1 ? ' passo' : ' passos') + '</div>' +
                '</div>';
        }).join('');

        lista.innerHTML =
            '<button type="button" class="jgd-btn-nova" id="jgd-btn-nova">＋ Nova jogada</button>' +
            (itens || '<div class="jgd-lista-vazia">Nenhuma jogada desenhada para ' + esc(t.nome) + ' ainda.</div>');

        el('jgd-btn-nova').addEventListener('click', novaJogada);
        lista.querySelectorAll('.jgd-item').forEach(function (item) {
            item.addEventListener('click', function () { abrirJogada(parseInt(item.dataset.id, 10)); });
        });
    }

    function mostrarEditorVazio() {
        if (M.palco) { M.palco.destruir(); M.palco = null; }
        el('jgd-editor').innerHTML =
            '<div class="jgd-vazio">' +
            '<div class="jgd-vazio-icone">🎬</div>' +
            '<div>Escolha uma jogada na lista ou crie uma nova.<br>' +
            'Você monta o lance em passos: posiciona o time e a bola, clica em <b>＋ Passo</b>, ' +
            'move quem se deslocou e a bola, e repete.<br>No fim, <b>▶ Reproduzir</b> anima a jogada inteira.</div>' +
            '</div>';
    }

    function novaJogada() {
        if (M.sujo && !confirm('A jogada aberta tem alterações não salvas. Criar outra mesmo assim?')) return;

        var t = ladoAtual();
        // Começa com a escalação titular do jogo em campo: quase toda jogada parte
        // dela, e montar 11 peças na mão a cada jogada nova seria insuportável.
        var sb = storyboardVazio();
        sb.elenco = t.escalacao.map(function (e) {
            return { id: e.id, num: e.num, nome: e.nome, sigla: e.sigla, foto: e.foto || '' };
        });
        sb.passos[0].pecas = t.escalacao.map(function (e) {
            return { id: e.id, x: e.x, y: e.y };
        });

        M.jogadaId = null;
        abrirEditor({ id: null, nome: '', descricao: '', storyboard: sb });
    }

    function abrirJogada(id) {
        if (M.sujo && !confirm('A jogada aberta tem alterações não salvas. Abrir outra mesmo assim?')) return;
        var j = ladoAtual().jogadas.find(function (x) { return x.id === id; });
        if (!j) return;
        M.jogadaId = id;
        renderizarLista();
        abrirEditor(j);
    }

    function abrirEditor(jogada) {
        var t = ladoAtual();

        // Salvar uma jogada nova reabre o editor: sem destruir o palco anterior,
        // o ResizeObserver e o listener de teclado dele ficariam vivos sobre um
        // storyboard que ninguém mais vê.
        if (M.palco) { M.palco.destruir(); M.palco = null; }

        el('jgd-editor').innerHTML =
            '<div class="jgd-barra">' +
                '<input type="text" class="jgd-nome-input" id="jgd-nome" maxlength="80"' +
                    ' placeholder="Nome da jogada (ex: saída de bola pela direita)" value="' + esc(jogada.nome || '') + '">' +
                '<div class="jgd-sep"></div>' +
                '<button type="button" class="jgd-btn" id="jgd-add-passo" title="Cria um passo copiando as posições atuais">＋ Passo</button>' +
                '<div class="jgd-passo-nav">' +
                    '<button type="button" id="jgd-ant" title="Passo anterior">‹</button>' +
                    '<span id="jgd-contador">1/1</span>' +
                    '<button type="button" id="jgd-prox" title="Próximo passo">›</button>' +
                '</div>' +
                '<button type="button" class="jgd-btn jgd-btn-perigo" id="jgd-del-passo" title="Excluir o passo atual">🗑</button>' +
                '<div class="jgd-sep"></div>' +
                '<button type="button" class="jgd-btn" id="jgd-desfazer" title="Desfazer (Ctrl+Z)">↶</button>' +
                '<button type="button" class="jgd-btn" id="jgd-refazer" title="Refazer (Ctrl+Shift+Z)">↷</button>' +
                '<div class="jgd-sep"></div>' +
                '<button type="button" class="jgd-btn" id="jgd-seta" title="Ligue e clique em dois pontos do campo para traçar uma seta. Duplo clique numa seta remove.">➹ Setas</button>' +
                '<button type="button" class="jgd-btn jgd-btn-bloco" id="jgd-bloco">⛓ Bloco</button>' +
                '<div class="jgd-sep"></div>' +
                '<button type="button" class="jgd-btn jgd-btn-play" id="jgd-play">▶ Reproduzir</button>' +
                '<select class="jgd-vel" id="jgd-vel" title="Velocidade da animação">' +
                    '<option value="0.5">0,5×</option>' +
                    '<option value="1" selected>1×</option>' +
                    '<option value="1.5">1,5×</option>' +
                    '<option value="2">2×</option>' +
                '</select>' +
                '<button type="button" class="jgd-btn ativo" id="jgd-loop" title="Repetir em loop">🔁</button>' +
                '<div class="jgd-espaco"></div>' +
                '<button type="button" class="jgd-btn jgd-btn-video" id="jgd-video"' +
                    ' title="Grava a jogada animada num arquivo de vídeo para postar ou mostrar na reunião">🎥 Vídeo</button>' +
                '<button type="button" class="jgd-btn jgd-btn-salvar" id="jgd-salvar">💾 Salvar</button>' +
                (jogada.id ? '<button type="button" class="jgd-btn jgd-btn-perigo" id="jgd-excluir">🗑 Excluir</button>' : '') +
            '</div>' +
            '<div class="jgd-dica-modo" id="jgd-dica-seta" style="display:none;">' +
                'clique no ponto de origem e depois no destino · duplo clique na seta remove · Esc sai' +
            '</div>' +
            '<div class="jgd-motor">' +
                '<span class="jgd-motor-rotulo">Motor de movimento</span>' +
                '<div class="jgd-seg" id="jgd-seg-motor">' +
                    '<button type="button" data-motor="v1">atual</button>' +
                    '<button type="button" data-motor="v2" class="ativo">novo</button>' +
                '</div>' +
                '<span class="jgd-motor-desc" id="jgd-motor-desc"></span>' +
                '<button type="button" class="jgd-btn jgd-btn-ciano" id="jgd-caminhos"' +
                    ' title="Desenha o trajeto que cada peça e a bola vão percorrer">⤳ Caminhos</button>' +
                '<button type="button" class="jgd-btn jgd-btn-ciano" id="jgd-snap"' +
                    ' title="Arredonda o arrasto para uma grade de 2,5%">⌗ Snap</button>' +
                '<div class="jgd-sep"></div>' +
                // Cor da camisa de cada lado e fotos dos jogadores: a prancheta
                // passa a ler como a escalação do match-up, onde o rosto e a cor
                // do time identificam mais rápido que o número sozinho.
                '<span class="jgd-motor-rotulo">Cores</span>' +
                '<input type="color" class="jgd-cor-time" id="jgd-cor-nos"' +
                    ' title="Cor da camisa do ' + esc(t.nome) + '">' +
                '<input type="color" class="jgd-cor-time" id="jgd-cor-adv"' +
                    ' title="Cor da camisa do ' + esc(adversario().nome) + ' (marcação)">' +
                '<button type="button" class="jgd-btn jgd-btn-ciano" id="jgd-fotos"' +
                    ' title="Mostra a foto do jogador no lugar do número">🖼 Fotos</button>' +
            '</div>' +
            '<div class="jgd-palco">' +
                '<div class="jgd-elenco">' +
                    '<div class="jgd-elenco-abas">' +
                        '<button type="button" class="jgd-elenco-aba ativa" data-alvo="nos" title="' + esc(t.nome) + '">' + esc(t.nome) + '</button>' +
                        '<button type="button" class="jgd-elenco-aba jgd-aba-adv" data-alvo="adv" title="' + esc(adversario().nome) + ' (adversário)">' + esc(adversario().nome) + '</button>' +
                    '</div>' +
                    '<input type="text" class="jgd-elenco-busca" id="jgd-busca" placeholder="🔍 Buscar jogador...">' +
                    '<div class="jgd-elenco-lista" id="jgd-elenco-lista"></div>' +
                    '<button type="button" class="jgd-elenco-todos" id="jgd-add-escalacao"></button>' +
                '</div>' +
                '<div class="jgd-campo-wrap" id="jgd-campo-host"></div>' +
            '</div>' +
            '<div class="jgd-scrub-linha">' +
                '<span class="jgd-scrub-tempo" id="jgd-relogio">0,0s</span>' +
                '<input type="range" class="jgd-scrub" id="jgd-scrub" min="0" max="1000" step="1" value="0"' +
                    ' title="Arraste para percorrer a jogada quadro a quadro">' +
                '<span class="jgd-scrub-tempo jgd-scrub-total" id="jgd-total">0,0s</span>' +
            '</div>' +
            '<div class="jgd-timeline" id="jgd-timeline"></div>' +
            '<div class="jgd-paineis">' +
                '<div class="jgd-painel">' +
                    '<div class="jgd-painel-tit" id="jgd-painel-passo">Passo 1</div>' +
                    '<div class="jgd-painel-linha">' +
                        '<span class="jgd-painel-rot">Tipo de bola</span>' +
                        '<select class="jgd-vel" id="jgd-passe" title="Como a bola vai deste passo para o próximo">' +
                            '<option value="toque">toque rasteiro</option>' +
                            '<option value="passe">passe</option>' +
                            '<option value="conducao">condução / drible</option>' +
                            '<option value="cruzamento">cruzamento</option>' +
                            '<option value="lancamento">lançamento</option>' +
                            '<option value="chute">chute</option>' +
                        '</select>' +
                        '<span class="jgd-painel-dur" id="jgd-dur">—</span>' +
                    '</div>' +
                    '<input type="text" class="jgd-legenda-input" id="jgd-legenda" maxlength="120"' +
                        ' placeholder="Legenda deste passo (ex: goleiro toca no lateral) — aparece na reprodução">' +
                '</div>' +
                '<div class="jgd-painel">' +
                    '<div class="jgd-painel-tit jgd-tit-ciano">Peça selecionada</div>' +
                    '<div class="jgd-painel-nome" id="jgd-sel-nome">nenhuma — clique numa peça no campo</div>' +
                    '<div class="jgd-painel-linha" id="jgd-sel-controles">' +
                        '<button type="button" class="jgd-btn jgd-btn-ciano" data-modo="andar">andar</button>' +
                        '<button type="button" class="jgd-btn jgd-btn-ciano" data-modo="trote">trote</button>' +
                        '<button type="button" class="jgd-btn jgd-btn-ciano" data-modo="sprint">sprint</button>' +
                        '<span class="jgd-painel-rot" title="Fração do passo que a peça espera antes de sair">sai em</span>' +
                        '<input type="range" class="jgd-atraso" id="jgd-atraso" min="0" max="60" step="5" value="0">' +
                        '<span class="jgd-painel-val" id="jgd-atraso-val">—</span>' +
                    '</div>' +
                '</div>' +
            '</div>' +
            '<div class="jgd-rodape-dica">' +
                'Arraste os jogadores do elenco para o campo (ou clique neles) · arraste as peças e a bola para montar cada passo · ' +
                'clique numa peça para selecioná-la e ajustar ritmo e atraso · as setas do teclado empurram a peça selecionada ' +
                '(0,5% — 2% com Shift) · <b>Ctrl+Z</b> desfaz · passe o mouse numa peça e clique no × para tirá-la da jogada.<br>' +
                '<b>🎥 Vídeo</b> grava a jogada animada num arquivo (MP4, com as fotos e as cores da prancheta) ' +
                'para postar ou levar para a reunião — a gravação corre no tempo da jogada, então deixe a janela na frente.<br>' +
                '<b>Blocos:</b> cerque um setor com o mouse no gramado (ou Ctrl+clique nas peças) e use <b>⛓ Bloco</b> ' +
                '(<b>Ctrl+G</b>) — a linha de quatro, o triângulo do meio ou o que você marcar passa a andar junto, ' +
                'mantendo a forma, no arrasto e nas setas do teclado. <b>Ctrl+Shift+G</b> desfaz.<br>' +
                'A aba <b>' + esc(adversario().nome) + '</b> traz a marcação (peças azuis): o botão <b>＋ escalação do ' +
                esc(adversario().nome) + '</b> põe os 11 de uma vez, na formação deste jogo e virados contra.' +
            '</div>';

        M.elencoAlvo = 'nos';

        M.palco = criarPalco(el('jgd-campo-host'), {
            editavel: true,
            fotos: fotosSalvas(),
            aoMudar: function () { marcarSujo(true); renderizarTimeline(); atualizarBarra(); },
            aoMudarElenco: renderizarElenco,
            aoTrocarPasso: function () { renderizarTimeline(); atualizarBarra(); },
            aoTempo: atualizarScrub,
            aoSelecionar: atualizarPainelPeca,
            aoParar: function () { el('jgd-play').textContent = '▶ Reproduzir'; }
        });

        M.palco.carregar(jogada.storyboard);
        M.sujo = false;

        ligarEventosEditor(jogada);
        renderizarElenco();
        renderizarTimeline();
        atualizarBarra();
    }

    function marcarSujo(v) {
        M.sujo = v;
        var b = el('jgd-salvar');
        if (b) b.textContent = v ? '💾 Salvar *' : '💾 Salvar';
    }

    function ligarEventosEditor(jogada) {
        el('jgd-add-passo').addEventListener('click', function () { M.palco.adicionarPasso(); });
        el('jgd-del-passo').addEventListener('click', function () {
            if (M.palco.passos().length <= 1) { alert('A jogada precisa de pelo menos um passo.'); return; }
            if (confirm('Excluir o passo ' + (M.palco.passoAtual() + 1) + '?')) M.palco.removerPasso();
        });
        el('jgd-ant').addEventListener('click', function () { M.palco.irParaPasso(M.palco.passoAtual() - 1); });
        el('jgd-prox').addEventListener('click', function () { M.palco.irParaPasso(M.palco.passoAtual() + 1); });

        el('jgd-desfazer').addEventListener('click', function () { M.palco.desfazer(); });
        el('jgd-refazer').addEventListener('click', function () { M.palco.refazer(); });

        // Segmento atual/novo: existe para comparar o mesmo lance nos dois motores
        // antes de aposentar o antigo de vez.
        el('jgd-seg-motor').querySelectorAll('button').forEach(function (b) {
            b.addEventListener('click', function () {
                M.palco.definirMotor(b.dataset.motor);
                el('jgd-seg-motor').querySelectorAll('button').forEach(function (o) {
                    o.classList.toggle('ativo', o === b);
                });
                el('jgd-play').textContent = '▶ Reproduzir';
                atualizarBarra();
            });
        });

        el('jgd-caminhos').addEventListener('click', function () {
            this.classList.toggle('ativo', M.palco.alternarCaminhos());
        });

        el('jgd-snap').addEventListener('click', function () {
            this.classList.toggle('ativo', M.palco.alternarSnap());
        });

        el('jgd-passe').addEventListener('change', function () {
            M.palco.definirTipoPasse(this.value);
        });

        el('jgd-sel-controles').querySelectorAll('[data-modo]').forEach(function (b) {
            b.addEventListener('click', function () {
                M.palco.definirModo(alvosDoPainel(), b.dataset.modo);
            });
        });

        // input pinta o valor enquanto arrasta; change registra um único snapshot
        // no fim, para o desfazer não voltar de 5 em 5%.
        el('jgd-atraso').addEventListener('input', function () {
            M.palco.definirAtraso(alvosDoPainel(), parseInt(this.value, 10) / 100, false);
        });
        el('jgd-atraso').addEventListener('change', function () {
            M.palco.definirAtraso(alvosDoPainel(), parseInt(this.value, 10) / 100, true);
        });

        el('jgd-scrub').addEventListener('input', function () {
            var total = M.palco.duracaoTotal();
            M.palco.irParaTempo((parseInt(this.value, 10) / 1000) * total);
            el('jgd-play').textContent = '▶ Reproduzir';
            el('jgd-relogio').textContent = (M.palco.tempo() / 1000).toFixed(1).replace('.', ',') + 's';
        });

        // Um botão só: com um bloco na seleção ele desfaz o bloco, senão junta o
        // que estiver selecionado. Quem decide é o estado da seleção, e o rótulo
        // (atualizarBotaoBloco) diz o que vai acontecer antes do clique.
        // input pinta enquanto o usuário arrasta o seletor; change registra o
        // desfazer e marca a jogada como alterada, uma vez só.
        ['nos', 'adv'].forEach(function (lado) {
            var inp = el('jgd-cor-' + lado);
            inp.value = M.palco.cores()[lado];
            inp.addEventListener('input', function () { M.palco.definirCor(lado, this.value, false); });
            inp.addEventListener('change', function () { M.palco.definirCor(lado, this.value, true); });
        });

        el('jgd-video').addEventListener('click', function () { exportarVideo(jogada); });

        el('jgd-fotos').addEventListener('click', function () {
            var v = M.palco.alternarFotos();
            this.classList.toggle('ativo', v);
            try { localStorage.setItem(CHAVE_FOTOS, v ? '1' : '0'); } catch (e) { /* modo privado */ }
        });
        el('jgd-fotos').classList.toggle('ativo', M.palco.fotos());

        el('jgd-bloco').addEventListener('click', function () {
            if (blocoNaSelecao()) M.palco.desagrupar();
            else M.palco.agrupar();
            atualizarPainelPeca(M.palco.selecionada());
        });

        el('jgd-seta').addEventListener('click', function () {
            var ativo = M.palco.alternarModoSeta();
            this.classList.toggle('ativo', ativo);
            el('jgd-dica-seta').style.display = ativo ? '' : 'none';
        });

        el('jgd-play').addEventListener('click', function () {
            if (M.palco.passos().length < 2) {
                alert('Crie pelo menos dois passos para a jogada ter movimento.');
                return;
            }
            M.palco.alternarPlay();
            this.textContent = M.palco.tocando() ? '⏸ Pausar' : '▶ Reproduzir';
        });

        el('jgd-vel').addEventListener('change', function () {
            M.palco.definirVelocidade(parseFloat(this.value));
        });

        el('jgd-loop').addEventListener('click', function () {
            var ativo = !this.classList.contains('ativo');
            this.classList.toggle('ativo', ativo);
            M.palco.definirLoop(ativo);
        });

        el('jgd-nome').addEventListener('input', function () { marcarSujo(true); });

        el('jgd-legenda').addEventListener('input', function () {
            M.palco.definirLegenda(this.value);
            renderizarTimeline();
        });

        el('jgd-busca').addEventListener('input', renderizarElenco);
        el('jgd-salvar').addEventListener('click', function () { salvarJogada(jogada); });

        document.querySelectorAll('.jgd-elenco-aba').forEach(function (aba) {
            aba.addEventListener('click', function () {
                M.elencoAlvo = aba.dataset.alvo;
                document.querySelectorAll('.jgd-elenco-aba').forEach(function (o) {
                    o.classList.toggle('ativa', o === aba);
                });
                el('jgd-busca').value = '';
                renderizarElenco();
            });
        });

        el('jgd-add-escalacao').addEventListener('click', function () {
            var alvo = elencoAlvo();
            alvo.time.escalacao.forEach(function (e) {
                var p = posicaoDaEscalacao(e, alvo.adv);
                M.palco.adicionarJogador(e, p.x, p.y, alvo.adv);
            });
        });

        var btnExcluir = el('jgd-excluir');
        if (btnExcluir) btnExcluir.addEventListener('click', function () { excluirJogada(jogada); });

        // Solta o jogador arrastado do elenco no ponto exato onde o mouse largou.
        var campo = M.palco.campo;
        campo.addEventListener('dragover', function (ev) { ev.preventDefault(); });
        campo.addEventListener('drop', function (ev) {
            ev.preventDefault();
            var id = parseInt(ev.dataTransfer.getData('text/plain'), 10);
            if (!id) return;
            var r = campo.getBoundingClientRect();
            adicionarDoElenco(id,
                limitar(((ev.clientX - r.left) / r.width) * 100, 0, 100),
                limitar(((ev.clientY - r.top) / r.height) * 100, 0, 100));
        });
    }

    function adicionarDoElenco(id, x, y) {
        var alvo = elencoAlvo();
        var j = alvo.time.elenco.find(function (e) { return e.id === id; });
        if (!j) return;
        M.palco.adicionarJogador(j, Math.round(x * 100) / 100, Math.round(y * 100) / 100, alvo.adv);
    }

    function renderizarElenco() {
        var alvo = elencoAlvo();
        var busca = (el('jgd-busca').value || '').toLowerCase().trim();
        var emCampo = {};
        M.palco.elenco().forEach(function (j) { emCampo[j.id] = true; });

        el('jgd-elenco-lista').classList.toggle('jgd-lista-adv', alvo.adv);

        // O botão vale para as DUAS abas: na do adversário ele entra com os 11 da
        // marcação, na formação do jogo e girados 180° (posicaoDaEscalacao). O
        // rótulo diz o nome do time porque, genérico, ninguém descobria que dava
        // para escalar o visitante inteiro em vez de arrastar jogador por jogador.
        var btnEsc = el('jgd-add-escalacao');
        var qtd = alvo.time.escalacao.length;
        btnEsc.disabled = !qtd;
        btnEsc.textContent = qtd
            ? '＋ escalação do ' + alvo.time.nome + ' (' + qtd + ')'
            : '＋ escalação do ' + alvo.time.nome;
        btnEsc.title = qtd
            ? 'Põe em campo os ' + qtd + ' titulares do ' + alvo.time.nome +
              ' de uma vez, na formação deste jogo' +
              (alvo.adv ? ' — virados contra, como marcação.' : '.')
            : 'Este jogo ainda não tem escalação titular do ' + alvo.time.nome + '.';

        var lista = alvo.time.elenco.filter(function (j) {
            if (!busca) return true;
            return (j.nome || '').toLowerCase().indexOf(busca) >= 0 || String(j.num || '').indexOf(busca) >= 0;
        });

        el('jgd-elenco-lista').innerHTML = lista.length
            ? lista.map(function (j) {
                var dentro = !!emCampo[j.id];
                return '<div class="jgd-elenco-item' + (dentro ? ' jgd-em-campo' : '') + '"' +
                    (dentro ? '' : ' draggable="true"') + ' data-id="' + j.id + '"' +
                    ' title="' + (dentro ? 'Já está na jogada' : 'Arraste para o campo ou clique para incluir') + '">' +
                    '<span class="jgd-elenco-num">' + esc(j.num || '–') + '</span>' +
                    '<span class="jgd-elenco-nome">' + esc(j.nome) + '</span>' +
                    '<span class="jgd-elenco-sigla">' + esc(j.sigla) + '</span>' +
                    '</div>';
            }).join('')
            : '<div class="jgd-lista-vazia">Nenhum jogador encontrado.</div>';

        el('jgd-elenco-lista').querySelectorAll('.jgd-elenco-item').forEach(function (item) {
            if (item.classList.contains('jgd-em-campo')) return;
            var id = parseInt(item.dataset.id, 10);
            item.addEventListener('dragstart', function (ev) {
                ev.dataTransfer.setData('text/plain', String(id));
                ev.dataTransfer.effectAllowed = 'copy';
            });
            // Clique é o atalho de quem não quer mirar: entra no meio-campo e o
            // usuário arrasta de lá (também é o caminho que funciona no toque).
            item.addEventListener('click', function () { adicionarDoElenco(id, 50, 50); });
        });
    }

    function renderizarTimeline() {
        var tl = el('jgd-timeline');
        if (!tl) return;
        var atual = M.palco.passoAtual();

        tl.innerHTML = M.palco.passos().map(function (p, i) {
            var rotulo = p.legenda ? (i + 1) + '. ' + p.legenda : 'Passo ' + (i + 1);
            return '<button type="button" class="jgd-chip' + (i === atual ? ' ativo' : '') + '" data-i="' + i + '"' +
                ' title="' + esc(rotulo) + '">' + esc(rotulo) + '</button>';
        }).join('');

        tl.querySelectorAll('.jgd-chip').forEach(function (c) {
            c.addEventListener('click', function () { M.palco.irParaPasso(parseInt(c.dataset.i, 10)); });
        });
    }

    function segundos(ms) { return (ms / 1000).toFixed(1).replace('.', ',') + 's'; }

    function atualizarBarra() {
        if (!M.palco || !el('jgd-contador')) return;
        var i = M.palco.passoAtual(), n = M.palco.passos().length;
        var p = M.palco.passos()[i];
        var v2 = M.palco.motor() === 'v2';

        el('jgd-contador').textContent = (i + 1) + '/' + n;
        el('jgd-ant').disabled = i === 0;
        el('jgd-prox').disabled = i >= n - 1;
        el('jgd-del-passo').disabled = n <= 1;
        el('jgd-desfazer').disabled = !M.palco.podeDesfazer();
        el('jgd-refazer').disabled = !M.palco.podeRefazer();
        atualizarBotaoBloco();

        // Escrever no input a cada mudança mandaria o cursor para o fim enquanto
        // o usuário digita a legenda.
        var leg = el('jgd-legenda');
        if (leg.value !== (p.legenda || '')) leg.value = p.legenda || '';

        el('jgd-motor-desc').textContent = v2
            ? 'corrida contínua entre passos, caminho curvo, duração por distância, ritmo e atraso por peça, bola colada em quem conduz.'
            : 'como era antes: todo passo dura o mesmo, easing em cada trecho (a peça para em todo keyframe) e trajetória reta.';

        // O trecho é do passo atual PARA o próximo: no último não há trecho.
        var ultimo = i >= n - 1;
        el('jgd-painel-passo').textContent = ultimo ? 'Passo ' + n + ' (final)' : 'Passo ' + (i + 1) + ' → ' + (i + 2);
        el('jgd-passe').value = p.passe || 'passe';
        el('jgd-passe').disabled = ultimo;

        var dur = M.palco.duracaoDoPasso(i);
        el('jgd-dur').textContent = dur == null ? '—' : (v2 ? 'auto · ' : 'fixo · ') + Math.round(dur) + ' ms';

        atualizarPainelPeca(M.palco.selecionada());
        atualizarScrub(M.palco.tempo(), M.palco.duracaoTotal());
    }

    // Peças que os controles de ritmo e atraso atingem: a seleção inteira, para
    // acertar o setor de uma vez em vez de peça por peça.
    function alvosDoPainel() {
        var s = M.palco.selecao();
        return s.length ? s : M.palco.selecionada();
    }

    function blocoNaSelecao() {
        var s = M.palco.selecao();
        var id = s.length ? s : (M.palco.selecionada() == null ? [] : [M.palco.selecionada()]);
        return id.some(function (i) { return !!M.palco.grupoDe(i); });
    }

    // Rótulo do botão da barra: diz o que o clique vai fazer com a seleção atual.
    function atualizarBotaoBloco() {
        var b = el('jgd-bloco');
        if (!b || !M.palco) return;
        var n = M.palco.selecao().length;
        var temBloco = blocoNaSelecao();

        b.classList.toggle('ativo', temBloco);
        b.disabled = !temBloco && n < 2;
        b.textContent = temBloco ? '⛓ Desagrupar' : (n >= 2 ? '⛓ Agrupar (' + n + ')' : '⛓ Bloco');
        b.title = temBloco
            ? 'Desfaz o bloco das peças selecionadas (Ctrl+Shift+G)'
            : 'Selecione duas ou mais peças (Ctrl+clique, ou cerque com o mouse no gramado) '
              + 'e clique para que elas passem a se mover juntas, mantendo a forma (Ctrl+G)';
    }

    function atualizarPainelPeca(id) {
        if (!M.palco || !el('jgd-sel-nome')) return;
        var c = M.palco.pecaSelecionada();
        var j = id == null ? null : M.palco.elenco().find(function (e) { return e.id === id; });
        var n = M.palco.selecao().length;

        atualizarBotaoBloco();

        el('jgd-sel-nome').textContent = n > 1
            ? n + ' peças selecionadas' + (blocoNaSelecao() ? ' · bloco' : '') +
              (j ? ' (' + j.nome + ' é a de referência)' : '')
            : (j ? (j.num ? j.num + ' · ' : '') + j.nome : 'nenhuma — clique numa peça no campo');

        var modo = c ? (c.modo || 'trote') : null;
        el('jgd-sel-controles').querySelectorAll('[data-modo]').forEach(function (b) {
            b.classList.toggle('ativo', b.dataset.modo === modo);
            b.disabled = !c;
        });

        var slider = el('jgd-atraso');
        slider.disabled = !c;
        var v = c ? Math.round((c.atraso || 0) * 100) : 0;
        if (parseInt(slider.value, 10) !== v) slider.value = String(v);
        el('jgd-atraso-val').textContent = c ? v + '%' : '—';
    }

    // Exporta a jogada aberta. A gravação é em tempo real e depende do
    // requestAnimationFrame, então a janela fica na frente mostrando o canvas que
    // está sendo gravado: é o aviso de "não troque de aba agora" que funciona.
    async function exportarVideo(jogada) {
        if (!M.palco) return;
        if (M.palco.passos().length < 2) {
            alert('A jogada precisa de pelo menos dois passos para virar vídeo — é o movimento entre eles que a animação mostra.');
            return;
        }

        M.palco.parar();

        var nome = (el('jgd-nome').value || '').trim() || 'jogada';
        var t = ladoAtual();
        var meta = { titulo: nome, sub: t.nome + ' × ' + adversario().nome };

        var painel = document.createElement('div');
        painel.className = 'jgd-video-overlay';
        painel.innerHTML =
            '<div class="jgd-video-caixa">' +
                '<div class="jgd-video-tit">🎥 Gravando o vídeo…</div>' +
                '<div class="jgd-video-tela" id="jgd-video-tela"></div>' +
                '<div class="jgd-video-barra"><i id="jgd-video-prog"></i></div>' +
                '<div class="jgd-video-nota" id="jgd-video-nota">' +
                    'A gravação acontece em tempo real, no ritmo da jogada — deixe esta aba visível até o fim.' +
                '</div>' +
                '<button type="button" class="jgd-btn" id="jgd-video-fechar">Fechar</button>' +
            '</div>';
        document.body.appendChild(painel);

        var fechar = function () { painel.remove(); };
        painel.querySelector('#jgd-video-fechar').addEventListener('click', fechar);

        var tela = painel.querySelector('#jgd-video-tela');
        var prog = painel.querySelector('#jgd-video-prog');
        var nota = painel.querySelector('#jgd-video-nota');
        var montado = false;

        try {
            var r = await gravarVideo(M.palco, meta, function (p, canvas) {
                if (!montado) { tela.appendChild(canvas); montado = true; }
                prog.style.width = Math.round(p * 100) + '%';
            });

            r = await paraMp4(r, function () {
                painel.querySelector('.jgd-video-tit').textContent = '🎞️ Convertendo para MP4…';
                nota.textContent = 'Este navegador grava em WebM; o servidor está convertendo o arquivo para MP4.';
            });

            var arquivo = nome.replace(/[^\p{L}\p{N} _-]/gu, '').trim().replace(/\s+/g, '-').toLowerCase()
                || 'jogada';
            var url = URL.createObjectURL(r.blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = arquivo + '.' + r.ext;
            document.body.appendChild(a);
            a.click();
            a.remove();
            // O objectURL segura o blob inteiro na memória: solta depois que o
            // download já começou.
            setTimeout(function () { URL.revokeObjectURL(url); }, 30000);

            painel.querySelector('.jgd-video-tit').textContent = '✅ Vídeo pronto';
            var tam = r.blob.size >= 1048576
                ? (r.blob.size / 1048576).toFixed(1) + ' MB'
                : Math.max(1, Math.round(r.blob.size / 1024)) + ' KB';
            nota.innerHTML = 'Baixado como <b>' + esc(arquivo + '.' + r.ext) + '</b> · ' + tam + ' · ' +
                (M.palco.duracaoTotal() / 1000).toFixed(1).replace('.', ',') + 's. ' +
                (r.aviso ? '<br><b>' + esc(r.aviso) + '</b> ' : '') +
                'Se o download não começou, verifique o bloqueio de downloads do navegador.';
        } catch (e) {
            painel.querySelector('.jgd-video-tit').textContent = '❌ Não deu para gravar';
            nota.textContent = (e && e.message) || 'Falha ao gravar o vídeo.';
        }
    }

    function atualizarScrub(t, total) {
        var barra = el('jgd-scrub');
        if (!barra) return;
        barra.value = String(Math.round((t / Math.max(1, total)) * 1000));
        barra.disabled = total <= 0;
        el('jgd-relogio').textContent = segundos(t);
        el('jgd-total').textContent = segundos(total);
    }

    async function salvarJogada(jogada) {
        var nome = el('jgd-nome').value.trim();
        if (!nome) { alert('Dê um nome para a jogada.'); el('jgd-nome').focus(); return; }

        var t = ladoAtual();
        var btn = el('jgd-salvar');
        btn.disabled = true;
        btn.textContent = 'Salvando…';

        try {
            var resp = await fetch('/Jogadas/Salvar', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    id: jogada.id,
                    jogoId: M.jogoId,
                    timeId: t.timeId,
                    nome: nome,
                    descricao: jogada.descricao || null,
                    storyboard: M.palco.storyboard()
                })
            });

            if (!resp.ok) {
                alert('Erro ao salvar a jogada: ' + (await resp.text() || resp.status));
                return;
            }

            var salva = await resp.json();

            if (jogada.id) {
                var i = t.jogadas.findIndex(function (x) { return x.id === salva.id; });
                if (i >= 0) t.jogadas[i] = salva;
            } else {
                t.jogadas.push(salva);
                jogada.id = salva.id;
            }

            M.jogadaId = salva.id;
            marcarSujo(false);
            renderizarLista();
            atualizarContagens();

            // Reabre para o botão Excluir aparecer na jogada que acabou de nascer.
            if (!el('jgd-excluir')) abrirEditor(t.jogadas.find(function (x) { return x.id === salva.id; }));
        } catch (e) {
            alert('Erro de conexão ao salvar a jogada.');
        } finally {
            btn.disabled = false;
            if (el('jgd-salvar') === btn) marcarSujo(M.sujo);
        }
    }

    async function excluirJogada(jogada) {
        if (!jogada.id) return;
        if (!confirm('Excluir a jogada "' + jogada.nome + '"? Isso não pode ser desfeito.')) return;

        try {
            var resp = await fetch('/Jogadas/Excluir', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: jogada.id })
            });
            if (!resp.ok) { alert('Erro ao excluir a jogada.'); return; }

            var t = ladoAtual();
            t.jogadas = t.jogadas.filter(function (x) { return x.id !== jogada.id; });
            M.jogadaId = null;
            M.sujo = false;
            renderizarLista();
            atualizarContagens();
            mostrarEditorVazio();
        } catch (e) {
            alert('Erro de conexão ao excluir a jogada.');
        }
    }

    // Esc: sai do modo seta antes de fechar o modal — quem está traçando espera
    // cancelar o traço, não perder a tela inteira.
    document.addEventListener('keydown', function (ev) {
        if (ev.key !== 'Escape') return;
        var modal = el('modal-jogadas');
        if (!modal || modal.style.display === 'none' || !modal.style.display) return;
        if (M.palco && M.palco.modoSeta()) {
            M.palco.alternarModoSeta();
            var b = el('jgd-seta');
            if (b) b.classList.remove('ativo');
            var d = el('jgd-dica-seta');
            if (d) d.style.display = 'none';
            return;
        }
        fecharModal();
    });

    // ══ Arsenal (/Times/Details) ════════════════════════════════════════════
    // Só reprodução: cada jogada salva vira um card com o próprio player.
    async function montarArsenal(containerId, timeId) {
        var cont = document.getElementById(containerId);
        if (!cont) return;
        cont.innerHTML = '<div class="jgd-arsenal-vazio">Carregando jogadas…</div>';

        try {
            var resp = await fetch('/Jogadas/DoTime/' + timeId);
            if (!resp.ok) throw new Error();
            var jogadas = await resp.json();

            if (!jogadas.length) {
                cont.innerHTML = '<div class="jgd-arsenal-vazio">' +
                    'Nenhuma jogada desenhada para este time ainda.<br>' +
                    'Abra a análise de um jogo dele e use <b>🎬 Jogadas</b> para montar a primeira.' +
                    '</div>';
                return;
            }

            cont.innerHTML = '<div class="jgd-arsenal-grid">' + jogadas.map(function (j, i) {
                var n = (j.storyboard.passos || []).length;
                var origem = [j.adversario ? 'vs ' + esc(j.adversario) : '', j.data || ''].filter(Boolean).join(' · ');
                return '<div class="jgd-arsenal-card">' +
                    '<div class="jgd-arsenal-head">' +
                        '<span class="jgd-arsenal-nome">' + esc(j.nome) + '</span>' +
                        '<span class="jgd-arsenal-meta">' + n + (n === 1 ? ' passo' : ' passos') + '</span>' +
                    '</div>' +
                    '<div class="jgd-campo-wrap" id="jgd-arsenal-campo-' + i + '"></div>' +
                    (j.descricao ? '<p class="jgd-arsenal-desc">' + esc(j.descricao) + '</p>' : '') +
                    '<div class="jgd-arsenal-acoes">' +
                        '<button type="button" class="jgd-btn jgd-btn-play" data-play="' + i + '">▶ Reproduzir</button>' +
                        '<span class="jgd-arsenal-meta">' + esc(origem) + '</span>' +
                        '<a class="jgd-btn" style="text-decoration:none; margin-left:auto;"' +
                          ' href="/Jogos/Analisar/' + j.jogoId + '" title="Abrir a análise onde a jogada foi desenhada">✎ Editar</a>' +
                    '</div>' +
                    '</div>';
            }).join('') + '</div>';

            jogadas.forEach(function (j, i) {
                var host = document.getElementById('jgd-arsenal-campo-' + i);
                var palco = criarPalco(host, {
                    editavel: false,
                    aoParar: function () {
                        var b = cont.querySelector('[data-play="' + i + '"]');
                        if (b) b.textContent = '▶ Reproduzir';
                    }
                });
                palco.carregar(j.storyboard);
                palco.definirLoop(false);

                var btn = cont.querySelector('[data-play="' + i + '"]');
                btn.addEventListener('click', function () {
                    if (palco.tocando()) {
                        palco.parar();
                        btn.textContent = '▶ Reproduzir';
                    } else {
                        palco.irParaPasso(0);
                        palco.tocar();
                        btn.textContent = '⏸ Pausar';
                    }
                });
            });
        } catch (e) {
            cont.innerHTML = '<div class="jgd-arsenal-vazio" style="color:#f87171;">Erro ao carregar as jogadas do time.</div>';
        }
    }

    window.Jogadas = {
        abrirModal: abrirModal,
        fecharModal: fecharModal,
        alternarMaximizar: alternarMaximizar,
        montarArsenal: montarArsenal
    };
})();
