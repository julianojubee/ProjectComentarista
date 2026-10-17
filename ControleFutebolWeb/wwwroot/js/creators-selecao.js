/* Seleção da competição (/creators/selecao): o creator escolhe à mão os 11.

   O campo e os slots vazios vêm prontos do servidor (posições da formação
   cadastrada); aqui só entram os jogadores, de três formas — arrastando da
   lista do elenco para a posição, tocando na posição e depois no jogador, ou
   tocando só no jogador (vai para a primeira posição livre que combina).

   A montagem fica no sessionStorage, por competição e temporada: trocar a
   formação recarrega a página (o formulário é GET) e, antes, apagava tudo.
   Nada vai para o servidor.

   Escopo GLOBAL de propósito: a view usa handlers inline (onclick/ondrop),
   como o resto do site. */

    // Slot selecionado por clique, aguardando o jogador (null = nenhum).
    let crSlotAlvo = null;

    function crEsc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function crCampo() { return document.getElementById('crCampoSelecao'); }
    function crSlots() { return Array.prototype.slice.call(document.querySelectorAll('#crCampoSelecao .cr-slot')); }

    // ── Elenco da lateral ─────────────────────────────────────────────────
    async function crCarregarElenco(timeId) {
        var lista = document.getElementById('crListaElenco');
        crGuardar('time', timeId || '');
        if (!timeId) {
            lista.innerHTML = '<p class="cr-dica">Escolha um time para ver os jogadores.</p>';
            return;
        }

        lista.innerHTML = '<p class="cr-dica">Carregando...</p>';
        try {
            var resp = await fetch('/creators/selecao/elenco?time=' + encodeURIComponent(timeId));
            if (!resp.ok) throw new Error();
            var d = await resp.json();
            if (!d.jogadores.length) {
                lista.innerHTML = '<p class="cr-dica">Nenhum jogador cadastrado para esse time.</p>';
                return;
            }
            lista.innerHTML = d.jogadores.map(crItemElencoHtml).join('');
            crMarcarNoCampo();
        } catch (e) {
            lista.innerHTML = '<p class="cr-erro">Não deu para carregar o elenco. Escolha o time de novo.</p>';
        }
    }

    function crItemElencoHtml(j) {
        return '<div class="cr-lista-item" draggable="true" role="button" tabindex="0"' +
            ' data-id="' + crEsc(j.id) + '"' +
            ' data-nome="' + crEsc(j.nome) + '"' +
            ' data-numero="' + crEsc(j.numero) + '"' +
            ' data-sigla="' + crEsc(j.sigla) + '"' +
            ' data-foto="' + crEsc(j.foto || '') + '"' +
            ' data-time="' + crEsc(j.time) + '"' +
            ' ondragstart="crArrastarJogador(event)"' +
            ' onclick="crJogadorClick(this)"' +
            ' onkeydown="if (event.key === \'Enter\' || event.key === \' \') { event.preventDefault(); crJogadorClick(this); }"' +
            ' title="' + crEsc(j.nome) + '">' +
            '<span class="cr-lista-num">' + crEsc(j.numero) + '</span>' +
            '<span class="cr-lista-nome">' + crEsc(j.nome) + '</span>' +
            '<span class="cr-lista-pos">' + crEsc(j.sigla) + '</span>' +
            '</div>';
    }

    // Quem já está no campo fica marcado na lista
    function crMarcarNoCampo() {
        var ids = {};
        crSlots().forEach(function (s) { if (s.dataset.jogador) ids[s.dataset.jogador] = true; });
        document.querySelectorAll('#crListaElenco .cr-lista-item').forEach(function (el) {
            el.classList.toggle('no-campo', !!ids[el.dataset.id]);
        });
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
        crColocar(document.getElementById(slotId), origem.dataset);
    }

    // ── Tocar na posição e depois no jogador ──────────────────────────────
    function crSlotClick(slotId) {
        var slot = document.getElementById(slotId);
        if (crSlotAlvo === slot) { crLimparAlvo(); return; } // tocar de novo cancela
        crLimparAlvo();
        crSlotAlvo = slot;
        slot.classList.add('alvo');
    }

    function crJogadorClick(el) {
        var slot = crSlotAlvo || crPosicaoLivrePara(el.dataset.sigla);
        if (!slot) return; // campo cheio e nenhuma posição escolhida
        crColocar(slot, el.dataset);
        crLimparAlvo();
    }

    // Primeira posição livre com a mesma sigla (ou que a sigla do jogador
    // contém, ex.: "MEI/CA" serve em CA); sem nenhuma, a primeira livre.
    function crPosicaoLivrePara(siglaJogador) {
        var livres = crSlots().filter(function (s) { return s.dataset.vazio === '1'; });
        var siglas = String(siglaJogador || '').split('/');
        var parecidas = siglas.reduce(function (acc, s) { return acc.concat(CR_PARECIDAS[s] || []); }, []);
        var goleiro = siglas.indexOf('GOL') >= 0;
        return livres.find(function (s) { return siglas.indexOf(s.dataset.sigla) >= 0; })
            || crPrimeiraPorPrioridade(livres, parecidas)
            || livres.find(function (s) { return (s.dataset.sigla === 'GOL') === goleiro; })
            || livres[0] || null;
    }

    // A primeira vaga na ordem de preferência das siglas (não na ordem do campo)
    function crPrimeiraPorPrioridade(livres, siglas) {
        for (var k = 0; k < siglas.length; k++) {
            var achou = livres.find(function (s) { return s.dataset.sigla === siglas[k]; });
            if (achou) return achou;
        }
        return null;
    }

    function crLimparAlvo() {
        if (crSlotAlvo) crSlotAlvo.classList.remove('alvo');
        crSlotAlvo = null;
    }

    // ── Estado dos slots ──────────────────────────────────────────────────
    // Põe o jogador na posição. Se ele já estava em outra, sai de lá: o mesmo
    // jogador não aparece duas vezes no campo.
    function crColocar(slot, d) {
        if (!slot) return;
        crSlots().forEach(function (s) {
            if (s !== slot && d.id && s.dataset.jogador === String(d.id)) crEsvaziarSlot(s);
        });
        crPreencherSlot(slot, d);
        crSalvar();
    }

    function crPreencherSlot(slot, d) {
        if (!slot) return;
        slot.dataset.vazio = '0';
        slot.dataset.jogador = d.id || '';
        slot.dataset.info = JSON.stringify({ id: d.id, nome: d.nome, numero: d.numero, sigla: d.sigla, foto: d.foto, time: d.time });
        slot.classList.remove('vazio');
        slot.title = d.nome;
        slot.setAttribute('aria-label', (slot.dataset.rotulo || '') + ', ' + d.nome);
        // Nome e time entram por textContent (ver crMiolo) — nada de escapar aqui,
        // senão um "&" no nome apareceria como "&amp;" na tela.
        crMiolo(slot,
            d.foto ? '<img src="' + crEsc(d.foto) + '" alt="" onerror="this.remove()">' : '<span>' + crEsc(d.numero || '') + '</span>',
            d.nome,
            d.time || '');
    }

    function crEsvaziarSlot(slot) {
        if (!slot) return;
        slot.dataset.vazio = '1';
        delete slot.dataset.jogador;
        delete slot.dataset.info;
        slot.classList.add('vazio');
        slot.title = slot.dataset.rotulo || '';
        slot.setAttribute('aria-label', (slot.dataset.rotulo || '') + ', livre');
        crMiolo(slot, '', slot.dataset.rotulo || '', '');
    }

    // Troca só o miolo: a sigla da posição e o botão de tirar são da formação e ficam.
    function crMiolo(slot, foto, nome, time) {
        slot.querySelector('.cr-slot-foto').innerHTML = foto;
        slot.querySelector('.cr-slot-nome').textContent = nome;
        slot.querySelector('.cr-slot-time').textContent = time;
    }

    function crLimparSelecao() {
        crLimparAlvo();
        crSlots().forEach(crEsvaziarSlot);
        crSalvar();
    }

    // ── Guardar e restaurar (sessionStorage, por competição e temporada) ──
    function crChave(nome) {
        var campo = crCampo();
        return 'crSelecao:' + (campo ? campo.dataset.chave : '') + ':' + nome;
    }

    function crGuardar(nome, valor) {
        try { sessionStorage.setItem(crChave(nome), typeof valor === 'string' ? valor : JSON.stringify(valor)); } catch (e) { /* aba privada etc. */ }
    }

    function crLer(nome) {
        try { return sessionStorage.getItem(crChave(nome)); } catch (e) { return null; }
    }

    // Grava a lista de jogadores com a sigla da posição em que estão, e
    // atualiza o contador, o botão Limpar e as marcas da lista.
    function crSalvar() {
        var slots = crSlots();
        var escolhidos = slots.filter(function (s) { return s.dataset.vazio === '0' && s.dataset.info; })
            .map(function (s) { return { sigla: s.dataset.sigla, jogador: JSON.parse(s.dataset.info) }; });
        crGuardar('jogadores', escolhidos);

        var cont = document.getElementById('crContador');
        if (cont) cont.textContent = escolhidos.length + ' de ' + slots.length + ' escolhidos';
        var limpar = document.getElementById('crLimpar');
        if (limpar) limpar.disabled = escolhidos.length === 0;
        crMarcarNoCampo();
    }

    // Posições que servem de segunda opção quando a formação nova não tem a sigla
    const CR_PARECIDAS = {
        LE: ['AE', 'ZAG'], LD: ['AD', 'ZAG'], AE: ['LE', 'ME', 'PE'], AD: ['LD', 'MD', 'PD'],
        ZAG: ['VOL', 'LE', 'LD'], VOL: ['MEI', 'ZAG'], MEI: ['MEO', 'VOL', 'ME', 'MD'],
        MEO: ['MEI', 'SA', 'CA'], ME: ['AE', 'MEI', 'PE'], MD: ['AD', 'MEI', 'PD'],
        PE: ['ME', 'AE', 'CA'], PD: ['MD', 'AD', 'CA'], SA: ['MEO', 'CA'], CA: ['SA', 'PE', 'PD', 'MEO'],
        // "ATA" é a sigla genérica do cadastro; as formações usam CA, SA, PE, PD
        ATA: ['CA', 'SA', 'PE', 'PD', 'MEO'],
    };

    // Depois de trocar a formação: cada jogador volta para uma posição de mesma
    // sigla; quem não acha a sua ocupa as que sobraram, na ordem.
    function crRestaurar() {
        var bruto = crLer('jogadores');
        var lista = [];
        try { lista = bruto ? JSON.parse(bruto) : []; } catch (e) { lista = []; }

        var livres = crSlots();
        var sobra = [];
        lista.forEach(function (item) {
            var i = livres.findIndex(function (s) { return s.dataset.sigla === item.sigla; });
            if (i >= 0) crPreencherSlot(livres.splice(i, 1)[0], item.jogador);
            else sobra.push(item);
        });
        // Sem a mesma sigla: tenta uma parecida (lateral vira ala, volante vira
        // meia...) e só então qualquer vaga, mas nunca a do goleiro para quem é de linha.
        sobra.forEach(function (item) {
            var parecidas = CR_PARECIDAS[item.sigla] || [];
            var i = livres.indexOf(crPrimeiraPorPrioridade(livres, parecidas));
            if (i < 0) i = livres.findIndex(function (s) { return (s.dataset.sigla === 'GOL') === (item.sigla === 'GOL'); });
            if (i < 0) i = livres.length ? 0 : -1;
            if (i >= 0) crPreencherSlot(livres.splice(i, 1)[0], item.jogador);
        });
        crSalvar();

        // E o time que estava aberto na lateral
        var time = crLer('time');
        var sel = document.getElementById('crTimeElenco');
        if (time && sel && sel.querySelector('option[value="' + time + '"]')) {
            sel.value = time;
            sel.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        var campo = crCampo();
        if (!campo) return;
        // Clique duplo numa posição preenchida devolve ela ao estado livre.
        campo.addEventListener('dblclick', function (ev) {
            var slot = ev.target.closest('.cr-slot');
            if (slot && slot.dataset.vazio === '0') { crEsvaziarSlot(slot); crSalvar(); }
        });
        // Esc cancela a posição escolhida
        document.addEventListener('keydown', function (ev) { if (ev.key === 'Escape') crLimparAlvo(); });
        crRestaurar();
    });
