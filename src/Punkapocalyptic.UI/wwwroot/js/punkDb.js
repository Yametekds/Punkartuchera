// Almacén clave-valor de texto sobre IndexedDB.
// Cada almacén guarda registros { key, value } donde value es siempre un string.

const DB_NAME = 'punkapocalyptic';
const DB_VERSION = 1;
const STORES = ['bands', 'photos'];

let dbPromise;

function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            for (const name of STORES) {
                if (!db.objectStoreNames.contains(name)) {
                    db.createObjectStore(name, { keyPath: 'key' });
                }
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function db() {
    dbPromise ??= openDb();
    return dbPromise;
}

async function run(store, mode, action) {
    const database = await db();
    return new Promise((resolve, reject) => {
        const tx = database.transaction(store, mode);
        const request = action(tx.objectStore(store));
        tx.oncomplete = () => resolve(request.result);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

export async function getAll(store) {
    const records = await run(store, 'readonly', s => s.getAll());
    return records.map(r => r.value);
}

export async function get(store, key) {
    const record = await run(store, 'readonly', s => s.get(key));
    return record ? record.value : null;
}

export async function put(store, key, value) {
    await run(store, 'readwrite', s => s.put({ key, value }));
}

export async function remove(store, key) {
    await run(store, 'readwrite', s => s.delete(key));
}

// Pide al navegador que no borre los datos cuando falte espacio.
export async function requestPersistence() {
    if (navigator.storage && navigator.storage.persist) {
        return await navigator.storage.persist();
    }
    return false;
}
