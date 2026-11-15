# Etapa 14 — Despliegue en la nube

**Depende de:** 13 · **Rama:** `etapa-14-despliegue`

## Prompt

> Prepara Opsynex AI para producción y despliégala en la nube. **Antes de empezar, pregúntale
> al usuario qué nube prefiere** (Azure, AWS o GCP) y si ya tiene cuenta. Azure es la opción
> más natural para .NET y SQL Server; las especificaciones mencionan AWS o GCP. Deja todo
> automatizado y documentado para que el cliente pueda operar la plataforma.

## Tareas

1. **Contenedores de producción**: API (.NET runtime, usuario no root), servicio de IA
   (uvicorn con workers), frontend compilado servido por nginx. `docker-compose.prod.yml` para
   probar localmente en modo producción.
2. **Arquitectura según la nube elegida**:
   - **Azure (recomendada)**: App Service o Container Apps (API, IA y frontend, o Static Web
     Apps para el frontend) + Azure SQL Database + Key Vault + Container Registry + Application Insights.
   - **AWS**: ECS Fargate o App Runner + RDS for SQL Server + Secrets Manager + ECR (frontend
     en S3 + CloudFront) + CloudWatch.
   - **GCP**: Cloud Run + Cloud SQL for SQL Server + Secret Manager + Artifact Registry + Cloud Logging.
   - El servicio de IA no debe ser accesible desde internet: solo desde el API (red interna o ingress privado).
3. **Infraestructura como código** (Bicep para Azure o Terraform) en `infra/`.
4. **CI/CD**: `deploy.yml` que, al hacer merge a `main`, construye las imágenes, aplica las
   migraciones de EF Core (bundle de migraciones) y despliega. Secretos en GitHub Actions o
   identidad federada (OIDC).
5. **Operación**: backups automáticos de la BD, logs y alertas básicas, dominio y HTTPS,
   variables de producción, health checks usados por la plataforma, y escalado mínimo y máximo.
6. Estimación del costo mensual (infraestructura y consumo de Claude API).

## Criterios de aceptación

- [ ] La plataforma responde en una URL pública con HTTPS.
- [ ] Un merge a `main` despliega automáticamente.
- [ ] El manual de despliegue y operación está completo.

## Documentación a actualizar

- `11_despliegue.md`: arquitectura cloud, pasos, variables, backups, monitoreo, costos y operación.
- `01_proyecto.md`: estado final y roadmap (fases 2 y 3).
- `13_bitacora.md`. Versión de la documentación `1.0.0`.
