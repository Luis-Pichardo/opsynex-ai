import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'

import { HomePage } from '@/pages/HomePage'

export function App() {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { refetchOnWindowFocus: false } } }),
  )

  return (
    <QueryClientProvider client={queryClient}>
      <HomePage />
    </QueryClientProvider>
  )
}
