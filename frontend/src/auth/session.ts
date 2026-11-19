import { readStorage, writeStorage } from '@/lib/storage'
import { SESSION_STORAGE_KEY, type Session } from './auth-context'

/** Sesión guardada, o `null` si no hay o ya venció. */
export function loadSession(now = Date.now()): Session | null {
  const raw = readStorage(SESSION_STORAGE_KEY)
  if (!raw) return null
  try {
    const session = JSON.parse(raw) as Session
    if (!session.token || !session.user || isExpired(session, now)) {
      writeStorage(SESSION_STORAGE_KEY, null)
      return null
    }
    return session
  } catch {
    writeStorage(SESSION_STORAGE_KEY, null)
    return null
  }
}

export function saveSession(session: Session | null) {
  writeStorage(SESSION_STORAGE_KEY, session ? JSON.stringify(session) : null)
}

export function isExpired(session: Session, now = Date.now()): boolean {
  const expiresAt = Date.parse(session.expiresAt)
  return Number.isNaN(expiresAt) || expiresAt <= now
}
