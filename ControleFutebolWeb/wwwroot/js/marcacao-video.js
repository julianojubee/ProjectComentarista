// Marcação por vídeo (/MarcacaoVideo/Marcar).
//
// O marcador assiste ao jogo e digita: número da camisa + tecla da ação
// ("10" + P = passe certo da camisa 10). Cada tecla vira uma marcação gravada
// no servidor; "Consolidar" soma tudo em EstatisticaJogador.
//
// Três decisões que explicam o resto do arquivo:
//
// 1. Gravação otimista. A marcação aparece na hora e o POST vai atrás. Quem
//    marca acompanha o jogo em tempo real e não pode esperar a rede a cada
//    tecla; se o POST falhar, a linha é removida e o aviso aparece.
//
// 2. O iframe do YouTube fica atrás de um escudo transparente. Um clique dentro
//    dele daria foco ao iframe, e a partir daí o teclado é dele — nenhum atalho
//    funcionaria e o motivo não seria óbvio para o usuário.
//
// 3. Segundo de vídeo e minuto de jogo são coisas diferentes. O segundo serve
//    para voltar ao lance; o minuto é o que vale para a estatística (é ele que
//    define quantos minutos cada jogadora ficou em campo) e só existe depois de
//    o usuário ancorar o início de cada tempo.
(function () {
    'use strict';

    var M = window.MARCACAO;
    if (!M) return;

    var el = function (id) { return document.getElementById(id); };

    // ── Estado ───────────────────────────────────────────────────────────

    var ladoAtivo = 'casa';
    var buffer = '';              // dígitos já digitados do número da camisa
    var bufferTimer = null;
    var selecionada = null;       // jogadora escolhida (objeto do elenco)
    var eventos = M.marcacoes.map(function (m) {
        return { id: m.id, jogadorId: m.jogadorId, acaoId: m.acaoId, segundoVideo: m.segundoVideo, minutoJogo: m.minutoJogo };
    });
    var ancoras = { t1: null, t2: null };
    var velocidades = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 2];
    var iVelocidade = 3;

    var acaoPorId = {};
    var acaoPorTecla = {};
    M.acoes.forEach(function (a) {
        acaoPorId[a.id] = a;
        acaoPorTecla['Key' + a.tecla + '|' + (a.shift ? 1 : 0)] = a;
    });

    var jogadoraPorId = {};
    ['casa', 'visitante'].forEach(function (lado) {
        M[lado].elenco.forEach(function (j) {
            j.lado = lado;
            jogadoraPorId[j.id] = j;
        });
    });

    // ── Preferências locais do jogo ──────────────────────────────────────
    // URL do vídeo e âncoras dos tempos ficam no navegador: são do ambiente de
    // quem marca (o link que ele usou, onde o vídeo dele começa), não do jogo.
    var chaveLocal = 'marcacao-video:' + M.jogoId;

    function salvarLocal() {
        try {
            localStorage.setItem(chaveLocal, JSON.stringify({
                url: el('mv-url').value || null, t1: ancoras.t1, t2: ancoras.t2
            }));
        } catch (e) { /* modo privativo: seguir sem persistir */ }
    }

    function carregarLocal() {
        try {
            var bruto = localStorage.getItem(chaveLocal);
            if (!bruto) return;
            var d = JSON.parse(bruto);
            if (d.url) el('mv-url').value = d.url;
            ancoras.t1 = typeof d.t1 === 'number' ? d.t1 : null;
            ancoras.t2 = typeof d.t2 === 'number' ? d.t2 : null;
        } catch (e) { /* json corrompido: começar limpo */ }
    }

    // ── Player (YouTube ou arquivo local, mesma interface) ───────────────

    var player = {
        tipo: null,
        yt: null,
        video: null,

        tempo: function () {
            if (this.tipo === 'youtube' && this.yt && this.yt.getCurrentTime) return this.yt.getCurrentTime() || 0;
            if (this.tipo === 'arquivo' && this.video) return this.video.currentTime || 0;
            return 0;
        },
        tocando: function () {
            if (this.tipo === 'youtube' && this.yt && this.yt.getPlayerState) return this.yt.getPlayerState() === 1;
            if (this.tipo === 'arquivo' && this.video) return !this.video.paused;
            return false;
        },
        ir: function (segundos) {
            var s = Math.max(0, segundos);
            if (this.tipo === 'youtube' && this.yt) this.yt.seekTo(s, true);
            if (this.tipo === 'arquivo' && this.video) this.video.currentTime = s;
        },
        tocar: function () {
            if (this.tipo === 'youtube' && this.yt) this.yt.playVideo();
            if (this.tipo === 'arquivo' && this.video) this.video.play();
        },
        pausar: function () {
            if (this.tipo === 'youtube' && this.yt) this.yt.pauseVideo();
            if (this.tipo === 'arquivo' && this.video) this.video.pause();
        },
        alternar: function () { this.tocando() ? this.pausar() : this.tocar(); },
        velocidade: function (v) {
            if (this.tipo === 'youtube' && this.yt) this.yt.setPlaybackRate(v);
            if (this.tipo === 'arquivo' && this.video) this.video.playbackRate = v;
        }
    };

    // Aceita as três formas de link que o YouTube usa. Devolve null para
    // qualquer outra coisa — aí o usuário é orientado a usar arquivo local.
    function idDoYoutube(url) {
        if (!url) return null;
        var m = url.match(/[?&]v=([\w-]{11})/)
             || url.match(/youtu\.be\/([\w-]{11})/)
             || url.match(/\/embed\/([\w-]{11})/)
             || url.match(/\/live\/([\w-]{11})/);
        return m ? m[1] : null;
    }

    var apiPronta = false;
    var pendenteYoutube = null;

    window.onYouTubeIframeAPIReady = function () {
        apiPronta = true;
        if (pendenteYoutube) { criarYoutube(pendenteYoutube); pendenteYoutube = null; }
    };

    function carregarApiYoutube() {
        if (document.getElementById('mv-yt-api')) return;
        var s = document.createElement('script');
        s.id = 'mv-yt-api';
        s.src = 'https://www.youtube.com/iframe_api';
        document.head.appendChild(s);
    }

    // O YT.Player SUBSTITUI o div #mv-player por um iframe. Sem devolver o div ao
    // lugar, carregar um segundo link (ou trocar para arquivo do disco) não teria
    // mais onde montar o player.
    function resetarPalco() {
        var escudo = document.getElementById('mv-escudo');
        if (escudo) escudo.remove();
        if (player.yt && player.yt.destroy) { try { player.yt.destroy(); } catch (e) { /* já morto */ } }
        player.yt = null;

        var atual = document.getElementById('mv-player');
        if (atual) atual.remove();
        var novo = document.createElement('div');
        novo.id = 'mv-player';
        var palco = el('mv-palco');
        palco.insertBefore(novo, palco.firstChild);
    }

    function criarYoutube(videoId) {
        resetarPalco();
        el('mv-vazio').hidden = true;
        el('mv-video-local').hidden = true;
        player.tipo = 'youtube';
        player.yt = new YT.Player('mv-player', {
            videoId: videoId,
            playerVars: { rel: 0, modestbranding: 1, controls: 1 },
            events: {
                onReady: function () { player.velocidade(velocidades[iVelocidade]); montarEscudo(); },
                onError: function () {
                    aviso('O YouTube recusou a reprodução embutida deste vídeo. Baixe o arquivo e use "Arquivo…".');
                }
            }
        });
    }

    // Escudo: o clique vira play/pause e o foco continua na página (ver cabeçalho).
    function montarEscudo() {
        if (document.getElementById('mv-escudo')) return;
        var d = document.createElement('div');
        d.id = 'mv-escudo';
        d.className = 'mv-escudo';
        d.title = 'Clique para dar play/pause (o teclado continua com os atalhos)';
        d.addEventListener('click', function () { player.alternar(); document.body.focus(); });
        el('mv-palco').appendChild(d);
    }

    function carregarVideo() {
        var url = el('mv-url').value.trim();
        var id = idDoYoutube(url);
        if (!id) { aviso('Link do YouTube não reconhecido. Use "Arquivo…" para um vídeo do disco.'); return; }
        salvarLocal();
        carregarApiYoutube();
        if (apiPronta) criarYoutube(id); else pendenteYoutube = id;
    }

    function carregarArquivo(file) {
        resetarPalco();
        var v = el('mv-video-local');
        v.src = URL.createObjectURL(file);
        v.hidden = false;
        el('mv-vazio').hidden = true;
        player.tipo = 'arquivo';
        player.video = v;
        player.velocidade(velocidades[iVelocidade]);
    }

    // ── Tempo de vídeo × minuto de jogo ──────────────────────────────────

    function formatarTempo(s) {
        s = Math.max(0, Math.floor(s));
        var h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), seg = s % 60;
        var mm = (h > 0 && m < 10 ? '0' : '') + m;
        return (h > 0 ? h + ':' : '') + mm + ':' + (seg < 10 ? '0' : '') + seg;
    }

    // Minuto de JOGO no instante atual do vídeo. Null enquanto o 1º tempo não
    // foi ancorado: sem âncora não dá para saber o minuto, e chutar seria pior
    // do que a consolidação assumir o padrão de 90 minutos.
    function minutoJogo() {
        if (ancoras.t1 == null) return null;
        var t = player.tempo();
        var seg = (ancoras.t2 != null && t >= ancoras.t2)
            ? 45 * 60 + (t - ancoras.t2)   // 2º tempo recomeça a contagem nos 45'
            : t - ancoras.t1;
        if (seg < 0) return null;
        return Math.min(150, Math.floor(seg / 60) + 1);
    }

    function pintarTempo() {
        el('mv-tempo-video').textContent = formatarTempo(player.tempo());
        var min = minutoJogo();
        el('mv-minuto').textContent = min == null ? 'ancore os tempos' : min + "'";
        el('mv-ancora-1').classList.toggle('mv-ancora-ok', ancoras.t1 != null);
        el('mv-ancora-2').classList.toggle('mv-ancora-ok', ancoras.t2 != null);
    }

    // ── Elenco e seleção ─────────────────────────────────────────────────

    function contagens() {
        var mapa = {};
        eventos.forEach(function (ev) { mapa[ev.jogadorId] = (mapa[ev.jogadorId] || 0) + 1; });
        return mapa;
    }

    function pintarElenco() {
        var lista = el('mv-lista');
        var qtd = contagens();
        lista.innerHTML = '';

        M[ladoAtivo].elenco.forEach(function (j) {
            var linha = document.createElement('div');
            linha.className = 'mv-jogadora' + (selecionada && selecionada.id === j.id ? ' ativa' : '');
            linha.dataset.id = j.id;

            var tag = j.titular ? 'titular' : (j.reserva ? 'banco' : (j.posicao || ''));
            linha.innerHTML =
                '<span class="mv-num">' + (j.numero != null ? j.numero : '–') + '</span>' +
                '<span class="mv-nome"></span>' +
                '<span class="mv-tag"></span>' +
                '<span class="mv-qtd">' + (qtd[j.id] || '') + '</span>';
            linha.querySelector('.mv-nome').textContent = j.nome;
            linha.querySelector('.mv-tag').textContent = tag;

            linha.addEventListener('click', function () { selecionar(j); });
            lista.appendChild(linha);
        });

        document.querySelectorAll('.mv-time').forEach(function (b) {
            b.classList.toggle('ativo', b.dataset.lado === ladoAtivo);
        });
    }

    function selecionar(j) {
        selecionada = j;
        el('mv-selecionada').textContent = j ? j.nome : 'nenhuma jogadora selecionada';
        pintarElenco();
    }

    function pintarBuffer() {
        el('mv-buffer').textContent = buffer || '—';
    }

    // Digitou um dígito: o número cresce da esquerda para a direita e a
    // jogadora é escolhida assim que bater. Não espera Enter — em jogo, a
    // confirmação extra custa o lance seguinte.
    function digitar(d) {
        buffer += d;
        pintarBuffer();

        var achou = M[ladoAtivo].elenco.filter(function (j) { return String(j.numero) === buffer; })[0];
        if (achou) selecionar(achou);

        // Ninguém no time tem número que COMECE com o que foi digitado: foi
        // engano, recomeça no dígito atual em vez de travar o buffer.
        var prefixo = M[ladoAtivo].elenco.some(function (j) {
            return j.numero != null && String(j.numero).indexOf(buffer) === 0;
        });
        if (!prefixo && !achou) {
            buffer = d;
            pintarBuffer();
            var so = M[ladoAtivo].elenco.filter(function (j) { return String(j.numero) === buffer; })[0];
            if (so) selecionar(so); else aviso('Camisa ' + d + ' não encontrada em ' + M[ladoAtivo].nome + '.');
        }

        clearTimeout(bufferTimer);
        bufferTimer = setTimeout(function () { buffer = ''; pintarBuffer(); }, 2000);
    }

    // ── Marcação ─────────────────────────────────────────────────────────

    function registrar(acao) {
        if (!selecionada) { aviso('Escolha a jogadora (digite o número da camisa) antes da ação.'); return; }

        var item = {
            id: null,
            jogadorId: selecionada.id,
            acaoId: acao.id,
            segundoVideo: Math.round(player.tempo()),
            minutoJogo: minutoJogo()
        };
        eventos.push(item);
        pintarTimeline();
        pintarElenco();
        pintarTotal();

        if (el('mv-pausar').checked) player.pausar();

        item.promessa = fetch('/MarcacaoVideo/Registrar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                jogoId: M.jogoId,
                jogadorId: item.jogadorId,
                acaoId: item.acaoId,
                segundoVideo: item.segundoVideo,
                minutoJogo: item.minutoJogo
            })
        }).then(function (r) {
            if (!r.ok) throw new Error('http ' + r.status);
            return r.json();
        }).then(function (d) {
            item.id = d.id;
            pintarTimeline();
        }).catch(function () {
            // Gravação otimista que falhou: a linha some da tela para a
            // contagem nunca ficar maior do que o que está no banco.
            var i = eventos.indexOf(item);
            if (i >= 0) eventos.splice(i, 1);
            pintarTimeline(); pintarElenco(); pintarTotal();
            aviso('Não deu para gravar a marcação. Confira a conexão.');
        });

        buffer = '';
        pintarBuffer();
    }

    function excluir(item) {
        var i = eventos.indexOf(item);
        if (i >= 0) eventos.splice(i, 1);
        pintarTimeline(); pintarElenco(); pintarTotal();

        // Pode ainda estar em voo: só dá para apagar depois de saber o id.
        Promise.resolve(item.promessa).then(function () {
            if (item.id == null) return;
            return fetch('/MarcacaoVideo/Excluir', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: item.id })
            });
        }).catch(function () { aviso('Não deu para apagar a marcação no servidor.'); });
    }

    function desfazer() {
        if (!eventos.length) return;
        excluir(eventos[eventos.length - 1]);
    }

    function pintarTotal() {
        el('mv-total').textContent = eventos.length;
    }

    // Mais recente no topo: é onde o olho vai para conferir o que acabou de
    // marcar. Só as últimas 60 — a lista completa não ajuda em nada aqui.
    function pintarTimeline() {
        var alvo = el('mv-timeline');
        alvo.innerHTML = '';

        eventos.slice(-60).reverse().forEach(function (ev) {
            var acao = acaoPorId[ev.acaoId];
            var j = jogadoraPorId[ev.jogadorId];
            var linha = document.createElement('div');
            linha.className = 'mv-evento' + (ev.id == null ? ' pendente' : '');

            var quando = document.createElement('span');
            quando.className = 'mv-quando';
            quando.textContent = (ev.minutoJogo != null ? ev.minutoJogo + "'" : formatarTempo(ev.segundoVideo));
            quando.title = 'Ir para ' + formatarTempo(ev.segundoVideo) + ' do vídeo';
            quando.addEventListener('click', function () { player.ir(ev.segundoVideo); });

            var desc = document.createElement('span');
            desc.className = 'mv-desc';
            desc.textContent = (j ? (j.numero != null ? j.numero + ' ' : '') + j.nome : '#' + ev.jogadorId)
                + ' — ' + (acao ? acao.rotulo : ev.acaoId);

            var x = document.createElement('button');
            x.type = 'button';
            x.className = 'mv-x';
            x.textContent = '×';
            x.title = 'Apagar marcação';
            x.addEventListener('click', function () { excluir(ev); });

            linha.appendChild(quando);
            linha.appendChild(desc);
            linha.appendChild(x);
            alvo.appendChild(linha);
        });
    }

    var avisoTimer = null;
    function aviso(texto) {
        var caixa = document.querySelector('.mv-aviso');
        if (!caixa) {
            caixa = document.createElement('div');
            caixa.className = 'mv-aviso';
            document.body.appendChild(caixa);
        }
        caixa.textContent = texto;
        clearTimeout(avisoTimer);
        avisoTimer = setTimeout(function () { caixa.remove(); }, 3500);
    }

    // ── Teclado ──────────────────────────────────────────────────────────

    document.addEventListener('keydown', function (ev) {
        var alvo = ev.target;
        if (alvo && (alvo.tagName === 'INPUT' || alvo.tagName === 'TEXTAREA' || alvo.isContentEditable)) return;
        if (ev.ctrlKey || ev.metaKey || ev.altKey) return;
        // Modal aberto (prévia): o teclado é dele.
        if (document.querySelector('.modal.show')) return;

        var code = ev.code;

        // Número da camisa (teclado normal ou numérico).
        var digito = /^Digit(\d)$/.exec(code) || /^Numpad(\d)$/.exec(code);
        if (digito) { ev.preventDefault(); digitar(digito[1]); return; }

        if (code === 'Space') { ev.preventDefault(); player.alternar(); return; }
        if (code === 'Tab') {
            ev.preventDefault();
            ladoAtivo = ladoAtivo === 'casa' ? 'visitante' : 'casa';
            buffer = ''; pintarBuffer();
            selecionar(null);
            return;
        }
        if (code === 'ArrowLeft') { ev.preventDefault(); player.ir(player.tempo() - (ev.shiftKey ? 1 : 5)); return; }
        if (code === 'ArrowRight') { ev.preventDefault(); player.ir(player.tempo() + (ev.shiftKey ? 1 : 5)); return; }
        if (code === 'Escape') { buffer = ''; pintarBuffer(); selecionar(null); return; }
        if (code === 'Backspace') {
            ev.preventDefault();
            // Com dígitos no buffer, apaga dígito; sem eles, desfaz a marcação.
            if (buffer) { buffer = buffer.slice(0, -1); pintarBuffer(); } else desfazer();
            return;
        }

        var acao = acaoPorTecla[code + '|' + (ev.shiftKey ? 1 : 0)];
        if (acao) { ev.preventDefault(); registrar(acao); }
    });

    // ── Prévia e consolidação ────────────────────────────────────────────

    var colunas = [
        ['min', 'minutos'], ['P', 'passes'], ['P✓', 'passesCertos'], ['P%', 'precisaoPasses'],
        ['PC', 'passesChave'], ['Fin', 'finalizacoes'], ['Fin↗', 'finalizacoesNoGol'],
        ['G', 'gols'], ['A', 'assistencias'], ['Des', 'desarmes'], ['Int', 'interceptacoes'],
        ['Blq', 'bloqueios'], ['Due', 'duelos'], ['Due✓', 'duelosVencidos'],
        ['Dri', 'dribles'], ['Dri✓', 'driblesCertos'], ['Def', 'defesas'],
        ['FC', 'faltasCometidas'], ['CA', 'cartoesAmarelos'], ['CV', 'cartoesVermelhos']
    ];

    function abrirPrevia() {
        var corpo = el('mv-previa-corpo');
        corpo.innerHTML = '<div class="text-muted">Calculando…</div>';
        new bootstrap.Modal(el('mv-modal-previa')).show();

        fetch('/MarcacaoVideo/Previa/' + M.jogoId)
            .then(function (r) { return r.json(); })
            .then(function (linhas) {
                if (!linhas.length) { corpo.innerHTML = '<div class="text-muted">Nenhuma marcação ainda.</div>'; return; }

                var cabecalho = '<th>Jogadora</th>' + colunas.map(function (c) {
                    return '<th class="text-center">' + c[0] + '</th>';
                }).join('');

                var corpoLinhas = linhas.map(function (l) {
                    var nome = (l.numero != null ? l.numero + ' ' : '') + l.nome
                        + (l.minutosAssumidos ? ' *' : '');
                    var celulas = colunas.map(function (c) {
                        var v = l[c[1]];
                        return '<td class="text-center">' + (v == null || v === 0 ? '<span class="text-muted">–</span>' : v) + '</td>';
                    }).join('');
                    return '<tr><td class="text-nowrap"></td>' + celulas + '</tr>';
                }).join('');

                corpo.innerHTML =
                    '<div class="table-responsive"><table class="table table-sm table-hover align-middle mb-2">' +
                    '<thead><tr>' + cabecalho + '</tr></thead><tbody>' + corpoLinhas + '</tbody></table></div>' +
                    '<div class="small text-muted">* minutos assumidos como jogo inteiro — marque a entrada dela ' +
                    '(tecla E) para a nota não ser diluída.</div>';

                // Nome vai por textContent: vem do cadastro e não pode ser
                // interpretado como HTML.
                var celulasNome = corpo.querySelectorAll('tbody tr td:first-child');
                linhas.forEach(function (l, i) {
                    celulasNome[i].textContent = (l.numero != null ? l.numero + ' ' : '') + l.nome
                        + (l.minutosAssumidos ? ' *' : '');
                });
            })
            .catch(function () { corpo.innerHTML = '<div class="text-danger">Não deu para calcular a prévia.</div>'; });
    }

    function consolidar() {
        var botao = el('mv-previa-consolidar');
        botao.disabled = true;
        botao.textContent = 'Consolidando…';

        fetch('/MarcacaoVideo/Consolidar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ jogoId: M.jogoId, sobrescreverOutrasFontes: el('mv-sobrescrever').checked })
        }).then(function (r) {
            if (!r.ok) throw new Error('http ' + r.status);
            return r.json();
        }).then(function (d) {
            var msg = d.gravadas + ' jogadora(s) com estatística gravada.';
            if (d.puladas.length) msg += ' Preservadas (já tinham dado de API): ' + d.puladas.join(', ') + '.';
            if (d.minutosAssumidos.length) msg += ' Minutos assumidos como jogo inteiro: ' + d.minutosAssumidos.join(', ') + '.';
            alert(msg);
        }).catch(function () {
            aviso('Falha ao consolidar.');
        }).finally(function () {
            botao.disabled = false;
            botao.textContent = 'Consolidar';
        });
    }

    // ── Ligações da tela ─────────────────────────────────────────────────

    el('mv-carregar').addEventListener('click', carregarVideo);
    el('mv-arquivo').addEventListener('change', function (e) {
        if (e.target.files && e.target.files[0]) carregarArquivo(e.target.files[0]);
    });
    el('mv-play').addEventListener('click', function () { player.alternar(); });
    el('mv-desfazer').addEventListener('click', desfazer);
    el('mv-btn-previa').addEventListener('click', abrirPrevia);
    el('mv-btn-consolidar').addEventListener('click', abrirPrevia);
    el('mv-previa-consolidar').addEventListener('click', consolidar);

    el('mv-ancora-1').addEventListener('click', function () {
        ancoras.t1 = Math.round(player.tempo()); salvarLocal(); pintarTempo();
    });
    el('mv-ancora-2').addEventListener('click', function () {
        ancoras.t2 = Math.round(player.tempo()); salvarLocal(); pintarTempo();
    });

    document.querySelectorAll('[data-vel]').forEach(function (b) {
        b.addEventListener('click', function () {
            iVelocidade = Math.min(velocidades.length - 1, Math.max(0, iVelocidade + parseInt(b.dataset.vel, 10)));
            player.velocidade(velocidades[iVelocidade]);
            el('mv-vel').textContent = velocidades[iVelocidade].toFixed(2).replace(/0$/, '') + '×';
        });
    });

    document.querySelectorAll('.mv-time').forEach(function (b) {
        b.addEventListener('click', function () {
            ladoAtivo = b.dataset.lado;
            buffer = ''; pintarBuffer(); selecionar(null);
        });
    });

    carregarLocal();
    pintarElenco();
    pintarBuffer();
    pintarTimeline();
    pintarTotal();
    setInterval(pintarTempo, 250);
})();
