# Etapa 09 — Frontend: carga de datos y homologación

**Depende de:** 04 y 08 · **Rama:** `etapa-09-frontend-carga`

## Prompt

> Implementa en el frontend el flujo completo de ingreso de datos: descarga de plantillas,
> subida de archivos con reporte de validación, revisión y confirmación de la homologación
> contable, consolidación y ejecución del análisis.

## Tareas

1. **Página Carga de datos** como asistente por pasos:
   1. Elegir tipo de archivo (con descripción y botón para descargar plantilla).
   2. Arrastrar/soltar archivo (validación de extensión y tamaño en cliente), barra de progreso.
   3. Mostrar resultado de validación: errores por fila/columna en tabla, advertencias y
      vista previa de datos. Permitir volver a subir.
2. **Página Homologación**: tabla de cuentas del cliente → categoría sugerida (Select),
   indicador de confianza (alto/medio/bajo con color), filtro "solo pendientes", aceptar todas
   las sugerencias de confianza alta, guardar y **Consolidar**.
3. Al consolidar, ejecutar el análisis (`POST /analysis/run`) y mostrar un resumen
   (KPIs calculados, problemas detectados, score) con botón "Ir al dashboard".
4. **Historial de cargas**: lista de uploads con estado, fecha, usuario y filas.
5. Vista de **datos consolidados** por año (tabla financiera y operacional) en solo lectura.

## Criterios de aceptación

- [ ] Flujo completo con `backend/sample_data/` desde el navegador: subir → validar →
      homologar → consolidar → ver resumen.
- [ ] Un archivo con errores muestra mensajes claros y no permite continuar.
- [ ] Tests de componentes del asistente y de la tabla de homologación.

## Documentación a actualizar

- `09_guia_usuario.md`: guía paso a paso con el flujo de carga y homologación.
- `06_frontend.md`: páginas y componentes nuevos.
- `12_bitacora.md`.
