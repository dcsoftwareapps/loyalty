# LoyaltyCloud - AI Handoff

Last updated: 2026-08-30

Branch: `feature/apple-wallet-logo-scale`

Last task worked: Apple Wallet logo scale per tenant.

## 2026-08-30 - Apple Wallet logo scale per tenant

Current branch for this work: `feature/apple-wallet-logo-scale`.

Scope:

- Added `TenantBranding.WalletLogoScalePercent` for Apple Wallet visual logo sizing.
- Range is 60-100, default 100. Default 100 preserves the previous rendering.
- Tenant Admin `/config` shows a 60-100 slider with step 5 in the Tarjeta digital section.
- Apple Wallet logo scaling is implemented by rendering the logo inside a smaller centered box while keeping final PNG dimensions unchanged: `logo.png` 160x50, `logo@2x.png` 320x100 and `logo@3x.png` 480x150.
- Existing wallet logos can be regenerated from the stored original blob when only the slider changes; users do not need to reupload the logo.
- Apple Wallet branding changes continue to use the existing best-effort installed-pass refresh/APNs path.
- Google Wallet is intentionally not affected by this feature. Google continues to use the unscaled shared wallet logo asset.

Validation expected for this task:

- focused TenantBranding / WalletProductionUpdate / Google Wallet mapper tests;
- `dotnet build .\LoyaltyCloud.sln -c Release`;
- `git diff --check`;
- no database update, deploy, commit or push.

## 2026-08-29 - Custom message notification/detail split

Current branch for this work: `feature/message-notification-details`.

Scope:

- Tenant Admin `/marketing-notifications` now collects two business-facing fields:
  - `Notificación`: short text for phone notifications.
  - `Detalle del mensaje`: longer content shown when the customer opens/consults the message.
- Existing technical model already had `CustomNotificationCampaign.ShortMessage`, `CustomNotificationCampaign.LongMessage`, `LoyaltyNotification.ShortMessage` and `LoyaltyNotification.LongMessage`; no schema change was required.
- Custom campaign creation sends the short text and long detail explicitly instead of deriving a short message from one textarea.
- Apple Wallet keeps using the short text for the temporary visible field/changeMessage and the long detail in pass back fields.
- Google Wallet notification delivery now maps short text to `Message.header` and long detail to `Message.body`, with `TEXT_AND_NOTIFY` still handled by the Google Wallet client.
- Backward compatibility: legacy notifications without short/long values fall back to existing `Title`/`Message`.

Validation expected for this task:

- focused Admin marketing notification tests;
- Google Wallet notification tests;
- Wallet pass JSON update tests;
- `dotnet build .\LoyaltyCloud.sln -c Release`;
- no database update, deploy, commit or push.

## 2026-08-27 - Reusable temporary STG deploy workflow

Current branch for this work: `feature/admin-dashboard-modernization`.

Added a reusable STG-only deploy script:

```powershell
.\scripts\deploy-stg.ps1 -Branch "feature/<branch-name>" -Target Admin -DryRun
.\scripts\deploy-stg.ps1 -Branch "feature/<branch-name>" -Target Admin -Execute
```

Supported targets are `Admin`, `Api` and `Both`.

Important behavior:

- Dry-run is the default when `-Execute` is not passed.
- The script checks repo root, working tree, Azure CLI login, branch/ref existence and commit metadata.
- Real deploys require a clean working tree before checkout/update.
- Dry-run does not checkout/update a branch when local changes are present; it reports that a real deploy would stop.
- Branch updates use `origin` and fast-forward only; no arbitrary merges are performed.
- The EF migrations path is detected from `AppDbContextModelSnapshot.cs`; current physical path is `src/LoyaltyCloud.Infrastructure/Persistence/Migrations`.
- New migration files are detected relative to `origin/staging`.
- If migration files are detected, deploy is blocked and no database update is executed.
- Packaging uses `tar -a -c -f`, not `Compress-Archive`.
- Targets are fixed to STG resources: `rg-loyaltycloud-stg`, `loyaltycloud-api-stg-01`, `loyaltycloud-admin-linux-stg-01`.
- PROD resource names are explicitly refused.
- The script does not modify App Settings, Key Vault, SQL, firewall, slots, commits, pushes, PRs, tags or merges.

Documentation updated:

- `docs/RELEASE_PROCESS.md` now documents temporary STG branch deploys.
- `docs/AI_CONTEXT.md` references `scripts/deploy-stg.ps1` as the canonical reusable STG deploy helper.

## 2026-08-26 - Reports v1 UX and navigation refactor

Current branch for this work: `feature/reports-summary`.

Scope:

- `/reports` is now a lightweight reports landing, not a KPI/dashboard page.
- Added dedicated report pages:
  - `/reports/inactive-customers`
  - `/reports/top-rewards`
- Customers and redemptions remain on their existing pages/routes and are linked from the Reports sidebar group.
- Reports v1 uses the existing Dashboard-style pattern: Blazor page -> MediatR query -> read service -> EF Core `AsNoTracking`.
- No new API endpoint was added.
- No schema/model change or migration was added.
- Individual report pages own their filters and query only the data they need.

Definitions:

- Active customer: unique customer with at least one point transaction or redemption in the selected period.
- Inactive customer: active customer/card with no point transaction or redemption for the selected threshold. Customers with no activity use `Customer.CreatedAt` as the reference.
- Registered purchase: `PointTransaction.Type == Purchase`.
- Registered purchase amount: sum of `PointTransaction.PurchaseAmount` for purchase transactions.
- Points issued: positive `Purchase`, `BonusWelcome`, `BonusBirthday` and `BonusReferral` point transactions. `RedemptionReversal` is intentionally excluded.
- Points redeemed: non-cancelled `Redemption.PointsSpent`, avoiding double counting with point transactions.
- Points expired: negative `PointTransaction` rows with `Type == Expiry` or `Expired`.
- Counted redemption: `Redemption.Status != Cancelled`.

Known limitation:

- Top redeemed rewards display the current `RewardCatalogItem.Name`. `Redemption` does not store a historical reward-name snapshot.
- The period/current-program KPI cards from the first Reports v1 draft were intentionally removed from the Reports UI. They were not moved to Dashboard in this task.

## 2026-08-25 - Apple Wallet branding refresh and APNs reliability

Current branch for this work: `feature/wallet-card-branding`.

Scope:

- Installed Apple Wallet passes did not reliably refresh after wallet color/logo changes even though regenerated passes contained the new branding.
- Branding had a parallel best-effort APNs loop instead of the `LoyaltyNotification`/`NotificationDelivery` path used by points and visible events.
- `ApnService` could log a non-2xx APNs response and return normally, letting callers count a rejected push as accepted.

Changes implemented:

- `IApnService.SendPassUpdateAsync` now returns an explicit APNs result.
- HTTP 200 is success.
- HTTP 429/5xx plus timeout/network failures are transient.
- Permanent APNs reasons such as `BadDeviceToken`, `Unregistered` and `DeviceTokenNotForTopic` are treated as permanent because all non-429/non-5xx APNs failures are not counted as success.
- `NoOpApnService` returns an unsupported/no-op result and must not be counted as APNs accepted.
- Added shared Apple Wallet pass refresh service for touch/save/device lookup/APNs/result logging.
- `TenantWalletCardBrandingService` now uses that shared refresh path after persisting branding. Branding remains best-effort: APNs failure does not roll back saved branding and no visible `changeMessage` is produced.
- `AppleWalletNotificationChannelProcessor` now consumes the shared refresh result for `NotificationDelivery` status/counts.
- Transient notification delivery failures become eligible for automatic retry with existing fields: `AttemptCount`, `CompletedAt`, `ProcessingStartedAt` and `FailureReason`.
- Backoff is intentionally simple: attempt 1 retry after about 1 minute, attempt 2 after about 5 minutes, max attempts still comes from `LoyaltyNotifications:MaxAttempts`.
- Permanent APNs failures are not selected for automatic retry.
- Old stuck `Processing` notifications are eligible for recovery after about 15 minutes.
- `LoyaltyNotifications:PollIntervalSeconds` changed from 43200 to 120.
- `LoyaltyNotifications:RunOnStartup` changed from false to true.
- `LoyaltyMaintenance` remains a separate 12-hour worker and was not changed.

Operational note:

- STG and PROD currently use Azure SQL Basic DTU, not Serverless auto-pause. The previous 12-hour notification polling interval was mainly a Serverless cost/cold-start guardrail and is no longer required in the same way.
- Azure App Settings can override `appsettings.json`. After deploy, update API STG/PROD settings intentionally if the environment already has `LoyaltyNotifications__PollIntervalSeconds` or `LoyaltyNotifications__RunOnStartup`.

## Current State

LoyaltyCloud is in RC1/UAT with Billing/Payments live in PROD.

Current codebase state at the end of this handoff task:

- `main` and `origin/main` point to `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.
- PROD was already deployed and validated from this SHA before the release tag was created.
- Annotated tag `v1.0.0` was created at `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.
- Tag `v1.0.0` was pushed to `origin`.
- `docs/RELEASE_PROCESS.md` was created to document the simple PROD release workflow.
- `docs/AI_CONTEXT.md` and this handoff were updated to reference the new release policy.
- Permanent development rule added: never develop a new feature directly on `main`.
- Before implementing a new feature, verify the current branch. If currently on `main`, create a dedicated feature branch before modifying code.
- Branch naming convention: `feature/<name>`, `bugfix/<name>`, `hotfix/<name>`.
- Wallet Card Branding finalization is on `feature/wallet-card-branding`.
- `feature/wallet-card-branding` was updated with `origin/staging` at `d515f23b6bf25f2238496f16faa95a7cfe21ae7a` before finalizing the branch, preserving recurring billing changes.
- EF migration `20260825052012_AddWalletCardBranding` adds `TenantBrandings.WalletBackgroundColor` and `TenantBrandings.WalletLogoBlobName`.
- Tenant Admin `/config` wallet branding mutations now go through the API with signed Admin API requests instead of executing MediatR/Application directly inside Admin.
- Wallet branding changes mark installed Apple passes updated through `TenantWalletCardBrandingService` and send best-effort APNs after persistence.
- Local validation passed: `dotnet build .\LoyaltyCloud.sln`, `dotnet test .\tests\LoyaltyCloud.Tests\LoyaltyCloud.Tests.csproj --filter "Category=TenantBranding|Category=WalletProductionUpdate"`, and EF pending-model check.
- No migration was applied.
- No database update was executed.
- No deploy was executed.

Active product status:

- PROD/UAT Admin public host for new links/QR: `https://admin.loyaltycloud.net`.
- New PROD Admin Linux App Service: `loyaltycloud-admin-prod-01`.
- Legacy PROD/UAT Admin Windows host remains online during transition: `https://loyaltycloud-admin.azurewebsites.net`.
- PROD/UAT API custom domain: `https://api.loyaltycloud.net`.
- PROD/UAT API Linux App Service: `loyaltycloud-api-894839`.
- Legacy PROD/UAT API host remains available: `https://loyaltycloud-api-894839.azurewebsites.net`.
- PROD/UAT active database: `LoyaltyCloudFree`.
- STG exists separately with API `loyaltycloud-api-stg-01`, original Admin Windows `loyaltycloud-admin-stg-01`, and Admin Linux `loyaltycloud-admin-linux-stg-01`.
- Apple Wallet works in production/UAT.
- Google Wallet is approved for production and STG generates Save Links correctly.
- PROD has `GoogleWallet__*` App Settings configured.
- PROD Key Vault contains `loyaltycloud-google-wallet-service-account-json`.
- PROD `GoogleWallet__ServiceAccountJson` references `loyaltycloud-google-wallet-service-account-json` through Key Vault.
- PROD SQL `LoyaltyCloudFree` was migrated successfully to Basic DTU with 2 GB max size.
- STG SQL `LoyaltyCloudStg` was migrated successfully to Basic DTU with 2 GB max size.
- PROD API and new PROD Admin Linux share App Service Plan `asp-loyaltycloud-api-free`, now SKU `B1`, tier `Basic`, capacity `1`, West US 3.
- The plan name still contains `free`, but it is no longer F1. Do not recreate/rename only because of the legacy name.
- Legacy PROD Admin Windows remains on its previous Windows plan during the transition.
- STG plans can remain F1 for now.
- PROD and STG no longer depend on Azure SQL Serverless auto-pause, so SQL cold start from waking a paused database is removed for both environments.
- API PROD, Admin PROD and Wallet PROD were manually validated after the PROD SQL migration.
- API STG, Admin STG and Wallet were manually validated after the STG SQL migration.
- Quick Help registration QR/poster now uses `Admin:PublicBaseUrl` when configured; PROD should use `https://admin.loyaltycloud.net`.
- Tenant Admin `/config` now owns Apple Wallet card branding only: optional wallet background color, optional wallet-specific logo, contrast preview and fallback to main tenant logo/color.
- Google Wallet tenant branding is implemented. Apple Wallet logo scale changes intentionally do not alter the Google Wallet logo asset.
- `Admin__PublicBaseUrl=https://admin.loyaltycloud.net` was also configured intentionally on the legacy PROD Admin Windows app so newly printed Quick Help QR posters point to the new Admin domain during transition.
- New PROD Admin Linux `Admin__ApiBaseUrl` uses `https://api.loyaltycloud.net`.
- Do not change `Apple__WebServiceURL` yet; Apple Wallet hostname migration needs a separate impact review.
- Pending decision: `GoogleWallet__ProgramName` is currently `KBeauty Loyalty`; changing it to `KBeauty` is under consideration, then making it configurable by tenant later.
- Current PROD release: `v1.0.0`.
- `v1.0.0` SHA: `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.
- Release policy: immutable SemVer tags documented in `docs/RELEASE_PROCESS.md`.
- Branch policy: `main` is PROD integration, `staging` is Azure STG integration/release-candidate validation, and `feature/*`/`bugfix/*`/`hotfix/*` are isolated development branches.
- New work must begin from updated `main` on a dedicated branch; do not use `main` or `staging` for everyday feature development.
- Feature branches should merge into `staging` by PR for integrated STG validation.
- Azure STG should be deployed from `staging` when validating the next release candidate.
- After STG approval, open a PR from `staging` into `main`.
- PROD must be deployed from integrated `main`, never directly from `staging` or a feature branch.
- Release tags are created only after PROD deploy and smoke test succeed.
- Rollback of code uses a known release tag; rollback of database is separate and must be reviewed explicitly.
- Deployment slots are not available on the current B1 plan and the plan should not be upgraded only to obtain slots without explicit approval.

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
| Admin Linux App Service for temporary branch deploys | `loyaltycloud-admin-linux-stg-01` |
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

PROD Linux Admin/domain transition state:

| Resource | Name / value |
| --- | --- |
| Resource Group | `rg-loyaltycloud-prod` |
| API App Service | `loyaltycloud-api-894839` |
| API OS/runtime | Linux, .NET 9 |
| API custom domain | `https://api.loyaltycloud.net` |
| New Admin App Service | `loyaltycloud-admin-prod-01` |
| New Admin OS/runtime | Linux, .NET 9 |
| New Admin custom domain | `https://admin.loyaltycloud.net` |
| Shared Linux App Service Plan | `asp-loyaltycloud-api-free` |
| Plan actual SKU/tier | `B1` / `Basic`, capacity `1`, West US 3 |
| Legacy Admin Windows App Service | `loyaltycloud-admin` |
| Legacy Admin Windows URL | `https://loyaltycloud-admin.azurewebsites.net` |
| SQL Server | `sql-loyaltycloud-894839` |
| Active DB | `LoyaltyCloudFree` |
| Key Vault | `kv-loyaltycloud-894839` |

PROD domain/DNS state:

- Domain purchased: `loyaltycloud.net`.
- DNS is managed in Cloudflare.
- `api.loyaltycloud.net` points to `loyaltycloud-api-894839.azurewebsites.net`.
- `admin.loyaltycloud.net` points to `loyaltycloud-admin-prod-01.azurewebsites.net`.
- Initial Cloudflare CNAMEs were left as DNS-only.
- TXT records `asuid.api` and `asuid.admin` were added for Azure custom-domain verification.
- Both custom domains appear Verified/Secured in Azure.
- Both use Azure App Service managed certificates.
- `GET /` on API returning 404 is expected because the API has no root GET endpoint; it confirmed HTTPS/TLS.

New PROD Admin Linux Key Vault/identity state:

- The new Admin Linux App Service has System Assigned Managed Identity enabled.
- Current Principal ID: `28e04e72-b2e1-4a77-9ab3-30430b81d8b0`.
- It was granted `Key Vault Secrets User` on `kv-loyaltycloud-894839`.
- Its `DefaultConnection` uses Key Vault reference `@Microsoft.KeyVault(VaultName=kv-loyaltycloud-894839;SecretName=loyaltycloud-sql-connection-string)`.
- This connection string reference is validated and working.
- Reliable method for setting the connection string was JSON plus `az rest` against the App Service `/config/connectionstrings` resource because PowerShell/Azure CLI repeatedly truncated `@Microsoft.KeyVault(...)` references, especially the final `)`.

New PROD Admin Linux validation:

- `https://admin.loyaltycloud.net` HTTPS/TLS is configured and validated.
- Login and navigation were manually tested.
- `/platform/tenants` was tested and loads real PROD data.
- `Admin__ApiBaseUrl` on new Admin Linux was changed from `https://loyaltycloud-api-894839.azurewebsites.net` to `https://api.loyaltycloud.net` and validated.
- Legacy Admin Windows `https://loyaltycloud-admin.azurewebsites.net` still exists and works as fallback.

PROD Google Wallet configuration state:

- Google Wallet is approved for production.
- PROD has `GoogleWallet__*` settings configured.
- `GoogleWallet__ServiceAccountJson` is configured through a Key Vault reference.
- Key Vault PROD contains the secret `loyaltycloud-google-wallet-service-account-json`.
- Never document or print the service account JSON, private key, passwords, tokens, shared secrets or connection strings.

Current SQL/App Service cost posture:

- PROD SQL `LoyaltyCloudFree` now uses Basic DTU with 2 GB max size.
- STG SQL `LoyaltyCloudStg` now uses Basic DTU with 2 GB max size.
- Neither PROD nor STG depends on Serverless auto-pause now, so the cold start caused by waking SQL Serverless is removed for both.
- PROD API and new Admin Linux share `asp-loyaltycloud-api-free`, currently SKU `B1`, tier `Basic`, capacity `1`.
- Legacy PROD Admin Windows remains online on its prior Windows plan during transition.
- STG plans can remain F1 for now.

Current PROD Billing/Payments state:

- Billing/Payments is live in PROD.
- Migration `AddBillingPayments` is applied in PROD.
- Stripe LIVE is configured.
- PROD webhook is configured at `https://api.loyaltycloud.net/api/billing/webhooks/stripe`.
- Tenant Billing UI with visual periods/savings is active and validated in PROD.
- Founder plan prices are currently: 1 month `$249 MXN`, 3 months `$699 MXN`, 6 months `$1,299 MXN`, 12 months `$2,490 MXN`.
- Billing UI savings currently show: 3 months `Ahorras $48`, 6 months `Ahorras $195`, 12 months `2 meses GRATIS` plus `Ahorras $498`.

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

Canonical PROD Admin Linux publish/deploy flow:

```powershell
cd C:\repos\Loyalty\loyalty

Remove-Item .\artifacts\admin-prod -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\admin-prod.zip -Force -ErrorAction SilentlyContinue

dotnet publish `
  .\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj `
  -c Release `
  -o .\artifacts\admin-prod

tar -a -c -f .\artifacts\admin-prod.zip `
  -C .\artifacts\admin-prod .

az webapp deploy `
  --resource-group rg-loyaltycloud-prod `
  --name loyaltycloud-admin-prod-01 `
  --src-path .\artifacts\admin-prod.zip `
  --type zip
```

The last Admin Linux deploy finished with `RuntimeSuccessful`: 1 successful instance, 0 failed instances.

Common validation commands used during RC1 work:

```powershell
dotnet ef migrations has-pending-model-changes --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
dotnet build .\LoyaltyCloud.sln
```

No commands were run for this documentation task except repository inspection.

Release commands used on 2026-08-24:

```powershell
git tag -a v1.0.0 cfe607c6f2b8f92922c4c07a1ce94fd089401091 -m "LoyaltyCloud PROD v1.0.0 - Billing/Payments, Stripe production and billing pricing UI"
git push origin v1.0.0
```

The first push attempt from the sandbox failed because SSH/network access to GitHub was denied. The retry with elevated network permission succeeded and pushed only the tag.

Staging branch initialization on 2026-08-25:

- Local branch `staging` was created from `main` at `59c0340`.
- `origin/staging` was pushed and configured as the upstream for local `staging`.
- No feature branches were merged into `staging` during initialization.
- Existing work in `feature/wallet-card-branding` remained uncommitted and was not merged.

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
- This happened again while configuring the new PROD Admin Linux `DefaultConnection`.

Root cause:

- PowerShell/Azure CLI quoting around App Service Key Vault references is fragile.

Reliable method:

- Use JSON files for `az webapp config connection-string set`.
- Do not hand-type long Key Vault reference values directly when a JSON file is safer.
- For the new PROD Admin Linux connection strings, the robust method that worked was creating JSON and using `az rest` against the App Service `/config/connectionstrings` resource.

### Admin Linux deployment ZIP created with Windows paths

Problem:

- The legacy Admin Windows packaging used `Compress-Archive`.
- Reusing `Compress-Archive` for the new Linux Admin deployment produced paths containing Windows `\` separators inside the ZIP.
- Kudu failed during Linux deployment/`rsync`.

Root cause:

- Linux App Service ZIP deployment expects portable archive paths. `Compress-Archive` output from Windows was not safe for the Linux Kudu/rsync path in this case.

Solution:

- For Linux App Services, use `tar -a -c -f` from the publish output directory.
- The validated Admin Linux package command is:

```powershell
tar -a -c -f .\artifacts\admin-prod.zip `
  -C .\artifacts\admin-prod .
```

Validation:

- Deployment to `loyaltycloud-admin-prod-01` completed with `RuntimeSuccessful`.
- 1 instance succeeded, 0 failed.

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

- At the time of the STG migration, PROD was not modified.
- PROD was migrated later only after STG validated the same procedure successfully.

### 2026-08-12 - Azure SQL PROD migrated from Serverless to Basic DTU

Incident/objective:

- PROD SQL `LoyaltyCloudFree` was still running on General Purpose Serverless and could cold start when waking from auto-pause.
- STG had already been migrated to Basic DTU and validated successfully.
- The goal was to apply the same controlled procedure to PROD to remove the Serverless auto-pause cold start.

Infrastructure:

- Resource Group: `rg-loyaltycloud-prod`.
- SQL Server: `sql-loyaltycloud-894839`.
- Database: `LoyaltyCloudFree`.
- Region: `westus3`.

Initial PROD state:

- Database: `LoyaltyCloudFree`.
- Initial observed status: `Paused`.
- SKU: `GP_S_Gen5`.
- Tier: `GeneralPurpose`.
- Model: Serverless.
- `maxSizeBytes=34359738368` (32 GB).
- `useFreeLimit=true`.

Discovery:

1. Listing databases without specifying server failed.

```powershell
az sql db list --resource-group rg-loyaltycloud-prod -o table
```

Azure responded:

```text
(--server | --ids) are required
```

2. The PROD SQL Server was identified correctly:

```powershell
az sql server list `
  --resource-group rg-loyaltycloud-prod `
  --query "[].{name:name,location:location,state:state}" `
  -o table
```

Relevant result:

```text
sql-loyaltycloud-894839  westus3  Ready
```

3. Databases were listed correctly after specifying the server:

```powershell
az sql db list `
  --resource-group rg-loyaltycloud-prod `
  --server sql-loyaltycloud-894839 `
  --query "[].{name:name,status:status,sku:sku.name,tier:sku.tier,maxSizeBytes:maxSizeBytes}" `
  -o table
```

Result:

```text
master            Online    GP_SYSTEM  System          107374182400
LoyaltyCloudFree  Paused    GP_S_Gen5  GeneralPurpose  34359738368
```

Resource ID:

```powershell
$dbIdProd = az sql db show `
  --resource-group rg-loyaltycloud-prod `
  --server sql-loyaltycloud-894839 `
  --name LoyaltyCloudFree `
  --query id `
  -o tsv
```

Returned:

```text
/subscriptions/90f061a5-f51e-4ed9-95d7-6f9ed3ca3995/resourceGroups/rg-loyaltycloud-prod/providers/Microsoft.Sql/servers/sql-loyaltycloud-894839/databases/LoyaltyCloudFree
```

Storage measurement attempt:

```powershell
az monitor metrics list `
  --resource $dbIdProd `
  --metric storage `
  --interval PT1H `
  --aggregation Average `
  -o json
```

The Azure Monitor metric call worked technically, but the returned data point did not contain an `average` value because the database was paused.

Returned metric shape:

```json
{
  "name": {
    "localizedValue": "Data space used",
    "value": "storage"
  },
  "timeseries": [
    {
      "data": [
        {
          "timeStamp": "2026-08-12T20:10:00Z"
        }
      ]
    }
  ],
  "unit": "Bytes"
}
```

Decision:

- Because the database was paused, Azure Monitor did not provide a usable storage `average` for that window.
- The user decided to proceed directly with the resize because PROD was small and STG had already validated the same procedure satisfactorily.

Reduce max size to 2 GB:

- PROD max size was reduced from 32 GB to 2 GB.
- Verification:

```powershell
az sql db show `
  --resource-group rg-loyaltycloud-prod `
  --server sql-loyaltycloud-894839 `
  --name LoyaltyCloudFree `
  --query "{status:status,sku:sku.name,tier:sku.tier,maxSizeBytes:maxSizeBytes,useFreeLimit:useFreeLimit}" `
  -o json
```

Result:

```json
{
  "maxSizeBytes": 2147483648,
  "sku": "GP_S_Gen5",
  "status": "Online",
  "tier": "GeneralPurpose",
  "useFreeLimit": true
}
```

This confirmed:

- max size was 2 GB;
- database was `Online`;
- database was still Serverless;
- `useFreeLimit=true` was still active.

Disable Free Limit:

- Free Limit was removed before changing tier.
- Verification:

```json
{
  "maxSizeBytes": 2147483648,
  "sku": "GP_S_Gen5",
  "status": "Online",
  "tier": "GeneralPurpose",
  "useFreeLimit": false
}
```

Final migration to Basic:

```powershell
az sql db update `
  --resource-group rg-loyaltycloud-prod `
  --server sql-loyaltycloud-894839 `
  --name LoyaltyCloudFree `
  --service-objective Basic
```

Final verification:

```powershell
az sql db show `
  --resource-group rg-loyaltycloud-prod `
  --server sql-loyaltycloud-894839 `
  --name LoyaltyCloudFree `
  --query "{status:status,sku:sku.name,tier:sku.tier,currentServiceObjectiveName:currentServiceObjectiveName,maxSizeBytes:maxSizeBytes,useFreeLimit:useFreeLimit}" `
  -o json
```

Result:

```json
{
  "currentServiceObjectiveName": "Basic",
  "maxSizeBytes": 2147483648,
  "sku": "Basic",
  "status": "Online",
  "tier": "Basic",
  "useFreeLimit": null
}
```

Final validated PROD state:

- Resource Group: `rg-loyaltycloud-prod`.
- SQL Server: `sql-loyaltycloud-894839`.
- Database: `LoyaltyCloudFree`.
- Tier: Basic.
- SKU: Basic.
- Service objective: Basic.
- Status: Online.
- Max size: 2 GB.
- `useFreeLimit=null`.
- No longer General Purpose Serverless.
- No longer depends on auto-pause.
- Serverless cold start is removed for PROD.
- API PROD, Admin PROD and Wallet PROD were manually validated after the migration.

STG:

- STG remains Basic DTU with 2 GB max size.
- STG API/Admin/Wallet were already validated after its migration.
- Current final state: STG and PROD are both Basic DTU.

### 2026-08-15/16 - Quick Help registration QR moved to new Admin public domain

Objective:

- Begin the controlled transition from legacy Admin Windows to the new Admin Linux custom domain.
- Keep the legacy Admin Windows app online as fallback.
- Ensure newly displayed/printed public registration QR codes point users to the new Admin domain.

Code/config state:

- QR source page: `src/LoyaltyCloud.Admin/Pages/QuickHelp.razor`.
- Public registration route remains `/{tenantSlug}/join`.
- Example PROD KBeauty URL: `https://admin.loyaltycloud.net/kbeauty/join`.
- Previous Quick Help behavior built the URL from `Navigation.BaseUri`.
- Current Quick Help behavior uses `Admin:PublicBaseUrl` when configured and falls back to `Navigation.BaseUri` when empty.
- Azure App Setting name: `Admin__PublicBaseUrl`.
- PROD value: `https://admin.loyaltycloud.net`.
- STG should use its own STG Admin host while it has no custom domain; do not point STG QR to PROD.
- The visible QR and printable poster use the same `registrationUrl`/`registrationQrDataUri`.
- Manual validation confirmed the new QR works.

Important operational detail:

- `Admin__PublicBaseUrl=https://admin.loyaltycloud.net` was also configured intentionally on the legacy PROD Admin Windows app.
- This means employees who temporarily keep using the old Admin will print new QR posters pointing to the new Admin domain.

Explicitly not changed:

- `Apple__WebServiceURL`.
- Apple PassKit `/v1/*` routes.
- Apple Wallet pass internal QR/barcode.
- APNs.
- Google Wallet pass QR/object content.

Do not confuse:

- Quick Help public registration QR: `https://admin.loyaltycloud.net/{tenantSlug}/join`.
- Apple/Google Wallet pass QR/barcode: separate Wallet content, not part of this change.

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
- Azure SQL PROD `LoyaltyCloudFree` was migrated successfully to Basic DTU.
- API PROD, Admin PROD and Wallet PROD were manually validated after the PROD SQL migration.
- API custom domain `https://api.loyaltycloud.net` is configured with HTTPS/TLS and works; `GET /` returning 404 is expected for this API.
- New Admin Linux custom domain `https://admin.loyaltycloud.net` is configured with HTTPS/TLS and works.
- New Admin Linux login/navigation and `/platform/tenants` were manually validated against real PROD data.
- New Admin Linux can consume the API through `Admin__ApiBaseUrl=https://api.loyaltycloud.net`.
- Quick Help registration QR/poster uses the same generated URL/data source and works with `Admin__PublicBaseUrl=https://admin.loyaltycloud.net`.
- Billing/Payments is deployed and validated in PROD.
- Stripe LIVE is configured.
- Migration `AddBillingPayments` is applied in PROD.
- Tenant Billing UI with visual period selector/savings is validated in PROD.
- PROD stable release `v1.0.0` points to `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.

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
   - New Admin public host for links/QR is `https://admin.loyaltycloud.net`.
   - Legacy Admin Windows host `https://loyaltycloud-admin.azurewebsites.net` remains online during transition.
   - API production custom domain is `https://api.loyaltycloud.net`.
   - Legacy API host `https://loyaltycloud-api-894839.azurewebsites.net` remains available.
   - PROD Admin should set `Admin__PublicBaseUrl=https://admin.loyaltycloud.net`.
   - STG Admin should set `Admin__PublicBaseUrl` to its own STG Admin public URL, not PROD.
   - New PROD Admin Linux should keep `Admin__ApiBaseUrl=https://api.loyaltycloud.net`.
   - Do not remove `loyaltycloud-admin` yet.
   - STG does not point to PROD SQL/Storage/Key Vault.
   - PROD and STG `GoogleWallet__ProgramName` are intentionally still `KBeauty Loyalty` until the naming decision is made.

4. SQL hosting follow-up:
   - Observe both STG and PROD on Basic DTU for behavior, costs and limits.
   - Serverless cold start is no longer active for STG or PROD.

5. Apple/Google Wallet hostname review:
   - Analyze strategy before changing `Apple__WebServiceURL` to `https://api.loyaltycloud.net`.
   - Determine impact on already installed Apple Wallet passes, `/v1/devices/*`, `/v1/passes/*`, device registrations and push/update compatibility.
   - Review whether Google Wallet has URLs/base URLs that should migrate to custom domains.
   - Keep `azurewebsites.net` hostnames compatible during transition.

## Next Recommended Step

For the next technical session:

1. Read `docs/AI_CONTEXT.md`.
2. Read this handoff.
3. Read `docs/RELEASE_PROCESS.md` before any PROD deploy/rollback work.
4. Use `docs/ROADMAP.md` as the live source for current pending items.
5. Before implementing a new feature, verify the current branch. If currently on `main` or `staging`, create a dedicated feature branch before modifying functional code.
6. If already on a related feature branch, continue there instead of creating another branch.
7. If working on STG, verify current App Settings and connection strings from Azure before changing code.
8. For Google Wallet STG, keep `reviewStatus = UNDER_REVIEW` in LoyaltyClass PATCH payloads and retry save-link with a known customer serial if a regression appears.
9. For Admin domain transition, keep legacy Admin Windows online until the new Linux Admin has been fully validated by users.
10. Before any Apple hostname work, inspect `Apple__WebServiceURL` impact on existing installed passes and design a safe migration plan.
11. Observe both Basic DTU databases after the migration and only revisit SQL tier if cost or limits require it.

Recommended first command for local orientation:

```powershell
git status --short
```

Do not run build/tests/deploy/migrations unless the user explicitly asks.
