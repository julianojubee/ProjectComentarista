// ── Proteção CSRF para chamadas AJAX ─────────────────────────────────────────
// Injeta o token antiforgery (header RequestVerificationToken) em todas as
// requisições não-seguras (POST/PUT/DELETE/PATCH) para a mesma origem. Assim os
// fetch/$.ajax existentes continuam funcionando com a validação global ativada.
(function () {
    var meta = document.querySelector('meta[name="csrf-token"]');
    var token = meta ? meta.getAttribute('content') : null;
    if (!token) return;

    var ehNaoSeguro = function (m) { return /^(POST|PUT|DELETE|PATCH)$/i.test(m || 'GET'); };
    var mesmaOrigem = function (url) {
        if (!url) return true;
        if (/^https?:\/\//i.test(url)) return url.indexOf(window.location.origin) === 0;
        return true; // URL relativa = mesma origem
    };

    // 1) fetch
    if (window.fetch) {
        var fetchOriginal = window.fetch;
        window.fetch = function (input, init) {
            init = init || {};
            var metodo = init.method || (typeof input === 'object' && input ? input.method : 'GET');
            var url = typeof input === 'string' ? input : (input && input.url) || '';
            if (ehNaoSeguro(metodo) && mesmaOrigem(url)) {
                var headers = new Headers(init.headers || (typeof input === 'object' && input ? input.headers : undefined) || {});
                if (!headers.has('RequestVerificationToken')) headers.set('RequestVerificationToken', token);
                init.headers = headers;
            }
            return fetchOriginal.call(this, input, init);
        };
    }

    // 2) jQuery ($.ajax / $.post)
    if (window.jQuery) {
        window.jQuery(document).ajaxSend(function (_e, xhr, opts) {
            if (ehNaoSeguro(opts.type) && mesmaOrigem(opts.url)) {
                xhr.setRequestHeader('RequestVerificationToken', token);
            }
        });
    }
})();

//// Código JavaScript para movimentar jogadores e atualizar placar

//document.addEventListener("DOMContentLoaded", () => {
//    // Permitir arrastar jogadores
//    document.querySelectorAll('.jogador').forEach(player => {
//        player.addEventListener('mousedown', function (event) {
//            const campo = document.getElementById('campo');
//            const shiftX = event.clientX - player.getBoundingClientRect().left;
//            const shiftY = event.clientY - player.getBoundingClientRect().top;

//            function moveAt(pageX, pageY) {
//                player.style.left = pageX - shiftX - campo.getBoundingClientRect().left + 'px';
//                player.style.top = pageY - shiftY - campo.getBoundingClientRect().top + 'px';
//            }

//            function onMouseMove(event) {
//                moveAt(event.pageX, event.pageY);
//            }

//            document.addEventListener('mousemove', onMouseMove);

//            player.onmouseup = function () {
//                document.removeEventListener('mousemove', onMouseMove);
//                player.onmouseup = null;
//            };
//        });

//        player.ondragstart = () => false;
//    });

//    // Botão salvar escalação
//    const salvarBtn = document.getElementById('salvar');
//    if (salvarBtn) {
//        salvarBtn.addEventListener('click', () => {
//            const escalacoes = [];
//            document.querySelectorAll('.jogador').forEach(player => {
//                escalacoes.push({
//                    Id: player.dataset.id,
//                    PosicaoX: parseFloat(player.style.left),
//                    PosicaoY: parseFloat(player.style.top)
//                });
//            });

//            const jogoId = salvarBtn.dataset.jogoId;

//            fetch(`/Jogos/EditEscalacao/${jogoId}`, {
//                method: 'POST',
//                headers: { 'Content-Type': 'application/json' },
//                body: JSON.stringify(escalacoes)
//            }).then(() => {
//                alert("Escalação salva com sucesso!");
//                window.location.href = `/Jogos/Details/${jogoId}`;
//            });
//        });
//    }
//});

///* ============================
//   Função para atualizar placar
//   ============================ */
//function atualizarPlacar(golsCasa, golsVisitante) {
//    document.getElementById("gols-casa").textContent = golsCasa;
//    document.getElementById("gols-visitante").textContent = golsVisitante;
//}

//// Exemplo de uso: quando registrar um gol via fetch
//// fetch('/Jogos/RegistrarGol', {...})
////   .then(response => response.json())
////   .then(data => atualizarPlacar(data.golsCasa, data.golsVisitante));