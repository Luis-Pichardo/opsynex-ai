import { writeFileSync } from 'node:fs'

import { request } from '@playwright/test'

import { STATE_FILE, type E2eState } from './fixtures'

/**
 * Prepara una empresa nueva (nombre único por ejecución) con un Analyst y un Client mediante el
 * API, con el Admin configurado. Así las pruebas no dependen de datos previos.
 */
export default async function globalSetup() {
  const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5080'
  const admin = {
    email: process.env.E2E_ADMIN_EMAIL ?? 'admin@demo.opsynex.local',
    password: process.env.E2E_ADMIN_PASSWORD ?? 'Demo2026!',
  }
  const api = await request.newContext({ baseURL: `${apiUrl}/api/v1/` })
  const login = await api.post('auth/login', { data: admin })
  if (!login.ok()) {
    throw new Error(`No se pudo iniciar sesión como Admin (${login.status()}): ${await login.text()}`)
  }
  const { accessToken } = (await login.json()) as { accessToken: string }
  const headers = { Authorization: `Bearer ${accessToken}` }

  const run = Date.now().toString(36)
  const companyName = `E2E Manufactura ${run}`
  const company = await api.post('companies', {
    headers,
    data: { name: companyName, industry: 'Manufacturing', sector: 'Pruebas', currency: 'DOP' },
  })
  if (!company.ok()) throw new Error(`No se pudo crear la empresa: ${await company.text()}`)
  const { id: companyId } = (await company.json()) as { id: string }

  const password = 'E2ePrueba2026'
  const users: Record<'Analyst' | 'Client', string> = {
    Analyst: `analista.${run}@e2e.opsynex.local`,
    Client: `cliente.${run}@e2e.opsynex.local`,
  }
  for (const [role, email] of Object.entries(users)) {
    const created = await api.post('users', {
      headers,
      data: { email, password, fullName: `${role} E2E`, role, companyId },
    })
    if (!created.ok()) throw new Error(`No se pudo crear el usuario ${role}: ${await created.text()}`)
  }
  await api.dispose()

  const result: E2eState = {
    companyId,
    companyName,
    password,
    analyst: users.Analyst,
    client: users.Client,
    admin,
  }
  writeFileSync(STATE_FILE, JSON.stringify(result, null, 2))
}
