import { SearchX } from 'lucide-react'
import { Link } from 'react-router'

import { EmptyState } from '@/components/common/EmptyState'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <EmptyState
      icon={SearchX}
      title="No encontramos esta página"
      description="Es posible que el enlace esté mal escrito o que la página ya no exista."
      action={
        <Button asChild variant="outline">
          <Link to="/">Ir al dashboard</Link>
        </Button>
      }
    />
  )
}
