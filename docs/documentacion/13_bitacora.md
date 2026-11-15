## Bitácora de desarrollo

Registro de cada etapa completada (la más reciente al final).

### Planificación del proyecto (15/11/2026)

- **Resumen:** análisis de las especificaciones del cliente, definición de 14 etapas,
  protocolo de trabajo con Claude Code y estructura de la documentación.
- **Archivos principales:** `CLAUDE.md`, `ESTADO.md`, `etapas/`, `docs/documentacion/`,
  `scripts/generar_documentacion.py`.
- **Decisiones:** núcleo del MVP sobre el modelo financiero anual; módulo industrial en la
  etapa 11; arquitectura multi-empresa; documentación Word generada desde Markdown.
- **Pendientes:** iniciar la Etapa 00.

### Cambio de stack y guía de diseño (15/11/2026)

- **Resumen:** se cambió el stack a C# / ASP.NET Core + SQL Server para el backend y Python
  para la inteligencia artificial; frontend con React + shadcn/ui. Se creó la guía de diseño
  de interfaz con tokens de color, tipografía y componentes.
- **Archivos principales:** `CLAUDE.md`, `etapas/` (15 etapas), `docs/diseno/`.
- **Decisiones:** servicio de IA separado y *stateless*; nueva etapa 11 de IA (recomendaciones
  y resumen ejecutivo con Claude, detección de anomalías); despliegue con la nube a elegir
  (Azure recomendada para .NET).
- **Pendientes:** iniciar la etapa 00.
