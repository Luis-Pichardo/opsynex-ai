import { zodResolver } from '@hookform/resolvers/zod'
import { CircleAlert, Info, LineChart, ShieldCheck, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'

import { loginErrorMessage } from '@/auth/login-errors'
import { useAuth } from '@/auth/useAuth'
import { Logo } from '@/components/layout/Logo'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'

const schema = z.object({
  email: z.string().trim().min(1, 'Escribe tu correo.').email('El correo no es válido.'),
  password: z.string().min(1, 'Escribe tu contraseña.'),
})

type LoginValues = z.infer<typeof schema>

const FEATURES = [
  { icon: LineChart, text: 'KPIs, tendencias y score de salud de 0 a 100' },
  { icon: Sparkles, text: 'Ineficiencias detectadas con su impacto en dinero' },
  { icon: ShieldCheck, text: 'Datos de cada empresa aislados y protegidos' },
]

/** Login a pantalla dividida: panel de marca (color del sidebar) y formulario. Sin registro. */
export function LoginPage() {
  const { isAuthenticated, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'
  const [error, setError] = useState<string | null>(null)
  const [showHelp, setShowHelp] = useState(false)
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  if (isAuthenticated) return <Navigate to={from} replace />

  const onSubmit = async (values: LoginValues) => {
    setError(null)
    try {
      await login(values.email, values.password)
      navigate(from, { replace: true })
    } catch (e) {
      setError(loginErrorMessage(e))
    }
  }

  return (
    <div className="grid min-h-screen grid-rows-[auto_1fr] bg-background lg:grid-cols-2 lg:grid-rows-1">
      <aside className="flex flex-col justify-between gap-8 bg-sidebar px-4 py-5 text-sidebar-foreground sm:px-10 lg:p-12">
        <Logo />
        <div className="hidden space-y-6 lg:block">
          <h2 className="text-h1 text-sidebar-foreground">
            Diagnóstico financiero y operacional para tu empresa
          </h2>
          <ul className="space-y-4">
            {FEATURES.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3 text-body text-sidebar-muted">
                <Icon className="size-5 shrink-0" aria-hidden="true" />
                {text}
              </li>
            ))}
          </ul>
        </div>
        <p className="hidden text-small text-sidebar-muted lg:block">Opsynex AI</p>
      </aside>

      <main className="relative flex items-start justify-center px-4 pb-10 pt-16 sm:px-8 lg:items-center lg:py-10">
        <div className="absolute right-4 top-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm space-y-6">
          <div className="space-y-1">
            <h1 className="text-h1 text-foreground">Inicia sesión</h1>
            <p className="text-body text-foreground-secondary">
              Usa el correo y la contraseña que te dio el administrador.
            </p>
          </div>

          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Correo</FormLabel>
                    <FormControl>
                      <Input
                        type="email"
                        autoComplete="email"
                        placeholder="nombre@empresa.com"
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Contraseña</FormLabel>
                    <FormControl>
                      <Input type="password" autoComplete="current-password" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {error ? (
                <div
                  role="alert"
                  className="flex items-start gap-2 rounded border border-critical bg-critical-soft p-3 text-small text-critical-text"
                >
                  <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                  {error}
                </div>
              ) : null}

              <Button type="submit" className="w-full" loading={form.formState.isSubmitting}>
                Iniciar sesión
              </Button>
            </form>
          </Form>

          <div className="space-y-2">
            <Button
              variant="link"
              className="h-auto px-0"
              aria-expanded={showHelp}
              aria-controls="password-help"
              onClick={() => setShowHelp((v) => !v)}
            >
              ¿Olvidaste tu contraseña?
            </Button>
            {showHelp ? (
              <p
                id="password-help"
                className="flex items-start gap-2 rounded bg-info-soft p-3 text-small text-info-text"
              >
                <Info className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                Pide al administrador de Opsynex que restablezca tu contraseña. Por seguridad, las
                cuentas solo las crea y modifica el administrador.
              </p>
            ) : null}
          </div>
        </div>
      </main>
    </div>
  )
}
