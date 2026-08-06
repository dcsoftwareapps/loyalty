targetScope = 'resourceGroup'

@allowed([
  'prod'
  'stg'
])
param environmentName string

param location string = resourceGroup().location
param uniqueSuffix string = take(uniqueString(subscription().id, resourceGroup().name, environmentName), 6)
param tags object = {
  product: 'LoyaltyCloud'
  environment: environmentName
  managedBy: 'bicep'
}

param apiAppName string = 'loyaltycloud-api-${environmentName}-${uniqueSuffix}'
param adminAppName string = 'loyaltycloud-admin-${environmentName}-${uniqueSuffix}'
param apiPlanName string = 'plan-loyaltycloud-api-${environmentName}-${uniqueSuffix}'
param adminPlanName string = 'plan-loyaltycloud-admin-${environmentName}-${uniqueSuffix}'
param keyVaultName string = 'kv-loyaltycloud-${environmentName}-${uniqueSuffix}'
param storageAccountName string = 'stloyaltycloud${environmentName}${uniqueSuffix}'
param sqlServerName string = 'sql-loyaltycloud-${environmentName}-${uniqueSuffix}'
param sqlDatabaseName string = 'LoyaltyCloud${environmentName == 'stg' ? 'Stg' : 'Free'}'

param apiPlanSkuName string = 'B1'
param apiPlanSkuTier string = 'Basic'
param adminPlanSkuName string = 'B1'
param adminPlanSkuTier string = 'Basic'
param apiRuntimeStack string = 'DOTNETCORE|9.0'
param adminRuntimeStack string = 'v9.0'

param sqlAdministratorLogin string = 'loyaltycloudadmin'
@secure()
param sqlAdministratorPassword string = ''
param allowAzureServicesToSql bool = true
param sqlDatabaseSkuName string = 'GP_S_Gen5'
param sqlDatabaseTier string = 'GeneralPurpose'
param sqlDatabaseFamily string = 'Gen5'
param sqlDatabaseCapacity int = 1
param sqlDatabaseMinCapacity string = '0.5'
param sqlAutoPauseDelayMinutes int = 60

param passContainerName string = 'passes'
param storageSasExpirationMinutes int = 15

param applePassTypeIdentifier string = 'pass.com.kbeautymx.loyalty'
param appleTeamIdentifier string = 'HS2XCFGQ75'
param appleOrganizationName string = 'KBeauty MX'
param appleApnHost string = 'https://api.push.apple.com'

param googleWalletEnabled bool = false
param googleWalletIssuerId string = ''
param googleWalletClassSuffix string = 'loyalty'
param googleWalletObjectIdPrefix string = 'member'
param googleWalletProgramName string = 'KBeauty Loyalty'
param googleWalletIssuerName string = 'KBeauty MX'
param googleWalletLogoUri string = ''
param googleWalletHeroImageUri string = ''
param googleWalletHexBackgroundColor string = '#FFFFFF'
param googleWalletOrigins string = ''

param loyaltyMaintenanceEnabled bool = true
param loyaltyMaintenanceRunOnStartup bool = false
param loyaltyMaintenanceIntervalHours int = 12
param loyaltyMaintenanceRunAtLocalTime string = '02:00'
param loyaltyMaintenanceTimeZoneId string = 'America/Tijuana'
param loyaltyNotificationsEnabled bool = true
param loyaltyNotificationsRunOnStartup bool = false
param loyaltyNotificationsPollIntervalSeconds int = 43200
param loyaltyNotificationsBatchSize int = 25
param loyaltyNotificationsMaxAttempts int = 3
param loyaltyNotificationsVisibleEventPriorityHours int = 24
param customNotificationCampaignBatchSize int = 50
param provisioningTrialDays int = 14
param billingGracePeriodDays int = 7
param tenantAdminSessionHours int = 168
param superAdminSessionHours int = 8

param sqlConnectionStringSecretName string = 'loyaltycloud-sql-connection-string'
param storageConnectionStringSecretName string = 'loyaltycloud-storage-connection-string'
param adminApiSharedSecretName string = 'loyaltycloud-admin-api-shared-secret'
param superAdminUsernameSecretName string = 'loyaltycloud-superadmin-username'
param superAdminPasswordHashSecretName string = 'loyaltycloud-superadmin-password-hash'
param googleWalletServiceAccountJsonSecretName string = 'loyaltycloud-google-wallet-service-account-json'

var keyVaultUri = keyVault.outputs.uri
var apiBaseUrl = 'https://${apiAppName}.azurewebsites.net'
var adminBaseUrl = 'https://${adminAppName}.azurewebsites.net'
var sqlConnectionString = 'Server=tcp:${sql.outputs.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var keyVaultReferencePrefix = '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName='
var keyVaultReferenceSuffix = ')'
var sqlConnectionStringReference = '${keyVaultReferencePrefix}${sqlConnectionStringSecretName}${keyVaultReferenceSuffix}'
var storageConnectionStringReference = '${keyVaultReferencePrefix}${storageConnectionStringSecretName}${keyVaultReferenceSuffix}'
var adminApiSharedSecretReference = '${keyVaultReferencePrefix}${adminApiSharedSecretName}${keyVaultReferenceSuffix}'
var superAdminUsernameReference = '${keyVaultReferencePrefix}${superAdminUsernameSecretName}${keyVaultReferenceSuffix}'
var superAdminPasswordHashReference = '${keyVaultReferencePrefix}${superAdminPasswordHashSecretName}${keyVaultReferenceSuffix}'
var googleWalletServiceAccountJsonReference = '${keyVaultReferencePrefix}${googleWalletServiceAccountJsonSecretName}${keyVaultReferenceSuffix}'

module sql 'modules/sql.bicep' = {
  name: 'sql-${environmentName}'
  params: {
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    location: location
    administratorLogin: sqlAdministratorLogin
    administratorPassword: sqlAdministratorPassword
    allowAzureServicesToSql: allowAzureServicesToSql
    databaseSkuName: sqlDatabaseSkuName
    databaseTier: sqlDatabaseTier
    databaseFamily: sqlDatabaseFamily
    databaseCapacity: sqlDatabaseCapacity
    minCapacity: sqlDatabaseMinCapacity
    autoPauseDelayMinutes: sqlAutoPauseDelayMinutes
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage-${environmentName}'
  params: {
    name: storageAccountName
    location: location
    passContainerName: passContainerName
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-${environmentName}'
  params: {
    name: keyVaultName
    location: location
    tags: tags
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVault.outputs.name}/${sqlConnectionStringSecretName}'
  properties: {
    value: sqlConnectionString
  }
}

resource storageConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVault.outputs.name}/${storageConnectionStringSecretName}'
  properties: {
    value: storage.outputs.connectionString
  }
}

var commonAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ConnectionStrings__DefaultConnection'
    value: sqlConnectionStringReference
  }
  {
    name: 'Azure__KeyVaultUri'
    value: keyVaultUri
  }
  {
    name: 'Azure__BlobStorage__ConnectionString'
    value: storageConnectionStringReference
  }
  {
    name: 'Azure__BlobStorage__PassContainer'
    value: passContainerName
  }
  {
    name: 'Azure__BlobStorage__SasExpirationMinutes'
    value: string(storageSasExpirationMinutes)
  }
  {
    name: 'AdminApi__SharedSecret'
    value: adminApiSharedSecretReference
  }
  {
    name: 'Apple__PassTypeIdentifier'
    value: applePassTypeIdentifier
  }
  {
    name: 'Apple__TeamIdentifier'
    value: appleTeamIdentifier
  }
  {
    name: 'Apple__OrganizationName'
    value: appleOrganizationName
  }
  {
    name: 'Apple__ApnHost'
    value: appleApnHost
  }
  {
    name: 'GoogleWallet__Enabled'
    value: string(googleWalletEnabled)
  }
  {
    name: 'GoogleWallet__IssuerId'
    value: googleWalletIssuerId
  }
  {
    name: 'GoogleWallet__ClassSuffix'
    value: googleWalletClassSuffix
  }
  {
    name: 'GoogleWallet__ObjectIdPrefix'
    value: googleWalletObjectIdPrefix
  }
  {
    name: 'GoogleWallet__ProgramName'
    value: googleWalletProgramName
  }
  {
    name: 'GoogleWallet__IssuerName'
    value: googleWalletIssuerName
  }
  {
    name: 'GoogleWallet__LogoUri'
    value: googleWalletLogoUri
  }
  {
    name: 'GoogleWallet__HeroImageUri'
    value: googleWalletHeroImageUri
  }
  {
    name: 'GoogleWallet__HexBackgroundColor'
    value: googleWalletHexBackgroundColor
  }
  {
    name: 'GoogleWallet__Origins'
    value: googleWalletOrigins
  }
  {
    name: 'GoogleWallet__ServiceAccountJson'
    value: googleWalletEnabled ? googleWalletServiceAccountJsonReference : ''
  }
  {
    name: 'Billing__GracePeriodDays'
    value: string(billingGracePeriodDays)
  }
  {
    name: 'Provisioning__TrialDays'
    value: string(provisioningTrialDays)
  }
]

var apiAppSettings = concat(commonAppSettings, [
  {
    name: 'Apple__WebServiceURL'
    value: apiBaseUrl
  }
  {
    name: 'Wallet__UseRealPassSigning'
    value: 'true'
  }
  {
    name: 'Wallet__UseRealApns'
    value: 'true'
  }
  {
    name: 'Cors__AllowedOrigins'
    value: adminBaseUrl
  }
  {
    name: 'LoyaltyMaintenance__Enabled'
    value: string(loyaltyMaintenanceEnabled)
  }
  {
    name: 'LoyaltyMaintenance__RunOnStartup'
    value: string(loyaltyMaintenanceRunOnStartup)
  }
  {
    name: 'LoyaltyMaintenance__IntervalHours'
    value: string(loyaltyMaintenanceIntervalHours)
  }
  {
    name: 'LoyaltyMaintenance__RunAtLocalTime'
    value: loyaltyMaintenanceRunAtLocalTime
  }
  {
    name: 'LoyaltyMaintenance__TimeZoneId'
    value: loyaltyMaintenanceTimeZoneId
  }
  {
    name: 'LoyaltyNotifications__Enabled'
    value: string(loyaltyNotificationsEnabled)
  }
  {
    name: 'LoyaltyNotifications__RunOnStartup'
    value: string(loyaltyNotificationsRunOnStartup)
  }
  {
    name: 'LoyaltyNotifications__PollIntervalSeconds'
    value: string(loyaltyNotificationsPollIntervalSeconds)
  }
  {
    name: 'LoyaltyNotifications__BatchSize'
    value: string(loyaltyNotificationsBatchSize)
  }
  {
    name: 'LoyaltyNotifications__MaxAttempts'
    value: string(loyaltyNotificationsMaxAttempts)
  }
  {
    name: 'LoyaltyNotifications__VisibleEventPriorityHours'
    value: string(loyaltyNotificationsVisibleEventPriorityHours)
  }
  {
    name: 'CustomNotificationCampaigns__BatchSize'
    value: string(customNotificationCampaignBatchSize)
  }
])

var adminAppSettings = concat(commonAppSettings, [
  {
    name: 'Admin__ApiBaseUrl'
    value: apiBaseUrl
  }
  {
    name: 'Admin__Auth__SessionHours'
    value: string(tenantAdminSessionHours)
  }
  {
    name: 'SuperAdmin__Username'
    value: superAdminUsernameReference
  }
  {
    name: 'SuperAdmin__PasswordHash'
    value: superAdminPasswordHashReference
  }
  {
    name: 'SuperAdmin__SessionHours'
    value: string(superAdminSessionHours)
  }
  {
    name: 'Apple__WebServiceURL'
    value: apiBaseUrl
  }
  {
    name: 'Wallet__UseRealPassSigning'
    value: 'true'
  }
  {
    name: 'Wallet__UseRealApns'
    value: 'true'
  }
])

module api 'modules/appservice.bicep' = {
  name: 'api-app-${environmentName}'
  params: {
    os: 'linux'
    planName: apiPlanName
    appName: apiAppName
    location: location
    skuName: apiPlanSkuName
    skuTier: apiPlanSkuTier
    runtimeStack: apiRuntimeStack
    alwaysOn: false
    appSettings: apiAppSettings
    tags: tags
  }
}

module admin 'modules/appservice.bicep' = {
  name: 'admin-app-${environmentName}'
  params: {
    os: 'windows'
    planName: adminPlanName
    appName: adminAppName
    location: location
    skuName: adminPlanSkuName
    skuTier: adminPlanSkuTier
    runtimeStack: adminRuntimeStack
    alwaysOn: false
    appSettings: adminAppSettings
    tags: tags
  }
}

module apiKeyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'api-kv-access-${environmentName}'
  params: {
    keyVaultName: keyVault.outputs.name
    principalId: api.outputs.principalId
  }
}

module adminKeyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'admin-kv-access-${environmentName}'
  params: {
    keyVaultName: keyVault.outputs.name
    principalId: admin.outputs.principalId
  }
}

output apiUrl string = apiBaseUrl
output adminUrl string = adminBaseUrl
output sqlServerFqdn string = sql.outputs.fullyQualifiedDomainName
output sqlDatabase string = sql.outputs.databaseName
output keyVaultUri string = keyVault.outputs.uri
output storageAccount string = storage.outputs.name
