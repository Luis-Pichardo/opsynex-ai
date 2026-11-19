import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { expect, type Page } from '@playwright/test'

const here = dirname(fileURLToPath(import.meta.url))

/** Libros sintéticos del repositorio (sin datos de clientes). */
export const SAMPLE_DATA = join(here, '..', 'backend', 'sample-data')

export const STATE_FILE = join(here, '.e2e-state.json')

/** Datos creados por global-setup: una empresa nueva con su Analyst y su Client. */
export interface E2eState {
  companyId: string
  companyName: string
  password: string
  analyst: string
  client: string
  admin: { email: string; password: string }
}

export function state(): E2eState {
  return JSON.parse(readFileSync(STATE_FILE, 'utf8')) as E2eState
}

export async function login(page: Page, email: string, password: string) {
  await page.goto('/login')
  await page.getByLabel('Correo').fill(email)
  await page.getByLabel('Contraseña').fill(password)
  await page.getByRole('button', { name: 'Iniciar sesión' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible()
}
