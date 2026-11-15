# Etapas del proyecto: cómo usarlas

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
revises. Puedes aprobarlo y hacer merge desde la app móvil de GitHub o desde Claude.

## Stack

C# / ASP.NET Core + SQL Server (backend) · Python / FastAPI (IA) · React + shadcn/ui (frontend).
Diseño: `docs/diseno/GUIA_DE_DISENO.md`.

## Orden y dependencias

```
00 Preparación ─► 01 BD ─► 02 Auth ─► 03 Carga ─► 04 Homologación+IA ─► 05 KPIs ─► 06 Diagnóstico ─► 07 Impacto+Score
                              │                                                                         │
                              └──────────────► 08 Frontend base ─► 09 Frontend carga ─► 10 Dashboard ◄──┘
                                                                                            │
                                                     11 Inteligencia artificial ◄───────────┤
                                                     12 Módulo industrial ◄─────────────────┘
                                                     13 Calidad y demo
                                                     14 Despliegue
```

- **El MVP está listo** al terminar la etapa 10.
- La etapa 08 puede hacerse en paralelo con las etapas 03–07 (en otra sesión), porque solo depende de la 02.
- Las etapas 11 y 12 son independientes entre sí.

## Consejos para trabajar desde el móvil

- Una etapa = una sesión nueva de Claude Code. Así el contexto queda limpio.
- Si la sesión se corta, abre otra y escribe: *"Retoma la etapa N; revisa ESTADO.md y la rama
  etapa-NN"*.
- Haz merge del PR de una etapa antes de empezar la siguiente.
- Para cambios pequeños fuera del plan, pídelos directamente; Claude los anotará en la bitácora.
- Para revisar en Visual Studio: haz `git pull` de `main` y abre `backend/Opsynex.sln`.
