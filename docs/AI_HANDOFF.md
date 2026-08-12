# LoyaltyCloud - AI Handoff

Last updated: 2026-08-12

Branch: `main`

Last task worked: document successful Azure SQL STG migration from General Purpose Serverless to Basic DTU.

## Current State

LoyaltyCloud is in RC1/UAT.

Current codebase state at the end of this handoff task:

- Documentation-only changes were made.
- No functional code was changed.
- No build was executed.
- No tests were executed.
- No EF migrations were created or applied.
- No database update was executed.
- No deploy was executed.
- No commit was created.

Active product status:

- PROD/UAT Admin official host: `https://loyaltycloud-admin.azurewebsites.net`.
- PROD/UAT API host: `https://loyaltycloud-api-894839.azurewebsites.net`.
- PROD/UAT active database: `LoyaltyCloudFree`.
- STG exists separately with `loyaltycloud-api-stg-01` and `loyaltycloud-admin-stg-01`.
- Apple Wallet works in production/UAT.
- Google Wallet is approved for production and STG generates Save Links correctly.
- PROD has `GoogleWallet__*` App Settings configured.
- PROD Key Vault contains `loyaltycloud-google-wallet-service-account-json`.
- PROD `GoogleWallet__ServiceAccountJson` references `loyaltycloud-google-wallet-service-account-json` through Key Vault.
- PROD SQL is currently General Purpose Serverless `GP_S_Gen5_2`, `minCapacity=0.5`, `autoPauseDelay=60`.
- STG SQL `LoyaltyCloudStg` was migrated successfully to Basic DTU with 2 GB max size.
- PROD and STG API/Admin App Service Plans are currently F1 Free.
- Basic DTU for PROD remains under evaluation to eliminate cold start, but PROD was not modified.
- API STG, Admin STG and Wallet were manually validated after the STG SQL migration.
- Pending decision: `GoogleWallet__ProgramName` is currently `KBeauty Loyalty`; changing it to `KBeauty` is under consideration, then making it configurable by tenant later.

## Recent Infrastructure and Configuration Work

STG infrastructure was created with PowerShell/Azure CLI scripts:

- `infra/create-stg.ps1`.
- `infra/configure-stg-secrets.ps1`.
- `infra/copy-apple-wallet-secrets-to-stg.ps1`.

STG resource names:

| Resource | Name |
| --- | --- |
| Resource Group | `rg-loyaltycloud-stg` |
| API App Service Plan Linux | `asp-loyaltycloud-api-stg-01` |
| API App Service | `loyaltycloud-api-stg-01` |
| API URL | `https://loyaltycloud-api-stg-01.azurewebsites.net` |
| Admin App Service Plan Windows | `asp-loyaltycloud-admin-stg-01` |
| Admin App Service | `loyaltycloud-admin-stg-01` |
| Admin URL | `https://loyaltycloud-admin-stg-01.azurewebsites.net` |
| SQL Server | `sql-loyaltycloud-stg-01` |
| SQL Database | `LoyaltyCloudStg` |
| Storage | `stloyaltycloudstg01` |
| Blob container | `passes` |
| Key Vault | `kv-loyaltycloud-stg-01` |

STG App Settings/connection strings were restored after App Services were recreated.

Critical STG settings:

- `Azure__KeyVaultUri` for API and Admin must be `https://kv-loyaltycloud-stg-01.vault.azure.net/`.
- `ConnectionStrings__DefaultConnection` for API and Admin STG uses Key Vault secret `loyaltycloud-sql-connection-string`.
- Correct Key Vault reference for SQL is `@Microsoft.KeyVault(VaultName=kv-loyaltycloud-stg-01;SecretName=loyaltycloud-sql-connection-string)`.
- `Admin__ApiBaseUrl` on Admin STG must point to `https://loyaltycloud-api-stg-01.azurewebsites.net`.
- `AdminApi__SharedSecret` must match between API STG and Admin STG.
- Google Wallet STG secret name is `loyaltycloud-google-wallet-service-account-json`.

PROD Google Wallet configuration state:

- Google Wallet is approved for production.
- PROD has `GoogleWallet__*` settings configured.
- `GoogleWallet__ServiceAccountJson` is configured through a Key Vault reference.
- Key Vault PROD contains the secret `loyaltycloud-google-wallet-service-account-json`.
- Never document or print the service account JSON, private key, passwords, tokens, shared secrets or connection strings.

Current SQL/App Service cost posture:

- PROD SQL currently uses Azure SQL General Purpose Serverless `GP_S_Gen5_2`, `minCapacity=0.5`, `autoPauseDelay=60`.
- STG SQL `LoyaltyCloudStg` now uses Basic DTU with 2 GB max size.
- STG no longer depends on Serverless auto-pause, so the cold start caused by waking SQL Serverless is removed for STG.
- PROD is still under evaluation for a possible future move to Basic DTU.
- API and Admin App Service Plans in both PROD and STG are currently F1 Free.

Important PowerShell/Azure CLI lesson:

- Setting Key Vault references from PowerShell can break if the final `)` is swallowed or misquoted.
- The reliable method for App Service connection strings was to use JSON files with `az webapp config connection-string set`.
- Azure CLI can print warnings to stderr even when exit code is 0; wrappers must fail only on nonzero exit code.

## Important Commands Used

STG infra dry-run:

```powershell
.\infra\create-stg.ps1 -Suffix 01
```

STG infra execute:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -Execute
```

Configure selected STG secrets:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureAdminApi -Execute
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureSuperAdmin -Execute
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureAppleWallet -Execute
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureGoogleWallet -GoogleWalletServiceAccountJsonPath "C:\secure\google-wallet-stg.json" -Execute
```

Copy allowlisted Apple Wallet secrets from PROD to STG:

```powershell
.\infra\copy-apple-wallet-secrets-to-stg.ps1
.\infra\copy-apple-wallet-secrets-to-stg.ps1 -Execute
```

Common validation commands used during RC1 work:

```powershell
dotnet ef migrations has-pending-model-changes --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
dotnet build .\LoyaltyCloud.sln
```

No commands were run for this documentation task except repository inspection.

## Recent Errors and Root Causes

### STG App Services lost settings after recreation

Problem:

- STG App Services were recreated.
- API and Admin lost App Settings and connection strings.
- Admin initially returned HTTP 500.30.

Root cause:

- Recreated App Services did not retain previous App Settings/connection strings.
- Admin tried to access legacy/default `kbeauty-kv.vault.azure.net`.
- Admin also lacked `DefaultConnection`.

Fix applied outside this doc task:

- API STG settings were restored.
- Admin STG settings were restored based on PROD, adapted to STG URLs/vaults.
- `Azure__KeyVaultUri` set to `https://kv-loyaltycloud-stg-01.vault.azure.net/`.
- `AdminApi__SharedSecret` restored to match API STG.
- `ConnectionStrings__DefaultConnection` restored for Admin STG.

Validated:

- Admin STG starts.
- SuperAdmin login works.
- API STG starts and responds.

Do not reinvestigate WebSockets, Blazor interactivity or SQL availability for this incident unless a new symptom appears.

### Key Vault reference quoting from PowerShell

Problem:

- Azure CLI commands from PowerShell could mishandle the final `)` in Key Vault references.

Root cause:

- PowerShell/Azure CLI quoting around App Service Key Vault references is fragile.

Reliable method:

- Use JSON files for `az webapp config connection-string set`.
- Do not hand-type long Key Vault reference values directly when a JSON file is safer.

### `configure-stg-secrets.ps1` prompted for unrelated secrets

Problem:

- Running only `-ConfigureGoogleWallet` still prompted for Admin API shared secret and previously could prompt unrelated secrets.

Root cause:

- `Configure-AdminApiSecret` was called unconditionally and had no switch guard.

Fix:

- Added `-ConfigureAdminApi`.
- `Configure-AdminApiSecret` now returns unless `-ConfigureAdminApi` is passed.
- Separate examples were documented in `docs/STAGING_SETUP.md`.

### `kbeauty-wwdr-certificate` missing in PROD Key Vault

Problem:

- Apple Wallet copy script originally treated WWDR as required, but PROD Key Vault did not contain it.

Root cause:

- Current production signing works without independent WWDR secret.

Code behavior:

- `PassGeneratorService` first uses certificates in the `.p12` and bundled `Certificates/AppleWWDRCAG4.cer`.
- `KeyVaultAppleWalletSecretsProvider.GetWwdrCertificateBytesAsync` returns null on 404.

Fix:

- `kbeauty-wwdr-certificate` is optional in the copy script/documentation.

### Google Wallet save-link generic 400

Problem:

- STG endpoint returned a generic 400: "No se pudo generar el enlace de Google Wallet. Revise configuracion, credenciales y logs."

Root cause:

- Exception was intentionally converted to a generic user-facing error and logs were not detailed enough.

Fix:

- Added structured logging in `GoogleWalletService.GetOrCreateSaveLinkAsync`.
- Full `ex.ToString()` is logged.
- If a `Google.GoogleApiException` is ever present, logging tries to capture status, error code, message, errors and response body without exposing secrets.
- `GoogleWalletClient.CreateExceptionAsync` now preserves full Google REST response body in thrown exception.

### 2026-08-11 - Google Wallet production-approved LoyaltyClass PATCH

Incident:

- After the Google Wallet Issuer was approved for production, Save Link generation started failing in STG with HTTP 400.

Error:

```text
Invalid review status "APPROVED". Use "UNDER_REVIEW" instead.
```

- The error occurred during the `PATCH` of `LoyaltyClass` in `GoogleWalletClient.EnsureLoyaltyClassAsync()`.

Root cause:

- `GoogleWalletClient.EnsureLoyaltyClassAsync()` sent a `PATCH` payload for `LoyaltyClass` that ended up including `reviewStatus = APPROVED`.
- Google Wallet does not allow clients to set `APPROVED` through the API.
- `APPROVED` is assigned only by Google.

Solution:

- `GoogleWalletClient` was updated so the `PATCH` of `LoyaltyClass` uses `reviewStatus = UNDER_REVIEW` again.
- No changes were made to `LoyaltyObject` generation or update logic.

Validation:

- `dotnet test .\tests\LoyaltyCloud.Tests\LoyaltyCloud.Tests.csproj --filter FullyQualifiedName~GoogleWalletObjectMapperTests`
- 3 tests passed.
- `dotnet build .\LoyaltyCloud.sln`
- Build succeeded with 0 warnings and 0 errors.

STG post-deploy validation:

- `POST /api/customers/{serial}/wallets/google/save-link` generated `saveUrl` correctly again.
- Google Wallet is now production approved.
- Google Wallet is no longer in Demo mode.
- STG generates Save Links correctly.

Do not change endpoint contract unless explicitly requested.

### 2026-08-12 - Azure SQL STG migrated from Serverless to Basic DTU

Incident/objective:

- STG SQL was causing cold start because `LoyaltyCloudStg` was running as General Purpose Serverless with auto-pause.
- The goal was to migrate only STG to Basic DTU before considering any PROD change.

Infrastructure:

- Resource Group: `rg-loyaltycloud-stg`.
- SQL Server: `sql-loyaltycloud-stg-01`.
- Database: `LoyaltyCloudStg`.
- Azure Monitor region reported: `westus3`.

Initial state:

- Service objective: `GP_S_Gen5_2`.
- Tier: `GeneralPurpose`.
- Model: Serverless.
- `minCapacity=0.5`.
- `autoPauseDelay=60`.
- `maxSizeBytes=34359738368` (32 GB).
- `useFreeLimit=true`.

Troubleshooting:

1. Direct Serverless to Basic change failed.
   - Cause: the database had 32 GB max size and Basic supports a much smaller max size.
   - Decision: do not reduce size arbitrarily until real storage usage was verified.

2. Direct SQL query with `sqlcmd -G` failed.
   - Cause: Azure/Entra `ActiveDirectoryIntegrated` authentication failed.
   - Decision: stop spending time on `sqlcmd` and use Azure Monitor for actual storage usage.

3. First Azure Monitor attempt failed because of PowerShell argument handling.
   - Error: `argument --resource: expected one argument`.
   - Fix: get the database Resource ID first and store it in `$dbId`.
   - Resource ID used:

```text
/subscriptions/90f061a5-f51e-4ed9-95d7-6f9ed3ca3995/resourceGroups/rg-loyaltycloud-stg/providers/Microsoft.Sql/servers/sql-loyaltycloud-stg-01/databases/LoyaltyCloudStg
```

4. Basic change was blocked after reducing max size.
   - Command attempted:

```powershell
az sql db update --resource-group rg-loyaltycloud-stg --server sql-loyaltycloud-stg-01 --name LoyaltyCloudStg --service-objective Basic
```

   - Azure error:

```text
(ProvisioningDisabled) Provisioning of free limit database is not supported for provided service level objective or region
```

   - Root cause: the database still had `useFreeLimit=true`.
   - Fix: remove Free Limit before changing service objective.

Commands that worked:

Get Resource ID:

```powershell
$dbId = az sql db show `
  --resource-group rg-loyaltycloud-stg `
  --server sql-loyaltycloud-stg-01 `
  --name LoyaltyCloudStg `
  --query id `
  -o tsv
```

Query storage through Azure Monitor:

```powershell
az monitor metrics list --resource $dbId --metric storage --interval PT1H --aggregation Average -o json
```

Result:

- Azure Monitor reported `28246016 bytes`.
- Approximate usage: 26.9 MiB.
- This was around 1.3% of 2 GB, so reducing max size from 32 GB to 2 GB was safe for current STG state.

Reduce max size to 2 GB:

- Verification after max-size reduction:

```json
{
  "currentServiceObjectiveName": "GP_S_Gen5_2",
  "maxSizeBytes": 2147483648,
  "sku": "GP_S_Gen5",
  "tier": "GeneralPurpose"
}
```

Remove Free Limit:

- `useFreeLimit=true` was removed before the Basic migration.

Final migration to Basic:

- After Free Limit was removed and `max-size=2GB`, migration to Basic succeeded.
- Final verification:

```json
{
  "maxSizeBytes": 2147483648,
  "sku": "Basic",
  "tier": "Basic",
  "useFreeLimit": null
}
```

Final validated state:

- Database: `LoyaltyCloudStg`.
- Tier: Basic.
- SKU: Basic.
- Max size: 2 GB.
- No longer General Purpose Serverless.
- No longer depends on auto-pause.
- Serverless cold start is removed for STG.
- Storage observed during migration: approximately 26.9 MiB.
- API STG, Admin STG and Wallet were manually validated after the migration.

PROD:

- PROD was not modified.
- This change applies only to STG.
- Do not assume PROD should move to Basic.
- The PROD decision will be made later after observing STG behavior, costs and limitations.

### PowerShell 5.1 incompatibilities in infra scripts

Problems:

- `ProcessStartInfo.ArgumentList` is unavailable in Windows PowerShell 5.1.
- Azure CLI stderr warnings were treated as fatal.
- Resource-not-found output from Azure CLI interrupted dry-runs.
- Key Vault "not found within subscription" message was not treated as expected not-found.
- Windows Web App create path was accidentally interpreted as Linux runtime.

Root causes:

- Scripts originally assumed newer PowerShell/runtime APIs and did not isolate stdout/stderr/exit code robustly.
- Windows and Linux App Service creation need different Azure CLI parameters.

Fixes:

- Rewrote Azure CLI wrappers with Windows PowerShell 5.1-compatible process APIs.
- Fail only on nonzero exit code.
- ResourceGroupNotFound/ResourceNotFound/Key Vault not-found are expected for read/show checks.
- Windows Web App is created without Linux runtime and then configured with `--net-framework-version v9.0`.

## What Is Already Validated

Do not re-investigate these without new evidence:

- Apple Wallet end-to-end in production/UAT: Admin/API/APNs/iPhone/registrations/GET pass works.
- Wallet pass generation reads current SQL data dynamically.
- `PointsAdded` visible notification uses a temporary field with `%@`, not permanent `points`.
- Level rename/display in Wallet was corrected to use dynamic tenant levels.
- Blazor Admin Interactive Server tenant context issue was corrected via tenant context restoration from authenticated claims.
- Admin STG HTTP 500.30 from wrong Key Vault URI was resolved by restoring correct STG settings.
- Admin STG SuperAdmin login works after restoring `DefaultConnection`.
- API STG starts and responds after settings restoration.
- Google Wallet is production approved and STG Save Link generation works after the LoyaltyClass `reviewStatus` PATCH fix.
- PROD Google Wallet settings exist and point `GoogleWallet__ServiceAccountJson` to Key Vault secret `loyaltycloud-google-wallet-service-account-json`.
- Azure SQL STG `LoyaltyCloudStg` was migrated successfully to Basic DTU.
- API STG, Admin STG and Wallet were manually validated after the STG SQL migration.

## What Still Needs Testing

Priority:

1. Full STG smoke after latest config restoration:
   - API health/basic response.
   - Admin SuperAdmin login.
   - Create tenant in STG if needed.
   - Tenant Admin login.
   - Public join.
   - Apple Wallet pass download from STG.
   - QR scan/add points.
   - Redeem monetary discount.

2. Google Wallet STG regression smoke:
   - Confirm `GoogleWallet__Enabled=true` only in intended environment.
   - Confirm `GoogleWallet__IssuerId`.
   - Confirm `GoogleWallet__LogoUri` is public HTTPS and reachable by Google.
   - Confirm `loyaltycloud-google-wallet-service-account-json` resolves from STG Key Vault.
   - Call `POST /api/customers/{serialNumber}/wallets/google/save-link`.
   - Open returned Save URL on Android.
   - Confirm no Demo/test-pass warning appears for production-approved issuer.
   - Validate today's final STG changes before promoting to PROD if additional non-SQL changes are made.

3. Review production/STG settings drift:
   - Admin official host remains `https://loyaltycloud-admin.azurewebsites.net`.
   - API production host remains `https://loyaltycloud-api-894839.azurewebsites.net`.
   - STG does not point to PROD SQL/Storage/Key Vault.
   - PROD and STG `GoogleWallet__ProgramName` are intentionally still `KBeauty Loyalty` until the naming decision is made.

4. SQL hosting decision:
   - Observe STG on Basic DTU for behavior, costs and limitations.
   - Evaluate later whether PROD should remain General Purpose Serverless `GP_S_Gen5_2` or move to Basic DTU.
   - PROD was not modified by the STG migration.

## Next Recommended Step

For the next technical session:

1. Read `docs/AI_CONTEXT.md`.
2. Read this handoff.
3. If working on STG, verify current App Settings and connection strings from Azure before changing code.
4. For Google Wallet STG, keep `reviewStatus = UNDER_REVIEW` in LoyaltyClass PATCH payloads and retry save-link with a known customer serial if a regression appears.
5. Finish validating today's STG changes before any PROD deploy.

Recommended first command for local orientation:

```powershell
git status --short
```

Do not run build/tests/deploy/migrations unless the user explicitly asks.
