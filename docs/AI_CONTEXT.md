# LoyaltyCloud - AI Development Context

Last updated: 2026-07-27

Purpose:
This document is the canonical technical handoff for continuing development of LoyaltyCloud with ChatGPT/Codex.

IMPORTANT:
Read this file before proposing commands, migrations, deployments or architecture changes.

Do not create or maintain `docs/DECISIONS.md`. This project intentionally does not use it.

## Product Status

Product: LoyaltyCloud.

Current state: RC1 / real UAT starting.

LoyaltyCloud is a multi-tenant loyalty SaaS for customer points, rewards, redemptions, campaigns, Apple Wallet passes, APNs wallet refreshes, platform tenant management, and tenant admin operations.

Architecture:

- .NET 9.
- Clean Architecture.
- EF Core 9.
- SQL Server / Azure SQL.
- Blazor Server / Interactive Server Admin.
- Azure App Service.
- Azure Blob Storage.
- Azure Key Vault.
- Apple Wallet / PassKit.
- APNs.

Projects:

- `LoyaltyCloud.Common`: shared constants, `Result<T>`, pagination and cross-cutting primitives.
- `LoyaltyCloud.Domain`: entities, domain events, enums, invariants and repository contracts.
- `LoyaltyCloud.Application`: CQRS/MediatR commands, queries, handlers, validators and service interfaces.
- `LoyaltyCloud.Infrastructure`: EF Core, repositories, read services, Key Vault, Blob Storage, Apple Wallet pass generation, APNs, notification processors, tenant services.
- `LoyaltyCloud.API`: REST API, Apple Wallet web service endpoints, public join API, admin API HMAC middleware, background jobs.
- `LoyaltyCloud.Admin`: Blazor Server tenant admin and platform admin.
- `LoyaltyCloud.Tools`: internal operational CLI tools.
- `LoyaltyCloud.Tests`: xUnit tests and guardrails.

## Multi-Tenancy Guardrails

LoyaltyCloud is multi-tenant. KBeauty is no longer special product behavior.

Rules:

- There is no production KBeauty seed.
- A new database can validly have 0 tenants.
- Tenants are created from Platform Admin.
- Platform Admin operates without `TenantContext`.
- Tenant Admin operates with `TenantContext`.
- `/platform/*` must never restore or carry business tenant context.
- `/{tenantSlug}/*` is tenant-aware.
- Do not hardcode `kbeauty`.
- Do not restore `Tenancy:DefaultTenantSlug`.
- Do not accept free-form `TenantId` from tenant-facing UI.

Known corrected bug:

Blazor Interactive Server circuits could restore `loyaltycloud.admin.auth` during `/platform/*` and contaminate `TenantContext`. The fix is in `AdminTenantContextInitializer`: platform routes clear/exclude tenant context. This is an architectural guardrail and must not be reverted.

Tenant-owned data uses `TenantId`, EF query filters, tenant-aware FKs and guards in `AppDbContext`. Platform services that need cross-tenant work use explicit platform paths and must not rely on a tenant fallback.

## Production URLs

Official Admin:

```text
https://loyaltycloud-admin.azurewebsites.net
```

Official API:

```text
https://loyaltycloud-api-894839.azurewebsites.net
```

Do not use the removed Admin hostname. There is a test guardrail to prevent
that retired Admin hostname from returning in product code/config. Do not
accidentally rename the API hostname; `loyaltycloud-api-894839` is still
correct.

## Azure Production Resources

Resource Group:

```text
rg-loyaltycloud-prod
```

Azure SQL Server:

```text
sql-loyaltycloud-894839
```

Current active database:

```text
LoyaltyCloudFree
```

The previous `LoyaltyCloud` database is not the active RC1/UAT database.

Key Vault:

```text
kv-loyaltycloud-894839
```

Storage:

```text
stloyaltycloud894839
```

## Azure Staging Infrastructure Baseline

The active RC1/UAT staging infrastructure path is PowerShell + Azure CLI under
`infra/`.

- `infra/create-stg.ps1`: dry-run-by-default script that creates STAGING Azure
  infrastructure only when `-Execute` is passed.
- `infra/configure-stg-secrets.ps1`: loads manual STAGING secrets into STAGING
  Key Vault.
- `infra/README.md`: operational instructions.

The Bicep files under `infra/` are experimental and not the active deployment
path. Do not deploy them unless a separate infrastructure review approves Bicep.

Staging target:

```text
Resource Group: rg-loyaltycloud-stg
API: loyaltycloud-api-stg-<uniqueSuffix>
Admin: loyaltycloud-admin-stg-<uniqueSuffix>
SQL Server: sql-loyaltycloud-stg-<uniqueSuffix>
Database: LoyaltyCloudStg
Key Vault: kv-loyaltycloud-stg-<uniqueSuffix>
Storage: stloyaltycloudstg<uniqueSuffix>
```

Staging must be isolated from production. Do not point staging at production SQL,
production Storage, or production Key Vault.

Current app configuration contracts reflected by the staging scripts:

- API and Admin both require `ConnectionStrings:DefaultConnection`.
- API and Admin both load Azure Key Vault through `Azure:KeyVaultUri`.
- API and Admin both register Infrastructure; outside Development the current
  code registers real Apple Wallet signing and `ApnService`.
- Admin also requires `Admin:ApiBaseUrl`.
- Admin/API HMAC requires `AdminApi:SharedSecret`.
- Tenant Admin sessions use `Admin:Auth:SessionHours`; RC1 target is 168 hours.
- Super Admin sessions use `SuperAdmin:SessionHours`; RC1 target remains 8 hours.
- SQL-light background defaults are `LoyaltyMaintenance:IntervalHours=12` and
  `LoyaltyNotifications:PollIntervalSeconds=43200`.
- Azure Blob Storage uses `Azure:BlobStorage:ConnectionString` and
  `Azure:BlobStorage:PassContainer=passes`.
- Tenant logos and Wallet assets are stored under tenant-scoped blob paths inside
  the `passes` container.

Key Vault secret names currently expected:

```text
loyaltycloud-sql-connection-string
loyaltycloud-storage-connection-string
loyaltycloud-admin-api-shared-secret
loyaltycloud-superadmin-username
loyaltycloud-superadmin-password-hash
kbeauty-pass-certificate
kbeauty-pass-certificate-password
kbeauty-wwdr-certificate
kbeauty-apn-private-key
kbeauty-apn-key-id
kbeauty-apn-team-id
loyaltycloud-google-wallet-service-account-json
```

The `kbeauty-*` Apple secret names are legacy provider names and must not be
renamed in configuration until `KeyVaultAppleWalletSecretsProvider` is changed.

Wallet environment guidance:

- `Apple:WebServiceURL` must be environment-specific because it is embedded in
  passes and used by Apple Wallet web service refresh.
- Apple Pass Type ID and Apple Team ID can be shared temporarily for RC1/UAT.
- Apple `.p12`, `.p12` password, APNs `.p8`, Google service account JSON and
  any signing/private material should be separated per environment long-term.
- Existing production Apple Wallet passes will keep calling production because
  their embedded `webServiceURL` points to production.

App Services:

- API: `loyaltycloud-api-894839`, Linux.
- Admin: `loyaltycloud-admin`, Windows.

Do not document or print secrets.

## Connection String / Key Vault

API and Admin use `ConnectionStrings:DefaultConnection`.

Production uses Key Vault references. The SQL connection string secret is expected to be:

```text
loyaltycloud-sql-connection-string
```

For RC1/UAT it must point to:

```text
Initial Catalog=LoyaltyCloudFree
```

Resolved incident:

After updating the Key Vault secret, App Service kept using a cached version. Symptom: SQL login failures even though the credentials worked from SSMS.

Procedure that worked:

1. Update the secret in Key Vault.
2. Rewrite the same Key Vault reference in both App Services with `az webapp config connection-string set`.
3. Restart both App Services.

Do not assume the SQL password is wrong when SSMS works. First verify App Service is not using a cached Key Vault reference.

## DEPLOYMENT - DO NOT FORGET

There are two different App Service types.

### API - Linux

App:

```text
loyaltycloud-api-894839
```

Publish:

```powershell
dotnet publish .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj -c Release -o .\artifacts\api
```

IMPORTANT:

Do not use `Compress-Archive` to create the API Linux ZIP. It caused deployment/runtime problems.

Create the ZIP with `tar` from Windows:

```powershell
tar -a -c -f .\artifacts\api.zip -C .\artifacts\api .
```

Deploy from Azure Cloud Shell:

```bash
az webapp deploy \
  -g rg-loyaltycloud-prod \
  -n loyaltycloud-api-894839 \
  --src-path ./api.zip \
  --type zip
```

### Admin - Windows

App:

```text
loyaltycloud-admin
```

Publish:

```powershell
dotnet publish .\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj -c Release -o .\artifacts\admin
```

Create ZIP:

```powershell
Compress-Archive -Path .\artifacts\admin\* -DestinationPath .\artifacts\admin.zip -Force
```

Deploy from Azure Cloud Shell:

```bash
az webapp deploy \
  -g rg-loyaltycloud-prod \
  -n loyaltycloud-admin \
  --src-path ./admin.zip \
  --type zip
```

After deploy:

```bash
az webapp restart -g rg-loyaltycloud-prod -n loyaltycloud-api-894839
az webapp restart -g rg-loyaltycloud-prod -n loyaltycloud-admin
```

Do not recommend `api.tar.gz` or `--type static`. That procedure failed.

## Migrations / Database

Migrations are created/applied from Windows local.

Current production/UAT DB target:

```text
LoyaltyCloudFree
```

Before any `database update`, always verify the effective connection string contains:

```text
Initial Catalog=LoyaltyCloudFree
```

Do not run migrations automatically during deploy.

Recent RC1 migrations:

- `20260724135525_AddTenantLoyaltyLevels`
- `20260726044226_RemoveKBeautySeed`
- `20260726055027_AddTenantBrandingLogoBlobName`

The `RemoveKBeautySeed` migration removes the old KBeauty production seed and associated KBeauty tenant data for the fixed seed tenant id. Development demo seeding is separate and only runs in Development if the KBeauty tenant already exists.

The `AddTenantBrandingLogoBlobName` migration adds nullable `TenantBrandings.LogoBlobName` with max length 500.

## Platform Admin

Routes:

- `/platform/login`
- `/platform/tenants`
- `/platform/tenants/{tenantId}`

Capabilities:

- Create tenant.
- View tenant detail.
- Suspend tenant.
- Reactivate tenant.
- Cancel tenant.
- Extend trial.
- Update grace period.
- Record manual subscription payment.
- Hard delete tenant.
- Upload/change/remove tenant logo.
- View branding/subscription information.
- Create tenant admin during tenant provisioning.

Delete tenant:

- Hard delete.
- Requires typing exact slug.
- Transactional.
- Platform Admin only.
- Deletes tenant-scoped operational data and then tenant records.

Platform routes must remain free of `TenantContext`.

## Tenant Admin

Login route:

```text
/{tenantSlug}/login
```

Root `/` redirects to `/platform/login`.

Tenant Admin session:

- Cookie: `loyaltycloud.admin.auth`.
- `Admin:Auth:SessionHours = 168` by default.
- 168 hours = 7 days.
- `SlidingExpiration = true`.
- Persistent sign-in is used by the login service.

Super Admin session:

- Cookie: `loyaltycloud.platform.auth`.
- `SuperAdmin:SessionHours = 8`.
- Keep separate from Tenant Admin.

Data Protection incident:

A manual Data Protection override to `HOME/data-protection-keys` was reverted. Do not re-add:

```csharp
PersistKeysToFileSystem(Path.Combine(homePath, "data-protection-keys"))
```

unless there is a new, validated reason.

Azure App Service has a standard key ring path:

```text
D:\home\ASP.NET\DataProtection-Keys
```

The manual override created a separate ring:

```text
D:\home\data-protection-keys
```

and caused auth/cookie confusion. Do not repeat.

## Tenant Admin Menu

Current menu from `MainLayout.razor`:

- Dashboard
- Puntos
  - Sumar puntos
  - Canjear puntos
- Clientes
  - Clientes
  - Canjes
- Programa de lealtad
  - Recompensas
  - Campanas
- Comunicacion
  - Mensajes
- Administracion
  - Niveles
  - Configuracion
- Ayuda rapida

`/notifications` exists historically/API-wise but is not in the visible tenant admin menu.

## Quick Help

Tenant Admin route:

```text
/quick-help
```

Includes:

- Public registration instructions.
- Sumar puntos.
- Canjear puntos.
- Search/scan guidance.
- Common problems.
- Public registration QR.
- Copy link.
- Open link.
- Print QR poster.

Public join:

```text
/{tenantSlug}/join
```

Examples:

```text
/kbeauty/join
/bitcafe/join
```

API:

```text
POST /api/public/{tenantSlug}/join
PUT /api/public/{tenantSlug}/join/{serialNumber}/birthday
```

The Quick Help QR uses:

```csharp
new Uri(new Uri(Navigation.BaseUri), $"{tenantSlug}/join")
```

QR implementation:

- QRCoder.
- Local SVG generation.
- No external API.
- Error correction level Q.
- Standard quiet zone.
- Round-trip tests with ZXing.

Do not reintroduce a hand-rolled QR encoder. A previous custom generator looked valid visually but was not reliably scannable.

## API Endpoints Overview

Main API controllers and route groups:

- `CustomersController`: `POST /api/customers`, `GET /api/customers/{serialNumber}`, `GET /api/customers/{serialNumber}/transactions`.
- `PointsController`: `POST /api/points`.
- `RedemptionsController`: `POST /api/redemptions`, `PUT /api/redemptions/{id}/confirm`, `PUT /api/redemptions/{id}/cancel`, `GET /api/redemptions/catalog/{serialNumber}`. `POST /api/redemptions` supports both catalog rewards and monetary discount redemptions.
- `RewardsController`: `GET /api/rewards`, `GET /api/rewards/{id}`, `POST /api/rewards`, `PUT /api/rewards/{id}`, `PUT /api/rewards/{id}/activate`, `PUT /api/rewards/{id}/deactivate`.
- `CampaignsController`: `GET /api/campaigns`, `GET /api/campaigns/{id}`, `POST /api/campaigns`, `PUT /api/campaigns/{id}`, `PUT /api/campaigns/{id}/activate`, `PUT /api/campaigns/{id}/deactivate`.
- `LevelsController`: `GET /api/levels`, `PUT /api/levels`.
- `CustomNotificationCampaignsController`: preview/list/get/create/send/cancel custom campaigns.
- `NotificationsController`: list/metrics/get/create/process/retry/cancel notifications.
- `ConfigController`: `GET /api/config`, `PUT /api/config`.
- `AdminController`: dashboard, expiration, level recalculation, notification candidate diagnostics.
- `PublicJoinController`: public tenant join.
- `PassesController`: Apple Wallet web service and pass download routes.

Do not invent endpoints. Inspect controllers/routing before proposing API calls.

## Apple Wallet

Public pass download:

```text
GET /api/passes/{serialNumber}
```

Content-Type:

```text
application/vnd.apple.pkpass
```

This route does not include tenant slug. Tenant is resolved from `LoyaltyCard.SerialNumber`.

Apple web service routes:

- `GET /v1/passes/{passTypeIdentifier}/{serialNumber}`
- `POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}`
- `DELETE /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}`
- `GET /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}`
- `POST /v1/log`

Serials currently still use the `KB-` prefix even for other tenants. This is known technical debt. Do not confuse it with tenant resolution. Do not change serial format without an Apple Wallet compatibility review.

The Apple Pass Type ID may still be:

```text
pass.com.kbeautymx.loyalty
```

and secrets may still have `kbeauty-*` names. These are safe legacy names for Apple/certificate compatibility in RC1.

## Wallet Branding

Tenant branding now supports tenant-specific logos.

SQL stores only:

```text
TenantBrandings.LogoBlobName
```

Logo bytes are stored in Azure Blob Storage, not SQL.

Storage path:

```text
tenant-branding/{tenantId}/logo-original...
tenant-branding/{tenantId}/wallet/logo.png
tenant-branding/{tenantId}/wallet/logo@2x.png
tenant-branding/{tenantId}/wallet/logo@3x.png
tenant-branding/{tenantId}/wallet/icon.png
tenant-branding/{tenantId}/wallet/icon@2x.png
tenant-branding/{tenantId}/wallet/icon@3x.png
```

Upload accepts PNG/JPG, validates size and image content, and generates Apple Wallet raster assets automatically.

`PassGeneratorService` requests assets by `TenantId`, not by slug, to avoid cross-tenant asset mixing.

Fallback assets:

- Bundled in `Assets/AppleWalletGeneric`.
- Neutral graphic.
- No `LC` text.

Pass visual rules:

- `logoText`: not used by production `PassGeneratorService`.
- `backgroundColor`: white/light (`rgb(255,255,255)`).
- `foregroundColor`: black (`rgb(0,0,0)`).
- `labelColor`: tenant `PrimaryColor`.
- Top of Wallet pass should show only the uploaded tenant logo, no extra text.

PassKit `storeCard` does not provide a simple independent header color property. Do not implement visual hacks for a header band without a proper PassKit review.

Changing a tenant logo affects the next generated pass immediately. RC1 does not currently trigger a tenant-wide APNs push solely for logo changes.

## KBeauty RC1 UAT Configuration Example

KBeauty is an example/UAT tenant, not product hardcode.

Expected KBeauty UAT values:

- Slug: `kbeauty`.
- Time zone: `America/Tijuana`.
- PrimaryColor: `#D98FA3`.
- SecondaryColor: `#F7E8EC`.

Expected levels:

- Inicial: 0.
- Preferente: 1000.
- Premium: 3000.
- Exclusivo: 5000.

Levels are tenant configurable and calculated with a rolling 12-month window.

Provisioning defaults in code are still legacy `Mist/Glow/Radiance`; customize tenant levels after provisioning when needed.

## Points / Levels / Expiration

Implemented:

- Points from purchase amount through `AddPointsCommand`.
- Birthday multiplier through ProgramConfig.
- Point campaigns with multiplier and level eligibility.
- FIFO point lots.
- `PointLotConsumption`.
- Expiration after configured number of months; default config is 12 months.
- Redemption consumes points immediately.
- Direct monetary redemption uses `ProgramConfig.points_per_peso_unit` as the server-side conversion rate. Formula: `monetaryAmount = pointsRedeemed / PointsPerPesoUnit`. Example: `10` means 10 points = $1.00 MXN, so 500 points = $50.00 MXN.
- Monetary redemptions persist a historical snapshot on `Redemption`: type, monetary amount, currency and points-per-peso rate. Do not recalculate old monetary redemptions from current ProgramConfig.
- Pending redemption can be confirmed or cancelled.
- Cancellation restores exact consumed lots through reversal.
- Dynamic tenant levels via `TenantLoyaltyLevel`.
- Rolling level calculation over the last 12 months.
- Customer Detail includes advanced audit data: balance, rolling progress, lots, FIFO consumption, point history and redemption history.

Do not use `CurrentPoints` for level progress. Level progress is rolling points, not available balance.

## Monetary Redemptions

Admin `/redeem` supports two redemption modes after identifying the customer:

- `Descuento en dinero` (default).
- `Recompensa`.

For `Descuento en dinero`, the cashier enters points to redeem. The UI shows an estimated monetary discount and a `Usar todos` action that rounds down to the nearest valid conversion unit. The browser calculation is UX only.

Server authority:

- Input authority: serial/customer and requested points.
- The API recalculates the monetary amount from tenant `ProgramConfig`.
- The browser never supplies a trusted monetary amount.
- Tenant context selects the tenant-specific rate; do not accept TenantId from UI.

Persistence:

- `Redemption.Type = MonetaryDiscount`.
- `RewardCatalogItemId = null`.
- `PointsSpent` stores the redeemed points.
- `MonetaryAmount`, `MonetaryCurrency` and `MonetaryPointsPerPesoUnit` store the historical snapshot.

FIFO/cancellation:

- Monetary redemptions consume `PointLot`s with the same FIFO mechanism as catalog reward redemptions.
- `PointLotConsumption.RedemptionId` links the consumed lots.
- Cancellation uses the existing cancellation flow and restores the exact consumed lots.

POS:

LoyaltyCloud calculates and records the discount. It does not apply the discount automatically in an external POS. The cashier must apply the shown amount manually in the store/POS before confirming the redemption.

## Notifications / Wallet Visible Events

Core entities:

- `LoyaltyNotification`.
- `NotificationDelivery`.

Channels:

- `AppleWallet` is implemented.

Current notification types include:

- `LevelChanged`.
- `PointsExpiring`.
- `MonthlyProductStarted`.
- `BirthdayBenefitStarted`.
- `PointCampaignStarted`.
- `PointsAdded`.
- `Custom`.

Wallet visible event selection:

- Uses recent delivered notifications with `DisplayUntilUtc`.
- Recency wins first.
- Priority is only a tie-breaker for same effective time.
- LevelChanged should beat PointsAdded for the same purchase/level-up scenario.
- `VisibleEventPriorityHours` default/configured value: 24.

Important Wallet changeMessage rule:

- Apple Wallet `changeMessage` must contain `%@`.
- For `PointsAdded`, do not put `changeMessage` on the permanent `points` field. The permanent points field value is total balance.
- PointsAdded uses a temporary field like `points_added` with value like `10 puntos` and `changeMessage = "🎉 Sumaste %@"`.

Point campaign visible text currently uses:

```text
🎉 {CampaignName} · Gana puntos x{Multiplier}
```

## Background Jobs

API hosted services:

### LoyaltyMaintenanceBackgroundService

Config section: `LoyaltyMaintenance`.

Current defaults from `appsettings.json`:

- `Enabled = true`.
- `RunOnStartup = false`.
- `IntervalHours = 12`.
- `RunAtLocalTime = "02:00"` is present in config, but the current service uses `IntervalHours`.
- `TimeZoneId = "America/Tijuana"`.

Runs:

- Subscription maintenance.
- Point expiration.
- Level recalculation.
- Point expiration notifications.
- Monthly product notifications.
- Birthday benefit notifications.
- Point campaign notifications.

It runs per operational tenant through `ITenantExecutionRunner`.

### LoyaltyNotificationBackgroundService

Config section: `LoyaltyNotifications`.

Current defaults:

- `Enabled = true`.
- `RunOnStartup = false`.
- `PollIntervalSeconds = 43200` (12 hours).
- Minimum poll interval in code: 15 seconds.
- `BatchSize = 25`.
- `MaxAttempts = 3`.
- `VisibleEventPriorityHours = 24`.

Runs:

- Due custom notification campaigns.
- Pending notification deliveries / retries.

RC1 cost-control decision: this worker intentionally runs every 12 hours to let Azure SQL Free/Serverless reach `AutoPauseDelay = 60` minutes and minimize vCore-seconds. Due custom campaigns, pending notification deliveries, Wallet delivery retries and background notification processing can take up to approximately 12 hours. Immediate user-triggered flows still create/process notifications through their foreground handlers when explicitly wired.

## Known RC1 Issues / Technical Debt

Safe legacy names:

- `KB-` serial prefix.
- Apple Pass Type ID may still include `kbeautymx`.
- Key Vault secret names may include `kbeauty-*`.
- JavaScript QR scanner may still be named `kbeautyQrScanner`.
- Some ProgramConfig legacy reward cost keys still exist for compatibility.

Must fix before GA or before broad multi-tenant scale:

- Decide neutral/configurable serial prefix strategy.
- Decide long-term Apple Pass Type ID/certificate strategy.
- Add/confirm tenant-wide Wallet refresh for branding-only changes if required.
- Review and remove remaining temporary diagnostic logs if any are too noisy for production.
- Provisioning defaults still create `Mist/Glow/Radiance`; update defaults or make template configurable before generic onboarding.
- Ensure production CORS/App Settings reference official Admin host only.

## Operational Lessons / Do Not Repeat

1. API Linux: create ZIP with `tar -a`; do not use `Compress-Archive`; do not deploy `api.tar.gz`; do not use `--type static`.
2. Admin Windows: use `Compress-Archive`.
3. The retired Admin hostname does not exist and must not be reintroduced.
4. Key Vault references can be cached by App Service; rewrite the reference and restart if a new secret version is not picked up.
5. Do not manually persist Data Protection to `HOME/data-protection-keys`.
6. `/platform/*` must be tenant-context-free.
7. Do not implement QR encoding by hand.
8. Do not recreate a production KBeauty seed.
9. Do not invent endpoints; inspect controllers.
10. Before `database update`, verify target DB is `LoyaltyCloudFree`.
11. RC1 cost control: maintenance worker and notification worker are 12h. Do not reduce notification polling to minutes without evaluating Azure SQL Free/Serverless autopause and vCore-second impact.

## Working Rules

- Make small, scoped changes.
- Avoid large refactors unless explicitly requested.
- Keep Clean Architecture boundaries.
- Do not change API/Wallet/APNs/routing accidentally.
- Do not create migrations unless model changes require them.
- Do not run `database update`, deploy, or commit unless explicitly requested.
- Run relevant tests for the changed area.
- Run `dotnet ef migrations has-pending-model-changes` after model work.
- Run `dotnet build .\LoyaltyCloud.sln` before closing implementation tasks.
- Update documentation at important checkpoints.
- Do not use `docs/DECISIONS.md`.

## Current Docs

Existing docs:

- `README.md`: human-facing setup and project overview.
- `docs/AppleWallet.md`: Wallet/pass technical documentation.
- `docs/AppleWallet-Development.md`: local Wallet development notes.
- `docs/ROADMAP.md`: current roadmap.
- `docs/AI_CONTEXT.md`: this canonical handoff.

Keep README concise. Put detailed operational/handoff context here.
