import { formatoCuit, formatoMoneda, nombreMes, totalRenglones } from '../lib/formato'
import { errorDesdeRespuesta, query } from '../lib/api'

describe('formato', () => {
  it('suma los renglones', () => {
    expect(totalRenglones([{ cantidad: 2, precioUnitario: 100.5 }, { cantidad: 3, precioUnitario: 10 }])).toBe(231)
    expect(totalRenglones([])).toBe(0)
  })

  it('pone guiones al CUIT', () => {
    expect(formatoCuit('30712345671')).toBe('30-71234567-1')
    expect(formatoCuit('123')).toBe('123')
  })

  it('formatea pesos argentinos', () => {
    // Intl usa espacio duro entre el signo y el número
    expect(formatoMoneda(1234.5).replace(/\s/g, ' ')).toBe('$ 1.234,50')
  })

  it('traduce el número de mes', () => {
    expect(nombreMes(1)).toBe('Enero')
    expect(nombreMes(12)).toBe('Diciembre')
  })
})

describe('api', () => {
  it('arma la query salteando vacíos y false', () => {
    expect(query({ texto: 'yerba', pagina: 2, estado: '', soloStockBajo: false, x: null })).toBe('?texto=yerba&pagina=2')
    expect(query({})).toBe('')
  })

  it('interpreta un ProblemDetails de stock insuficiente', async () => {
    const resp = new Response(JSON.stringify({
      title: 'Stock insuficiente', detail: 'No hay stock suficiente para confirmar el pedido.',
      faltantes: [{ productoId: 1, sku: 'ALM-001', pedido: 10, disponible: 3 }],
    }), { status: 409 })
    const error = await errorDesdeRespuesta(resp)
    expect(error.estado).toBe(409)
    expect(error.message).toContain('No hay stock')
    expect(error.faltantes[0].sku).toBe('ALM-001')
  })

  it('usa el primer error de validación si no hay detail', async () => {
    const resp = new Response(JSON.stringify({ title: 'Hay datos inválidos', errors: { Cuit: ['El CUIT no es válido.'] } }), { status: 400 })
    const error = await errorDesdeRespuesta(resp)
    expect(error.message).toBe('El CUIT no es válido.')
    expect(error.errores.Cuit).toHaveLength(1)
  })
})
