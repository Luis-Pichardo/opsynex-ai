import { Navigate, type RouteObject } from 'react-router'

import { ROLES } from '@/auth/permissions'
import { ProtectedRoute } from '@/auth/ProtectedRoute'
import { RoleGuard } from '@/auth/RoleGuard'
import { AppLayout, type RouteHandle } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import {
  DashboardPage,
  KpisPage,
  MappingsPage,
  ProblemsPage,
  UploadsPage,
} from '@/pages/SectionPages'

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
            { path: 'problems', element: <ProblemsPage />, handle: handle('Problemas') },
            { path: 'kpis', element: <KpisPage />, handle: handle('KPIs') },
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
