// Contenedores de Opsynex AI en Azure Container Apps:
// - web  (nginx con el frontend compilado): ingreso público HTTPS.
// - api  (ASP.NET Core): ingreso público HTTPS, sondas /health/live y /health/ready.
// - ai   (FastAPI): ingreso INTERNO, solo accesible desde el entorno (el API); nunca desde internet.
// - job de migraciones (bundle de EF Core): manual, lo ejecuta el pipeline antes de cada versión.
// Los secretos se leen de Key Vault con la identidad administrada (nunca en texto plano).

param location string
param tags object
param environmentId string
param environmentDomain string
param identityId string
param registryServer string
param keyVaultUri string
param imageTag string
param webName string
param apiName string
param aiName string
param jobName string
@description('Crear o actualizar las apps (false: solo el job de migraciones).')
param deployApps bool = true
param seedAdminEmail string
param anthropicModel string = 'claude-opus-5-5'
param apiMinReplicas int = 1
param apiMaxReplicas int = 3
param aiMinReplicas int = 1
param aiMaxReplicas int = 2
param webMinReplicas int = 1
param webMaxReplicas int = 2

var webUrl = 'https://${webName}.${environmentDomain}'
var apiUrl = 'https://${apiName}.${environmentDomain}'
var registries = [
  {
    server: registryServer
    identity: identityId
  }
]

// Referencia a un secreto de Key Vault leído con la identidad administrada.
func kvSecret(name string, vaultUri string, identity string) object => {
  name: name
  keyVaultUrl: '${vaultUri}secrets/${name}'
  identity: identity
}

// --- Job de migraciones (bundle de EF Core) ---
resource migrations 'Microsoft.App/jobs@2024-03-01' = {
  name: jobName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 900
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: registries
      secrets: [kvSecret('sql-connection', keyVaultUri, identityId)]
    }
    template: {
      containers: [
        {
          name: 'migrations'
          image: '${registryServer}/opsynex-migrations:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [{ name: 'ConnectionStrings__Default', secretRef: 'sql-connection' }]
        }
      ]
    }
  }
}

// --- Servicio de IA (interno) ---
resource ai 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: aiName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false // solo dentro del entorno de Container Apps
        targetPort: 8001
        transport: 'http'
        allowInsecure: true // tráfico interno del entorno (el API llama por http://<nombre>)
      }
      registries: registries
      secrets: [kvSecret('ai-service-api-key', keyVaultUri, identityId), kvSecret('anthropic-api-key', keyVaultUri, identityId)]
    }
    template: {
      containers: [
        {
          name: 'ai'
          image: '${registryServer}/opsynex-ai:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'AI_SERVICE_API_KEY', secretRef: 'ai-service-api-key' }
            { name: 'ANTHROPIC_API_KEY', secretRef: 'anthropic-api-key' }
            { name: 'ANTHROPIC_MODEL', value: anthropicModel }
            { name: 'UVICORN_WORKERS', value: '2' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/health', port: 8001 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health', port: 8001 }, periodSeconds: 10 }
          ]
        }
      ]
      scale: {
        minReplicas: aiMinReplicas
        maxReplicas: aiMaxReplicas
        rules: [{ name: 'http', http: { metadata: { concurrentRequests: '20' } } }]
      }
    }
  }
}

// --- API ---
resource api 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: apiName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false // HTTP se redirige a HTTPS
      }
      registries: registries
      secrets: [
        kvSecret('sql-connection', keyVaultUri, identityId)
        kvSecret('jwt-secret', keyVaultUri, identityId)
        kvSecret('ai-service-api-key', keyVaultUri, identityId)
        kvSecret('seed-admin-password', keyVaultUri, identityId)
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registryServer}/opsynex-api:${imageTag}'
          resources: { cpu: json('1.0'), memory: '2Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__Default', secretRef: 'sql-connection' }
            { name: 'Database__MigrateOnStartup', value: 'false' } // las aplica el job
            { name: 'Jwt__Secret', secretRef: 'jwt-secret' }
            { name: 'Cors__Origins', value: webUrl }
            { name: 'AiService__BaseUrl', value: 'http://${aiName}' }
            { name: 'AiService__ApiKey', secretRef: 'ai-service-api-key' }
            { name: 'Seed__AdminEmail', value: seedAdminEmail }
            { name: 'Seed__AdminPassword', secretRef: 'seed-admin-password' }
            // El balanceador de Container Apps termina TLS y está delante del API.
            { name: 'ReverseProxy__TrustAll', value: 'true' }
            { name: 'ReverseProxy__ForwardLimit', value: '2' }
          ]
          probes: [
            { type: 'Startup', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 5, failureThreshold: 30 }
            { type: 'Liveness', httpGet: { path: '/health/live', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080 }, periodSeconds: 10 }
          ]
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: apiMaxReplicas
        rules: [{ name: 'http', http: { metadata: { concurrentRequests: '50' } } }]
      }
    }
  }
  dependsOn: [ai]
}

// --- Frontend ---
resource web 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: webName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: registries
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${registryServer}/opsynex-web:${imageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [{ name: 'API_ORIGIN', value: apiUrl }]
          probes: [
            { type: 'Liveness', httpGet: { path: '/healthz', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/healthz', port: 8080 }, periodSeconds: 10 }
          ]
        }
      ]
      scale: {
        minReplicas: webMinReplicas
        maxReplicas: webMaxReplicas
        rules: [{ name: 'http', http: { metadata: { concurrentRequests: '100' } } }]
      }
    }
  }
}

output webUrl string = webUrl
output apiUrl string = apiUrl
output apiAppId string = deployApps ? api.id : ''
output migrationsJobName string = migrations.name
