# LoyaltyCloud Staging Infrastructure

This folder contains the operational scripts for creating a fully isolated
LoyaltyCloud STAGING environment with PowerShell + Azure CLI.

The Bicep files in this folder are experimental and are not the active path for
RC1/UAT staging creation. Do not deploy the Bicep templates unless a separate
infrastructure review explicitly approves that path.

## Production Reference

Production must not be modified by these scripts.

```text
Resource Group: rg-loyaltycloud-prod
API: loyaltycloud-api-894839
Admin: loyaltycloud-admin
SQL Server: sql-loyaltycloud-894839
Database: LoyaltyCloudFree
Key Vault: kv-loyaltycloud-894839
Storage: stloyaltycloud894839
API URL: https://loyaltycloud-api-894839.azurewebsites.net
Admin URL: https://loyaltycloud-admin.azurewebsites.net
```

## Staging Target

`create-stg.ps1` creates resources under:

```text
Resource Group: rg-loyaltycloud-stg
Location: westus3
API plan: asp-loyaltycloud-api-stg-$Suffix
API app: loyaltycloud-api-stg-$Suffix
Admin plan: asp-loyaltycloud-admin-stg-$Suffix
Admin app: loyaltycloud-admin-stg-$Suffix
SQL server: sql-loyaltycloud-stg-$Suffix
Database: LoyaltyCloudStg
Key Vault: kv-loyaltycloud-stg-$Suffix
Storage: stloyaltycloudstg$Suffix
Blob container: passes
```

Storage account names are normalized to lowercase alphanumeric characters.

## Prerequisites

Install Azure CLI and authenticate:

```powershell
az --version
az login
az account show
```

Optionally select a subscription:

```powershell
az account set --subscription <subscription-id>
```

The scripts validate Azure CLI, login and target names. They do not run `az login`
for you.

## Dry Run

Dry-run is the default. It validates inputs and prints the plan without creating
or modifying resources.

```powershell
.\infra\create-stg.ps1 -Suffix 894839
```

Use a suffix that is globally available. The example suffix is not guaranteed to
be available.

With explicit subscription:

```powershell
.\infra\create-stg.ps1 -Suffix 894839 -SubscriptionId <subscription-id>
```

## Real Execution

```powershell
.\infra\create-stg.ps1 -Suffix 894839 -Execute
```

The script asks for explicit confirmation:

```text
CREATE STAGING
```

It then asks for the SQL admin password using `Read-Host -AsSecureString`.
The password is never printed, written to disk or stored in source control.

## Parameters

Main parameters:

- `-Suffix`: required, alphanumeric suffix for globally unique names.
- `-Location`: default `westus3`.
- `-ResourceGroup`: default `rg-loyaltycloud-stg`.
- `-SubscriptionId`: optional active subscription selector.
- `-SqlAdminUser`: default `loyaltysqladmin`.
- `-LinuxPlanSku`: default `B1`.
- `-WindowsPlanSku`: default `B1`.
- `-LinuxRuntime`: default `DOTNETCORE:9.0`.
- `-WindowsRuntime`: default `DOTNET:9.0`.
- `-AllowAzureServices`: creates the Azure SQL `0.0.0.0` firewall rule for Azure services only.
- `-DeveloperIp`: optional single IP firewall rule for temporary local migrations.
- `-Execute`: performs writes.

SQL defaults:

- Edition: `GeneralPurpose`.
- Compute model: serverless.
- Family: `Gen5`.
- Min capacity: `0.5`.
- Max capacity: `1`.
- Auto-pause: `60` minutes.
- Backup redundancy: local.
- Max size: `32GB`.

The Free Offer is not assumed automatically. Inspect the created database after
deployment if you need to confirm free-limit behavior.

## App Settings

The scripts configure names currently consumed by the code.

API:

- `ASPNETCORE_ENVIRONMENT=Staging`
- `DOTNET_ENVIRONMENT=Staging`
- `ConnectionStrings__DefaultConnection`
- `Azure__KeyVaultUri`
- `Azure__BlobStorage__ConnectionString`
- `Azure__BlobStorage__PassContainer=passes`
- `Azure__BlobStorage__SasExpirationMinutes=15`
- `AdminApi__SharedSecret`
- `Apple__PassTypeIdentifier`
- `Apple__TeamIdentifier`
- `Apple__WebServiceURL=https://loyaltycloud-api-stg-$Suffix.azurewebsites.net`
- `Apple__OrganizationName`
- `Apple__ApnHost`
- `Wallet__UseRealPassSigning=true`
- `Wallet__UseRealApns=true`
- `GoogleWallet__*`
- `Cors__AllowedOrigins=https://loyaltycloud-admin-stg-$Suffix.azurewebsites.net`
- `LoyaltyMaintenance__IntervalHours=12`
- `LoyaltyMaintenance__RunOnStartup=false`
- `LoyaltyNotifications__PollIntervalSeconds=43200`
- `LoyaltyNotifications__RunOnStartup=false`
- `CustomNotificationCampaigns__BatchSize=50`
- `Provisioning__TrialDays=14`
- `Billing__GracePeriodDays=7`

Admin:

- `ASPNETCORE_ENVIRONMENT=Staging`
- `DOTNET_ENVIRONMENT=Staging`
- `ConnectionStrings__DefaultConnection`
- `Admin__ApiBaseUrl=https://loyaltycloud-api-stg-$Suffix.azurewebsites.net`
- `Admin__Auth__SessionHours=168`
- `AdminApi__SharedSecret`
- `SuperAdmin__Username`
- `SuperAdmin__PasswordHash`
- `SuperAdmin__SessionHours=8`
- `Azure__KeyVaultUri`
- `Azure__BlobStorage__ConnectionString`
- `Azure__BlobStorage__PassContainer=passes`
- `Apple__*`
- `Wallet__UseRealPassSigning=true`
- `Wallet__UseRealApns=true`
- `GoogleWallet__*`
- `Provisioning__TrialDays=14`
- `Billing__GracePeriodDays=7`

Current code registers real Apple Wallet signing and `ApnService` outside
Development when `Azure:KeyVaultUri` is present. The Admin should still send
operational Wallet/APNs work through the API where the flows already do so, but
its process must be able to start with the same Key Vault-backed infrastructure
registrations.

## Key Vault References

App settings use Key Vault references:

```text
@Microsoft.KeyVault(SecretUri=https://kv-loyaltycloud-stg-$Suffix.vault.azure.net/secrets/<secret-name>)
```

API and Admin use only the STAGING Key Vault.

## Automatic Secrets

`create-stg.ps1` creates these STAGING secrets because their values are derived
from STAGING resources:

- `loyaltycloud-sql-connection-string`
- `loyaltycloud-storage-connection-string`

They are not printed and are not written to disk.

## Manual Secrets

Use:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 894839 -ConfigureSuperAdmin -ConfigureAppleWallet -Execute
```

Optional Google Wallet:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 894839 -ConfigureGoogleWallet -GoogleWalletServiceAccountJsonPath C:\secure\google-wallet-stg.json -Execute
```

Manual secret names:

- `loyaltycloud-admin-api-shared-secret`
- `loyaltycloud-superadmin-username`
- `loyaltycloud-superadmin-password-hash`
- `kbeauty-pass-certificate`
- `kbeauty-pass-certificate-password`
- `kbeauty-wwdr-certificate`
- `kbeauty-apn-private-key`
- `kbeauty-apn-key-id`
- `kbeauty-apn-team-id`
- `loyaltycloud-google-wallet-service-account-json`

The `kbeauty-*` names are legacy provider names still expected by
`KeyVaultAppleWalletSecretsProvider`.

## Apple Wallet

Do not copy Apple credentials from production automatically.

May be shared temporarily for RC1/UAT:

- Apple Team ID.
- Apple Pass Type ID, subject to the current strategy.

Must be loaded manually:

- Pass certificate `.p12` as Base64 secret `kbeauty-pass-certificate`.
- Pass certificate password.
- APNs `.p8` private key.
- APNs key ID.
- APNs team ID.

`Apple:WebServiceURL` must point to the staging API URL. Using the same Pass Type
ID in production and staging means both environments can issue passes of the
same type; test carefully and avoid mixing real customers across environments.

## Google Wallet

Google Wallet settings were introduced by commit `584dd1f feat: GOOGLE WALLET`.

Relevant settings:

- `GoogleWallet:Enabled`
- `GoogleWallet:IssuerId`
- `GoogleWallet:ClassSuffix`
- `GoogleWallet:ObjectIdPrefix`
- `GoogleWallet:ProgramName`
- `GoogleWallet:IssuerName`
- `GoogleWallet:LogoUri`
- `GoogleWallet:HeroImageUri`
- `GoogleWallet:HexBackgroundColor`
- `GoogleWallet:Origins`
- `GoogleWallet:ServiceAccountJson`
- `GoogleWallet:ServiceAccountJsonPath`
- `GoogleWallet:ApiBaseUrl`
- `GoogleWallet:TokenEndpoint`
- `GoogleWallet:SaveUrlBase`

For staging, use a staging Google Wallet issuer/service account when available.
Do not copy the production service account JSON automatically. `LogoUri` must be
a public HTTPS image reachable by Google.

## Blob Storage

The code currently uses one configured container:

```text
Azure:BlobStorage:PassContainer = passes
```

Tenant logos and Wallet assets are stored as blob prefixes under that container:

```text
tenant-branding/{tenantId}/logo-original...
tenant-branding/{tenantId}/wallet/logo.png
tenant-branding/{tenantId}/wallet/logo@2x.png
tenant-branding/{tenantId}/wallet/logo@3x.png
tenant-branding/{tenantId}/wallet/icon.png
tenant-branding/{tenantId}/wallet/icon@2x.png
tenant-branding/{tenantId}/wallet/icon@3x.png
tenants/{tenantSlug}/passes/{serial}.pkpass
```

Do not copy blobs from production.

## Firewall for Migrations

For Azure-hosted apps, use:

```powershell
.\infra\create-stg.ps1 -Suffix 894839 -AllowAzureServices -Execute
```

For temporary local EF migrations from a developer machine, explicitly pass a
single known public IP:

```powershell
.\infra\create-stg.ps1 -Suffix 894839 -DeveloperIp 203.0.113.10 -Execute
```

Do not open `0.0.0.0 - 255.255.255.255`.

## Migrations

The infrastructure script does not apply EF migrations.

After STAGING infrastructure and secrets are ready, run migrations manually
against `LoyaltyCloudStg` only after verifying the effective connection string.

## Deploy API Linux

```powershell
dotnet publish .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj -c Release -o .\artifacts\api
tar -a -c -f .\artifacts\api.zip -C .\artifacts\api .
az webapp deploy --resource-group rg-loyaltycloud-stg --name loyaltycloud-api-stg-894839 --src-path .\artifacts\api.zip --type zip
```

## Deploy Admin Windows

```powershell
dotnet publish .\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj -c Release -o .\artifacts\admin
Compress-Archive -Path .\artifacts\admin\* -DestinationPath .\artifacts\admin.zip -Force
az webapp deploy --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-894839 --src-path .\artifacts\admin.zip --type zip
```

## Verification

After deploy:

```powershell
az webapp config appsettings list -g rg-loyaltycloud-stg -n loyaltycloud-api-stg-894839
az webapp config appsettings list -g rg-loyaltycloud-stg -n loyaltycloud-admin-stg-894839
az sql db show -g rg-loyaltycloud-stg -s sql-loyaltycloud-stg-894839 -n LoyaltyCloudStg
az keyvault secret list --vault-name kv-loyaltycloud-stg-894839
```

Smoke tests:

- API starts without Key Vault reference failures.
- Admin `/platform/login` loads.
- Platform Admin can provision a tenant.
- Tenant Admin can log in.
- Public join works.
- Apple Wallet pass can be generated from STAGING API.
- Google Wallet save link works only if Google Wallet was configured.

## Rollback Manual

No automatic deletion is provided.

If staging must be removed, review resources in `rg-loyaltycloud-stg` and delete
the resource group manually only after confirming it contains no production
resources:

```powershell
az group show --name rg-loyaltycloud-stg
az group delete --name rg-loyaltycloud-stg
```

## Cost Notes

- SQL serverless with auto-pause is selected to reduce idle cost.
- B1 App Service plans are economical but are not free.
- Always On is disabled by the script.
- Storage is Standard LRS.
- Key Vault operations may still incur minor usage cost.

## What Is Not Shared With Production

- SQL server.
- Database.
- Key Vault.
- Storage account.
- App Services.
- App Service plans.
- Runtime app settings.
- Derived connection string secrets.

Production Apple/Google credentials are not copied automatically.
