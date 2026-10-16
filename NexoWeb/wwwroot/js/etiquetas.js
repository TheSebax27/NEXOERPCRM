window.nexoDescargarBlob = async function (url, filename) {
    try {
        const resp = await fetch(url);
        const blob = await resp.blob();
        const objUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = objUrl;
        a.download = filename || 'qr.png';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(objUrl), 1000);
    } catch (e) {
        console.error('Download failed:', e);
    }
};

window.nexoEtiqueta = {
    renderBarcode: function (svgId, valor, options) {
        var el = document.getElementById(svgId);
        if (!el || !valor) return;
        try {
            JsBarcode(el, valor, Object.assign({
                format: "CODE128",
                width: 1.5,
                height: 40,
                displayValue: true,
                fontSize: 11,
                margin: 4,
                background: "transparent"
            }, options || {}));
        } catch (e) {
            console.warn("JsBarcode error:", e);
        }
    },
    renderAllBarcodes: function (prefix, valor, count, options) {
        for (var i = 0; i < count; i++) {
            this.renderBarcode(prefix + i, valor, options);
        }
    },
    print: function () {
        var area = document.getElementById('etq-print-area');
        if (!area) { window.print(); return; }

        var win = window.open('', '_blank', 'width=900,height=700');
        if (!win) { window.print(); return; }

        var html = [
            '<!DOCTYPE html><html><head><meta charset="utf-8">',
            '<base href="' + window.location.origin + '/">',
            '<title>Etiquetas NEXO</title>',
            '<style>',
            '* { box-sizing: border-box; margin: 0; padding: 0; }',
            'body { background: #fff; font-family: "Plus Jakarta Sans", Arial, sans-serif; }',
            '.etq-labels-grid { display: flex; flex-wrap: wrap; gap: 3mm; padding: 4mm; }',
            '.etq-label { background: #fff; border: 1px solid #999; border-radius: 2px;',
            '  display: flex; flex-direction: column; justify-content: space-between;',
            '  padding: 1.5mm 2mm; overflow: hidden; color: #111;',
            '  page-break-inside: avoid; break-inside: avoid; }',
            '.etq-label.etq-pequeña { width: 50mm; height: 25mm; }',
            '.etq-label.etq-mediana { width: 70mm; height: 40mm; }',
            '.etq-label.etq-grande  { width: 100mm; height: 60mm; }',
            '.etq-nombre { font-weight: 700; line-height: 1.2; overflow: hidden;',
            '  display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }',
            '.etq-pequeña .etq-nombre { font-size: 6.5pt; }',
            '.etq-mediana .etq-nombre { font-size: 8pt; }',
            '.etq-grande  .etq-nombre { font-size: 10pt; }',
            '.etq-ref { font-size: 6pt; opacity: .6; }',
            '.etq-mediana .etq-ref { font-size: 7pt; }',
            '.etq-grande  .etq-ref { font-size: 8.5pt; }',
            '.etq-precio { font-weight: 800; color: #1565C0; }',
            '.etq-pequeña .etq-precio { font-size: 7.5pt; }',
            '.etq-mediana .etq-precio { font-size: 9.5pt; }',
            '.etq-grande  .etq-precio { font-size: 12pt; }',
            '.etq-extra { font-size: 5.5pt; opacity: .55; white-space: nowrap;',
            '  overflow: hidden; text-overflow: ellipsis; }',
            '.etq-codes-row { display: flex; align-items: flex-end; gap: 2px; margin-top: auto; }',
            '.etq-barcode-wrap { flex: 1; overflow: hidden; }',
            '.etq-barcode-wrap svg { max-width: 100%; height: auto; display: block; }',
            '.etq-pequeña .etq-barcode-wrap svg { max-height: 9mm; }',
            '.etq-mediana .etq-barcode-wrap svg { max-height: 13mm; }',
            '.etq-grande  .etq-barcode-wrap svg { max-height: 18mm; }',
            '.etq-qr-wrap { flex-shrink: 0; }',
            '.etq-qr-img { display: block; image-rendering: pixelated; }',
            '.etq-pequeña .etq-qr-img { width: 10mm; height: 10mm; }',
            '.etq-mediana .etq-qr-img { width: 14mm; height: 14mm; }',
            '.etq-grande  .etq-qr-img { width: 18mm; height: 18mm; }',
            '@page { margin: 5mm; size: auto; }',
            '</style></head><body>',
            area.outerHTML,
            '</body></html>'
        ].join('\n');

        win.document.write(html);
        win.document.close();
        win.focus();
        setTimeout(function () { win.print(); win.close(); }, 500);
    }
};
