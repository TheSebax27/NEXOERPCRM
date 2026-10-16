// signature_pad interop — carga signature_pad desde CDN y expone métodos a Blazor
(function () {
    const pads = {};
    const imgData = {}; // canvasId -> { img: Image, ratio: number }

    window.signaturePadInterop = {

        // ---- canvas de dibujo (signature_pad) ----

        init: function (canvasId) {
            const canvas = document.getElementById(canvasId);
            if (!canvas || !window.SignaturePad) return;
            const ratio = Math.max(window.devicePixelRatio || 1, 1);
            canvas.width = canvas.offsetWidth * ratio;
            canvas.height = canvas.offsetHeight * ratio;
            const ctx = canvas.getContext('2d');
            if (ctx) ctx.scale(ratio, ratio);
            pads[canvasId] = new SignaturePad(canvas, {
                penColor: '#1A1A2E',
                backgroundColor: 'rgba(255,255,255,0)'
            });
        },
        clear: function (canvasId) {
            if (pads[canvasId]) pads[canvasId].clear();
        },
        isEmpty: function (canvasId) {
            return pads[canvasId] ? pads[canvasId].isEmpty() : true;
        },
        toDataURL: function (canvasId) {
            const pad = pads[canvasId];
            if (!pad || pad.isEmpty()) return null;
            return pad.toDataURL('image/png');
        },
        destroy: function (canvasId) {
            if (pads[canvasId]) {
                pads[canvasId].off();
                delete pads[canvasId];
            }
        },

        // ---- canvas de imagen subida ----

        clickElement: function (id) {
            const el = document.getElementById(id);
            if (el) el.click();
        },

        initImageCanvas: function (canvasId) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return;
            const ratio = Math.max(window.devicePixelRatio || 1, 1);
            canvas.width = canvas.offsetWidth * ratio;
            canvas.height = canvas.offsetHeight * ratio;
            imgData[canvasId] = { img: null, ratio: ratio };
        },

        loadImage: function (canvasId, dataUrl, scale, threshold) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return;
            if (!imgData[canvasId]) {
                const ratio = Math.max(window.devicePixelRatio || 1, 1);
                canvas.width = canvas.offsetWidth * ratio;
                canvas.height = canvas.offsetHeight * ratio;
                imgData[canvasId] = { img: null, ratio: ratio };
            }
            const img = new Image();
            const self = this;
            img.onload = function () {
                imgData[canvasId].img = img;
                self._redrawImage(canvasId, scale, threshold);
            };
            img.src = dataUrl;
        },

        redrawImage: function (canvasId, scale, threshold) {
            this._redrawImage(canvasId, scale, threshold);
        },

        _redrawImage: function (canvasId, scale, threshold) {
            const canvas = document.getElementById(canvasId);
            const entry = imgData[canvasId];
            if (!canvas || !entry || !entry.img) return;

            const ctx = canvas.getContext('2d');
            const cw = canvas.width;
            const ch = canvas.height;
            const img = entry.img;

            ctx.clearRect(0, 0, cw, ch);

            const baseScale = Math.min(cw / img.naturalWidth, ch / img.naturalHeight);
            const finalScale = baseScale * scale;
            const w = img.naturalWidth * finalScale;
            const h = img.naturalHeight * finalScale;
            const x = (cw - w) / 2;
            const y = (ch - h) / 2;

            ctx.drawImage(img, x, y, w, h);

            if (threshold > 0) {
                const imageData = ctx.getImageData(0, 0, cw, ch);
                const d = imageData.data;
                for (let i = 0; i < d.length; i += 4) {
                    if (d[i] > threshold && d[i + 1] > threshold && d[i + 2] > threshold) {
                        d[i + 3] = 0;
                    }
                }
                ctx.putImageData(imageData, 0, 0);
            }
        },

        getImageDataUrl: function (canvasId) {
            const canvas = document.getElementById(canvasId);
            const entry = imgData[canvasId];
            if (!canvas || !entry || !entry.img) return null;
            return canvas.toDataURL('image/png');
        },

        destroyImage: function (canvasId) {
            delete imgData[canvasId];
        }
    };
})();
