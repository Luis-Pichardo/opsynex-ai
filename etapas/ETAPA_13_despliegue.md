# Etapa 13 — Despliegue en la nube

**Depende de:** 12 · **Rama:** `etapa-13-despliegue`

## Prompt

> Prepara Opsynex AI para producción y su despliegue en la nube (AWS o GCP según lo que
> confirme el usuario; pregúntale antes de empezar cuál prefiere y si ya tiene cuenta).
> Deja todo automatizado y documentado para que el cliente pueda operar la plataforma.

## Tareas

1. **Contenedores de producción**: backend con gunicorn/uvicorn workers, frontend compilado
   servido por nginx; `docker-compose.prod.yml` para pruebas locales de producción.
2. **Opción recomendada GCP**: Cloud Run (backend y frontend) + Cloud SQL PostgreSQL + Secret
   Manager + Artifact Registry. **Opción AWS**: App Runner o ECS Fargate + RDS PostgreSQL +
   Secrets Manager + ECR (frontend opcionalmente en S3 + CloudFront).
3. **Infraestructura como código** mínima (Terraform o scripts `gcloud`/`aws` documentados).
4. **CI/CD**: workflow `deploy.yml` que al hacer merge a `main` construye imágenes, ejecuta
   migraciones y despliega (con secretos en GitHub Actions).
5. **Operación**: backups automáticos de la BD, logs y alertas básicas, dominio y HTTPS,
   variables de entorno de producción, endpoint `/health` usado por el balanceador.
6. Estimación de costos mensuales aproximados del entorno.

## Criterios de aceptación

- [ ] La plataforma responde en una URL pública con HTTPS.
- [ ] Un merge a `main` despliega automáticamente.
- [ ] Manual de despliegue y operación completo.

## Documentación a actualizar

- `10_despliegue.md`: arquitectura cloud, pasos de despliegue, variables, backups, costos, operación.
- `01_proyecto.md`: estado final y roadmap (Fases 2 y 3 de las especificaciones).
- `12_bitacora.md`. Versión de la documentación `1.0.0`.
