//// Adiciona evento de drag nos jogadores
//document.querySelectorAll('.jogador-row').forEach(row => {
//    row.addEventListener('dragstart', e => {
//        console.log("Drag iniciado para jogador:", row.dataset.id, row.cells[0].innerText, row.dataset.camisaUrl);

//        e.dataTransfer.setData('jogadorId', row.dataset.id);
//        e.dataTransfer.setData('jogadorNome', row.cells[0].innerText);
//        e.dataTransfer.setData('camisaUrl', row.dataset.camisaUrl);
//    });
//});

//// Adiciona eventos de drop nas posições do campo
//document.querySelectorAll('.posicao-drop').forEach(pos => {
//    pos.addEventListener('dragover', e => e.preventDefault());
//    pos.addEventListener('drop', e => {
//        e.preventDefault();
//        const jogadorId = e.dataTransfer.getData('jogadorId');
//        const jogadorNome = e.dataTransfer.getData('jogadorNome');
//        const camisaUrl = e.dataTransfer.getData('camisaUrl');

//        console.log("Drop na posição:", pos.dataset.posid, pos.dataset.posicao);
//        console.log("Dados recebidos -> jogadorId:", jogadorId, "jogadorNome:", jogadorNome, "camisaUrl:", camisaUrl);

//        // Determina classe da camisa pela posição
//        let posicao = pos.dataset.posicao.toLowerCase();
//        let classeCamisa = "camisa-img";
//        if (posicao.includes("goleiro")) classeCamisa += " camisa-goleiro";
//        else if (posicao.includes("zagueiro")) classeCamisa += " camisa-zagueiro";
//        else if (posicao.includes("lateral")) classeCamisa += " camisa-lateral";
//        else if (posicao.includes("volante")) classeCamisa += " camisa-volante";
//        else if (posicao.includes("meia") || posicao.includes("meio")) classeCamisa += " camisa-meia";
//        else if (posicao.includes("ponta")) classeCamisa += " camisa-ponta";
//        else if (posicao.includes("atacante") || posicao.includes("centroavante")) classeCamisa += " camisa-atacante";

//        // Atualiza visual da posição com a camisa real
//        pos.innerHTML = `
//            <div class="jogador-campo">
//                <img src="${camisaUrl}" class="${classeCamisa}" alt="Camisa" />
//                <div>${jogadorNome}</div>
//            </div>
//        `;

//        // Cria/atualiza hidden input para enviar ao controller
//        let hidden = document.querySelector(`input[name="escalacao[${pos.dataset.posid}].JogadorId"]`);
//        if (!hidden) {
//            hidden = document.createElement("input");
//            hidden.type = "hidden";
//            hidden.name = `escalacao[${pos.dataset.posid}].JogadorId`;
//            document.querySelector("form").appendChild(hidden);
//        }
//        hidden.value = jogadorId;

//        console.log("Hidden input atualizado:", hidden.name, "=", hidden.value);
//    });
//});
