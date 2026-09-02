// Redimensionado de columnas de la tabla de facturas (arrastrar el borde derecho de
// un encabezado). Es puro DOM del lado del cliente — no pasa por SignalR en cada
// pixel de arrastre — porque un roundtrip al servidor por cada mousemove se sentiría
// con retraso. Se inicializa una sola vez desde Revision.razor (OnAfterRenderAsync)
// una vez que la tabla ya tiene filas.
window.quickfiscalTabla = {
    initResize(tablaId) {
        const tabla = document.getElementById(tablaId);
        if (!tabla || tabla.dataset.resizeInit === '1') return;
        tabla.dataset.resizeInit = '1';

        const cols = tabla.querySelectorAll('colgroup col');
        const ths = tabla.querySelectorAll('thead th');

        ths.forEach((th, i) => {
            if (i >= cols.length - 1) return; // sin manija en la última columna (Foto)

            const manija = document.createElement('span');
            manija.className = 'col-resizer';
            th.appendChild(manija);

            manija.addEventListener('mousedown', (e) => {
                e.preventDefault();
                const anchoInicial = cols[i].getBoundingClientRect().width;
                const xInicial = e.pageX;
                manija.classList.add('resizing');
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';

                function mover(ev) {
                    const nuevoAncho = Math.max(32, anchoInicial + (ev.pageX - xInicial));
                    cols[i].style.width = nuevoAncho + 'px';
                }
                function soltar() {
                    document.removeEventListener('mousemove', mover);
                    document.removeEventListener('mouseup', soltar);
                    manija.classList.remove('resizing');
                    document.body.style.cursor = '';
                    document.body.style.userSelect = '';
                }
                document.addEventListener('mousemove', mover);
                document.addEventListener('mouseup', soltar);
            });
        });
    }
};
