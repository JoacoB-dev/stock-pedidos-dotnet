import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { EditorRenglones, type RenglonEnEdicion } from '../componentes/EditorRenglones'
import type { Producto } from '../lib/tipos'

const productos: Producto[] = [
  { id: 1, sku: 'ALM-001', nombre: 'Yerba', categoriaId: 1, categoria: 'Almacén', precio: 1000, stock: 5, stockMinimo: 1, stockBajo: false, activo: true, rowVersion: '' },
  { id: 2, sku: 'BEB-001', nombre: 'Agua', categoriaId: 2, categoria: 'Bebidas', precio: 250, stock: 100, stockMinimo: 10, stockBajo: false, activo: true, rowVersion: '' },
]

// Envoltorio con estado, como lo usa el formulario real
function Prueba() {
  const [renglones, setRenglones] = useState<RenglonEnEdicion[]>([])
  return <EditorRenglones productos={productos} renglones={renglones} onCambiar={setRenglones} />
}

const textoTotal = () => screen.getByTestId('total').textContent!.replace(/\s/g, ' ')

describe('EditorRenglones', () => {
  it('agrega productos, recalcula el total y no deja repetir', async () => {
    const usuario = userEvent.setup()
    render(<Prueba />)

    await usuario.selectOptions(screen.getByLabelText('Producto a agregar'), '1')
    await usuario.clear(screen.getByLabelText('Cantidad a agregar'))
    await usuario.type(screen.getByLabelText('Cantidad a agregar'), '2')
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }))

    await usuario.selectOptions(screen.getByLabelText('Producto a agregar'), '2')
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }))

    expect(textoTotal()).toBe('$ 2.250,00')
    // Ya agregados: no aparecen más en el selector
    expect(screen.queryByRole('option', { name: /ALM-001/ })).not.toBeInTheDocument()
  })

  it('avisa cuando la cantidad supera el stock y permite quitar', async () => {
    const usuario = userEvent.setup()
    render(<Prueba />)
    await usuario.selectOptions(screen.getByLabelText('Producto a agregar'), '1')
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }))

    const cantidad = screen.getByLabelText('Cantidad de ALM-001')
    await usuario.clear(cantidad)
    await usuario.type(cantidad, '8')
    expect(screen.getByText(/Hay 5 en stock/)).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Quitar ALM-001' }))
    expect(screen.getByText('Todavía no agregaste productos.')).toBeInTheDocument()
    expect(textoTotal()).toBe('$ 0,00')
  })
})
