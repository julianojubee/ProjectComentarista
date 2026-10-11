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
        if (j.gols > 0) partes.push('<span class="pgj-evt-gol">' + (j.gols === 1 ? 'gol' : j.gols + ' gols') + '</span>');
        if (j.assistencias > 0) partes.push('<span class="pgj-evt-assist">' + (j.assistencias === 1 ? 'assistência' : j.assistencias + ' assistências') + '</span>');
        (j.cartoesAmarelos || []).forEach(function (m) {
            partes.push('<span class="pgj-evt-cartao" title="Cartão amarelo"><i class="pgj-cartao amarelo"></i>' + m + "'</span>");
        });
        if (j.cartaoVermelho) partes.push('<span class="pgj-evt-cartao" title="Cartão vermelho"><i class="pgj-cartao vermelho"></i>' + j.cartaoVermelho + "'</span>");
        if (j.saiuMinuto) partes.push('<span class="pgj-evt-saiu">saiu ' + j.saiuMinuto + "'</span>");
        if (j.entrouMinuto) partes.push('<span class="pgj-evt-entrou">entrou ' + j.entrouMinuto + "'</span>");
        return partes.join('');
    }

    // Média com vírgula, como se escreve em português
    function decimal(n) {
        return n == null ? '' : n.toFixed(1).replace('.', ',');
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
            (media != null ? '<span class="pgj-team-col-media">média ' + decimal(media) + '</span>' : '') +
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

    var NOME_RESULTADO = { V: 'Vitória', E: 'Empate', D: 'Derrota' };

    // Quadradinhos V/E/D, do mais antigo (esquerda) ao mais recente (direita).
    // legendas[i], quando vem, descreve o jogo ("2 x 1 contra Santos, em casa, 12/08").
    function formaBadges(forma, legendas) {
        return (forma || []).map(function (f, i) {
            var titulo = NOME_RESULTADO[f] + (legendas && legendas[i] ? ': ' + legendas[i] : '');
            return '<span class="pj-res ' + f + '" title="' + escHtml(titulo) + '">' + f + '</span>';
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
                    '<button type="button" class="pgj-obs-editar" onclick="editarObsPosJogo(' + o.id + ')" title="Editar observação" aria-label="Editar observação">Editar</button>' +
                    '<button type="button" class="pgj-obs-remover" onclick="removerObsPosJogo(' + o.id + ')" title="Remover observação" aria-label="Remover observação">Remover</button>';
                return '<div class="pgj-obs-row" data-obs-id="' + o.id + '">' +
                    '<span class="pgj-obs-badge ' + o.tipo + '">' + rotuloTipoObs(o.tipo) + '</span>' +
                    jogadorHtml +
                    '<p class="pgj-obs-texto">' + renderTexto(o.texto) + '</p>' +
                    acoes +
                '</div>';
            }).join('')
            : '<div class="pgj-vazio">Nenhuma observação ainda. Escreva abaixo o que chamou atenção no jogo.</div>';

        if (op.somenteLeitura) return rows;

        var jogadorOpcoes = jogadoresLineup
            .map(function (j) { return '<option value="' + j.id + '">' + escHtml(j.nome) + '</option>'; })
            .join('');

        var form = '<div class="pgj-obs-form">' +
            '<label class="pgj-obs-form-tit" for="pgjObsTexto">Nova observação</label>' +
            '<textarea id="pgjObsTexto" placeholder="O que chamou atenção? Use @ para mencionar um jogador." rows="3"></textarea>' +
            '<div class="pgj-obs-form-row">' +
                '<select id="pgjObsTipo" aria-label="Sobre o que é a observação" onchange="document.getElementById(\'pgjObsJogadorWrap\').style.display = this.value === \'JOGADOR\' ? \'\' : \'none\';">' +
                    '<option value="MANDANTE">Sobre o mandante</option><option value="VISITANTE">Sobre o visitante</option><option value="COMPETICAO">Sobre a competição</option><option value="JOGADOR">Sobre um jogador</option><option value="MARCO">Marco do jogo</option>' +
                '</select>' +
                '<span id="pgjObsJogadorWrap" style="display:none;"><select id="pgjObsJogador" aria-label="Jogador">' + jogadorOpcoes + '</select></span>' +
                '<button type="button" onclick="adicionarObsPosJogo()">Salvar observação</button>' +
            '</div>' +
        '</div>';

        return (d.observacoes && d.observacoes.length)
            ? '<div style="margin-bottom:16px;">' + rows + '</div>' + form
            : rows + form;
    }

    // Ícone de cada tipo de curiosidade (CuriosidadesJogoHelper.Curiosidade.Tipo)
    var ICONES_CURIO = { recorde: '🏆', pessoal: '⭐', media: '📈', gol: '⚽', time: '📊' };

    // Aba Curiosidades: o que esta partida teve de fora do comum, comparado com o
    // campeonato e a temporada antes dela. Já vem ordenado por relevância.
    function montarCuriosidades(d) {
        var lista = d.curiosidades || [];
        if (!lista.length)
            return '<div class="pgj-vazio">Nenhuma curiosidade encontrada para esta partida — nada fora do comum em relação ao campeonato e à temporada, ou faltam estatísticas importadas.</div>';

        var escudos = {
            casa: d.casa && d.casa.escudo ? d.casa.escudo : null,
            vis: d.visitante && d.visitante.escudo ? d.visitante.escudo : null
        };
        var cards = lista.map(function (c) {
            var visual = c.foto
                ? '<img class="pgj-curio-foto" src="' + escHtml(c.foto) + '" alt="" loading="lazy" onerror="this.onerror=null;this.src=\'/images/placeholder-jogador.png\'">'
                : '<span class="pgj-curio-icone" aria-hidden="true">' + (ICONES_CURIO[c.tipo] || '•') + '</span>';
            var escudo = c.lado && escudos[c.lado] ? '<img class="pgj-curio-escudo" src="' + escudos[c.lado] + '" alt="">' : '';
            return '<div class="pgj-curio-card pgj-curio-' + escHtml(c.tipo) + '">' +
                visual +
                '<div class="pgj-curio-corpo">' +
                    '<div class="pgj-curio-titulo">' + (c.foto ? (ICONES_CURIO[c.tipo] || '') + ' ' : '') + escHtml(c.titulo) + escudo + '</div>' +
                    '<div class="pgj-curio-texto">' + escHtml(c.texto) + '</div>' +
                '</div>' +
            '</div>';
        }).join('');

        return '<div class="pgj-curio-lista">' + cards + '</div>' +
            '<div class="pgj-curio-rodape">Comparado com os jogos ' + (d.competicao ? 'de ' + escHtml(d.competicao) + ' ' : 'do campeonato ') + 'antes desta partida; recordes pessoais também olham a temporada inteira.</div>';
    }

    // HTML completo do pós-jogo (hero + abas + painéis).
    // opcoes: { somenteLeitura: bool }
    function montarPosJogo(d, opcoes) {
        var op = opcoes || {};
        var escCasa = d.casa && d.casa.escudo ? '<img src="' + d.casa.escudo + '" alt="">' : '';
        var escVis = d.visitante && d.visitante.escudo ? '<img src="' + d.visitante.escudo + '" alt="">' : '';

        var meta = [d.competicao ? escHtml(d.competicao) : '', d.rodada ? 'rodada ' + d.rodada : '', d.data ? escHtml(d.data) : '']
            .filter(Boolean).join(', ');
        // Média das notas dos titulares, embaixo do nome de cada time
        var mediaHero = function (m) {
            return m != null ? '<div class="pgj-hero-media">média <b>' + decimal(m) + '</b></div>' : '';
        };
        var tituloForma = 'Últimos jogos antes desta partida, o mais recente à direita';
        var hero = '<div class="pgj-hero">' +
            (meta ? '<div class="pgj-hero-meta">' + meta + '</div>' : '') +
            '<div class="pgj-hero-teams">' +
                '<div class="pgj-hero-team">' +
                    '<div class="pgj-hero-escudo">' + escCasa + '</div>' +
                    '<div class="pgj-hero-nome">' + escHtml(d.casa ? d.casa.nome : '') + '</div>' +
                    mediaHero(d.mediaCasa) +
                    (d.formaCasa && d.formaCasa.length ? '<div class="pgj-hero-forma" title="' + tituloForma + '">' + formaBadges(d.formaCasa) + '</div>' : '') +
                '</div>' +
                '<div class="pgj-hero-center">' +
                    '<div class="pgj-hero-placar">' + (d.placarCasa ?? '-') + '<span class="x">x</span>' + (d.placarVisitante ?? '-') + '</div>' +
                    (d.penaltisCasa != null && d.penaltisVisitante != null ? '<div class="pgj-hero-pen">' + d.penaltisCasa + ' x ' + d.penaltisVisitante + ' nos pênaltis</div>' : '') +
                '</div>' +
                '<div class="pgj-hero-team">' +
                    '<div class="pgj-hero-escudo">' + escVis + '</div>' +
                    '<div class="pgj-hero-nome">' + escHtml(d.visitante ? d.visitante.nome : '') + '</div>' +
                    mediaHero(d.mediaVisitante) +
                    (d.formaVisitante && d.formaVisitante.length ? '<div class="pgj-hero-forma" title="' + tituloForma + '">' + formaBadges(d.formaVisitante) + '</div>' : '') +
                '</div>' +
            '</div>' +
        '</div>';

        var nObs = (d.observacoes || []).length;
        var nCurio = (d.curiosidades || []).length;
        var tabs = '<div class="pgj-tabs" role="tablist">' +
            '<button type="button" class="pgj-tab" role="tab" data-pgj-tab="notas" onclick="ativarTabPosJogo(\'notas\')">Notas</button>' +
            '<button type="button" class="pgj-tab" role="tab" data-pgj-tab="campo" onclick="ativarTabPosJogo(\'campo\')">Campo</button>' +
            '<button type="button" class="pgj-tab" role="tab" data-pgj-tab="stats" onclick="ativarTabPosJogo(\'stats\')">Estatísticas</button>' +
            '<button type="button" class="pgj-tab" role="tab" data-pgj-tab="curio" onclick="ativarTabPosJogo(\'curio\')">Curiosidades' + (nCurio ? ' <span class="pgj-tab-n">' + nCurio + '</span>' : '') + '</button>' +
            '<button type="button" class="pgj-tab" role="tab" data-pgj-tab="obs" onclick="ativarTabPosJogo(\'obs\')">Observações' + (nObs ? ' <span class="pgj-tab-n">' + nObs + '</span>' : '') + '</button>' +
        '</div>';

        var painelNotas = '<div class="pgj-panel" id="pgj-panel-notas"><div class="pgj-notas-grid">' +
            colunaNotas(d, 'casa', op) + colunaNotas(d, 'vis', op) +
        '</div></div>';

        var painelCampo = '<div class="pgj-panel" id="pgj-panel-campo"><div class="pgj-campo-grid">' +
            '<div class="pgj-campo-col"><div class="pgj-campo-col-label">' + escCasa + '<span>' + escHtml(d.casa ? d.casa.nome : '') + '</span><small>escalação inicial</small></div>' + campoTime(d.campo.casa, 'casa', op) + '</div>' +
            '<div class="pgj-campo-col"><div class="pgj-campo-col-label">' + escVis + '<span>' + escHtml(d.visitante ? d.visitante.nome : '') + '</span><small>escalação inicial</small></div>' + campoTime(d.campo.visitante, 'vis', op) + '</div>' +
        '</div></div>';

        // Barras neutras: o lado com o número maior fica forte, o outro apagado. A cor
        // da camisa não serve aqui (dois times de branco somem no fundo claro); ela
        // fica só na faixa ao lado do nome do time.
        var corCasa = corTime('casa'), corVis = corTime('vis');
        var statsRows = (d.estatisticas || []).map(function (m) {
            var vc = parseFloat(String(m.casa).replace('%', '').replace(',', '.')) || 0;
            var vv = parseFloat(String(m.vis).replace('%', '').replace(',', '.')) || 0;
            var total = vc + vv;
            var pc = total > 0 ? (vc / total * 100) : 50;
            var pv = 100 - pc;
            return '<div class="pgj-stat-row">' +
                '<span class="num' + (vc < vv ? ' menor' : '') + '">' + escHtml(m.casa) + '</span>' +
                '<div class="pgj-stat-meio">' +
                    '<div class="pgj-stat-lbl">' + escHtml(m.label) + '</div>' +
                    '<div class="pgj-stat-bar">' +
                        '<div class="' + (vc < vv ? 'menor' : '') + '" style="width:' + pc + '%;"></div>' +
                        '<div class="' + (vv < vc ? 'menor' : '') + '" style="width:' + pv + '%;"></div>' +
                    '</div>' +
                '</div>' +
                '<span class="num direita' + (vv < vc ? ' menor' : '') + '">' + escHtml(m.vis) + '</span>' +
            '</div>';
        }).join('');

        var destaquesCards = (d.estatisticasJogador || []).map(function (m) {
            var vc = m.casa ? parseFloat(m.casa.valor) || 0 : -1;
            var vv = m.vis ? parseFloat(m.vis.valor) || 0 : -1;
            var linha = function (x, esc, menor) {
                return x
                    ? '<div class="pgj-destaque-row' + (menor ? ' menor' : '') + '">' + esc + '<span class="pgj-destaque-nome">' + escHtml(x.nome) + '</span><span class="pgj-destaque-val">' + escHtml(x.valor) + '</span></div>'
                    : '<div class="pgj-destaque-row menor"><span class="pgj-destaque-nome">Sem registro</span></div>';
            };
            return '<div class="pgj-destaque-card"><div class="pgj-destaque-label">' + escHtml(m.label) + '</div>' +
                linha(m.casa, escCasa, vc < vv) + linha(m.vis, escVis, vv < vc) + '</div>';
        }).join('');

        var painelStats = '<div class="pgj-panel" id="pgj-panel-stats">' +
            (statsRows ? '<div class="pgj-stats-card">' +
                '<div class="pgj-stats-head">' +
                    '<div class="pgj-stats-head-team" style="--cor:' + corCasa + '">' + escCasa + '<span>' + escHtml(d.casa ? d.casa.nome : '') + '</span></div>' +
                    '<span class="pgj-stats-title">Estatísticas da partida</span>' +
                    '<div class="pgj-stats-head-team direita" style="--cor:' + corVis + '"><span>' + escHtml(d.visitante ? d.visitante.nome : '') + '</span>' + escVis + '</div>' +
                '</div>' + statsRows +
            '</div>' : '') +
            (destaquesCards ? '<div class="pgj-stats-card">' +
                '<div class="pgj-stats-head"><span class="pgj-stats-title">Líderes de cada time</span></div>' +
                '<div class="pgj-destaques-grid">' + destaquesCards + '</div>' +
            '</div>' : '') +
            (!statsRows && !destaquesCards ? '<div class="pgj-vazio">Estatísticas ainda não disponíveis para esta partida.</div>' : '') +
        '</div>';

        var painelCurio = '<div class="pgj-panel" id="pgj-panel-curio">' + montarCuriosidades(d) + '</div>';

        var painelObs = '<div class="pgj-panel" id="pgj-panel-obs">' + montarObservacoes(d, op) + '</div>';

        return hero + tabs + '<div class="pgj-content">' + painelNotas + painelCampo + painelStats + painelCurio + painelObs + '</div>';
    }

    function ativarTab(tab) {
        document.querySelectorAll('.pgj-tab').forEach(function (b) {
            var ativa = b.dataset.pgjTab === tab;
            b.classList.toggle('active', ativa);
            b.setAttribute('aria-selected', ativa ? 'true' : 'false');
        });
        document.querySelectorAll('.pgj-panel').forEach(function (p) { p.classList.toggle('active', p.id === 'pgj-panel-' + tab); });
    }

    // ── Pré-jogo: uma coluna por time (V/E/D, forma, destaques, observações) ──
    // Tudo conta só os jogos da mesma competição e temporada ANTES desta partida.
    function colunaPreJogo(t, lado) {
        if (!t) return '';
        var escudo = t.escudo ? '<img src="' + t.escudo + '" alt="">' : '';

        var record = '<div class="pj-record">' +
            '<div class="pj-rec-box pj-v"><div class="pj-rec-num">' + t.vitorias + '</div><div class="pj-rec-label">' + (t.vitorias === 1 ? 'vitória' : 'vitórias') + '</div></div>' +
            '<div class="pj-rec-box pj-e"><div class="pj-rec-num">' + t.empates + '</div><div class="pj-rec-label">' + (t.empates === 1 ? 'empate' : 'empates') + '</div></div>' +
            '<div class="pj-rec-box pj-d"><div class="pj-rec-num">' + t.derrotas + '</div><div class="pj-rec-label">' + (t.derrotas === 1 ? 'derrota' : 'derrotas') + '</div></div>' +
        '</div>';

        var resumo = t.jogos > 0
            ? '<div class="pj-aprov">' + t.jogos + (t.jogos === 1 ? ' jogo' : ' jogos') + ', <b>' + t.aproveitamento + '%</b> de aproveitamento</div>'
            : '<div class="pj-aprov">Nenhum jogo finalizado antes desta partida.</div>';

        var form = (t.form && t.form.length)
            ? '<div class="pj-section-title">Últimos jogos <small>o mais recente à direita</small></div>' +
              '<div class="pj-form">' + formaBadges(t.form, t.formJogos) + '</div>'
            : '';

        var players = '';
        if (t.destaques && t.destaques.length) {
            players = '<div class="pj-section-title pj-player-cab"><span>Artilharia</span><span class="pj-num">gols</span><span class="pj-num">assist.</span></div>' +
                t.destaques.map(function (p) {
                    var foto = p.foto
                        ? '<img src="' + escHtml(p.foto) + '" alt="" loading="lazy" onerror="this.onerror=null;this.src=\'/images/placeholder-jogador.png\'">'
                        : '<div class="pj-noimg"></div>';
                    return '<div class="pj-player">' + foto +
                        '<span class="pj-player-nome">' + escHtml(p.nome) + '</span>' +
                        '<span class="pj-num' + (p.gols ? '' : ' zero') + '">' + p.gols + '</span>' +
                        '<span class="pj-num' + (p.assists ? '' : ' zero') + '">' + p.assists + '</span></div>';
                }).join('');
        } else if (t.jogos > 0) {
            players = '<div class="pj-section-title">Artilharia</div><div class="pj-vazio">Nenhum gol ou assistência registrado.</div>';
        }

        var obs = (t.observacoes && t.observacoes.length)
            ? '<ul class="pj-obs">' + t.observacoes.map(function (o) { return '<li>' + escHtml(o) + '</li>'; }).join('') + '</ul>'
            : '';

        return '<div class="pj-col ' + lado + '">' +
            '<div class="pj-team-head">' + escudo + '<span class="pj-team-nome">' + escHtml(t.nome) + '</span></div>' +
            record + resumo + obs + form + players +
        '</div>';
    }

    // Grade completa do resumo (as duas colunas + a origem dos números)
    function resumoPreJogo(dados) {
        var origem = [dados.competicao, dados.temporada].filter(Boolean).join(' ');
        return '<div class="pj-grid">' + colunaPreJogo(dados.casa, 'casa') + colunaPreJogo(dados.visitante, 'vis') + '</div>' +
            '<div class="pj-ind-rodape">' + (origem ? escHtml(origem) + ', s' : 'S') + 'ó os jogos antes desta partida.</div>';
    }

    window.PainelJogo = {
        montarPosJogo: montarPosJogo,
        ativarTab: ativarTab,
        colunaPreJogo: colunaPreJogo,
        resumoPreJogo: resumoPreJogo,
        abrevPosicao: abrevPosicao,
        escHtml: escHtml
    };
})();
