import { createContext } from 'react'

import type { UserDto } from '@/types/api'

export interface Session {
  token: string
  expiresAt: string
  user: UserDto
}

export type LogoutReason = 'user' | 'expired'

export interface AuthContextValue {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<UserDto>
  logout: (reason?: LogoutReason) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export const SESSION_STORAGE_KEY = 'opsynex.session'
