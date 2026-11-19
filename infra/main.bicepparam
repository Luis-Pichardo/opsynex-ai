// Parámetros de producción. Los valores con readEnvironmentVariable los pasa el pipeline
// (variables de GitHub Actions); los secretos viven en Key Vault, nunca aquí.
using 'main.bicep'

param namePrefix = 'opsynex'
param keyVaultName = readEnvironmentVariable('AZURE_KEY_VAULT', '')
param imageTag = readEnvironmentVariable('IMAGE_TAG', 'latest')
param deployJob = bool(readEnvironmentVariable('DEPLOY_JOB', 'false'))
param deployApps = bool(readEnvironmentVariable('DEPLOY_APPS', 'false'))
param seedAdminEmail = readEnvironmentVariable('SEED_ADMIN_EMAIL', '')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')
param apiMinReplicas = 1
param apiMaxReplicas = 3
