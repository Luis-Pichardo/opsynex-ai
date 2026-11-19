import { Navigate, type RouteObject } from 'react-router'

import { ROLES } from '@/auth/permissions'
import { ProtectedRoute } from '@/auth/ProtectedRoute'
import { RoleGuard } from '@/auth/RoleGuard'
import { AppLayout, type RouteHandle } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { MappingsPage } from '@/pages/MappingsPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { CostsPage } from '@/pages/CostsPage'
import { KpisPage } from '@/pages/KpisPage'
import { ProductionPage } from '@/pages/ProductionPage'
import { ProblemDetailPage } from '@/pages/ProblemDetailPage'
import { ProblemsPage } from '@/pages/ProblemsPage'
import { UploadsPage } from '@/pages/UploadsPage'

const handle = (title: string): RouteHandle => ({ title })

/**
 * Rutas. Todo exige sesión salvo el login; Carga de datos y Homologación exigen Admin o
 * Analyst, y Administración solo Admin (también por URL directa). `/design` solo en desarrollo.
 */
export function buildRoutes(options: { includeDesign: boolean }): RouteObject[] {
  return [
    { path: '/login', element: <LoginPage /> },
    ...(options.includeDesign
      ? [
          {
            path: '/design',
            lazy: async () => ({ Component: (await import('@/pages/DesignPage')).DesignPage }),
          },
        ]
      : []),
    {
      element: <ProtectedRoute />,
      children: [
        {
          element: <AppLayout />,
          children: [
            { index: true, element: <DashboardPage />, handle: handle('Dashboard') },
            { path: 'dashboard', element: <Navigate to="/" replace /> },
            { path: 'problems', element: <ProblemsPage />, handle: handle('Problemas') },
            {
              path: 'problems/:id',
              element: <ProblemDetailPage />,
              handle: handle('Detalle del problema'),
            },
            { path: 'kpis', element: <KpisPage />, handle: handle('KPIs') },
            { path: 'production', element: <ProductionPage />, handle: handle('Producción') },
            { path: 'costs', element: <CostsPage />, handle: handle('Costos') },
            {
              element: <RoleGuard roles={ROLES.analyze} />,
              children: [
                { path: 'uploads', element: <UploadsPage />, handle: handle('Carga de datos') },
                { path: 'mappings', element: <MappingsPage />, handle: handle('Homologación') },
              ],
            },
            {
              path: 'admin',
              element: <RoleGuard roles={ROLES.manage} />,
              handle: handle('Administración'),
              children: [
                {
                  // La administración se carga aparte (solo la usa el Admin).
                  lazy: async () => ({
                    Component: (await import('@/pages/admin/AdminPage')).AdminPage,
                  }),
                  children: [
                    { index: true, element: <Navigate to="companies" replace /> },
                    {
                      path: 'companies',
                      lazy: async () => ({
                        Component: (await import('@/pages/admin/CompaniesPage')).CompaniesPage,
                      }),
                    },
                    {
                      path: 'users',
                      lazy: async () => ({
                        Component: (await import('@/pages/admin/UsersPage')).UsersPage,
                      }),
                    },
                  ],
                },
              ],
            },
            { path: '*', element: <NotFoundPage />, handle: handle('Página no encontrada') },
          ],
        },
      ],
    },
  ]
}
