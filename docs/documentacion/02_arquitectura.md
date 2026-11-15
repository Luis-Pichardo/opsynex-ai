# Parte II: Documentación técnica

## Arquitectura del sistema

### Stack tecnológico

| Capa | Tecnología |
|---|---|
| API principal | C# / ASP.NET Core (.NET 10), Entity Framework Core |
| Base de datos | SQL Server |
| Servicio de IA | Python + FastAPI, Claude (Anthropic), scikit-learn, rapidfuzz |
| Frontend | React + TypeScript + Vite + Tailwind CSS + shadcn/ui + Recharts |
| Hosting | Azure, AWS o GCP |

### Componentes

| Componente | Se comunica con | Cómo |
|---|---|---|
| Frontend (React) | API ASP.NET Core | HTTPS + token JWT |
| API ASP.NET Core | SQL Server | Entity Framework Core |
| API ASP.NET Core | Servicio de IA (Python) | HTTP interno con clave de servicio |
| Servicio de IA | Claude (Anthropic) | API de Anthropic (solo datos agregados) |

El API .NET concentra los datos, la seguridad y la lógica determinista (KPIs, reglas, impacto
y score). El servicio de IA no tiene base de datos: recibe información agregada y devuelve
sugerencias. Si el servicio de IA no está disponible, la plataforma sigue funcionando.

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
