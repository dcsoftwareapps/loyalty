@allowed([
  'linux'
  'windows'
])
param os string
param planName string
param appName string
param location string
param skuName string = 'B1'
param skuTier string = 'Basic'
param workerSize string = '0'
param runtimeStack string
param alwaysOn bool = false
param appSettings array = []
param tags object = {}

var isLinux = os == 'linux'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
    size: skuName
    family: substring(skuName, 0, 1)
    capacity: 1
  }
  properties: {
    reserved: isLinux
    targetWorkerSizeId: int(workerSize)
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: isLinux ? 'app,linux' : 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: union({
      alwaysOn: alwaysOn
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: appSettings
    }, isLinux ? {
      linuxFxVersion: runtimeStack
    } : {
      netFrameworkVersion: runtimeStack
    })
  }
}

output id string = app.id
output name string = app.name
output defaultHostName string = app.properties.defaultHostName
output principalId string = app.identity.principalId
