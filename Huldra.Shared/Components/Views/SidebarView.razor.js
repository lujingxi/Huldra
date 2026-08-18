export function checkOverflow(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return false;
    return el.scrollHeight > el.clientHeight;
}

export function scrollByAmount(elementId, amount) {
    const el = document.getElementById(elementId);
    if (el) el.scrollBy({ top: amount, behavior: 'smooth' });
}
