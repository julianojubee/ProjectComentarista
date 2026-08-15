// Transforma qualquer <select data-buscavel> num combo com campo de busca —
// o mesmo "digitar pra filtrar" que a barra de filtros de Times/Treinadores já
// tinha, mas sem trocar o contrato do form: o <select> continua no DOM, com o
// mesmo name/value, e é ele que vai no GET. Só some da tela.
//
// Melhoria progressiva: sem JS (ou se algo quebrar aqui), o select nativo fica
// visível e a tela continua funcionando como antes.
//
// O valor do data-buscavel, quando presente, vira o placeholder da busca.
(function () {
    'use strict';

    // Sem acento e minúsculo — "Grêmio" tem que ser achado digitando "gremio".
    function normalizar(texto) {
        return (texto || '')
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .toLowerCase()
            .trim();
    }

    function fecharTodos(exceto) {
        document.querySelectorAll('.sb-wrap.sb-aberto').forEach(function (w) {
            if (w !== exceto) {
                w.classList.remove('sb-aberto');
                w.querySelector('.sb-trigger').setAttribute('aria-expanded', 'false');
            }
        });
    }

    function montar(select) {
        if (select.dataset.sbPronto === '1') return;
        select.dataset.sbPronto = '1';

        var wrap = document.createElement('div');
        wrap.className = 'sb-wrap';

        var trigger = document.createElement('button');
        trigger.type = 'button';
        // Herda as classes do select pra ficar idêntico ao resto da barra de filtros.
        trigger.className = 'sb-trigger ' + select.className;
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');

        var label = document.createElement('span');
        label.className = 'sb-label';
        trigger.appendChild(label);

        var panel = document.createElement('div');
        panel.className = 'sb-panel';

        var busca = document.createElement('input');
        busca.type = 'text';
        busca.className = 'sb-busca';
        busca.autocomplete = 'off';
        busca.placeholder = select.dataset.buscavel || 'Buscar…';

        var lista = document.createElement('div');
        lista.className = 'sb-opts';
        lista.setAttribute('role', 'listbox');

        panel.appendChild(busca);
        panel.appendChild(lista);
        wrap.appendChild(trigger);
        wrap.appendChild(panel);

        select.parentNode.insertBefore(wrap, select);
        // Trava a largura na que o select nativo tinha, senão o filtro "pula"
        // de tamanho a cada troca de seleção. Se o select estiver oculto na
        // montagem (aba inativa), offsetWidth é 0 — aí deixa o CSS decidir.
        if (select.offsetWidth > 0) wrap.style.minWidth = select.offsetWidth + 'px';
        select.hidden = true;

        var ativa = -1;

        function opcoesVisiveis() {
            return Array.prototype.slice.call(lista.querySelectorAll('.sb-opt'));
        }

        function pintarAtiva() {
            opcoesVisiveis().forEach(function (el, i) {
                el.classList.toggle('sb-ativa', i === ativa);
                if (i === ativa) el.scrollIntoView({ block: 'nearest' });
            });
        }

        function atualizarLabel() {
            var opt = select.options[select.selectedIndex];
            label.textContent = opt ? opt.textContent.trim() : '';
            // "Todos os times", "Todas"… são o estado sem filtro: cor de placeholder.
            trigger.classList.toggle('sb-vazio', !select.value);
        }

        function desenhar() {
            var termo = normalizar(busca.value);
            lista.innerHTML = '';
            ativa = -1;

            Array.prototype.forEach.call(select.options, function (opt) {
                var texto = opt.textContent.trim();
                if (termo && normalizar(texto).indexOf(termo) === -1) return;

                var item = document.createElement('div');
                item.className = 'sb-opt';
                item.setAttribute('role', 'option');
                item.textContent = texto;
                if (opt.value === select.value) item.classList.add('sb-selecionada');
                item.addEventListener('mousedown', function (e) {
                    e.preventDefault();   // não tirar o foco da busca antes do clique
                    escolher(opt.value);
                });
                lista.appendChild(item);
            });

            if (!lista.children.length) {
                var vazio = document.createElement('div');
                vazio.className = 'sb-vazio-msg';
                vazio.textContent = 'Nada encontrado';
                lista.appendChild(vazio);
            }
        }

        function escolher(valor) {
            select.value = valor;
            // Telas como /Jogos recarregam os times ao trocar de competição,
            // e é neste evento que elas escutam.
            select.dispatchEvent(new Event('change', { bubbles: true }));
            atualizarLabel();
            fechar();
        }

        function abrir() {
            fecharTodos(wrap);
            wrap.classList.add('sb-aberto');
            trigger.setAttribute('aria-expanded', 'true');
            busca.value = '';
            desenhar();
            busca.focus();
        }

        function fechar() {
            wrap.classList.remove('sb-aberto');
            trigger.setAttribute('aria-expanded', 'false');
        }

        trigger.addEventListener('click', function () {
            if (wrap.classList.contains('sb-aberto')) fechar(); else abrir();
        });

        busca.addEventListener('input', desenhar);

        busca.addEventListener('keydown', function (e) {
            var itens = opcoesVisiveis();
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (!itens.length) return;
                ativa = e.key === 'ArrowDown'
                    ? Math.min(ativa + 1, itens.length - 1)
                    : Math.max(ativa - 1, 0);
                pintarAtiva();
            } else if (e.key === 'Enter') {
                e.preventDefault();
                // Sem navegar com as setas, Enter pega a primeira da lista filtrada.
                var alvo = itens[ativa >= 0 ? ativa : 0];
                if (alvo) alvo.dispatchEvent(new Event('mousedown'));
            } else if (e.key === 'Escape') {
                e.preventDefault();
                fechar();
                trigger.focus();
            }
        });

        // A lista de times de /Jogos é reescrita por fetch ao trocar a competição.
        new MutationObserver(function () {
            atualizarLabel();
            if (wrap.classList.contains('sb-aberto')) desenhar();
        }).observe(select, { childList: true, subtree: true });

        atualizarLabel();
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('select[data-buscavel]').forEach(montar);
    });

    document.addEventListener('mousedown', function (e) {
        var alvo = e.target instanceof Element ? e.target : null;
        if (!alvo || !alvo.closest('.sb-wrap')) fecharTodos(null);
    });
})();
