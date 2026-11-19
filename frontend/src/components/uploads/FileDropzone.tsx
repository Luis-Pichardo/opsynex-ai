import { CircleAlert, FileSpreadsheet, UploadCloud, X } from 'lucide-react'
import { useId, useRef, useState, type DragEvent } from 'react'

import { Button } from '@/components/ui/button'
import { formatFileSize } from '@/lib/format'
import { ACCEPTED_EXTENSIONS, checkFile, MAX_UPLOAD_MB } from '@/lib/upload-rules'
import { cn } from '@/lib/utils'

interface FileDropzoneProps {
  file: File | null
  onFileChange: (file: File | null) => void
  disabled?: boolean
}

/** Zona para arrastrar y soltar (o elegir) un archivo; valida extensión y tamaño en el cliente. */
export function FileDropzone({ file, onFileChange, disabled }: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const helpId = useId()
  const errorId = useId()
  const [dragging, setDragging] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const pick = (candidate: File | undefined) => {
    if (!candidate) return
    const check = checkFile(candidate)
    if (!check.ok) {
      setError(check.message)
      onFileChange(null)
      return
    }
    setError(null)
    onFileChange(candidate)
  }

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    setDragging(false)
    if (!disabled) pick(event.dataTransfer.files[0])
  }

  return (
    <div className="space-y-2">
      <div
        onDragOver={(event) => {
          event.preventDefault()
          if (!disabled) setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={cn(
          'flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed px-4 py-10 text-center transition-colors duration-150 ease-out',
          dragging ? 'border-primary bg-primary-soft' : 'border-input bg-surface',
          error && 'border-critical',
          disabled && 'cursor-not-allowed opacity-50',
        )}
      >
        {file ? (
          <div className="flex w-full max-w-md items-center gap-3 rounded border border-border bg-surface-muted p-3 text-left">
            <FileSpreadsheet className="size-8 shrink-0 text-primary-text" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-body font-medium text-foreground">{file.name}</p>
              <p className="text-small tabular-nums text-foreground-secondary">
                {formatFileSize(file.size)}
              </p>
            </div>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onFileChange(null)}
              disabled={disabled}
              aria-label="Quitar archivo"
            >
              <X />
            </Button>
          </div>
        ) : (
          <>
            <UploadCloud
              className="size-10 text-foreground-muted"
              strokeWidth={1.5}
              aria-hidden="true"
            />
            <div className="space-y-1">
              <p className="text-body font-medium text-foreground">Arrastra el archivo aquí</p>
              <p id={helpId} className="max-w-prose text-small text-foreground-secondary">
                Sube tu libro de estados financieros tal como lo prepara tu contador (.xls o .xlsx),
                o una plantilla (.xlsx o .csv). Máximo {MAX_UPLOAD_MB} MB.
              </p>
            </div>
            <Button variant="outline" onClick={() => inputRef.current?.click()} disabled={disabled}>
              Elegir archivo
            </Button>
          </>
        )}
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPTED_EXTENSIONS.join(',')}
          className="sr-only"
          tabIndex={-1}
          aria-label="Archivo a subir"
          aria-describedby={error ? `${helpId} ${errorId}` : helpId}
          disabled={disabled}
          onChange={(event) => {
            pick(event.target.files?.[0])
            event.target.value = ''
          }}
        />
      </div>
      {error ? (
        <p
          id={errorId}
          role="alert"
          className="flex items-start gap-1 text-small text-critical-text"
        >
          <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          {error}
        </p>
      ) : null}
    </div>
  )
}
