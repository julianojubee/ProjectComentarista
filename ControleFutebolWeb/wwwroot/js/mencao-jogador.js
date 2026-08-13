// Menção de jogador com "@" em caixas de texto livre.
//
// Usado na tela /Jogos/Analisar (observações do jogo) e em /AnotacoesTime
// (anotações do clube). A lista de jogadores é passada pela página — cada item
// precisa de { id, nome }.
//
// API pública: window.MencaoJogador = { renderizarTexto, ativar, fecharDropdown }
(function () {
    'use strict';

    function escHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function removerAcentos(s) {
        return s.normalize('NFD').replace(/[̀-ͯ]/g, '');
    }

    // Envolve ocorrências de "@Nome" (nomes reconhecidos na lista de jogadores) num
    // link para o perfil do jogador, para deixar a menção clicável e visualmente clara.
    // opcoes.link = false renderiza a menção como texto destacado, sem <a>: é o
    // caso da análise compartilhada (/analise/{token}), onde o visitante não
    // pode navegar para a ficha do jogador (nem para nenhuma outra tela).
    function renderizarTexto(texto, jogadores, opcoes) {
        const comLink = !opcoes || opcoes.link !== false;
        const textoEscapado = escHtml(texto);
        const idPorNomeEscapado = new Map();
        (jogadores || []).forEach(j => {
            if (!j || !j.nome) return;
            const nomeEsc = escHtml(j.nome);
            if (!idPorNomeEscapado.has(nomeEsc)) idPorNomeEscapado.set(nomeEsc, j.id);
        });
        if (!idPorNomeEscapado.size) return textoEscapado;

        const alternativas = [...idPorNomeEscapado.keys()]
            .sort((a, b) => b.length - a.length)
            .map(n => n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
        const regex = new RegExp('@(' + alternativas.join('|') + ')', 'g');
        return textoEscapado.replace(regex, (match, nomeCapturado) => {
            const jogadorId = idPorNomeEscapado.get(nomeCapturado);
            if (!jogadorId) return match;
            if (!comLink) return '<span class="obs-mencao">@' + nomeCapturado + '</span>';
            return '<a class="obs-mencao" href="/Jogadores/Estatisticas/' + jogadorId + '" onclick="event.stopPropagation();">@' + nomeCapturado + '</a>';
        });
    }

    function fecharDropdown() {
        document.querySelectorAll('.obs-mencao-dropdown').forEach(el => el.remove());
    }

    function atualizarItemAtivo(dropdown, indice) {
        dropdown.querySelectorAll('.obs-mencao-item').forEach((el, i) => el.classList.toggle('ativo', i === indice));
    }

    function abrirDropdown(textarea, jogadores, query, onSelect) {
        fecharDropdown();
        const termo = removerAcentos(query.toLowerCase());
        const filtrados = (jogadores || [])
            .filter(j => removerAcentos((j.nome || '').toLowerCase()).includes(termo))
            .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'))
            .slice(0, 8);

        const dropdown = document.createElement('div');
        dropdown.className = 'obs-mencao-dropdown';
        const rect = textarea.getBoundingClientRect();
        dropdown.style.left = `${rect.left + window.scrollX}px`;
        dropdown.style.top = `${rect.bottom + window.scrollY + 4}px`;

        if (filtrados.length === 0) {
            dropdown.innerHTML = '<div class="obs-mencao-vazio">Nenhum jogador encontrado</div>';
        } else {
            filtrados.forEach((j, idx) => {
                const item = document.createElement('div');
                item.className = 'obs-mencao-item' + (idx === 0 ? ' ativo' : '');
                item.textContent = j.nome;
                item.addEventListener('mousedown', (e) => { e.preventDefault(); onSelect(j); });
                dropdown.appendChild(item);
            });
        }

        document.body.appendChild(dropdown);
        dropdown._itensFiltrados = filtrados;
        return dropdown;
    }

    // Liga o gatilho "@" numa textarea/input: getJogadores() é chamada a cada
    // digitação para permitir listas dinâmicas (ex.: popup pós-jogo).
    function ativar(campo, getJogadores) {
        if (!campo || campo._mencaoAtiva) return;
        campo._mencaoAtiva = true;

        let dropdownAtual = null;
        let indiceAtivo = 0;
        let estadoAtual = null;

        function fechar() {
            fecharDropdown();
            dropdownAtual = null;
            estadoAtual = null;
        }

        function detectarMencao() {
            const valor = campo.value;
            const cursor = campo.selectionStart;
            const antesCursor = valor.slice(0, cursor);
            const arroba = antesCursor.lastIndexOf('@');
            if (arroba === -1) return null;
            const trecho = antesCursor.slice(arroba + 1);
            if (/[\s@]/.test(trecho)) return null;
            return { inicio: arroba, fim: cursor, query: trecho };
        }

        function selecionar(jogador) {
            if (!estadoAtual) return;
            const valor = campo.value;
            const inserir = '@' + jogador.nome + ' ';
            campo.value = valor.slice(0, estadoAtual.inicio) + inserir + valor.slice(estadoAtual.fim);
            const novaPosicao = estadoAtual.inicio + inserir.length;
            fechar();
            campo.focus();
            campo.setSelectionRange(novaPosicao, novaPosicao);
            campo.dispatchEvent(new Event('input', { bubbles: true }));
        }

        campo.addEventListener('input', () => {
            estadoAtual = detectarMencao();
            if (!estadoAtual) { fecharDropdown(); dropdownAtual = null; return; }
            indiceAtivo = 0;
            dropdownAtual = abrirDropdown(campo, getJogadores(), estadoAtual.query, selecionar);
        });

        campo.addEventListener('keydown', (e) => {
            if (!dropdownAtual) return;
            const itens = dropdownAtual._itensFiltrados;
            if (!itens || !itens.length) return;

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                indiceAtivo = (indiceAtivo + 1) % itens.length;
                atualizarItemAtivo(dropdownAtual, indiceAtivo);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                indiceAtivo = (indiceAtivo - 1 + itens.length) % itens.length;
                atualizarItemAtivo(dropdownAtual, indiceAtivo);
            } else if (e.key === 'Enter' || e.key === 'Tab') {
                e.preventDefault();
                selecionar(itens[indiceAtivo]);
            } else if (e.key === 'Escape') {
                e.preventDefault();
                fechar();
            }
        });

        campo.addEventListener('blur', () => setTimeout(fechar, 150));
    }

    // Substitui o prompt() do navegador na edição de textos livres: caixa grande,
    // que cresce com o conteúdo, e com o mesmo gatilho de "@" da caixa de criação.
    // Resolve com o texto novo (já trimado) ou null se o usuário cancelar.
    function editarTexto(opcoes) {
        const cfg = opcoes || {};
        const getJogadores = cfg.getJogadores || (() => []);

        return new Promise(resolve => {
            const overlay = document.createElement('div');
            overlay.className = 'mencao-editor-overlay';
            overlay.innerHTML = `
                <div class="mencao-editor-box">
                    <div class="mencao-editor-head">${escHtml(cfg.titulo || 'Editar observação')}</div>
                    <textarea class="mencao-editor-input" rows="6"
                              placeholder="${escHtml(cfg.placeholder || 'Digite @ para mencionar um jogador')}"></textarea>
                    <div class="mencao-editor-hint">Digite <kbd>@</kbd> para mencionar um jogador · <kbd>Ctrl</kbd>+<kbd>Enter</kbd> salva · <kbd>Esc</kbd> cancela</div>
                    <div class="mencao-editor-acoes">
                        <button type="button" class="mencao-editor-cancelar">Cancelar</button>
                        <button type="button" class="mencao-editor-salvar">Salvar</button>
                    </div>
                </div>`;

            const textarea = overlay.querySelector('.mencao-editor-input');
            textarea.value = cfg.valor || '';

            // A caixa acompanha o tamanho do texto — a observação inteira fica visível
            // sem rolagem interna, até o limite em que o modal passa a rolar.
            function ajustarAltura() {
                textarea.style.height = 'auto';
                textarea.style.height = Math.min(textarea.scrollHeight, 420) + 'px';
            }
            textarea.addEventListener('input', ajustarAltura);

            let finalizado = false;
            function fechar(resultado) {
                if (finalizado) return;
                finalizado = true;
                fecharDropdown();
                document.removeEventListener('keydown', aoTeclar, true);
                overlay.remove();
                resolve(resultado);
            }

            function aoTeclar(e) {
                if (e.key === 'Escape') {
                    // Enquanto o dropdown de "@" está aberto, Esc só fecha o dropdown.
                    if (document.querySelector('.obs-mencao-dropdown')) return;
                    e.preventDefault();
                    fechar(null);
                } else if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                    e.preventDefault();
                    fechar(textarea.value.trim());
                }
            }

            overlay.querySelector('.mencao-editor-cancelar').addEventListener('click', () => fechar(null));
            overlay.querySelector('.mencao-editor-salvar').addEventListener('click', () => fechar(textarea.value.trim()));
            overlay.addEventListener('mousedown', e => { if (e.target === overlay) fechar(null); });
            document.addEventListener('keydown', aoTeclar, true);

            document.body.appendChild(overlay);
            ativar(textarea, getJogadores);
            ajustarAltura();
            textarea.focus();
            textarea.setSelectionRange(textarea.value.length, textarea.value.length);
        });
    }

    window.MencaoJogador = { renderizarTexto, ativar, fecharDropdown, editarTexto };
})();
