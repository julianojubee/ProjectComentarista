// ── Proteção CSRF para chamadas AJAX ─────────────────────────────────────────
// Injeta o token antiforgery (header RequestVerificationToken) em todas as
// requisições não-seguras (POST/PUT/DELETE/PATCH) para a mesma origem. Assim os
// fetch existentes continuam funcionando com a validação global ativada.
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

// ── Mapa de calor ────────────────────────────────────────────────────────
// Desenha, num canvas anexado por cima do conteúdo do elemento `campoId`
// (precisa ter position:relative + overflow:hidden), a "densidade" dos
// pontos (x/y em % do campo) recebidos — cada ponto vira um borrão radial e
// os borrões se somam (composição 'lighter') antes de virar cor, igual ao
// algoritmo clássico de heatmap.js, só que sem depender de lib externa. Fica
// por cima (não na frente das linhas por baixo) porque fora dos borrões o
// canvas é 100% transparente. Compartilhada entre /Jogos/Analisar e
// /Jogadores/Estatisticas.
function desenharMapaCalor(campoId, pontos) {
    const campo = document.getElementById(campoId);
    if (!campo) return;
    let canvas = campo.querySelector('.heatmap-overlay');
    if (!canvas) {
        canvas = document.createElement('canvas');
        canvas.className = 'heatmap-overlay';
        campo.appendChild(canvas);
    }
    const w = campo.clientWidth || 300;
    const h = campo.clientHeight || 450;
    canvas.width = w;
    canvas.height = h;
    canvas.style.display = 'block';

    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, w, h);
    if (!pontos.length) return;

    const off = document.createElement('canvas');
    off.width = w;
    off.height = h;
    const octx = off.getContext('2d');
    octx.globalCompositeOperation = 'lighter';
    // 0.12 da maior dimensão: borrão mais contido — com 0.17 a mancha de um
    // ponto único ficava larga demais no mini-campo e "vazava" pra fora do campo.
    const raio = Math.max(w, h) * 0.12;
    pontos.forEach(p => {
        const px = (p.x / 100) * w;
        const py = (p.y / 100) * h;
        // peso < 1 (ex.: destino de seta de movimentação) gera um borrão mais
        // fraco, que fica amarelo/verde em vez de vermelho na escala de cor —
        // marca "lugar por onde passou", não a posição principal.
        const peso = p.peso == null ? 1 : p.peso;
        const grad = octx.createRadialGradient(px, py, 0, px, py, raio);
        grad.addColorStop(0, `rgba(0,0,0,${.55 * peso})`);
        grad.addColorStop(1, 'rgba(0,0,0,0)');
        octx.fillStyle = grad;
        octx.beginPath();
        octx.arc(px, py, raio, 0, Math.PI * 2);
        octx.fill();
    });

    const stops = [[74, 222, 128], [250, 204, 21], [249, 115, 22], [239, 68, 68]];
    function corPorIntensidade(t) {
        const pos = t * (stops.length - 1);
        const i = Math.min(stops.length - 2, Math.floor(pos));
        const frac = pos - i;
        const c0 = stops[i], c1 = stops[i + 1];
        return [
            c0[0] + (c1[0] - c0[0]) * frac,
            c0[1] + (c1[1] - c0[1]) * frac,
            c0[2] + (c1[2] - c0[2]) * frac
        ];
    }

    const img = octx.getImageData(0, 0, w, h);
    const data = img.data;
    for (let i = 0; i < data.length; i += 4) {
        const a = data[i + 3] / 255;
        if (a <= 0.03) { data[i + 3] = 0; continue; }
        const t = Math.min(1, a / 0.55);
        const [r, g, b] = corPorIntensidade(t);
        data[i] = r; data[i + 1] = g; data[i + 2] = b;
        data[i + 3] = Math.min(255, a * 255 * 1.5);
    }
    ctx.putImageData(img, 0, 0);
}
////   .then(response => response.json())
////   .then(data => atualizarPlacar(data.golsCasa, data.golsVisitante));