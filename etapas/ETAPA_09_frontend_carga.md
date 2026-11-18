# Etapa 09 — Frontend: carga de datos y homologación

**Depende de:** 04 y 08 · **Rama:** `etapa-09-frontend-carga`

## Prompt

> Implementa en el frontend el flujo completo de ingreso de datos, siguiendo
> `docs/diseno/GUIA_DE_DISENO.md`: descarga de plantillas, subida de archivos con reporte de
> validación, revisión y confirmación de la homologación contable, consolidación y ejecución
> del análisis.

## Tareas

1. **Página Carga de datos** como asistente de 3 pasos con indicador de progreso:
   1. Subir el archivo: arrastrar y soltar el **libro contable completo** (`.xls`, `.xlsx`) o la
      plantilla simple (`.xlsx`, `.csv`), con validación de extensión y tamaño en el cliente y
      barra de progreso. Texto de ayuda: "Sube tu libro de estados financieros tal como lo
      prepara tu contador". Enlace secundario "¿No tienes estados financieros? Descarga la plantilla".
   2. **Hojas detectadas:** lista de hojas con el tipo reconocido (Balance general, Estado de
      resultados, Notas, Empleados, Ventas mensuales…), los años encontrados y las filas
      extraídas; las hojas ignoradas aparecen atenuadas con el motivo.
   3. Resultado de la validación: errores bloqueantes y advertencias de calidad (cuadres,
      notas vs. estados) con hoja, fila y columna, y vista previa. Permite volver a subir.
   Caso de prueba manual: el libro sintético de `backend/sample-data/`.
2. **Página Homologación**: tabla de cuenta del cliente (con su hoja y sección) → categoría sugerida (Select),
   indicador de confianza (Alta/Media/Baja con badge e icono), método (IA, fallback,
   confirmado), filtro "solo pendientes", botón "Aceptar sugerencias de confianza alta",
   guardar y **Consolidar**.
3. Al consolidar se ejecuta el análisis y se muestra un resumen (KPIs calculados, problemas
   detectados, score) con el botón "Ir al dashboard".
4. **Historial de cargas**: tabla de uploads con estado (badge), fecha, usuario y filas.
5. **Datos consolidados** por año (tablas financiera y operacional, solo lectura, con `tabular-nums`).

## Criterios de aceptación

- [ ] Flujo completo con `backend/sample-data/` desde el navegador: subir → validar → homologar →
      consolidar → resumen.
- [ ] Un archivo con errores muestra mensajes claros y no permite continuar.
- [ ] Cumple la guía de diseño (sin estilos sueltos), en móvil y en ambos temas.
- [ ] Tests de componentes del asistente y de la tabla de homologación.

## Documentación a actualizar

- `10_guia_usuario.md`: guía paso a paso de la carga y la homologación (con capturas en
  `docs/documentacion/img/`).
- `07_frontend.md`.
- `13_bitacora.md`.
