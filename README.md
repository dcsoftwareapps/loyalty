# LoyaltyCloud

LoyaltyCloud is a .NET 9 multi-tenant loyalty SaaS. It supports tenant provisioning, public customer registration, Apple Wallet passes, points, rewards, redemptions, dynamic tenant levels, campaigns, Wallet notifications, platform administration and tenant administration.

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
 SQL Server / Azure Blob / Key Vault / Apple Wallet / APNs
```

## Projects

| Project | Responsibility |
| --- | --- |
| `LoyaltyCloud.Common` | Shared constants, results and primitives. |
| `LoyaltyCloud.Domain` | Entities, enums, value objects, domain events and invariants. |
| `LoyaltyCloud.Application` | CQRS/MediatR commands, queries, handlers, validators and interfaces. |
| `LoyaltyCloud.Infrastructure` | EF Core, repositories, read services, Azure integrations, Wallet pass generation, APNs and tenant services. |
| `LoyaltyCloud.API` | REST API, public join API, Apple Wallet web service, admin API HMAC middleware and background jobs. |
| `LoyaltyCloud.Admin` | Blazor Server Platform Admin and Tenant Admin. |
| `LoyaltyCloud.Tools` | Internal operational CLI tools. |
| `LoyaltyCloud.Tests` | xUnit tests and guardrails. |

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

The old Admin hostname no longer exists and must not be used:

```text
loyaltycloud-admin-894839.azurewebsites.net
```

The API hostname `loyaltycloud-api-894839.azurewebsites.net` is still correct.

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
dotnet ef migrations has-pending-model-changes `
  --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj

dotnet build .\LoyaltyCloud.sln
```

Do not run `database update`, deploy, create migrations or commit unless explicitly requested.

## Documentation

- `docs/AI_CONTEXT.md`: canonical handoff for future sessions.
- `docs/ROADMAP.md`: current RC1 roadmap.
- `docs/AppleWallet.md`: Apple Wallet technical history and current behavior.
- `docs/AppleWallet-Development.md`: local Wallet testing notes.

Do not create `docs/DECISIONS.md`.
