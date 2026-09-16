/* Comportamento da tela de análise tática (/Jogos/Analisar).
   Extraído dos blocos <script> inline da view: ~120 KB que eram retransmitidos
   a cada partida aberta. Aqui vira arquivo estático versionado
   (asp-append-version), que o navegador baixa uma vez.

   Os dados do jogo chegam pelo objeto window.ANALISAR, montado inline na view
   — é a única parte que muda de partida para partida.

   Escopo GLOBAL de propósito: o HTML da view tem 79 handlers inline
   (onclick="abrirModalEvento()" e afins) que chamam estas funções pelo nome.
   Envolver o arquivo num IIFE ou carregá-lo como módulo quebraria todos. */

        function toggleToolbarEventos() {
            var toolbar = document.getElementById('mbToolbar');
            var icon = document.getElementById('mbToolbarToggleIcon');
            var aberto = toolbar.style.display !== 'none';
            toolbar.style.display = aberto ? 'none' : '';
            icon.style.transform = aberto ? 'rotate(0deg)' : 'rotate(180deg)';
        }

        function toggleOutrosEventos() {
            var tl = document.getElementById('mbTimelineOutros');
            var icon = document.getElementById('mbOutrosToggleIcon');
            var aberto = tl.style.display !== 'none';
            tl.style.display = aberto ? 'none' : '';
            icon.style.transform = aberto ? 'rotate(0deg)' : 'rotate(180deg)';
        }

        function toggleEstatisticasPanel() {
            var painel = document.getElementById('painelEstatisticas');
            var icon = document.getElementById('mbEstatisticasToggleIcon');
            var escalacao = document.getElementById('painelEscalacao');
            if (!painel) return;
            var aberto = painel.style.display !== 'none';
            painel.style.display = aberto ? 'none' : '';
            if (!aberto) escalacao.style.display = 'none';
            else escalacao.style.display = '';
            icon.style.transform = aberto ? 'rotate(0deg)' : 'rotate(180deg)';
            // Sincroniza com o tab btn legado
            if (document.getElementById('tabBtnEstatisticas')) {
                document.getElementById('tabBtnEstatisticas').classList.toggle('active', !aberto);
            }
        }

    function mostrarPainel(painel) {
        var estatisticas = painel === 'estatisticas';
        document.getElementById('painelEscalacao').style.display = estatisticas ? 'none' : '';
        document.getElementById('painelEstatisticas').style.display = estatisticas ? '' : 'none';
        if (estatisticas) {
            document.getElementById('tabBtnEscalacaoInicial').classList.remove('active');
            document.getElementById('tabBtnEscalacaoFinal').classList.remove('active');
        }
    }

    // ── Estado global ──────────────────────────────────────────────────────
    const usados = { casa: new Set(), vis: new Set() };

    let dragData          = {};
    let draggingPosicao   = false;
    let posicaoArrastando = null;

    // Referência fixa ao form principal — evita pegar o form errado
    const formPrincipal = document.getElementById('formSalvarEscalacao');

    const camisas = ANALISAR.camisas;
    const faseEscalacaoAtual = ANALISAR.faseEscalacaoAtual;
    const JOGO_ID_OBS_TAG = ANALISAR.jogoId;
    const JOGADORES_ESCALADOS_OBS = ANALISAR.jogadoresEscalados;

    // ── Menção de jogador com "@" nas caixas de observação ──────────────────
    // Implementação em wwwroot/js/mencao-jogador.js (compartilhada com /AnotacoesTime).
    const renderizarTextoComMencoes = window.MencaoJogador.renderizarTexto;
    const ativarMencaoJogador = window.MencaoJogador.ativar;

    function destacarMencoesExistentes(containerId, jogadores) {
        document.querySelectorAll(`#${containerId} .obs-tag-texto`).forEach(el => {
            el.innerHTML = renderizarTextoComMencoes(el.textContent, jogadores);
        });
    }
    destacarMencoesExistentes('obsTagList', JOGADORES_ESCALADOS_OBS);
    ativarMencaoJogador(document.getElementById('obsTagTexto'), () => JOGADORES_ESCALADOS_OBS);

    const rotulosObsTag = { MANDANTE: 'Mandante', VISITANTE: 'Visitante', COMPETICAO: 'Competição', JOGADOR: 'Jogador', MARCO: 'Marco' };

    function atualizarSeletorJogadorObs() {
        const tipo = document.getElementById('obsTagTipo').value;
        document.getElementById('obsTagJogador').style.display = tipo === 'JOGADOR' ? '' : 'none';
    }

    function autoResizeObsTagTexto(textarea) {
        if (!textarea) return;
        textarea.style.height = 'auto';
        textarea.style.height = `${textarea.scrollHeight}px`;
    }

    function adicionarLinhaObsTag(obs) {
        const container = document.getElementById('obsTagList');
        if (!container) return;

        const row = document.createElement('div');
        row.className = 'obs-tag-row';
        row.dataset.id = obs.id;
        const nomeJogadorHtml = obs.jogadorNome
            ? `<span class="obs-tag-jogador-nome">${obs.jogadorNome}</span>`
            : '';
        row.innerHTML = `
            <span class="obs-tag-badge obs-tag-badge-${obs.tipo.toLowerCase()}">${rotulosObsTag[obs.tipo] || obs.tipo}</span>
            ${nomeJogadorHtml}
            <span class="obs-tag-texto"></span>
            <span class="obs-tag-editar" title="Editar">&#9998;</span>
            <span class="obs-tag-remover" title="Remover">&times;</span>
        `;
        row.querySelector('.obs-tag-texto').innerHTML = renderizarTextoComMencoes(obs.texto, JOGADORES_ESCALADOS_OBS);
        row.querySelector('.obs-tag-editar').addEventListener('click', () => editarObservacaoTag(obs.id, row.querySelector('.obs-tag-editar')));
        row.querySelector('.obs-tag-remover').addEventListener('click', () => removerObservacaoTag(obs.id, row.querySelector('.obs-tag-remover')));
        container.appendChild(row);
    }

    async function editarObservacaoTag(id, btn) {
        const row = btn.closest('.obs-tag-row');
        const textoEl = row.querySelector('.obs-tag-texto');
        const textoAtual = textoEl.textContent;

        const textoTrim = await window.MencaoJogador.editarTexto({
            titulo: 'Editar observação',
            valor: textoAtual,
            getJogadores: () => JOGADORES_ESCALADOS_OBS
        });
        if (textoTrim === null) return;
        if (!textoTrim) { alert('O texto da observação não pode ficar vazio.'); return; }
        if (textoTrim === textoAtual) return;

        try {
            const resp = await fetch('/Jogos/EditarObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id, texto: textoTrim })
            });
            if (resp.ok) {
                textoEl.innerHTML = renderizarTextoComMencoes(textoTrim, JOGADORES_ESCALADOS_OBS);
            } else {
                alert('Erro ao editar a observação.');
            }
        } catch (e) {
            alert('Erro de conexão ao editar a observação.');
        }
    }

    async function adicionarObservacaoTag() {
        const tipo = document.getElementById('obsTagTipo').value;
        const jogadorSelect = document.getElementById('obsTagJogador');
        const textoEl = document.getElementById('obsTagTexto');
        const texto = (textoEl.value || '').trim();

        if (!texto) { alert('Escreva o texto da observação.'); return; }
        if (tipo === 'JOGADOR' && !jogadorSelect.value) { alert('Selecione o jogador.'); return; }

        try {
            const resp = await fetch('/Jogos/AdicionarObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    jogoId: JOGO_ID_OBS_TAG,
                    tipo,
                    jogadorId: tipo === 'JOGADOR' ? parseInt(jogadorSelect.value, 10) : null,
                    texto
                })
            });
            if (resp.ok) {
                const obs = await resp.json();
                adicionarLinhaObsTag(obs);
                textoEl.value = '';
                autoResizeObsTagTexto(textoEl);
            } else {
                alert('Erro ao salvar a observação.');
            }
        } catch {
            alert('Erro de conexão ao salvar a observação.');
        }
    }

    async function removerObservacaoTag(id, btn) {
        if (!confirm('Remover esta observação?')) return;
        try {
            const resp = await fetch('/Jogos/RemoverObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id })
            });
            if (resp.ok) {
                btn.closest('.obs-tag-row')?.remove();
            } else {
                alert('Erro ao remover a observação.');
            }
        } catch {
            alert('Erro de conexão ao remover a observação.');
        }
    }

    document.querySelectorAll('#obsTagList .obs-tag-remover').forEach(btn => {
        btn.addEventListener('click', () => removerObservacaoTag(parseInt(btn.closest('.obs-tag-row').dataset.id, 10), btn));
    });

    // ── Inicializa conjuntos de usados ─────────────────────────────────────
    function initUsados() {
        document.querySelectorAll('[id^="escalacaoCasa_"][id$="__JogadorId"]').forEach(el => {
            if (el.value) usados.casa.add(el.value);
        });
        document.querySelectorAll('[id^="escalacaoVisitante_"][id$="__JogadorId"]').forEach(el => {
            if (el.value) usados.vis.add(el.value);
        });
        document.querySelectorAll('[id^="reservaCasa_"][id$="__JogadorId"]').forEach(el => {
            if (el.value) usados.casa.add(el.value);
        });
        document.querySelectorAll('[id^="reservaVis_"][id$="__JogadorId"]').forEach(el => {
            if (el.value) usados.vis.add(el.value);
        });
        atualizarLista('casa');
        atualizarLista('vis');
    }

    function atualizarLista(time) {
        const prefix = time === 'casa' ? 'jogador-casa-' : 'jogador-vis-';
        usados[time].forEach(id => {
            const row = document.getElementById(prefix + id);
            if (row) row.classList.add('usado');
        });
    }

    function marcarUsado(time, jogadorId) {
        usados[time].add(String(jogadorId));
        atualizarLista(time);
    }

    function desmarcarUsado(time, jogadorId) {
        usados[time].delete(String(jogadorId));
        const prefix = time === 'casa' ? 'jogador-casa-' : 'jogador-vis-';
        const row = document.getElementById(prefix + jogadorId);
        if (row) row.classList.remove('usado');
    }

    // ── Drag helpers ───────────────────────────────────────────────────────
    function allowDrop(ev) { ev.preventDefault(); }

    function dragJogador(ev, jogadorId, jogadorNome, jogadorNumero, camisaUrl, time) {
        draggingPosicao = false;
        dragData = { jogadorId: String(jogadorId), jogadorNome, jogadorNumero, camisaUrl, time };
        ev.dataTransfer.setData("text/plain", String(jogadorId));
    }

    // ── Jogador em campo: número ou foto ───────────────────────────────────
    // O slot sempre carrega a foto (quando o jogador tem) E o número; quem
    // escolhe o que aparece é a classe .campo-foto no .an-root, ligada pelo
    // seletor "👤 campo" da barra — mesmo HTML que o Razor gera nos titulares.
    function htmlCirculoCampo(jogadorId, numero, circleClass) {
        const dados = DADOS_JOGADORES ? DADOS_JOGADORES[jogadorId] : null;
        const foto = dados && dados.foto ? dados.foto : '';
        return `<div class="player-circle ${circleClass}${foto ? ' tem-foto' : ''}">` +
               (foto ? `<img class="player-foto" src="${foto}" alt="" loading="lazy">` : '') +
               `<span class="player-num">${numero == null ? '' : numero}</span></div>`;
    }

    // Guarda a escolha no navegador: vale para as próximas partidas abertas
    // nesta máquina (é preferência visual, não um dado da análise).
    function trocarVisualCampo(modo) {
        const root = document.querySelector('.an-root');
        if (root) root.classList.toggle('campo-foto', modo === 'foto');
        try { localStorage.setItem('analisar.campoVisual', modo); } catch (e) { /* navegação anônima */ }
    }
    window.trocarVisualCampo = trocarVisualCampo;

    (function aplicarVisualCampoSalvo() {
        let modo = 'numero';
        try { modo = localStorage.getItem('analisar.campoVisual') || 'numero'; } catch (e) { /* idem */ }
        const sel = document.getElementById('campoVisual');
        if (sel) sel.value = modo;
        trocarVisualCampo(modo);
    })();

    // ── Trocar dois jogadores de lugar ─────────────────────────────────────
    // Arrastar um titular sobre outro troca os dois de slot: cada um herda a
    // posição do outro (coordenadas no campo E posição nominal, porque o slot
    // é o registro de Escalacao — só o JogadorId muda de lado).
    function slotPorIndex(index) {
        return document.querySelector(`.posicao-drop[data-index='${index}']`);
    }

    function hiddenJogadorDoSlot(index) {
        const prefix = index.startsWith('casa_') ? 'escalacaoCasa' : 'escalacaoVisitante';
        return document.getElementById(`${prefix}_${index.split('_')[1]}__JogadorId`);
    }

    // O botão "+" carrega a posição no onclick gerado pelo Razor; depois da
    // troca ele precisa apontar para a posição nova do slot que o abriga.
    function religarBotaoAvaliar(slotEl) {
        const btn = slotEl.querySelector('.btn-avaliar');
        if (!btn) return;
        const jogadorId = parseInt((btn.id || '').replace('btn-avaliar-', ''), 10);
        if (!jogadorId) return;
        const nome = slotEl.querySelector('.player-name')?.textContent.trim() || '';
        const posicao = slotEl.dataset.posicaoFull || slotEl.dataset.posicao || '';
        btn.removeAttribute('onclick');
        btn.onclick = e => abrirAvaliacao(jogadorId, nome, posicao, e);
    }

    function trocarJogadoresDeSlot(origem, destino) {
        if (!origem || !destino || origem === destino) return false;
        // Só faz sentido dentro do mesmo time
        if (origem.split('_')[0] !== destino.split('_')[0]) return false;

        const elO = slotPorIndex(origem), elD = slotPorIndex(destino);
        const hO  = hiddenJogadorDoSlot(origem), hD = hiddenJogadorDoSlot(destino);
        if (!elO || !elD || !hO || !hD) return false;
        if (!hO.value && !hD.value) return false;

        const idTmp = hO.value;
        hO.value = hD.value;
        hD.value = idTmp;

        const htmlTmp = elO.innerHTML;
        elO.innerHTML = elD.innerHTML;
        elD.innerHTML = htmlTmp;

        const temTmp = elO.dataset.temjogador;
        elO.dataset.temjogador = elD.dataset.temjogador;
        elD.dataset.temjogador = temTmp;

        religarBotaoAvaliar(elO);
        religarBotaoAvaliar(elD);
        return true;
    }

    // Destaque do alvo enquanto se arrasta um titular sobre outro
    document.addEventListener('dragover', ev => {
        if (!draggingPosicao) return;
        const alvo = ev.target.closest?.('.posicao-drop');
        document.querySelectorAll('.posicao-drop.alvo-troca')
            .forEach(el => { if (el !== alvo) el.classList.remove('alvo-troca'); });
        if (alvo && alvo.dataset.index !== posicaoArrastando &&
            alvo.dataset.index.split('_')[0] === String(posicaoArrastando).split('_')[0]) {
            alvo.classList.add('alvo-troca');
        }
    });
    function limparAlvoTroca() {
        document.querySelectorAll('.posicao-drop.alvo-troca')
            .forEach(el => el.classList.remove('alvo-troca'));
    }
    document.addEventListener('dragend', limparAlvoTroca);
    document.addEventListener('drop', limparAlvoTroca);

    // ── Drop em slot do campo ──────────────────────────────────────────────
    function dropEscalacao(ev, index) {
        ev.preventDefault();
        ev.stopPropagation();
        if (draggingPosicao) {
            const origem = posicaoArrastando;
            draggingPosicao   = false;
            posicaoArrastando = null;
            trocarJogadoresDeSlot(origem, index);
            return;
        }

        const { jogadorId, jogadorNome, jogadorNumero, camisaUrl, time } = dragData;
        if (!jogadorId) return;

        if (usados[time].has(jogadorId)) {
            alert("Esse jogador já está em campo ou no banco!");
            return;
        }

        const isCasa  = index.startsWith("casa_");
        const prefix  = isCasa ? "escalacaoCasa" : "escalacaoVisitante";
        const numIdx  = index.split("_")[1];

        const hidden = document.getElementById(`${prefix}_${numIdx}__JogadorId`);
        if (!hidden) return;

        const antigoId = hidden.value;
        if (antigoId) desmarcarUsado(time, antigoId);

        hidden.value = jogadorId;
        marcarUsado(time, jogadorId);

        const posEl = document.querySelector(`.posicao-drop[data-index='${index}']`);
        const nomePosicao = posEl.dataset.posicao;
        const circleClass = isCasa ? 'player-circle-casa' : 'player-circle-vis';
        posEl.innerHTML = `
            <span>${nomePosicao}</span>
            ${htmlCirculoCampo(jogadorId, jogadorNumero, circleClass)}
            <div class="player-name">${jogadorNome}</div>`;
        posEl.appendChild(criarBotaoInfo(jogadorId));
    }

    // ── Drop no banco de reservas ──────────────────────────────────────────
    function dropNoBanco(ev, time) {
        ev.preventDefault();
        ev.stopPropagation();
        if (draggingPosicao) return;

        const { jogadorId, jogadorNome, jogadorNumero, camisaUrl } = dragData;
        if (!jogadorId) return;

        if (usados[time].has(jogadorId)) {
            alert("Esse jogador já está em campo ou no banco!");
            return;
        }

        const bancoId  = time === 'casa' ? 'bancoCasa'         : 'bancoVisitante';
        const totalKey = time === 'casa' ? 'totalReservasCasa' : 'totalReservasVis';
        const prefixH  = time === 'casa' ? 'reservaCasa'       : 'reservaVis';
        const prefixN  = time === 'casa' ? 'reservasCasa'      : 'reservasVisitante';

        const totalEl = document.getElementById(totalKey);
        const idx     = parseInt(totalEl.value);

        // Cria card visual
        const banco = document.getElementById(bancoId);
        const card  = document.createElement('div');
        card.className = 'reserva-card';
        card.id = `reserva-${time}-card-${idx}`;
        const bancoCircleClass = time === 'casa' ? 'player-circle-casa' : 'player-circle-vis';
        card.innerHTML = `
            <span class="rem-btn" onclick="removerReserva('${time}', ${idx})">×</span>
            <div class="player-circle ${bancoCircleClass}" style="width:28px;height:28px;font-size:11px;">${jogadorNumero}</div>
            <div style="max-width:54px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:10px;color:#fff;text-shadow:1px 1px 2px #000;">${jogadorNome}</div>`;
        card.appendChild(criarBotaoInfo(jogadorId));
        banco.appendChild(card);

        // ── Cria inputs hidden DENTRO do formPrincipal ──────────────────
        function addHidden(name, value, inputId) {
            const inp = document.createElement('input');
            inp.type  = 'hidden';
            inp.name  = name;
            inp.value = value;
            if (inputId) inp.id = inputId;
            formPrincipal.appendChild(inp);  // ← sempre no form correto
        }

        addHidden(`${prefixN}[${idx}].Id`,        '0',       null);
        addHidden(`${prefixN}[${idx}].JogadorId`, jogadorId, `${prefixH}_${idx}__JogadorId`);
        addHidden(`${prefixN}[${idx}].PosicaoX`,  '0',       `${prefixH}_${idx}__PosicaoX`);
        addHidden(`${prefixN}[${idx}].PosicaoY`,  '0',       `${prefixH}_${idx}__PosicaoY`);

        totalEl.value = idx + 1;
        marcarUsado(time, jogadorId);
    }

    // ── Remover reserva ────────────────────────────────────────────────────
    function removerReserva(time, idx) {
        const prefixH = time === 'casa' ? 'reservaCasa' : 'reservaVis';
        const prefixN = time === 'casa' ? 'reservasCasa' : 'reservasVisitante';

        const jInp = document.getElementById(`${prefixH}_${idx}__JogadorId`);
        if (!jInp) return;

        const jogadorId = jInp.value;
        desmarcarUsado(time, jogadorId);

        // Remove card visual
        const card = document.getElementById(`reserva-${time}-card-${idx}`);
        if (card) card.remove();

        // Remove inputs hidden
        [`${prefixH}_${idx}__JogadorId`,
         `${prefixH}_${idx}__PosicaoX`,
         `${prefixH}_${idx}__PosicaoY`].forEach(elId => {
            const el = document.getElementById(elId);
            if (el) el.remove();
        });
        // Remove o Id hidden (sem id próprio, localiza pelo name)
        formPrincipal.querySelectorAll(`input[name="${prefixN}[${idx}].Id"]`)
            .forEach(el => el.remove());
    }

    // ── Mover posição dentro do campo ─────────────────────────────────────
    function dragPosicao(ev, index) {
        if (modoSeta) { ev.preventDefault(); return; } // no modo seta o clique seleciona, não arrasta
        draggingPosicao   = true;
        posicaoArrastando = index;
        ev.dataTransfer.setData("text/plain", index);
    }

    function dropNoCampo(ev, campoId) {
        ev.preventDefault();
        ev.stopPropagation();

        if (!draggingPosicao || !posicaoArrastando) return;

        const index = posicaoArrastando;
        const campo = document.getElementById(campoId);
        const rect  = campo.getBoundingClientRect();

        const newX = Math.round(((ev.clientX - rect.left)  / rect.width)  * 100);
        const newY = Math.round(((ev.clientY - rect.top)   / rect.height) * 100);

        if (newX < 0 || newX > 100 || newY < 0 || newY > 100) return;

        const el = document.querySelector(`.posicao-drop[data-index='${index}']`);
        if (!el) return;

        el.style.left = newX + "%";
        el.style.top  = newY + "%";

        const isCasa = index.startsWith("casa_");
        const prefix = isCasa ? "escalacaoCasa" : "escalacaoVisitante";
        const numIdx = index.split("_")[1];

        document.getElementById(`${prefix}_${numIdx}__PosicaoX`).value = newX;
        document.getElementById(`${prefix}_${numIdx}__PosicaoY`).value = newY;

        draggingPosicao   = false;
        posicaoArrastando = null;

        // A origem das setas acompanha o jogador
        desenharSetas(campoId);
    }

    // ── Setas de movimentação dos jogadores ────────────────────────────────
    // Guarda { escalacaoId: [{id, x, y}, ...] } — coordenadas em % do campo.
    const setasStore = ANALISAR.setas;

    let modoSeta = false;
    let setaOrigem = null; // { escId, slotEl, campoId }

    function toggleModoSeta() {
        modoSeta = !modoSeta;
        document.body.classList.toggle('modo-seta', modoSeta);
        document.getElementById('btnModoSeta')?.classList.toggle('ativo', modoSeta);
        const hint = document.getElementById('modoSetaHint');
        if (hint) hint.style.display = modoSeta ? '' : 'none';
        limparOrigemSeta();
    }

    function limparOrigemSeta() {
        if (setaOrigem?.slotEl) setaOrigem.slotEl.classList.remove('seta-origem');
        setaOrigem = null;
    }

    function desenharSetas(campoId) {
        const campo = document.getElementById(campoId);
        const svg = campo?.querySelector('.setas-svg');
        if (!campo || !svg) return;

        svg.querySelectorAll('line').forEach(l => l.remove());
        const w = campo.clientWidth, h = campo.clientHeight;
        const marker = campoId === 'campoCasa' ? 'setaHeadCasa' : 'setaHeadVis';

        campo.querySelectorAll('.posicao-drop[data-escid]').forEach(slot => {
            const escId = slot.dataset.escid;
            const setas = setasStore[escId];
            if (!setas?.length) return;

            const x1 = parseFloat(slot.style.left) / 100 * w;
            const y1 = parseFloat(slot.style.top)  / 100 * h;

            setas.forEach(s => {
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', x1);
                line.setAttribute('y1', y1);
                line.setAttribute('x2', s.x / 100 * w);
                line.setAttribute('y2', s.y / 100 * h);
                line.setAttribute('marker-end', `url(#${marker})`);
                line.addEventListener('dblclick', ev => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    removerSeta(s.id, escId);
                });
                svg.appendChild(line);
            });
        });
    }

    function desenharTodasSetas() {
        desenharSetas('campoCasa');
        desenharSetas('campoVisitante');
    }

    async function adicionarSeta(escId, x, y) {
        try {
            const resp = await fetch('/Jogos/AdicionarSeta', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ escalacaoId: parseInt(escId, 10), x, y })
            });
            if (!resp.ok) {
                alert(resp.status === 400
                    ? 'Salve a escalação antes de adicionar setas a um jogador recém-posicionado.'
                    : 'Erro ao salvar a seta.');
                return;
            }
            const d = await resp.json();
            (setasStore[escId] = setasStore[escId] || []).push({ id: d.id, x: d.x, y: d.y });
            desenharTodasSetas();
        } catch {
            alert('Erro de conexão ao salvar a seta.');
        }
    }

    async function removerSeta(setaId, escId) {
        try {
            const resp = await fetch('/Jogos/RemoverSeta', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: setaId })
            });
            if (!resp.ok) return;
            setasStore[escId] = (setasStore[escId] || []).filter(s => s.id !== setaId);
            desenharTodasSetas();
        } catch { }
    }

    function onCampoClickSeta(ev, campoId) {
        if (!modoSeta) return;
        if (ev.target.tagName === 'line') return; // interação com a própria seta (dblclick remove)

        const slot = ev.target.closest('.posicao-drop[data-escid]');
        if (slot && slot.dataset.temjogador === '1') {
            limparOrigemSeta();
            setaOrigem = { escId: slot.dataset.escid, slotEl: slot, campoId };
            slot.classList.add('seta-origem');
            return;
        }

        if (!setaOrigem || setaOrigem.campoId !== campoId) return;

        const rect = document.getElementById(campoId).getBoundingClientRect();
        const x = Math.max(0, Math.min(100, (ev.clientX - rect.left) / rect.width  * 100));
        const y = Math.max(0, Math.min(100, (ev.clientY - rect.top)  / rect.height * 100));
        adicionarSeta(setaOrigem.escId, x, y);
        // Mantém a origem selecionada: permite adicionar várias setas ao mesmo jogador
    }

    ['campoCasa', 'campoVisitante'].forEach(id => {
        const campo = document.getElementById(id);
        if (!campo) return;
        campo.addEventListener('click', ev => onCampoClickSeta(ev, id));
        new ResizeObserver(() => desenharSetas(id)).observe(campo);
    });

    document.addEventListener('keydown', ev => {
        if (ev.key === 'Escape' && modoSeta) {
            if (setaOrigem) limparOrigemSeta();
            else toggleModoSeta();
        }
    });

    desenharTodasSetas();

    initUsados();
    atualizarSeletorJogadorObs();

    // Critérios carregados do banco via /Notas/BuscarCriterios (Cadastros > Notas)
    let criterios = [];
    let criteriosCarregados = false;

    async function carregarCriterios() {
        if (criteriosCarregados) return;
        try {
            const resp = await fetch('/Notas/BuscarCriterios');
            if (resp.ok) {
                criterios = await resp.json();
                criteriosCarregados = true;
            }
        } catch { }
        // fallback mínimo se a chamada falhar
        if (criterios.length === 0) {
            criterios = [
                { id: 'gol',            label: 'Gol',           peso: 2 },
                { id: 'assistencia',    label: 'Assistência',   peso: 1 },
                { id: 'gol_sofrido',    label: 'Gol sofrido',   peso: -1 },
                { id: 'cartao_amarelo', label: 'Cartão amarelo',peso: -0.5 },
                { id: 'cartao_vermelho',label: 'Cartão vermelho',peso: -1 },
            ];
            criteriosCarregados = true;
        }
    }

    const avaliacoes = {};
    let jogadorAtual = null;
    let estatisticasAtuais = null;

    // jogadorId -> { minutos, goleiroDecisivo }, de /Notas/BuscarContextos.
    let contextosNota = {};

    async function carregarContextosNota() {
        try {
            const jogoId = parseInt(document.getElementById('jogoId').value);
            const resp = await fetch(`/Notas/BuscarContextos?jogoId=${jogoId}`);
            if (!resp.ok) return;
            const lista = await resp.json();
            contextosNota = {};
            lista.forEach(c => { contextosNota[c.jogadorId] = c; });
        } catch { }
    }

    // ── Cor do botão de avaliação conforme a nota do jogador ───────────────
    const AV_NOTA_BASE = 4.0;         // base fixa
    const AV_PISO_POSITIVO = 6.0;     // mais tipos de ação verde que vermelha vale ao menos isto
    const AV_PISO_EQUILIBRADO = 5.0;  // mesmo número de verdes e vermelhas
    const AV_MIN_PARTICIPACAO = 45;   // abaixo disso vale o piso de participação curta
    const AV_PISO_PARTICIPACAO = 5.0;
    const AV_BONUS_GOLEIRO = 2.0;     // 100% dos chutes no alvo defendidos
    const AV_TETO_GOLEIRO = 7.0;
    const AV_BONUS_GOL_VITORIA = 1.0; // marcou o gol que decidiu a partida

    // Até onde a nota sobe pelo merecimento, como o CriteriosNotaHelper.PisoDeMerecimento:
    // conta os TIPOS de ação marcados (cada um vale 1, sem olhar quantidade nem peso).
    function pisoDeMerecimento(av) {
        let verdes = 0, vermelhas = 0;
        criterios.forEach(c => {
            if (!(av[c.id] > 0)) return;
            if (c.peso > 0) verdes++;
            else if (c.peso < 0) vermelhas++;
        });
        if (verdes === 0 && vermelhas === 0) return av.total > 0 ? AV_PISO_POSITIVO : 0;
        if (verdes > vermelhas) return AV_PISO_POSITIVO;
        if (verdes === vermelhas) return AV_PISO_EQUILIBRADO;
        return 0;
    }

    // Mesma régua do CriteriosNotaHelper.NotaFinalComBase (C#), na mesma ordem.
    function calcularNotaFinalJogador(av, jogadorId) {
        if (!av) return null;
        if (av.notaManual !== null && av.notaManual !== undefined)
            return Math.max(0, Math.min(10, av.notaManual));            // override absoluto
        if (av.total === null || av.total === undefined) return null;   // sem nota salva

        const ctx = contextosNota[jogadorId] || {};
        let bruta = AV_NOTA_BASE + av.total;

        const piso = pisoDeMerecimento(av);
        if (bruta < piso) bruta = piso;

        if (ctx.minutos > 0 && ctx.minutos < AV_MIN_PARTICIPACAO && bruta < AV_PISO_PARTICIPACAO)
            bruta = AV_PISO_PARTICIPACAO;

        if (ctx.goleiroDecisivo && bruta < AV_TETO_GOLEIRO) bruta += AV_BONUS_GOLEIRO;

        if (ctx.golDaVitoria) bruta += AV_BONUS_GOL_VITORIA;

        return Math.max(0, Math.min(10, bruta));
    }

    function corDaNota(notaFinal) {
        if (notaFinal === null) return { bg: '#f5c400', fg: '#333' };   // sem nota → amarelo
        if (notaFinal >= 7.5)   return { bg: '#22c55e', fg: '#fff' };   // ótima → verde
        if (notaFinal >= 5.5)   return { bg: '#3b82f6', fg: '#fff' };   // boa → azul
        return { bg: '#ef4444', fg: '#fff' };                           // ruim → vermelho
    }

    function atualizarCorAvaliacao(jogadorId) {
        const btn = document.getElementById('btn-avaliar-' + jogadorId);
        if (!btn) return;
        const nf = calcularNotaFinalJogador(avaliacoes[jogadorId], jogadorId);
        const c = corDaNota(nf);
        btn.style.background = c.bg;
        btn.style.color = c.fg;
    }

    function atualizarTodasCoresAvaliacao() {
        Object.keys(avaliacoes).forEach(id => atualizarCorAvaliacao(id));
    }

    async function abrirAvaliacao(jogadorId, nome, posicao, event) {
        event.stopPropagation();

        jogadorAtual = jogadorId;
        estatisticasAtuais = null;

        await carregarCriterios();

        if (!avaliacoes[jogadorId]) avaliacoes[jogadorId] = { obs: '' };

        document.getElementById('av-nome').textContent = nome;
        document.getElementById('av-pos').textContent  = abrevPosicao(posicao) + ' · Avaliação da partida';
        document.getElementById('av-obs').value        = avaliacoes[jogadorId].obs || '';
        const nmAtual = avaliacoes[jogadorId].notaManual;
        document.getElementById('av-nota-manual').value = (nmAtual === null || nmAtual === undefined) ? '' : nmAtual;

        // Busca estatísticas importadas para pré-preencher (só na primeira vez que abre)
        const jaTinhaValores = criterios.some(c => (avaliacoes[jogadorId][c.id] || 0) > 0);
        if (!jaTinhaValores) {
            try {
                const jogoId = parseInt(document.getElementById('jogoId').value);
                const resp = await fetch(`/Notas/BuscarEstatisticas?jogoId=${jogoId}&jogadorId=${jogadorId}`);
                if (resp.ok) {
                    const dados = await resp.json();
                    if (dados.encontrado) {
                        estatisticasAtuais = dados;
                        criterios.forEach(c => { avaliacoes[jogadorId][c.id] = dados[c.id] || 0; });
                    }
                }
            } catch { }
        }

        const container = document.getElementById('av-acoes');
        container.innerHTML = '';

        if (estatisticasAtuais && (estatisticasAtuais.minutos || estatisticasAtuais.rating)) {
            const info = document.createElement('div');
            info.style.cssText = 'font-size:12px;color:#6b7280;padding:4px 0 10px;';
            info.textContent = [
                estatisticasAtuais.minutos != null ? `${estatisticasAtuais.minutos} min` : null,
                estatisticasAtuais.rating != null ? `nota api: ${estatisticasAtuais.rating}` : null
            ].filter(Boolean).join(' · ');
            container.appendChild(info);
        }

        criterios.forEach(a => {
            if (!avaliacoes[jogadorId][a.id]) avaliacoes[jogadorId][a.id] = 0;

            const row = document.createElement('div');
            row.style.cssText = 'display:flex;align-items:center;gap:8px;padding:7px 0;border-bottom:1px solid #262626;color:#e5e7eb;';
            row.innerHTML = `
                <div style="flex:1;font-size:13px;">
                    <div>${a.label}</div>
                    <div style="font-size:11px;color:#6b7280;">${a.peso > 0 ? '+' + a.peso : a.peso} ponto(s)</div>
                </div>
                <button type="button" onclick="ajustarAcao(${jogadorId},'${a.id}',-1)"
                    style="width:28px;height:28px;border-radius:50%;border:1px solid #2a2a2a;background:#1a1a1a;color:#e5e7eb;font-size:16px;cursor:pointer;">&#8722;</button>
                <span id="av-cnt-${a.id}" style="min-width:28px;text-align:center;font-size:14px;font-weight:600;color:#f1f5f9;">${avaliacoes[jogadorId][a.id]}</span>
                <button type="button" onclick="ajustarAcao(${jogadorId},'${a.id}',+1)"
                    style="width:28px;height:28px;border-radius:50%;border:1px solid #2a2a2a;background:#1a1a1a;color:#e5e7eb;font-size:16px;cursor:pointer;">+</button>`;
            container.appendChild(row);
        });

        recalcularTotal();
        document.getElementById('modal-avaliacao').style.display = 'flex';
    }

    function ajustarAcao(jogadorId, acaoId, delta) {
        if (delta < 0 && avaliacoes[jogadorId][acaoId] <= 0) return;
        avaliacoes[jogadorId][acaoId] += delta;
        document.getElementById('av-cnt-' + acaoId).textContent = avaliacoes[jogadorId][acaoId];
        recalcularTotal();
    }

    function recalcularTotal() {
        let total = 0;
        criterios.forEach(a => {
            total += (avaliacoes[jogadorAtual][a.id] || 0) * a.peso;
        });
        total = Math.round(total * 100) / 100;

        const el   = document.getElementById('av-total');
        const chip = document.getElementById('av-nota');
        el.textContent = total;
        el.style.color = total > 0 ? '#4ade80' : total < 0 ? '#f87171' : '#9ca3af';

        if      (total >= 5) { chip.textContent = 'Excelente'; chip.style.cssText = 'padding:3px 10px;border-radius:20px;font-size:12px;font-weight:500;background:rgba(34,197,94,0.15);color:#4ade80;'; }
        else if (total >= 2) { chip.textContent = 'Bom';       chip.style.cssText = 'padding:3px 10px;border-radius:20px;font-size:12px;font-weight:500;background:rgba(34,197,94,0.15);color:#4ade80;'; }
        else if (total >= 0) { chip.textContent = 'Regular';   chip.style.cssText = 'padding:3px 10px;border-radius:20px;font-size:12px;font-weight:500;background:rgba(245,158,11,0.15);color:#fbbf24;'; }
        else                 { chip.textContent = 'Ruim';      chip.style.cssText = 'padding:3px 10px;border-radius:20px;font-size:12px;font-weight:500;background:rgba(239,68,68,0.15);color:#f87171;'; }
    }

    async function salvarAvaliacao() {
        if (!jogadorAtual) return;

        avaliacoes[jogadorAtual].obs = document.getElementById('av-obs').value;

        const notaManualRaw = document.getElementById('av-nota-manual').value.trim();
        let notaManual = null;
        if (notaManualRaw !== '') {
            const parsed = parseFloat(notaManualRaw.replace(',', '.'));
            if (!isNaN(parsed)) notaManual = Math.max(0, Math.min(10, parsed));
        }
        avaliacoes[jogadorAtual].notaManual = notaManual;

        const jogoId = parseInt(document.getElementById('jogoId').value);

        const detalhes = criterios
            .filter(a => (avaliacoes[jogadorAtual][a.id] || 0) > 0)
            .map(a => ({
                acaoId:     a.id,
                acaoLabel:  a.label,
                quantidade: avaliacoes[jogadorAtual][a.id],
                peso:       a.peso
            }));

        const total = Math.round(criterios.reduce((sum, a) =>
            sum + (avaliacoes[jogadorAtual][a.id] || 0) * a.peso, 0) * 100) / 100;

        const body = {
            jogadorId:  jogadorAtual,
            jogoId:     jogoId,
            total:      total,
            observacao: avaliacoes[jogadorAtual].obs,
            notaManual: notaManual,
            detalhes:   detalhes
        };

        try {
            const resp = await fetch('/Notas/Salvar', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json' },
                body:    JSON.stringify(body)
            });

            if (resp.ok) {
                avaliacoes[jogadorAtual].total = total;
                atualizarCorAvaliacao(jogadorAtual);
                fecharAvaliacao();
            } else {
                alert('Erro ao salvar avaliação.');
            }
        } catch (e) {
            alert('Erro de conexão ao salvar.');
        }
    }

    function fecharAvaliacao() {
        document.getElementById('modal-avaliacao').style.display = 'none';
        jogadorAtual = null;
        estatisticasAtuais = null;
    }

    // Carrega avaliações já salvas ao abrir a página
    (async function carregarAvaliacoesSalvas() {
        const jogoId = document.getElementById('jogoId').value;
        // Antes das notas: os pisos de participação curta e de goleiro decisivo
        // dependem do contexto, senão a primeira pintura sai com a cor errada.
        await carregarContextosNota();
        try {
            const resp = await fetch(`/Notas/BuscarPorJogo?jogoId=${jogoId}`);
            if (!resp.ok) return;
            const notas = await resp.json();
            notas.forEach(n => {
                if (!avaliacoes[n.jogadorId]) avaliacoes[n.jogadorId] = { obs: '' };
                avaliacoes[n.jogadorId].obs = n.observacao || '';
                avaliacoes[n.jogadorId].notaManual = (n.notaManual === null || n.notaManual === undefined) ? null : n.notaManual;
                avaliacoes[n.jogadorId].total = n.total;
                n.detalhes.forEach(d => {
                    avaliacoes[n.jogadorId][d.acaoId] = d.quantidade;
                });
            });
            atualizarTodasCoresAvaliacao();
        } catch (e) {
            console.warn('Não foi possível carregar avaliações:', e);
        }
    })();

    // ══ Dados vindos do servidor ══════════════════════════════════════════
    const jogoIdPlacar  = ANALISAR.jogoId;
    const placarCasaIni = ANALISAR.placarCasaInicial;
    const placarVisIni  = ANALISAR.placarVisitanteInicial;

    const jogadoresCasaLista = ANALISAR.jogadoresCasa;
    const jogadoresVisLista = ANALISAR.jogadoresVisitante;

    // ══ Estado local ══════════════════════════════════════════════════════
    let eventoAtualTipo  = 'gol';   // 'gol' | 'amarelo' | 'vermelho'
    let eventoAtualTime  = 'casa';  // 'casa' | 'vis'

    // ══ Carregar eventos ao abrir página ═════════════════════════════════
    (async function carregarEventos() {
        try {
            const resp = await fetch(`/Jogos/BuscarEventos?jogoId=${jogoIdPlacar}`);
            if (!resp.ok) return;
            const data = await resp.json();

            // Renderiza gols
            (data.gols || []).forEach(g => {
                adicionarEventoNaTimeline({
                    id: g.id, tipo: 'gol',
                    time: g.timeCasaId ? 'casa' : 'vis',
                    minuto: g.minuto, acrescimo: 0,
                    nomeJogador: g.nomeJogador,
                    nomeAssistencia: g.nomeAssistencia || null,
                    isContra: g.contra
                });
            });

            // Renderiza cartões
            (data.cartoes || []).forEach(c => {
                adicionarEventoNaTimeline({
                    id: c.id, tipo: c.tipo.toLowerCase() === 'vermelho' ? 'vermelho' : 'amarelo',
                    time: c.timeCasaId ? 'casa' : 'vis',
                    minuto: c.minuto, acrescimo: 0,
                    nomeJogador: c.nomeJogador
                });
            });

            // Renderiza substituições
            (data.substituicoes || []).forEach(s => {
                adicionarEventoNaTimeline({
                    id: 'sub_' + s.id, tipo: 'substituicao',
                    time: s.timeCasaId ? 'casa' : 'vis',
                    minuto: s.minuto, acrescimo: 0,
                    nomeJogador: s.nomeEntrou,
                    nomeSaiu: s.nomeSaiu || null
                });
            });

            // Renderiza pênaltis perdidos
            (data.penaltisPerdidos || []).forEach(p => {
                adicionarEventoNaTimeline({
                    id: 'pp_' + p.id, tipo: 'penaltiPerdido',
                    time: p.timeCasaId ? 'casa' : 'vis',
                    minuto: p.minuto, acrescimo: 0,
                    nomeJogador: p.nomeJogador
                });
            });

            // Renderiza a disputa de pênaltis (cobranças convertidas e perdidas)
            renderizarDisputaPenaltis(data.penaltisDisputa || [], data.penaltisCasa, data.penaltisVis);
        } catch (e) {
            console.warn('Não foi possível carregar eventos:', e);
        }
    })();

    // ══ Disputa de pênaltis ═══════════════════════════════════════════════
    function renderizarDisputaPenaltis(lista, placarCasa, placarVis) {
        const box = document.getElementById('mbPenaltis');
        if (!box || !lista.length) return;

        // i = índice dentro da coluna do time → numera 1..n por time (igual ao placar oficial),
        // em vez da ordem global alternada entre os dois times.
        const linha = (p, i) => `
            <div class="mb-pen-kick ${p.convertido ? 'ok' : 'fail'}">
                <span class="mb-pen-ord">${i + 1}</span>
                <span class="mb-pen-ball">${p.convertido ? '●' : '✖'}</span>
                <span class="mb-pen-name">${p.nomeJogador || '?'}</span>
            </div>`;

        const casa = lista.filter(p => p.isCasa);
        const vis  = lista.filter(p => !p.isCasa);
        const placar = (placarCasa != null && placarVis != null) ? ` (${placarCasa}-${placarVis})` : '';

        box.innerHTML = `
            <div class="mb-pen-head">PÊNALTIS${placar}</div>
            <div class="mb-pen-cols">
                <div class="mb-pen-col casa">${casa.map(linha).join('')}</div>
                <div class="mb-pen-col vis">${vis.map(linha).join('')}</div>
            </div>`;
        box.style.display = '';
    }

    // ══ Modal de Evento ═══════════════════════════════════════════════════
    function abrirModalEvento(tipo, time) {
        eventoAtualTipo = tipo;
        eventoAtualTime = time;

        const isGol = tipo === 'gol';
        const nomeTime = time === 'casa'
            ? ANALISAR.timeCasaNome
            : ANALISAR.timeVisitanteNome;

        const icones = { gol: '⚽', amarelo: '🟨', vermelho: '🟥' };
        const cores  = { gol: '#4ade80', amarelo: '#fde047', vermelho: '#f87171' };

        document.getElementById('mev-titulo').textContent =
            `Registrar ${isGol ? 'Gol' : 'Cartão'} — ${nomeTime}`;

        document.getElementById('mev-tipo-badge-wrap').innerHTML = `
            <div class="mev-tipo-badge" style="background:${cores[tipo]}22; color:${cores[tipo]}; border:1px solid ${cores[tipo]}44;">
                ${icones[tipo]} ${tipo.charAt(0).toUpperCase() + tipo.slice(1)} — ${nomeTime}
            </div>`;

        // Assistência só para gol
        document.getElementById('mev-assistencia-wrap').style.display = isGol ? '' : 'none';
        document.getElementById('mev-tipo-cartao-wrap').style.display = isGol ? 'none' : '';
        if (!isGol) {
            document.getElementById('mev-tipo-cartao').value =
                tipo === 'vermelho' ? 'Vermelho' : 'Amarelo';
        }

        // Preenche selects de jogadores
        const lista = time === 'casa' ? jogadoresCasaLista : jogadoresVisLista;
        const sel   = document.getElementById('mev-jogador');
        const selA  = document.getElementById('mev-assistencia');

        sel.innerHTML  = '<option value="">Selecione...</option>';
        selA.innerHTML = '<option value="">Nenhuma</option>';
        lista.forEach(j => {
            sel.innerHTML  += `<option value="${j.id}">${j.nome}</option>`;
            selA.innerHTML += `<option value="${j.id}">${j.nome}</option>`;
        });

        document.getElementById('mev-minuto').value    = '';
        document.getElementById('mev-acrescimo').value = '';

        document.getElementById('modal-evento').style.display = 'flex';
        document.getElementById('mev-minuto').focus();
    }

    function fecharModalEvento() {
        document.getElementById('modal-evento').style.display = 'none';
    }

    async function salvarEvento() {
        const minuto     = parseInt(document.getElementById('mev-minuto').value) || 0;
        const acrescimo  = parseInt(document.getElementById('mev-acrescimo').value) || 0;
        const jogadorId  = parseInt(document.getElementById('mev-jogador').value) || 0;
        const assistId   = parseInt(document.getElementById('mev-assistencia').value) || 0;
        const tipoCartao = document.getElementById('mev-tipo-cartao').value;

        if (!jogadorId) { alert('Selecione um jogador.'); return; }
        if (!minuto)    { alert('Informe o minuto.'); return; }

        const isGol  = eventoAtualTipo === 'gol';
        const isCasa = eventoAtualTime === 'casa';

        const body = isGol
            ? {
                jogoId: jogoIdPlacar,
                jogadorId,
                assistenciaJogadorId: assistId || null,
                minuto,
                acrescimo,
                contra: false,
                isTimeCasa: isCasa
              }
            : {
                jogoId: jogoIdPlacar,
                jogadorId,
                minuto,
                acrescimo,
                tipo: tipoCartao,
                isTimeCasa: isCasa
              };

        const url = isGol ? '/Jogos/RegistrarGol' : '/Jogos/RegistrarCartao';

        try {
            const resp = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const data = await resp.json();
            if (!resp.ok) { alert(data.erro || 'Erro ao salvar.'); return; }

            // Atualiza placar na tela (retornado pelo endpoint)
            if (data.placarCasa !== undefined) {
                document.getElementById('placarCasaBox').textContent = data.placarCasa;
                document.getElementById('placarVisBox').textContent  = data.placarVis;
            }

            // Encontra nome do jogador
            const lista = isCasa ? jogadoresCasaLista : jogadoresVisLista;
            const nomeJ = lista.find(j => j.id === jogadorId)?.nome || '?';
            const nomeA = assistId
                ? lista.find(j => j.id === assistId)?.nome || null
                : null;

            adicionarEventoNaTimeline({
                id: data.id, tipo: eventoAtualTipo,
                time: eventoAtualTime,
                minuto, acrescimo,
                nomeJogador: nomeJ,
                nomeAssistencia: nomeA,
                isContra: false
            });

            fecharModalEvento();
        } catch (e) {
            alert('Erro de conexão.');
            console.error(e);
        }
    }

    // ══ Timeline de eventos ═══════════════════════════════════════════════
    function adicionarEventoNaTimeline(ev) {
        const tl = ev.tipo === 'gol'
            ? document.getElementById('mbTimelineGols')
            : document.getElementById('mbTimelineOutros');
        const icones = { gol: '⚽', amarelo: '🟨', vermelho: '🟥', substituicao: '🔄', penaltiPerdido: '🚫' };
        const icon   = ev.isContra ? '🔴⚽' : icones[ev.tipo] || '📌';
        const minStr = ev.acrescimo > 0 ? `${ev.minuto}+${ev.acrescimo}'` : `${ev.minuto}'`;
        const assistStr = ev.nomeAssistencia
            ? `<small>Assist.: ${ev.nomeAssistencia}</small>` : '';
        const subStr = ev.tipo === 'substituicao' && ev.nomeSaiu
            ? `<small>↓ ${ev.nomeSaiu}</small>` : '';
        const ppStr = ev.tipo === 'penaltiPerdido'
            ? `<small>Pênalti perdido</small>` : '';

        const isSub = ev.tipo === 'substituicao';
        // Pênalti perdido vem da importação e não tem endpoint de remoção manual.
        const semDel = isSub || ev.tipo === 'penaltiPerdido';
        const delBtn = semDel ? '' : `<button class="mb-event-del"
                    onclick="removerEvento(this, '${ev.tipo}', ${ev.id || 0})"
                    title="Remover">×</button>`;

        const div = document.createElement('div');
        div.className = `mb-event ${ev.time}${isSub ? ' mb-event-sub' : ''}`;
        div.dataset.eventoId   = ev.id   || '';
        div.dataset.eventoTipo = ev.tipo || '';
        div.innerHTML = `
            <span class="mb-event-min">${minStr}</span>
            <span class="mb-event-icon">${icon}</span>
            <div class="mb-event-text">
                <strong>${isSub ? '↑ ' : ''}${ev.nomeJogador || '?'}</strong>
                ${assistStr}${subStr}${ppStr}
            </div>
            ${delBtn}`;

        // Insere ordenado por minuto
        const todos = Array.from(tl.children);
        const minAtual = ev.minuto * 100 + (ev.acrescimo || 0);
        let inserido = false;
        for (const el of todos) {
            const m = parseInt(el.querySelector('.mb-event-min')?.textContent) || 0;
            if (minAtual < m * 100) {
                tl.insertBefore(div, el);
                inserido = true;
                break;
            }
        }
        if (!inserido) tl.appendChild(div);
    }

    async function removerEvento(btn, tipo, id) {
        if (!id || !confirm('Remover este evento?')) return;

        const url = tipo === 'gol' ? `/Jogos/RemoverGol?id=${id}` : `/Jogos/RemoverCartao?id=${id}`;
        try {
            const resp = await fetch(url, { method: 'DELETE' });
            const data = await resp.json();
            if (!resp.ok) { alert(data.erro || 'Erro ao remover.'); return; }

            // Atualiza placar
            if (data.placarCasa !== undefined) {
                document.getElementById('placarCasaBox').textContent = data.placarCasa;
                document.getElementById('placarVisBox').textContent  = data.placarVis;
            }

            btn.closest('.mb-event').remove();
        } catch (e) {
            alert('Erro de conexão.');
        }
    }

    // ══ Editar placar manual ══════════════════════════════════════════════
    function editarPlacar() {
        const atual_c = document.getElementById('placarCasaBox').textContent.trim();
        const atual_v = document.getElementById('placarVisBox').textContent.trim();
        document.getElementById('edit-placar-casa').value = isNaN(parseInt(atual_c)) ? 0 : parseInt(atual_c);
        document.getElementById('edit-placar-vis').value  = isNaN(parseInt(atual_v)) ? 0 : parseInt(atual_v);
        document.getElementById('modal-placar').style.display = 'flex';
    }

    async function salvarPlacar() {
        const casa = parseInt(document.getElementById('edit-placar-casa').value);
        const vis  = parseInt(document.getElementById('edit-placar-vis').value);
        if (isNaN(casa) || isNaN(vis)) { alert('Informe valores válidos.'); return; }

        try {
            const resp = await fetch('/Jogos/AtualizarPlacar', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jogoId: jogoIdPlacar, placarCasa: casa, placarVis: vis })
            });
            const data = await resp.json();
            if (!resp.ok) { alert(data.erro || 'Erro.'); return; }

            document.getElementById('placarCasaBox').textContent = data.placarCasa;
            document.getElementById('placarVisBox').textContent  = data.placarVis;
            document.getElementById('modal-placar').style.display = 'none';
        } catch (e) {
            alert('Erro de conexão.');
        }
    }

    // Fecha modais com ESC
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            fecharModalEvento();
            document.getElementById('modal-placar').style.display = 'none';
        }
    });

    async function toggleAnalisado(checkbox) {
    const analisado = checkbox.checked ? 1 : 0;
    const jogoId    = parseInt(document.getElementById('jogoId').value);

    try {
        const resp = await fetch('/Jogos/MarcarAnalisado', {
            method:  'POST',
            headers: { 'Content-Type': 'application/json' },
            body:    JSON.stringify({ jogoId, analisado })
        });

        if (!resp.ok) {
            checkbox.checked = !checkbox.checked; // reverte se falhou
            alert('Erro ao salvar status.');
            return;
        }

        // Atualiza visual
        const icon  = document.getElementById('statusIcon');
        const label = document.getElementById('statusLabel');

        if (analisado === 1) {
            icon.style.background    = 'rgba(34,197,94,0.12)';
            icon.style.borderColor   = '#22c55e';
            icon.textContent         = '✅';
            label.style.color        = '#4ade80';
            label.textContent        = 'Jogo analisado';
        } else {
            icon.style.background    = 'rgba(255,255,255,0.06)';
            icon.style.borderColor   = '#333';
            icon.textContent         = '⏳';
            label.style.color        = '#9ca3af';
            label.textContent        = 'Aguardando análise';
        }
    } catch (e) {
        checkbox.checked = !checkbox.checked;
        alert('Erro de conexão.');
    }
    }

    async function abrirH2H(jogoId) {
        var modal = document.getElementById('modal-h2h');
        var corpo = document.getElementById('h2h-corpo');
        modal.style.display = 'flex';
        corpo.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Carregando...</div>';

        try {
            var resp = await fetch('/Jogos/UltimosConfrontos/' + jogoId);
            if (!resp.ok) {
                var msg = await resp.text();
                corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">' + msg + '</div>';
                return;
            }
            var dados = await resp.json();
            if (!dados.length) {
                corpo.innerHTML = '<div style="color:#94a3b8;text-align:center;padding:1rem;">Nenhum confronto encontrado.</div>';
                return;
            }

            corpo.innerHTML = dados.map(function(c) {
                var corCasa = c.vencedor === 'home' ? 'h2h-venceu' : c.vencedor === 'draw' ? 'h2h-empate' : 'h2h-perdeu';
                var corVis  = c.vencedor === 'away' ? 'h2h-venceu' : c.vencedor === 'draw' ? 'h2h-empate' : 'h2h-perdeu';
                var logoMandante = c.logoMandante ? '<img src="' + c.logoMandante + '" alt="">' : '';
                var logoVisitante = c.logoVisitante ? '<img src="' + c.logoVisitante + '" alt="">' : '';
                return '<div class="h2h-card">' +
                    '<div class="h2h-card-header">' + c.data + ' &bull; ' + c.competicao + ' ' + c.temporada + '</div>' +
                    '<div class="h2h-placar">' +
                        '<div class="h2h-time">' + logoMandante + '<span class="h2h-time-nome ' + corCasa + '">' + c.mandante + '</span></div>' +
                        '<div class="h2h-gols">' + (c.placarMandante ?? '-') + '<span style="opacity:.4;font-size:14px;"> x </span>' + (c.placarVisitante ?? '-') + '</div>' +
                        '<div class="h2h-time direita">' + logoVisitante + '<span class="h2h-time-nome ' + corVis + '">' + c.visitante + '</span></div>' +
                    '</div>' +
                '</div>';
            }).join('');
        } catch(e) {
            corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">Erro ao carregar confrontos.</div>';
        }
    }

    function fecharH2H() {
        document.getElementById('modal-h2h').style.display = 'none';
    }

    document.getElementById('modal-h2h').addEventListener('click', function(e) {
        if (e.target === this) fecharH2H();
    });

    async function abrirPreJogo(jogoId) {
        var modal = document.getElementById('modal-prejogo');
        var corpo = document.getElementById('prejogo-corpo');
        modal.style.display = 'flex';
        corpo.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Carregando...</div>';

        try {
            var resp = await fetch('/Jogos/PreJogo/' + jogoId);
            if (!resp.ok) {
                var msg = await resp.text();
                corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">' + msg + '</div>';
                return;
            }
            var dados = await resp.json();
            corpo.innerHTML = '<div class="pj-grid">' +
                colunaPreJogo(dados.casa, 'casa') +
                colunaPreJogo(dados.visitante, 'vis') +
            '</div>';
        } catch (e) {
            corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">Erro ao carregar dados do pré-jogo.</div>';
        }
    }

    function escHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function colunaPreJogo(t, lado) {
        return PainelJogo.colunaPreJogo(t, lado);
    }

    function fecharPreJogo() {
        document.getElementById('modal-prejogo').style.display = 'none';
        if (document.getElementById('prejogo-box').classList.contains('pj-maximizado'))
            alternarMaximizarPreJogo(); // reabre no tamanho padrão
    }

    // Tela cheia do modal: o match-up com 22 jogadores fica apertado na largura
    // padrão. A classe manda no tamanho (o CSS vence o width inline das abas);
    // setas e formas se redesenham sozinhas pelo ResizeObserver do campo.
    function alternarMaximizarPreJogo() {
        var box = document.getElementById('prejogo-box');
        var btn = document.getElementById('pj-btn-maximizar');
        var maximizado = box.classList.toggle('pj-maximizado');
        if (btn) {
            btn.textContent = maximizado ? '✕⛶' : '⛶';
            btn.title = maximizado ? 'Restaurar tamanho' : 'Maximizar (tela cheia)';
        }
    }

    document.getElementById('modal-prejogo').addEventListener('click', function (e) {
        if (e.target === this) fecharPreJogo();
    });

    // ── Abas do modal Pré-jogo (Resumo | Match-up) ─────────────────────────
    let muPreJogoCarregado = false;

    let indispPreJogoCarregado = false;

    let momentoPreJogoCarregado = false;

    function mostrarAbaPreJogo(aba) {
        document.getElementById('pj-tab-resumo').classList.toggle('active', aba === 'resumo');
        document.getElementById('pj-tab-matchup').classList.toggle('active', aba === 'matchup');
        document.getElementById('pj-tab-indisp').classList.toggle('active', aba === 'indisp');
        document.getElementById('prejogo-corpo').style.display = aba === 'resumo' ? '' : 'none';
        document.getElementById('prejogo-matchup').style.display = aba === 'matchup' ? '' : 'none';
        document.getElementById('prejogo-indisp').style.display = aba === 'indisp' ? '' : 'none';
        document.getElementById('pj-tab-momento').classList.toggle('active', aba === 'momento');
        document.getElementById('prejogo-momento').style.display = aba === 'momento' ? '' : 'none';
        // O match-up precisa de largura extra: campo horizontal + dois bancos
        document.getElementById('prejogo-box').style.width = aba === 'matchup' || aba === 'momento' ? '1140px' : '780px';
        if (aba === 'momento' && !momentoPreJogoCarregado) {
            momentoPreJogoCarregado = true;
            carregarMomentoPreJogo();
        }
        if (aba === 'matchup' && !muPreJogoCarregado) {
            muPreJogoCarregado = true;
            carregarMatchUpPreJogo();
        }
        if (aba === 'indisp' && !indispPreJogoCarregado) {
            indispPreJogoCarregado = true;
            carregarIndisponiveisPreJogo();
        }
    }

    // ── Aba Indisponíveis ─────────────────────────────────────────────────
    // Só lê o que a última busca de escalação gravou (/Jogos/Indisponiveis) — abrir
    // a aba não chama a api-football.
    async function carregarIndisponiveisPreJogo() {
        var cont = document.getElementById('prejogo-indisp');
        cont.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Carregando...</div>';
        try {
            var resp = await fetch('/Jogos/Indisponiveis/' + ANALISAR.jogoId);
            if (!resp.ok) throw new Error();
            cont.innerHTML = montarIndisponiveis(await resp.json());
        } catch (e) {
            indispPreJogoCarregado = false; // permite tentar de novo ao reabrir a aba
            cont.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">Erro ao carregar os indisponíveis.</div>';
        }
    }

    function montarIndisponiveis(d) {
        var total = (d.casa.jogadores.length + d.visitante.jogadores.length);

        // Lista vazia não é "ninguém está fora": a api-football devolve vazio tanto
        // nesse caso quanto nas ligas sem essa cobertura, e afirmar elenco completo
        // com base nisso seria informar errado justamente quem escala o time.
        if (total === 0) {
            return '<div style="text-align:center;color:rgba(255,255,255,.55);padding:2rem 1rem;font-size:12.5px;line-height:1.6">' +
                'Nenhum desfalque informado para esta partida.<br>' +
                '<span style="opacity:.7">A lista vem junto com o "🔄 Reimportar dados" — e nem toda liga tem essa cobertura na fonte.</span>' +
            '</div>';
        }

        return '<div class="pj-grid">' +
                colunaIndisponiveis(d.casa, 'casa') +
                colunaIndisponiveis(d.visitante, 'vis') +
            '</div>' +
            (d.atualizadoEm ? '<div class="pj-ind-rodape">Atualizado em ' + escHtml(d.atualizadoEm) + ' · fonte: api-football</div>' : '');
    }

    function colunaIndisponiveis(t, lado) {
        var escudo = t.escudo ? '<img src="' + escHtml(t.escudo) + '" alt="">' : '';

        var itens = t.jogadores.length === 0
            ? '<div class="pj-ind-vazio">Nenhum desfalque informado.</div>'
            : t.jogadores.map(function (j) {
                var icone = j.categoria === 'suspensao' ? '🟨'
                          : j.categoria === 'lesao' ? '🩹' : '⛔';
                var foto = j.foto
                    ? '<img class="pj-ind-foto" src="' + escHtml(j.foto) + '" alt="" onerror="this.onerror=null;this.src=\x27/images/placeholder-jogador.png\x27">'
                    : '<img class="pj-ind-foto" src="/images/placeholder-jogador.png" alt="">';
                return '<div class="pj-ind-item">' +
                        foto +
                        '<span class="pj-ind-icone">' + icone + '</span>' +
                        '<div class="pj-ind-txt">' +
                            '<div class="pj-ind-nome">' + escHtml(j.nome) + '</div>' +
                            '<div class="pj-ind-motivo">' + escHtml(j.motivo) + '</div>' +
                        '</div>' +
                        '<span class="pj-ind-tag ' + (j.duvida ? 'duvida' : 'fora') + '">' + escHtml(j.situacao) + '</span>' +
                    '</div>';
            }).join('');

        return '<div class="pj-col ' + lado + '">' +
                '<div class="pj-team-head">' + escudo + '<span class="pj-team-nome">' + escHtml(t.time || '') + '</span></div>' +
                itens +
            '</div>';
    }

    // ── Aba Momento ───────────────────────────────────────────────────────
    // Como cada jogador chega ao jogo. A lista abre na hora (só banco local); os
    // resumos chegam um por vez — o servidor já serializa as buscas com intervalo,
    // e pedir tudo em paralelo só faria as requisições esperarem na fila. Titulares entram na fila sozinhos,
    // alternando os dois times; do banco, só quem for clicado (e fura a fila).
    var momentoFila = [];
    var momentoProcessando = false;
    var momentoPedidos = {};

    async function carregarMomentoPreJogo() {
        var cont = document.getElementById('prejogo-momento');
        cont.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Carregando...</div>';
        try {
            var resp = await fetch('/Jogos/MomentoJogadores/' + ANALISAR.jogoId);
            if (!resp.ok) throw new Error();
            var d = await resp.json();
            cont.innerHTML = '<div class="pj-grid">' +
                    colunaMomento(d.casa, 'casa') +
                    colunaMomento(d.visitante, 'vis') +
                '</div>' +
                '<div class="pj-ind-rodape">Atualizado a cada 6h</div>';

            var n = Math.max(d.casa.titulares.length, d.visitante.titulares.length);
            for (var i = 0; i < n; i++) {
                [d.casa.titulares[i], d.visitante.titulares[i]].forEach(function (j) {
                    if (j && j.sincronizado) enfileirarMomento(j.id, false);
                });
            }
        } catch (e) {
            momentoPreJogoCarregado = false; // permite tentar de novo ao reabrir a aba
            cont.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">Erro ao carregar os jogadores.</div>';
        }
    }

    function colunaMomento(t, lado) {
        var escudo = t.escudo ? '<img src="' + escAttr(t.escudo) + '" alt="">' : '';
        var sincronizados = t.titulares.filter(function (j) { return j.sincronizado; }).length;
        var titulares = t.titulares.length === 0
            ? '<div class="pj-ind-vazio">Nenhuma escalação registrada para este time.</div>'
            : t.titulares.map(cardMomento).join('');
        var reservas = t.reservas.length === 0 ? '' :
            '<details class="pjm-banco">' +
                '<summary><span>🪑 Banco / elenco</span><span class="pjm-contador">' + t.reservas.length + '</span></summary>' +
                '<div class="pjm-banco-dica">Toque no jogador para carregar</div>' +
                t.reservas.map(cardMomento).join('') +
            '</details>';

        return '<div class="pj-col ' + lado + '">' +
                '<div class="pj-team-head">' + escudo +
                    '<div style="min-width:0">' +
                        '<div class="pj-team-nome">' + escHtml(t.time || '') + '</div>' +
                        (t.escalacaoDe
                            ? '<div class="pjm-sub">Titulares de ' + escHtml(t.escalacaoDe) + ' · ' + sincronizados + '/' + t.titulares.length + ' sincronizados</div>'
                            : '') +
                    '</div>' +
                '</div>' +
                titulares + reservas +
            '</div>';
    }

    function cardMomento(j) {
        var foto = '<img class="pjm-foto" src="' + escAttr(j.foto || '/images/placeholder-jogador.png') + '" alt="" onerror="this.onerror=null;this.src=\x27/images/placeholder-jogador.png\x27">';
        var corpo = j.sincronizado
            ? '<div class="pjm-corpo pjm-pendente">Toque para carregar</div>'
            : '<div class="pjm-corpo pjm-pendente">' +
                    '<div class="pjm-vinc-linha">Não sincronizado' +
                        '<button type="button" class="pjm-vinc-btn" onclick="abrirVinculoMomento(' + j.id + ')">🔗 Vincular</button>' +
                    '</div>' +
                '</div>';
        return '<div class="pjm-card' + (j.sincronizado ? '' : ' nao-sincronizado') + '" id="pjm-' + j.id + '"' +
                (j.sincronizado ? ' onclick="enfileirarMomento(' + j.id + ', true)"' : '') + '>' +
                '<div class="pjm-topo">' +
                    '<div class="pjm-foto-wrap">' + foto +
                        (j.numero ? '<span class="pjm-num">' + escHtml(String(j.numero)) + '</span>' : '') +
                    '</div>' +
                    '<div class="pjm-id">' +
                        '<div class="pjm-nome">' + escHtml(j.nome) + '</div>' +
                        '<div class="pjm-meta"><span class="pjm-sigla">' + escHtml(j.sigla || '') + '</span><span class="pjm-perfil"></span></div>' +
                    '</div>' +
                    '<div class="pjm-forma"></div>' +
                '</div>' +
                corpo +
            '</div>';
    }

    function enfileirarMomento(id, prioridade) {
        if (momentoPedidos[id]) return;
        momentoPedidos[id] = true;
        var corpo = document.querySelector('#pjm-' + id + ' .pjm-corpo');
        if (corpo) corpo.innerHTML = esqueletoMomento();
        if (prioridade) momentoFila.unshift(id); else momentoFila.push(id);
        processarFilaMomento();
    }

    // ── Vínculo sem sair da aba ─────────────────────────────────────────────
    // Mesma busca da tela de vincular do jogador, num painel dentro do card. Quem
    // escolhe é o usuário (nomes se repetem); foto e clube de cada candidato servem
    // para conferir. Vinculou, o card vira sincronizado e já carrega o resumo.
    function abrirVinculoMomento(id, termo) {
        var card = document.getElementById('pjm-' + id);
        if (!card) return;
        var corpo = card.querySelector('.pjm-corpo');
        corpo.classList.remove('pjm-pendente');
        corpo.innerHTML =
            '<div class="pjm-vinc">' +
                '<form class="pjm-vinc-busca" onsubmit="event.preventDefault(); buscarVinculoMomento(' + id + ', this.termo.value)">' +
                    '<input name="termo" type="text" placeholder="Nome do jogador" value="' + escAttr(termo || '') + '">' +
                    '<button type="submit">Buscar</button>' +
                    '<button type="button" class="pjm-vinc-fechar" title="Cancelar" onclick="fecharVinculoMomento(' + id + ')">✕</button>' +
                '</form>' +
                '<div class="pjm-vinc-lista">' + esqueletoMomento() + '</div>' +
            '</div>';
        buscarVinculoMomento(id, termo || '');
    }

    function fecharVinculoMomento(id) {
        var corpo = document.querySelector('#pjm-' + id + ' .pjm-corpo');
        if (!corpo) return;
        corpo.classList.add('pjm-pendente');
        corpo.innerHTML = '<div class="pjm-vinc-linha">Não sincronizado' +
            '<button type="button" class="pjm-vinc-btn" onclick="abrirVinculoMomento(' + id + ')">🔗 Vincular</button></div>';
    }

    async function buscarVinculoMomento(id, termo) {
        var card = document.getElementById('pjm-' + id);
        var lista = card && card.querySelector('.pjm-vinc-lista');
        if (!lista) return;
        lista.innerHTML = esqueletoMomento();
        try {
            var resp = await fetch('/Jogos/MomentoCandidatos/' + id + '?termo=' + encodeURIComponent(termo || ''));
            if (!resp.ok) throw new Error();
            var d = await resp.json();
            var input = card.querySelector('.pjm-vinc-busca input');
            if (input && !input.value) input.value = d.termo || '';

            lista.innerHTML = d.candidatos.length === 0
                ? '<div class="pjm-vinc-vazio">Ninguém encontrado para “' + escHtml(d.termo) + '”. Tente o nome curto ou o apelido.</div>'
                : d.candidatos.map(function (c) {
                    return '<div class="pjm-vinc-item">' +
                            '<img src="' + escAttr(c.foto) + '" alt="" onerror="this.onerror=null;this.src=\x27/images/placeholder-jogador.png\x27">' +
                            '<div class="pjm-vinc-txt"><b>' + escHtml(c.nome) + '</b><small>' + escHtml(c.time || 'sem clube') + '</small></div>' +
                            '<button type="button" onclick="confirmarVinculoMomento(' + id + ', ' + c.id + ', this)">Vincular</button>' +
                        '</div>';
                }).join('');
        } catch (e) {
            lista.innerHTML = '<div class="pjm-erro">Erro ao buscar. Tente de novo.</div>';
        }
    }

    async function confirmarVinculoMomento(id, idExterno, btn) {
        btn.disabled = true;
        btn.textContent = '...';
        try {
            var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
            var resp = await fetch('/Jogos/MomentoVincular/' + id, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ idExterno: idExterno })
            });
            if (!resp.ok) throw new Error();

            var card = document.getElementById('pjm-' + id);
            card.classList.remove('nao-sincronizado');
            card.setAttribute('onclick', 'enfileirarMomento(' + id + ', true)');
            momentoPedidos[id] = false;
            enfileirarMomento(id, true);
        } catch (e) {
            btn.disabled = false;
            btn.textContent = 'Vincular';
            alert('Não foi possível gravar o vínculo.');
        }
    }

    function esqueletoMomento() {
        return '<div class="pjm-skel"><i style="width:92%"></i><i style="width:70%"></i><i style="width:80%"></i></div>';
    }

    async function processarFilaMomento() {
        if (momentoProcessando) return;
        momentoProcessando = true;
        while (momentoFila.length) {
            var id = momentoFila.shift();
            var card = document.getElementById('pjm-' + id);
            if (!card) continue;
            var corpo = card.querySelector('.pjm-corpo');
            corpo.classList.remove('pjm-pendente');
            corpo.innerHTML = esqueletoMomento();
            try {
                var resp = await fetch('/Jogos/MomentoJogador/' + id);
                if (!resp.ok) throw new Error();
                preencherMomento(card, await resp.json());
            } catch (e) {
                momentoPedidos[id] = false; // novo toque tenta de novo
                corpo.innerHTML = '<span class="pjm-erro">Erro ao carregar. Toque para tentar de novo.</span>';
            }
        }
        momentoProcessando = false;
    }

    // Ícone repetido até 3 (⚽⚽); acima disso, ícone com multiplicador (⚽×4).
    function iconesMomento(icone, n) {
        if (!n) return '';
        return n <= 3 ? new Array(n + 1).join(icone) : icone + '×' + n;
    }

    function classeResultado(g) {
        return !g.jogou ? 'nj' : (g.resultado || '').toLowerCase();
    }

    function logoLiga(id, titulo) {
        return id ? '<img class="pjm-liga" src="/MediaProxy/Liga/' + id + '" alt="" title="' + escAttr(titulo || '') + '" onerror="this.remove()">' : '';
    }

    function escudoTime(id, titulo) {
        return id ? '<img class="pjm-escudo" src="/MediaProxy/Escudo/' + id + '" alt="" title="' + escAttr(titulo || '') + '" onerror="this.remove()">' : '';
    }

    function preencherMomento(card, m) {
        var corpo = card.querySelector('.pjm-corpo');
        card.removeAttribute('onclick');
        corpo.classList.remove('pjm-pendente');

        if (m.erro) {
            corpo.innerHTML = '<span class="pjm-erro">' + escHtml(m.erro) + '</span>';
            return;
        }

        var jogos = m.jogos || [];

        // Cabeçalho: posição · pé · clube, e a forma dos últimos 5 à direita.
        var perfil = [];
        if (m.posicao) perfil.push(escHtml(m.posicao));
        if (m.pe) perfil.push('🦶 ' + escHtml(m.pe));
        if (m.clube) perfil.push(escudoTime(m.clubeId, m.clube) + escHtml(m.clube));
        card.querySelector('.pjm-perfil').innerHTML = perfil.join('<span class="pjm-ponto">·</span>');

        card.querySelector('.pjm-forma').innerHTML = jogos.slice(0, 5).map(function (g) {
            var dica = dataCurta(g.data) + ' ' + (g.mandante ? 'vs ' : '@ ') + (g.adversario || '') +
                (g.placar ? ' ' + g.placar : '') + ' — ' + detalheJogoMomento(g);
            return '<span class="pjm-res ' + classeResultado(g) + '" title="' + escAttr(dica) + '">' + (g.resultado || '–') + '</span>';
        }).join('');

        var html = '';

        if (m.lesao) {
            html += '<div class="pjm-alerta">' +
                    '<span class="pjm-alerta-ico">🩹</span>' +
                    '<div><b>' + escHtml(m.lesao.lesao) + '</b>' +
                        (m.lesao.retorno ? '<small>' + escHtml(m.lesao.retorno) + '</small>' : '') +
                    '</div>' +
                '</div>';
        }

        // Últimos 5 jogos em que entrou em campo, em blocos.
        var jogados = jogos.filter(function (g) { return g.jogou; }).slice(0, 5);
        if (jogados.length) {
            var soma = function (campo) { return jogados.reduce(function (a, g) { return a + (g[campo] || 0); }, 0); };
            var cartoes = soma('amarelos') + soma('vermelhos');
            var tiles = [
                ['⚽', soma('gols'), soma('gols') === 1 ? 'gol' : 'gols', 'gol'],
                ['🅰️', soma('assistencias'), soma('assistencias') === 1 ? 'assist.' : 'assists.', 'ast'],
                ['⏱️', Math.round(soma('minutos') / jogados.length) + "'", 'min/jogo', ''],
            ];
            if (cartoes) tiles.push(['🟨', cartoes, cartoes === 1 ? 'cartão' : 'cartões', 'cartao']);
            html += '<div class="pjm-bloco-titulo">Últimos ' + jogados.length + ' jogos</div>' +
                '<div class="pjm-tiles">' + tiles.map(function (t) {
                    return '<div class="pjm-tile ' + t[3] + (t[1] === 0 ? ' zero' : '') + '">' +
                        '<span class="pjm-tile-ico">' + t[0] + '</span>' +
                        '<b>' + t[1] + '</b><small>' + t[2] + '</small>' +
                    '</div>';
                }).join('') + '</div>';
        }

        var t = m.temporada;
        if (t) {
            var nums = [
                '<span title="Jogos (titular)">🏟️ ' + t.jogos + (t.titular !== t.jogos ? ' <small>(' + t.titular + ' tit.)</small>' : '') + '</span>',
                '<span title="Minutos">⏱️ ' + t.minutos + "'</span>",
                '<span title="Gols">⚽ ' + t.gols + '</span>',
                '<span title="Assistências">🅰️ ' + t.assistencias + '</span>',
            ];
            if (t.amarelos) nums.push('<span title="Cartões amarelos">🟨 ' + t.amarelos + '</span>');
            if (t.vermelhos) nums.push('<span title="Cartões vermelhos">🟥 ' + t.vermelhos + '</span>');
            html += '<div class="pjm-temp">' +
                    '<div class="pjm-temp-liga">' + logoLiga(t.ligaId, t.liga) +
                        '<span>' + escHtml(t.liga) + '</span>' +
                        (t.temporada ? '<small>' + escHtml(t.temporada) + '</small>' : '') +
                    '</div>' +
                    '<div class="pjm-temp-nums">' + nums.join('') + '</div>' +
                '</div>';
        }

        if (m.destaques && m.destaques.length) {
            html += '<div class="pjm-bloco-titulo">Destaques entre os ' + escHtml(m.grupoComparacao || 'da posição') + '</div>' +
                '<div class="pjm-dest">' + m.destaques.map(function (x) {
                    return '<div class="pjm-dest-item" title="Supera ' + x.percentil + '% dos ' + escAttr(m.grupoComparacao || '') + '">' +
                        '<span>' + escHtml(x.nome) + '</span>' +
                        '<div class="pjm-barra"><i style="width:' + x.percentil + '%"></i></div>' +
                        '<b>' + x.percentil + '%</b>' +
                    '</div>';
                }).join('') + '</div>';
        }

        if (jogos.length) {
            html += '<details class="pjm-jogos"><summary>📅 Últimos ' + jogos.length + ' jogos</summary>' +
                jogos.map(function (g) {
                    var eventos = iconesMomento('⚽', g.gols) + iconesMomento('🅰️', g.assistencias) +
                        iconesMomento('🟨', g.amarelos) + iconesMomento('🟥', g.vermelhos);
                    return '<div class="pjm-jogo' + (g.jogou ? '' : ' nj') + '">' +
                        '<span class="pjm-res ' + classeResultado(g) + '">' + (g.resultado || '–') + '</span>' +
                        '<span class="pjm-jogo-data">' + dataCurta(g.data) + '</span>' +
                        '<span class="pjm-jogo-comp">' + logoLiga(g.competicaoId, g.competicao) + '</span>' +
                        '<span class="pjm-jogo-adv">' +
                            '<small>' + (g.mandante ? 'vs' : '@') + '</small>' +
                            escudoTime(g.adversarioId, g.adversario) +
                            '<span class="pjm-jogo-nome">' + escHtml(g.adversario || '') + '</span>' +
                            (g.placar ? '<b>' + escHtml(g.placar) + '</b>' : '') +
                        '</span>' +
                        '<span class="pjm-jogo-ev">' + eventos + '</span>' +
                        '<span class="pjm-jogo-min">' + (g.jogou
                            ? (g.minutos != null ? g.minutos + "'" : '') + (g.reserva ? ' <small>↑</small>' : '')
                            : '<small>' + (g.reserva ? 'banco' : 'fora') + '</small>') + '</span>' +
                    '</div>';
                }).join('') +
            '</details>';
        }

        corpo.innerHTML = html || '<span class="pjm-pendente">Sem informações recentes deste jogador.</span>';
    }

    function detalheJogoMomento(g) {
        if (!g.jogou) return g.reserva ? 'não saiu do banco' : 'não jogou';
        var p = [(g.minutos != null ? g.minutos : '?') + "'"];
        if (g.reserva) p.push('entrou');
        if (g.gols) p.push(g.gols + (g.gols === 1 ? ' gol' : ' gols'));
        if (g.assistencias) p.push(g.assistencias + (g.assistencias === 1 ? ' assistência' : ' assistências'));
        if (g.amarelos) p.push('amarelo');
        if (g.vermelhos) p.push('vermelho');
        return p.join(' · ');
    }

    function escAttr(s) {
        return escHtml(s).replace(/"/g, '&quot;');
    }

    function dataCurta(iso) {
        if (!iso) return '';
        var d = new Date(iso);
        return ('0' + d.getDate()).slice(-2) + '/' + ('0' + (d.getMonth() + 1)).slice(-2);
    }

    async function carregarMatchUpPreJogo() {
        var cont = document.getElementById('prejogo-matchup');
        cont.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:2rem 0;">Carregando...</div>';
        try {
            var resp = await fetch('/Jogos/MatchUpPreJogo/' + ANALISAR.jogoId);
            if (!resp.ok) throw new Error();
            muRender(await resp.json(), cont);
        } catch (e) {
            muPreJogoCarregado = false; // permite tentar de novo ao reabrir a aba
            cont.innerHTML = '<div style="color:#f87171;text-align:center;padding:1rem;">Erro ao carregar o match-up.</div>';
        }
    }


    var posJogoDadosAtual = null;

    async function abrirPosJogo(jogoId) {
        var modal = document.getElementById('modal-posjogo');
        var corpo = document.getElementById('posjogo-corpo');
        modal.style.display = 'flex';
        corpo.innerHTML = '<div style="text-align:center;color:#94a3b8;padding:3rem 0;">Carregando...</div>';
        modal.dataset.jogoId = jogoId;

        try {
            var resp = await fetch('/Jogos/PosJogo/' + jogoId);
            if (!resp.ok) {
                var msg = await resp.text();
                corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:2rem;">' + msg + '</div>';
                return;
            }
            posJogoDadosAtual = await resp.json();
            corpo.innerHTML = montarPosJogo(posJogoDadosAtual);
            ativarTabPosJogo('notas');
            ativarMencaoJogador(document.getElementById('pgjObsTexto'), () => window._pgjJogadoresAtual || []);
        } catch (e) {
            corpo.innerHTML = '<div style="color:#f87171;text-align:center;padding:2rem;">Erro ao carregar dados do pós-jogo.</div>';
        }
    }

    // Notas, escalação, campinho e badges do pós-jogo: painel-jogo.js.
    // abrevPosicao continua exposta aqui porque a lista de elenco e o painel de
    // estatísticas desta tela também a usam.
    function abrevPosicao(pos) {
        return PainelJogo.abrevPosicao(pos);
    }

    // O HTML do pós-jogo é montado em painel-jogo.js, compartilhado com a página
    // pública /analise/{token}. Aqui é sempre o modo de edição.
    function montarPosJogo(d) {
        return PainelJogo.montarPosJogo(d, { somenteLeitura: false });
    }

    // (montarObservacoes/rotuloTipoObs vivem em painel-jogo.js)

    async function adicionarObsPosJogo() {
        var tipo = document.getElementById('pgjObsTipo').value;
        var texto = document.getElementById('pgjObsTexto').value.trim();
        if (!texto) return;
        var jogadorId = tipo === 'JOGADOR' ? parseInt(document.getElementById('pgjObsJogador').value, 10) : null;
        var jogoId = document.getElementById('modal-posjogo').dataset.jogoId;

        try {
            var resp = await fetch('/Jogos/AdicionarObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ JogoId: parseInt(jogoId, 10), Tipo: tipo, JogadorId: jogadorId, Texto: texto })
            });
            if (!resp.ok) return;
            await recarregarPosJogo(jogoId, 'obs');
        } catch (e) { /* silencioso */ }
    }

    async function editarObsPosJogo(id) {
        var row = document.querySelector('.pgj-obs-row[data-obs-id="' + id + '"]');
        var textoEl = row ? row.querySelector('.pgj-obs-texto') : null;
        var textoAtual = textoEl ? textoEl.textContent : '';

        var textoTrim = await window.MencaoJogador.editarTexto({
            titulo: 'Editar observação',
            valor: textoAtual,
            getJogadores: () => window._pgjJogadoresAtual || []
        });
        if (textoTrim === null) return;
        if (!textoTrim) { alert('O texto da observação não pode ficar vazio.'); return; }
        if (textoTrim === textoAtual) return;

        var jogoId = document.getElementById('modal-posjogo').dataset.jogoId;
        try {
            var resp = await fetch('/Jogos/EditarObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ Id: id, Texto: textoTrim })
            });
            if (!resp.ok) { alert('Erro ao editar a observação.'); return; }
            await recarregarPosJogo(jogoId, 'obs');
        } catch (e) { alert('Erro de conexão ao editar a observação.'); }
    }

    async function removerObsPosJogo(id) {
        var jogoId = document.getElementById('modal-posjogo').dataset.jogoId;
        try {
            var resp = await fetch('/Jogos/RemoverObservacaoTag', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ Id: id })
            });
            if (!resp.ok) return;
            await recarregarPosJogo(jogoId, 'obs');
        } catch (e) { /* silencioso */ }
    }

    async function recarregarPosJogo(jogoId, manterTab) {
        var resp = await fetch('/Jogos/PosJogo/' + jogoId);
        if (!resp.ok) return;
        posJogoDadosAtual = await resp.json();
        document.getElementById('posjogo-corpo').innerHTML = montarPosJogo(posJogoDadosAtual);
        ativarTabPosJogo(manterTab || 'notas');
        ativarMencaoJogador(document.getElementById('pgjObsTexto'), () => window._pgjJogadoresAtual || []);
    }

    // Delegado para painel-jogo.js. Continua global porque os botões de aba do
    // pós-jogo são gerados com onclick="ativarTabPosJogo('...')".
    function ativarTabPosJogo(tab) {
        PainelJogo.ativarTab(tab);
    }

    function fecharPosJogo() {
        document.getElementById('modal-posjogo').style.display = 'none';
    }

    document.getElementById('modal-posjogo').addEventListener('click', function (e) {
        if (e.target === this) fecharPosJogo();
    });

    // ══════════════ COMPARTILHAR ANÁLISE ══════════════
    // Link público somente-leitura desta análise (/analise/{token}), para mostrar
    // a quem não tem conta. O servidor reaproveita o link ativo do jogo — clicar
    // duas vezes no botão não gera dois tokens.

    function fecharCompartilhar() {
        document.getElementById('modal-compartilhar').style.display = 'none';
    }

    document.getElementById('modal-compartilhar').addEventListener('click', function (e) {
        if (e.target === this) fecharCompartilhar();
    });

    async function abrirCompartilhar(jogoId) {
        var modal = document.getElementById('modal-compartilhar');
        var corpo = document.getElementById('compartilhar-corpo');
        modal.style.display = 'flex';
        modal.dataset.jogoId = jogoId;
        corpo.innerHTML = '<div style="text-align:center; padding:1.5rem 0">Carregando...</div>';

        try {
            var resp = await fetch('/Jogos/LinkAnalise/' + jogoId);
            if (!resp.ok) throw new Error();
            renderCompartilhar(await resp.json());
        } catch (e) {
            corpo.innerHTML = '<div style="color:#f87171; text-align:center; padding:1rem;">Erro ao consultar o link de compartilhamento.</div>';
        }
    }

    function renderCompartilhar(dados) {
        var corpo = document.getElementById('compartilhar-corpo');
        var jogoId = document.getElementById('modal-compartilhar').dataset.jogoId;

        if (!dados || !dados.existe) {
            corpo.innerHTML =
                '<p style="margin:0 0 14px;">Gere um link público desta análise. Quem receber vê o placar, as notas, o campo, as estatísticas e as observações — <strong>sem poder alterar nada</strong> e sem acesso ao restante do sistema.</p>' +
                '<button type="button" class="mb-btn" onclick="gerarLinkCompartilhado(' + jogoId + ')">🔗 Gerar link</button>';
            return;
        }

        var visualizacoes = dados.visualizacoes || 0;
        corpo.innerHTML =
            '<p style="margin:0 0 10px;">Link ativo — qualquer pessoa com ele vê esta análise em modo leitura.</p>' +
            '<div style="display:flex; gap:8px; margin-bottom:10px;">' +
                '<input id="compartilharUrl" type="text" readonly value="' + PainelJogo.escHtml(dados.url) + '"' +
                    ' onclick="this.select()" style="flex:1; min-width:0; background:#0b1220; border:1px solid rgba(255,255,255,.15); border-radius:8px; color:var(--text-strong); padding:8px 10px; font-size:12px;">' +
                '<button type="button" class="mb-btn" onclick="copiarLinkCompartilhado()">Copiar</button>' +
            '</div>' +
            '<div style="display:flex; justify-content:space-between; align-items:center; gap:10px; flex-wrap:wrap;">' +
                '<span style="font-size:12px;">👁 ' + visualizacoes + ' visualização' + (visualizacoes === 1 ? '' : 'es') + '</span>' +
                '<button type="button" class="mb-btn" style="border-color:#ef4444; color:#ef4444;" onclick="revogarLinkCompartilhado(' + jogoId + ')">Revogar link</button>' +
            '</div>';
    }

    async function gerarLinkCompartilhado(jogoId) {
        try {
            var resp = await fetch('/Jogos/CompartilharAnalise', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ JogoId: parseInt(jogoId, 10) })
            });
            if (!resp.ok) { alert('Erro ao gerar o link.'); return; }
            var dados = await resp.json();
            renderCompartilhar({ existe: true, url: dados.url, visualizacoes: dados.visualizacoes });
        } catch (e) { alert('Erro de conexão ao gerar o link.'); }
    }

    async function revogarLinkCompartilhado(jogoId) {
        if (!confirm('Revogar o link? Quem já tem a URL deixa de conseguir abrir a análise.')) return;
        try {
            var resp = await fetch('/Jogos/RevogarAnaliseCompartilhada', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ JogoId: parseInt(jogoId, 10) })
            });
            if (!resp.ok) { alert('Erro ao revogar o link.'); return; }
            renderCompartilhar({ existe: false });
        } catch (e) { alert('Erro de conexão ao revogar o link.'); }
    }

    function copiarLinkCompartilhado() {
        var input = document.getElementById('compartilharUrl');
        if (!input) return;
        input.select();
        // navigator.clipboard só existe em contexto seguro (https/localhost);
        // execCommand cobre o resto.
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(input.value);
        } else {
            document.execCommand('copy');
        }
    }

    // ══════════════ FASES TÁTICAS ══════════════
    const CRONO_JOGO_ID = ANALISAR.jogoId;

    // ── Preserva a posição de rolagem ao trocar de aba/fase de escalação ──
    // (a troca de aba recarrega a página inteira via query string e o navegador
    // volta pro topo, fazendo o usuário perder de vista a área das escalações)
    const ESCALACAO_SCROLL_KEY = `analisarScroll_${CRONO_JOGO_ID}`;
    function salvarScrollEscalacao() {
        sessionStorage.setItem(ESCALACAO_SCROLL_KEY, String(window.scrollY));
    }
    document.querySelectorAll('#tabsEscalacao a.nav-link').forEach(a => {
        a.addEventListener('click', salvarScrollEscalacao);
    });

    // ── Fases táticas: salvar a escalação atual como uma nova fase ──
    function coletarTitularesFase() {
        const titulares = [];
        function coletar(prefixo, isCasa) {
            document.querySelectorAll(`input[id^="${prefixo}_"][id$="__JogadorId"]`).forEach(inp => {
                const base = inp.id.replace('__JogadorId', '');
                const jid = parseInt(inp.value || '0', 10);
                if (!jid) return;
                const px = parseFloat((document.getElementById(base + '__PosicaoX') || {}).value || '0');
                const py = parseFloat((document.getElementById(base + '__PosicaoY') || {}).value || '0');
                // Congela as setas de movimentação atuais do slot na fase
                const numIdx = base.split('_')[1];
                const slot = document.querySelector(`.posicao-drop[data-index='${isCasa ? 'casa' : 'vis'}_${numIdx}']`);
                const setas = (slot && setasStore[slot.dataset.escid])
                    ? setasStore[slot.dataset.escid].map(s => ({ x: s.x, y: s.y }))
                    : [];
                titulares.push({ jogadorId: jid, posicao: null, posicaoX: px, posicaoY: py, isTimeCasa: isCasa, setas });
            });
        }
        coletar('escalacaoCasa', true);
        coletar('escalacaoVisitante', false);
        return titulares;
    }

    async function salvarFaseTatica() {
        const minuto = parseInt(document.getElementById('faseMinuto').value || '0', 10) || 0;
        const nome = document.getElementById('faseNome').value.trim();
        const titulares = coletarTitularesFase();
        if (titulares.length === 0) {
            alert('Não há jogadores escalados para salvar como fase tática.');
            return;
        }
        try {
            const resp = await fetch('/Jogos/SalvarFaseTatica', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jogoId: CRONO_JOGO_ID, minuto, nome, titulares })
            });
            if (resp.ok) {
                const d = await resp.json();
                salvarScrollEscalacao();
                window.location = `/Jogos/Analisar/${CRONO_JOGO_ID}?faseEscalacao=${encodeURIComponent(d.chave)}`;
            } else {
                alert('Erro ao salvar a fase tática.');
            }
        } catch {
            alert('Erro de conexão ao salvar a fase.');
        }
    }

    async function excluirFaseTatica(chave) {
        if (!confirm('Excluir esta fase tática? As posições registradas nela serão removidas.')) return;
        try {
            const resp = await fetch('/Jogos/ExcluirFaseTatica', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ jogoId: CRONO_JOGO_ID, chave })
            });
            if (resp.ok) {
                salvarScrollEscalacao();
                window.location = `/Jogos/Analisar/${CRONO_JOGO_ID}?faseEscalacao=INICIAL`;
            } else {
                alert('Erro ao excluir a fase.');
            }
        } catch {
            alert('Erro de conexão ao excluir a fase.');
        }
    }

    // desenharMapaCalor(campoId, pontos) mora em site.js (compartilhada com
    // /Jogadores/Estatisticas).

    const MC_TIME_CASA_ID = ANALISAR.timeCasaId;
    const MC_TIME_VISITANTE_ID = ANALISAR.timeVisitanteId;
    let mcAtual = { casa: null, visitante: null }; // params atuais de cada lado, pra redesenhar em resize

    async function abrirMapaCalor() {
        document.getElementById('modal-mapacalor').style.display = 'flex';

        const selCasa = document.getElementById('mcSelectCasa');
        const selVis = document.getElementById('mcSelectVisitante');
        // Reseta pros dois defaults (mantém só "— selecione —" e "Todo o time") enquanto carrega
        selCasa.querySelectorAll('option[data-jogador]').forEach(o => o.remove());
        selVis.querySelectorAll('option[data-jogador]').forEach(o => o.remove());
        selCasa.value = '';
        selVis.value = '';
        mcOcultarLado('casa');
        mcOcultarLado('visitante');

        try {
            const resp = await fetch(`/Jogos/MapaCalorJogadores?id=${CRONO_JOGO_ID}`);
            if (!resp.ok) return;
            const dados = await resp.json();
            (dados.casa || []).forEach(j => {
                const opt = document.createElement('option');
                opt.value = 'j-' + j.jogadorId;
                opt.dataset.jogador = '1';
                opt.textContent = j.nome;
                selCasa.appendChild(opt);
            });
            (dados.visitante || []).forEach(j => {
                const opt = document.createElement('option');
                opt.value = 'j-' + j.jogadorId;
                opt.dataset.jogador = '1';
                opt.textContent = j.nome;
                selVis.appendChild(opt);
            });
        } catch {
            alert('Erro ao carregar os jogadores do mapa de calor.');
        }
    }

    function fecharMapaCalor() {
        document.getElementById('modal-mapacalor').style.display = 'none';
    }

    async function mcMostrar(lado) {
        const sel = document.getElementById(lado === 'casa' ? 'mcSelectCasa' : 'mcSelectVisitante');
        const valor = sel.value;
        if (!valor) { mcOcultarLado(lado); return; }

        const campoId = lado === 'casa' ? 'mcCampoCasa' : 'mcCampoVisitante';
        let params;
        if (valor === 'time') {
            params = `timeId=${lado === 'casa' ? MC_TIME_CASA_ID : MC_TIME_VISITANTE_ID}`;
        } else {
            params = `jogadorId=${valor.slice(2)}`;
        }

        try {
            const resp = await fetch(`/Jogos/MapaCalor?id=${CRONO_JOGO_ID}&${params}`);
            if (!resp.ok) { alert('Erro ao carregar o mapa de calor.'); return; }
            const pontos = await resp.json();
            mcAtual[lado] = params;
            desenharMapaCalor(campoId, pontos);
        } catch {
            alert('Erro de conexão ao carregar o mapa de calor.');
        }
    }

    function mcOcultarLado(lado) {
        const campoId = lado === 'casa' ? 'mcCampoCasa' : 'mcCampoVisitante';
        const canvas = document.getElementById(campoId)?.querySelector('.heatmap-overlay');
        if (canvas) canvas.style.display = 'none';
        mcAtual[lado] = null;
    }

    window.addEventListener('resize', () => {
        ['casa', 'visitante'].forEach(async lado => {
            if (!mcAtual[lado]) return;
            const campoId = lado === 'casa' ? 'mcCampoCasa' : 'mcCampoVisitante';
            try {
                const resp = await fetch(`/Jogos/MapaCalor?id=${CRONO_JOGO_ID}&${mcAtual[lado]}`);
                if (resp.ok) desenharMapaCalor(campoId, await resp.json());
            } catch { /* ignora falha de rede no redesenho por resize */ }
        });
    });

    // ── Tooltip de info do jogador ──────────────────────────────────────────
    // Dados do tooltip por jogador (id → objeto) — ver dadosJogadores no topo da
    // view. O mapa deixa o botão ℹ ser criado também em JS, para o jogador
    // arrastado ou cadastrado na hora já nascer com ele.
    const DADOS_JOGADORES = ANALISAR.dadosJogadores;

    // Médias por jogo das estatísticas importadas, por jogador (id → médias),
    // com as mesmas fórmulas de /Jogadores/Estatisticas.
    let MEDIAS_JOGADORES = ANALISAR.medias;
    // Jogos como titular por id de jogador, na competição deste jogo.
    let TITULAR_JOGADORES = ANALISAR.titularCompeticao;
    // Dados da temporada mostrada no tooltip (todas as competições do mesmo ano) —
    // linha "Temporada". TEMPORADA_TOOLTIP 0 = "Todas as temporadas" (oculta a linha).
    // Não são const porque o seletor "ℹ tooltip" troca a temporada em tempo de tela.
    let TEMPORADA_TOOLTIP = ANALISAR.temporadaTooltip;
    let GOLS_TEMPORADA = ANALISAR.golsTemporada;
    let ASSISTS_TEMPORADA = ANALISAR.assistsTemporada;
    let TITULAR_TEMPORADA = ANALISAR.titularTemporada;
    // Clube da temporada anterior por jogador (id → {nome, escudo, temporada, jogos}),
    // só para quem não jogou pelo clube atual naquela temporada — o reforço.
    let TIME_ANTERIOR = ANALISAR.timeAnterior || {};

    // Troca a temporada dos números do tooltip (gols/assists/titular por competição
    // e por temporada + médias por jogo). Recarrega só esses dicionários: a tela tem
    // escalação em edição e não pode ser recarregada por causa de um filtro.
    async function trocarTemporadaTooltip(valor) {
        const status = document.getElementById('tooltipTemporadaStatus');
        const temporada = parseInt(valor, 10) || 0;
        if (status) status.textContent = 'carregando…';
        try {
            const resp = await fetch(`/Jogos/TooltipTemporada/${ANALISAR.jogoId}?temporada=${temporada}`);
            if (!resp.ok) throw new Error(resp.status);
            const d = await resp.json();

            MEDIAS_JOGADORES = d.medias || {};
            TITULAR_JOGADORES = d.titularCompeticao || {};
            GOLS_TEMPORADA = d.golsTemporada || {};
            ASSISTS_TEMPORADA = d.assistsTemporada || {};
            TITULAR_TEMPORADA = d.titularTemporada || {};
            TIME_ANTERIOR = d.timeAnterior || {};
            TEMPORADA_TOOLTIP = d.temporada;

            // Gols/assists da competição moram dentro de DADOS_JOGADORES (é de lá
            // que o tooltip lê a linha "Competição").
            Object.keys(DADOS_JOGADORES).forEach(function (id) {
                DADOS_JOGADORES[id].gols = (d.gols || {})[id] || 0;
                DADOS_JOGADORES[id].assists = (d.assists || {})[id] || 0;
            });

            sincronizarTooltip(); // os dicionários acima foram substituídos
            if (status) status.textContent = '';
        } catch (e) {
            if (status) status.textContent = 'falhou';
        }
    }
    window.trocarTemporadaTooltip = trocarTemporadaTooltip;

    // O tooltip ℹ mora em js/jogador-tooltip.js, compartilhado com o campo do
    // Match Up. Aqui ficam só os atalhos com os nomes que os ~40 handlers inline
    // da view (onmouseenter="mostrarInfoJogador(...)") já chamam.
    function mostrarInfoJogador(btn, dados) { JogadorTooltip.mostrar(btn, dados); }
    function esconderInfoJogador() { JogadorTooltip.esconder(); }
    function criarBotaoInfo(jogadorId) { return JogadorTooltip.criarBotao(jogadorId); }

    // Números do tooltip desta tela; roda de novo quando o seletor "ℹ tooltip"
    // troca a temporada e os dicionários são substituídos.
    function sincronizarTooltip() {
        JogadorTooltip.configurar({
            dados: DADOS_JOGADORES,
            medias: MEDIAS_JOGADORES,
            titularCompeticao: TITULAR_JOGADORES,
            golsTemporada: GOLS_TEMPORADA,
            assistsTemporada: ASSISTS_TEMPORADA,
            titularTemporada: TITULAR_TEMPORADA,
            timeAnterior: TIME_ANTERIOR,
            temporada: TEMPORADA_TOOLTIP,
        });
    }
    sincronizarTooltip();

    // ── Cadastro rápido de jogador fora da API ────────────────────────────
    // A escalação só pode ser montada com quem existe no elenco. Quando a API
    // ainda não trouxe o jogador (garoto da base, reforço recém-anunciado, elenco
    // não importado), este "+" cadastra ele no time sem sair da análise. O jogador
    // nasce sem IdApi: a próxima importação reaproveita este cadastro pelo nome e
    // completa os dados, em vez de criar um duplicado.
    function toggleFormNovoJogador(lado) {
        const form = document.getElementById(lado === 'casa' ? 'formNovoJogadorCasa' : 'formNovoJogadorVis');
        if (!form) return;
        const abrir = !form.classList.contains('aberto');
        form.classList.toggle('aberto', abrir);
        if (abrir) document.getElementById(lado === 'casa' ? 'novoJogNomeCasa' : 'novoJogNomeVis').focus();
    }

    async function criarJogadorNoTime(lado) {
        const sufixo = lado === 'casa' ? 'Casa' : 'Vis';
        const nomeEl = document.getElementById('novoJogNome' + sufixo);
        const numEl = document.getElementById('novoJogNum' + sufixo);
        const posEl = document.getElementById('novoJogPos' + sufixo);

        const nome = nomeEl.value.trim();
        if (nome.length < 2) { nomeEl.focus(); return; }

        const numero = parseInt(numEl.value.trim(), 10);
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

        try {
            const resp = await fetch('/Jogos/CriarJogadorNoTime', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({
                    jogoId: ANALISAR.jogoId,
                    isTimeCasa: lado === 'casa',
                    nome: nome,
                    numero: isNaN(numero) ? null : numero,
                    posicao: posEl.value.trim().toUpperCase()
                })
            });

            if (!resp.ok) {
                const erro = await resp.json().catch(() => null);
                alert(erro?.erro ?? 'Não foi possível cadastrar o jogador.');
                return;
            }

            const j = await resp.json();
            // O jogador acabou de nascer: não veio no DADOS_JOGADORES da página.
            // Registra o que existe dele para o ℹ já funcionar (sem foto, sem stats).
            if (!DADOS_JOGADORES[j.id]) {
                DADOS_JOGADORES[j.id] = {
                    id: j.id, nome: j.nome, foto: '', posicao: j.posicao ?? '',
                    numero: parseInt(j.numero, 10) || null,
                    idade: null, altura: null, peso: null,
                    nac: '', nacFlag: '', time: '', timeEscudo: '',
                    gols: 0, assists: 0, obs: ''
                };
            }
            adicionarJogadorNaLista(lado, j);
            nomeEl.value = '';
            numEl.value = '';
            posEl.value = '';
            toggleFormNovoJogador(lado);

            if (j.jaExistia) alert(`${j.nome} já estava no elenco — use o jogador da lista.`);
        } catch (e) {
            alert('Erro de rede ao cadastrar o jogador.');
        }
    }

    // Insere a linha na lista do time, arrastável como as demais (sem recarregar a página).
    function adicionarJogadorNaLista(lado, j) {
        const lista = document.getElementById(lado === 'casa' ? 'listaCasa' : 'listaVisitante');
        if (!lista) return;

        const idLinha = `jogador-${lado}-${j.id}`;
        const jaNaLista = document.getElementById(idLinha);
        if (jaNaLista) { destacarLinhaJogador(jaNaLista); return; }

        const tr = document.createElement('tr');
        tr.className = 'jogador-row';
        tr.id = idLinha;
        tr.draggable = true;
        tr.addEventListener('dragstart', ev => dragJogador(ev, j.id, j.nome, j.numero, '', lado));

        const tdNum = document.createElement('td');
        // Mesmo wrapper do HTML da lista: o <td> não ancora o ℹ (position absolute).
        const ancora = document.createElement('div');
        ancora.style.cssText = 'position:relative;display:inline-block;';
        const circulo = document.createElement('div');
        circulo.className = 'player-circle player-circle-' + lado;
        circulo.style.cssText = 'width:28px;height:28px;font-size:11px;';
        circulo.textContent = j.numero ?? '';
        ancora.append(circulo, criarBotaoInfo(j.id));
        tdNum.appendChild(ancora);

        // Mesmo link de perfil das linhas renderizadas pelo servidor. draggable=false
        // deixa o arrasto para a linha inteira, em vez de o navegador arrastar a âncora.
        const tdNome = document.createElement('td');
        const linkNome = document.createElement('a');
        linkNome.className = 'jog-nome-link';
        linkNome.draggable = false;
        linkNome.href = `/Jogadores/Estatisticas/${j.id}`;
        linkNome.title = `Ver perfil de ${j.nome}`;
        linkNome.textContent = j.nome;
        tdNome.appendChild(linkNome);

        const tdPos = document.createElement('td');
        tdPos.textContent = j.posicao ?? '';

        tr.append(tdNum, tdNome, tdPos);
        lista.appendChild(tr);
        destacarLinhaJogador(tr);
    }

    function destacarLinhaJogador(tr) {
        tr.scrollIntoView({ block: 'nearest' });
        tr.style.transition = 'background-color .8s';
        tr.style.backgroundColor = 'rgba(212,175,55,0.25)';
        setTimeout(() => { tr.style.backgroundColor = ''; }, 1200);
    }

    function filtrarLista(listaId, termo) {
        const t = termo.toLowerCase().trim();
        document.querySelectorAll(`#${listaId} tr.jogador-row`).forEach(tr => {
            const nome = tr.querySelector('td:nth-child(2)')?.textContent.toLowerCase() ?? '';
            const numero = tr.querySelector('td:nth-child(1)')?.textContent.toLowerCase().trim() ?? '';
            tr.style.display = (!t || nome.includes(t) || numero.includes(t)) ? '' : 'none';
        });
    }

    // ── Restaura a rolagem salva ao trocar de aba/fase de escalação ──
    // (se não houver valor salvo, é um acesso normal à página e ela abre no topo)
    (function restaurarScrollEscalacao() {
        const salvo = sessionStorage.getItem(ESCALACAO_SCROLL_KEY);
        if (salvo === null) return;
        sessionStorage.removeItem(ESCALACAO_SCROLL_KEY);
        const alvo = parseInt(salvo, 10) || 0;
        // behavior: 'instant' ignora o scroll-behavior: smooth do html (sem animação)
        const aplicar = () => window.scrollTo({ top: alvo, left: 0, behavior: 'instant' });
        aplicar();
        // Neste ponto as imagens (fotos de jogadores, campo etc.) ainda não
        // carregaram e o documento é mais curto — o scrollTo acima pode ser
        // limitado (clamp) à altura atual. Reaplica quando tudo carregar e mais
        // uma vez logo depois, caso algo ainda mude o layout.
        const reaplicar = () => { aplicar(); setTimeout(aplicar, 100); };
        if (document.readyState === 'complete') {
            reaplicar();
        } else {
            window.addEventListener('load', reaplicar, { once: true });
        }
    })();

