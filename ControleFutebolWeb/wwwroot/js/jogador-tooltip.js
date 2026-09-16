/* Tooltip ℹ do jogador — quem ele é, o que fez na competição/temporada e as
   médias por jogo das estatísticas importadas.

   Saiu de analisar.js quando o campo do Match Up (modal Pré-jogo e a página
   pública /creators/escalacao) passou a mostrar o mesmo ℹ: o HTML do tooltip
   é longo o bastante para que duas cópias divergissem na primeira mudança.

   Quem usa carrega os números com `configurar` e chama `mostrar`/`esconder`
   (ou usa `criarBotao`, que já liga os dois no mouseenter/mouseleave):

       JogadorTooltip.configurar({ dados, medias, titularCompeticao, ... });
       campo.appendChild(JogadorTooltip.criarBotao(jogadorId));

   A tela de análise reconfigura tudo quando o seletor "ℹ tooltip" troca a
   temporada; o Match Up configura uma vez, com os dados que vieram no JSON.

   Estilos em css/jogador-tooltip.css. */

window.JogadorTooltip = (function () {
    'use strict';

    // Números mostrados no tooltip. Chaves são ids de jogador (string, como o
    // JSON entrega). temporada 0 = sem recorte: a linha "Temporada" some.
    var ctx = {
        dados: {},               // id → { nome, foto, posicao, numero, idade, ... }
        medias: {},              // id → médias por jogo (MediasPorJogo)
        titularCompeticao: {},   // id → jogos como titular na competição
        golsTemporada: {},
        assistsTemporada: {},
        titularTemporada: {},
        timeAnterior: {},        // id → { nome, escudo, temporada, jogos }
        temporada: 0,
    };

    function configurar(novo) {
        Object.keys(novo || {}).forEach(function (k) {
            if (novo[k] != null) ctx[k] = novo[k];
        });
    }

    // Soma um pacote ao que já está carregado, sem apagar o resto: a tela de
    // análise configura o elenco do jogo e o Match Up chega depois com os
    // jogadores que faltavam. Temporada só entra se o pacote trouxer uma.
    function acrescentar(pacote) {
        if (!pacote) return;
        ['dados', 'medias', 'titularCompeticao', 'golsTemporada',
         'assistsTemporada', 'titularTemporada', 'timeAnterior'].forEach(function (k) {
            if (pacote[k]) Object.assign(ctx[k], pacote[k]);
        });
        if (pacote.temporada) ctx.temporada = pacote.temporada;
    }

    function dadosDe(jogadorId) {
        return ctx.dados[jogadorId];
    }

    // O contêiner é único na página e criado sob demanda: a tela de análise já
    // declara <div id="jogador-tooltip">, as demais não precisam.
    function caixa() {
        var tt = document.getElementById('jogador-tooltip');
        if (!tt) {
            tt = document.createElement('div');
            tt.id = 'jogador-tooltip';
            document.body.appendChild(tt);
        }
        return tt;
    }

    // A sigla pode vir pronta do servidor; senão usa o abreviador da tela de
    // análise, e no pior caso mostra a posição como veio.
    function abrevPosicao(dados) {
        if (dados.sigla) return dados.sigla;
        if (window.PainelJogo && window.PainelJogo.abrevPosicao) return window.PainelJogo.abrevPosicao(dados.posicao);
        return dados.posicao;
    }

    // Um bloco de stats do tooltip (Competição/Temporada): rótulo em cima e,
    // logo abaixo, jogos como titular, gols e assists no escopo — vazio se
    // não houver nada a mostrar.
    function ttLinhaStats(rotulo, gols, assists, titular) {
        const partes = [
            titular ? `<span class="tt-stat-tit">🏁 ${titular} titular</span>` : '',
            (gols > 0)    ? `<span class="tt-stat-gol">⚽ ${gols} gol${gols > 1 ? 's' : ''}</span>` : '',
            (assists > 0) ? `<span class="tt-stat-ast">🅰️ ${assists} assist${assists > 1 ? 's' : ''}</span>` : '',
        ].filter(Boolean).join('');
        return partes
            ? `<div class="tt-stats"><div class="tt-stat-rotulo">${rotulo}</div><div class="tt-stat-valores">${partes}</div></div>`
            : '';
    }

    // Botão ℹ de um jogador, para os elementos montados em JS (arrastar para o
    // campo/banco, cadastro rápido pelo "+", slots do Match Up). Os renderizados
    // pelo servidor chamam mostrar/esconder direto no onmouseenter.
    function criarBotao(jogadorId) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn-info-jogador';
        btn.textContent = 'ℹ';
        btn.addEventListener('mouseenter', () => mostrar(btn, dadosDe(jogadorId)));
        btn.addEventListener('mouseleave', esconder);
        return btn;
    }

    function mostrar(btn, dados) {
        const tt = caixa();
        if (!dados) return;
        // Foto local (ex.: /MediaProxy/FotoJogador/123, vinda do FotMob) vai direto; só
        // URL externa passa pelo proxy genérico.
        const fotoSrc = !dados.foto ? '/images/placeholder-jogador.png'
            : dados.foto.startsWith('/') ? dados.foto
            : `/MediaProxy/Imagem?url=${encodeURIComponent(dados.foto)}`;
        let html = `<div class="tt-header"><img class="tt-foto" src="${fotoSrc}" /><div class="tt-nome">${dados.nome}</div></div>`;
        const meta = [
            dados.posicao ? `<span>🎽 ${abrevPosicao(dados)}</span>` : '',
            dados.numero  ? `<span># ${dados.numero}</span>`   : '',
            dados.idade   ? `<span>📅 ${dados.idade}a</span>`  : '',
            dados.altura  ? `<span>📏 ${(dados.altura / 100).toFixed(2).replace('.', ',')}m</span>` : '',
            dados.peso    ? `<span>⚖️ ${dados.peso}kg</span>`  : '',
            dados.nac ? (dados.nacFlag
                ? `<span><img class="tt-icon" src="${dados.nacFlag}" /> ${dados.nac}</span>`
                : `<span>🌍 ${dados.nac}</span>`) : '',
            // Clube (só em jogo de seleção — ver ClubeTooltip/ClubeEscudoTooltip)
            dados.time ? (dados.timeEscudo
                ? `<span><img class="tt-icon" src="${dados.timeEscudo}" /> ${dados.time}</span>`
                : `<span>🏟️ ${dados.time}</span>`) : '',
        ].filter(Boolean).join('');
        if (meta) html += `<div class="tt-meta">${meta}</div>`;
        // "Vinha do": clube da temporada passada, quando não é o clube de hoje.
        const ant = ctx.timeAnterior[dados.id];
        if (ant) {
            const escudo = ant.escudo ? `<img class="tt-icon" src="${ant.escudo}" />` : '🏟️';
            html += `<div class="tt-vinha-do">` +
                `<span class="tt-vinha-icone" title="Vinha do">🔄</span>` +
                `<span class="tt-vinha-time">${escudo} ${ant.nome}</span>` +
                // Sem contagem quando o dado veio da janela de transferências,
                // que não sabe quantos jogos ele fez pelo clube antigo.
                `<span class="tt-vinha-obs">${ant.temporada}${ant.jogos > 0 ? ` · ${ant.jogos} jogo${ant.jogos > 1 ? 's' : ''}` : ''}</span>` +
                `</div>`;
        }
        // Stats separadas por escopo: competição do jogo e temporada (todas as
        // competições do mesmo ano). dados.gols/assists vêm da competição.
        html += ttLinhaStats('Competição', dados.gols, dados.assists, ctx.titularCompeticao[dados.id]);
        if (ctx.temporada > 0)
            html += ttLinhaStats(`Temporada ${ctx.temporada}`,
                ctx.golsTemporada[dados.id] || 0, ctx.assistsTemporada[dados.id] || 0, ctx.titularTemporada[dados.id]);
        const md = ctx.medias[dados.id];
        if (md) {
            const celulas = [
                [`${md.finalizacoes}`, `Finaliz. · ${md.finalizacoesPct}% gol`],
                [`${md.dribles}`, `Dribles · ${md.driblesPct}% certos`],
                [`${md.duelos}`, `Duelos · ${md.duelosPct}% venc.`],
                [`${md.passes}`, 'Passes'],
                [`${md.passesChave}`, 'Passes-chave'],
                [`${md.desarmes}`, 'Desarmes'],
                [`${md.interceptacoes}`, 'Intercept.'],
                [`${md.bloqueios}`, 'Bloqueios'],
                [`${md.faltasSofridas}`, 'Faltas sofr.'],
                [`${md.faltasCometidas}`, 'Faltas com.'],
            ];
            if (md.defesas > 0) celulas.unshift([`${md.defesas}`, 'Defesas']);
            html += `<div class="tt-medias-titulo">Médias por jogo · ${md.jogos} jogo${md.jogos > 1 ? 's' : ''}</div>` +
                `<div class="tt-medias">` +
                celulas.map(([v, l]) => `<div class="tt-media-cel"><b>${v}</b><span>${l}</span></div>`).join('') +
                `</div>`;
        }
        if (dados.obs)     html += `<div class="tt-obs">${dados.obs}</div>`;
        tt.innerHTML = html;
        tt.style.display = 'block';
        const rect = btn.getBoundingClientRect();
        const tw = tt.offsetWidth;
        const th = tt.offsetHeight;
        let left = rect.right + 8;
        let top  = rect.top - th / 2;
        if (left + tw > window.innerWidth - 8) left = rect.left - tw - 8;
        if (top < 8) top = 8;
        if (top + th > window.innerHeight - 8) top = window.innerHeight - th - 8;
        tt.style.left = left + 'px';
        tt.style.top  = top + 'px';
    }

    function esconder() {
        const tt = document.getElementById('jogador-tooltip');
        if (tt) tt.style.display = 'none';
    }

    return {
        configurar: configurar,
        acrescentar: acrescentar,
        dados: dadosDe,
        mostrar: mostrar,
        esconder: esconder,
        criarBotao: criarBotao,
    };
})();
