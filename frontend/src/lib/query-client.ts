import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { ApiError, errorMessage } from '@/api/client'

declare module '@tanstack/react-query' {
  interface Register {
    queryMeta: { silentError?: boolean }
    mutationMeta: { silentError?: boolean }
  }
}

/** Los errores del API se muestran como toast (los 401 los maneja la sesión). */
function notify(error: unknown, silent?: boolean) {
  if (silent || (error instanceof ApiError && error.status === 401)) return
  toast.error(errorMessage(error), { duration: Infinity })
}

export function createQueryClient() {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error, query) => notify(error, query.meta?.silentError),
    }),
    mutationCache: new MutationCache({
      onError: (error, _vars, _ctx, mutation) => notify(error, mutation.meta?.silentError),
    }),
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
      },
    },
  })
}
