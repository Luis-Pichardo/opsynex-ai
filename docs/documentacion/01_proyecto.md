# Parte I: Documentación del proyecto

## Descripción general

Opsynex AI es una plataforma SaaS en la nube de analítica financiero-operacional. Identifica
ineficiencias dentro de las organizaciones analizando automáticamente su información
financiera y operacional, cuantifica su impacto económico y apoya la toma de decisiones
ejecutivas.

No es solo un dashboard de reporting: su base es un **motor analítico** que traduce
ineficiencias operacionales en impacto financiero medible. Su objetivo central es ayudar a las
organizaciones a entender **dónde se destruye la rentabilidad y qué factores operacionales o
financieros son responsables**.

## Problema que resuelve

La mayoría de las organizaciones tienen información financiera, pero no la capacidad analítica para:

- detectar ineficiencias ocultas;
- conectar el desempeño operacional con la rentabilidad;
- identificar las fuentes de deterioro del margen;
- cuantificar el impacto económico de las ineficiencias;
- priorizar los problemas financiero-operacionales.

## Objetivos

**Objetivo principal:** crear una plataforma inteligente de diagnóstico financiero-operacional
capaz de analizar datos organizacionales y generar insights orientados a optimizar la rentabilidad.

**Objetivos específicos:**

- Centralizar información financiera y operacional.
- Calcular KPIs estratégicos automáticamente.
- Detectar ineficiencias empresariales.
- Cuantificar el impacto económico.
- Generar un score operacional-financiero.
- Proveer dashboards ejecutivos.
- Apoyar la toma de decisiones estratégicas.

## Mercado objetivo (MVP)

| Segmento | Ejemplos |
|---|---|
| Empresas manufactureras | Alimentos, bienes de consumo, producción industrial, empaques, textiles, manufactura plástica |
| Empresas de servicios | Consultoras, outsourcing, operadores logísticos, centros de servicios, servicios administrativos, tecnológicas |

## Componentes de la plataforma

| Área | Propósito |
|---|---|
| Analítica financiera | Evaluar rentabilidad y estructura de costos |
| Analítica operacional | Identificar ineficiencias operativas |
| Motor de diagnóstico | Detectar patrones de deterioro empresarial |
| Sistema de scoring | Medir la salud operacional-financiera |
| Motor de recomendaciones | Sugerir oportunidades de mejora |

## Alcance del MVP

El MVP se considera funcional cuando el sistema:

- carga archivos financieros correctamente;
- calcula KPIs automáticamente;
- genera diagnósticos financiero-operacionales (al menos 3 tipos de problemas);
- estima el impacto financiero;
- muestra dashboards correctamente;
- genera el score operacional-financiero.

### Decisión de alcance sobre los modelos de datos

Las especificaciones del cliente incluyen dos enfoques complementarios: un modelo financiero
**anual** (estados financieros más datos operacionales por año) y un modelo **industrial** (datos
de producción y costos por fecha, máquina y línea, con OEE, scrap y downtime). El núcleo del MVP
se construye sobre el modelo anual (etapas 3 a 10). El módulo industrial se agrega como
extensión en la etapa 11 y comparte usuarios, empresas, diagnóstico, impacto y score.

## Decisiones de tecnología

Las especificaciones sugieren Python + FastAPI, React + Tailwind, PostgreSQL y AWS/GCP, y
permiten que el programador use el lenguaje de su preferencia. Se decidió:

| Capa | Tecnología elegida |
|---|---|
| Backend principal (API) | C# con ASP.NET Core (.NET 10) y Entity Framework Core |
| Base de datos | SQL Server |
| Inteligencia artificial | Servicio en Python (FastAPI) con Claude (Anthropic) |
| Frontend | React + TypeScript + Tailwind CSS + shadcn/ui |
| Nube | Azure, AWS o GCP (se decide antes del despliegue) |

La interfaz sigue una guía de diseño propia (`docs/diseno/GUIA_DE_DISENO.md`) con colores,
tipografía y componentes uniformes.

## Plan de trabajo por etapas

| # | Etapa | Resultado |
|---|---|---|
| 00 | Preparación del entorno | Solución .NET, servicio de IA, frontend y CI |
| 01 | Base de datos y modelos | Esquema SQL Server con EF Core y migraciones |
| 02 | Autenticación y roles | Login JWT, roles Admin/Analyst/Client |
| 03 | Carga y validación de datos | Subida de Excel/CSV con validaciones |
| 04 | Homologación contable | Mapeo de cuentas con sugerencias del servicio de IA |
| 05 | Motor de KPIs | Cálculo, tendencias y normalización |
| 06 | Motor de diagnóstico | Reglas, severidad y priorización |
| 07 | Impacto financiero y score | Pérdidas estimadas y score 0–100 |
| 08 | Frontend base | Sistema de diseño, autenticación y navegación |
| 09 | Frontend de carga | Asistente de carga y homologación |
| 10 | Dashboard (cierre del MVP) | Dashboard ejecutivo y problemas |
| 11 | Inteligencia artificial | Recomendaciones y resumen ejecutivo con IA, anomalías |
| 12 | Módulo industrial | OEE, scrap, downtime y costo por unidad |
| 13 | Calidad y demo | Seguridad, pruebas E2E y datos demo |
| 14 | Despliegue | Producción en la nube |

## Estado actual

Etapa 00 completada (15/11/2026): el repositorio tiene la estructura base del monorepo con
el API .NET, el servicio de IA y el frontend funcionando, cada uno con su endpoint o pantalla
de salud, pruebas automáticas, linters, imágenes Docker e integración continua. Todavía no
hay lógica de negocio.

Etapa 01 completada (15/11/2026): modelo de datos en SQL Server con EF Core (10 tablas,
migración inicial, filtro por empresa, repositorios y pruebas contra SQL Server real). La
siguiente etapa es la autenticación y los roles (Etapa 02).

Etapa 02 completada (18/11/2026): inicio de sesión con JWT, contraseñas con hash seguro,
bloqueo temporal tras 5 intentos fallidos, roles Admin / Analyst / Client con aislamiento por
empresa, administración de empresas y usuarios (solo Admin, sin auto-registro) y creación del
primer Admin. La siguiente etapa es la carga y validación de datos (Etapa 03).

Etapa 03 completada (18/11/2026): el sistema acepta el libro contable real de los clientes
(`.xls`/`.xlsx`) y plantillas simples; reconoce cada hoja por su contenido, extrae los estados
financieros, notas, nómina (solo conteos), ventas mensuales y balance de comprobación, y reporta
las inconsistencias de calidad como advertencias. La siguiente etapa es la homologación contable
con el servicio de IA (Etapa 04).

Etapa 04 completada (18/11/2026): al subir un archivo, el sistema homologa las cuentas
automáticamente con el servicio de IA, consolida los estados financieros de todos los años del
archivo y avisa cuando conviene revisar cuentas dudosas. El analista puede corregir los mapeos,
que se recuerdan para las cargas siguientes. Sin IA, el análisis funciona con un diccionario
básico. La siguiente etapa es el motor de KPIs (Etapa 05).

Etapa 05 completada (18/11/2026): el sistema calcula automáticamente 14 KPIs por año
(rentabilidad, eficiencia de costos, productividad y estabilidad financiera), sus tendencias
frente al año anterior, una versión normalizada de 0 a 100 con referencias por industria y moneda,
y la serie mensual de ventas. La siguiente etapa es el motor de diagnóstico (Etapa 06).

Etapa 06 completada (19/11/2026): un motor de 13 reglas configurables detecta automáticamente
ineficiencias (caída de márgenes, costos que crecen más que las ventas, baja productividad,
liquidez y endeudamiento), asigna su severidad, las prioriza, propone una recomendación base y
registra también los hallazgos positivos. El analista puede marcar cada problema como reconocido
o resuelto. La siguiente etapa es el impacto financiero y el score (Etapa 07).

Etapa 07 completada (19/11/2026): cada problema tiene ahora una pérdida económica estimada, con
el margen que representa y la magnitud del deterioro, y la empresa recibe un Score Opsynex de 0 a
100 por año (rentabilidad, eficiencia de costos, productividad y estabilidad financiera) con su
interpretación. Las recomendaciones incluyen las cifras del problema y su impacto. Con esto el
backend del MVP está completo. La siguiente etapa es el frontend base (Etapa 08).

Etapa 08 completada (19/11/2026): ya existe la interfaz web base. Incluye el inicio de sesión, el
menú y la barra superior según el rol, el modo claro y oscuro, la versión para teléfono, y la
administración de empresas y usuarios. Sigue el sistema de diseño de Opsynex. La siguiente etapa
es la carga de datos y la homologación en la interfaz (Etapa 09).

Etapa 09 completada (19/11/2026): el analista sube el libro contable desde la web. Ve las hojas
reconocidas, las advertencias de calidad y el resultado del análisis automático (score, KPIs,
problemas e impacto), y desde ahí va al dashboard. Si alguna cuenta quedó con dudas, el sistema
lo avisa y permite corregirla y volver a analizar. También muestra el historial de cargas y los
datos consolidados por año. La siguiente etapa es el dashboard ejecutivo y los problemas
(Etapa 10).

**Etapa 10 completada (19/11/2026) — MVP completo.**
- **Dashboard ejecutivo:** score con sus componentes, impacto económico total y por categoría,
  KPIs con su variación anual, tendencias, principales problemas y recomendaciones.
- **Problemas:** lista con filtros y una vista de detalle con el cálculo del impacto, el
  histórico y la evidencia.
- **KPIs:** página con la tabla histórica y un gráfico por indicador.
- **Informe PDF:** informe ejecutivo descargable para todos los roles.

Se cumplen todos los puntos de la «Definición MVP» de las dos especificaciones (ver *Pruebas →
Checklist MVP*). Las etapas siguientes agregan inteligencia artificial (11), el módulo
industrial (12), calidad y datos demo (13) y el despliegue (14).

Etapa 11 (19/11/2026): la plataforma suma inteligencia artificial.
- **Resumen ejecutivo:** escrito con Claude para la gerencia, con 3 prioridades.
- **Recomendaciones:** para los problemas de severidad alta y media, con causas raíz y
  acciones.
- **Anomalías:** detección en las ventas mensuales.
- **Funcionamiento sin IA:** si la IA no está configurada o no responde, todo sigue funcionando
  con las recomendaciones base y la pantalla lo indica.
- **Privacidad:** a la IA solo llegan cifras agregadas, nunca datos personales.

Etapa 12 (19/11/2026): **módulo industrial**.
- Producción, costos y estándares por máquina y línea.
- OEE con sus componentes, scrap, paradas y costo por unidad.
- Problemas industriales con su impacto.
- Score industrial junto al Score Opsynex.
- Páginas de Producción y Costos.

Su alcance está pendiente de confirmar con el cliente.

Etapa 13 (19/11/2026): **seguridad, calidad y demo**.
- Límites de peticiones, cabeceras de seguridad y CSP, y matriz de permisos de todos los
  endpoints.
- Cobertura superior al 95 % en el núcleo.
- Pruebas E2E con auditoría de accesibilidad en la CI.
- Comando `seed-demo` para presentar la plataforma.

**Etapa 14 (19/11/2026) — versión 1.0.**
- **Contenedores de producción:** sin privilegios.
- **Infraestructura como código:** Azure Container Apps, Azure SQL con backups, Key Vault,
  Container Registry y Application Insights con alertas.
- **Despliegue automático:** en cada merge a `main`, con migraciones previas y prueba de humo.
- **Manual:** despliegue, operación y costos.

**Estado final:** la plataforma está lista para producción. Falta el primer despliegue con una
suscripción de Azure del cliente (se hace con el bootstrap y el workflow Deploy).

## Roadmap

| Fase | Contenido |
|---|---|
| **1 — MVP y versión 1.0** (completada) | Carga y homologación de libros contables, KPIs, diagnóstico por reglas, impacto económico, Score Opsynex, dashboard, informe PDF, IA (resumen, recomendaciones y anomalías), módulo industrial, seguridad, E2E y despliegue en Azure |
| **2 — Integración y predicción** | Conectores con ERP (en lugar de archivos), benchmarking entre empresas del mismo sector (anonimizado), forecasting de ingresos, costos y márgenes, alertas por correo cuando aparece un problema alto, sección industrial en el informe PDF y SQL sin contraseña (identidad administrada) |
| **3 — Tiempo real e IoT** | Ingesta continua de datos de planta (MES / IoT) para OEE en tiempo real, modelos de machine learning por industria, detección automática de anomalías en streaming, módulos analíticos específicos por sector y API pública para integraciones de clientes |
