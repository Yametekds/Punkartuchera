// Lector de QR con la cámara. Usa BarcodeDetector si el navegador lo tiene
// (Chrome en Android) y si no, jsQR, que se carga solo cuando hace falta.

let session = null;

function loadJsQr() {
    if (window.jsQR) return Promise.resolve(window.jsQR);
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = new URL('./vendor/jsQR.js', import.meta.url).href;
        script.onload = () => resolve(window.jsQR);
        script.onerror = () => reject(new Error('jsQR'));
        document.head.appendChild(script);
    });
}

async function createReader() {
    if ('BarcodeDetector' in window) {
        try {
            const formats = await window.BarcodeDetector.getSupportedFormats();
            if (formats.includes('qr_code')) {
                const detector = new window.BarcodeDetector({ formats: ['qr_code'] });
                return async video => (await detector.detect(video))[0]?.rawValue ?? null;
            }
        } catch {
            // Sigue con jsQR.
        }
    }

    const jsQR = await loadJsQr();
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d', { willReadFrequently: true });
    return async video => {
        const scale = Math.min(1, 800 / Math.max(video.videoWidth, video.videoHeight));
        canvas.width = Math.round(video.videoWidth * scale);
        canvas.height = Math.round(video.videoHeight * scale);
        context.drawImage(video, 0, 0, canvas.width, canvas.height);
        const image = context.getImageData(0, 0, canvas.width, canvas.height);
        return jsQR(image.data, image.width, image.height, { inversionAttempts: 'attemptBoth' })?.data ?? null;
    };
}

// Devuelve "ok", "denied", "nocamera" o "error". Cada lectura se pasa a .NET con OnCode,
// que responde si hay que seguir escaneando.
export async function start(video, dotnet) {
    stop();
    if (!navigator.mediaDevices?.getUserMedia) return 'nocamera';

    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 1280 } },
            audio: false,
        });
    } catch (error) {
        if (error?.name === 'NotAllowedError' || error?.name === 'SecurityError') return 'denied';
        if (error?.name === 'NotFoundError' || error?.name === 'OverconstrainedError') return 'nocamera';
        return 'error';
    }

    const current = { stream, stopped: false, timer: 0 };
    session = current;
    video.srcObject = stream;
    video.setAttribute('playsinline', '');
    video.muted = true;
    try {
        await video.play();
    } catch {
        // Algunos navegadores tardan en arrancar; el bucle espera a que haya imagen.
    }

    let read;
    try {
        read = await createReader();
    } catch {
        stop();
        return 'error';
    }

    let last = null;
    const tick = async () => {
        if (current.stopped) return;
        let keepGoing = true;
        try {
            if (video.readyState >= 2 && video.videoWidth > 0) {
                const text = await read(video);
                if (text && text !== last) {
                    last = text;
                    keepGoing = await dotnet.invokeMethodAsync('OnCode', text);
                }
            }
        } catch {
            // Fotograma ilegible: se prueba con el siguiente.
        }

        if (!keepGoing) {
            stop();
        } else if (!current.stopped) {
            current.timer = setTimeout(tick, 180);
        }
    };
    tick();
    return 'ok';
}

export function stop() {
    if (!session) return;
    session.stopped = true;
    clearTimeout(session.timer);
    session.stream.getTracks().forEach(track => track.stop());
    session = null;
}
