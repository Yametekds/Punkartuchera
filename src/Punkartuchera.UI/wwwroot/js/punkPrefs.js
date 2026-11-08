// Perfil del jugador (nickname, tamaño de letra y tipografía) en localStorage.
// Se usa localStorage y no IndexedDB porque el index.html lo lee de forma síncrona al arrancar,
// así la letra elegida ya está puesta antes de que se pinte nada.

const KEY = 'punkartuchera.user';

export function load() {
    try {
        return localStorage.getItem(KEY);
    } catch {
        return null;
    }
}

export function save(json) {
    try {
        localStorage.setItem(KEY, json);
        return true;
    } catch {
        return false;
    }
}

// Aplica el tamaño (porcentaje) y la tipografía a toda la página.
export function apply(scale, font) {
    const root = document.documentElement;
    root.style.fontSize = scale === 100 ? '' : scale + '%';
    if (font && font !== 'punk') {
        root.dataset.font = font;
    } else {
        delete root.dataset.font;
    }
}
