// Aba "Estatísticas Jogadores" (Views/Shared/_EstatisticasJogadores), em
// /Relatorios e nas telas de competição.
//
// Cards "Melhores jogadores"/"Recordes em um jogo": todas as métricas já vêm
// renderizadas (uma .rpt-metric-list por métrica); aqui só alternamos qual fica
// visível e paginamos de 5 em 5 linhas. Recebe a raiz porque nas telas de
// competição o HTML chega depois, por fetch, e precisa ser ligado de novo.
window.iniciarPickersEstatJogadores = function (raiz) {
    (raiz || document).querySelectorAll('.rpt-picker').forEach(card => {
        const sel   = card.querySelector('.rpt-metric-select');
        const prev  = card.querySelector('[data-nav="prev"]');
        const next  = card.querySelector('[data-nav="next"]');
        const label = card.querySelector('.rpt-pg-label');
        const listas = card.querySelectorAll('.rpt-metric-list');
        const TAM = 5;
        let pagina = 0;

        function render() {
            listas.forEach(l => l.style.display = 'none');
            const lista = card.querySelector('.rpt-metric-list[data-chave="' + sel.value + '"]');
            if (!lista) return;
            lista.style.display = '';

            const linhas = Array.from(lista.children);
            const ultima = Math.max(0, Math.ceil(linhas.length / TAM) - 1);
            if (pagina > ultima) pagina = ultima;

            const ini = pagina * TAM;
            linhas.forEach((li, i) => li.style.display = (i >= ini && i < ini + TAM) ? '' : 'none');

            label.textContent = linhas.length
                ? (ini + 1) + '-' + Math.min(linhas.length, ini + TAM) + ' de ' + linhas.length
                : 'Sem dados';
            prev.disabled = pagina === 0;
            next.disabled = pagina >= ultima;
        }

        sel.addEventListener('change', () => { pagina = 0; render(); });
        prev.addEventListener('click', () => { if (pagina > 0) { pagina--; render(); } });
        next.addEventListener('click', () => { pagina++; render(); });
        render();
    });
};
