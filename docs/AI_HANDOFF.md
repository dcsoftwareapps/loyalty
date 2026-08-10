# LoyaltyCloud - AI Handoff

Last updated: 2026-08-10

Branch: `main`

Last task worked: establish permanent AI context by updating `docs/AI_CONTEXT.md` and creating `docs/AI_HANDOFF.md`.

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
- Google Wallet save-link reached working state in STG previously, but Google issuer remains in Demo mode.

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

Do not change endpoint contract unless explicitly requested.

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
- Google Wallet save-link reached working state previously in STG, subject to Google issuer Demo status.

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

2. Google Wallet STG:
   - Confirm `GoogleWallet__Enabled=true` only in intended environment.
   - Confirm `GoogleWallet__IssuerId`.
   - Confirm `GoogleWallet__LogoUri` is public HTTPS and reachable by Google.
   - Confirm `loyaltycloud-google-wallet-service-account-json` resolves from STG Key Vault.
   - Call `POST /api/customers/{serialNumber}/wallets/google/save-link`.
   - Open returned Save URL on Android.
   - Move/publish Google issuer out of Demo mode to remove test-pass warning.

3. Review production/STG settings drift:
   - Admin official host remains `https://loyaltycloud-admin.azurewebsites.net`.
   - API production host remains `https://loyaltycloud-api-894839.azurewebsites.net`.
   - STG does not point to PROD SQL/Storage/Key Vault.

## Next Recommended Step

For the next technical session:

1. Read `docs/AI_CONTEXT.md`.
2. Read this handoff.
3. If working on STG, verify current App Settings and connection strings from Azure before changing code.
4. For Google Wallet STG, tail API logs and retry save-link with a known customer serial after confirming Google issuer/LogoUri/service account setup.

Recommended first command for local orientation:

```powershell
git status --short
```

Do not run build/tests/deploy/migrations unless the user explicitly asks.
