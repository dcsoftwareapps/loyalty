# LoyaltyCloud - AI Context

Last updated: 2026-08-25

Purpose: permanent technical context for continuing LoyaltyCloud with ChatGPT/Codex without losing important repository, infrastructure and product memory between chats.

Do not create `docs/DECISIONS.md`. This repository intentionally uses `docs/AI_CONTEXT.md`, `docs/AI_HANDOFF.md`, `docs/ROADMAP.md`, `docs/RELEASE_PROCESS.md` and focused feature docs instead.

## Product Objective

LoyaltyCloud is a multi-tenant loyalty SaaS originally evolved from KBeauty Loyalty.

It lets each business tenant run a customer loyalty program with:

- public customer registration;
- Apple Wallet pass generation and refresh;
- Google Wallet save-link support;
- points from purchases;
- birthday and campaign multipliers;
- FIFO point lots and expiration;
- catalog reward redemptions;
- direct monetary discount redemptions;
- dynamic tenant loyalty levels;
- customer audit/detail;
- custom and automatic Wallet notifications;
- platform admin tenant management;
- tenant admin cashier/admin workflows.

KBeauty is now a tenant/UAT business, not a product-level special case. Some Apple identifiers and secret names still include KBeauty for certificate compatibility and must not be casually renamed.

## Solution Architecture

Solution: `LoyaltyCloud.sln`.

Projects:

| Project | Purpose |
| --- | --- |
| `LoyaltyCloud.Common` | Shared constants, result types, pagination, utility extensions, Admin API HMAC signature helper. |
| `LoyaltyCloud.Domain` | Entities, enums, value objects, domain events, invariants and repository contracts. |
| `LoyaltyCloud.Application` | CQRS commands, queries, handlers, validators, application interfaces and read/write orchestration. |
| `LoyaltyCloud.Infrastructure` | EF Core, repositories, read services, tenant services, Blob Storage, Key Vault, Apple Wallet, APNs, Google Wallet and cross-cutting adapters. |
| `LoyaltyCloud.API` | REST API, Admin API HMAC middleware, public join API, Apple PassKit web service, Wallet endpoints and hosted workers. |
| `LoyaltyCloud.Admin` | Blazor Server / Interactive Server tenant admin and platform admin. |
| `LoyaltyCloud.Tools` | Internal operational CLI commands and wallet diagnostics. |
| `LoyaltyCloud.Tests` | xUnit integration, application, infrastructure and guardrail tests. |

Main technologies:

- .NET 9.
- C#.
- ASP.NET Core.
- Blazor Server / Interactive Server.
- MediatR.
- EF Core 9 with SQL Server provider and retrying execution strategy.
- Azure SQL.
- Azure App Service Linux for API and new PROD Admin.
- Azure App Service Windows for legacy Admin during the PROD transition.
- Azure Key Vault.
- Azure Blob Storage.
- Apple Wallet / PassKit.
- APNs HTTP/2.
- Google Wallet Objects API through REST and RS256 JWTs.
- PowerShell/Azure CLI infra scripts.

## Domain Model

Main entities:

| Entity | Purpose |
| --- | --- |
| `Tenant` | Platform tenant/business. Slug identifies tenant routes. |
| `TenantBranding` | Branding, colors, support links and logo blob reference. |
| `TenantSubscription` | Trial/active/past-due/suspended/cancelled subscription state and billing dates. |
| `TenantAdminUser` | Tenant admin/cashier login user. Passwords use `IPasswordHashingService`. |
| `TenantLoyaltyLevel` | Dynamic loyalty level per tenant: name, normalized name, threshold, sort order, active flag. |
| `Customer` | Tenant customer/member. Phone is normalized for lookup/deduplication and same-tenant card recovery. |
| `LoyaltyCard` | Central loyalty card aggregate: serial, current balance, lifetime points, level, auth token, last activity. |
| `PointTransaction` | Point ledger movement. Includes transaction type, points, campaign and tenant context. |
| `PointLot` | FIFO lot created by positive earn transactions, with expiry and remaining amount. |
| `PointLotConsumption` | FIFO consumption record linked to lot, consuming transaction and redemption when applicable. |
| `Redemption` | Reward or monetary discount redemption, status, consumed points and monetary snapshot. |
| `RewardCatalogItem` | Redeemable catalog item, monthly product support, validity dates and minimum level. |
| `ProgramConfig` | Tenant program key/value configuration such as earning rate, expiration, birthday multiplier and monetary conversion. |
| `PointCampaign` | Time-bound point multiplier campaign with optional minimum purchase and level eligibility. |
| `CustomNotificationCampaign` | Admin-created Wallet message campaign with audience, schedule and display window. |
| `LoyaltyNotification` | Notification/event unit. Used by Wallet visible event mechanism and deliveries. |
| `NotificationDelivery` | Delivery attempt/status for a notification/channel. |
| `DeviceRegistration` | Apple Wallet device registration and push token by pass serial. |
| `MemberDigitalWallet` | Provider link/sync state for external wallets such as Google Wallet. |

Important enums include `TransactionType`, `RedemptionType`, `RedemptionStatus`, `NotificationType`, `NotificationChannel`, `NotificationStatus`, `NotificationDeliveryStatus`, `DigitalWalletProvider`, `DigitalWalletStatus`, `CustomNotificationCampaignStatus`, `CampaignLevelEligibility`, `TenantSubscriptionStatus`, `TenantSuspensionReason` and `PassUpdateReason`.

## Multi-Tenancy Rules

Current architecture:

- Shared database.
- Each business is a `Tenant`.
- Tenant routes use `/{tenantSlug}/...`.
- Platform routes use `/platform/...`.
- No production default tenant.
- No production KBeauty seed.
- Platform Admin must operate without business `TenantContext`.
- Tenant Admin must operate with `TenantContext`.
- TenantContext is scoped and stored in `TenantContext` implementing `ITenantContext` and `IMutableTenantContext`.
- Most business entities are tenant-owned and filtered/guarded by EF tenant context.
- Admin-to-API calls send a tenant slug in signed HMAC headers; they do not send a free-form TenantId.

Guardrails:

- Do not hardcode `kbeauty`.
- Do not restore `Tenancy:DefaultTenantSlug`.
- Do not accept TenantId from tenant-facing UI.
- Do not let `/platform/*` resolve a business tenant.
- Do not weaken AppDbContext tenant guards.
- Do not introduce cross-tenant joins without explicit review.

Known corrected issue: Blazor Interactive Server has a different DI scope than SSR/request middleware. Tenant context for interactive circuits is restored from authenticated tenant claims through Admin-specific circuit/context services. Do not revert that pattern.

## Admin Routes

Blazor Admin pages:

| Route | Page | Purpose |
| --- | --- | --- |
| `/` | mapped endpoint | Redirects to `/platform/login`. |
| `/platform/login` | `PlatformLogin.razor` | Super Admin login. Public. |
| `/platform` | `PlatformTenants.razor` | Platform tenant list. Super Admin only. |
| `/platform/tenants` | `PlatformTenants.razor` | Platform tenant list/create. Super Admin only. |
| `/platform/tenants/{tenantId}` | `PlatformTenantDetail.razor` | Tenant operations: billing, suspend/reactivate/cancel/delete/logo. |
| `/{tenantSlug}/login` | `Login.razor` | Tenant Admin login. Public for the tenant slug. |
| `/{tenantSlug}/join` | `Join.razor` | Public customer registration and wallet add flow. |
| `/dashboard` | `Dashboard.razor` | Tenant dashboard. |
| `/reports` | `Reports.razor` | Tenant reports landing with links to report pages. |
| `/reports/inactive-customers` | `InactiveCustomersReport.razor` | Customers active in the program with no point movements or redemptions for the selected inactivity threshold. |
| `/reports/top-rewards` | `TopRewardsReport.razor` | Most redeemed catalog rewards for the selected period. |
| `/scan` | `Scan.razor` | Add points. Supports manual ID/serial and QR scanner. |
| `/redeem` | `Redeem.razor` | Redeem points, including monetary discount and catalog rewards. Supports QR scanner. |
| `/customers` | `Customers.razor` | Customer list/search. |
| `/customers/{CustomerId:guid}` | `CustomerDetail.razor` | Customer detail/audit by ID. |
| `/customers/{SerialNumber}` | `CustomerDetail.razor` | Customer detail/audit by serial. |
| `/redemptions` | `Redemptions.razor` | Redemption history/operations. |
| `/rewards` | `Rewards.razor` | Reward catalog and monthly product admin. |
| `/campaigns` | `Campaigns.razor` | Point campaigns. |
| `/levels` | `Levels.razor` | Dynamic tenant loyalty levels and recalculation. |
| `/marketing-notifications` | `MarketingNotifications.razor` | Custom Wallet messages. |
| `/notifications` | `Notifications.razor` | Historical/admin notification page. Exists but is hidden from main menu. |
| `/config` | `Config.razor` | Program configuration. Some legacy settings are visually hidden. |
| `/quick-help` | `QuickHelp.razor` | Quick cashier/admin help, registration QR and printable poster. |

Visible Admin menu is grouped by operation, customers, loyalty program, communication and administration. Do not reintroduce any retired Admin hostname.

Admin customer screens intentionally ignore `Customer.Email` as visible customer data. The field still exists for legacy/domain/API compatibility, but tenant Admin UI should show name, phone, customer ID/serial and operational data instead. Platform tenant creation has synchronized color picker and hex fields for tenant branding colors. Tenant Admin `/config` includes Apple Wallet card branding: optional `TenantBranding.WalletBackgroundColor` (`#RRGGBB`), optional wallet-specific logo, Apple Wallet logo scale and Apple Wallet primary content mode. Quick Help registration QR/poster must continue using the same tenant join URL source (`Admin:PublicBaseUrl` when configured, otherwise current Admin base URI). The poster top uses `TenantBrandingInfo.LogoUrl` when available and falls back to tenant display name.

## API Endpoints

All normal Admin API calls are tenant-aware via HMAC middleware when their route matches `AdminApiAuthenticationMiddleware.RequiresAdminApiAuthentication`.

### Admin and Maintenance

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/admin/dashboard` | Dashboard aggregates. |
| POST | `/api/admin/points/expire` | Execute FIFO point expiration. |
| POST | `/api/admin/levels/recalculate` | Recalculate rolling levels. |
| GET | `/api/admin/points/expiration-notification-candidates` | Preview expiring point notification candidates. |
| GET | `/api/admin/rewards/monthly-product-notification-candidates` | Preview monthly product notification candidates. |
| GET | `/api/admin/customers/birthday-notification-candidates` | Preview birthday benefit notification candidates. |
| GET | `/api/admin/campaigns/notification-candidates` | Preview point campaign notification candidates. |

### Customers

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/customers` | Register customer and card through API. |
| GET | `/api/customers/{serialNumber}` | Find customer/card by serial/ID. |
| GET | `/api/customers/{serialNumber}/transactions` | Paginated point transaction history. |

### Points

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/points` | Add points from purchase amount. Executes existing multiplier/campaign/birthday/level/Wallet flow. |

### Redemptions

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/redemptions` | Create catalog reward redemption or monetary discount redemption. |
| PUT | `/api/redemptions/{id}/confirm` | Confirm pending redemption. |
| PUT | `/api/redemptions/{id}/cancel` | Cancel pending redemption and restore FIFO lots. |
| GET | `/api/redemptions/catalog/{serialNumber}` | Catalog available to customer level/balance. |

### Rewards

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/rewards` | List reward catalog/admin items. |
| GET | `/api/rewards/{id}` | Get reward by ID. |
| POST | `/api/rewards` | Create reward or monthly product. |
| PUT | `/api/rewards/{id}` | Update reward. |
| PUT | `/api/rewards/{id}/activate` | Activate reward. |
| PUT | `/api/rewards/{id}/deactivate` | Deactivate reward. |

### Campaigns

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/campaigns` | List point campaigns. |
| GET | `/api/campaigns/{id}` | Get point campaign. |
| POST | `/api/campaigns` | Create point campaign. Active/current campaigns trigger notifications. |
| PUT | `/api/campaigns/{id}` | Update point campaign. Active/current campaigns trigger notifications. |
| PUT | `/api/campaigns/{id}/activate` | Activate point campaign. |
| PUT | `/api/campaigns/{id}/deactivate` | Deactivate point campaign. |

### Config and Levels

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/config` | Read tenant ProgramConfig entries. |
| PUT | `/api/config` | Update ProgramConfig entries. |
| GET | `/api/levels` | List dynamic tenant levels. |
| PUT | `/api/levels` | Update tenant levels, rename dependent references and recalculate cards transactionally. |

### Custom Notifications

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/custom-notification-campaigns/preview` | Preview audience. |
| GET | `/api/custom-notification-campaigns` | List custom notification campaigns. |
| GET | `/api/custom-notification-campaigns/{id}` | Get custom notification campaign. |
| POST | `/api/custom-notification-campaigns` | Create custom campaign. |
| POST | `/api/custom-notification-campaigns/{id}/send` | Send/process campaign now. |
| PUT | `/api/custom-notification-campaigns/{id}/cancel` | Cancel scheduled/unprocessed campaign. |

Custom campaigns store a short notification text and a longer message detail. Tenant Admin UI labels them `Notificación` and `Detalle del mensaje`. Keep those concepts provider-neutral; Apple Wallet and Google Wallet adapt them per channel.

### Notifications

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/notifications` | List notifications. |
| GET | `/api/notifications/metrics` | Notification metrics. |
| GET | `/api/notifications/{id}` | Notification detail. |
| POST | `/api/notifications` | Create notification. |
| POST | `/api/notifications/{id}/process` | Process one notification. |
| POST | `/api/notifications/{id}/retry` | Retry notification delivery. |
| PUT | `/api/notifications/{id}/cancel` | Cancel notification. |

### Public Join

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/public/{tenantSlug}/join` | Public tenant-aware customer join. Reuses an existing same-tenant card only when normalized phone and normalized first/last name match. |
| PUT | `/api/public/{tenantSlug}/join/{serialNumber}/birthday` | Public birthday update after join. |

### Apple Wallet and PassKit

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/passes/{serialNumber}` | Download production `.pkpass` for Safari/iPhone. |
| GET | `/api/dev/passes/{serialNumber}` | Development pass download route. |
| GET | `/v1/passes/{passTypeIdentifier}/{serialNumber}` | Apple Wallet pass refresh endpoint. |
| POST | `/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}` | Register Apple Wallet device/push token. |
| DELETE | `/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}` | Unregister Apple Wallet device. |
| GET | `/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}` | Return updatable serials since `passesUpdatedSince`. |
| POST | `/v1/log` | Apple Wallet log endpoint. |
| GET | `/api/wallet-assets/apple/{assetName}` | Serve bundled Apple Wallet image assets. |

`/api/passes/{serialNumber}` returns `application/vnd.apple.pkpass` and can be opened directly in Safari on iPhone. Tenant is resolved from `LoyaltyCard.SerialNumber`.

### Google Wallet

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/customers/{serialNumber}/wallets/google/save-link` | Create/update Google LoyaltyClass/LoyaltyObject and return Save to Google Wallet URL. |

## Admin to API Authentication

Admin uses `Admin:ApiBaseUrl` to configure named HttpClient `LoyaltyCloudApi`.

Tenant Admin API calls use:

- `AdminApi:SharedSecret`.
- `X-LoyaltyCloud-TenantSlug`.
- `X-LoyaltyCloud-OperatorId`.
- `X-LoyaltyCloud-Timestamp`.
- `X-LoyaltyCloud-Signature`.

The API validates HMAC, resolves tenant by slug, verifies operational subscription state, sets `TenantContext`, then runs the controller/handler.

Do not pass TenantId from browser/UI. Do not replace this with plain relative requests against Admin.

## Apple Wallet Flow

Customer iPhone flow:

1. Customer opens `/{tenantSlug}/join`.
2. Admin/public join page calls `POST /api/public/{tenantSlug}/join`.
3. API resolves tenant from slug and creates/reuses customer/card. Existing cards are reused only when normalized phone and normalized first/last name match.
4. Response includes `PassDownloadUrl`.
5. Safari opens `GET /api/passes/{serialNumber}`.
6. `PassGeneratorService` resolves tenant by card serial, reads tenant branding and dynamic levels, builds `pass.json`, signs manifest with Apple certificate, returns `.pkpass`.

Wallet card branding is tenant-aware:

- `TenantBranding.WalletBackgroundColor` overrides the Apple Wallet background color.
- `TenantBranding.WalletLogoScalePercent` controls the visual size of the Apple Wallet logo inside the fixed Apple `logo*.png` canvas. Range is 60-100 and default is 100, preserving historical rendering.
- `TenantBranding.AppleWalletPrimaryContentMode` controls the mutually exclusive main Apple Wallet content: `CustomerName` keeps the existing primary field with the customer's first name; `Image` omits the customer-name primary field and includes Apple Wallet strip assets.
- Default Apple Wallet primary content mode is `CustomerName` so existing tenants keep their current pass layout after migration/deploy without configuration changes.
- `TenantBranding.AppleWalletStripImageBlobName` stores the independent source image for Apple Wallet strip/banner mode. The source remains stored when switching back to `CustomerName`, allowing tenants to switch back to `Image` without reuploading.
- Image mode generates Apple Wallet storeCard strip assets named `strip.png`, `strip@2x.png` and `strip@3x.png` at 375x144, 750x288 and 1125x432 respectively, using centered cover crop with preserved aspect ratio.
- If no wallet color is set, Apple Wallet falls back to `TenantBranding.PrimaryColor`, then white.
- Apple Wallet `foregroundColor` and `labelColor` are derived automatically from background luminance for readable contrast.
- `TenantBranding.WalletLogoBlobName` points to wallet-specific generated assets under `tenant-branding/{tenantId}/wallet-branding/...`.
- If no wallet logo is set, Apple Wallet falls back to generated assets from the tenant's main `LogoBlobName`, then bundled generic LoyaltyCloud assets.
- Changing wallet color, wallet logo scale, Apple Wallet primary content mode or strip image marks installed Apple passes updated and sends APNs best-effort; no visible `changeMessage` is generated for visual branding changes.
- Wallet branding refresh now uses the shared Apple Wallet pass refresh path: touch `LoyaltyCard.LastActivityAt`, save, inspect `DeviceRegistration`, send APNs, and log `NoRecipients`, `NoOp`/unsupported, accepted pushes and rejected pushes.
- APNs responses are explicit results. HTTP 200 is success; HTTP 429/5xx and network/timeout failures are transient; APNs permanent reasons such as `BadDeviceToken`, `Unregistered` and `DeviceTokenNotForTopic` are permanent. Non-2xx APNs responses must never be counted as success.
7. iPhone installs pass and calls PassKit registration route.
8. API stores `DeviceRegistration`.
9. Later business events update `LoyaltyCard.LastActivityAt` and send APNs.
10. iPhone calls `/v1/devices/.../registrations/...`.
11. API returns updated serials.
12. iPhone calls `/v1/passes/.../{serialNumber}`.
13. API dynamically generates a fresh pass from SQL and returns it.

Important implementation details:

- `PassGeneratorService` generates dynamically from current SQL state, not from a cached `.pkpass` blob.
- Permanent `points` field shows current available balance.
- Rolling level progress uses rolling points, not `CurrentPoints`.
- `LevelChanged`, `PointsAdded`, `PointsExpiring`, `MonthlyProductStarted`, `BirthdayBenefitStarted`, `PointCampaignStarted` and `Custom` can create visible events.
- Apple Wallet `changeMessage` must contain `%@`.
- For `PointsAdded`, the temporary field is used for points earned in the operation; permanent `points` remains total balance without changeMessage.
- For `Custom`, the short notification text is used on the temporary visible/changeMessage field and the long detail is shown on the back of the pass.
- Tenant logos are read from Blob Storage through `TenantWalletAssetProvider`; fallback is neutral bundled assets.
- Scaled Apple Wallet logo assets are stored under the Apple-specific asset folder so Google Wallet keeps using its unscaled logo asset.
- Public join recovery does not use SMS/OTP. It permits re-adding the same card when phone plus first/last name match after normalization, and returns a generic rejection for phone/name mismatch without exposing serial, points or existing account details.
- Apple Pass Type ID may still be `pass.com.kbeautymx.loyalty`.
- Apple Key Vault secret names may still be `kbeauty-*`.
- WWDR secret is optional. Production works without `kbeauty-wwdr-certificate` because the implementation first uses the certificate chain in the `.p12` or bundled `Certificates/AppleWWDRCAG4.cer`, then Key Vault as fallback.

## Google Wallet Flow

Google Wallet is implemented as a separate provider from Apple Wallet.

Current status: Production Approved.

Custom Wallet messages use Google Wallet `Message.header` for LoyaltyCloud's short notification text and `Message.body` for the long message detail. `TEXT_AND_NOTIFY` is used when adding a message that should also notify Android users.

Flow:

1. Customer joins through `/{tenantSlug}/join`.
2. Join UI uses platform detection. If the same tenant/phone/name already exists, the existing loyalty card serial is reused.
3. Android path calls `POST /api/customers/{serialNumber}/wallets/google/save-link`.
4. `WalletsController` resolves and sets tenant by card serial through `IWalletTenantContextResolver`.
5. `CreateGoogleWalletSaveLinkCommand` calls `IGoogleWalletService`.
6. `GoogleWalletService` reads `MemberWalletData`, creates/updates `MemberDigitalWallet`, builds deterministic class/object IDs, ensures Google `LoyaltyClass`, creates/updates Google `LoyaltyObject`, stores sync status and returns a Google Save URL.
7. Client opens `https://pay.google.com/gp/v/save/{jwt}`.

Update behavior:

- `AddPointsHandler` calls Google Wallet synchronization best-effort after points are saved when a Google wallet link exists.
- Google Wallet sync is disabled when `GoogleWallet:Enabled=false`.
- There is no full outbox/retry worker for Google Wallet yet.
- Issuer is approved for production.
- `LoyaltyClass` is synchronized through PATCH.
- `LoyaltyObject` is created/updated automatically.
- Save Link generation works correctly in STG and is compatible with production-approved Google Wallet setup.
- PROD has `GoogleWallet__*` App Settings configured.
- PROD Key Vault contains `loyaltycloud-google-wallet-service-account-json`.
- `GoogleWallet__ServiceAccountJson` in PROD references `loyaltycloud-google-wallet-service-account-json` through Key Vault.
- Current implementation still does not include a robust outbox/retry model.

Google ID pattern:

- Class ID: `{issuerId}.{classSuffix}`.
- Object ID: `{issuerId}.{objectIdPrefix}-{normalized-serial}`.

Known Google Wallet STG status:

- STG Key Vault contains `loyaltycloud-google-wallet-service-account-json`.
- `GoogleWallet__Enabled=true` was configured during debugging.
- Issuer ID used in STG: `3388000000023165331`.
- Issuer is Production Approved.
- Save Link generation works correctly.
- STG is no longer in Demo mode.

Known Google Wallet PROD status:

- Google Wallet is approved for production.
- PROD has `GoogleWallet__*` configured.
- `GoogleWallet__ServiceAccountJson` is a Key Vault reference to `loyaltycloud-google-wallet-service-account-json`.
- Do not document or print the service account JSON, private key, tokens or any related secret values.
- Pending decision: `GoogleWallet__ProgramName` is currently `KBeauty Loyalty`; the current option under consideration is changing it to `KBeauty`, then later making the program name configurable per tenant.

Important review status rule:

- Do not set `reviewStatus = APPROVED` from code.
- Google assigns `APPROVED` automatically.
- The API should send `UNDER_REVIEW` when synchronizing `LoyaltyClass`.
- `APPROVED` in a PATCH payload causes Google Wallet to reject the request with HTTP 400.

Do not log service account JSON, private keys, access tokens or Save JWTs.

## Points, Redemptions, Levels and Notifications

Points:

- `POST /api/points` accepts purchase amount and card serial.
- Business logic calculates points using ProgramConfig, birthday multiplier and active point campaigns.
- It writes `PointTransaction`, `PointLot`, updates `LoyaltyCard`, recalculates levels and triggers Wallet update/notifications where applicable.

FIFO and expiration:

- Positive earn transactions create `PointLot`s.
- Redemptions consume lots through `PointLotConsumption`.
- Expiration consumes/removes expired availability and writes expiration transactions.
- Reversal/cancellation restores exact consumed lot amounts.

Redemptions:

- Catalog redemption uses `RewardCatalogItemId`.
- Monetary redemption uses requested points and tenant `ProgramConfig` conversion.
- Browser monetary estimate is UX only; API is authoritative.
- Monetary redemption persists snapshot fields on `Redemption`.

Levels:

- Dynamic per tenant through `TenantLoyaltyLevel`.
- Rolling 12-month calculation.
- Do not use `CurrentPoints` to calculate level progress.
- Level update flow renames references in cards/rewards/campaigns/custom audiences and recalculates cards transactionally.
- APNs after level update remains best-effort outside SQL transaction.

Notifications:

- `LoyaltyNotification` plus `NotificationDelivery`.
- Apple Wallet channel implemented.
- Visible event selection is recency-first; priority is a tie-breaker.
- `LevelChanged` must beat `PointsAdded` when they happen for the same effective operation.

## Background Services

Only API registers hosted services.

`LoyaltyMaintenanceBackgroundService`:

- Config section: `LoyaltyMaintenance`.
- Default: enabled, no startup run, every 12 hours.
- Runs subscription maintenance, point expiration, level recalculation, expiring point notifications, monthly product notifications, birthday benefit notifications and point campaign notifications.
- Runs per operational tenant.

`LoyaltyNotificationBackgroundService`:

- Config section: `LoyaltyNotifications`.
- Default: enabled, runs once on startup, `PollIntervalSeconds=120`.
- Runs due custom notification campaigns and pending notification deliveries/retries.
- `ProcessImmediately` remains the normal path for manual/current notifications; polling is the recovery/fallback path.
- Transient APNs failures are retried with simple backoff using existing delivery timestamps and attempt counts. Permanent APNs failures are terminal and should not loop forever.
- Old `Processing` notifications can be recovered by the scheduler without adding new columns.

Historical note: the notification polling interval was previously extended to 12 hours to avoid keeping Azure SQL Serverless awake. STG and PROD now use Azure SQL Basic DTU, so that auto-pause constraint no longer applies in the same way. `LoyaltyMaintenance` remains a separate 12-hour maintenance worker.

## Configuration

Connection strings:

- `ConnectionStrings:DefaultConnection`.
- Azure App Service form: `ConnectionStrings__DefaultConnection`.

Important App Settings:

| Setting | Applies to | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | API/Admin | Runtime environment. |
| `DOTNET_ENVIRONMENT` | API/Admin | Generic host environment. |
| `ConnectionStrings:DefaultConnection` | API/Admin/Tools | SQL Server connection string. |
| `Azure:KeyVaultUri` | API/Admin/Tools | Adds Azure Key Vault to configuration and registers `SecretClient`. |
| `Azure:BlobStorage:ConnectionString` | API/Admin | Blob Storage connection string. |
| `Azure:BlobStorage:PassContainer` | API/Admin | Blob container, normally `passes`. |
| `Azure:BlobStorage:SasExpirationMinutes` | API/Admin | SAS URL duration for logo assets. |
| `Admin:ApiBaseUrl` | Admin | Base URL of API backend. Required at Admin startup. |
| `Admin:PublicBaseUrl` | Admin | Public Admin base URL used by Quick Help registration links, QR and printable poster. Environment-specific; empty falls back to current Admin request/base URI. |
| `Admin:Auth:SessionHours` | Admin | Tenant Admin cookie duration. RC1 target 168. |
| `AdminApi:SharedSecret` | API/Admin | HMAC secret for Admin to API calls. Must match on both. |
| `SuperAdmin:Username` | Admin | Platform Admin username. |
| `SuperAdmin:PasswordHash` | Admin | Platform Admin password hash. |
| `SuperAdmin:SessionHours` | Admin | Platform Admin cookie duration, default 8. |
| `Apple:PassTypeIdentifier` | API/Admin | Apple pass type identifier. Legacy KBeauty value is still expected. |
| `Apple:TeamIdentifier` | API/Admin | Apple Team ID. |
| `Apple:WebServiceURL` | API/Admin | Public API base embedded in passes and public join response. Must be environment-specific. |
| `Apple:OrganizationName` | API/Admin | Apple pass organization. Legacy KBeauty value may remain. |
| `Apple:ApnHost` | API/Admin | APNs host, normally `https://api.push.apple.com`. |
| `Apple:ApnPrivateKeyPath` | Development | Local APNs `.p8` path when real APNs is enabled locally. |
| `Wallet:UseRealPassSigning` | Development mainly | Chooses real vs development pass generator in Development. Non-Development always uses real signing. |
| `Wallet:UseRealApns` | Development mainly | Chooses ApnService vs NoOp locally. Non-Development uses ApnService. |
| `Cors:AllowedOrigins` | API | Allowed Admin/front-end origins. |
| `LoyaltyMaintenance:*` | API | Maintenance worker configuration. |
| `LoyaltyNotifications:*` | API | Notification worker and visible event config. |
| `CustomNotificationCampaigns:BatchSize` | API | Due custom campaign batch size. |
| `Provisioning:TrialDays` | API/Admin/Tools | Default tenant trial days. |
| `Billing:GracePeriodDays` | API/Admin | Subscription grace behavior. |
| `GoogleWallet:*` | API/Admin | Google Wallet provider settings. |

Key Vault secret names in current implementation/scripts:

| Secret | Purpose |
| --- | --- |
| `loyaltycloud-sql-connection-string` | SQL connection string. |
| `loyaltycloud-storage-connection-string` | Storage connection string. |
| `loyaltycloud-admin-api-shared-secret` | Admin/API HMAC secret. |
| `loyaltycloud-superadmin-username` | Platform Admin username. |
| `loyaltycloud-superadmin-password-hash` | Platform Admin password hash. |
| `kbeauty-pass-certificate` | Apple Wallet signing `.p12` as Base64. |
| `kbeauty-pass-certificate-password` | Apple Wallet `.p12` password. |
| `kbeauty-wwdr-certificate` | Optional WWDR cert as Base64. |
| `kbeauty-apn-private-key` | APNs `.p8` PEM content. |
| `kbeauty-apn-key-id` | APNs key ID. |
| `kbeauty-apn-team-id` | APNs team ID. |
| `loyaltycloud-google-wallet-service-account-json` | Google Wallet service account JSON. |

Never store secret values, tokens, private keys, connection strings or passwords in docs or source.

## Azure PROD

Production/UAT resources currently referenced:

| Resource | Name |
| --- | --- |
| Resource Group | `rg-loyaltycloud-prod` |
| API App Service | `loyaltycloud-api-894839` |
| API OS/runtime | Linux, .NET 9 |
| API URL | `https://api.loyaltycloud.net` |
| Legacy API URL | `https://loyaltycloud-api-894839.azurewebsites.net` |
| Shared Linux App Service Plan | `asp-loyaltycloud-api-free` |
| Shared Linux App Service Plan SKU | Basic B1, capacity 1, West US 3 |
| New Admin App Service Linux | `loyaltycloud-admin-prod-01` |
| New Admin OS/runtime | Linux, .NET 9 |
| Admin public URL | `https://admin.loyaltycloud.net` |
| Legacy Admin App Service Windows | `loyaltycloud-admin` |
| Legacy Admin URL | `https://loyaltycloud-admin.azurewebsites.net` |
| DNS provider | Cloudflare |
| SQL Server | `sql-loyaltycloud-894839` |
| Active DB | `LoyaltyCloudFree` |
| Storage | `stloyaltycloud894839` |
| Key Vault | `kv-loyaltycloud-894839` |

Current PROD compute/cost state:

- API and new Admin Linux run in the same Linux B1 App Service Plan `asp-loyaltycloud-api-free`.
- The plan name still contains `free`, but the plan was scaled to SKU `B1`, tier `Basic`, capacity `1`. Do not recreate/rename it only because of the legacy name.
- Legacy Admin Windows `loyaltycloud-admin` still exists during the transition and must not be removed or broken until cutover is complete.
- Admin Windows is a fallback while the team transitions to `https://admin.loyaltycloud.net`.
- Azure SQL `LoyaltyCloudFree` was migrated successfully from General Purpose Serverless `GP_S_Gen5_2` to Basic DTU after validating the same procedure in STG.
- Final PROD SQL state: status `Online`, tier `Basic`, SKU `Basic`, service objective `Basic`, `maxSizeBytes=2147483648` (2 GB), `useFreeLimit=null`.
- PROD SQL no longer uses Serverless auto-pause, so the cold start caused by waking the database is removed for PROD.
- API PROD, Admin PROD and Wallet PROD were manually validated after the migration.

Current PROD release state:

- Current stable PROD release tag: `v1.0.0`.
- Release SHA: `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.
- `main` remains the primary branch.
- Permanent integration branch `staging` exists for Azure STG release-candidate validation.
- Formal release process is documented in `docs/RELEASE_PROCESS.md`.
- Never develop a new feature directly on `main`.
- Never develop directly on `staging`.
- Before implementing a new feature, verify the current branch. If currently on `main` or `staging`, create a dedicated feature branch before modifying functional code.
- Use branch prefixes `feature/`, `bugfix/` and `hotfix/`.
- STG validation remains required before PROD.
- Feature branches merge by PR into `staging`.
- Azure STG should be deployed from `staging` when validating the next integrated release.
- After STG approval, `staging` merges by PR into `main`.
- PROD must be deployed from integrated `main`, never directly from `staging` or a feature branch.
- Release tags are created only after PROD deploy and smoke test succeed.
- Code rollback uses a known immutable release tag.
- Database rollback is a separate reviewed process and is not implied by checking out an older tag.
- Deployment slots are not available on the current B1 App Service Plan, and the plan should not be upgraded only to obtain slots unless explicitly approved.
- Existing historical checkpoint tag: `prod-2026-08-24-before-billing`.

Current PROD Billing/Payments state:

- Billing/Payments is deployed and validated in PROD.
- Migration `AddBillingPayments` is already applied in PROD.
- Stripe LIVE is configured.
- PROD Stripe webhook endpoint: `https://api.loyaltycloud.net/api/billing/webhooks/stripe`.
- Tenant Billing UI is active and validated in PROD.
- Current Founder plan prices: 1 month `$249 MXN`, 3 months `$699 MXN`, 6 months `$1,299 MXN`, 12 months `$2,490 MXN`.
- Billing UI displays 3 months `Ahorras $48`, 6 months `Ahorras $195`, and 12 months `2 meses GRATIS` plus `Ahorras $498`.

Quick Help/public registration QR should use `Admin:PublicBaseUrl=https://admin.loyaltycloud.net` in PROD. Do not change Apple Wallet `Apple:WebServiceURL` as part of Admin-domain QR work unless explicitly requested.

Current PROD domain state:

- `loyaltycloud.net` is managed in Cloudflare.
- `api.loyaltycloud.net` points to `loyaltycloud-api-894839.azurewebsites.net`.
- `admin.loyaltycloud.net` points to `loyaltycloud-admin-prod-01.azurewebsites.net`.
- During initial configuration, Cloudflare CNAME records were left as DNS-only.
- Azure verification TXT records `asuid.api` and `asuid.admin` were added.
- Both custom domains are Verified/Secured in Azure and use managed Azure App Service certificates.
- `GET /` on the API returns 404 because there is no API root endpoint; this is expected and was used only to confirm HTTPS/TLS.

Current PROD Key Vault/Admin Linux state:

- PROD Key Vault is `kv-loyaltycloud-894839` with RBAC enabled.
- New Admin PROD Linux uses a System Assigned Managed Identity.
- Current Admin Linux principal ID: `28e04e72-b2e1-4a77-9ab3-30430b81d8b0`.
- It has `Key Vault Secrets User` on `kv-loyaltycloud-894839`.
- New Admin PROD Linux `DefaultConnection` uses Key Vault reference `@Microsoft.KeyVault(VaultName=kv-loyaltycloud-894839;SecretName=loyaltycloud-sql-connection-string)`.
- The Key Vault reference was validated and works.
- PowerShell/Azure CLI repeatedly truncated `@Microsoft.KeyVault(...)` references, especially losing the final `)`. The robust workaround that worked was to create JSON and use `az rest` against the App Service `/config/connectionstrings` resource.

Current PROD Admin/API transition state:

- New Admin PROD Linux `Admin__ApiBaseUrl` was changed from `https://loyaltycloud-api-894839.azurewebsites.net` to `https://api.loyaltycloud.net`.
- Login, navigation and `/platform/tenants` on the new Admin Linux were manually validated against real PROD data.
- `Admin__PublicBaseUrl=https://admin.loyaltycloud.net` was configured both on the new Admin Linux and intentionally on the legacy Admin Windows so any newly printed Quick Help QR points to the new Admin domain.
- Do not remove the legacy Admin Windows app or plan until the transition is complete and explicitly approved.
- Do not change `Apple__WebServiceURL` yet.

## Azure STG

See `docs/STAGING_SETUP.md` for the full real procedure.

Current STG resources:

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
| Database | `LoyaltyCloudStg` |
| Storage | `stloyaltycloudstg01` |
| Blob container | `passes` |
| Key Vault | `kv-loyaltycloud-stg-01` |
| Managed Identity | system-assigned identities on API and Admin |
| RBAC | `Key Vault Secrets User` for API/Admin identities on STG Key Vault |

Current STG compute/cost state:

- API App Service Plan is currently F1 Free.
- Admin App Service Plan is currently F1 Free.
- Azure SQL `LoyaltyCloudStg` was migrated successfully from General Purpose Serverless `GP_S_Gen5_2` to Basic DTU.
- Final STG SQL state: tier `Basic`, SKU `Basic`, `maxSizeBytes=2147483648` (2 GB), `useFreeLimit=null`.
- STG SQL no longer uses Serverless auto-pause, so the cold start caused by waking the database is removed for STG.
- Storage observed through Azure Monitor during migration was approximately 26.9 MiB, around 1.3% of 2 GB.
- API STG, Admin STG and Wallet were manually validated after the migration.
- Admin Linux STG test was successful. Do not remove existing STG resources yet.

Critical STG incident:

- STG App Services were recreated.
- API and Admin lost App Settings and connection strings.
- API STG settings were restored.
- Admin STG initially failed with HTTP 500.30 because it still tried to access `kbeauty-kv.vault.azure.net`.
- Correct `Azure__KeyVaultUri` for API and Admin STG is `https://kv-loyaltycloud-stg-01.vault.azure.net/`.
- `DefaultConnection` for API and Admin STG uses Key Vault secret `loyaltycloud-sql-connection-string`.
- Correct Key Vault reference is `@Microsoft.KeyVault(VaultName=kv-loyaltycloud-stg-01;SecretName=loyaltycloud-sql-connection-string)`.
- PowerShell/Azure CLI had quoting issues with the final parenthesis of Key Vault references; reliable method was JSON files passed to `az webapp config connection-string set`.
- Admin STG also needed `AdminApi__SharedSecret` restored to match API STG.
- Admin STG also needed `ConnectionStrings__DefaultConnection`.
- Current status after recovery: Admin STG starts and SuperAdmin login works; API STG starts and responds.

## Local Configuration

Local development typically uses:

- `src/LoyaltyCloud.API/appsettings.Development.json`.
- `src/LoyaltyCloud.Admin/appsettings.Development.json`.
- user-secrets for local passwords, connection strings and Apple/Google secrets.
- LocalDB or configured SQL Server.

Do not put certificate files, `.p8`, `.p12`, service account JSON or connection strings in the repo.

For local real Apple Wallet:

- `Wallet:UseRealPassSigning=true`.
- `Wallet:UseRealApns=true` only when APNs credentials are present.
- Local provider reads local Apple secrets/config paths.

For Google Wallet:

- Keep disabled unless real issuer/service account data exists.
- Prefer `GoogleWallet:ServiceAccountJsonPath` in local user-secrets.

## Infrastructure Scripts

Important scripts:

| Script | Purpose |
| --- | --- |
| `infra/create-stg.ps1` | Dry-run by default; creates STG Azure resources only with `-Execute`. Compatible with Windows PowerShell 5.1. |
| `infra/configure-stg-secrets.ps1` | Configures selected STG secrets. Switches: `-ConfigureAdminApi`, `-ConfigureSuperAdmin`, `-ConfigureAppleWallet`, `-ConfigureGoogleWallet`. |
| `infra/copy-apple-wallet-secrets-to-stg.ps1` | Copies allowlisted Apple Wallet secrets from PROD Key Vault to STG Key Vault. Dry-run by default. |
| `scripts/deploy-stg.ps1` | Reusable STG-only temporary deploy script for a requested branch and target (`Admin`, `Api` or `Both`). Dry-run by default, deploys Admin to `loyaltycloud-admin-linux-stg-01`, and blocks deployment when migration files are present relative to `origin/staging`. |

Lessons embedded in scripts:

- Azure CLI warnings can appear on stderr with exit code 0. Only nonzero exit code is fatal.
- Resource-not-found during dry-run/show should be treated as planned creation where appropriate.
- Windows Web App creation must not reuse Linux runtime arguments.
- Windows PowerShell 5.1 does not support `ProcessStartInfo.ArgumentList`.
- SQL password prompt should only happen when creating SQL Server.
- Temporary branch deploys to STG should use `scripts/deploy-stg.ps1`; it must never be repointed to PROD, it deploys Admin to the Linux STG Admin app, and it never runs EF database updates.

## Deployment Notes

Release procedure:

- Use immutable SemVer tags for PROD releases.
- Current PROD release: `v1.0.0` at `cfe607c6f2b8f92922c4c07a1ce94fd089401091`.
- `main` is the PROD integration branch.
- `staging` is the Azure STG integration/release-candidate branch.
- Never develop features directly on `main` or `staging`; create a dedicated `feature/`, `bugfix/` or `hotfix/` branch from updated `main` first.
- Do not use floating tags such as `latest` for rollback.
- Merge feature branches into `staging` by PR.
- Deploy Azure STG from `staging` for integrated validation.
- Temporary feature-branch deploys to STG should use `scripts/deploy-stg.ps1`; keep them STG-only and continue to use PRs for promotion.
- Promote `staging` to `main` by PR after STG approval.
- Deploy PROD only from integrated `main`.
- Create release tags only after PROD smoke testing confirms the deploy is healthy.
- See `docs/RELEASE_PROCESS.md` for the full procedure.

API is Linux:

- Publish with `dotnet publish`.
- Create deployment ZIP using `tar -a -c -f`.
- Do not use `Compress-Archive` for API Linux ZIP; it caused deployment/runtime issues.

New PROD Admin is Linux:

- Publish with `dotnet publish`.
- Create deployment ZIP using `tar -a -c -f`.
- Do not use `Compress-Archive` for Linux App Services; it produced Windows `\` paths inside the ZIP and Kudu failed during `rsync`.
- Last validated Admin Linux deployment target: `loyaltycloud-admin-prod-01`.
- Last validated package command shape:

```powershell
tar -a -c -f .\artifacts\admin-prod.zip `
  -C .\artifacts\admin-prod .
```

Legacy Admin Windows still exists during transition:

- Publish with `dotnet publish`.
- Create ZIP with `Compress-Archive`.

Do not deploy, run database update or commit unless the user explicitly asks.

## Roadmap Status

Current state: RC1 / UAT real.

Live pending list: `docs/ROADMAP.md`.

Done:

- Apple Wallet signed pass and PassKit web service.
- APNs refresh and Device Registration.
- Public tenant join.
- Admin tenant login and Platform Admin.
- Multi-tenant foundation, tenantized root/dependent entities, filters and tenant guards.
- Tenant provisioning from Platform Admin.
- Hard delete tenant.
- Tenant dynamic levels and tenant-level recalculation.
- Customer detail audit.
- Reports v1 with separate report pages for inactive customers and top redeemed rewards.
- QR add-points and redemption flows.
- Direct monetary discount redemption.
- Reward catalog/monthly product.
- Point campaigns.
- Custom Wallet messages.
- Automatic Wallet visible notifications.
- Tenant branding/logo for Admin and Apple Wallet.
- Quick Help registration QR/poster.
- Google Wallet first vertical slice.
- STG infrastructure scripts and STG setup documentation.

Active/UAT focus:

- Stabilize STG and PROD/UAT configuration.
- Validate KBeauty real flows.
- Continue Google Wallet STG/production smoke testing after deploys.
- Observe both Basic DTU environments for behavior, costs and limits.
- Continue controlled transition from legacy Admin Windows to new Admin Linux.

Known current/pending:

- Google Wallet issuer is Production Approved.
- Reports v1 is a tenant Admin section using the same in-process MediatR/read-service pattern as Dashboard. `/reports` is a lightweight landing, while individual report pages own their filters and queries. It does not add database schema, an API endpoint, charts or exports.
- Google Wallet does not yet have a robust outbox/retry model.
- Google Wallet sync is currently limited mainly to add-points sync once a member is linked.
- Review whether Google Wallet has URLs/base URLs that should move to the new custom domains.
- Analyze safe migration strategy before changing `Apple__WebServiceURL` to `https://api.loyaltycloud.net`.
- Determine impact of changing `Apple__WebServiceURL` on already installed Apple Wallet passes, device registrations and `/v1/*` update flow.
- Do not assume changing `Apple__WebServiceURL` automatically migrates existing installed passes.
- Keep legacy `azurewebsites.net` hostnames compatible during the transition.
- Do not remove `loyaltycloud-admin` or its Windows plan until cutover is complete and explicitly approved.
- Pending decision: `GoogleWallet__ProgramName` is currently `KBeauty Loyalty`; consider `KBeauty` now and tenant-configurable naming later.
- STG and PROD SQL are now Basic DTU and validated; Azure SQL Serverless cold start is no longer a known active issue for these environments.
- Some committed default display values still say KBeauty for Apple/Google compatibility or provisional defaults.
- Provisioning defaults may still be legacy `Mist/Glow/Radiance`; update defaults/templates before generic onboarding if not already handled.
- Serial format still uses `KB-`; do not change without a PassKit/Wallet migration plan.
- Review diagnostic logs before GA.

## Working Conventions

- Inspect before changing.
- Before implementing a new feature, verify the current branch. If currently on `main` or `staging`, create a dedicated feature branch before modifying functional code.
- Do not use `main` or `staging` for everyday feature development.
- Keep changes scoped.
- No large refactors unless explicitly requested.
- No functional code changes for documentation-only tasks.
- No migrations unless model changes require them and the user approves.
- No `database update`, deploy or commit unless explicitly requested.
- Do not run build/tests if the user forbids them.
- For implementation tasks, run relevant tests and `dotnet build .\LoyaltyCloud.sln` only when requested/appropriate.
- Use `rg` for search.
- Use `apply_patch` for manual file edits.
- Never print secrets.
- Keep Apple Wallet/APNs/PassKit endpoints stable.
- Keep public routes stable: `/api/*`, `/v1/*`, `/{tenantSlug}/join`, `/{tenantSlug}/login`, `/platform/login`, `/scan`, `/redeem`.
