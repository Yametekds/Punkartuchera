// Comparte un archivo con el menú del sistema (Web Share API) o lo descarga si no se puede.
// Chrome solo deja compartir archivos con ciertas extensiones y tipos (.txt/text/plain sí;
// .punkband o .json no) y rechaza el resto al llamar a share(), cuando ya ha gastado el gesto
// del usuario. Por eso la variante en texto plano va primero: es la que abre WhatsApp, Gmail…

function candidates(fileName, bytes) {
    return [
        new File([bytes], fileName + '.txt', { type: 'text/plain' }),
        new File([bytes], fileName, { type: 'text/plain' }),
        new File([bytes], fileName, { type: 'application/json' }),
    ];
}

function download(fileName, bytes) {
    const url = URL.createObjectURL(new Blob([bytes], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 30000);
}

// Devuelve "shared", "downloaded" o "cancelled".
export async function shareFile(fileName, bytes, title, text) {
    if (navigator.share && navigator.canShare) {
        const file = candidates(fileName, bytes).find(f => {
            try { return navigator.canShare({ files: [f] }); } catch { return false; }
        });
        if (file) {
            try {
                await navigator.share({ files: [file], title, text });
                return 'shared';
            } catch (error) {
                if (error && error.name === 'AbortError') return 'cancelled';
                // NotAllowedError y similares: se cae a la descarga.
            }
        }
    }

    download(fileName, bytes);
    return 'downloaded';
}
