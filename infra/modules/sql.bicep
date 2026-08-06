@description('Azure SQL logical server name.')
param serverName string
param databaseName string
param location string
param administratorLogin string
@secure()
param administratorPassword string
param allowAzureServicesToSql bool = true
param databaseSkuName string = 'GP_S_Gen5'
param databaseTier string = 'GeneralPurpose'
param databaseFamily string = 'Gen5'
param databaseCapacity int = 1
param minCapacity string = '0.5'
param autoPauseDelayMinutes int = 60
param maxSizeBytes int = 34359738368
param tags object = {}

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (allowAzureServicesToSql) {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: databaseSkuName
    tier: databaseTier
    family: databaseFamily
    capacity: databaseCapacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: maxSizeBytes
    minCapacity: json(minCapacity)
    autoPauseDelay: autoPauseDelayMinutes
    requestedBackupStorageRedundancy: 'Local'
  }
}

output serverName string = server.name
output databaseName string = database.name
output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName
