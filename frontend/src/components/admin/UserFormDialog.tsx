import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { errorMessage } from '@/api/client'
import { usersApi } from '@/api/users'
import { ROLE_LABELS } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { applyServerErrors } from '@/lib/form-errors'
import { passwordSchema } from '@/lib/password'
import type { CompanyDto, UserDto, UserRole } from '@/types/api'

const NO_COMPANY = 'none'

const baseSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, 'El nombre es obligatorio.')
    .max(200, 'Máximo 200 caracteres.'),
  email: z.string().trim().min(1, 'El correo es obligatorio.').email('El correo no es válido.'),
  role: z.enum(['Admin', 'Analyst', 'Client']),
  companyId: z.string(),
  password: z.string(),
  isActive: z.boolean(),
})

/** El alta exige contraseña; Analyst y Client deben pertenecer a una empresa. */
function schemaFor(isNew: boolean) {
  return baseSchema.superRefine((values, ctx) => {
    if (values.role !== 'Admin' && values.companyId === NO_COMPANY) {
      ctx.addIssue({
        code: 'custom',
        path: ['companyId'],
        message: 'Analistas y clientes deben pertenecer a una empresa.',
      })
    }
    if (isNew) {
      const result = passwordSchema.safeParse(values.password)
      if (!result.success) {
        ctx.addIssue({
          code: 'custom',
          path: ['password'],
          message: result.error.issues[0]?.message ?? 'Contraseña no válida.',
        })
      }
    }
  })
}

type UserValues = z.infer<typeof baseSchema>

interface UserFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  companies: CompanyDto[]
  /** Usuario a editar; sin él, crea uno nuevo. */
  user?: UserDto | null
}

const ROLE_HELP: Record<UserRole, string> = {
  Admin: 'Administra empresas y usuarios de toda la plataforma.',
  Analyst: 'Sube archivos, revisa cuentas y ejecuta el análisis de su empresa.',
  Client: 'Consulta los resultados de su empresa.',
}

export function UserFormDialog({ open, onOpenChange, companies, user }: UserFormDialogProps) {
  const queryClient = useQueryClient()
  const isNew = !user
  const form = useForm<UserValues>({
    resolver: zodResolver(schemaFor(isNew)),
    defaultValues: {
      fullName: '',
      email: '',
      role: 'Analyst',
      companyId: NO_COMPANY,
      password: '',
      isActive: true,
    },
  })
  const role = useWatch({ control: form.control, name: 'role' })

  useEffect(() => {
    if (open) {
      form.reset({
        fullName: user?.fullName ?? '',
        email: user?.email ?? '',
        role: user?.role ?? 'Analyst',
        companyId: user?.companyId ?? NO_COMPANY,
        password: '',
        isActive: user?.isActive ?? true,
      })
    }
  }, [open, user, form])

  const mutation = useMutation({
    mutationFn: (values: UserValues) => {
      const companyId = values.companyId === NO_COMPANY ? null : values.companyId
      return user
        ? usersApi.update(user.id, {
            email: values.email,
            fullName: values.fullName,
            role: values.role,
            companyId,
            isActive: values.isActive,
          })
        : usersApi.create({
            email: values.email,
            fullName: values.fullName,
            role: values.role,
            companyId,
            password: values.password,
          })
    },
    meta: { silentError: true },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['users'] })
      toast.success(
        user ? `Usuario «${saved.fullName}» actualizado.` : `Usuario «${saved.fullName}» creado.`,
      )
      onOpenChange(false)
    },
    onError: (error) => {
      if (!applyServerErrors(form, error)) form.setError('root', { message: errorMessage(error) })
    },
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? 'Nuevo usuario' : 'Editar usuario'}</DialogTitle>
          <DialogDescription>
            {isNew
              ? 'Comparte la contraseña inicial con la persona por un canal seguro.'
              : 'Si cambias el rol o la empresa, la persona deberá iniciar sesión de nuevo.'}
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form
            onSubmit={form.handleSubmit((v) => mutation.mutate(v))}
            className="space-y-4"
            noValidate
          >
            <FormField
              control={form.control}
              name="fullName"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nombre completo</FormLabel>
                  <FormControl>
                    <Input autoComplete="name" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Correo</FormLabel>
                  <FormControl>
                    <Input type="email" autoComplete="email" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="role"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Rol</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {(Object.keys(ROLE_LABELS) as UserRole[]).map((value) => (
                          <SelectItem key={value} value={value}>
                            {ROLE_LABELS[value]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="companyId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Empresa</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Elige una empresa" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {role === 'Admin' ? (
                          <SelectItem value={NO_COMPANY}>Sin empresa</SelectItem>
                        ) : null}
                        {companies.map((company) => (
                          <SelectItem key={company.id} value={company.id}>
                            {company.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <p className="text-small text-foreground-secondary">{ROLE_HELP[role]}</p>
            {isNew ? (
              <FormField
                control={form.control}
                name="password"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Contraseña inicial</FormLabel>
                    <FormControl>
                      <Input type="password" autoComplete="new-password" {...field} />
                    </FormControl>
                    <FormDescription>Al menos 8 caracteres, con letras y números.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ) : (
              <FormField
                control={form.control}
                name="isActive"
                render={({ field }) => (
                  <FormItem className="flex-row items-center gap-3">
                    <FormControl>
                      <input
                        type="checkbox"
                        checked={field.value}
                        onChange={(e) => field.onChange(e.target.checked)}
                        className="size-5 rounded-sm border-input accent-primary"
                      />
                    </FormControl>
                    <FormLabel className="font-normal">Usuario activo</FormLabel>
                  </FormItem>
                )}
              />
            )}
            {form.formState.errors.root ? (
              <p role="alert" className="text-small text-critical-text">
                {form.formState.errors.root.message}
              </p>
            ) : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Cancelar
              </Button>
              <Button type="submit" loading={mutation.isPending}>
                {isNew ? 'Crear usuario' : 'Guardar cambios'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
