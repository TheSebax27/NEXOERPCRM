// Mapa de clientes — Colombia choropleth con Leaflet
window.nexoMapa = (() => {
    const _mapas = {};

    function colorParaConteo(conteo, maximo) {
        if (conteo === 0) return '#e8eaf0';
        const t = Math.min(conteo / maximo, 1);
        if (t < 0.25) return '#bfdbfe';
        if (t < 0.50) return '#60a5fa';
        if (t < 0.75) return '#2563eb';
        return '#1e3a8a';
    }

    async function init(elementId, puntos) {
        if (_mapas[elementId]) {
            _mapas[elementId].remove();
            delete _mapas[elementId];
        }

        const el = document.getElementById(elementId);
        if (!el || typeof L === 'undefined') return;

        // Carga el GeoJSON de departamentos
        let geojson;
        try {
            const resp = await fetch('/data/colombia-depts.geojson');
            geojson = await resp.json();
        } catch {
            // Fallback: mapa con marcadores si falla la carga
            initFallback(elementId, puntos);
            return;
        }

        // Construye lookup conteo por dept (normaliza tildes/mayúsculas)
        const normalizar = s => s ? s.normalize('NFD').replace(/[̀-ͯ]/g,'').toLowerCase() : '';
        const conteoMap = {};
        let maximo = 1;
        puntos.forEach(p => {
            const k = normalizar(p.dept);
            conteoMap[k] = (conteoMap[k] || 0) + p.total;
            if (conteoMap[k] > maximo) maximo = conteoMap[k];
        });
        // También indexa por nombre original para el popup
        const puntosMap = {};
        puntos.forEach(p => { puntosMap[normalizar(p.dept)] = p; });

        const mapa = L.map(elementId, {
            zoomControl: true,
            attributionControl: false
        }).setView([4.5, -74.1], 5);
        _mapas[elementId] = mapa;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 18,
            attribution: '© OpenStreetMap'
        }).addTo(mapa);

        L.geoJSON(geojson, {
            style: feature => {
                const key = normalizar(feature.properties.name);
                const conteo = conteoMap[key] || 0;
                return {
                    fillColor: colorParaConteo(conteo, maximo),
                    fillOpacity: conteo > 0 ? 0.75 : 0.3,
                    color: '#475569',
                    weight: 1.2,
                    opacity: 0.8
                };
            },
            onEachFeature: (feature, layer) => {
                const key = normalizar(feature.properties.name);
                const conteo = conteoMap[key] || 0;
                const p = puntosMap[key];
                let html = `<strong>${feature.properties.name}</strong><br>${conteo} cliente${conteo !== 1 ? 's' : ''}`;
                if (p && p.nombres && p.nombres.length > 0) {
                    const lista = p.nombres.map(n => `<li>${n}</li>`).join('');
                    const mas = p.total > 5 ? `<li>…y ${p.total - 5} más</li>` : '';
                    html += `<ul style="padding-left:14px;margin:4px 0 0">${lista}${mas}</ul>`;
                }
                layer.bindTooltip(feature.properties.name, { sticky: true, className: 'nexo-mapa-tip' });
                layer.bindPopup(html);
                if (conteo > 0) {
                    layer.on('mouseover', function() { this.setStyle({ fillOpacity: 0.95, weight: 2 }); });
                    layer.on('mouseout', function() { this.setStyle({ fillOpacity: 0.75, weight: 1.2 }); });
                }
            }
        }).addTo(mapa);

        // Leyenda
        const leyenda = L.control({ position: 'bottomright' });
        leyenda.onAdd = () => {
            const div = L.DomUtil.create('div');
            div.style.cssText = 'background:white;padding:8px 12px;border-radius:8px;font-size:12px;box-shadow:0 2px 8px rgba(0,0,0,.2)';
            div.innerHTML = `
                <div style="font-weight:600;margin-bottom:4px">Clientes</div>
                ${[['Sin clientes','#e8eaf0'],['Pocos','#bfdbfe'],['Moderado','#60a5fa'],['Muchos','#2563eb'],['Máximo','#1e3a8a']]
                  .map(([l,c]) => `<div style="display:flex;align-items:center;gap:6px;margin:2px 0">
                    <span style="width:14px;height:14px;background:${c};border-radius:2px;display:inline-block"></span>${l}</div>`).join('')}
            `;
            return div;
        };
        leyenda.addTo(mapa);
    }

    function initFallback(elementId, puntos) {
        const mapa = L.map(elementId).setView([4.5, -74.1], 5);
        _mapas[elementId] = mapa;
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18 }).addTo(mapa);
        const maxTotal = Math.max(...puntos.map(p => p.total), 1);
        puntos.forEach(p => {
            const radio = 10 + Math.round((p.total / maxTotal) * 30);
            L.circleMarker([p.lat, p.lng], {
                radius: radio, fillColor: '#2563eb', color: '#1d4ed8',
                weight: 2, opacity: 0.9, fillOpacity: 0.55
            }).addTo(mapa).bindPopup(`<strong>${p.dept}</strong><br>${p.total} cliente(s)`);
        });
    }

    function destroy(elementId) {
        if (_mapas[elementId]) {
            _mapas[elementId].remove();
            delete _mapas[elementId];
        }
    }

    return { init, destroy };
})();
