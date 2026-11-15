# Etapas del proyecto — cómo usarlas

Cada archivo `ETAPA_NN_*.md` contiene el **prompt** completo de una etapa, sus tareas, los
criterios de aceptación y qué documentación actualizar. Claude Code ya conoce el protocolo
(está en `CLAUDE.md`), así que basta con pedirle:

```
Continúa con la etapa 3
```

o simplemente:

```
Sigue con la siguiente etapa
```

Claude leerá `ESTADO.md`, abrirá el archivo de la etapa, la implementará en una rama nueva,
correrá las pruebas, actualizará la documentación Word y abrirá un Pull Request para que lo
revises (puedes aprobarlo y hacer merge desde la app móvil de GitHub o desde Claude).

## Orden y dependencias

```
00 Preparación ─► 01 BD ─► 02 Auth ─► 03 Carga ─► 04 Homologación ─► 05 KPIs ─► 06 Diagnóstico ─► 07 Impacto+Score
                                │                                                                       │
                                └──────────────► 08 Frontend base ─► 09 Frontend carga ─► 10 Dashboard ◄┘
                                                                                             │
                                                                     11 Módulo industrial ◄──┘
                                                                     12 Calidad y demo
                                                                     13 Despliegue
```

- **MVP listo** al terminar la etapa 10.
- La etapa 08 puede hacerse en paralelo con 03–07 (otra sesión), porque solo depende de la 02.

## Consejos para trabajar desde el móvil

- Una etapa = una sesión nueva de Claude Code. Así el contexto queda limpio.
- Si la sesión se corta, abre otra y di: *"Retoma la etapa N; revisa ESTADO.md y la rama
  etapa-NN"*.
- Haz merge del PR de una etapa antes de empezar la siguiente.
- Para cambios pequeños fuera del plan, pídelos directamente; Claude anotará en la bitácora.
