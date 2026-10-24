// Desplaza la vista hasta el ancla de la URL una vez pintada la página de reglas.
export function scrollToFragment(id) {
    if (!id) return;
    const target = document.getElementById(decodeURIComponent(id));
    if (!target) return;
    target.scrollIntoView({ block: 'start' });
    target.classList.remove('is-target');
    void target.offsetWidth;
    target.classList.add('is-target');
}
