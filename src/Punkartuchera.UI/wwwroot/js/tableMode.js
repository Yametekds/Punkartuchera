// Modo mesa de la partida: letra y botones grandes (clase en <body>) y pantalla
// siempre encendida con la Screen Wake Lock API. El navegador suelta el bloqueo
// al cambiar de pestaña, así que se vuelve a pedir al volver.
const key = 'punk.tableMode';
let enabled = false;
let lock = null;

async function acquire() {
    if (!('wakeLock' in navigator) || document.visibilityState !== 'visible' || lock) return;
    try {
        lock = await navigator.wakeLock.request('screen');
        lock.addEventListener('release', () => { lock = null; });
    } catch {
        lock = null;
    }
}

function onVisibility() {
    if (enabled) acquire();
}

export function stored() {
    try { return localStorage.getItem(key) === '1'; } catch { return false; }
}

// Devuelve si el navegador permite mantener la pantalla encendida.
export async function set(on) {
    enabled = on;
    document.body.classList.toggle('table-mode', on);
    try { localStorage.setItem(key, on ? '1' : '0'); } catch { }
    if (on) {
        document.addEventListener('visibilitychange', onVisibility);
        await acquire();
    } else {
        leave(false);
    }
    return 'wakeLock' in navigator;
}

// Al salir de la página de partida se quita todo, pero se recuerda la preferencia.
export function leave() {
    enabled = false;
    document.body.classList.remove('table-mode');
    document.removeEventListener('visibilitychange', onVisibility);
    if (lock) {
        lock.release().catch(() => { });
        lock = null;
    }
}
