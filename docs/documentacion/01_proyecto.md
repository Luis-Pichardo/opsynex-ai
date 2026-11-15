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

## Plan de trabajo por etapas

| # | Etapa | Resultado |
|---|---|---|
| 00 | Preparación del entorno | Repositorio, backend y frontend base, CI |
| 01 | Base de datos y modelos | Esquema PostgreSQL y migraciones |
| 02 | Autenticación y roles | Login JWT, roles Admin/Analyst/Client |
| 03 | Carga y validación de datos | Subida de Excel/CSV con validaciones |
| 04 | Homologación contable | Mapeo de cuentas a categorías estándar |
| 05 | Motor de KPIs | Cálculo, tendencias y normalización |
| 06 | Motor de diagnóstico | Reglas, severidad y priorización |
| 07 | Impacto financiero y score | Pérdidas estimadas y score 0–100 |
| 08 | Frontend base | Diseño, autenticación y navegación |
| 09 | Frontend de carga | Asistente de carga y homologación |
| 10 | Dashboard (cierre del MVP) | Dashboard ejecutivo y problemas |
| 11 | Módulo industrial | OEE, scrap, downtime, costos por unidad |
| 12 | Calidad y demo | Seguridad, pruebas E2E, datos demo |
| 13 | Despliegue | Producción en AWS o GCP |

## Estado actual

> Pendiente (Etapa 00). Proyecto planificado; desarrollo no iniciado.
