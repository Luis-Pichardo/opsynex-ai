import { QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router'

import { AuthProvider } from '@/auth/AuthProvider'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { createQueryClient } from '@/lib/query-client'
import { buildRoutes } from '@/router/routes'
import { ThemeProvider } from '@/theme/ThemeProvider'
import { WorkspaceProvider } from '@/workspace/WorkspaceProvider'

const router = createBrowserRouter(buildRoutes({ includeDesign: import.meta.env.DEV }))

export function App() {
  const [queryClient] = useState(createQueryClient)

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <WorkspaceProvider>
            <TooltipProvider delayDuration={300}>
              <RouterProvider router={router} />
              <Toaster />
            </TooltipProvider>
          </WorkspaceProvider>
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  )
}
