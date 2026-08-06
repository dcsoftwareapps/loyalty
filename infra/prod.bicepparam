using './main.bicep'

param environmentName = 'prod'
param location = 'westus3'
param uniqueSuffix = '894839'

param apiAppName = 'loyaltycloud-api-894839'
param adminAppName = 'loyaltycloud-admin'
param apiPlanName = 'plan-loyaltycloud-api-prod-894839'
param adminPlanName = 'plan-loyaltycloud-admin-prod-894839'
param sqlServerName = 'sql-loyaltycloud-894839'
param sqlDatabaseName = 'LoyaltyCloudFree'
param keyVaultName = 'kv-loyaltycloud-894839'
param storageAccountName = 'stloyaltycloud894839'

param apiPlanSkuName = 'B1'
param apiPlanSkuTier = 'Basic'
param adminPlanSkuName = 'B1'
param adminPlanSkuTier = 'Basic'

param sqlAdministratorLogin = 'loyaltycloudadmin'
param sqlDatabaseSkuName = 'GP_S_Gen5'
param sqlDatabaseTier = 'GeneralPurpose'
param sqlDatabaseFamily = 'Gen5'
param sqlDatabaseCapacity = 1
param sqlDatabaseMinCapacity = '0.5'
param sqlAutoPauseDelayMinutes = 60
param allowAzureServicesToSql = true

param applePassTypeIdentifier = 'pass.com.kbeautymx.loyalty'
param appleTeamIdentifier = 'HS2XCFGQ75'
param appleOrganizationName = 'KBeauty MX'
param appleApnHost = 'https://api.push.apple.com'

param googleWalletEnabled = false
param googleWalletClassSuffix = 'loyalty'
param googleWalletObjectIdPrefix = 'member'
param googleWalletProgramName = 'KBeauty Loyalty'
param googleWalletIssuerName = 'KBeauty MX'
param googleWalletHexBackgroundColor = '#FFFFFF'
param googleWalletOrigins = 'https://loyaltycloud-admin.azurewebsites.net'

param loyaltyMaintenanceIntervalHours = 12
param loyaltyNotificationsPollIntervalSeconds = 43200
param tenantAdminSessionHours = 168
param superAdminSessionHours = 8
