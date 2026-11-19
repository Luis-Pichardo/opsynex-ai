import { useEffect, useState } from 'react'

/** `true` mientras la media query se cumple (p. ej. "(min-width: 1024px)"). */
export function useMediaQuery(query: string): boolean {
  const get = () => typeof window !== 'undefined' && (window.matchMedia?.(query).matches ?? false)
  const [matches, setMatches] = useState(get)
  useEffect(() => {
    const media = window.matchMedia?.(query)
    if (!media) return
    const onChange = () => setMatches(media.matches)
    onChange()
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [query])
  return matches
}
