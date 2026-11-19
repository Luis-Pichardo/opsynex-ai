import { Navigate, Outlet, useLocation } from 'react-router'

import { useAuth } from './useAuth'

/** Exige sesión; si no hay, lleva al login y recuerda a dónde volver. */
export function ProtectedRoute() {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  if (!isAuthenticated) {
    return (
      <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
    )
  }
  return <Outlet />
}
