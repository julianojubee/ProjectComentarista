/* Match Up: campo horizontal com os dois times frente a frente, arrastável.

   Compartilhado pela aba Match-up do modal Pré-jogo (/Jogos/Analisar) e pela
   página pública /creators/escalacao. Quem carrega os dados é o chamador; aqui
   só entra o JSON pronto (ver muRender) — mesmo formato nas duas telas.

   Escopo GLOBAL de propósito, como analisar.js: o HTML montado aqui usa
   handlers inline (onclick="muToggleModoSeta()" e afins), que só enxergam
   funções globais. Estilos em css/matchup.css.

   NADA é salvo: o arrasto, as setas e as formas vivem só na tela aberta. */

    // escHtml não cobre aspas — aqui o texto vai dentro de atributos (data-nome etc.)
    function muEsc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }


    // ── Campo em perspectiva ("modo estádio") ─────────────────────────────
    // Ligado só por quem chama muRender com opts.campo3d (hoje a página pública
    // /creators/escalacao). A aba Match Up de /Relatorios e o modal Pré-jogo
    // seguem no campo chapado — o caminho 2D abaixo continua intocado.
    //
    // A ideia é a da escalação de transmissão: o GRAMADO foge em perspectiva,
    // os JOGADORES não. Cada slot é contra-rotacionado e reescalado para sair
    // do mesmo tamanho na tela; sem isso quem está no fundo vira um borrão,
    // que é justamente o dado que o creator quer mostrar.
    // ── Cor do time (borda dos jogadores) ─────────────────────────────────
    // Só quem chama muRender com opts.cores (hoje /creators/escalacao) ganha o
    // seletor no topo do elenco. As cores viram as mesmas variáveis que a
    // análise já usa (--cor-camisa-casa/--cor-camisa-vis), aplicadas no
    // container do match-up para não vazar para o resto da página.
    const MU_COR_PADRAO = { 1: '#dc2626', 2: '#2563eb' };
    let muCores = false;  // seletor de cor ligado nesta renderização
    let muRoot = null;    // container onde as variáveis de cor são escritas

    function muSetCorTime(time, cor) {
        if (!muRoot) return;
        muRoot.style.setProperty(time === 1 ? '--cor-camisa-casa' : '--cor-camisa-vis', cor);
    }

    const MU3D_ANGULO = 30;        // graus de inclinação do gramado (rotateX)
    const MU3D_PERSPECTIVA = 1150; // px — distância de câmera (perspective da cena)
    let mu3d = null;               // { angulo, perspectiva } quando o campo está inclinado

    // O CSS pode desligar a perspectiva por conta própria (media query de tela
    // estreita). Quem manda é o computed style: assim a projeção do cursor
    // nunca diverge do gramado que está de fato desenhado na tela.
    function muAtivo3d() {
        if (!mu3d) return null;
        const cena = document.getElementById('muCena');
        if (!cena) return null;
        return getComputedStyle(cena).perspective === 'none' ? null : mu3d;
    }

    // Ponto da TELA → % do gramado. No campo chapado é regra de três. Inclinado,
    // desfaz a rotação em X e a divisão de perspectiva: com transform-origin e
    // perspective-origin no centro, um ponto (X,Y) do gramado em px (relativo ao
    // centro) cai na tela em
    //     z = Y·sen      s = p/(p − z)      sx = X·s      sy = Y·cos·s
    // e invertendo o sy:  Y = sy·p / (p·cos + sy·sen),  daí  X = sx/s.
    // Sem isso o jogador não cai onde o cursor está, e o erro cresce conforme
    // sobe o campo — logo onde a perspectiva mais deforma.
    function muTelaParaCampo(clientX, clientY) {
        const campo = document.getElementById('muCampo');
        if (!campo) return { x: 50, y: 50 };

        const t = muAtivo3d();
        if (!t) {
            const r = campo.getBoundingClientRect();
            return { x: (clientX - r.left) / r.width * 100,
                     y: (clientY - r.top) / r.height * 100 };
        }

        // A cena NÃO é transformada: o rect dela é a referência honesta na tela
        // (o do campo devolveria a caixa do trapézio projetado, que não serve).
        const r = (document.getElementById('muCena') || campo).getBoundingClientRect();
        const w = campo.clientWidth, h = campo.clientHeight;
        const p = t.perspectiva;
        const rad = t.angulo * Math.PI / 180;
        const sen = Math.sin(rad), cos = Math.cos(rad);

        const sx = clientX - (r.left + r.width / 2);
        const sy = clientY - (r.top + r.height / 2);
        const Y = sy * p / (p * cos + sy * sen);
        const s = p / (p - Y * sen);
        const X = s ? sx / s : 0;

        return { x: (X + w / 2) / w * 100, y: (Y + h / 2) / h * 100 };
    }

    // Fator que ANULA a escala da perspectiva na profundidade daquele slot.
    function muEscalaSlot(topPct) {
        const t = muAtivo3d();
        if (!t) return 1;
        const h = document.getElementById('muCampo')?.clientHeight || 0;
        const Y = ((isNaN(topPct) ? 50 : topPct) / 100 - 0.5) * h;
        return (t.perspectiva - Y * Math.sin(t.angulo * Math.PI / 180)) / t.perspectiva;
    }

    // O CSS lê o fator em --mu-k. Precisa ser reaplicado toda vez que o slot
    // muda de top (arrasto, troca de posições) ou o campo muda de tamanho.
    function muAjustarSlot3d(slot) {
        if (!mu3d || !slot) return;
        slot.style.setProperty('--mu-k', muEscalaSlot(parseFloat(slot.style.top)).toFixed(4));
    }

    function muAjustarSlots3d() {
        if (!mu3d) return;
        document.querySelectorAll('#muCampo .mu-slot').forEach(muAjustarSlot3d);
    }

    // Traves com rede nos dois gols — o único cenário que o campo tem. São
    // planos de verdade, levantados do gramado com rotateY/rotateX (o campo
    // está em preserve-3d), e ficam fora do hit-test. A rede é malha vazada de
    // propósito: o goleiro continua visível através dela.
    // Cada gol tem quatro planos, como o gol de verdade: a boca (só as traves,
    // vazada, dá para ver o campo por dentro dela), o teto da rede caindo do
    // travessão até a barra de trás, e as duas laterais em triângulo ligando o
    // poste ao chão. É o volume que faltava — uma rede chapada na linha de fundo
    // não lê como goleira de ângulo nenhum.
    function muCenario3dHtml() {
        return ['esq', 'dir'].map(function (l) {
                return '<div class="mu-gol-sombra mu-gol-sombra-' + l + '"></div>' +
                    '<div class="mu-gol-teto mu-gol-teto-' + l + '"></div>' +
                    '<div class="mu-gol-lado mu-gol-lado-' + l + ' mu-gol-lado-a"></div>' +
                    '<div class="mu-gol-lado mu-gol-lado-' + l + ' mu-gol-lado-b"></div>' +
                    '<div class="mu-gol-frame mu-gol-frame-' + l + '"></div>';
            }).join('');
    }

    // A altura do gol manda em tudo (largura da trave, profundidade da rede,
    // tamanho das laterais) e sai da altura do campo, que muda com a janela —
    // por isso vive num custom property recalculado junto com as setas.
    function muAjustarGol3d() {
        if (!mu3d) return;
        const campo = document.getElementById('muCampo');
        if (!campo) return;
        // Altura = 1/3 da boca (--mu-boca-h no CSS, hoje 24% da altura do campo),
        // que é a proporção do gol real.
        campo.style.setProperty('--mu-golh', (campo.clientHeight * 0.08).toFixed(1) + 'px');
    }


    // Media do jogador na competicao/temporada do jogo (vem do servidor como
    // numero; nos data-attributes vira string, por isso o parseFloat).
    function muMediaTexto(media) {
        var n = parseFloat(media);
        return isNaN(n) ? '' : n.toFixed(1);
    }

    function muMediaClasse(media) {
        var n = parseFloat(media);
        if (isNaN(n)) return '';
        return n >= 7 ? ' mu-media-alta' : n >= 5 ? ' mu-media-media' : ' mu-media-baixa';
    }

    // Gols e assistencias na competicao (tambem chegam como string no dataset)
    function muInteiro(valor) {
        var n = parseInt(valor, 10);
        return isNaN(n) ? 0 : n;
    }

    // Pilula abaixo do circulo: chuteira = assistencias, bola = gols. So aparece
    // o que o jogador tem; sem gol nem assistencia a pilula nao e desenhada.
    function muStatsHtml(d) {
        var gols = muInteiro(d.gols);
        var assists = muInteiro(d.assistencias);
        if (gols === 0 && assists === 0) return '';
        return '<span class="mu-stats">' +
            (assists ? '<span class="mu-stat" title="Assistências na competição">👟 ' + assists + '</span>' : '') +
            (gols ? '<span class="mu-stat" title="Gols na competição">⚽ ' + gols + '</span>' : '') +
            '</span>';
    }

    // Botão ℹ do slot: mesmo tooltip da tela de análise (js/jogador-tooltip.js),
    // alimentado pelo pacote que veio no JSON. Sai vazio para quem não tem ficha
    // (jogador avulso criado na hora pelo "+" do banco), para não abrir um card
    // em branco.
    function muInfoHtml(jogadorId) {
        if (!window.JogadorTooltip || !JogadorTooltip.dados(jogadorId)) return '';
        return '<button type="button" class="btn-info-jogador"' +
            ' onmouseenter="JogadorTooltip.mostrar(this, JogadorTooltip.dados(' + jogadorId + '))"' +
            ' onmouseleave="JogadorTooltip.esconder()">ℹ</button>';
    }

    // Miolo do slot: ℹ, sigla, circulo (foto quando o jogador tem, senao so o
    // numero) com a media no canto, gols/assistencias e o nome. Usado na
    // renderizacao inicial e quando o slot troca de jogador (substituicao
    // vinda do elenco).
    function muSlotInnerHtml(time, d) {
        var circle = time === 1 ? 'player-circle-casa' : 'player-circle-vis';
        var media = muMediaTexto(d.media);
        return muInfoHtml(d.jogadorid || d.id) +
            '<button type="button" class="mu-btn-remover" title="Tirar do campo (volta para o elenco)"' +
                ' onclick="event.stopPropagation(); muRemoverDoCampo(this)">×</button>' +
            '<span class="mu-slot-sigla">' + muEsc(d.sigla) + '</span>' +
            '<div class="player-circle ' + circle + (d.foto ? ' mu-com-foto' : '') + '">' +
                (d.foto ? '<img class="mu-foto" src="' + muEsc(d.foto) + '" alt="">' : '') +
                '<span class="mu-num">' + muEsc(d.numero) + '</span>' +
                (media ? '<span class="mu-media' + muMediaClasse(d.media) + '" title="Média na competição">' + media + '</span>' : '') +
            '</div>' +
            muStatsHtml(d) +
            '<div class="player-name">' + muEsc(d.nome) + '</div>';
    }

    function muSlotHtml(time, idx, e) {
        return '<div id="mu-slot-' + time + '-' + idx + '" class="mu-slot"' +
            ' data-jogadorid="' + e.id + '"' +
            ' data-numero="' + muEsc(e.numero) + '"' +
            ' data-nome="' + muEsc(e.nome) + '"' +
            ' data-sigla="' + muEsc(e.sigla) + '"' +
            ' data-foto="' + muEsc(e.foto || '') + '"' +
            ' data-media="' + muEsc(e.media == null ? '' : e.media) + '"' +
            ' data-gols="' + muInteiro(e.gols) + '"' +
            ' data-assistencias="' + muInteiro(e.assistencias) + '"' +
            ' title="' + muEsc(e.nome) + '"' +
            ' style="left:' + e.x + '%; top:' + e.y + '%;"' +
            ' onpointerdown="muPointerDown(event, ' + time + ')"' +
            ' ondragover="event.preventDefault()"' +
            ' ondrop="muDropSlot(event, ' + time + ')">' +
            muSlotInnerHtml(time, e) +
            '</div>';
    }

    function muBancoItemHtml(time, j) {
        var media = muMediaTexto(j.media);
        return '<div class="mu-banco-item" draggable="true"' +
            ' data-jogadorid="' + j.id + '"' +
            ' data-numero="' + muEsc(j.numero) + '"' +
            ' data-nome="' + muEsc(j.nome) + '"' +
            ' data-sigla="' + muEsc(j.sigla) + '"' +
            ' data-foto="' + muEsc(j.foto || '') + '"' +
            ' data-media="' + muEsc(j.media == null ? '' : j.media) + '"' +
            ' data-gols="' + muInteiro(j.gols) + '"' +
            ' data-assistencias="' + muInteiro(j.assistencias) + '"' +
            ' title="' + muEsc(j.nome) + '"' +
            ' ondragstart="muDragStart(event, ' + time + ')">' +
            '<span class="mu-banco-num">' + (muEsc(j.numero) || '–') + '</span>' +
            '<span class="mu-banco-nome">' + muEsc(j.nome) + '</span>' +
            '<span class="mu-banco-pos">' + muEsc(j.sigla) + '</span>' +
            '<span class="mu-banco-media' + muMediaClasse(j.media) + '" title="Média na competição">' + (media || '–') + '</span>' +
            '</div>';
    }

    // d: { casa, visitante, nomeCasa, nomeVisitante } — cada time com
    // { nome, escudo, adversario, data, escalacao[], elenco[] }.
    // opts.dica troca o texto do rodapé (a tela pública fala em simulação, o
    // modal fala em pré-jogo); o resto do comportamento é igual nas duas.
    // opts.campo3d inclina o gramado e monta o estádio (ver bloco no topo).
    // opts.buscaJogador: URL que busca jogador por nome no acervo todo (ver
    // bloco "Buscar jogador"); sem ela, o elenco fica sem o campo de busca.
    function muRender(d, cont, opts) {
        if (!d.casa || !d.visitante) {
            var faltam = [];
            if (!d.casa) faltam.push(d.nomeCasa || 'time da casa');
            if (!d.visitante) faltam.push(d.nomeVisitante || 'time visitante');
            cont.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Sem escalação titular registrada para: ' + muEsc(faltam.join(' e ')) + '.</div>';
            return;
        }
        // Estado visual do match-up recomeça do zero a cada render
        muModoSeta = false;
        muSetaOrigem = null;
        muSetas = {};
        muModoForma = false;
        muFormaDraw = null;
        muFormas = [];
        const c3d = !!(opts && opts.campo3d);
        muCores = !!(opts && opts.cores);
        muBuscaUrl = (opts && opts.buscaJogador) || null;
        const comTransmissao = !!(opts && opts.transmissao);
        if (muRoot && muRoot !== cont) muRoot.classList.remove('mu-transmissao');
        cont.classList.remove('mu-transmissao');
        muRoot = cont;
        // Recomeça nas cores padrão a cada montagem do campo
        muSetCorTime(1, MU_COR_PADRAO[1]);
        muSetCorTime(2, MU_COR_PADRAO[2]);
        mu3d = c3d ? { angulo: MU3D_ANGULO, perspectiva: MU3D_PERSPECTIVA } : null;

        // Fichas e números do ℹ dos jogadores deste match-up (ver muInfoHtml).
        // Acrescenta em vez de substituir: na tela de análise o mapa já tem o
        // elenco do jogo, e aqui chegam os que faltavam.
        if (window.JogadorTooltip) JogadorTooltip.acrescentar(d.tooltip);

        var t1 = d.casa, t2 = d.visitante;
        var escudo1 = t1.escudo ? '<img src="' + t1.escudo + '" alt="">' : '';
        var escudo2 = t2.escudo ? '<img src="' + t2.escudo + '" alt="">' : '';
        var jogo1 = 'Última escalação: vs ' + muEsc(t1.adversario || '?') + ' — ' + muEsc(t1.data || 'data desconhecida');
        var jogo2 = 'Última escalação: vs ' + muEsc(t2.adversario || '?') + ' — ' + muEsc(t2.data || 'data desconhecida');

        cont.innerHTML =
            '<div class="mu-header">' +
                '<div class="mu-time-info">' + escudo1 + '<div><div class="mu-time-nome">' + muEsc(t1.nome) + '</div><div class="mu-time-jogo">' + jogo1 + '</div></div></div>' +
                '<div class="mu-vs">×</div>' +
                '<div class="mu-time-info mu-dir">' + escudo2 + '<div><div class="mu-time-nome">' + muEsc(t2.nome) + '</div><div class="mu-time-jogo">' + jogo2 + '</div></div></div>' +
            '</div>' +
            '<div class="mu-toolbar">' +
                '<button type="button" id="muBtnSeta" class="btn btn-sm btn-outline-warning" onclick="muToggleModoSeta()"' +
                    ' title="Ligue, clique num jogador e depois no ponto do campo para onde ele se movimenta. Clique duplo numa seta para removê-la.">➹ Setas</button>' +
                '<span id="muSetaHint" style="font-size:11px; color:#facc15; display:none;">clique no jogador e depois no destino · duplo clique na seta remove · Esc sai</span>' +
                '<button type="button" id="muBtnForma" class="btn btn-sm btn-outline-danger" onclick="muToggleModoForma()"' +
                    ' title="Ligue e arraste no campo para desenhar uma área; ao soltar dá para digitar um texto. Clique duplo numa forma para removê-la.">▱ Formas</button>' +
                '<span id="muFormaTipos" style="display:none; gap:4px;">' +
                    '<button type="button" class="mu-forma-tipo" data-tipo="rect" onclick="muSetFormaTipo(\'rect\')" title="Retângulo">▭</button>' +
                    '<button type="button" class="mu-forma-tipo" data-tipo="elipse" onclick="muSetFormaTipo(\'elipse\')" title="Elipse">◯</button>' +
                    '<button type="button" class="mu-forma-tipo" data-tipo="livre" onclick="muSetFormaTipo(\'livre\')" title="Traço livre">✎</button>' +
                '</span>' +
                '<span id="muFormaHint" style="font-size:11px; color:#f87171; display:none;">arraste no campo para desenhar · Enter confirma o texto · arraste o texto para reposicionar · duplo clique na forma remove · Esc sai</span>' +
                (comTransmissao
                    ? '<button type="button" id="muBtnTransmissao" class="btn btn-sm btn-outline-info" onclick="muToggleTransmissao()"' +
                        ' title="Campo ocupando a janela inteira, sem o resto do site, com os jogadores maiores — para capturar no OBS. Esc sai.">📺 Transmissão</button>' +
                      '<button type="button" id="muBtnTelaCheia" class="btn btn-sm btn-outline-secondary mu-so-transmissao" onclick="muToggleTelaCheia()"' +
                        ' title="Tela cheia do navegador (some a barra de endereço e as abas)">⛶ Tela cheia</button>'
                    : '') +
                '<span id="muAviso" style="font-size:12px; color:#fbbf24; display:none;"></span>' +
            '</div>' +
            '<div class="mu-layout">' +
                muBancoHtml(1, t1.nome, t1.elenco) +
                // A cena é só a caixa que segura a perspectiva: no campo chapado
                // ela é display:contents e some do layout, deixando o grid como era.
                '<div id="muCena" class="mu-cena' + (c3d ? ' mu-cena-3d' : '') + '">' +
                '<div id="muCampo" class="mu-campo theme-dark-zone' + (c3d ? ' mu-campo-3d' : '') + '" onclick="muCampoClick(event)" onpointerdown="muFormaPointerDown(event)" ondragover="event.preventDefault()" ondrop="muDropCampo(event)">' +
                    '<div class="mu-linha-meio"></div><div class="mu-circulo"></div>' +
                    '<div class="mu-area-esq"></div><div class="mu-area-dir"></div>' +
                    '<div class="mu-gol-esq"></div><div class="mu-gol-dir"></div>' +
                    (c3d ? muCenario3dHtml() : '') +
                    '<svg id="muSetasSvg" class="setas-svg"><defs>' +
                        '<marker id="muSetaHead" markerWidth="12" markerHeight="10" refX="10" refY="5" orient="auto" markerUnits="userSpaceOnUse">' +
                            '<path d="M0,0 L12,5 L0,10 z" fill="#facc15"></path>' +
                        '</marker>' +
                    '</defs><g id="muFormasG"></g></svg>' +
                    t1.escalacao.map(function (e, i) { return muSlotHtml(1, i, e); }).join('') +
                    t2.escalacao.map(function (e, i) { return muSlotHtml(2, i, e); }).join('') +
                '</div>' +
                '</div>' +
                muBancoHtml(2, t2.nome, t2.elenco) +
            '</div>' +
            '<p class="mu-dica">Arraste um jogador para qualquer ponto do campo, solte sobre um companheiro para trocar as posições, arraste alguém do elenco sobre um titular para substituí-lo ou sobre uma área vazia para incluí-lo no campo (até 11 por time). O × no jogador o devolve ao elenco. O botão + do elenco cria um jogador avulso (ex.: garoto da base ainda fora da API)' + (muBuscaUrl ? '; a busca abaixo dele traz qualquer jogador cadastrado, de qualquer time (ex.: um convocado que não jogou a última)' : '') + '. ' + muEsc((opts && opts.dica) || 'Simulação de pré-jogo — nada é salvo.') + '</p>';

        // Ângulo e perspectiva saem daqui para o CSS: assim o JS que projeta o
        // cursor e o CSS que inclina o gramado não têm como divergir.
        if (mu3d) {
            const cena = document.getElementById('muCena');
            const campo = document.getElementById('muCampo');
            cena.style.perspective = mu3d.perspectiva + 'px';
            campo.style.setProperty('--mu-ang', mu3d.angulo + 'deg');
            campo.style.transform = 'rotateX(' + mu3d.angulo + 'deg)';
            muAjustarGol3d();
            muAjustarSlots3d();
        }

        // Setas e formas guardam % do campo: redesenha quando o campo muda de tamanho
        // (a escala dos slots também depende da altura do campo, por isso entra aqui)
        new ResizeObserver(function () { muAjustarTamanho(); muDesenharSetas(); muDesenharFormas(); muAjustarGol3d(); muAjustarSlots3d(); }).observe(document.getElementById('muCampo'));
    }

    // ── Modo transmissão (OBS) ─────────────────────────────────────────────
    // O container do match-up vira uma camada fixa do tamanho da janela: some o
    // resto do site, os elencos recolhem e o campo cresce até a altura da tela.
    // Crescer o campo não basta — os jogadores têm tamanho fixo em px e viravam
    // formiguinhas num gramado de 1500px. Por isso eles escalam junto com a
    // altura do campo (--mu-base, lido no transform do .mu-slot).
    const MU_ALTURA_REFERENCIA = 460; // px de campo em que o jogador sai no tamanho "normal"

    function muEmTransmissao() {
        return !!(muRoot && muRoot.classList.contains('mu-transmissao'));
    }

    function muAjustarTamanho() {
        const campo = document.getElementById('muCampo');
        if (!campo) return;
        const h = campo.clientHeight;
        const base = muEmTransmissao() ? Math.min(Math.max(h / MU_ALTURA_REFERENCIA, 1), 2.4) : 1;
        campo.style.setProperty('--mu-base', base.toFixed(3));
        // A perspectiva é uma distância de câmera em px: fixa, num campo muito
        // maior, a câmera "entra" no gramado e o fundo some. Cresce na mesma
        // proporção do campo (a referência é a altura típica da tela normal).
        if (mu3d) {
            mu3d.perspectiva = muEmTransmissao()
                ? Math.round(MU3D_PERSPECTIVA * Math.max(1, h / 800))
                : MU3D_PERSPECTIVA;
            const cena = document.getElementById('muCena');
            if (cena) cena.style.perspective = mu3d.perspectiva + 'px';
        }
    }

    function muToggleTransmissao() {
        if (!muRoot) return;
        const ligar = !muRoot.classList.contains('mu-transmissao');
        muRoot.classList.toggle('mu-transmissao', ligar);
        document.documentElement.classList.toggle('mu-transmissao-aberta', ligar);
        // Entrando, os elencos recolhem para o campo ganhar a largura toda
        // (continuam a um clique de distância, na tira lateral).
        if (ligar) [1, 2].forEach(function (t) {
            const b = document.getElementById('muBancoBox' + t);
            if (b && !b.classList.contains('mu-banco-fechado')) muToggleBanco(t);
        });
        if (!ligar && document.fullscreenElement) document.exitFullscreen().catch(function () { });
        const btn = document.getElementById('muBtnTransmissao');
        if (btn) btn.textContent = ligar ? '✕ Sair da transmissão' : '📺 Transmissão';
        muAjustarTamanho();
        muAjustarGol3d();
        muAjustarSlots3d();
        muDesenharSetas();
        muDesenharFormas();
    }

    function muToggleTelaCheia() {
        if (document.fullscreenElement) document.exitFullscreen().catch(function () { });
        else document.documentElement.requestFullscreen?.().catch(function () { });
    }

    function muBancoHtml(time, nome, elenco) {
        return '<div id="muBancoBox' + time + '" class="mu-banco">' +
            '<div class="mu-banco-header">' +
                '<button type="button" class="mu-banco-toggle" title="Recolher/expandir o elenco" onclick="muToggleBanco(' + time + ')">' + (time === 1 ? '‹' : '›') + '</button>' +
                '<span class="mu-banco-nome-time">' + muEsc(nome) + ' — elenco</span>' +
                (muCores ? '<input type="color" class="mu-banco-cor" value="' + MU_COR_PADRAO[time] + '"' +
                    ' title="Cor da borda dos jogadores deste time"' +
                    ' oninput="muSetCorTime(' + time + ', this.value)">' : '') +
                '<button type="button" class="mu-btn-add" title="Adicionar jogador avulso (ainda fora da API)" onclick="muToggleFormFicticio(' + time + ')">+</button></div>' +
            '<div id="muFormFic' + time + '" class="mu-form-ficticio" style="display:none;">' +
                '<input id="muFicNum' + time + '" class="mu-fic-num" maxlength="3" placeholder="Nº">' +
                '<input id="muFicNome' + time + '" class="mu-fic-nome" placeholder="Nome do jogador" onkeydown="if (event.key === \'Enter\') muCriarFicticio(' + time + ')">' +
                '<button type="button" onclick="muCriarFicticio(' + time + ')">OK</button>' +
            '</div>' +
            (muBuscaUrl ? '<div class="mu-busca">' +
                '<input id="muBusca' + time + '" class="mu-busca-input" type="search" autocomplete="off"' +
                    ' placeholder="🔍 Buscar jogador…" title="Busca em todos os times (ex.: um convocado que não jogou a última)"' +
                    ' oninput="muBuscarJogador(' + time + ')"' +
                    ' onkeydown="if (event.key === \'Escape\') muFecharBusca(' + time + ', true)">' +
                '<div id="muBuscaRes' + time + '" class="mu-busca-res" style="display:none;"></div>' +
            '</div>' : '') +
            '<div id="muBanco' + time + '" class="mu-banco-lista">' +
                elenco.map(function (j) { return muBancoItemHtml(time, j); }).join('') +
            '</div></div>';
    }

    // Recolher o elenco: a lista some e a coluna encolhe para uma tira, e toda
    // a largura vai para o campo. É o jeito de ver a escalação inteira em tela
    // cheia sem perder o banco — reabre no mesmo clique.
    function muToggleBanco(time) {
        const banco = document.getElementById('muBancoBox' + time);
        if (!banco) return;
        const fechado = banco.classList.toggle('mu-banco-fechado');
        const btn = banco.querySelector('.mu-banco-toggle');
        // A seta aponta para onde a lista vai: some para fora, volta para dentro
        if (btn) btn.textContent = (time === 1) === fechado ? '›' : '‹';
        // O campo mudou de largura: setas, formas e escala dos jogadores seguem junto
        muDesenharSetas();
        muDesenharFormas();
        muAjustarGol3d();
        muAjustarSlots3d();
    }

    // ── Match-up: arrasto 100% client-side, só para simulação visual.
    // Mesma mecânica da aba Match Up de /Relatorios:
    //   campo → outro jogador: troca a posição (left/top) dos dois (swap);
    //   campo → área vazia:    reposiciona o jogador ali;
    //   banco → campo:         o jogador do elenco substitui o titular, que
    //                          volta pro banco.
    // O arrasto no campo usa POINTER EVENTS (não HTML5 drag-and-drop): o círculo
    // segue o cursor de verdade, sem depender do dragstart nativo — o DnD do
    // HTML5 fica só no banco→campo, onde funciona bem.
    // Nada é enviado ao servidor: sem persistência.

    // ── Campo: arrasto por pointer events ─────────────────────────────────
    let muPtr = null; // { el, time, pointerId, startX, startY, origLeft, origTop, moved }

    function muPointerDown(ev, time) {
        // O ℹ é o único filho do slot que recebe ponteiro: clicar nele não arrasta.
        if (ev.target.closest('.btn-info-jogador, .mu-btn-remover')) return;
        if (muModoSeta) return; // no modo seta o clique seleciona/traça, não arrasta
        if (muModoForma) return; // no modo forma o arrasto desenha (tratado no campo)
        if (ev.button !== 0 && ev.pointerType === 'mouse') return; // só botão esquerdo
        const el = ev.target.closest('.mu-slot');
        if (!el) return;
        ev.preventDefault(); // evita seleção de texto/drag nativo
        // Captura o ponteiro (mantém o fluxo de eventos em touch mesmo saindo do
        // elemento); os listeners de move/up ficam no document, então falhar aqui
        // não quebra o arrasto com mouse.
        try { el.setPointerCapture(ev.pointerId); } catch { }
        muPtr = { el, time, pointerId: ev.pointerId, startX: ev.clientX, startY: ev.clientY,
                  origLeft: el.style.left, origTop: el.style.top, moved: false };
    }

    // Converte a posição do cursor para % do campo, preso apenas às bordas —
    // qualquer jogador pode ser movido por todo o campo, inclusive no lado adversário.
    function muPosNoCampo(clientX, clientY) {
        const p = muTelaParaCampo(clientX, clientY);
        // Faixa vertical 10-90%: mesmo limite da renderização inicial, para o
        // círculo/nome do jogador não serem cortados pelo overflow do campo.
        return { x: Math.min(Math.max(p.x, 2), 98), y: Math.min(Math.max(p.y, 10), 90) };
    }

    document.addEventListener('pointermove', ev => {
        if (!muPtr || ev.pointerId !== muPtr.pointerId) return;
        // pequeno limiar para diferenciar clique de arrasto
        if (!muPtr.moved && Math.hypot(ev.clientX - muPtr.startX, ev.clientY - muPtr.startY) < 4) return;
        muPtr.moved = true;
        muPtr.el.classList.add('mu-arrastando');
        const pos = muPosNoCampo(ev.clientX, ev.clientY);
        muPtr.el.style.left = pos.x + '%';
        muPtr.el.style.top = pos.y + '%';
        muAjustarSlot3d(muPtr.el); // mudou de profundidade: reescala
        muDesenharSetas(); // a origem das setas acompanha o jogador
    });

    document.addEventListener('pointerup', ev => {
        if (!muPtr || ev.pointerId !== muPtr.pointerId) return;
        const drag = muPtr;
        muPtr = null;
        drag.el.classList.remove('mu-arrastando');
        if (!drag.moved) return; // foi só um clique

        // Soltou sobre outro jogador DO MESMO TIME? → swap: o arrastado assume a
        // posição do alvo e o alvo vai para a posição ORIGINAL do arrastado.
        // (elementsFromPoint ignora o próprio arrastado, que está sob o cursor)
        const alvo = document.elementsFromPoint(ev.clientX, ev.clientY)
            .find(e => e !== drag.el && e.classList && e.classList.contains('mu-slot'));
        if (alvo && alvo.id.startsWith(`mu-slot-${drag.time}-`)) {
            drag.el.style.left = alvo.style.left;
            drag.el.style.top = alvo.style.top;
            alvo.style.left = drag.origLeft;
            alvo.style.top = drag.origTop;
            muAjustarSlot3d(drag.el);
            muAjustarSlot3d(alvo);
        }
        // Área vazia (ou adversário): fica onde soltou — o left/top já foi
        // aplicado no pointermove.
        muDesenharSetas();
    });

    document.addEventListener('pointercancel', ev => {
        if (!muPtr || ev.pointerId !== muPtr.pointerId) return;
        // arrasto abortado (ex.: gesto do sistema): volta pra posição original
        muPtr.el.classList.remove('mu-arrastando');
        muPtr.el.style.left = muPtr.origLeft;
        muPtr.el.style.top = muPtr.origTop;
        muAjustarSlot3d(muPtr.el);
        muPtr = null;
        muDesenharSetas();
    });

    // ── Banco → campo: HTML5 drag-and-drop ────────────────────────────────
    let muDrag = null; // { time, el } — item do banco sendo arrastado

    function muDragStart(ev, time) {
        // closest: garante o container certo mesmo se o dragstart nascer num filho
        const el = ev.target.closest('.mu-banco-item');
        if (!el) return;
        muDrag = { time, el };
        ev.dataTransfer.setData('text/plain', 'matchup'); // exigido por alguns navegadores p/ permitir o drag
        ev.dataTransfer.effectAllowed = 'move';
    }

    function muDropSlot(ev, time) {
        ev.preventDefault();
        ev.stopPropagation();
        const alvo = ev.target.closest('.mu-slot');
        const drag = muDrag;
        muDrag = null;
        if (!drag || !alvo || drag.time !== time) return;

        // Substituição: guarda os dados do titular que sai, aplica o novo
        // jogador no slot e devolve o removido pro banco.
        const removido = { ...alvo.dataset };
        muAplicarNoSlot(alvo, drag.el.dataset);
        muCriarItemBanco(time, removido);
        drag.el.remove();
    }

    // Atualiza data-attributes e o visual (foto/número/média/nome/sigla) de um
    // slot do campo. Redesenha o miolo inteiro: com foto e média o conteúdo do
    // círculo muda de estrutura, não só de texto.
    function muAplicarNoSlot(slot, d) {
        var time = slot.id.startsWith('mu-slot-1-') ? 1 : 2;
        slot.dataset.jogadorid = d.jogadorid;
        slot.dataset.numero = d.numero;
        slot.dataset.nome = d.nome;
        slot.dataset.sigla = d.sigla;
        slot.dataset.foto = d.foto || '';
        slot.dataset.media = d.media || '';
        slot.dataset.gols = muInteiro(d.gols);
        slot.dataset.assistencias = muInteiro(d.assistencias);
        slot.title = d.nome;
        slot.innerHTML = muSlotInnerHtml(time, slot.dataset);
    }

    // Recria a linha do banco para o titular que saiu do campo (textContent, não
    // innerHTML: nome de jogador nunca deve virar markup)
    function muCriarItemBanco(time, d) {
        const item = document.createElement('div');
        item.className = 'mu-banco-item';
        item.draggable = true;
        item.dataset.jogadorid = d.jogadorid;
        item.dataset.numero = d.numero;
        item.dataset.nome = d.nome;
        item.dataset.sigla = d.sigla;
        item.dataset.foto = d.foto || '';
        item.dataset.media = d.media || '';
        item.dataset.gols = muInteiro(d.gols);
        item.dataset.assistencias = muInteiro(d.assistencias);
        item.title = d.nome;

        const num = document.createElement('span');
        num.className = 'mu-banco-num';
        num.textContent = d.numero || '–';
        const nome = document.createElement('span');
        nome.className = 'mu-banco-nome';
        nome.textContent = d.nome;
        const pos = document.createElement('span');
        pos.className = 'mu-banco-pos';
        pos.textContent = d.sigla;
        const media = document.createElement('span');
        media.className = 'mu-banco-media' + muMediaClasse(d.media);
        media.title = 'Média na competição';
        media.textContent = muMediaTexto(d.media) || '–';
        item.append(num, nome, pos, media);

        item.addEventListener('dragstart', ev => muDragStart(ev, time));

        const banco = document.getElementById('muBanco' + time);
        banco.prepend(item); // no topo: é o jogador que o usuário acabou de tirar
    }

    // ── Elenco → área vazia do campo: inclui o jogador sem tirar ninguém ──
    // (caso típico: campo com menos de 11 porque um jogador não estava na API)
    let muExtraSeq = 0;
    const MU_MAX_TITULARES = 11;

    function muSlotsDoTime(time) {
        return Array.from(document.querySelectorAll('#muCampo .mu-slot[id^="mu-slot-' + time + '-"]'));
    }

    // Titular do time sob o ponto de soltura, pelo retângulo NA TELA. O drop do
    // HTML5 nem sempre cai no slot: no campo 3D o slot flutua (translateZ) e o
    // hit-test do navegador entrega o gramado — e aí o jogador era incluído como
    // 12º em vez de substituir. A folga cobre soltar no nome ou na borda do círculo.
    // Com o time já completo, vale o titular mais próximo (até ~80px).
    function muSlotAlvoDoDrop(time, clientX, clientY, completo) {
        const FOLGA = 10, RAIO_COMPLETO = 80;
        // Slots vizinhos se sobrepõem: entre os que contêm o ponto, ganha o de
        // centro mais próximo, não o primeiro da lista.
        let melhor = null, melhorDist = Infinity, melhorDentro = false;
        muSlotsDoTime(time).forEach(function (s) {
            const r = s.getBoundingClientRect();
            const dentro = clientX >= r.left - FOLGA && clientX <= r.right + FOLGA &&
                           clientY >= r.top - FOLGA && clientY <= r.bottom + FOLGA;
            const dist = Math.hypot(clientX - (r.left + r.width / 2), clientY - (r.top + r.height / 2));
            if ((dentro && !melhorDentro) || (dentro === melhorDentro && dist < melhorDist)) {
                melhor = s; melhorDist = dist; melhorDentro = dentro;
            }
        });
        if (melhorDentro) return melhor;
        return completo && melhorDist <= RAIO_COMPLETO ? melhor : null;
    }

    // Aviso curto na barra de ferramentas (some sozinho)
    let muAvisoTimer = null;
    function muAviso(texto) {
        const el = document.getElementById('muAviso');
        if (!el) return;
        el.textContent = texto;
        el.style.display = 'inline';
        clearTimeout(muAvisoTimer);
        muAvisoTimer = setTimeout(function () { el.style.display = 'none'; }, 3500);
    }

    // Botão × do slot: o jogador sai do campo e volta pro topo do elenco,
    // levando junto as setas de movimentação dele.
    function muRemoverDoCampo(btn) {
        const slot = btn.closest('.mu-slot');
        if (!slot) return;
        const time = slot.id.startsWith('mu-slot-1-') ? 1 : 2;
        if (window.JogadorTooltip) JogadorTooltip.esconder();
        muCriarItemBanco(time, { ...slot.dataset });
        delete muSetas[slot.id];
        if (muSetaOrigem === slot) muLimparOrigemSeta();
        slot.remove();
        muDesenharSetas();
    }

    function muDropCampo(ev) {
        ev.preventDefault();
        const drag = muDrag;
        muDrag = null;
        if (!drag) return;

        const completo = muSlotsDoTime(drag.time).length >= MU_MAX_TITULARES;
        const alvo = muSlotAlvoDoDrop(drag.time, ev.clientX, ev.clientY, completo);
        if (alvo) {
            // Mesma substituição do drop direto no slot
            const removido = { ...alvo.dataset };
            muAplicarNoSlot(alvo, drag.el.dataset);
            muCriarItemBanco(drag.time, removido);
            drag.el.remove();
            return;
        }
        if (completo) {
            muAviso('Já tem 11 em campo: solte sobre um titular para substituir ou tire alguém no ×.');
            return;
        }

        const pos = muPosNoCampo(ev.clientX, ev.clientY);
        const d = drag.el.dataset;
        document.getElementById('muCampo').insertAdjacentHTML('beforeend',
            muSlotHtml(drag.time, 'x' + (++muExtraSeq),
                { id: d.jogadorid, numero: d.numero, nome: d.nome, sigla: d.sigla,
                  foto: d.foto, media: d.media, gols: d.gols, assistencias: d.assistencias,
                  x: pos.x, y: pos.y }));
        muAjustarSlot3d(document.getElementById('muCampo').lastElementChild);
        drag.el.remove();
    }

    // ── Jogador avulso (ainda fora da API): entra no elenco e daí pro campo ──
    let muFicSeq = 0;

    function muToggleFormFicticio(time) {
        const form = document.getElementById('muFormFic' + time);
        const abrir = form.style.display === 'none';
        form.style.display = abrir ? 'flex' : 'none';
        if (abrir) document.getElementById('muFicNome' + time).focus();
    }

    function muCriarFicticio(time) {
        const numEl = document.getElementById('muFicNum' + time);
        const nomeEl = document.getElementById('muFicNome' + time);
        const nome = nomeEl.value.trim();
        if (!nome) { nomeEl.focus(); return; }
        muCriarItemBanco(time, { jogadorid: 'fic-' + (++muFicSeq), numero: numEl.value.trim(), nome: nome, sigla: '?' });
        numEl.value = '';
        nomeEl.value = '';
        muToggleFormFicticio(time);
    }

    // ── Buscar jogador (acervo todo): convocado ou reforço fora da última
    // escalação. O escolhido entra no topo do elenco, igual ao avulso, mas com
    // id, foto e posição de verdade. Só existe quando muRender recebe
    // opts.buscaJogador (hoje /creators/escalacao).
    let muBuscaUrl = null;
    let muBuscaTimer = {};
    let muBuscaSeq = {};       // descarta resposta atrasada de uma busca antiga
    let muBuscaAchados = {};   // time -> resultados da última busca

    function muBuscarJogador(time) {
        clearTimeout(muBuscaTimer[time]);
        const q = document.getElementById('muBusca' + time).value.trim();
        if (q.length < 3) { muFecharBusca(time); return; }
        muBuscaTimer[time] = setTimeout(() => muExecutarBusca(time, q), 300);
    }

    async function muExecutarBusca(time, q) {
        const seq = muBuscaSeq[time] = (muBuscaSeq[time] || 0) + 1;
        const res = document.getElementById('muBuscaRes' + time);
        res.style.display = '';
        res.innerHTML = '<div class="mu-busca-vazio">Buscando…</div>';
        let lista;
        try {
            const resp = await fetch(muBuscaUrl + (muBuscaUrl.includes('?') ? '&' : '?') + 'q=' + encodeURIComponent(q));
            if (!resp.ok) throw new Error();
            lista = await resp.json();
        } catch (e) {
            lista = null;
        }
        if (seq !== muBuscaSeq[time]) return;
        if (!document.getElementById('muBuscaRes' + time)) return; // campo remontado no meio

        muBuscaAchados[time] = lista || [];
        if (!lista) { res.innerHTML = '<div class="mu-busca-vazio">Não deu para buscar. Tente de novo.</div>'; return; }
        if (!lista.length) { res.innerHTML = '<div class="mu-busca-vazio">Nenhum jogador encontrado.</div>'; return; }

        res.innerHTML = lista.map(function (j, i) {
            const clube = [j.time, j.selecao].filter(Boolean).join(' · ');
            return '<button type="button" class="mu-busca-item" onclick="muEscolherBusca(' + time + ', ' + i + ')" title="' + muEsc(j.nome + (clube ? ' — ' + clube : '')) + '">' +
                (j.foto ? '<img src="' + muEsc(j.foto) + '" alt="">' : '<span class="mu-busca-sem-foto">' + (muEsc(j.numero) || '?') + '</span>') +
                '<span class="mu-busca-txt"><span class="mu-busca-nome">' + muEsc(j.nome) + '</span>' +
                '<span class="mu-busca-clube">' + muEsc(j.sigla) + (clube ? ' · ' + muEsc(clube) : '') + '</span></span>' +
            '</button>';
        }).join('');
    }

    function muFecharBusca(time, limpar) {
        clearTimeout(muBuscaTimer[time]);
        muBuscaSeq[time] = (muBuscaSeq[time] || 0) + 1;
        const res = document.getElementById('muBuscaRes' + time);
        if (res) { res.style.display = 'none'; res.innerHTML = ''; }
        const input = document.getElementById('muBusca' + time);
        if (limpar && input) input.value = '';
    }

    function muEscolherBusca(time, i) {
        const j = (muBuscaAchados[time] || [])[i];
        if (!j) return;
        muFecharBusca(time, true);

        // Já está em campo ou no elenco deste time: só destaca, não duplica
        const campo = document.getElementById('muCampo');
        const banco = document.getElementById('muBanco' + time);
        const sel = '[data-jogadorid="' + j.id + '"]';
        const existente = banco.querySelector(sel) ||
            Array.from(campo.querySelectorAll('.mu-slot' + sel)).find(s => s.id.startsWith('mu-slot-' + time + '-'));
        if (existente) { muPiscar(existente); return; }

        muCriarItemBanco(time, { jogadorid: j.id, numero: j.numero, nome: j.nome, sigla: j.sigla, foto: j.foto });
        muPiscar(banco.firstElementChild);
    }

    function muPiscar(el) {
        if (!el) return;
        el.scrollIntoView({ block: 'nearest' });
        el.classList.remove('mu-piscar');
        void el.offsetWidth; // reinicia a animação
        el.classList.add('mu-piscar');
    }

    // ── Setas de movimentação: mesma mecânica da tela de análise, mas 100%
    // client-side (nada é salvo). Ligue o modo, clique num jogador (origem) e
    // depois no destino; duplo clique na seta remove; Esc limpa/sai.
    let muModoSeta = false;
    let muSetaOrigem = null;  // elemento .mu-slot selecionado como origem
    let muSetas = {};         // id do slot -> [{x, y}] em % do campo

    function muToggleModoSeta() {
        muModoSeta = !muModoSeta;
        if (muModoSeta && muModoForma) muToggleModoForma(); // modos são excludentes
        document.getElementById('muCampo')?.classList.toggle('mu-modo-seta', muModoSeta);
        document.getElementById('muBtnSeta')?.classList.toggle('ativo', muModoSeta);
        const hint = document.getElementById('muSetaHint');
        if (hint) hint.style.display = muModoSeta ? '' : 'none';
        muLimparOrigemSeta();
    }

    function muLimparOrigemSeta() {
        muSetaOrigem?.classList.remove('mu-seta-origem');
        muSetaOrigem = null;
    }

    function muCampoClick(ev) {
        if (!muModoSeta) return;
        if (ev.target.tagName === 'line') return; // interação com a própria seta (dblclick remove)

        const slot = ev.target.closest('.mu-slot');
        if (slot) {
            muLimparOrigemSeta();
            muSetaOrigem = slot;
            slot.classList.add('mu-seta-origem');
            return;
        }

        if (!muSetaOrigem) return;
        const destino = muPctCampo(ev.clientX, ev.clientY);
        (muSetas[muSetaOrigem.id] = muSetas[muSetaOrigem.id] || []).push(destino);
        muDesenharSetas();
        // Mantém a origem selecionada: permite adicionar várias setas ao mesmo jogador
    }

    function muDesenharSetas() {
        const campo = document.getElementById('muCampo');
        const svg = document.getElementById('muSetasSvg');
        if (!campo || !svg) return;

        svg.querySelectorAll('line').forEach(l => l.remove());
        const w = campo.clientWidth, h = campo.clientHeight;

        Object.keys(muSetas).forEach(slotId => {
            const slot = document.getElementById(slotId);
            const setas = muSetas[slotId];
            if (!slot || !setas.length) return;

            const x1 = parseFloat(slot.style.left) / 100 * w;
            const y1 = parseFloat(slot.style.top)  / 100 * h;

            setas.forEach(s => {
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', x1);
                line.setAttribute('y1', y1);
                line.setAttribute('x2', s.x / 100 * w);
                line.setAttribute('y2', s.y / 100 * h);
                line.setAttribute('marker-end', 'url(#muSetaHead)');
                line.addEventListener('dblclick', ev => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    muSetas[slotId] = muSetas[slotId].filter(o => o !== s);
                    muDesenharSetas();
                });
                svg.appendChild(line);
            });
        });
    }

    // ── Formas anotadas (retângulo/elipse/traço livre + texto): arraste para
    // desenhar a área, solte e digite o texto (Enter confirma, Esc pula).
    // Igual às setas: 100% client-side, nada é salvo.
    let muModoForma = false;
    let muFormaTipo = 'rect';  // 'rect' | 'elipse' | 'livre'
    let muFormas = [];         // { tipo, x1, y1, x2, y2, pontos, texto } em % do campo
    let muFormaDraw = null;    // forma em desenho (arrasto em andamento)

    function muToggleModoForma() {
        muModoForma = !muModoForma;
        if (muModoForma && muModoSeta) muToggleModoSeta(); // modos são excludentes
        document.getElementById('muCampo')?.classList.toggle('mu-modo-forma', muModoForma);
        document.getElementById('muBtnForma')?.classList.toggle('ativo', muModoForma);
        const tipos = document.getElementById('muFormaTipos');
        if (tipos) tipos.style.display = muModoForma ? 'inline-flex' : 'none';
        const hint = document.getElementById('muFormaHint');
        if (hint) hint.style.display = muModoForma ? '' : 'none';
        if (muModoForma) muSetFormaTipo(muFormaTipo);
        else {
            muFormaDraw = null;
            document.querySelector('.mu-forma-input')?.remove();
            muDesenharFormas();
        }
    }

    function muSetFormaTipo(tipo) {
        muFormaTipo = tipo;
        document.querySelectorAll('.mu-forma-tipo').forEach(b =>
            b.classList.toggle('ativo', b.dataset.tipo === tipo));
    }

    // Posição do cursor em % do campo, presa às bordas
    function muPctCampo(clientX, clientY) {
        const p = muTelaParaCampo(clientX, clientY);
        return { x: Math.max(0, Math.min(100, p.x)),
                 y: Math.max(0, Math.min(100, p.y)) };
    }

    // O preventDefault do pointerdown (necessário pro desenho) suprime o
    // click/dblclick nativo — o "duplo clique remove" é detectado na mão:
    // dois pointerdowns na mesma forma em até 400ms. A comparação é pelo
    // OBJETO da forma (__muForma), não pelo elemento: o redesenho após o
    // primeiro clique recria os nós SVG.
    let muFormaUltimoTap = { forma: null, t: 0 };

    function muFormaPointerDown(ev) {
        if (!muModoForma) return;
        if (ev.button !== 0 && ev.pointerType === 'mouse') return;
        if (ev.target.closest && ev.target.closest('.mu-forma-input')) return;
        ev.preventDefault();

        const alvo = ev.target.closest && ev.target.closest('.mu-forma, .mu-forma-texto');
        if (alvo && alvo.__muForma) {
            const agora = Date.now();
            if (muFormaUltimoTap.forma === alvo.__muForma && agora - muFormaUltimoTap.t < 400) {
                muFormaUltimoTap = { forma: null, t: 0 };
                alvo.__muRemover();
                return;
            }
            muFormaUltimoTap = { forma: alvo.__muForma, t: agora };
            // No texto, o arrasto reposiciona o rótulo em vez de desenhar por cima
            if (alvo.classList.contains('mu-forma-texto')) {
                muTextoDrag = { forma: alvo.__muForma, moved: false, startX: ev.clientX, startY: ev.clientY };
                return;
            }
        } else {
            muFormaUltimoTap = { forma: null, t: 0 };
        }

        const p = muPctCampo(ev.clientX, ev.clientY);
        muFormaDraw = { tipo: muFormaTipo, x1: p.x, y1: p.y, x2: p.x, y2: p.y, pontos: [p] };
    }

    // Arrasto do rótulo de texto de uma forma (reposicionamento)
    let muTextoDrag = null; // { forma, moved, startX, startY }

    document.addEventListener('pointermove', ev => {
        if (muTextoDrag) {
            // pequeno limiar: sem ele o tap de remoção viraria um micro-arrasto
            if (!muTextoDrag.moved && Math.hypot(ev.clientX - muTextoDrag.startX, ev.clientY - muTextoDrag.startY) < 4) return;
            muTextoDrag.moved = true;
            const p = muPctCampo(ev.clientX, ev.clientY);
            muTextoDrag.forma.tx = p.x;
            muTextoDrag.forma.ty = p.y;
            muDesenharFormas();
            return;
        }
        if (!muFormaDraw) return;
        const p = muPctCampo(ev.clientX, ev.clientY);
        muFormaDraw.x2 = p.x;
        muFormaDraw.y2 = p.y;
        if (muFormaDraw.tipo === 'livre') {
            const ult = muFormaDraw.pontos[muFormaDraw.pontos.length - 1];
            if (Math.hypot(p.x - ult.x, p.y - ult.y) > 0.7) muFormaDraw.pontos.push(p);
        }
        muDesenharFormas();
    });

    document.addEventListener('pointerup', () => {
        if (muTextoDrag) {
            // arrasto de verdade não conta como 1º clique da remoção
            if (muTextoDrag.moved) muFormaUltimoTap = { forma: null, t: 0 };
            muTextoDrag = null;
            return;
        }
        if (!muFormaDraw) return;
        const f = muFormaDraw;
        muFormaDraw = null;
        const c = muFormaBBox(f);
        // Extensão mínima: descarta o "desenho" de um clique parado (inclusive
        // os dois pointerdowns do duplo clique que remove uma forma)
        if (c.w < 2 && c.h < 2) { muDesenharFormas(); return; }
        muFormaUltimoTap = { forma: null, t: 0 }; // desenho concluído não conta como 1º clique da remoção
        muFormas.push(f);
        muDesenharFormas();
        muPedirTextoForma(f);
    });

    document.addEventListener('pointercancel', () => {
        muTextoDrag = null;
        if (!muFormaDraw) return;
        muFormaDraw = null;
        muDesenharFormas();
    });

    function muFormaBBox(f) {
        let minX, maxX, minY, maxY;
        if (f.tipo === 'livre') {
            const xs = f.pontos.map(p => p.x), ys = f.pontos.map(p => p.y);
            minX = Math.min.apply(null, xs); maxX = Math.max.apply(null, xs);
            minY = Math.min.apply(null, ys); maxY = Math.max.apply(null, ys);
        } else {
            minX = Math.min(f.x1, f.x2); maxX = Math.max(f.x1, f.x2);
            minY = Math.min(f.y1, f.y2); maxY = Math.max(f.y1, f.y2);
        }
        return { x: minX, y: minY, w: maxX - minX, h: maxY - minY,
                 cx: (minX + maxX) / 2, cy: (minY + maxY) / 2 };
    }

    function muFormaEl(f, w, h) {
        const ns = 'http://www.w3.org/2000/svg';
        if (f.tipo === 'livre') {
            const el = document.createElementNS(ns, 'polyline');
            el.setAttribute('points', f.pontos.map(p =>
                (p.x / 100 * w).toFixed(1) + ',' + (p.y / 100 * h).toFixed(1)).join(' '));
            return el;
        }
        const b = muFormaBBox(f);
        const x = b.x / 100 * w, y = b.y / 100 * h, lw = b.w / 100 * w, lh = b.h / 100 * h;
        if (f.tipo === 'elipse') {
            const el = document.createElementNS(ns, 'ellipse');
            el.setAttribute('cx', x + lw / 2);
            el.setAttribute('cy', y + lh / 2);
            el.setAttribute('rx', lw / 2);
            el.setAttribute('ry', lh / 2);
            return el;
        }
        const el = document.createElementNS(ns, 'rect');
        el.setAttribute('x', x);
        el.setAttribute('y', y);
        el.setAttribute('width', lw);
        el.setAttribute('height', lh);
        el.setAttribute('rx', 6);
        return el;
    }

    function muDesenharFormas() {
        const campo = document.getElementById('muCampo');
        const g = document.getElementById('muFormasG');
        if (!campo || !g) return;

        while (g.firstChild) g.removeChild(g.firstChild);
        const w = campo.clientWidth, h = campo.clientHeight;
        const todas = muFormaDraw ? muFormas.concat([muFormaDraw]) : muFormas;

        todas.forEach(f => {
            const remover = () => {
                muFormas = muFormas.filter(o => o !== f);
                muDesenharFormas();
            };
            const el = muFormaEl(f, w, h);
            el.classList.add('mu-forma');
            if (f !== muFormaDraw) { el.__muForma = f; el.__muRemover = remover; }
            g.appendChild(el);

            if (f.texto) {
                const b = muFormaBBox(f);
                const t = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                t.classList.add('mu-forma-texto');
                // tx/ty: posição escolhida arrastando o rótulo; sem eles, centro da forma
                t.setAttribute('x', (f.tx != null ? f.tx : b.cx) / 100 * w);
                t.setAttribute('y', (f.ty != null ? f.ty : b.cy) / 100 * h);
                t.setAttribute('text-anchor', 'middle');
                t.setAttribute('dominant-baseline', 'middle');
                t.textContent = f.texto;
                t.__muForma = f;
                t.__muRemover = remover;
                g.appendChild(t);
            }
        });
    }

    // Input flutuante sobre a forma recém-desenhada para o texto opcional
    function muPedirTextoForma(f) {
        const campo = document.getElementById('muCampo');
        document.querySelector('.mu-forma-input')?.remove();

        const b = muFormaBBox(f);
        const inp = document.createElement('input');
        inp.className = 'mu-forma-input';
        inp.placeholder = 'Texto (opcional) — Enter';
        inp.style.left = Math.max(9, Math.min(91, b.cx)) + '%';
        inp.style.top = Math.max(6, Math.min(94, b.cy)) + '%';

        let feito = false;
        const fim = confirmar => {
            if (feito) return;
            feito = true;
            if (confirmar && inp.value.trim()) {
                f.texto = inp.value.trim();
                muDesenharFormas();
            }
            inp.remove();
        };
        inp.addEventListener('keydown', ev => {
            ev.stopPropagation(); // Esc aqui só fecha o input, não o modo
            if (ev.key === 'Enter') fim(true);
            else if (ev.key === 'Escape') fim(false);
        });
        inp.addEventListener('blur', () => fim(true)); // clicar fora confirma o digitado
        inp.addEventListener('pointerdown', ev => ev.stopPropagation()); // não inicia outro desenho

        campo.appendChild(inp);
        inp.focus();
    }

    document.addEventListener('keydown', ev => {
        if (ev.key !== 'Escape') return;
        if (muModoSeta) {
            if (muSetaOrigem) muLimparOrigemSeta();
            else muToggleModoSeta();
        } else if (muModoForma) {
            if (muFormaDraw) { muFormaDraw = null; muDesenharFormas(); }
            else muToggleModoForma();
        } else if (muEmTransmissao() && !document.fullscreenElement) {
            // Em tela cheia o Esc é do navegador (sai dela); o próximo sai do modo
            muToggleTransmissao();
        }
    });
