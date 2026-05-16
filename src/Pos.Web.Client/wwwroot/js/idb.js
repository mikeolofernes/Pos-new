// Minimal IndexedDB helper for offline catalog + outbox.
// Exposes: put(store, key, value), get(store, key), getAll(store), del(store, key), clear(store)

const DB_NAME = 'pos-db';
const DB_VERSION = 1;
const STORES = ['catalog', 'outbox', 'receipts', 'meta'];

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            for (const s of STORES) {
                if (!db.objectStoreNames.contains(s)) db.createObjectStore(s);
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

async function withStore(store, mode, fn) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(store, mode);
        const os = tx.objectStore(store);
        const result = fn(os);
        tx.oncomplete = () => resolve(result.value);
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

export async function put(store, key, value) {
    return withStore(store, 'readwrite', os => ({ value: os.put(value, key) }));
}

export async function get(store, key) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const req = db.transaction(store).objectStore(store).get(key);
        req.onsuccess = () => resolve(req.result ?? null);
        req.onerror = () => reject(req.error);
    });
}

export async function getAll(store) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const req = db.transaction(store).objectStore(store).getAll();
        req.onsuccess = () => resolve(req.result ?? []);
        req.onerror = () => reject(req.error);
    });
}

export async function del(store, key) {
    return withStore(store, 'readwrite', os => ({ value: os.delete(key) }));
}

export async function clear(store) {
    return withStore(store, 'readwrite', os => ({ value: os.clear() }));
}
