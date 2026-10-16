window.nexoAppSelect = {
    positionList: (wrapEl, listEl) => {
        if (!wrapEl || !listEl) return;
        // Teleport to body to escape any CSS transform containing block (e.g. MudDialog animation)
        if (listEl.parentNode !== document.body) {
            document.body.appendChild(listEl);
        }
        const r = wrapEl.getBoundingClientRect();
        const vh = window.innerHeight;
        const gap = 4;
        const maxH = 300;
        const spaceBelow = vh - r.bottom - gap;
        listEl.style.position = 'fixed';
        listEl.style.left     = r.left + 'px';
        listEl.style.width    = r.width + 'px';
        listEl.style.right    = 'auto';
        listEl.style.margin   = '0';
        if (spaceBelow >= 120) {
            listEl.style.top       = (r.bottom + gap) + 'px';
            listEl.style.bottom    = 'auto';
            listEl.style.maxHeight = Math.min(maxH, spaceBelow) + 'px';
        } else {
            listEl.style.bottom    = (vh - r.top + gap) + 'px';
            listEl.style.top       = 'auto';
            listEl.style.maxHeight = Math.min(maxH, r.top - gap) + 'px';
        }
    },
    returnList: (wrapEl, listEl) => {
        if (!listEl) return;
        // Move back to component so Blazor can manage it; reset to hidden state
        if (listEl.parentNode === document.body && wrapEl) {
            wrapEl.appendChild(listEl);
        }
        listEl.style.left   = '-9999px';
        listEl.style.top    = '0';
        listEl.style.bottom = 'auto';
    }
};
