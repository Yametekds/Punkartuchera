// Sacudir el móvil para tirar el dado (DeviceMotion).
// En iPhone el permiso solo se puede pedir dentro de un toque, así que el interruptor
// se engancha aquí con un listener nativo y no desde Blazor.
const key = 'punk.diceShake';
const threshold = 14;      // m/s² de cambio entre muestras para contar un meneo
const sampleMs = 60;
const windowMs = 800;      // meneos que cuentan juntos
const hitsNeeded = 3;
const cooldownMs = 1500;

let dotnet = null;
let listening = false;
let prev = null;
let lastSample = 0;
let hits = [];
let lastShake = 0;
let toggle = null;

function store(on) {
    try { localStorage.setItem(key, on ? '1' : '0'); } catch { }
}

function stored() {
    try { return localStorage.getItem(key) === '1'; } catch { return false; }
}

function supported() {
    return typeof window !== 'undefined' && 'DeviceMotionEvent' in window;
}

function needsPermission() {
    return supported() && typeof DeviceMotionEvent.requestPermission === 'function';
}

function onMotion(e) {
    const a = e.accelerationIncludingGravity;
    if (!a || a.x == null || document.visibilityState !== 'visible') return;
    const now = performance.now();
    if (now - lastSample < sampleMs) return;
    lastSample = now;
    if (prev) {
        const delta = Math.max(Math.abs(a.x - prev.x), Math.abs(a.y - prev.y), Math.abs(a.z - prev.z));
        if (delta > threshold) hits.push(now);
    }
    prev = { x: a.x, y: a.y, z: a.z };
    hits = hits.filter(t => now - t < windowMs);
    if (hits.length >= hitsNeeded && now - lastShake > cooldownMs) {
        lastShake = now;
        hits = [];
        dotnet?.invokeMethodAsync('OnShake').catch(() => { });
    }
}

function listen(on) {
    if (on === listening) return;
    listening = on;
    prev = null;
    hits = [];
    if (on) window.addEventListener('devicemotion', onMotion);
    else window.removeEventListener('devicemotion', onMotion);
}

function report(state) {
    dotnet?.invokeMethodAsync('OnShakeState', state).catch(() => { });
}

async function turnOn() {
    if (!supported()) return 'unsupported';
    if (needsPermission()) {
        try {
            if (await DeviceMotionEvent.requestPermission() !== 'granted') return 'denied';
        } catch {
            return 'denied';
        }
    }
    listen(true);
    store(true);
    return 'on';
}

function onToggleClick() {
    if (listening) {
        listen(false);
        store(false);
        report('off');
        return;
    }
    // La petición de permiso sale en este mismo toque.
    turnOn().then(report);
}

// Al abrir la partida: devuelve 'on', 'off', 'tap' (iPhone: hay que volver a tocar el
// interruptor para dar permiso) o 'unsupported'.
export async function init(ref) {
    dotnet = ref;
    if (!supported()) return 'unsupported';
    if (!stored()) return 'off';
    if (!needsPermission()) {
        listen(true);
        return 'on';
    }
    try {
        // Si ya se dio permiso en esta sesión responde sin preguntar; si no, falla.
        if (await DeviceMotionEvent.requestPermission() === 'granted') {
            listen(true);
            return 'on';
        }
    } catch { }
    return 'tap';
}

export function bindToggle(element) {
    if (toggle === element) return;
    toggle?.removeEventListener('click', onToggleClick);
    toggle = element;
    toggle?.addEventListener('click', onToggleClick);
}

export function buzz() {
    try { navigator.vibrate?.([30, 40, 60]); } catch { }
}

export function leave() {
    listen(false);
    toggle?.removeEventListener('click', onToggleClick);
    toggle = null;
    dotnet = null;
}
