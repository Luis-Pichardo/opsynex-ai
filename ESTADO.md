# Estado del proyecto Opsynex AI

Leyenda: ⬜ pendiente · 🟡 en progreso · ✅ completada

| # | Etapa | Archivo | Estado | Fecha | PR / Notas |
|---|---|---|---|---|---|
| 00 | Preparación del entorno y estructura | `etapas/ETAPA_00_preparacion.md` | ⬜ | | |
| 01 | Base de datos y modelos (EF Core + SQL Server) | `etapas/ETAPA_01_base_de_datos.md` | ⬜ | | |
| 02 | Autenticación y roles | `etapas/ETAPA_02_autenticacion.md` | ⬜ | | |
| 03 | Carga y validación de datos | `etapas/ETAPA_03_carga_de_datos.md` | ⬜ | | |
| 04 | Homologación contable + servicio de IA | `etapas/ETAPA_04_homologacion.md` | ⬜ | | |
| 05 | Motor de KPIs | `etapas/ETAPA_05_motor_kpis.md` | ⬜ | | |
| 06 | Motor de diagnóstico | `etapas/ETAPA_06_motor_diagnostico.md` | ⬜ | | |
| 07 | Impacto financiero y score | `etapas/ETAPA_07_impacto_y_score.md` | ⬜ | | |
| 08 | Frontend base y sistema de diseño | `etapas/ETAPA_08_frontend_base.md` | ⬜ | | |
| 09 | Frontend: carga de datos y homologación | `etapas/ETAPA_09_frontend_carga.md` | ⬜ | | |
| 10 | Dashboard ejecutivo y problemas (cierre MVP) | `etapas/ETAPA_10_dashboard.md` | ⬜ | | |
| 11 | Inteligencia artificial: recomendaciones, anomalías y resumen | `etapas/ETAPA_11_inteligencia_artificial.md` | ⬜ | | |
| 12 | Módulo industrial | `etapas/ETAPA_12_modulo_industrial.md` | ⬜ | | |
| 13 | Seguridad, calidad y datos demo | `etapas/ETAPA_13_calidad_y_demo.md` | ⬜ | | |
| 14 | Despliegue en la nube | `etapas/ETAPA_14_despliegue.md` | ⬜ | | |

**Hito MVP:** etapas 00–10 completas (cumple la "Definición MVP" de las especificaciones).

## Notas del entorno

- Contenedor de Claude Code en la nube (revisado el 15/11/2026): Ubuntu 24.04, **sin .NET SDK
  instalado** y sin el daemon de Docker en ejecución (el cliente `docker` existe, pero
  `/var/run/docker.sock` no). Responden `packages.microsoft.com`, `api.nuget.org` y
  `mcr.microsoft.com`; `builds.dotnet.microsoft.com` no respondió. Python 3, Node, pandoc y
  LibreOffice sí están.
- (Claude: anota aquí todo lo que la próxima sesión deba saber: cómo se instaló .NET, cómo se
  ejecutan la BD y los tests en la nube, las variables necesarias y los problemas conocidos.)
