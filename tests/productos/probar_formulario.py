#!/usr/bin/env python3
"""Prueba aislada del componente con Chromium/Playwright; no inicia IIS.
Ejecutar desde la raíz del repositorio con Playwright y /usr/bin/chromium.
"""
from pathlib import Path
from playwright.sync_api import sync_playwright
html='''<!doctype html><html lang="es"><head><meta charset="utf-8"></head><body>
<label><input id="GenerarCodigoBarras" type="checkbox">El producto no tiene código de barras</label>
<label><input id="BalanzaPesable" type="checkbox">Pesable de balanza</label>
<label><input id="BalanzaUnitario" type="checkbox">Unitario de balanza</label>
<input id="CodigoBarra" value="6935364099978" data-ean13="2800000000363" data-pesable="2000036" data-unitario="2100036">
<p id="estado-barra"></p>
<select id="PorcentajeIva"><option value="">Selecciona</option><option value="0">Exento</option><option value="5">5 %</option><option value="10">10 %</option></select>
</body></html>'''
with sync_playwright() as p:
    b=p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox'])
    try:
        page=b.new_page();page.set_content(html)
        page.add_script_tag(content=Path('SistemaVentas.Web/Scripts/ProductoFormulario.js').read_text())
        campo=page.locator('#CodigoBarra')
        assert campo.input_value()=='6935364099978' and campo.evaluate('(e)=>e.required && !e.readOnly')
        page.locator('#GenerarCodigoBarras').check()
        assert campo.input_value()=='2800000000363' and campo.evaluate('(e)=>e.readOnly && !e.required')
        page.locator('#GenerarCodigoBarras').uncheck()
        assert campo.input_value()=='6935364099978' and campo.evaluate('(e)=>!e.readOnly && e.required')
        page.locator('#BalanzaPesable').check()
        assert campo.input_value()=='2000036' and campo.evaluate('(e)=>e.readOnly')
        assert not page.locator('#GenerarCodigoBarras').is_checked() and not page.locator('#BalanzaUnitario').is_checked()
        page.locator('#BalanzaUnitario').check()
        assert campo.input_value()=='2100036' and not page.locator('#BalanzaPesable').is_checked()
        page.locator('#GenerarCodigoBarras').check()
        assert campo.input_value()=='2800000000363' and not page.locator('#BalanzaUnitario').is_checked()
        page.locator('#GenerarCodigoBarras').uncheck()
        assert campo.input_value()=='6935364099978' and not campo.evaluate('(e)=>e.readOnly')
        campo.evaluate("(e)=>e.setAttribute('data-pesable','')")
        page.locator('#BalanzaPesable').check()
        assert not page.locator('#BalanzaPesable').evaluate('(e)=>e.checkValidity()')
        assert page.locator('#estado-barra').inner_text()
        page.locator('#BalanzaPesable').uncheck()
        assert campo.input_value()=='6935364099978' and page.locator('#BalanzaPesable').evaluate('(e)=>e.checkValidity()')
        print('Casillas EAN-13/pesable/unitario excluyentes, restauración manual y aviso de formato no disponible: OK.')
        print('Valores IVA:',page.locator('#PorcentajeIva option').evaluate_all('(es)=>es.map(e=>e.value)'))
        for rate in ['0','5','10']:
            page.locator('#PorcentajeIva').select_option(value=rate,timeout=5000)
            assert page.locator('#PorcentajeIva').input_value()==rate
        print('Selector IVA: OK. Prueba DOM del componente con el script real; no es HTTP completo.')
    finally:b.close()
