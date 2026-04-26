//document.addEventListener("DOMContentLoaded", () => {
//    document.querySelectorAll('.jogador').forEach(player => {
//        const left = parseFloat(player.style.left.replace('%', '')) || 0;
//        const top = parseFloat(player.style.top.replace('%', '')) || 0;
//        player.dataset.posicaoX = left.toFixed(2);
//        player.dataset.posicaoY = top.toFixed(2);

//        player.addEventListener('mousedown', function (event) {
//            event.preventDefault();

//            function moveAt(clientX, clientY) {
//                const campo = player.closest('.campo');
//                if (!campo) return;

//                const rect = campo.getBoundingClientRect();
//                let x = ((clientX - rect.left) / rect.width) * 100;
//                let y = ((clientY - rect.top) / rect.height) * 100;

//                x -= (player.offsetWidth / 2 / rect.width) * 100;
//                y -= (player.offsetHeight / 2 / rect.height) * 100;

//                x = Math.max(0, Math.min(100, x));
//                y = Math.max(0, Math.min(100, y));

//                player.style.left = x.toFixed(2) + '%';
//                player.style.top = y.toFixed(2) + '%';

//                player.dataset.posicaoX = x.toFixed(2);
//                player.dataset.posicaoY = y.toFixed(2);
//            }

//            function onMouseMove(event) {
//                moveAt(event.clientX, event.clientY);
//            }

//            document.addEventListener('mousemove', onMouseMove);

//            function onMouseUp() {
//                document.removeEventListener('mousemove', onMouseMove);
//                document.removeEventListener('mouseup', onMouseUp);
//            }

//            document.addEventListener('mouseup', onMouseUp);
//        });

//        player.ondragstart = () => false;
//    });

//    const form = document.querySelector('form[method="post"]');
//    if (form) {
//        form.onsubmit = function () {
//            document.querySelectorAll('.jogador').forEach(jogador => {
//                const escId = jogador.dataset.escid;
//                const x = parseFloat(jogador.dataset.posicaoX) || 0;
//                const y = parseFloat(jogador.dataset.posicaoY) || 0;
//                const isCasa = jogador.closest('.bloco-casa') !== null;
//                const prefixX = isCasa ? "posicoesCasaX" : "posicoesVisitanteX";
//                const prefixY = isCasa ? "posicoesCasaY" : "posicoesVisitanteY";

//                let inputX = document.querySelector(`input[name="${prefixX}[${escId}]"]`);
//                if (!inputX) {
//                    inputX = document.createElement('input');
//                    inputX.type = 'hidden';
//                    inputX.name = `${prefixX}[${escId}]`;
//                    form.appendChild(inputX);
//                }
//                inputX.value = Math.round(x * 100);

//                let inputY = document.querySelector(`input[name="${prefixY}[${escId}]"]`);
//                if (!inputY) {
//                    inputY = document.createElement('input');
//                    inputY.type = 'hidden';
//                    inputY.name = `${prefixY}[${escId}]`;
//                    form.appendChild(inputY);
//                }
//                inputY.value = Math.round(y * 100);
//            });
//        };
//    }
//});
