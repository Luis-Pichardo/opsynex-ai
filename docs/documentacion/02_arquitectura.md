# Parte II: Documentación técnica

## Arquitectura del sistema

### Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | Python + FastAPI |
| Base de datos | PostgreSQL |
| Frontend | React + Tailwind CSS |
| Hosting | AWS o GCP |

> Pendiente (Etapa 00): versiones exactas, estructura de carpetas y diagrama de componentes.

### Flujo de datos

1. El usuario carga los archivos financieros.
2. El sistema valida la estructura.
3. El sistema normaliza (homologa) las cuentas.
4. Los datos se almacenan en la base de datos.
5. El motor de KPIs ejecuta los cálculos.
6. El motor de diagnóstico evalúa las condiciones.
7. El motor de impacto financiero estima las pérdidas.
8. El dashboard muestra los resultados.

### Seguridad

Requerimientos iniciales: autenticación JWT, encriptación de contraseñas, validación de
entradas, validación de archivos y control de acceso basado en roles.

> Pendiente (Etapas 02 y 12): implementación detallada.
