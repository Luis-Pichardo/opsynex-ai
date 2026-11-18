# Etapa 09 — Frontend: carga de datos y homologación

**Depende de:** 04 y 08 · **Rama:** `etapa-09-frontend-carga`

## Prompt

> Implementa en el frontend el flujo de ingreso de datos descrito en
> `docs/especificaciones/FLUJO_DE_USUARIO.md`, siguiendo `docs/diseno/GUIA_DE_DISENO.md`. El
> analista sube el archivo y el **análisis se ejecuta automáticamente**; la revisión de la
> homologación es **opcional** y se ofrece solo cuando el sistema la recomienda o el analista
> quiere ajustar algo.

## Tareas

1. **Página Carga de datos** como asistente de 3 pasos con indicador de progreso:
   1. Subir el archivo: arrastrar y soltar el **libro contable completo** (`.xls`, `.xlsx`) o la
      plantilla simple (`.xlsx`, `.csv`), con validación de extensión y tamaño en el cliente y
      barra de progreso. Texto de ayuda: "Sube tu libro de estados financieros tal como lo
      prepara tu contador". Enlace secundario "¿No tienes estados financieros? Descarga la plantilla".
   2. **Hojas detectadas:** lista de hojas con el tipo reconocido (Balance general, Estado de
      resultados, Notas, Empleados, Ventas mensuales…), los años encontrados y las filas
      extraídas; las hojas ignoradas aparecen atenuadas con el motivo.
   3. Resultado: errores bloqueantes (no permiten continuar), advertencias de calidad
      (cuadres, notas vs. estados) con hoja, fila y columna, y el **resumen del análisis
      automático** (años analizados, KPIs calculados, problemas detectados, score) con el botón
      "Ir al dashboard". Si `needsReview`, muestra un aviso: "Algunas cuentas se clasificaron
      con baja confianza. Revisa la homologación para mayor precisión" con el botón "Revisar".
   Caso de prueba manual: el libro sintético de `backend/sample-data/`.
2. **Página Homologación (opcional)**: tabla de cuenta del cliente (con su hoja y sección) →
   categoría aplicada (Select editable), indicador de confianza (Alta/Media/Baja con badge e
   icono), método (automático, IA, fallback, confirmado), filtro inicial "solo dudosas", botón
   "Confirmar todas" y botón **"Guardar y volver a analizar"**.
3. Al volver a analizar se muestra el mismo resumen, con lo que cambió (por ejemplo, "Score
   64 → 67").
4. **Historial de cargas**: tabla de uploads con estado (badge), fecha, usuario y filas.
5. **Datos consolidados** por año (tablas financiera y operacional, solo lectura, con `tabular-nums`).

## Criterios de aceptación

- [ ] Flujo con `backend/sample-data/` desde el navegador: subir → resumen del análisis →
      dashboard, **sin pasos manuales intermedios**.
- [ ] Con la variante que tiene cuentas desconocidas aparece el aviso de revisión; al
      corregirlas y volver a analizar, el resumen se actualiza.
- [ ] Un archivo con errores muestra mensajes claros y no permite continuar.
- [ ] Cumple la guía de diseño (sin estilos sueltos), en móvil y en ambos temas.
- [ ] Tests de componentes del asistente y de la tabla de homologación.

## Documentación a actualizar

- `10_guia_usuario.md`: guía paso a paso de la carga y la homologación (con capturas en
  `docs/documentacion/img/`).
- `07_frontend.md`.
- `13_bitacora.md`.
