import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import { errorMessage } from '@/api/client'
import { companiesApi } from '@/api/companies'
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
import { INDUSTRY_LABELS } from '@/lib/labels'
import type { CompanyDto } from '@/types/api'

const schema = z.object({
  name: z.string().trim().min(1, 'El nombre es obligatorio.').max(200, 'Máximo 200 caracteres.'),
  industry: z.enum(['Manufacturing', 'Services'], { message: 'Elige la industria.' }),
  sector: z.string().trim().max(100, 'Máximo 100 caracteres.'),
  currency: z
    .string()
    .trim()
    .regex(/^[A-Z]{3}$/, 'Usa un código ISO de 3 letras en mayúsculas (p. ej. DOP o USD).'),
})

type CompanyValues = z.infer<typeof schema>

interface CompanyFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Empresa a editar; sin ella, crea una nueva. */
  company?: CompanyDto | null
}

export function CompanyFormDialog({ open, onOpenChange, company }: CompanyFormDialogProps) {
  const queryClient = useQueryClient()
  const form = useForm<CompanyValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', industry: 'Manufacturing', sector: '', currency: 'DOP' },
  })

  useEffect(() => {
    if (open) {
      form.reset({
        name: company?.name ?? '',
        industry: company?.industry ?? 'Manufacturing',
        sector: company?.sector ?? '',
        currency: company?.currency ?? 'DOP',
      })
    }
  }, [open, company, form])

  const mutation = useMutation({
    mutationFn: (values: CompanyValues) => {
      const request = { ...values, sector: values.sector || null }
      return company ? companiesApi.update(company.id, request) : companiesApi.create(request)
    },
    meta: { silentError: true },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['companies'] })
      toast.success(
        company ? `Empresa «${saved.name}» actualizada.` : `Empresa «${saved.name}» creada.`,
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
          <DialogTitle>{company ? 'Editar empresa' : 'Nueva empresa'}</DialogTitle>
          <DialogDescription>
            La industria y la moneda definen las referencias del análisis.
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
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Nombre</FormLabel>
                  <FormControl>
                    <Input autoComplete="organization" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="industry"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Industria</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {Object.entries(INDUSTRY_LABELS).map(([value, label]) => (
                          <SelectItem key={value} value={value}>
                            {label}
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
                name="currency"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Moneda</FormLabel>
                    <FormControl>
                      <Input
                        maxLength={3}
                        className="uppercase"
                        {...field}
                        onChange={(e) => field.onChange(e.target.value.toUpperCase())}
                      />
                    </FormControl>
                    <FormDescription>Código ISO 4217, p. ej. DOP.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <FormField
              control={form.control}
              name="sector"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Sector (opcional)</FormLabel>
                  <FormControl>
                    <Input placeholder="Ej.: alimentos, metalmecánica" {...field} />
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
                {company ? 'Guardar cambios' : 'Crear empresa'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
