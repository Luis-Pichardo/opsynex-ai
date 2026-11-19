import { defineConfig, devices } from '@playwright/test'

/**
 * E2E contra una plataforma ya levantada (API, servicio de IA y frontend). Variables:
 * - E2E_BASE_URL (frontend, por defecto http://localhost:5173)
 * - E2E_API_URL (API, por defecto http://localhost:5080)
 * - E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD (un Admin existente; en la CI lo crea Seed__Admin*)
 * - PW_CHROMIUM_PATH (opcional: Chromium ya instalado, en lugar del de `playwright install`)
 */
export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    locale: 'es-DO',
    trace: 'off',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: process.env.PW_CHROMIUM_PATH
          ? { executablePath: process.env.PW_CHROMIUM_PATH }
          : {},
      },
    },
  ],
})
