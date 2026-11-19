import type * as LabelPrimitive from '@radix-ui/react-label'
import { Slot } from '@radix-ui/react-slot'
import { CircleAlert } from 'lucide-react'
import * as React from 'react'
import {
  Controller,
  FormProvider,
  useFormContext,
  type ControllerProps,
  type FieldPath,
  type FieldValues,
} from 'react-hook-form'

import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

/**
 * Form (React Hook Form + Zod, patrón de shadcn/ui). Label arriba; ayuda o error abajo
 * (`small`; el error en texto critical con icono, asociado con `aria-describedby`).
 */
const Form = FormProvider

interface FormFieldContextValue {
  name: string
}

const FormFieldContext = React.createContext<FormFieldContextValue | null>(null)
interface FormItemContextValue {
  id: string
  /** Si el campo tiene FormDescription (para no referenciar un id que no existe). */
  hasDescription: boolean
  setHasDescription: (value: boolean) => void
}

const FormItemContext = React.createContext<FormItemContextValue | null>(null)

function FormField<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
>(props: ControllerProps<TFieldValues, TName>) {
  return (
    <FormFieldContext.Provider value={{ name: props.name }}>
      <Controller {...props} />
    </FormFieldContext.Provider>
  )
}

function useFormField() {
  const field = React.useContext(FormFieldContext)
  const item = React.useContext(FormItemContext)
  const { getFieldState, formState } = useFormContext()
  if (!field || !item)
    throw new Error('useFormField debe usarse dentro de <FormField> y <FormItem>')
  const state = getFieldState(field.name, formState)
  return {
    name: field.name,
    formItemId: `${item.id}-item`,
    descriptionId: `${item.id}-description`,
    messageId: `${item.id}-message`,
    hasDescription: item.hasDescription,
    setHasDescription: item.setHasDescription,
    ...state,
  }
}

const FormItem = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
  ({ className, ...props }, ref) => {
    const id = React.useId()
    const [hasDescription, setHasDescription] = React.useState(false)
    const value = React.useMemo(
      () => ({ id, hasDescription, setHasDescription }),
      [id, hasDescription],
    )
    return (
      <FormItemContext.Provider value={value}>
        <div ref={ref} className={cn('flex flex-col gap-1.5', className)} {...props} />
      </FormItemContext.Provider>
    )
  },
)
FormItem.displayName = 'FormItem'

const FormLabel = React.forwardRef<
  React.ElementRef<typeof LabelPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof LabelPrimitive.Root>
>(({ className, ...props }, ref) => {
  const { formItemId } = useFormField()
  return <Label ref={ref} htmlFor={formItemId} className={className} {...props} />
})
FormLabel.displayName = 'FormLabel'

const FormControl = React.forwardRef<
  React.ElementRef<typeof Slot>,
  React.ComponentPropsWithoutRef<typeof Slot>
>(({ ...props }, ref) => {
  const { error, formItemId, descriptionId, messageId, hasDescription } = useFormField()
  // Solo los ids que existen: la descripción si la hay y el mensaje de error si hay error.
  const describedBy = [hasDescription ? descriptionId : null, error ? messageId : null]
    .filter(Boolean)
    .join(' ')
  return (
    <Slot
      ref={ref}
      id={formItemId}
      aria-describedby={describedBy || undefined}
      aria-invalid={!!error}
      {...props}
    />
  )
})
FormControl.displayName = 'FormControl'

const FormDescription = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, ...props }, ref) => {
  const { descriptionId, setHasDescription } = useFormField()
  React.useEffect(() => {
    setHasDescription(true)
    return () => setHasDescription(false)
  }, [setHasDescription])
  return (
    <p
      ref={ref}
      id={descriptionId}
      className={cn('text-small text-foreground-secondary', className)}
      {...props}
    />
  )
})
FormDescription.displayName = 'FormDescription'

const FormMessage = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, children, ...props }, ref) => {
  const { error, messageId } = useFormField()
  const body = error?.message ? String(error.message) : children
  if (!body) return null
  return (
    <p
      ref={ref}
      id={messageId}
      className={cn('flex items-center gap-1 text-small text-critical-text', className)}
      {...props}
    >
      <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
      {body}
    </p>
  )
})
FormMessage.displayName = 'FormMessage'

export {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  useFormField,
}
