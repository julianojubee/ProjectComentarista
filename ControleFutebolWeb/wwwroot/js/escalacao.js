//document.addEventListener("DOMContentLoaded", () => {
//    function atualizarDropdowns() {
//        const selectedValues = new Set(
//            Array.from(document.querySelectorAll('.select-jogador'))
//                .map(s => s.value)
//                .filter(v => v !== "")
//        );

//        document.querySelectorAll('.select-jogador').forEach(s => {
//            const isDuplicate = Array.from(document.querySelectorAll('.select-jogador'))
//                .filter(other => other.value === s.value).length > 1;

//            // Feedback visual
//            if (isDuplicate) {
//                s.classList.add('duplicado');
//            } else {
//                s.classList.remove('duplicado');
//            }

//            // Desabilita opções já escolhidas em outros selects
//            Array.from(s.options).forEach(opt => {
//                if (selectedValues.has(opt.value) && opt.value !== s.value) {
//                    opt.disabled = true;
//                } else {
//                    opt.disabled = false;
//                }
//            });
//        });
//    }

//    // Aplica exclusividade ao mudar seleção
//    document.querySelectorAll('.select-jogador').forEach(select => {
//        select.addEventListener('change', atualizarDropdowns);
//    });

//    // Executa na carga inicial
//    atualizarDropdowns();
//});
