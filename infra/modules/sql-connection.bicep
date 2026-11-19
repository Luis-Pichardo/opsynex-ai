// Guarda la cadena de conexión de Azure SQL como secreto de Key Vault ("sql-connection").

param keyVaultName string
param serverFqdn string
param databaseName string
param adminLogin string
@secure()
param adminPassword string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'sql-connection'
  properties: {
    contentType: 'ConnectionString'
    value: 'Server=tcp:${serverFqdn},1433;Database=${databaseName};User ID=${adminLogin};Password=${adminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
  }
}
