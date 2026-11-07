(function () {
    'use strict';
    var campo = document.getElementById('CodigoBarra');
    var opciones = [
        { id: 'GenerarCodigoBarras', atributo: 'data-ean13' },
        { id: 'BalanzaPesable', atributo: 'data-pesable' },
        { id: 'BalanzaUnitario', atributo: 'data-unitario' }
    ].map(function (opcion) {
        return { casilla: document.getElementById(opcion.id), atributo: opcion.atributo };
    }).filter(function (opcion) { return opcion.casilla; });
    if (!campo || !opciones.length) return;
    var manual = campo.value;
    var eraAutomatico = opciones.some(function (opcion) { return opcion.casilla.checked; });
    function actualizar(cambiada) {
        if (cambiada && cambiada.casilla.checked) {
            if (!eraAutomatico) manual = campo.value;
            opciones.forEach(function (opcion) {
                if (opcion !== cambiada) opcion.casilla.checked = false;
            });
        }
        var marcadas = opciones.filter(function (opcion) { return opcion.casilla.checked; });
        var automatico = marcadas.length > 0;
        var mensaje = '';
        if (automatico) {
            campo.value = campo.getAttribute(marcadas[0].atributo) || '';
            if (!campo.value) mensaje = 'El número de producto no cabe en el formato seleccionado.';
            if (marcadas.length > 1) mensaje = 'Selecciona un solo tipo de código de barras.';
        } else if (eraAutomatico) {
            campo.value = manual;
        }
        campo.readOnly = automatico;
        campo.required = !automatico;
        opciones.forEach(function (opcion) {
            opcion.casilla.setCustomValidity(opcion.casilla.checked ? mensaje : '');
        });
        var estado = document.getElementById('estado-barra');
        if (estado) estado.textContent = mensaje;
        eraAutomatico = automatico;
    }
    opciones.forEach(function (opcion) {
        opcion.casilla.addEventListener('change', function () { actualizar(opcion); });
    });
    actualizar(null);
}());
