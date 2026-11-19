import { expect, test } from '@playwright/test'

import { login, state } from '../fixtures'

/** El Client solo consulta (FLUJO_DE_USUARIO.md): no sube, no homologa ni cambia estados. */
test.describe('Permisos del Client', () => {
  test('ve los resultados pero no puede cargar, homologar ni administrar', async ({ page }) => {
    const { client, password } = state()
    await login(page, client, password)

    const nav = page.getByRole('navigation', { name: 'Navegación principal' })
    for (const hidden of ['Carga de datos', 'Homologación', 'Administración']) {
      await expect(nav.getByText(hidden)).toHaveCount(0)
    }
    await expect(page.getByRole('img', { name: /Score Opsynex \d+ de 100/ })).toBeVisible()

    // Por URL directa tampoco.
    for (const path of ['/uploads', '/mappings', '/admin/users']) {
      await page.goto(path)
      await expect(page.getByText('No tienes acceso a esta sección')).toBeVisible()
    }

    // El detalle de un problema no ofrece cambiar el estado.
    await page.goto('/problems')
    await page.locator('main table a').first().click()
    await expect(page.getByRole('heading', { name: 'Impacto económico' })).toBeVisible()
    await expect(page.getByLabel('Estado')).toHaveCount(0)

    // Puede descargar el informe PDF.
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: /Descargar informe PDF/ }).first().click()
    expect((await download).suggestedFilename()).toMatch(/\.pdf$/)
  })
})
