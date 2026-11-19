# Guía de diseño de interfaz — Opsynex AI

Fuente única de verdad visual del frontend. **Todo componente, página o gráfico debe usar
estos tokens.** Prohibido escribir colores hex, tamaños de fuente o sombras sueltas en los
componentes: se usan las variables de `docs/diseno/tokens.css` (copiadas a
`frontend/src/styles/tokens.css`) a través de las clases de Tailwind configuradas.

Vista previa visual: abre `docs/diseno/guia_visual.html` en el navegador.

---

## 1. Principios

1. **Claridad ejecutiva.** Un gerente debe entender en 5 segundos si la empresa está bien o
   mal: primero el score, luego los problemas con mayor impacto en dinero y al final el detalle.
2. **El dinero manda.** Toda ineficiencia se muestra con su impacto económico. Las cifras
   monetarias son el elemento tipográfico más destacado después del score.
3. **Calma visual.** Fondos neutros, un solo color de marca y color semántico **solo** donde
   significa algo (severidad, tendencia, score). Si todo resalta, nada resalta.
4. **Consistencia.** El mismo dato se ve igual en todas partes: formato, color, icono y posición.
5. **Accesible por defecto.** El color nunca comunica solo: siempre va con icono, texto o forma.
   Contraste mínimo AA (4.5:1 en texto normal).

---

## 2. Color

### 2.1 Marca y neutros (UI)

| Token | Uso | Claro | Oscuro |
|---|---|---|---|
| `--background` | Fondo de página | `#F9F9F7` | `#0D0D0D` |
| `--surface` | Tarjetas, tablas, modales | `#FFFFFF` | `#1A1A19` |
| `--surface-muted` | Fondo de cabeceras de tabla, inputs deshabilitados, hover | `#F2F2EF` | `#232322` |
| `--sidebar` | Barra lateral (siempre oscura) | `#141C3D` | `#0F1530` |
| `--sidebar-foreground` | Texto en sidebar | `#F0EFEC` | `#F0EFEC` |
| `--sidebar-muted` | Iconos y texto secundario del sidebar | `#A9B4E8` | `#A9B4E8` |
| `--border` | Bordes y divisores (1px) | `#E1E0D9` | `#2C2C2A` |
| `--input` | Borde de inputs | `#C3C2B7` | `#383835` |
| `--foreground` | Texto principal | `#0B0B0B` | `#F0EFEC` |
| `--foreground-secondary` | Texto secundario, descripciones | `#52514E` | `#C3C2B7` |
| `--foreground-muted` | Placeholders, metadatos, ejes (texto ≥ 12px no esencial) | `#6E6C66` | `#9A9891` |
| `--primary` | Botón principal, enlaces, foco, ítem activo | `#3B4FB8` | `#3B4FB8` |
| `--primary-hover` | Hover del botón principal | `#2F3F95` | `#5366CF` |
| `--primary-foreground` | Texto sobre `--primary` | `#FFFFFF` | `#FFFFFF` |
| `--primary-soft` | Fondo de selección, chips activos | `#EEF1FB` | `#1E2547` |
| `--primary-text` | Enlaces y texto de marca sobre superficie | `#3B4FB8` | `#8B9BF0` |
| `--ring` | Anillo de foco (2px, offset 2px) | `#3B4FB8` | `#8B9BF0` |

Reglas:
- Solo **un** botón primario por vista o sección. El resto son `secondary`, `outline` o `ghost`.
- El color de marca **no** se usa para datos. Los gráficos usan la paleta de la sección 2.4.

### 2.2 Estado y severidad (semánticos, reservados)

Estos colores solo se usan para su significado. Nunca como decoración ni como color de series.

| Significado | Token base | Base | Texto sobre fondo suave (claro) | Fondo suave (claro) | Texto (oscuro) | Fondo suave (oscuro) | Icono (lucide) |
|---|---|---|---|---|---|---|---|
| Crítico / severidad **Alta** | `--critical` | `#D03B3B` | `#B42318` | `#FDECEC` | `#F5A3A3` | `#3A1A1A` | `octagon-alert` |
| Serio / severidad **Media** | `--serious` | `#EC835A` | `#B54708` | `#FEF3E2` | `#FCC98A` | `#3A2A12` | `triangle-alert` |
| Advertencia / severidad **Baja** (monitoreo) | `--warning` | `#FAB219` | `#8A5A00` | `#FFF7E0` | `#FFE08A` | `#3A3212` | `eye` |
| Bueno / saludable | `--good` | `#0CA30C` | `#067647` | `#E8F7EE` | `#7FD9A0` | `#12301F` | `circle-check` |
| Informativo | `--info` | `#3B4FB8` | `#1D3A8A` | `#EEF1FB` | `#A9B4E8` | `#1E2547` | `info` |

**Badge de severidad** = fondo suave + texto oscuro del mismo tono + icono + palabra
("Alta", "Media", "Baja"). Ejemplo: `[⬢ Alta]` en rojo. Nunca un punto de color solo.

### 2.3 Score Opsynex (0–100)

| Rango | Interpretación | Color | Etiqueta |
|---|---|---|---|
| 80–100 | Operación saludable | `--good` | "Saludable" |
| 60–79 | Oportunidades de mejora | `--warning` | "Mejorable" |
| 0–59 | Deterioro operacional significativo | `--critical` | "Deterioro" |

El medidor del score usa el color del rango en el arco. El número va en `--foreground`, no
en el color del rango.

### 2.4 Paleta de gráficos (validada para daltonismo)

Asignación **en este orden fijo**, nunca en ciclo. El color sigue a la entidad: si
"Revenue" es azul en un gráfico, es azul en todos.

| Slot | Tono | Claro | Oscuro | Asignación fija sugerida |
|---|---|---|---|---|
| 1 | Azul | `#2A78D6` | `#3987E5` | Revenue / serie principal |
| 2 | Naranja | `#EB6834` | `#D95926` | Costo total / COGS |
| 3 | Aqua | `#1BAF7A` | `#199E70` | EBITDA |
| 4 | Amarillo | `#EDA100` | `#C98500` | Gastos operacionales |
| 5 | Magenta | `#E87BA4` | `#D55181` | Overhead |
| 6 | Verde | `#008300` | `#0CA30C` | Energía |
| 7 | Violeta | `#6250D6` | `#9085E9` | Mantenimiento |
| 8 | Rojo | `#E34948` | `#E66767` | Material loss |

**Asignación del módulo industrial** (páginas Producción y Costos, `INDUSTRIAL_SERIES`):

| Entidad | Slot |
|---|---|
| OEE / costo estándar (serie de referencia) | 1 |
| Costo real por unidad / minutos de parada | 2 |
| Disponibilidad | 3 |
| Materiales | 4 |
| Gastos generales (overhead) | 5 |
| Energía | 6 |
| Rendimiento / mano de obra | 7 |
| Calidad | 8 |

Rendimiento y mano de obra comparten el slot 7, pero nunca aparecen en la misma página.

- **Estado frente a un umbral** (OEE < 60 %, scrap > 5 %, paradas > 15 %, costo > estándar +
  10 %): badge con icono y texto («Bajo el 60 %», «Sobre el 15 %», «En rango»), alineado al
  inicio (no ocupa todo el ancho de la tarjeta). Solo es crítico lo que dispara un problema.
- Máximo 8 series; más de eso se agrupa en "Otros".
- En dispersión, mapas de calor por categoría o *small multiples*, máximo **3** series.
- Magnitud (mapas de calor): una sola escala azul de `#CDE2FB` (bajo) a `#0D366B` (alto).
- Positivo vs. negativo (variaciones): azul ↔ rojo con gris neutro en el centro
  (`#F0EFEC` claro, `#383835` oscuro).
- Elementos del gráfico: superficie `#FCFCFB` / `#1A1A19`; líneas de grilla `#E1E0D9` /
  `#2C2C2A` (horizontales solamente); ejes `#C3C2B7` / `#383835`; textos de ejes en
  `--foreground-muted`.

### 2.5 Tendencias (variación año contra año)

| Caso | Color de texto | Icono |
|---|---|---|
| Variación **favorable** (sube un KPI donde subir es bueno, o baja uno donde bajar es bueno) | `--good` texto (`#067647` / `#7FD9A0`) | `trending-up` o `trending-down` según dirección real |
| Variación **desfavorable** | `--critical` texto (`#B42318` / `#F5A3A3`) | según dirección real |
| Estable (±1 %) | `--foreground-secondary` | `minus` |

La flecha indica la **dirección** del valor; el color indica si es **bueno o malo**. Ejemplo:
"Cost efficiency ▼ 3,2 %" va en verde, porque bajar el costo es favorable.

---

## 3. Tipografía

- **Fuente:** `Inter` (Google Fonts, pesos 400, 500, 600 y 700), con respaldo
  `system-ui, -apple-system, "Segoe UI", sans-serif`.
- **Cifras:** siempre `font-variant-numeric: tabular-nums` (clase `tabular-nums`) en tablas,
  KPIs y montos, para que los dígitos queden alineados.
- **Código y IDs:** `JetBrains Mono` o `ui-monospace`, solo en Administración y logs.

| Token | Tamaño / interlineado | Peso | Uso |
|---|---|---|---|
| `display` | 48 / 56 px | 700 | Score principal del dashboard |
| `h1` | 30 / 36 px | 600 | Título de página (uno por página) |
| `h2` | 22 / 28 px | 600 | Título de sección |
| `h3` | 18 / 26 px | 600 | Título de tarjeta |
| `kpi` | 28 / 34 px | 600 | Valor de un KPI o monto en una tarjeta |
| `body` | 15 / 24 px | 400 | Texto general |
| `body-strong` | 15 / 24 px | 500 | Etiquetas destacadas, celdas clave |
| `small` | 13 / 20 px | 400 | Descripciones, ayuda bajo inputs, celdas secundarias |
| `caption` | 12 / 16 px | 500 | Metadatos, ejes de gráficos, badges (mayúscula inicial, nunca todo en mayúsculas salvo en encabezados de tabla) |
| `overline` | 11 / 16 px, `letter-spacing: 0.06em`, MAYÚSCULAS | 600 | Encabezados de tabla y etiquetas de tarjeta KPI |

Reglas: máximo **3 tamaños** distintos por tarjeta; texto de párrafo con máximo ~75
caracteres por línea; nunca menos de 12 px.

---

## 4. Espaciado, forma y elevación

- **Escala de espaciado (múltiplos de 4 px):** 4, 8, 12, 16, 20, 24, 32, 40, 48, 64.
  - Padding de tarjeta: 20 px (móvil 16 px). Separación entre tarjetas: 16 px (móvil 12 px).
  - Separación entre secciones de una página: 32 px.
  - Márgenes laterales de página: 32 px escritorio, 16 px móvil.
- **Radios:** `--radius-sm` 6 px (badges, inputs pequeños), `--radius` 10 px (botones,
  inputs, tarjetas), `--radius-lg` 14 px (modales, panel de score), `full` (avatares, pills).
- **Bordes:** 1 px `--border`. Las tarjetas se separan del fondo con borde y sombra mínima.
- **Sombras:**
  - `--shadow-sm` `0 1px 2px rgba(16,24,40,0.06)`: tarjetas.
  - `--shadow-md` `0 4px 12px rgba(16,24,40,0.08)`: dropdowns y popovers.
  - `--shadow-lg` `0 12px 32px rgba(16,24,40,0.14)`: modales.
  - En modo oscuro las sombras casi no se ven: se apoyan en el borde.

---

## 5. Layout

- **Sidebar** fija de 256 px (colapsada a 72 px, solo iconos) con fondo `--sidebar` en
  ambos modos. El ítem activo lleva fondo `rgba(255,255,255,0.08)`, una barra izquierda de
  3 px `--primary` y texto blanco.
- **Topbar** de 64 px sobre `--surface` con borde inferior: título de la página, selector de
  empresa (Admin), selector de año, botón de tema y menú de usuario.
- **Contenido** con ancho máximo de 1440 px, en grilla de 12 columnas y gap de 16 px.
- **Breakpoints:** `sm` 640, `md` 768, `lg` 1024, `xl` 1280, `2xl` 1536. Por debajo de
  `lg` la sidebar se convierte en un drawer con botón de hamburguesa.
- Por debajo de `md`, los selectores de empresa y año pasan a una segunda fila de la topbar.
- Un ítem del menú aún no disponible se muestra con opacidad del 50 % y un tooltip que lo
  explica (no se oculta).
- **Login:** pantalla dividida en `lg` (panel de marca con `--sidebar` a la izquierda y
  formulario a la derecha); en móvil el panel queda como una franja superior con el logo.
- **Orden del dashboard:** (1) Score + resumen de impacto, (2) fila de KPIs, (3) tendencias,
  (4) principales problemas + recomendaciones.

---

## 6. Componentes (shadcn/ui)

Base: **shadcn/ui** (componentes de Radix + Tailwind copiados al proyecto en
`src/components/ui/`) + iconos **lucide-react** + gráficos **Recharts**. Se personalizan
solo mediante tokens; no se crean variantes ad hoc.

| Componente | Especificación |
|---|---|
| **Button** | Altura 40 px (sm 32, lg 44); padding horizontal 16 px; radio 10 px; peso 500. Variantes: `default` (primary), `secondary` (surface-muted), `outline`, `ghost`, `destructive` (critical), `link`. Icono de 16 px a la izquierda. Estado de carga: spinner y texto "Procesando…". |
| **Input / Select** | Altura 40 px, borde `--input`, foco con ring de 2 px `--ring`. Label arriba (`small`, peso 500); ayuda o error abajo (`small`; error en texto critical con icono). |
| **Card** | Superficie, borde y `--shadow-sm`, radio 10 px, padding 20 px. Cabecera: título `h3` + acción opcional a la derecha. |
| **StatCard (KPI)** | Etiqueta `overline` → valor `kpi` con `tabular-nums` → variación YoY con icono y color (sección 2.5) → sparkline opcional de 32 px de alto con color slot 1. |
| **Badge** | Altura 22 px, radio 6 px, `caption`, fondo suave + texto oscuro + icono de 12 px. |
| **Table** | Cabecera `overline` sobre `--surface-muted`; filas de 48 px; hover `--surface-muted`; números alineados a la derecha con `tabular-nums`; paginación abajo a la derecha; columna de acciones a la derecha. |
| **Tabs** | Subrayado de 2 px `--primary` en la pestaña activa. |
| **Dialog / Sheet** | Radio 14 px, `--shadow-lg`, overlay `--overlay` (`rgba(13,13,13,0.5)`). Botones abajo a la derecha: secundario, luego primario. |
| **Toast** (sonner) | Arriba a la derecha (abajo en móvil); icono semántico; duración de 4 s; los errores no se cierran solos. |
| **Skeleton** | Para estados de carga (nunca una pantalla en blanco): bloques `--surface-muted` con animación pulse. |
| **EmptyState** | Icono lucide de 40 px en `--foreground-muted`, título `h3`, descripción `small` y botón de acción. |
| **Tooltip** | Fondo `--foreground` y texto `--background`, `caption`, radio 6 px. |
| **ScoreGauge** | Arco de 240° y grosor de 12 px: pista en `--surface-muted`, progreso en el color del rango. Número `display` al centro y etiqueta del rango debajo. |

---

## 7. Gráficos (Recharts)

- **Tipo según la pregunta:** evolución en el tiempo → líneas; comparación entre categorías →
  barras horizontales ordenadas; composición → barras apiladas (no tortas de más de 4 partes);
  un solo número → StatCard, no un gráfico.
- **Nunca** un gráfico con dos ejes Y. Para dos medidas con escalas distintas se hacen dos
  gráficos o se indexan a una base común.
- Líneas de 2 px; puntos de 8 px solo al pasar el cursor o en el último valor; barras con
  esquinas de 4 px en el extremo y 2 px de separación entre barras.
- Grilla horizontal tenue, sin grilla vertical; ejes sin línea salvo la base.
- **Tooltip siempre activo:** en líneas, crosshair vertical con todas las series del punto;
  en barras, el valor de la barra. Valores con formato (sección 9).
- **Leyenda** siempre que haya 2 o más series, arriba a la izquierda. Con hasta 4 series,
  etiquetas directas al final de las líneas.
- Los textos de valores y leyendas van en colores de texto, **nunca** en el color de la serie.
- Cada gráfico tiene título (`h3`), subtítulo con unidad y período (`small`), y un enlace
  "Ver tabla" con los mismos datos.
- Modo oscuro: usar la columna "Oscuro" de la paleta, no invertir colores.

---

## 8. Interacción y estados

- **Foco visible** en todo elemento interactivo: ring de 2 px `--ring` con offset de 2 px.
- **Hover:** cambio de fondo sutil (`--surface-muted`) o `--primary-hover`. Sin cambios de tamaño.
- **Transiciones:** 150 ms `ease-out` en colores y opacidad; 200 ms en drawers y modales.
  Respetar `prefers-reduced-motion`.
- **Deshabilitado:** opacidad del 50 % y `cursor-not-allowed`; explicar el motivo con un tooltip.
- **Carga:** skeletons en tarjetas y tablas; spinner solo dentro de botones.
- **Errores:** mensaje cercano al origen, en lenguaje claro y con la acción para resolverlo
  ("El archivo no tiene la columna «2024». Descarga la plantilla y vuelve a intentarlo.").
- **Confirmación** solo para acciones destructivas (diálogo con botón `destructive`).
- **Objetivos táctiles:** mínimo 44×44 px en móvil.

---

## 9. Contenido y formato de datos

- Interfaz en **español**, tuteo neutro ("Sube tu archivo"), frases cortas y verbos de acción
  en los botones ("Subir archivo", "Confirmar mapeo", "Ejecutar análisis").
- Los nombres de KPI se muestran en español con el término técnico como ayuda:
  "Margen EBITDA" (tooltip: "EBITDA / Ingresos").
- **Moneda:** `Intl.NumberFormat('es', { style: 'currency', currency })` según la moneda de
  la empresa. En tarjetas, formato compacto: "$1,2 M", "$350 K".
- **Porcentajes:** 1 decimal ("12,4 %"). Variaciones de margen en **puntos porcentuales**
  ("−2,3 pp").
- **Fechas:** "15 nov 2026"; período anual: "2024".
- Valores vacíos o no calculables: "—" con un tooltip que explique el motivo.
- Utilidades únicas en `src/lib/format.ts` (`formatCurrency`, `formatPercent`, `formatPp`,
  `formatNumber`, `formatDate`). Prohibido formatear números a mano en los componentes.

---

## 10. Accesibilidad (checklist por pantalla)

- [ ] Contraste AA (4,5:1) en todo el texto, también `--foreground-muted` sobre `--surface-muted`
  (desde la etapa 13 el token se oscureció en claro y se aclaró en oscuro para cumplirlo).
- [ ] La severidad, las tendencias y el score llevan texto o icono además del color.
- [ ] Navegación completa con teclado; orden lógico; `Esc` cierra modales.
- [ ] Inputs con `<label>`; errores asociados con `aria-describedby`.
- [ ] Gráficos con `aria-label` descriptivo y enlace "Ver tabla".
- [ ] Funciona a 375 px de ancho sin scroll horizontal (las tablas tienen scroll propio).
- [ ] Modo claro y oscuro revisados.
