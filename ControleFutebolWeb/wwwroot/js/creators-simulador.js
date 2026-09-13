/* Simulador público (/creators/simulador).

   Mesma tela da versão logada, com uma diferença de fundo: lá cada placar é
   gravado em SimulacoesJogoUsuario; aqui não há conta, então os palpites vivem
   NESTE navegador (localStorage, uma chave por competição/temporada) e são
   reenviados inteiros a cada recálculo.

   O cálculo continua sendo do servidor — é ele que conhece os critérios de
   desempate da competição —, e o que volta é o HTML da tabela pronto. */
(function () {
    'use strict';

    const raiz = document.getElementById('sim-root');
    ligarFiltros();
    if (!raiz) return;

    const competicao = raiz.dataset.competicao;
    const temporada = raiz.dataset.temporada || '';
    const chave = 'creators-simulador:' + competicao + ':' + temporada;

    const avisoSalvo = document.getElementById('sim-salvo');
    let timerAviso = null;
    let debounce = null;

    // { jogoId: [casa, visitante] } — o que o visitante imaginou até agora.
    let palpites = ler();

    aplicarNosInputs();
    recalcular(false);
    ligarRodada();
    ligarPlacares();
    ligarLimpar();

    // ── Estado no navegador ──────────────────────────────────────────────────
    // Falha de localStorage (aba anônima, storage bloqueado) não pode derrubar a
    // tela: sem ele a simulação continua funcionando, só não sobrevive ao F5.
    function ler() {
        try { return JSON.parse(localStorage.getItem(chave) || '{}') || {}; }
        catch (e) { return {}; }
    }

    function gravar() {
        try { localStorage.setItem(chave, JSON.stringify(palpites)); }
        catch (e) { /* segue sem persistir */ }
    }

    // ── Filtros (competição / temporada) ─────────────────────────────────────
    function ligarFiltros() {
        const form = document.getElementById('sim-filtros');
        if (!form) return;
        const comp = document.getElementById('sim-select-competicao');
        const temp = document.getElementById('sim-select-temporada');
        if (comp) comp.addEventListener('change', function () {
            // Trocar de competição invalida a temporada escolhida na anterior.
            if (temp) temp.disabled = true;
            form.submit();
        });
        if (temp) temp.addEventListener('change', () => form.submit());
    }

    // ── Navegação de rodada ──────────────────────────────────────────────────
    function ligarRodada() {
        document.querySelectorAll('#sim-rodada .sim-seta').forEach(function (btn) {
            btn.addEventListener('click', function () {
                if (btn.dataset.ir) irParaRodada(btn.dataset.ir);
            });
        });
    }

    function irParaRodada(rodada) {
        const painel = document.getElementById('sim-rodada');
        if (painel) painel.classList.add('carregando');

        const url = '/creators/simulador/rodada?competicao=' + encodeURIComponent(competicao)
            + (temporada ? '&temporada=' + encodeURIComponent(temporada) : '')
            + '&rodada=' + encodeURIComponent(rodada);

        fetch(url)
            .then(r => { if (!r.ok) throw new Error('rodada'); return r.text(); })
            .then(html => {
                painel.outerHTML = html;
                // A rodada vem do servidor sem placar nenhum (ele não guarda os
                // palpites do visitante) — quem repõe os campos é este JS.
                aplicarNosInputs();
                ligarRodada();
                ligarPlacares();
            })
            .catch(() => {
                if (painel) painel.classList.remove('carregando');
                aviso('Não foi possível carregar a rodada', true);
            });
    }

    // ── Placares ─────────────────────────────────────────────────────────────
    function ligarPlacares() {
        document.querySelectorAll('#sim-rodada .sim-input').forEach(function (input) {
            input.addEventListener('input', function () {
                // Limita a 2 dígitos direto no campo: com type=number o max não
                // impede a digitação, só marca o campo como inválido.
                if (input.value.length > 2) input.value = input.value.slice(0, 2);
                clearTimeout(debounce);
                debounce = setTimeout(() => registrar(input.closest('.sim-jogo')), 500);
            });
            input.addEventListener('blur', function () {
                clearTimeout(debounce);
                registrar(input.closest('.sim-jogo'));
            });
        });
    }

    // Repõe nos campos o que já foi imaginado (na carga da página e a cada
    // troca de rodada).
    function aplicarNosInputs() {
        document.querySelectorAll('#sim-rodada .sim-jogo').forEach(function (jogoEl) {
            if (jogoEl.classList.contains('realizado')) return;
            const par = palpites[jogoEl.dataset.jogo];
            const casa = jogoEl.querySelector('.sim-input[data-lado="casa"]');
            const visitante = jogoEl.querySelector('.sim-input[data-lado="visitante"]');
            if (!casa || !visitante) return;

            casa.value = par ? par[0] : '';
            visitante.value = par ? par[1] : '';
            jogoEl.classList.toggle('simulado', !!par);
            marcarTag(jogoEl, !!par);
        });
    }

    function registrar(jogoEl) {
        if (!jogoEl) return;
        const casa = jogoEl.querySelector('.sim-input[data-lado="casa"]');
        const visitante = jogoEl.querySelector('.sim-input[data-lado="visitante"]');
        if (!casa || !visitante) return;

        const vCasa = casa.value.trim();
        const vVisitante = visitante.value.trim();

        // Um lado só preenchido ainda não é um resultado — espera o outro.
        const apagar = vCasa === '' && vVisitante === '';
        if (!apagar && (vCasa === '' || vVisitante === '')) return;

        const estado = apagar ? 'vazio' : vCasa + 'x' + vVisitante;
        if (jogoEl.dataset.ultimo === estado) return; // nada mudou
        jogoEl.dataset.ultimo = estado;

        if (apagar) delete palpites[jogoEl.dataset.jogo];
        else palpites[jogoEl.dataset.jogo] = [parseInt(vCasa, 10), parseInt(vVisitante, 10)];

        gravar();
        jogoEl.classList.toggle('simulado', !apagar);
        marcarTag(jogoEl, !apagar);
        recalcular(true, apagar ? 'Placar removido' : 'Anotado');
    }

    function ligarLimpar() {
        const btn = document.getElementById('sim-limpar');
        if (!btn) return;
        btn.addEventListener('click', function () {
            if (!Object.keys(palpites).length) { aviso('Não havia nada simulado'); return; }
            if (!confirm('Apagar todos os placares simulados desta competição?')) return;
            palpites = {};
            gravar();
            aplicarNosInputs();
            recalcular(true, 'Simulação limpa');
        });
    }

    // ── Recálculo ────────────────────────────────────────────────────────────
    // Manda TODOS os palpites: o servidor não guarda nada entre uma chamada e
    // outra, então cada recálculo parte do zero.
    function recalcular(mostrarAviso, texto) {
        const corpo = {
            competicao: parseInt(competicao, 10),
            temporada: temporada ? parseInt(temporada, 10) : null,
            palpites: Object.keys(palpites).map(function (id) {
                return { jogoId: parseInt(id, 10), casa: palpites[id][0], visitante: palpites[id][1] };
            }),
        };

        return fetch('/creators/simulador/tabela', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(corpo),
        })
            .then(r => { if (!r.ok) throw new Error('tabela'); return r.text(); })
            .then(html => {
                const tabela = document.getElementById('sim-tabela');
                if (tabela) tabela.outerHTML = html;
                atualizarContador();
                if (mostrarAviso) aviso(texto || 'Tabela atualizada');
            })
            .catch(() => { if (mostrarAviso) aviso('Falha ao recalcular a tabela', true); });
    }

    function marcarTag(jogoEl, simulado) {
        const data = jogoEl.querySelector('.sim-jogo-data');
        if (!data) return;
        let tag = data.querySelector('.sim-tag.palpite');
        if (simulado && !tag) {
            tag = document.createElement('span');
            tag.className = 'sim-tag palpite';
            tag.textContent = 'simulado';
            data.appendChild(tag);
        } else if (!simulado && tag) {
            tag.remove();
        }
    }

    function atualizarContador() {
        const tabela = document.getElementById('sim-tabela');
        const contador = document.getElementById('sim-contador');
        if (tabela && contador) contador.textContent = tabela.dataset.simulados;
    }

    function aviso(texto, erro) {
        if (!avisoSalvo) return;
        avisoSalvo.textContent = (erro ? '⚠ ' : '✓ ') + texto;
        avisoSalvo.classList.toggle('erro', !!erro);
        avisoSalvo.classList.add('visivel');
        clearTimeout(timerAviso);
        timerAviso = setTimeout(() => avisoSalvo.classList.remove('visivel'), 2000);
    }
})();
