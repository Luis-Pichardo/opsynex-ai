// Azure SQL: servidor lógico + base de datos serverless con backups automáticos.
// - PITR (restauración a un punto en el tiempo): `pitrDays` días.
// - LTR (retención larga): semanal 4 semanas, mensual 12 meses, anual 5 años.
// - TDE (cifrado en reposo) y TLS 1.2 mínimo.

param location string
param namePrefix string
param tags object
param adminLogin string
@secure()
param adminPassword string
@description('SKU de la base: GP_S_Gen5_1 = General Purpose serverless, 1 vCore máx.')
param skuName string = 'GP_S_Gen5_1'
@description('Minutos sin actividad antes de pausar (serverless). -1 = nunca.')
param autoPauseDelay int = 60
@minValue(7)
@maxValue(35)
param pitrDays int = 14

resource server 'Microsoft.Sql/servers@2021-11-01' = {
  name: 'sql-${namePrefix}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Container Apps (plan Consumption) sale a internet por IPs de Azure: se permiten solo los
// servicios de Azure (0.0.0.0). Para aislarlo por completo, ver "Red privada" en el manual.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: server
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: server
  name: 'Opsynex'
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: 'GeneralPurpose'
  }
  properties: {
    collation: 'Modern_Spanish_CI_AS'
    autoPauseDelay: autoPauseDelay
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368 // 32 GB
    requestedBackupStorageRedundancy: 'Geo'
    zoneRedundant: false
  }
}

resource shortTerm 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2021-11-01' = {
  parent: database
  name: 'default'
  properties: {
    retentionDays: pitrDays
    diffBackupIntervalInHours: 12
  }
}

resource longTerm 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2021-11-01' = {
  parent: database
  name: 'default'
  properties: {
    weeklyRetention: 'P4W'
    monthlyRetention: 'P12M'
    yearlyRetention: 'P5Y'
    weekOfYear: 1
  }
}

output serverName string = server.name
output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
output databaseId string = database.id
