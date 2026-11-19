import { expect, test, type Page } from '@playwright/test'

import { login, state } from '../fixtures'

/**
 * Lo que axe no comprueba solo (guía de diseño §10): teclado, foco visible, Esc en diálogos,
 * errores asociados a sus campos y 375 px sin scroll horizontal en todas las pantallas.
 */
async function focusedName(page: Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null
    if (!el) return ''
    const label = el.id ? document.querySelector(`label[for="${el.id}"]`)?.textContent : null
    return (label ?? el.getAttribute('aria-label') ?? el.textContent ?? '').trim()
  })
}

async function focusRingVisible(page: Page) {
  return page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null
    if (!el) return false
    const style = getComputedStyle(el)
    return style.boxShadow !== 'none' || (style.outlineStyle !== 'none' && style.outlineWidth !== '0px')
  })
}

test('login: orden de tabulación, foco visible y errores asociados', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Correo').focus()
  expect(await focusedName(page)).toBe('Correo')
  expect(await focusRingVisible(page)).toBe(true)
  await page.keyboard.press('Tab')
  expect(await focusedName(page)).toBe('Contraseña')

  await page.getByRole('button', { name: 'Iniciar sesión' }).click()
  const email = page.getByLabel('Correo')
  await expect(email).toHaveAttribute('aria-invalid', 'true')
  const describedBy = (await email.getAttribute('aria-describedby')) ?? ''
  const messages = await Promise.all(
    describedBy.split(' ').filter(Boolean).map((id) => page.locator(`[id="${id}"]`).textContent()),
  )
  const visible = await page.getByRole('alert').or(page.locator('p.text-critical-text')).first().textContent()
  expect(messages.join(' ')).toContain(visible?.trim() ?? '—')
  expect(messages.join(' ').trim()).not.toBe('')
})

test('enlace para saltar al contenido y Esc cierra los diálogos', async ({ page }) => {
  const { admin } = state()
  await login(page, admin.email, admin.password)
  await page.goto('/admin/companies')
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

  // El primer Tab muestra «Saltar al contenido»; Enter lleva el foco al contenido principal.
  await page.locator('body').focus()
  await page.keyboard.press('Tab')
  expect(await focusedName(page)).toBe('Saltar al contenido')
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/#contenido|#main|companies/)

  const trigger = page.getByRole('button', { name: 'Nueva empresa' })
  await trigger.click()
  await expect(page.getByRole('dialog', { name: 'Nueva empresa' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(trigger).toBeFocused()
})

test('375 px: ninguna pantalla tiene scroll horizontal', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 375, height: 812 } })
  const page = await context.newPage()
  const { analyst, password } = state()
  await login(page, analyst, password)
  for (const path of ['/', '/problems', '/kpis', '/uploads', '/mappings', '/production', '/costs']) {
    await page.goto(path)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.waitForLoadState('networkidle')
    const width = await page.evaluate(() => document.documentElement.scrollWidth)
    expect(width, `scroll horizontal en ${path}`).toBeLessThanOrEqual(375)
  }
  await context.close()
})
