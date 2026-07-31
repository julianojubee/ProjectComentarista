// ── Simulador de tabela (/Simulador) ─────────────────────────────────────────
// Preenchimento de placar com salvamento automático: assim que os DOIS campos de
// um jogo estão preenchidos, o palpite vai pro servidor e a classificação volta
// pronta em HTML (o servidor é a única fonte do cálculo — o JS só troca o painel).
// O token anti-CSRF entra sozinho nos fetch via site.js.
(function () {
    const raiz = document.getElementById('sim-root');
    if (!raiz) return;

    const competicaoId = raiz.dataset.competicao;
    const temporada = raiz.dataset.temporada;
    if (!competicaoId) { ligarFiltros(); return; }

    const avisoSalvo = document.getElementById('sim-salvo');
    let timerAviso = null;
    let debounce = null;

    ligarFiltros();
    ligarRodada();
    ligarPlacares();

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

    // ── Navegação de rodada pelas setas ──────────────────────────────────────
    function ligarRodada() {
        document.querySelectorAll('#sim-rodada .sim-seta').forEach(function (btn) {
            btn.addEventListener('click', function () {
                const destino = btn.dataset.ir;
                if (!destino) return;
                irParaRodada(destino);
            });
        });
    }

    function irParaRodada(rodada) {
        const painel = document.getElementById('sim-rodada');
        if (painel) painel.classList.add('carregando');

        const url = '/Simulador/Rodada?competicaoId=' + encodeURIComponent(competicaoId)
            + (temporada ? '&temporada=' + encodeURIComponent(temporada) : '')
            + '&rodada=' + encodeURIComponent(rodada);

        fetch(url, { headers: { 'X-Requested-With': 'fetch' } })
            .then(r => { if (!r.ok) throw new Error('rodada'); return r.text(); })
            .then(html => {
                painel.outerHTML = html;
                ligarRodada();
                ligarPlacares();
            })
            .catch(() => {
                if (painel) painel.classList.remove('carregando');
                mostrarAviso('Não foi possível carregar a rodada', true);
            });
    }

    // ── Placares ─────────────────────────────────────────────────────────────
    function ligarPlacares() {
        document.querySelectorAll('#sim-rodada .sim-input').forEach(function (input) {
            input.addEventListener('input', function () {
                // Limita a 2 dígitos direto no campo: com type=number o max não
                // impede a digitação, só marca o campo como inválido.
                if (input.value.length > 2) input.value = input.value.slice(0, 2);
                agendarSalvar(input.closest('.sim-jogo'));
            });
            input.addEventListener('blur', function () {
                salvar(input.closest('.sim-jogo'));
            });
        });
    }

    function agendarSalvar(jogoEl) {
        clearTimeout(debounce);
        debounce = setTimeout(() => salvar(jogoEl), 500);
    }

    function salvar(jogoEl) {
        if (!jogoEl) return;
        clearTimeout(debounce);

        const casa = jogoEl.querySelector('.sim-input[data-lado="casa"]');
        const visitante = jogoEl.querySelector('.sim-input[data-lado="visitante"]');
        if (!casa || !visitante) return;

        const vCasa = casa.value.trim();
        const vVisitante = visitante.value.trim();

        // Um lado só preenchido ainda não é um resultado — espera o outro.
        const apagar = vCasa === '' && vVisitante === '';
        if (!apagar && (vCasa === '' || vVisitante === '')) return;

        const estadoAtual = apagar ? 'vazio' : vCasa + 'x' + vVisitante;
        if (jogoEl.dataset.ultimo === estadoAtual) return; // nada mudou
        jogoEl.dataset.ultimo = estadoAtual;

        const corpo = new URLSearchParams();
        corpo.append('jogoId', jogoEl.dataset.jogo);
        if (!apagar) {
            corpo.append('placarCasa', vCasa);
            corpo.append('placarVisitante', vVisitante);
        }

        fetch('/Simulador/Salvar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: corpo.toString()
        })
            .then(r => { if (!r.ok) throw new Error('salvar'); return r.text(); })
            .then(html => {
                const tabela = document.getElementById('sim-tabela');
                if (tabela) tabela.outerHTML = html;

                jogoEl.classList.toggle('simulado', !apagar);
                atualizarTag(jogoEl, !apagar);
                atualizarContador();
                mostrarAviso(apagar ? 'Placar removido' : 'Salvo', false);
            })
            .catch(() => {
                jogoEl.dataset.ultimo = ''; // permite nova tentativa
                mostrarAviso('Falha ao salvar', true);
            });
    }

    function atualizarTag(jogoEl, simulado) {
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

    function mostrarAviso(texto, erro) {
        if (!avisoSalvo) return;
        avisoSalvo.textContent = (erro ? '⚠ ' : '✓ ') + texto;
        avisoSalvo.classList.toggle('erro', !!erro);
        avisoSalvo.classList.add('visivel');
        clearTimeout(timerAviso);
        timerAviso = setTimeout(() => avisoSalvo.classList.remove('visivel'), 2000);
    }
})();
