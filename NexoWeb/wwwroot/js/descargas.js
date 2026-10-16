// Inserta texto en la posicion del cursor de un textarea por su id HTML
window.nexoInsertarEnCursor = function (inputId, texto) {
    const el = document.getElementById(inputId);
    if (!el) return;
    const start = el.selectionStart ?? el.value.length;
    const end   = el.selectionEnd   ?? el.value.length;
    el.value = el.value.substring(0, start) + texto + el.value.substring(end);
    el.selectionStart = el.selectionEnd = start + texto.length;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.focus();
};

// Descarga un archivo a partir de un string base64 (usado para archivos de texto
// generados en el servidor como appsettings.json).
window.nexoDescargarArchivo = (nombreArchivo, tipoContenido, base64) => {
    // Deteccion: si es un DotNetStreamReference (objeto) usamos arrayBuffer;
    // si es string, asumimos base64.
    if (typeof base64 === 'string') {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const blob = new Blob([bytes], { type: tipoContenido });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = nombreArchivo;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
        return;
    }
    // Fallback legacy: streamRef
    base64.arrayBuffer().then(buffer => {
        const blob = new Blob([buffer], { type: tipoContenido });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = nombreArchivo;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    });
};

// Navega el navegador a una URL de descarga directa (el servidor envía Content-Disposition: attachment).
window.nexoDescargarUrl = (url) => { window.location.href = url; };

// Scroll del chat al fondo
window.nexoChatScroll = () => {
    const el = document.getElementById('nexo-chat-scroll');
    if (el) el.scrollTop = el.scrollHeight;
};

// Abre un PDF base64 en una pestaña nueva para imprimir.
window.nexoImprimirPdf = (base64) => {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob  = new Blob([bytes], { type: 'application/pdf' });
    const url   = URL.createObjectURL(blob);
    window.open(url, '_blank');
    setTimeout(() => URL.revokeObjectURL(url), 120000);
};

// Version legacy (stream) — mantenida para compatibilidad con otras pantallas.
window.nexoDescargarArchivoStream = async (nombreArchivo, tipoContenido, streamRef) => {
    const arrayBuffer = await streamRef.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: tipoContenido });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = nombreArchivo;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(url);
};
