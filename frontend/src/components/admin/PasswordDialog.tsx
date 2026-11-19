import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { errorMessage } from '@/api/client'
import { usersApi } from '@/api/users'
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
import { passwordSchema } from '@/lib/password'
import type { UserDto } from '@/types/api'

const schema = z
  .object({ password: passwordSchema, confirm: z.string() })
  .refine((v) => v.password === v.confirm, {
    path: ['confirm'],
    message: 'Las contraseñas no coinciden.',
  })

type PasswordValues = z.infer<typeof schema>

interface PasswordDialogProps {
  user: UserDto | null
  onOpenChange: (open: boolean) => void
}

/** El Admin asigna una contraseña nueva (también desbloquea la cuenta). */
export function PasswordDialog({ user, onOpenChange }: PasswordDialogProps) {
  const form = useForm<PasswordValues>({
    resolver: zodResolver(schema),
    defaultValues: { password: '', confirm: '' },
  })

  useEffect(() => {
    if (user) form.reset({ password: '', confirm: '' })
  }, [user, form])

  const mutation = useMutation({
    mutationFn: (values: PasswordValues) => usersApi.setPassword(user!.id, values.password),
    meta: { silentError: true },
    onSuccess: () => {
      toast.success(`Contraseña de «${user?.fullName}» actualizada.`)
      onOpenChange(false)
    },
    onError: (error) => form.setError('root', { message: errorMessage(error) }),
  })

  return (
    <Dialog open={user !== null} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cambiar contraseña</DialogTitle>
          <DialogDescription>
            Nueva contraseña para {user?.fullName}. Su sesión actual se cerrará y la cuenta se
            desbloquea.
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
              name="password"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nueva contraseña</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormDescription>Al menos 8 caracteres, con letras y números.</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="confirm"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Confirmar contraseña</FormLabel>
                  <FormControl>
                    <Input type="password" autoComplete="new-password" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
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
                Guardar contraseña
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
