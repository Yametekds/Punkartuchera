// Comparte un archivo con el menú del sistema (Web Share API) o lo descarga si no se puede.
// Algunos navegadores solo dejan compartir ciertos tipos de archivo, así que se prueban
// variantes antes de rendirse.

function candidates(fileName, bytes) {
    const plain = fileName.replace(/\.[^.]+$/, '');
    return [
        new File([bytes], fileName, { type: 'application/json' }),
        new File([bytes], fileName, { type: 'text/plain' }),
        new File([bytes], plain + '.punkband.txt', { type: 'text/plain' }),
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
