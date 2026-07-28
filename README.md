# LoyaltyCloud

LoyaltyCloud is a .NET 9 multi-tenant loyalty SaaS. It supports tenant provisioning, public customer registration, Apple Wallet passes, Google Wallet save links, points, rewards, redemptions, dynamic tenant levels, campaigns, Wallet notifications, platform administration and tenant administration.

Current state: RC1 / real UAT starting.

For future ChatGPT/Codex sessions, read this first:

```text
docs/AI_CONTEXT.md
```

`docs/AI_CONTEXT.md` is the canonical technical and operational handoff. It includes production URLs, Azure resources, deployment rules, migration guardrails, multi-tenant rules, Apple Wallet details and known RC1 debt.

## Architecture

```text
LoyaltyCloud.API      LoyaltyCloud.Admin
        |                         |
        +-----------+-------------+
                    |
                    v
        LoyaltyCloud.Application
                    |
                    v
          LoyaltyCloud.Domain
                    |
                    v
       LoyaltyCloud.Infrastructure
                    |
                    v
 SQL Server / Azure Blob / Key Vault / Apple Wallet / APNs / Google Wallet
```

## Projects

| Project | Responsibility |
| --- | --- |
| `LoyaltyCloud.Common` | Shared constants, results and primitives. |
| `LoyaltyCloud.Domain` | Entities, enums, value objects, domain events and invariants. |
| `LoyaltyCloud.Application` | CQRS/MediatR commands, queries, handlers, validators and interfaces. |
| `LoyaltyCloud.Infrastructure` | EF Core, repositories, read services, Azure integrations, Wallet pass generation, APNs, Google Wallet REST/JWT services and tenant services. |
| `LoyaltyCloud.API` | REST API, public join API, Apple Wallet web service, admin API HMAC middleware and background jobs. |
| `LoyaltyCloud.Admin` | Blazor Server Platform Admin and Tenant Admin. |
| `LoyaltyCloud.Tools` | Internal operational CLI tools. |
| `LoyaltyCloud.Tests` | xUnit tests and guardrails. |

## Wallet Integrations

Apple Wallet is the production `.pkpass` path. In Development it can use either the unsigned `DevelopmentPassGeneratorService` or the real `PassGeneratorService` when local signing secrets are configured.

Google Wallet is currently implemented as the first vertical slice for loyalty cards:

```text
API /api/customers/{serialNumber}/wallets/google/save-link
  -> MediatR CreateGoogleWalletSaveLinkCommand
  -> IGoogleWalletService
  -> MemberDigitalWalletRepository
  -> Google Wallet LoyaltyClass / LoyaltyObject REST API
  -> signed Save to Google Wallet URL
```

Google Wallet is disabled by default with `GoogleWallet:Enabled=false`. When enabled, configuration must provide `GoogleWallet:IssuerId` and either `GoogleWallet:ServiceAccountJson` or `GoogleWallet:ServiceAccountJsonPath`. Object IDs include a tenant id prefix, so the same serial cannot collide across tenants under the same issuer.

The Google Wallet save-link endpoint resolves the tenant from the loyalty card
serial before entering Application, using the same tenant resolution pattern as
Apple Wallet. This keeps `MemberDigitalWallet` writes tenant-scoped and prevents
cross-tenant serial leakage.

## Local Development

Typical local services:

- .NET 9 SDK.
- SQL Server LocalDB.
- Azurite when testing Blob Storage.
- EF Core CLI tools.

Local launch profiles:

| Project | HTTP | HTTPS |
| --- | --- | --- |
| API | `http://localhost:55131` | `https://localhost:55128` |
| Admin | `http://localhost:55130` | `https://localhost:55129` |

Apply migrations locally:

```powershell
dotnet ef database update `
  --project src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

Run API:

```powershell
dotnet run `
  --project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj `
  --launch-profile LoyaltyCloud.API
```

Run Admin:

```powershell
dotnet run `
  --project src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj `
  --launch-profile LoyaltyCloud.Admin
```

Open Platform Admin:

```text
http://localhost:55130/platform/login
```

Open a tenant login after provisioning a tenant:

```text
http://localhost:55130/{tenantSlug}/login
```

## Production RC1

Official Admin:

```text
https://loyaltycloud-admin.azurewebsites.net
```

Official API:

```text
https://loyaltycloud-api-894839.azurewebsites.net
```

Active production/UAT database:

```text
LoyaltyCloudFree
```

The previous Admin hostname was retired and must not be used. Use the current Admin
deployment URL documented for the active environment. The API hostname
`loyaltycloud-api-894839.azurewebsites.net` is still correct.

## Deployment Guardrail

API runs on Linux App Service. Publish to `artifacts/api`, then create the ZIP with Windows `tar`:

```powershell
dotnet publish .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj -c Release -o .\artifacts\api
tar -a -c -f .\artifacts\api.zip -C .\artifacts\api .
```

Admin runs on Windows App Service. Publish to `artifacts/admin`, then create the ZIP with `Compress-Archive`:

```powershell
dotnet publish .\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj -c Release -o .\artifacts\admin
Compress-Archive -Path .\artifacts\admin\* -DestinationPath .\artifacts\admin.zip -Force
```

Do not deploy API as `api.tar.gz` and do not use `--type static`.

## Validation Commands

Run relevant tests for the area changed. Common final checks for implementation work:

```powershell
dotnet restore .\LoyaltyCloud.sln
dotnet build .\LoyaltyCloud.sln -v minimal
dotnet test .\LoyaltyCloud.sln -v minimal --no-build

dotnet ef migrations has-pending-model-changes `
  --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

Do not run `database update`, deploy, create migrations or commit unless explicitly requested.

## Documentation

- `docs/AI_CONTEXT.md`: canonical handoff for future sessions.
- `docs/ROADMAP.md`: current RC1 roadmap.
- `docs/AppleWallet.md`: Apple Wallet technical history and current behavior.
- `docs/AppleWallet-Development.md`: local Wallet testing notes.
- `docs/GoogleWallet.md`: Google Wallet vertical slice, configuration, endpoint and operational checklist.

## Recent Development Improvements

- Repaired the main rebase conflict after the Google Wallet branch diverged from RC1.
- Ported the first Google Wallet vertical slice to the `LoyaltyCloud.*` project structure.
- Added tenant-owned `MemberDigitalWallet` persistence and tenant-aware Google Wallet object IDs.
- Added Google Wallet save-link sync on point updates.
- Kept Apple Wallet and development pass generation paths intact.
- Updated QR generation to expose an accessible registration label.
- Validated `dotnet build .\LoyaltyCloud.sln -v minimal` and `dotnet test .\LoyaltyCloud.sln -v minimal --no-build`.

Do not create `docs/DECISIONS.md`.
