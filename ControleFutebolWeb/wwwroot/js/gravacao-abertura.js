/* Gravar a abertura do modo Transmissão (/Jogos/Analisar) em vídeo, para
   postar no X.

   A abertura (js/abertura-jogo.js) é DOM + CSS 3D: perspectiva, blur dos
   refletores, transições disparadas por classe. Redesenhar tudo isso num
   canvas seria outra animação, parecida e não a mesma. Então o vídeo é a
   própria aba: getDisplayMedia com preferCurrentTab (o navegador pergunta
   uma vez se pode compartilhar esta aba), a abertura roda numa caixa 16:9 no
   meio da janela (.ab-gravando) e cada quadro da captura é recortado nessa
   caixa e desenhado num canvas de 1920×1080, que é o que o MediaRecorder grava.
   No Firefox, que não captura aba, o usuário compartilha a janela ou a tela,
   e a calibração (calibrar) acha a página dentro do quadro.

   O arquivo sai do MediaRecorder em MP4 (Chrome/Edge recentes) ou WebM e
   passa sempre pelo ffmpeg do servidor (/Jogadas/ConverterMp4 com fps): H.264
   yuv420p, taxa constante e faststart, o que o upload do X aceita sem
   reclamar. Sem ffmpeg, baixa o que o navegador gravou.

   Gravação em tempo real: a aba precisa ficar na frente até o fim (~40s).
   Esc cancela. Só leitura: nada aqui salva ou altera a análise. */
(function () {
    'use strict';

    const LARGURA = 1920, ALTURA = 1080, FPS = 30;
    const CAUDA_MS = 1500;          // o campo da transmissão parado depois do pouso
    const BITS_POR_SEGUNDO = 10_000_000;

    const TIPOS = [
        { mime: 'video/mp4;codecs=avc1.640028', ext: 'mp4' },
        { mime: 'video/mp4;codecs=avc1', ext: 'mp4' },
        { mime: 'video/mp4', ext: 'mp4' },
        { mime: 'video/webm;codecs=vp9', ext: 'webm' },
        { mime: 'video/webm;codecs=vp8', ext: 'webm' },
        { mime: 'video/webm', ext: 'webm' },
    ];

    let gravando = false;

    function tipoDeVideo() {
        if (typeof MediaRecorder === 'undefined') return null;
        return TIPOS.find(t => MediaRecorder.isTypeSupported(t.mime)) || null;
    }

    function botao(texto) {
        const b = document.getElementById('anBtnGravarVideo');
        if (b) { b.textContent = texto; b.disabled = gravando; }
    }

    // Marca d'água "Análise de Craque" (fundo preto, dourado, Anton). A fonte
    // vem do Google Fonts (liberado no CSP); sem ela, cai numa condensada do
    // sistema — a marca é opcional, não pode travar a gravação.
    async function carregarFonteMarca() {
        if (!document.querySelector('link[data-fonte-marca]')) {
            const l = document.createElement('link');
            l.rel = 'stylesheet';
            l.href = 'https://fonts.googleapis.com/css2?family=Anton&display=swap';
            l.dataset.fonteMarca = '1';
            document.head.appendChild(l);
        }
        try {
            await Promise.race([document.fonts.load('40px Anton'), new Promise(r => setTimeout(r, 2500))]);
        } catch (e) { /* sem a fonte */ }
    }

    function desenharMarca(ctx) {
        const txt = 'ANÁLISE DE CRAQUE';
        ctx.save();
        ctx.font = '30px Anton, Impact, "Arial Narrow", sans-serif';
        ctx.textBaseline = 'middle';
        const w = ctx.measureText(txt).width;
        const padX = 18, h = 48, x = LARGURA - w - padX * 2 - 32, y = ALTURA - h - 30;
        ctx.globalAlpha = 0.82;
        ctx.fillStyle = '#0b0b0b';
        ctx.fillRect(x, y, w + padX * 2, h);
        ctx.fillStyle = '#d4a520';
        ctx.fillRect(x, y, 5, h);
        ctx.fillStyle = '#f2c94c';
        ctx.fillText(txt, x + padX + 2, y + h / 2 + 1);
        ctx.restore();
    }

    // Onde a página está dentro do quadro capturado. Capturando a aba (Chrome,
    // Edge), o quadro é a página; capturando a janela ou a tela (o Firefox
    // não captura aba), vêm junto a barra de endereço, as abas, a barra de
    // tarefas… Então a página se pinta de magenta por um instante e o
    // retângulo magenta achado no quadro vira a régua: origem e escala da
    // página na captura, valendo para qualquer superfície.
    const MAGENTA = (r, g, b) => r > 200 && g < 70 && b > 200;

    function caixaMagenta(dados, w, h) {
        let x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (let y = 0; y < h; y++) {
            for (let x = 0; x < w; x++) {
                const i = (y * w + x) * 4;
                if (!MAGENTA(dados[i], dados[i + 1], dados[i + 2])) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        }
        return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 };
    }

    async function calibrar(video) {
        const tinta = document.createElement('div');
        tinta.style.cssText = 'position:fixed;inset:0;z-index:2147483647;background:#ff00ff;pointer-events:none';
        document.body.appendChild(tinta);
        try {
            const c = document.createElement('canvas');
            const cx = c.getContext('2d', { willReadFrequently: true });
            const proporcao = window.innerWidth / window.innerHeight;
            for (let tentativa = 0; tentativa < 40; tentativa++) {
                await esperar(100);
                const vw = video.videoWidth, vh = video.videoHeight;
                if (!vw) continue;
                // Primeiro numa miniatura (barato), até o magenta aparecer na captura
                const ew = Math.min(1, 480 / vw);
                c.width = Math.round(vw * ew); c.height = Math.round(vh * ew);
                cx.drawImage(video, 0, 0, c.width, c.height);
                const m = caixaMagenta(cx.getImageData(0, 0, c.width, c.height).data, c.width, c.height);
                if (!m || m.w * m.h < c.width * c.height * 0.04) continue;
                if (Math.abs(m.w / m.h / proporcao - 1) > 0.12) continue; // não é a página (outro magenta)
                // Achou: mede de novo na resolução cheia para o recorte ficar no pixel
                c.width = vw; c.height = vh;
                cx.drawImage(video, 0, 0);
                const f = caixaMagenta(cx.getImageData(0, 0, vw, vh).data, vw, vh);
                if (!f) continue;
                return { x: f.x, y: f.y, ex: f.w / window.innerWidth, ey: f.h / window.innerHeight };
            }
            return null;
        } finally {
            tinta.remove();
        }
    }

    // Recorte da caixa da abertura dentro do quadro capturado. Sem régua
    // (calibração falhou numa captura de aba), supõe o quadro = a página,
    // reescalado e com tarja se a proporção não bater.
    function recorte(video, caixa, regua) {
        if (regua) {
            return {
                sx: regua.x + caixa.left * regua.ex, sy: regua.y + caixa.top * regua.ey,
                sw: caixa.width * regua.ex, sh: caixa.height * regua.ey,
            };
        }
        const vw = video.videoWidth, vh = video.videoHeight;
        const jw = window.innerWidth, jh = window.innerHeight;
        const esc = Math.min(vw / jw, vh / jh);
        const offX = (vw - jw * esc) / 2, offY = (vh - jh * esc) / 2;
        return {
            sx: offX + caixa.left * esc, sy: offY + caixa.top * esc,
            sw: caixa.width * esc, sh: caixa.height * esc,
        };
    }

    function nomeArquivo(ext) {
        const A = window.ANALISAR || {};
        const slug = s => (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '')
            .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
        const partes = ['escalacao', slug(A.timeCasaNome), 'x', slug(A.timeVisitanteNome)].filter(Boolean);
        return `${partes.join('-')}.${ext}`;
    }

    function baixar(blob, nome) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = nome;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }

    async function paraMp4(blob, ext) {
        const fd = new FormData();
        fd.append('arquivo', blob, `abertura.${ext}`);
        fd.append('fps', String(FPS));
        try {
            const resp = await fetch('/Jogadas/ConverterMp4', { method: 'POST', body: fd });
            if (resp.ok) return { blob: await resp.blob(), ext: 'mp4' };
            console.warn('ConverterMp4:', resp.status, await resp.text());
        } catch (e) {
            console.warn('ConverterMp4:', e);
        }
        return null;
    }

    function esperar(ms) { return new Promise(r => setTimeout(r, ms)); }

    async function gravarVideoAbertura() {
        if (gravando) return;
        if (!window.AberturaJogo) return;
        const tipo = tipoDeVideo();
        if (!tipo || !navigator.mediaDevices?.getDisplayMedia) {
            alert('Este navegador não grava a tela em vídeo. Use o Chrome, o Edge ou o Firefox.');
            return;
        }
        // O botão fica na barra da transmissão, mas a gravação também vale
        // chamada de fora: liga o modo (sem tocar a abertura, quem toca é aqui).
        // A abertura pousa no campo da transmissão, que só existe no layout
        // "Mesmo campo".
        const emTransmissao = () => document.getElementById('anUnico')?.classList.contains('an-transmissao');
        if (!emTransmissao()) window.toggleTransmissaoAnalise?.(true);
        if (!emTransmissao()) {
            alert('Para gravar, use o layout "Mesmo campo" e entre no 📺 Transmissão.');
            return;
        }

        let stream;
        try {
            stream = await navigator.mediaDevices.getDisplayMedia({
                video: { displaySurface: 'browser', frameRate: { ideal: FPS, max: 60 } },
                audio: false,
                preferCurrentTab: true,
                selfBrowserSurface: 'include',
                surfaceSwitching: 'exclude',
                monitorTypeSurfaces: 'exclude',
            });
        } catch (e) {
            if (e?.name !== 'NotAllowedError') alert('Não foi possível capturar a aba: ' + (e?.message || e));
            return;
        }

        const trilha = stream.getVideoTracks()[0];
        const ehAba = trilha?.getSettings?.().displaySurface === 'browser';

        gravando = true;
        botao('⏺ Gravando…');
        document.documentElement.classList.add('an-gravando-video');
        let cancelado = false;
        trilha.addEventListener('ended', () => { cancelado = true; window.AberturaJogo.pular(); });

        try {
            await carregarFonteMarca();

            const video = document.createElement('video');
            video.muted = true;
            video.playsInline = true;
            video.srcObject = stream;
            await video.play();
            // A faixa "compartilhando esta aba" do navegador encolhe a janela
            // logo depois do aceite: espera ela assentar antes de começar.
            await esperar(700);
            // Janela ou tela: a régua é obrigatória (sem ela o vídeo sairia com
            // a barra do navegador). Aba: se falhar, a conta do quadro inteiro vale.
            botao('⏺ Preparando…');
            const regua = await calibrar(video);
            if (!regua && !ehAba) {
                alert('Não achei esta página na captura. Compartilhe esta aba ou a janela do navegador '
                    + '(visível, sem nada por cima) e tente de novo.');
                return;
            }
            await esperar(150); // o magenta sai da captura antes do primeiro quadro
            botao('⏺ Gravando…');

            const canvas = document.createElement('canvas');
            canvas.width = LARGURA;
            canvas.height = ALTURA;
            const ctx = canvas.getContext('2d');
            ctx.imageSmoothingQuality = 'high';

            let root = null, caixa = null, parado = false, pretoAte = 0;
            function quadro() {
                if (parado) return;
                if (root?.isConnected) caixa = root.getBoundingClientRect();
                ctx.fillStyle = '#000';
                ctx.fillRect(0, 0, LARGURA, ALTURA);
                if (caixa && video.videoWidth && performance.now() >= pretoAte) {
                    const r = recorte(video, caixa, regua);
                    ctx.drawImage(video, r.sx, r.sy, r.sw, r.sh, 0, 0, LARGURA, ALTURA);
                }
                desenharMarca(ctx);
                // setTimeout e não rAF: o rAF para com a aba em segundo plano e a
                // gravação ficaria pendurada (mesmo motivo do js/jogadas.js).
                setTimeout(quadro, 1000 / FPS);
            }

            const pedacos = [];
            const rec = new MediaRecorder(canvas.captureStream(FPS),
                { mimeType: tipo.mime, videoBitsPerSecond: BITS_POR_SEGUNDO });
            rec.ondataavailable = e => { if (e.data?.size) pedacos.push(e.data); };
            const parou = new Promise(res => { rec.onstop = res; });

            const fimAbertura = window.AberturaJogo.tocar({
                gravacao: true,
                aoIniciar: el => { root = el; caixa = el.getBoundingClientRect(); },
            });
            // A captura chega atrasada (bem mais na de janela que na de aba):
            // os primeiros quadros ainda mostram a página sem a abertura por
            // cima. O vídeo abre com um instante de preto no lugar deles.
            pretoAte = performance.now() + 350;
            quadro();
            rec.start(1000);

            const { pulou } = await fimAbertura;
            if (!pulou && !cancelado) await esperar(CAUDA_MS);
            parado = true;
            rec.stop();
            await parou;
            stream.getTracks().forEach(t => t.stop());
            document.documentElement.classList.remove('an-gravando-video');

            if (pulou || cancelado) {
                botao('🎥 Gravar vídeo');
                return;
            }

            botao('⏳ Convertendo…');
            const bruto = new Blob(pedacos, { type: tipo.mime });
            const mp4 = await paraMp4(bruto, tipo.ext);
            if (mp4) baixar(mp4.blob, nomeArquivo('mp4'));
            else {
                baixar(bruto, nomeArquivo(tipo.ext));
                alert(tipo.ext === 'mp4'
                    ? 'O servidor não converteu o vídeo: baixei o MP4 gravado pelo navegador (pode ter taxa de quadros variável).'
                    : 'O servidor não converteu o vídeo (sem ffmpeg?): o arquivo saiu em WebM, que o X não aceita.');
            }
        } catch (e) {
            console.error(e);
            alert('A gravação falhou: ' + (e?.message || e));
        } finally {
            stream.getTracks().forEach(t => t.stop());
            document.documentElement.classList.remove('an-gravando-video');
            gravando = false;
            botao('🎥 Gravar vídeo');
        }
    }

    window.gravarVideoAbertura = gravarVideoAbertura;
})();
