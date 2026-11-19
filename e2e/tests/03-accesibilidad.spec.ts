import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

import { login, state } from '../fixtures'

/**
 * Checklist de accesibilidad de la guía de diseño (§10) con axe-core: WCAG 2.1 A y AA (contraste,
 * nombres accesibles, etiquetas, roles, foco) en todas las pantallas, en modo claro y oscuro y en
 * escritorio y móvil (375 px).
 */
const PAGES = [
  { path: '/', ready: { name: /Score Opsynex/ } },
  { path: '/problems', ready: null },
  { path: '/kpis', ready: null },
  { path: '/uploads', ready: null },
  { path: '/mappings', ready: null },
  { path: '/production', ready: null },
  { path: '/costs', ready: null },
]

async function audit(page: Page, label: string) {
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(500)
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()
  const summary = results.violations.map(
    (v) => `${v.id} (${v.impact}): ${v.help} → ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`,
  )
  expect(summary, `Violaciones de accesibilidad en ${label}`).toEqual([])
}

for (const scheme of ['light', 'dark'] as const) {
  for (const viewport of [
    { name: 'escritorio', width: 1440, height: 900 },
    { name: 'móvil', width: 375, height: 812 },
  ]) {
    test(`accesibilidad (${scheme}, ${viewport.name})`, async ({ browser }) => {
      const context = await browser.newContext({ colorScheme: scheme, viewport })
      const page = await context.newPage()

      await page.goto('/login')
      await audit(page, `/login ${scheme} ${viewport.name}`)

      const { analyst, password, admin } = state()
      await login(page, analyst, password)
      for (const { path } of PAGES) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await audit(page, `${path} ${scheme} ${viewport.name}`)
      }

      // Detalle de un problema.
      await page.goto('/problems')
      await page.locator('main table a').first().click()
      await expect(page.getByRole('heading', { name: 'Impacto económico' })).toBeVisible()
      await audit(page, `detalle de problema ${scheme} ${viewport.name}`)

      // Administración (Admin).
      await context.clearCookies()
      await page.evaluate(() => window.localStorage.clear())
      await login(page, admin.email, admin.password)
      for (const path of ['/admin/companies', '/admin/users']) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
        await audit(page, `${path} ${scheme} ${viewport.name}`)
      }
      await context.close()
    })
  }
}
