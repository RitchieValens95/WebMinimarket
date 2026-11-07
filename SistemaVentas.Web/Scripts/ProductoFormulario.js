(function () {
    'use strict';
    var casilla = document.getElementById('GenerarCodigoBarras');
    var campo = document.getElementById('CodigoBarra');
    if (!casilla || !campo) return;
    var manual = campo.value;
    function actualizar(inicial) {
        if (casilla.checked) {
            if (!inicial) manual = campo.value;
            campo.value = campo.getAttribute('data-ean13') || '';
        } else if (!inicial) {
            campo.value = manual;
        }
        campo.readOnly = casilla.checked;
        campo.required = !casilla.checked;
    }
    casilla.addEventListener('change', function () { actualizar(false); });
    actualizar(true);
}());
