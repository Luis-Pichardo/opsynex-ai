import { readdirSync, readFileSync, statSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * Criterio de la guía de diseño: ningún color hex, tamaño de fuente arbitrario ni sombra fuera
 * de los tokens en `src/` (salvo `tokens.css`).
 */
const SRC = path.resolve(import.meta.dirname, '..')

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = path.join(dir, name)
    return statSync(full).isDirectory() ? files(full) : [full]
  })
}

const RULES: [string, RegExp][] = [
  ['color hex', /#[0-9a-fA-F]{3,8}\b(?![-\w])/],
  ['color rgb/hsl', /\b(rgba?|hsla?)\(/],
  ['tamaño de fuente arbitrario', /\btext-\[\d/],
  ['sombra arbitraria', /\bshadow-\[/],
  ['font-size en estilo', /fontSize\s*:/],
]

describe('tokens de diseño', () => {
  const sources = files(SRC).filter(
    (f) =>
      /\.(tsx?|css)$/.test(f) &&
      !f.endsWith('tokens.css') &&
      !f.endsWith('.test.ts') &&
      !f.endsWith('.test.tsx'),
  )

  it.each(RULES)('no hay %s en los componentes', (_, pattern) => {
    const offenders = sources.filter((file) => pattern.test(readFileSync(file, 'utf8')))
    expect(offenders.map((f) => path.relative(SRC, f))).toEqual([])
  })

  it('tokens.css es copia exacta de docs/diseno/tokens.css', () => {
    const app = readFileSync(path.join(SRC, 'styles/tokens.css'), 'utf8')
    const guide = readFileSync(path.resolve(SRC, '../../docs/diseno/tokens.css'), 'utf8')
    expect(app).toBe(guide)
  })
})
