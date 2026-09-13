/* Seleção da competição (/creators/selecao): o creator escolhe à mão os 11.

   O campo e os slots vazios vêm prontos do servidor (posições da formação
   cadastrada); aqui só entram os jogadores, de duas formas — arrastando da
   lista do elenco para a posição ou clicando na posição e depois no jogador.

   Escopo GLOBAL de propósito: a view usa handlers inline (onclick/ondrop),
   como o resto do site. NADA é salvo: o que for montado vive só nesta aba. */

    // Slot selecionado por clique, aguardando o jogador (null = nenhum).
    let crSlotAlvo = null;

    function crEsc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    async function crCarregarElenco(timeId) {
        var lista = document.getElementById('crListaElenco');
        if (!timeId) {
            lista.innerHTML = '<p class="cr-dica">Escolha um time para ver os jogadores.</p>';
            return;
        }

        lista.innerHTML = '<p class="cr-dica">Carregando…</p>';
        try {
            var resp = await fetch('/creators/selecao/elenco?time=' + encodeURIComponent(timeId));
            if (!resp.ok) throw new Error();
            var d = await resp.json();
            if (!d.jogadores.length) {
                lista.innerHTML = '<p class="cr-dica">Nenhum jogador cadastrado para esse time.</p>';
                return;
            }
            lista.innerHTML = d.jogadores.map(crItemElencoHtml).join('');
        } catch (e) {
            lista.innerHTML = '<p class="cr-erro">Não deu para carregar o elenco.</p>';
        }
    }

    function crItemElencoHtml(j) {
        return '<div class="cr-lista-item" draggable="true"' +
            ' data-nome="' + crEsc(j.nome) + '"' +
            ' data-numero="' + crEsc(j.numero) + '"' +
            ' data-sigla="' + crEsc(j.sigla) + '"' +
            ' data-foto="' + crEsc(j.foto || '') + '"' +
            ' data-time="' + crEsc(j.time) + '"' +
            ' ondragstart="crArrastarJogador(event)"' +
            ' onclick="crJogadorClick(this)"' +
            ' title="' + crEsc(j.nome) + '">' +
            '<span class="cr-lista-num">' + crEsc(j.numero) + '</span>' +
            '<span class="cr-lista-nome">' + crEsc(j.nome) + '</span>' +
            '<span class="cr-lista-pos">' + crEsc(j.sigla) + '</span>' +
            '</div>';
    }

    // ── Arrastar da lista para o campo ────────────────────────────────────
    let crArrastado = null;

    function crArrastarJogador(ev) {
        var el = ev.target.closest('.cr-lista-item');
        if (!el) return;
        crArrastado = el;
        ev.dataTransfer.setData('text/plain', 'creators'); // exigido por alguns navegadores
        ev.dataTransfer.effectAllowed = 'copy';
    }

    function crSoltarNoSlot(ev, slotId) {
        ev.preventDefault();
        var origem = crArrastado;
        crArrastado = null;
        if (!origem) return;
        crPreencherSlot(document.getElementById(slotId), origem.dataset);
    }

    // ── Clicar na posição e depois no jogador ─────────────────────────────
    function crSlotClick(slotId) {
        var slot = document.getElementById(slotId);
        if (crSlotAlvo === slot) { crLimparAlvo(); return; } // clicar de novo cancela
        crLimparAlvo();
        crSlotAlvo = slot;
        slot.classList.add('alvo');
    }

    function crJogadorClick(el) {
        if (!crSlotAlvo) return; // sem posição escolhida o clique não faz nada
        crPreencherSlot(crSlotAlvo, el.dataset);
        crLimparAlvo();
    }

    function crLimparAlvo() {
        if (crSlotAlvo) crSlotAlvo.classList.remove('alvo');
        crSlotAlvo = null;
    }

    // ── Estado dos slots ──────────────────────────────────────────────────
    function crPreencherSlot(slot, d) {
        if (!slot) return;
        slot.dataset.vazio = '0';
        slot.classList.remove('vazio');
        slot.title = d.nome;
        // Nome e time entram por textContent (ver crMiolo) — nada de escapar aqui,
        // senão um "&" no nome apareceria como "&amp;" na tela.
        crMiolo(slot,
            d.foto ? '<img src="' + crEsc(d.foto) + '" alt="">' : '<span>' + crEsc(d.numero || '?') + '</span>',
            d.nome,
            d.time || '');
    }

    function crEsvaziarSlot(slot) {
        if (!slot) return;
        slot.dataset.vazio = '1';
        slot.classList.add('vazio');
        slot.title = '';
        crMiolo(slot, '<span>—</span>', 'livre', '');
    }

    // Troca só o miolo: a sigla da posição (primeiro filho) é da formação e fica.
    function crMiolo(slot, foto, nome, time) {
        slot.querySelector('.cr-slot-foto').innerHTML = foto;
        slot.querySelector('.cr-slot-nome').textContent = nome;
        slot.querySelector('.cr-slot-time').textContent = time;
    }

    function crLimparSelecao() {
        crLimparAlvo();
        document.querySelectorAll('#crCampoSelecao .cr-slot').forEach(crEsvaziarSlot);
    }

    // Clique duplo numa posição preenchida devolve ela ao estado livre.
    document.addEventListener('DOMContentLoaded', function () {
        var campo = document.getElementById('crCampoSelecao');
        if (!campo) return;
        campo.addEventListener('dblclick', function (ev) {
            var slot = ev.target.closest('.cr-slot');
            if (slot && slot.dataset.vazio === '0') crEsvaziarSlot(slot);
        });
    });
