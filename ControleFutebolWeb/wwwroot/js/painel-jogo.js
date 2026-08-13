/* Renderização dos painéis Pós-jogo e Pré-jogo de uma partida.

   Extraído de analisar.js porque dois lugares desenham exatamente a mesma
   coisa a partir do mesmo JSON (montado no servidor por PainelJogoService):

     1. o modal Pós-jogo da tela logada  (/Jogos/Analisar → analisar.js)
     2. a página pública somente-leitura (/analise/{token} → analise-publica.js)

   A diferença entre as duas é só a opção `somenteLeitura`: no modo público não
   há formulário de observação, botões de editar/remover nem links que tirem o
   visitante da página (nome de jogador e menções "@" viram texto).

   IIFE de propósito: nada aqui é chamado por handler inline do HTML — quem
   precisa expor função global (ativarTabPosJogo etc.) faz o wrapper do seu lado. */
(function () {
    'use strict';

    function escHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function notaCor(n) {
        if (n >= 8.5) return '#f59e0b';
        if (n >= 7.5) return '#22c55e';
        if (n >= 6.5) return '#3b82f6';
        if (n >= 5.5) return '#6b7280';
        return '#ef4444';
    }

    function abrevPosicaoParte(p) {
        p = (p || '').trim().toLowerCase();
        if (!p) return '';
        if (p.indexOf('gol') === 0) return 'GOL';
        if (p.indexOf('zag') === 0 || p === 'defensor') return 'ZAG';
        if (p.indexOf('lateral') === 0) return p.indexOf('esq') >= 0 ? 'LE' : 'LD';
        if (p.indexOf('ala') === 0) return p.indexOf('esq') >= 0 ? 'AE' : 'AD';
        if (p.indexOf('volante') === 0) return 'VOL';
        if (p.indexOf('meia') === 0 || p === 'meia') return p.indexOf('ofensiv') >= 0 ? 'MEO' : 'MEI';
        if (p.indexOf('ponta') === 0) return p.indexOf('esq') >= 0 ? 'PE' : 'PD';
        if (p.indexOf('centro') === 0) return 'CA';
        if (p.indexOf('ata') === 0) return 'ATA';
        return p.slice(0, 3).toUpperCase();
    }

    // Abrevia todas as posições (ex.: "Lateral Esquerdo/Ala Esquerdo" → "LE/AE").
    function abrevPosicao(pos) {
        return (pos || '').split('/').map(abrevPosicaoParte).filter(Boolean).join('/');
    }

    function iconesEventos(j) {
        var partes = [];
        if (j.gols > 0) partes.push('<span style="font-size:.72rem; font-weight:700; color:#4ade80; display:inline-flex; align-items:center; gap:2px;">⚽ ' + j.gols + '</span>');
        if (j.assistencias > 0) partes.push('<span style="font-size:.72rem; font-weight:700; color:#fb923c; display:inline-flex; align-items:center; gap:2px;">👟 ' + j.assistencias + '</span>');
        (j.cartoesAmarelos || []).forEach(function (m) {
            partes.push('<span class="pgj-badge-amarelo">🟨 ' + m + "'</span>");
        });
        if (j.cartaoVermelho) partes.push('<span class="pgj-badge-vermelho">🟥 ' + j.cartaoVermelho + "'</span>");
        if (j.saiuMinuto) partes.push('<span class="pgj-evt-saiu">↕ ' + j.saiuMinuto + "'</span>");
        if (j.entrouMinuto) partes.push('<span class="pgj-evt-entrou">↑ ' + j.entrouMinuto + "'</span>");
        return partes.join('');
    }

    function corTime(lado) {
        return getComputedStyle(document.documentElement).getPropertyValue(lado === 'casa' ? '--cor-camisa-casa' : '--cor-camisa-vis').trim() || (lado === 'casa' ? '#dc2626' : '#2563eb');
    }

    // Camisas claras (branco, amarelo...) precisam de número escuro — senão o
    // número some no fundo claro dos círculos (lista e campinho).
    function isLightColor(hex) {
        hex = (hex || '').replace('#', '');
        if (hex.length !== 6) return false;
        var r = parseInt(hex.slice(0, 2), 16), g = parseInt(hex.slice(2, 4), 16), b = parseInt(hex.slice(4, 6), 16);
        return (r * 299 + g * 587 + b * 114) / 1000 > 128;
    }

    // Nome do jogador: link para a ficha dele na tela logada, texto puro no modo
    // público (o visitante não pode sair da página compartilhada).
    function nomeJogadorHtml(j, classe, op) {
        var texto = escHtml(j.nome);
        if (op.somenteLeitura) return '<span class="' + classe + '">' + texto + '</span>';
        return '<a class="' + classe + '" href="/Jogadores/Estatisticas/' + j.jogadorId + '" title="Ver estatísticas de ' + texto + '">' + texto + '</a>';
    }

    function linhaJogador(j, lado, reserva, op) {
        var nc = j.nota != null ? notaCor(j.nota) : '#2a2a2a';
        var notaTxtCor = j.nota != null ? nc : '#4b5563';
        var corNum = reserva ? '#374151' : corTime(lado);
        var claro = !reserva && isLightColor(corNum);
        var numStyle = 'background:' + corNum + ';' +
            (claro ? 'color:#111111; text-shadow:0 0 3px rgba(0,0,0,.5); border:1.5px solid rgba(0,0,0,.25);' : 'color:#fff; text-shadow:0 1px 2px rgba(0,0,0,.8);');
        return '<div class="pgj-jog-row">' +
            '<div class="pgj-jog-num" style="' + numStyle + '">' + (j.numero != null ? j.numero : '') + '</div>' +
            '<div class="pgj-jog-info">' +
                nomeJogadorHtml(j, 'pgj-jog-nome' + (reserva ? ' res' : ''), op) +
                '<div class="pgj-jog-meta">' +
                    (j.posicao ? '<span class="pgj-jog-pos">' + abrevPosicao(j.posicao) + '</span>' : '') +
                    iconesEventos(j) +
                '</div>' +
            '</div>' +
            '<div class="pgj-jog-nota" style="border-color:' + nc + ';"><span style="color:' + notaTxtCor + ';">' + (j.nota != null ? j.nota.toFixed(1) : '–') + '</span></div>' +
        '</div>';
    }

    function colunaNotas(d, lado, op) {
        var time = lado === 'casa' ? d.casa : d.visitante;
        var media = lado === 'casa' ? d.mediaCasa : d.mediaVisitante;
        var titulares = lado === 'casa' ? d.lineup.casaTitulares : d.lineup.visTitulares;
        var reservas = (lado === 'casa' ? d.lineup.casaReservas : d.lineup.visReservas).filter(function (j) { return j.entrouMinuto != null; });

        var escudo = time && time.escudo ? '<img src="' + time.escudo + '" alt="">' : '';
        var header = '<div class="pgj-team-col-header">' + escudo +
            '<span class="pgj-team-col-nome">' + escHtml(time ? time.nome : '') + '</span>' +
            (media != null ? '<span class="pgj-team-col-media">Média ' + media.toFixed(1) + '</span>' : '') +
        '</div>';

        var corpo = '<div class="pgj-subsection-label">Titulares</div>';
        corpo += titulares && titulares.length
            ? titulares.map(function (j) { return linhaJogador(j, lado, false, op); }).join('')
            : '<div class="pgj-vazio">Escalação não registrada.</div>';

        if (reservas.length) {
            corpo += '<div class="pgj-subsection-label sep">Substitutos</div>' +
                reservas.map(function (j) { return linhaJogador(j, lado, true, op); }).join('');
        }

        return '<div class="pgj-team-col">' + header + corpo + '</div>';
    }

    function jogadorCampo(j, lado, op) {
        var nc = j.nota != null ? notaCor(j.nota) : '#374151';
        var notaHtml = j.nota != null ? ('<div class="pgjc-nota" style="background:' + nc + ';">' + j.nota.toFixed(1) + '</div>') : '';

        var corNum = corTime(lado);
        var claro = isLightColor(corNum);
        var circleStyle = 'background:' + corNum + ';' +
            (claro ? 'color:#111111; text-shadow:0 0 3px rgba(0,0,0,.5); border:2px solid rgba(0,0,0,.3);' : 'color:#fff; text-shadow:0 1px 2px rgba(0,0,0,.8); border:2px solid rgba(255,255,255,.7);');

        var eventos = '';
        for (var i = 0; i < (j.gols || 0); i++) eventos += '⚽';
        for (var i2 = 0; i2 < (j.assistencias || 0); i2++) eventos += '👟';
        var eventosHtml = eventos ? '<div style="position:absolute; top:-10px; left:50%; transform:translateX(-50%); font-size:12px; line-height:1; white-space:nowrap; filter:drop-shadow(0 1px 2px rgba(0,0,0,.8));">' + eventos + '</div>' : '';

        // Foto do jogador no campinho; quem não tem foto cadastrada continua com
        // o círculo numerado da camisa. Com foto, o número vira um badge no canto.
        var numeroBadge = j.numero != null
            ? '<span class="pgjc-foto-num" style="background:' + corNum + '; color:' + (claro ? '#111' : '#fff') + ';">' + j.numero + '</span>'
            : '';
        var avatar = j.foto
            ? '<div class="pgjc-foto" style="border-color:' + corNum + ';">' +
                  '<img src="' + escHtml(j.foto) + '" alt="" loading="lazy">' + numeroBadge +
              '</div>'
            : '<div class="pgjc-circle" style="' + circleStyle + '">' + (j.numero != null ? j.numero : '') + '</div>';

        return '<div class="pgjc-jog" style="left:' + j.posicaoX + '%; top:' + j.posicaoY + '%;">' +
            '<div style="position:relative; display:inline-block; margin:0 auto;">' +
                avatar +
                eventosHtml +
            '</div>' +
            nomeJogadorHtml(j, 'pgjc-nome', op) +
            notaHtml +
        '</div>';
    }

    function campoTime(lista, lado, op) {
        var jogadores = (lista || []).map(function (j) { return jogadorCampo(j, lado, op); }).join('');
        return '<div class="campo" style="position:relative; width:100%;">' +
            '<div class="campo-linha-meio"></div><div class="campo-circulo"></div>' +
            '<div class="campo-area-sup"></div><div class="campo-area-inf"></div>' +
            '<div class="campo-gol-sup"></div><div class="campo-gol-inf"></div>' +
            jogadores +
        '</div>';
    }

    function formaBadges(forma) {
        return (forma || []).map(function (f) {
            var bg = f === 'V' ? '#22c55e' : f === 'E' ? '#6b7280' : '#ef4444';
            return '<span style="background:' + bg + ';">' + f + '</span>';
        }).join('');
    }

    function rotuloTipoObs(tipo) {
        return { MANDANTE: 'Mandante', VISITANTE: 'Visitante', COMPETICAO: 'Competição', JOGADOR: 'Jogador', MARCO: 'Marco' }[tipo] || tipo;
    }

    function montarObservacoes(d, op) {
        var jogadoresLineup = []
            .concat(d.lineup.casaTitulares, d.lineup.casaReservas, d.lineup.visTitulares, d.lineup.visReservas)
            .map(function (j) { return { id: j.jogadorId, nome: j.nome }; });
        // Guardado no window porque o dropdown de "@" (modo edição) precisa da
        // lista de jogadores do jogo que está aberto agora.
        window._pgjJogadoresAtual = jogadoresLineup;

        var renderTexto = function (texto) {
            return window.MencaoJogador.renderizarTexto(texto, jogadoresLineup, { link: !op.somenteLeitura });
        };

        var rows = (d.observacoes && d.observacoes.length)
            ? d.observacoes.map(function (o) {
                var jogadorHtml = (o.tipo === 'JOGADOR' && o.jogadorNome) ? '<span class="pgj-obs-jogador">' + escHtml(o.jogadorNome) + '</span>' : '';
                var acoes = op.somenteLeitura ? '' :
                    '<button type="button" class="pgj-obs-editar" onclick="editarObsPosJogo(' + o.id + ')" title="Editar">&#9998;</button>' +
                    '<button type="button" class="pgj-obs-remover" onclick="removerObsPosJogo(' + o.id + ')">✕</button>';
                return '<div class="pgj-obs-row" data-obs-id="' + o.id + '">' +
                    '<span class="pgj-obs-badge ' + o.tipo + '">' + rotuloTipoObs(o.tipo) + '</span>' +
                    jogadorHtml +
                    '<p class="pgj-obs-texto">' + renderTexto(o.texto) + '</p>' +
                    acoes +
                '</div>';
            }).join('')
            : '<div class="pgj-vazio">Nenhuma observação registrada para esta partida.</div>';

        if (op.somenteLeitura) return rows;

        var jogadorOpcoes = jogadoresLineup
            .map(function (j) { return '<option value="' + j.id + '">' + escHtml(j.nome) + '</option>'; })
            .join('');

        var form = '<div class="pgj-obs-form">' +
            '<div class="pgj-obs-form-row">' +
                '<select id="pgjObsTipo" onchange="document.getElementById(\'pgjObsJogadorWrap\').style.display = this.value === \'JOGADOR\' ? \'\' : \'none\';">' +
                    '<option value="MANDANTE">Mandante</option><option value="VISITANTE">Visitante</option><option value="COMPETICAO">Competição</option><option value="JOGADOR">Jogador</option><option value="MARCO">Marco</option>' +
                '</select>' +
                '<span id="pgjObsJogadorWrap" style="display:none;"><select id="pgjObsJogador">' + jogadorOpcoes + '</select></span>' +
                '<button type="button" onclick="adicionarObsPosJogo()">Salvar</button>' +
            '</div>' +
            '<textarea id="pgjObsTexto" placeholder="Adicionar observação... use @ para mencionar um jogador" rows="3"></textarea>' +
        '</div>';

        return (d.observacoes && d.observacoes.length)
            ? '<div style="margin-bottom:16px;">' + rows + '</div>' + form
            : rows + form;
    }

    // HTML completo do pós-jogo (hero + abas + painéis).
    // opcoes: { somenteLeitura: bool }
    function montarPosJogo(d, opcoes) {
        var op = opcoes || {};
        var escCasa = d.casa && d.casa.escudo ? '<img src="' + d.casa.escudo + '" alt="">' : '';
        var escVis = d.visitante && d.visitante.escudo ? '<img src="' + d.visitante.escudo + '" alt="">' : '';

        var pillComp = (d.competicao ? escHtml(d.competicao) : '') + (d.rodada ? ' · Rod. ' + d.rodada : '');
        var hero = '<div class="pgj-hero">' +
            (pillComp || d.data ? '<div class="pgj-hero-pills">' +
                (pillComp ? '<span class="pgj-pill-comp">' + pillComp + '</span>' : '') +
                (d.data ? '<span class="pgj-pill-date">' + escHtml(d.data) + '</span>' : '') +
            '</div>' : '') +
            '<div class="pgj-hero-teams">' +
                '<div class="pgj-hero-team">' +
                    '<div class="pgj-hero-escudo">' + escCasa + '</div>' +
                    '<div class="pgj-hero-nome">' + escHtml(d.casa ? d.casa.nome : '') + '</div>' +
                    '<div class="pgj-hero-forma">' + formaBadges(d.formaCasa) + '</div>' +
                '</div>' +
                '<div class="pgj-hero-center">' +
                    '<div class="pgj-hero-placar">' + (d.placarCasa ?? '-') + ' &ndash; ' + (d.placarVisitante ?? '-') + '</div>' +
                    (d.penaltisCasa != null && d.penaltisVisitante != null ? '<div class="pgj-hero-pen">Pênaltis: ' + d.penaltisCasa + '–' + d.penaltisVisitante + '</div>' : '') +
                    '<div class="pgj-hero-medias">' +
                        (d.mediaCasa != null ? '<div class="pgj-hero-media"><span class="val">' + d.mediaCasa.toFixed(1) + '</span><span class="lbl">Média Casa</span></div>' : '') +
                        (d.mediaVisitante != null ? '<div class="pgj-hero-media"><span class="val">' + d.mediaVisitante.toFixed(1) + '</span><span class="lbl">Média Visit.</span></div>' : '') +
                    '</div>' +
                '</div>' +
                '<div class="pgj-hero-team">' +
                    '<div class="pgj-hero-escudo">' + escVis + '</div>' +
                    '<div class="pgj-hero-nome">' + escHtml(d.visitante ? d.visitante.nome : '') + '</div>' +
                    '<div class="pgj-hero-forma">' + formaBadges(d.formaVisitante) + '</div>' +
                '</div>' +
            '</div>' +
        '</div>';

        var tabs = '<div class="pgj-tabs">' +
            '<button type="button" class="pgj-tab" data-pgj-tab="notas" onclick="ativarTabPosJogo(\'notas\')">⭐ Notas dos Jogadores</button>' +
            '<button type="button" class="pgj-tab" data-pgj-tab="campo" onclick="ativarTabPosJogo(\'campo\')">🟩 Campo</button>' +
            '<button type="button" class="pgj-tab" data-pgj-tab="stats" onclick="ativarTabPosJogo(\'stats\')">📊 Estatísticas</button>' +
            '<button type="button" class="pgj-tab" data-pgj-tab="obs" onclick="ativarTabPosJogo(\'obs\')">📝 Observações</button>' +
        '</div>';

        var painelNotas = '<div class="pgj-panel" id="pgj-panel-notas"><div class="pgj-notas-grid">' +
            colunaNotas(d, 'casa', op) + colunaNotas(d, 'vis', op) +
        '</div></div>';

        var painelCampo = '<div class="pgj-panel" id="pgj-panel-campo"><div class="pgj-campo-grid">' +
            '<div><div class="pgj-campo-col-label">' + escCasa + '<span>' + escHtml(d.casa ? d.casa.nome : '') + ' · Escalação Inicial</span></div>' + campoTime(d.campo.casa, 'casa', op) + '</div>' +
            '<div><div class="pgj-campo-col-label">' + escVis + '<span>' + escHtml(d.visitante ? d.visitante.nome : '') + ' · Escalação Inicial</span></div>' + campoTime(d.campo.visitante, 'vis', op) + '</div>' +
        '</div></div>';

        var statsRows = (d.estatisticas || []).map(function (m) {
            var vc = parseFloat(String(m.casa).replace('%', '')) || 0;
            var vv = parseFloat(String(m.vis).replace('%', '')) || 0;
            var total = vc + vv;
            var pc = total > 0 ? (vc / total * 100) : 50;
            var pv = 100 - pc;
            return '<div class="pgj-stat-row">' +
                '<div class="pgj-stat-lbl">' + escHtml(m.label) + '</div>' +
                '<div class="pgj-stat-vals">' +
                    '<span class="num right">' + escHtml(m.casa) + '</span>' +
                    '<div class="pgj-stat-bar"><div style="width:' + pc + '%; background:#dc2626;"></div><div style="width:' + pv + '%; background:#16a34a;"></div></div>' +
                    '<span class="num">' + escHtml(m.vis) + '</span>' +
                '</div>' +
            '</div>';
        }).join('');

        var destaquesCards = (d.estatisticasJogador || []).map(function (m) {
            var casaHtml = m.casa
                ? '<div class="pgj-destaque-row">' + escCasa + '<span class="pgj-destaque-nome" style="color:#e5e7eb;">' + escHtml(m.casa.nome) + '</span><span class="pgj-destaque-val" style="color:#f1f5f9;">' + m.casa.valor + '</span></div>'
                : '<div class="pgj-destaque-row"><span class="pgj-destaque-nome" style="color:#4b5563;">–</span></div>';
            var visHtml = m.vis
                ? '<div class="pgj-destaque-row">' + escVis + '<span class="pgj-destaque-nome" style="color:#9ca3af;">' + escHtml(m.vis.nome) + '</span><span class="pgj-destaque-val" style="color:#6b7280;">' + m.vis.valor + '</span></div>'
                : '<div class="pgj-destaque-row"><span class="pgj-destaque-nome" style="color:#4b5563;">–</span></div>';
            return '<div class="pgj-destaque-card"><div class="pgj-destaque-label">' + escHtml(m.label) + '</div>' + casaHtml + visHtml + '</div>';
        }).join('');

        var painelStats = '<div class="pgj-panel" id="pgj-panel-stats">' +
            (statsRows ? '<div class="pgj-stats-card">' +
                '<div class="pgj-stats-head">' +
                    '<div class="pgj-stats-head-team">' + escCasa + '<span style="color:#dc2626;">' + escHtml(d.casa ? d.casa.nome : '') + '</span></div>' +
                    '<span class="pgj-stats-title">Estatísticas da Partida</span>' +
                    '<div class="pgj-stats-head-team"><span style="color:#16a34a;">' + escHtml(d.visitante ? d.visitante.nome : '') + '</span>' + escVis + '</div>' +
                '</div>' + statsRows +
            '</div>' : '') +
            (destaquesCards ? '<div class="pgj-stats-card">' +
                '<div class="pgj-stats-head"><span class="pgj-stats-title">Destaques Individuais</span></div>' +
                '<div class="pgj-destaques-grid">' + destaquesCards + '</div>' +
            '</div>' : '') +
            (!statsRows && !destaquesCards ? '<div class="pgj-vazio">Estatísticas ainda não disponíveis para esta partida.</div>' : '') +
        '</div>';

        var painelObs = '<div class="pgj-panel" id="pgj-panel-obs">' + montarObservacoes(d, op) + '</div>';

        return hero + tabs + '<div class="pgj-content">' + painelNotas + painelCampo + painelStats + painelObs + '</div>';
    }

    function ativarTab(tab) {
        document.querySelectorAll('.pgj-tab').forEach(function (b) { b.classList.toggle('active', b.dataset.pgjTab === tab); });
        document.querySelectorAll('.pgj-panel').forEach(function (p) { p.classList.toggle('active', p.id === 'pgj-panel-' + tab); });
    }

    // ── Pré-jogo: uma coluna por time (V/E/D, forma, destaques, observações) ──
    function colunaPreJogo(t, lado) {
        if (!t) return '';
        var escudo = t.escudo ? '<img src="' + t.escudo + '" alt="">' : '';

        var record = '<div class="pj-record">' +
            '<div class="pj-rec-box pj-v"><div class="pj-rec-num">' + t.vitorias + '</div><div class="pj-rec-label">Vitórias</div></div>' +
            '<div class="pj-rec-box pj-e"><div class="pj-rec-num">' + t.empates + '</div><div class="pj-rec-label">Empates</div></div>' +
            '<div class="pj-rec-box pj-d"><div class="pj-rec-num">' + t.derrotas + '</div><div class="pj-rec-label">Derrotas</div></div>' +
        '</div>';

        var form = (t.form && t.form.length)
            ? '<div class="pj-section-title">Últimos jogos</div><div class="pj-form">' +
              t.form.map(function (f) { return '<span class="' + f + '">' + f + '</span>'; }).join('') + '</div>'
            : '';

        var players = '<div class="pj-section-title">Jogadores em destaque</div>';
        if (t.destaques && t.destaques.length) {
            players += t.destaques.map(function (p) {
                var foto = p.foto ? '<img src="' + p.foto + '" alt="">' : '<div class="pj-noimg"></div>';
                var stat = [];
                if (p.gols) stat.push('⚽ ' + p.gols);
                if (p.assists) stat.push('🅰 ' + p.assists);
                return '<div class="pj-player">' + foto +
                    '<span class="pj-player-nome">' + escHtml(p.nome) + '</span>' +
                    '<span class="pj-player-stat">' + stat.join(' · ') + '</span></div>';
            }).join('');
        } else {
            players += '<div class="pj-vazio">Sem gols/assistências registrados.</div>';
        }

        var obs = (t.observacoes && t.observacoes.length)
            ? '<div class="pj-section-title">Observações</div><ul class="pj-obs">' +
              t.observacoes.map(function (o) { return '<li>' + escHtml(o) + '</li>'; }).join('') + '</ul>'
            : '';

        return '<div class="pj-col ' + lado + '">' +
            '<div class="pj-team-head">' + escudo + '<span class="pj-team-nome">' + escHtml(t.nome) + '</span></div>' +
            record + form + players + obs +
        '</div>';
    }

    window.PainelJogo = {
        montarPosJogo: montarPosJogo,
        ativarTab: ativarTab,
        colunaPreJogo: colunaPreJogo,
        abrevPosicao: abrevPosicao,
        escHtml: escHtml
    };
})();
