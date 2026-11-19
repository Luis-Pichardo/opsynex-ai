// Observabilidad: Log Analytics (logs de los contenedores) y Application Insights
// (disponibilidad y alertas). Los logs estructurados de Serilog llegan por la consola.

param location string
param namePrefix string
param tags object
@description('Días de retención de los logs (30 incluidos en el precio de ingesta).')
param retentionDays int = 30

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${namePrefix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: retentionDays
    workspaceCapping: { dailyQuotaGb: 2 } // tope de seguridad contra un exceso de logs
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${namePrefix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

output workspaceId string = workspace.id
output workspaceCustomerId string = workspace.properties.customerId
#disable-next-line outputs-should-not-contain-secrets
output workspaceSharedKey string = workspace.listKeys().primarySharedKey
output insightsId string = insights.id
output insightsName string = insights.name
output insightsConnectionString string = insights.properties.ConnectionString
