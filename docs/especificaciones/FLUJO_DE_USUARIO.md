# Flujo de usuario de Opsynex AI (decisiones acordadas)

Decisiones confirmadas por el responsable del proyecto. Mandan sobre cualquier suposición de
las etapas.

## Decisiones

| # | Tema | Decisión |
|---|---|---|
| 1 | Quién sube los archivos | El **Analyst** (o el Admin). El **Client** solo ve resultados |
| 2 | Revisión de la homologación | El análisis es **automático** al subir el archivo; la revisión de cuentas es **opcional** (el sistema la recomienda cuando hay cuentas dudosas) |
| 3 | Creación de usuarios | **Solo el Admin** crea usuarios y empresas. No hay auto-registro |
| 4 | Exportación | El dashboard y los resultados se **exportan a PDF** (obligatorio en el MVP) |
| 5 | Módulo industrial (OEE, máquinas, scrap, downtime) | **Pendiente de confirmar con el cliente** (etapa 12, después del MVP) |

## Roles

| Rol | Puede |
|---|---|
| Admin | Crear empresas y usuarios, ver todas las empresas, todo lo del Analyst |
| Analyst | Subir archivos, revisar la homologación (opcional), volver a analizar, cambiar el estado de los problemas, descargar el PDF |
| Client | Ver el dashboard, los problemas, los KPIs y las recomendaciones de su empresa, y descargar el PDF |

## Flujo

1. **Alta (Admin, una vez):** crea la empresa (nombre, industria, moneda) y sus usuarios.
2. **Inicio de sesión:** con correo y contraseña; el menú depende del rol.
3. **Carga (Analyst):** sube el libro contable (`.xls`/`.xlsx`) o la plantilla simple.
4. **Procesamiento automático, sin pasos manuales:**
   1. Lee y clasifica las hojas y detecta los años.
   2. Valida la calidad de los datos y genera advertencias.
   3. Homologa las cuentas con las mejores sugerencias.
   4. Consolida los datos y ejecuta el análisis completo: KPIs, diagnóstico, impacto, score y,
      desde la etapa 11, la IA.
5. **Resumen de la carga:** hojas detectadas, advertencias y resultado del análisis (score y
   problemas) con el botón "Ir al dashboard". Si hay cuentas dudosas, se muestra el aviso
   "revisión recomendada".
6. **Revisión opcional (Analyst):** corrige categorías de cuentas y pulsa "Guardar y volver a
   analizar". Los mapeos confirmados se reutilizan en las cargas siguientes de la empresa.
7. **Dashboard (todos):** score, impacto económico, KPIs con variación anual, tendencias, top
   de problemas, recomendaciones y resumen ejecutivo.
8. **Detalle de problema:** descripción, severidad, impacto y su cálculo, histórico y
   recomendaciones; el Analyst cambia el estado (abierto, reconocido, resuelto).
9. **Informe PDF (todos):** "Descargar informe PDF" con score, impacto, KPIs, tendencia,
   problemas, recomendaciones y advertencias de calidad.
10. **Períodos siguientes:** el Analyst sube el libro del nuevo año y el historial crece.

Este flujo cumple la sección 15 de `ESPECIFICACIONES_DEL_SISTEMA` (carga → valida → normaliza
→ almacena → KPIs → diagnóstico → impacto → dashboard) y le agrega el resumen de calidad, la
revisión opcional y el informe PDF.
