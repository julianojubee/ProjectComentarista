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