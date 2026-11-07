// Encuadre cuadrado de fotos: decodifica la imagen ya reducida, deja moverla y
// acercarla con el dedo, la rueda o el control deslizante, y exporta un JPEG de
// tamaño fijo. Da igual lo que pese la foto original.

const WORK_SIDE = 2048;   // lado máximo de la copia con la que se trabaja
const OUTPUT_SIDE = 600;  // lado de la foto guardada
const OUTPUT_QUALITY = 0.85;
const MAX_ZOOM = 6;

let session = null;

function loadImage(file) {
    return new Promise((resolve, reject) => {
        const url = URL.createObjectURL(file);
        const img = new Image();
        img.onload = () => resolve({ img, url });
        img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('decode')); };
        img.src = url;
    });
}

// Devuelve un lienzo o bitmap de como mucho WORK_SIDE de lado, con la orientación EXIF aplicada.
async function decode(file) {
    const { img, url } = await loadImage(file);
    try {
        const w = img.naturalWidth, h = img.naturalHeight;
        if (!w || !h) throw new Error('decode');
        const ratio = Math.min(1, WORK_SIDE / Math.max(w, h));
        const tw = Math.max(1, Math.round(w * ratio)), th = Math.max(1, Math.round(h * ratio));

        // createImageBitmap reduce al decodificar y no necesita la foto entera en memoria.
        if (window.createImageBitmap) {
            try {
                const bitmap = await createImageBitmap(file, {
                    resizeWidth: tw, resizeHeight: th, resizeQuality: 'high', imageOrientation: 'from-image'
                });
                // Algunos navegadores aplican el tamaño antes de girar: si sale deformada, se descarta.
                if (Math.abs(bitmap.width / bitmap.height - w / h) < 0.02) return bitmap;
                bitmap.close();
            } catch { /* se intenta con el lienzo */ }
        }

        const canvas = document.createElement('canvas');
        canvas.width = tw;
        canvas.height = th;
        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingQuality = 'high';
        ctx.drawImage(img, 0, 0, tw, th);
        return canvas;
    } finally {
        URL.revokeObjectURL(url);
    }
}

function release() {
    if (!session) return;
    session.detach?.();
    session.image.close?.();
    session = null;
}

// Lee la foto elegida en el <input type="file">. Devuelve { status: 'ok' | 'empty' | 'error', ... }.
export async function load(input) {
    const file = input.files && input.files[0];
    if (!file) return { status: 'empty' };
    input.value = ''; // para poder volver a elegir la misma foto
    release();
    try {
        const image = await decode(file);
        session = { image, width: image.width, height: image.height, cx: image.width / 2, cy: image.height / 2, zoom: 1 };
        return { status: 'ok' };
    } catch {
        return { status: 'error', type: file.type || '', name: file.name || '', size: file.size || 0 };
    }
}

function geometry(side) {
    const s = session;
    const cover = side / Math.min(s.width, s.height);
    const scale = cover * s.zoom;
    const half = side / 2 / scale;
    // El centro no puede salirse tanto como para dejar huecos en el cuadro.
    s.cx = Math.min(Math.max(s.cx, half), s.width - half);
    s.cy = Math.min(Math.max(s.cy, half), s.height - half);
    return { scale, sx: s.cx - half, sy: s.cy - half, size: half * 2 };
}

// Conecta el lienzo y el deslizador del editor a la foto cargada.
export function attach(canvas, range) {
    if (!session) return;
    const s = session;
    const ctx = canvas.getContext('2d');
    const pointers = new Map();
    let pinch = null;

    const side = () => canvas.clientWidth || 1;

    const draw = () => {
        const dpr = window.devicePixelRatio || 1;
        const px = Math.round(side() * dpr);
        if (canvas.width !== px) { canvas.width = px; canvas.height = px; }
        const g = geometry(side());
        ctx.imageSmoothingQuality = 'high';
        ctx.clearRect(0, 0, px, px);
        ctx.drawImage(s.image, g.sx, g.sy, g.size, g.size, 0, 0, px, px);
        if (range) range.value = String(s.zoom);
    };

    // Acerca manteniendo quieto el punto (x, y) del cuadro, en píxeles de pantalla.
    const zoomAt = (zoom, x, y) => {
        const before = geometry(side());
        const ix = before.sx + x / before.scale, iy = before.sy + y / before.scale;
        s.zoom = Math.min(Math.max(zoom, 1), MAX_ZOOM);
        const scale = side() / Math.min(s.width, s.height) * s.zoom;
        s.cx = ix - x / scale + side() / 2 / scale;
        s.cy = iy - y / scale + side() / 2 / scale;
        draw();
    };

    const local = e => {
        const r = canvas.getBoundingClientRect();
        return { x: e.clientX - r.left, y: e.clientY - r.top };
    };

    const pinchState = () => {
        const [a, b] = [...pointers.values()];
        return { dist: Math.hypot(a.x - b.x, a.y - b.y) || 1, x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    };

    const onDown = e => {
        canvas.setPointerCapture(e.pointerId);
        pointers.set(e.pointerId, local(e));
        if (pointers.size === 2) pinch = { ...pinchState(), zoom: s.zoom };
    };

    const onMove = e => {
        if (!pointers.has(e.pointerId)) return;
        const prev = pointers.get(e.pointerId);
        const now = local(e);
        pointers.set(e.pointerId, now);
        if (pointers.size >= 2 && pinch) {
            const p = pinchState();
            const g = geometry(side());
            s.cx -= (p.x - pinch.x) / g.scale;
            s.cy -= (p.y - pinch.y) / g.scale;
            pinch.x = p.x;
            pinch.y = p.y;
            zoomAt(pinch.zoom * p.dist / pinch.dist, p.x, p.y);
        } else if (pointers.size === 1) {
            const g = geometry(side());
            s.cx -= (now.x - prev.x) / g.scale;
            s.cy -= (now.y - prev.y) / g.scale;
            draw();
        }
    };

    const onUp = e => {
        pointers.delete(e.pointerId);
        if (pointers.size < 2) pinch = null;
    };

    const onWheel = e => {
        e.preventDefault();
        const p = local(e);
        zoomAt(s.zoom * Math.exp(-e.deltaY / 400), p.x, p.y);
    };

    const onRange = () => zoomAt(parseFloat(range.value), side() / 2, side() / 2);

    canvas.addEventListener('pointerdown', onDown);
    canvas.addEventListener('pointermove', onMove);
    canvas.addEventListener('pointerup', onUp);
    canvas.addEventListener('pointercancel', onUp);
    canvas.addEventListener('wheel', onWheel, { passive: false });
    range?.addEventListener('input', onRange);
    const observer = new ResizeObserver(draw);
    observer.observe(canvas);
    if (range) { range.min = '1'; range.max = String(MAX_ZOOM); range.step = '0.01'; }

    s.detach = () => {
        observer.disconnect();
        canvas.removeEventListener('pointerdown', onDown);
        canvas.removeEventListener('pointermove', onMove);
        canvas.removeEventListener('pointerup', onUp);
        canvas.removeEventListener('pointercancel', onUp);
        canvas.removeEventListener('wheel', onWheel);
        range?.removeEventListener('input', onRange);
    };
    draw();
}

// Recorta el cuadro visible y lo devuelve como JPEG de OUTPUT_SIDE de lado.
export async function exportJpeg() {
    if (!session) throw new Error('No hay foto cargada.');
    const g = geometry(Math.min(session.width, session.height) / session.zoom);
    const out = document.createElement('canvas');
    out.width = OUTPUT_SIDE;
    out.height = OUTPUT_SIDE;
    const ctx = out.getContext('2d');
    ctx.imageSmoothingQuality = 'high';
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, OUTPUT_SIDE, OUTPUT_SIDE); // las PNG con transparencia quedan sobre negro
    ctx.drawImage(session.image, g.sx, g.sy, g.size, g.size, 0, 0, OUTPUT_SIDE, OUTPUT_SIDE);
    const blob = await new Promise(resolve => out.toBlob(resolve, 'image/jpeg', OUTPUT_QUALITY));
    if (!blob) throw new Error('No se pudo generar la foto.');
    release();
    return new Uint8Array(await blob.arrayBuffer());
}

// Devuelve la foto entera, sin recortar, como JPEG con el lado mayor en maxSide como mucho.
// Para las fotos de la mesa, donde importa ver todo.
export async function exportWhole(maxSide, quality) {
    if (!session) throw new Error('No hay foto cargada.');
    const ratio = Math.min(1, maxSide / Math.max(session.width, session.height));
    const out = document.createElement('canvas');
    out.width = Math.max(1, Math.round(session.width * ratio));
    out.height = Math.max(1, Math.round(session.height * ratio));
    const ctx = out.getContext('2d');
    ctx.imageSmoothingQuality = 'high';
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, out.width, out.height);
    ctx.drawImage(session.image, 0, 0, out.width, out.height);
    const blob = await new Promise(resolve => out.toBlob(resolve, 'image/jpeg', quality));
    if (!blob) throw new Error('No se pudo generar la foto.');
    release();
    return new Uint8Array(await blob.arrayBuffer());
}

export function cancel() {
    release();
}
