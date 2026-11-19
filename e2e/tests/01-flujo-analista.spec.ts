import { join } from 'node:path'

import { expect, test } from '@playwright/test'

import { login, SAMPLE_DATA, state } from '../fixtures'

/**
 * Flujo principal (FLUJO_DE_USUARIO.md): el Analyst sube el libro sintético → el análisis es
 * automático → revisa la homologación → dashboard → detalle de un problema. Luego sube los
 * datos industriales y el menú Producción se habilita.
 */
test.describe.serial('Flujo del Analyst', () => {
  test('carga → homologación → dashboard → detalle de problema', async ({ page }) => {
    const { analyst, password } = state()
    await login(page, analyst, password)

    // Sin datos: el dashboard invita a subir el libro.
    await expect(page.getByText('Sube tus estados financieros para empezar')).toBeVisible()

    // 1. Carga del libro contable sintético (3 años).
    await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('link', { name: 'Carga de datos' }).click()
    await page.locator('input[type=file]').setInputFiles(join(SAMPLE_DATA, 'libro_sintetico_3a.xls'))
    await page.getByRole('button', { name: 'Subir y analizar' }).click()
    await expect(page.getByRole('heading', { name: 'Hojas detectadas' })).toBeVisible({ timeout: 60_000 })
    await expect(page.getByText('Balance general').first()).toBeVisible()
    await page.getByRole('button', { name: 'Ver el resultado del análisis' }).click()
    await expect(page.getByRole('heading', { name: 'Análisis completado' })).toBeVisible()
    await expect(page.getByText('2022, 2023 y 2024')).toBeVisible()

    // 2. Homologación (revisión opcional).
    await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('link', { name: 'Homologación' }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Homologación' })).toBeVisible()
    await expect(page.locator('main')).toContainText(/cuentas|Cuenta/)

    // 3. Dashboard con score, impacto, KPIs y problemas.
    await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('link', { name: 'Dashboard' }).click()
    await expect(page.getByRole('img', { name: /Score Opsynex \d+ de 100/ })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Principales problemas' })).toBeVisible()

    // 4. Detalle del primer problema: impacto, recomendación y cambio de estado.
    await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('link', { name: 'Problemas' }).click()
    await page.locator('main table a').first().click()
    await expect(page.getByRole('heading', { name: 'Impacto económico' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Recomendación' })).toBeVisible()
    await page.getByLabel('Estado').click()
    await page.getByRole('option', { name: 'Reconocido' }).click()
    await expect(page.getByText('Estado cambiado a «Reconocido».')).toBeVisible()
  })

  test('los datos industriales habilitan Producción y Costos', async ({ page }) => {
    const { analyst, password } = state()
    await login(page, analyst, password)
    const nav = page.getByRole('navigation', { name: 'Navegación principal' })
    await expect(nav.getByRole('link', { name: 'Producción' })).toHaveAttribute('aria-disabled', 'true')

    for (const file of ['estandares.xlsx', 'costos.xlsx', 'produccion.xlsx']) {
      // Cada archivo empieza el asistente desde el primer paso.
      await page.goto('/uploads')
      await page.locator('input[type=file]').setInputFiles(join(SAMPLE_DATA, 'industrial', file))
      await page.getByRole('button', { name: 'Subir y analizar' }).click()
      await page.getByRole('button', { name: 'Ver el resultado del análisis' }).click()
      await expect(page.getByRole('heading', { name: 'Análisis completado' })).toBeVisible()
    }

    await page.getByRole('link', { name: 'Ir a producción' }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Producción' })).toBeVisible()
    const ranking = page.getByRole('table', { name: 'Ranking de máquinas por OEE' })
    await expect(ranking.getByRole('row').nth(1)).toContainText('MAQ-03')
    await expect(ranking).toContainText('Bajo el 60 %')
    await nav.getByRole('link', { name: 'Costos' }).click()
    await expect(page.getByRole('table', { name: 'Costos por máquina' })).toContainText('sobre el estándar')
  })
})
