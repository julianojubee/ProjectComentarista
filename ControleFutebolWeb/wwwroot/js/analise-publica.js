/* Página pública somente-leitura de uma análise (/analise/{token}).

   Toda a renderização é do painel-jogo.js (o mesmo do modal Pós-jogo da tela
   logada), com somenteLeitura: true — sem formulário de observação, sem botões
   de editar/remover e sem links que tirem o visitante da página.

   Escopo global de propósito: os botões de aba gerados pelo painel usam
   onclick="ativarTabPosJogo(...)", e o botão do pré-jogo está inline na view. */

var AP_TOKEN = (window.ANALISE_PUBLICA || {}).token || '';

// Nome global esperado pelos botões de aba montados em painel-jogo.js.
function ativarTabPosJogo(tab) {
    PainelJogo.ativarTab(tab);
}

async function carregarAnalisePublica() {
    var corpo = document.getElementById('ap-corpo');
    try {
        var resp = await fetch('/analise/' + encodeURIComponent(AP_TOKEN) + '/dados');
        if (!resp.ok) {
            corpo.innerHTML = '<div class="pgj-vazio" style="padding:3rem 0;">Esta análise não está mais disponível.</div>';
            return;
        }
        corpo.innerHTML = PainelJogo.montarPosJogo(await resp.json(), { somenteLeitura: true });
        ativarTabPosJogo('notas');
    } catch (e) {
        corpo.innerHTML = '<div class="pgj-vazio" style="padding:3rem 0;">Erro ao carregar a análise.</div>';
    }
}

async function abrirPreJogoPublico() {
    var modal = document.getElementById('modal-prejogo-publico');
    var corpo = document.getElementById('prejogo-publico-corpo');
    modal.style.display = 'flex';
    corpo.innerHTML = '<div style="text-align:center; color:var(--text-muted); padding:2rem 0">Carregando...</div>';

    try {
        var resp = await fetch('/analise/' + encodeURIComponent(AP_TOKEN) + '/prejogo');
        if (!resp.ok) throw new Error();
        var dados = await resp.json();
        corpo.innerHTML = '<div class="pj-grid">' +
            PainelJogo.colunaPreJogo(dados.casa, 'casa') +
            PainelJogo.colunaPreJogo(dados.visitante, 'vis') +
        '</div>';
    } catch (e) {
        corpo.innerHTML = '<div style="color:#f87171; text-align:center; padding:1rem;">Erro ao carregar dados do pré-jogo.</div>';
    }
}

function fecharPreJogoPublico() {
    document.getElementById('modal-prejogo-publico').style.display = 'none';
}

document.getElementById('modal-prejogo-publico').addEventListener('click', function (e) {
    if (e.target === this) fecharPreJogoPublico();
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') fecharPreJogoPublico();
});

carregarAnalisePublica();
