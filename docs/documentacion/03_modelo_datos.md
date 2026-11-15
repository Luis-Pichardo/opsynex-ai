## Modelo de datos

Base de datos: **SQL Server**, gestionada con Entity Framework Core (code-first y migraciones).

Tablas principales previstas en las especificaciones: `users`, `financial_data`,
`operational_data`, `kpi_results` y `problems`. A ellas se suman `companies`, `uploads`,
`account_mappings` y `scores` para soportar multi-empresa, la carga de archivos y la homologación.

> Pendiente (Etapa 01): detalle de cada tabla, columnas, tipos, relaciones y diagrama ER.
