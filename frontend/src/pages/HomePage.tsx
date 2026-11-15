import { ApiHealthCard } from '@/components/health/ApiHealthCard'

/** Página inicial provisional (etapa 00): muestra el estado del API. */
export function HomePage() {
  return (
    <main className="mx-auto w-full max-w-content px-4 py-8 lg:px-8">
      <header className="mb-8 space-y-2">
        <p className="text-overline uppercase text-primary-text">Opsynex AI</p>
        <h1 className="text-h1 text-foreground">Diagnóstico financiero-operacional</h1>
        <p className="max-w-prose text-body text-foreground-secondary">
          Plataforma en construcción. Esta pantalla comprueba la conexión con el API.
        </p>
      </header>
      <section className="grid gap-3 sm:gap-4 md:grid-cols-2 xl:grid-cols-3">
        <ApiHealthCard />
      </section>
    </main>
  )
}
