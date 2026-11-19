// Opsynex AI en Azure (etapa 14). Ámbito: grupo de recursos.
//
// Requisitos previos (scripts/azure-bootstrap.sh, una sola vez): grupo de recursos, Key Vault con
// los secretos y la identidad de GitHub Actions (OIDC). Este archivo crea y actualiza lo demás.
//
// Fases (las ejecuta .github/workflows/deploy.yml):
//   1) deployJob=false, deployApps=false → base: monitoreo, ACR, identidad, SQL y entorno.
//   2) (el pipeline sube las imágenes al ACR)
//   3) deployJob=true,  deployApps=false → job de migraciones (el pipeline lo ejecuta).
//   4) deployJob=true,  deployApps=true  → web, api y ai con la nueva versión.

targetScope = 'resourceGroup'

@description('Región de Azure.')
param location string = resourceGroup().location

@description('Prefijo de los nombres (minúsculas, sin espacios).')
@minLength(3)
@maxLength(12)
param namePrefix string = 'opsynex'

@description('Key Vault creado por el bootstrap, con los secretos de producción.')
param keyVaultName string

@description('Etiqueta de las imágenes a desplegar (el SHA del commit en el pipeline).')
param imageTag string = 'latest'

param deployJob bool = false
param deployApps bool = false

@description('Correo del primer Admin (se crea al arrancar el API si no existe).')
param seedAdminEmail string

@description('Correo que recibe las alertas.')
param alertEmail string

param sqlAdminLogin string = 'opsynexadmin'

@description('Réplicas del API (mínimo y máximo).')
param apiMinReplicas int = 1
param apiMaxReplicas int = 3

param anthropicModel string = 'claude-opus-5-5'

var tags = {
  app: 'opsynex-ai'
  environment: 'production'
}
var suffix = uniqueString(resourceGroup().id)
var names = {
  web: '${namePrefix}-web'
  api: '${namePrefix}-api'
  ai: '${namePrefix}-ai'
  job: '${namePrefix}-migrations'
}

// --- Secretos existentes (bootstrap) ---
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// --- Monitoreo ---
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
  }
}

// --- Identidad de las apps (lee el ACR y Key Vault) ---
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${namePrefix}-apps'
  location: location
  tags: tags
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: '${namePrefix}acr${suffix}'
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// --- Base de datos ---
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
    adminLogin: sqlAdminLogin
    adminPassword: vault.getSecret('sql-admin-password')
  }
}

// La cadena de conexión se guarda en Key Vault (las apps la leen de ahí).
module connection 'modules/sql-connection.bicep' = {
  name: 'sql-connection'
  params: {
    keyVaultName: vault.name
    serverFqdn: sql.outputs.serverFqdn
    databaseName: sql.outputs.databaseName
    adminLogin: sqlAdminLogin
    adminPassword: vault.getSecret('sql-admin-password')
  }
}

// --- Entorno de Container Apps ---
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${namePrefix}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: monitoring.outputs.workspaceCustomerId
        sharedKey: monitoring.outputs.workspaceSharedKey
      }
    }
  }
}

module apps 'modules/apps.bicep' = if (deployJob || deployApps) {
  name: 'apps'
  params: {
    location: location
    tags: tags
    environmentId: environment.id
    environmentDomain: environment.properties.defaultDomain
    identityId: identity.id
    registryServer: registry.properties.loginServer
    keyVaultUri: vault.properties.vaultUri
    imageTag: imageTag
    webName: names.web
    apiName: names.api
    aiName: names.ai
    jobName: names.job
    deployApps: deployApps
    seedAdminEmail: seedAdminEmail
    anthropicModel: anthropicModel
    apiMinReplicas: apiMinReplicas
    apiMaxReplicas: apiMaxReplicas
  }
  dependsOn: [acrPull, secretsUser, connection]
}

// --- Alertas (solo con las apps desplegadas) ---
module alerts 'modules/alerts.bicep' = if (deployApps) {
  name: 'alerts'
  params: {
    location: location
    namePrefix: namePrefix
    tags: tags
    alertEmail: alertEmail
    insightsId: monitoring.outputs.insightsId
    apiAppId: resourceId('Microsoft.App/containerApps', names.api)
    apiHealthUrl: 'https://${names.api}.${environment.properties.defaultDomain}/health/ready'
    sqlDatabaseId: sql.outputs.databaseId
  }
  dependsOn: [apps]
}

output registryName string = registry.name
output registryServer string = registry.properties.loginServer
output environmentDomain string = environment.properties.defaultDomain
output webUrl string = 'https://${names.web}.${environment.properties.defaultDomain}'
output apiUrl string = 'https://${names.api}.${environment.properties.defaultDomain}'
output migrationsJobName string = names.job
output sqlServerName string = sql.outputs.serverName
output insightsName string = monitoring.outputs.insightsName
