// Abre un <dialog> como modal y avisa a .NET cuando hay que cerrarlo (Escape o toque fuera).
// Quien lo quita del DOM es Blazor; al cerrar, el foco vuelve a donde estaba.

export function open(dialog, dotnet) {
    const opener = document.activeElement;
    const requestClose = () => dotnet.invokeMethodAsync('RequestClose');

    const onCancel = e => {
        e.preventDefault();
        requestClose();
    };

    // Un toque en el fondo llega al propio <dialog>, pero fuera de su caja.
    const onClick = e => {
        if (e.target !== dialog) {
            return;
        }
        const box = dialog.getBoundingClientRect();
        if (e.clientX < box.left || e.clientX > box.right || e.clientY < box.top || e.clientY > box.bottom) {
            requestClose();
        }
    };

    dialog.addEventListener('cancel', onCancel);
    dialog.addEventListener('click', onClick);
    if (!dialog.open) {
        dialog.showModal();
    }
    // El foco empieza en la propia hoja, no en el aspa: así no aparece marcada al abrir.
    dialog.focus({ preventScroll: true });

    return {
        close() {
            dialog.removeEventListener('cancel', onCancel);
            dialog.removeEventListener('click', onClick);
            if (dialog.open) {
                dialog.close();
            }
            if (opener instanceof HTMLElement && opener.isConnected) {
                opener.focus({ preventScroll: true });
            }
        },
    };
}
