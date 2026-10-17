/* Abertura 3D do jogo — modo transmissão do "Mesmo campo" (/Jogos/Analisar).

   No estilo das aberturas de videogame (EA FC/FIFA): estádio à noite, a
   câmera desce até o gramado, os escudos e as informações do jogo entram, e
   as escalações são montadas jogador a jogador, primeiro o mandante, depois o
   visitante. No fim a câmera pousa exatamente sobre o campo da transmissão e
   a abertura some, deixando a análise de onde ela estava.

   Os jogadores NÃO vêm do servidor: são lidos dos campinhos da análise na hora
   (posição, número, foto, nome), então a abertura mostra a escalação como o
   analista deixou. O resto (competição, estádio, escudos, técnicos) vem de
   window.ANALISAR.abertura. Estilos em css/abertura-jogo.css.

   Só leitura: nada aqui altera a escalação nem salva nada. */
(function () {
    'use strict';

    let execucao = null; // { cancelada } da abertura em andamento

    // ── Dados ──────────────────────────────────────────────────────────────
    // Campinho vertical do time → posição no campo horizontal: a mesma conta do
    // "Mesmo campo" (casa com o gol à esquerda, visitante à direita).
    function jogadoresDo(campoId, casa) {
        const lista = [];
        document.querySelectorAll(`#${campoId} .posicao-drop[data-temjogador="1"]`).forEach(slot => {
            const x = parseFloat(slot.style.left), y = parseFloat(slot.style.top);
            if (isNaN(x) || isNaN(y)) return;
            const foto = slot.querySelector('img.player-foto');
            lista.push({
                X: casa ? 100 - y : y,
                Y: casa ? x : 100 - x,
                numero: (slot.querySelector('.player-num')?.textContent || slot.querySelector('.player-circle')?.textContent || '').trim(),
                nome: (slot.querySelector('.player-name')?.textContent || '').trim(),
                foto: foto?.getAttribute('src') || '',
                sigla: slot.dataset.posicao || '',
            });
        });
        // Do goleiro ao ataque: é a ordem em que a escalação é "montada"
        lista.sort((a, b) => casa ? a.X - b.X : b.X - a.X);
        return lista;
    }

    function formacao(nomeSelect) {
        const txt = document.querySelector(`select[name="${nomeSelect}"]`)?.selectedOptions?.[0]?.textContent || '';
        return txt.trim();
    }

    function dados() {
        const A = window.ANALISAR || {};
        const ab = A.abertura || {};
        return {
            ab,
            casa: {
                lado: 'casa', nome: A.timeCasaNome || '', escudo: ab.escudoCasa || '',
                formacao: formacao('formacaoCasaId'), treinador: ab.treinadorCasa || '',
                jogadores: jogadoresDo('campoCasa', true),
            },
            vis: {
                lado: 'vis', nome: A.timeVisitanteNome || '', escudo: ab.escudoVis || '',
                formacao: formacao('formacaoVisitanteId'), treinador: ab.treinadorVis || '',
                jogadores: jogadoresDo('campoVisitante', false),
            },
        };
    }

    // ── DOM ────────────────────────────────────────────────────────────────
    function h(tag, cls, texto) {
        const el = document.createElement(tag);
        if (cls) el.className = cls;
        if (texto != null) el.textContent = texto;
        return el;
    }

    function img(src, cls) {
        const el = h('img', cls);
        el.src = src;
        el.alt = '';
        el.onerror = () => el.remove(); // escudo/foto quebrado some em vez do ícone de imagem partida
        return el;
    }

    function dataPorExtenso(iso) {
        if (!iso) return '';
        const d = new Date(iso);
        if (isNaN(d)) return '';
        const dia = d.toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' });
        const hora = d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
        // "quarta-feira, 22 de abril" → só a primeira letra maiúscula
        return `${dia.charAt(0).toUpperCase()}${dia.slice(1)} · ${hora}`;
    }

    function montarFundo() {
        const fundo = h('div', 'ab-fundo');
        fundo.appendChild(h('div', 'ab-arquibancada'));
        for (let i = 0; i < 4; i++) fundo.appendChild(h('div', `ab-refletor ab-refletor-${i + 1}`));
        // Flashes da torcida: pontos que piscam em tempos diferentes
        const flashes = h('div', 'ab-flashes');
        for (let i = 0; i < 70; i++) {
            const f = h('span');
            f.style.left = (Math.random() * 100).toFixed(1) + '%';
            f.style.top = (8 + Math.random() * 38).toFixed(1) + '%';
            f.style.animationDelay = (Math.random() * 4).toFixed(2) + 's';
            f.style.animationDuration = (1.5 + Math.random() * 3).toFixed(2) + 's';
            flashes.appendChild(f);
        }
        fundo.appendChild(flashes);
        return fundo;
    }

    function montarCampo(d) {
        const cena = h('div', 'ab-cena');
        const campo = h('div', 'ab-campo');
        ['ab-linha-meio', 'ab-circulo', 'ab-area ab-area-esq', 'ab-area ab-area-dir',
         'ab-pequena ab-pequena-esq', 'ab-pequena ab-pequena-dir', 'ab-brilho']
            .forEach(c => campo.appendChild(h('div', c)));

        [d.casa, d.vis].forEach(time => {
            time.jogadores.forEach((j, i) => {
                const sombra = h('div', `ab-sombra ab-${time.lado}`);
                sombra.style.left = j.X + '%';
                sombra.style.top = j.Y + '%';

                const jog = h('div', `ab-jog ab-${time.lado}`);
                jog.style.left = j.X + '%';
                jog.style.top = j.Y + '%';
                const dentro = h('div', 'ab-jog-in');
                const circ = h('div', 'ab-circ');
                if (j.foto) circ.appendChild(img(j.foto, 'ab-foto'));
                circ.appendChild(h('span', 'ab-num', j.numero));
                dentro.appendChild(circ);
                dentro.appendChild(h('div', 'ab-nome', j.nome));
                jog.appendChild(dentro);

                campo.appendChild(sombra);
                campo.appendChild(jog);
                j.el = jog;
                j.sombra = sombra;
                j.ordem = i;
            });
        });

        cena.appendChild(campo);
        return { cena, campo };
    }

    function montarConfronto(d) {
        const ab = d.ab;
        const box = h('div', 'ab-confronto');

        const comp = h('div', 'ab-comp');
        if (ab.competicaoLogo) comp.appendChild(img(ab.competicaoLogo, 'ab-comp-logo'));
        const partes = [ab.competicao];
        if (ab.grupo) partes.push(`Grupo ${ab.grupo}`);
        else if (ab.rodada > 0) partes.push(`Rodada ${ab.rodada}`);
        comp.appendChild(h('span', null, partes.filter(Boolean).join(' · ') || 'Partida'));
        box.appendChild(comp);

        const times = h('div', 'ab-times');
        [d.casa, 'x', d.vis].forEach(t => {
            if (t === 'x') { times.appendChild(h('div', 'ab-x', '×')); return; }
            const col = h('div', `ab-time ab-time-${t.lado}`);
            const esc = h('div', 'ab-escudo');
            if (t.escudo) esc.appendChild(img(t.escudo));
            col.appendChild(esc);
            col.appendChild(h('div', 'ab-time-nome', t.nome));
            times.appendChild(col);
        });
        box.appendChild(times);

        const meta = h('div', 'ab-meta');
        [['🏟️', ab.estadio], ['📅', dataPorExtenso(ab.data)], ['🟨', ab.arbitro ? `Árbitro: ${ab.arbitro}` : '']]
            .filter(([, t]) => t)
            .forEach(([ic, t]) => {
                const item = h('span', 'ab-meta-item');
                item.appendChild(h('span', 'ab-meta-ic', ic));
                item.appendChild(h('span', null, t));
                meta.appendChild(item);
            });
        box.appendChild(meta);
        return box;
    }

    function montarPainel(time) {
        const p = h('div', `ab-painel ab-painel-${time.lado}`);
        const cab = h('div', 'ab-painel-cab');
        if (time.escudo) cab.appendChild(img(time.escudo, 'ab-painel-escudo'));
        const tit = h('div');
        tit.appendChild(h('div', 'ab-painel-nome', time.nome));
        const sub = [time.formacao, time.treinador ? `Téc. ${time.treinador}` : ''].filter(Boolean).join(' · ');
        if (sub) tit.appendChild(h('div', 'ab-painel-sub', sub));
        cab.appendChild(tit);
        p.appendChild(cab);
        p.appendChild(h('div', 'ab-painel-rotulo', 'Escalação'));

        const ol = h('ol', 'ab-lista');
        time.jogadores.forEach(j => {
            const li = h('li');
            li.appendChild(h('span', 'ab-lista-num', j.numero));
            li.appendChild(h('span', 'ab-lista-nome', j.nome));
            li.appendChild(h('span', 'ab-lista-pos', j.sigla));
            ol.appendChild(li);
            j.linha = li;
        });
        p.appendChild(ol);
        return p;
    }

    // ── Câmera ─────────────────────────────────────────────────────────────
    // A câmera é o próprio gramado se mexendo: inclinação (rx), giro (rz),
    // distância (tz) e deslocamento no plano do campo (tx). Os jogadores leem
    // as mesmas variáveis para ficar sempre de frente para a câmera.
    function camera(campo, { rx, rz = 0, tz = 0, tx = 0 }, dur) {
        if (dur != null) campo.style.setProperty('--ab-dur', dur + 'ms');
        campo.style.setProperty('--ab-rx', rx + 'deg');
        campo.style.setProperty('--ab-rz', rz + 'deg');
        campo.style.setProperty('--ab-tz', tz + 'px');
        campo.style.setProperty('--ab-tx', tx + '%');
    }

    function esperar(ms, exec) {
        return new Promise(res => {
            const t = setTimeout(res, ms);
            exec.timers.push(t);
        });
    }

    // ── Roteiro ────────────────────────────────────────────────────────────
    async function roteiro(root, partes, d, exec) {
        const { campo, confronto, painelCasa, painelVis, chamada } = partes;
        const vivo = () => !exec.cancelada;
        // Ritmo de narrador lendo a escalação: um jogador por vez, com tempo de
        // "falar" o nome, e um respiro a mais quando muda a linha
        // (goleiro → defesa → meio → ataque).
        const cadencia = 1050;   // ms por jogador
        const trocaDeLinha = 450; // ms extras ao passar para a linha seguinte

        async function montarTime(time, painel) {
            painel.classList.add('on');
            await esperar(700, exec);
            let distAnterior = null;
            for (const j of time.jogadores) {
                if (!vivo()) return;
                // Distância do próprio gol (0 = linha de fundo): salto grande = nova linha
                const dist = time.lado === 'casa' ? j.X : 100 - j.X;
                if (distAnterior != null && dist - distAnterior > 10) {
                    await esperar(trocaDeLinha, exec);
                    if (!vivo()) return;
                }
                distAnterior = dist;

                time.jogadores.forEach(o => { o.el.classList.remove('atual'); o.linha.classList.remove('atual'); });
                j.el.classList.add('on', 'atual');
                j.sombra.classList.add('on');
                j.linha.classList.add('on', 'atual');
                anunciar(chamada, time, j);
                await esperar(cadencia, exec);
            }
            time.jogadores.forEach(o => { o.el.classList.remove('atual'); o.linha.classList.remove('atual'); });
            chamada.classList.remove('on');
        }

        // 1. Estádio e câmera descendo até o gramado
        camera(campo, { rx: 76, rz: -28, tz: -1100 }, 0);
        await esperar(60, exec);
        root.classList.add('ab-cena-on');
        camera(campo, { rx: 62, rz: -9, tz: -260 }, 2600);
        await esperar(700, exec); if (!vivo()) return;

        // 2. Confronto: competição, escudos, nomes e informações do jogo
        confronto.classList.add('on');
        await esperar(3600, exec); if (!vivo()) return;

        // 3. Mandante: câmera vai para o lado dele e o time é montado
        confronto.classList.add('compacto');
        camera(campo, { rx: 54, rz: 0, tz: -40, tx: 21 }, 1700);
        await esperar(900, exec); if (!vivo()) return;
        await montarTime(d.casa, painelCasa); if (!vivo()) return;
        await esperar(1100, exec); if (!vivo()) return;

        // 4. Visitante — o mandante sai de cena para o gramado ficar só com
        // quem está sendo anunciado; ele volta quando os dois times se juntam.
        painelCasa.classList.remove('on');
        mostrarTime(d.casa, false);
        camera(campo, { rx: 54, rz: 0, tz: -40, tx: -21 }, 1900);
        await esperar(1000, exec); if (!vivo()) return;
        await montarTime(d.vis, painelVis); if (!vivo()) return;
        await esperar(1100, exec); if (!vivo()) return;

        // 5. Os dois times juntos (o mandante reaparece), e a câmera pousa
        // sobre o campo da transmissão
        painelVis.classList.remove('on');
        camera(campo, { rx: 48, rz: 0, tz: -160, tx: 0 }, 1600);
        mostrarTime(d.casa, true, 90);
        await esperar(1900, exec); if (!vivo()) return;

        confronto.classList.add('sai');
        root.classList.add('ab-pouso');
        pousarNoCampoReal(campo);
        camera(campo, { rx: 0, rz: 0, tz: 0, tx: 0 }, 1500);
        await esperar(1650, exec); if (!vivo()) return;

        encerrar(exec, false);
    }

    // Tira/devolve um time inteiro do gramado (jogador e sombra). Na volta,
    // "passo" escalona a reentrada do goleiro ao ataque.
    function mostrarTime(time, visivel, passo = 0) {
        time.jogadores.forEach((j, i) => {
            const atraso = visivel ? i * passo : 0;
            j.el.style.transitionDelay = j.sombra.style.transitionDelay = atraso ? `0ms, ${atraso}ms` : '';
            j.el.classList.toggle('oculto', !visivel);
            j.sombra.classList.toggle('oculto', !visivel);
        });
    }

    // Tarja de TV com o jogador que está sendo "chamado": número grande na cor
    // do time, nome e posição. Recriar a animação a cada jogador dá o corte
    // de uma chamada para a outra.
    // Siglas de PosicaoJogadorHelper.Sigla
    const NOME_POSICAO = {
        GOL: 'Goleiro', ZAG: 'Zagueiro', LD: 'Lateral-direito', LE: 'Lateral-esquerdo',
        AD: 'Ala direito', AE: 'Ala esquerdo', VOL: 'Volante', MEI: 'Meia',
        MEO: 'Meia ofensivo', PD: 'Ponta-direita', PE: 'Ponta-esquerda',
        ATA: 'Atacante', CA: 'Centroavante', DEF: 'Defensor',
    };

    function anunciar(chamada, time, j) {
        chamada.className = `ab-chamada ab-${time.lado}`;
        chamada.querySelector('.ab-chamada-num').textContent = j.numero;
        chamada.querySelector('.ab-chamada-nome').textContent = j.nome;
        chamada.querySelector('.ab-chamada-pos').textContent =
            [NOME_POSICAO[j.sigla] || j.sigla, time.nome].filter(Boolean).join(' · ');
        void chamada.offsetWidth; // reinicia a animação de entrada
        chamada.classList.add('on');
    }

    function montarChamada() {
        const c = h('div', 'ab-chamada');
        c.appendChild(h('div', 'ab-chamada-num'));
        const txt = h('div', 'ab-chamada-txt');
        txt.appendChild(h('div', 'ab-chamada-nome'));
        txt.appendChild(h('div', 'ab-chamada-pos'));
        c.appendChild(txt);
        return c;
    }

    // No pouso o gramado da abertura assume o retângulo exato do #campoUnico
    // (que já está na tela, por baixo): as posições são as mesmas, então a
    // troca de um para o outro não "pula".
    // As coordenadas são relativas ao .ab-root: na gravação ele é uma caixa
    // 16:9 no meio da janela, e não a janela inteira.
    function pousarNoCampoReal(campo) {
        const alvo = document.getElementById('campoUnico')?.getBoundingClientRect();
        if (!alvo || !alvo.width) return;
        const base = campo.closest('.ab-root').getBoundingClientRect();
        campo.style.left = (alvo.left - base.left + alvo.width / 2) + 'px';
        campo.style.top = (alvo.top - base.top + alvo.height / 2) + 'px';
        campo.style.width = alvo.width + 'px';
    }

    // ── Ciclo de vida ──────────────────────────────────────────────────────
    function onTecla(ev) {
        if (!execucao) return;
        if (ev.key === 'Escape' || ev.key === ' ' || ev.key === 'Enter') {
            // Captura na window: roda antes do Esc da transmissão (que sairia do modo)
            ev.preventDefault();
            ev.stopImmediatePropagation();
            encerrar(execucao, true);
        }
    }

    function encerrar(exec, pulou) {
        if (!exec || exec.encerrada) return;
        exec.encerrada = true;
        exec.cancelada = true;
        exec.timers.forEach(clearTimeout);
        exec.root.classList.add(pulou ? 'ab-pulando' : 'ab-saindo');
        setTimeout(() => {
            exec.root.remove();
            exec.fim({ pulou });
        }, pulou ? 350 : 700);
        if (execucao === exec) execucao = null;
        window.removeEventListener('keydown', onTecla, true);
    }

    // Devolve uma Promise que resolve com { pulou } quando a abertura some da
    // tela. { gravacao: true } roda na caixa 16:9 e sem o botão Pular (o
    // gravador, js/gravacao-abertura.js, recorta essa caixa da captura da aba);
    // aoIniciar(root) entrega o elemento antes do primeiro quadro.
    function tocar(opcoes = {}) {
        if (execucao) encerrar(execucao, true);

        const d = dados();
        const root = h('div', 'ab-root');
        if (opcoes.gravacao) root.classList.add('ab-gravando');
        root.setAttribute('role', 'dialog');
        root.setAttribute('aria-label', `Abertura: ${d.casa.nome} × ${d.vis.nome}`);

        // Cores das camisas escolhidas na análise (variáveis do :root)
        const css = getComputedStyle(document.documentElement);
        ['casa', 'vis'].forEach(l => {
            root.style.setProperty(`--ab-cor-${l}`, css.getPropertyValue(`--cor-camisa-${l}`).trim() || (l === 'casa' ? '#dc2626' : '#2563eb'));
            root.style.setProperty(`--ab-num-${l}`, css.getPropertyValue(`--cor-numero-${l}`).trim() || '#fff');
        });

        root.appendChild(montarFundo());
        const { cena, campo } = montarCampo(d);
        root.appendChild(cena);
        const confronto = montarConfronto(d);
        root.appendChild(confronto);
        const painelCasa = montarPainel(d.casa);
        const painelVis = montarPainel(d.vis);
        root.appendChild(painelCasa);
        root.appendChild(painelVis);
        const chamada = montarChamada();
        root.appendChild(chamada);

        const pular = h('button', 'ab-pular', 'Pular ⏭');
        pular.type = 'button';
        pular.title = 'Pular a abertura (Esc, espaço ou Enter)';
        root.appendChild(pular);

        document.body.appendChild(root);

        let fim;
        const terminou = new Promise(res => { fim = res; });
        const exec = { root, timers: [], cancelada: false, encerrada: false, fim };
        execucao = exec;
        pular.addEventListener('click', () => encerrar(exec, true));
        window.addEventListener('keydown', onTecla, true);
        opcoes.aoIniciar?.(root);

        roteiro(root, { campo, confronto, painelCasa, painelVis, chamada }, d, exec);
        return terminou;
    }

    window.AberturaJogo = { tocar, pular: () => execucao && encerrar(execucao, true) };
})();
