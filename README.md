# LoyaltyCloud

Sistema de lealtad por puntos para KBeauty MX. La solucion permite registrar clientas, emitir tarjetas de lealtad, acumular puntos, consultar historial, canjear rewards y generar pases Apple Wallet (`.pkpass`) para journeys digitales y operacion en tienda.

El repositorio esta organizado con Clean Architecture en .NET 9: API REST, Admin Blazor Server, capa Application con CQRS/MediatR, dominio rico, Infrastructure con EF Core/SQL Server, Azure integrations y herramientas seguras para desarrollo local.

## Project Summary

| Area | Description |
| --- | --- |
| Business domain | Loyalty program for KBeauty customers: points, levels, rewards, redemptions, wallet passes. |
| Backend | ASP.NET Core Web API on .NET 9. |
| Admin UI | Blazor Server / Blazor Web App on .NET 9. |
| Persistence | EF Core 9 with SQL Server / LocalDB. |
| Messaging pattern | CQRS with MediatR commands, queries, handlers and pipeline behaviors. |
| External services | Apple Wallet `.pkpass`, APN, Azure Key Vault, Azure Blob Storage. |
| Local dev services | LocalDB, Azurite, development `.pkpass` mock, development login, demo data seeding. |
| Tests | xUnit, Moq, WebApplicationFactory, EF InMemory for integration tests. |

## Architecture

```text
LoyaltyCloud.API      LoyaltyCloud.Admin
        |                         |
        +-----------+-------------+
                    |
                    v
        LoyaltyCloud.Application
        CQRS, MediatR, validation, app contracts
                    |
                    v
          LoyaltyCloud.Domain
     Entities, value objects, invariants, events
                    |
                    v
       LoyaltyCloud.Infrastructure
 EF Core, repositories, Key Vault, Blob, APN, pass generation
                    |
                    v
 SQL Server / Azure Blob / Key Vault / Apple Wallet / APN
```

### Architectural Style

- **Clean Architecture / Onion**: Domain has no dependency on EF, HTTP, Azure or Blazor.
- **CQRS with MediatR**: Application use cases are commands and queries with focused handlers.
- **Domain-driven model**: `LoyaltyCard` owns point accrual, redemption and level behavior.
- **Infrastructure inversion**: Application depends on interfaces such as `IPassGeneratorService`, `IApnService`, `IStorageService`.
- **Blazor Server Admin**: Admin runs in-process against Application and Infrastructure, not through HTTP API calls.
- **EF Core Unit of Work**: `AppDbContext` implements `IUnitOfWork` and dispatches domain events after successful commits.
- **Development-safe adapters**: Development uses mock `.pkpass` generation and demo seed data without production certificates or Key Vault.

## Project Structure

```text
src/
  LoyaltyCloud.Common
  LoyaltyCloud.Domain
  LoyaltyCloud.Application
  LoyaltyCloud.Infrastructure
  LoyaltyCloud.API
  LoyaltyCloud.Admin

tests/
  LoyaltyCloud.Tests
```

| Project | Responsibility |
| --- | --- |
| `LoyaltyCloud.Common` | Shared constants, result types, pagination, cross-cutting interfaces. |
| `LoyaltyCloud.Domain` | Entities, value objects, enums, domain events, domain exceptions, repository contracts. |
| `LoyaltyCloud.Application` | CQRS commands/queries, handlers, validators, MediatR behaviors, application service interfaces. |
| `LoyaltyCloud.Infrastructure` | EF Core, repositories, SQL persistence, Azure Key Vault, Blob Storage, APN, pass generation, read services, development seeder. |
| `LoyaltyCloud.API` | ASP.NET Core REST API, Swagger, Apple pass endpoints, global exception handling. |
| `LoyaltyCloud.Admin` | Blazor Server admin panel, cookie auth, dashboard, customers, scan, redemptions, config, development login. |
| `LoyaltyCloud.Tests` | Unit and integration tests with fakes and EF InMemory. |

## Runtime URLs and Ports

These are defined in each project's `Properties/launchSettings.json`.

| Project | HTTP | HTTPS |
| --- | --- | --- |
| API | `http://localhost:55131` | `https://localhost:55128` |
| Admin | `http://localhost:55130` | `https://localhost:55129` |
| Azurite Blob | `http://127.0.0.1:10000` | n/a |
| Azurite Queue | `http://127.0.0.1:10001` | n/a |
| Azurite Table | `http://127.0.0.1:10002` | n/a |

## Prerequisites

- .NET 9 SDK.
- SQL Server LocalDB for local development.
- EF Core CLI tools when creating/applying migrations:

```powershell
dotnet tool install --global dotnet-ef
```

- Azurite for local Azure Storage emulation:

```powershell
npm install -g azurite
```

On Windows, if PowerShell blocks the `azurite.ps1` shim, use the `.cmd` shim:

```powershell
$env:APPDATA\npm\azurite.cmd --silent --location .\.azurite
```

## Configuration

### Development

Development configuration is stored in:

```text
src/LoyaltyCloud.API/appsettings.Development.json
src/LoyaltyCloud.Admin/appsettings.Development.json
```

Local development currently uses:

```text
ConnectionStrings:DefaultConnection = Server=(localdb)\MSSQLLocalDB;Database=KBeautyLoyalty;Trusted_Connection=True;TrustServerCertificate=True;
Azure:BlobStorage:ConnectionString = UseDevelopmentStorage=true
Azure:KeyVaultUri = ""
Admin:Auth:Username = owner
Admin:Auth:Password = dev-password
```

### Production

Production must use Azure Key Vault and real credentials/certificates. Do not commit real secrets.

Required secrets include:

| Secret / Config | Purpose |
| --- | --- |
| `ConnectionStrings--DefaultConnection` | SQL Server / Azure SQL connection string. |
| `Azure--BlobStorage--ConnectionString` | Azure Blob Storage connection string for `.pkpass` files. |
| `Admin--Auth--Password` | Admin password override. |
| Apple pass certificate secret | `.p12` certificate payload or configured certificate material. |
| Apple pass certificate password | Password for the `.p12`. |
| APN private key / key id / team id | Push updates for Apple Wallet passes. |

## How to Run Locally

### 1. Restore and Build

```powershell
dotnet restore .\LoyaltyCloud.sln
dotnet build .\LoyaltyCloud.sln -v minimal
```

### 2. Start Azurite

```powershell
$env:APPDATA\npm\azurite.cmd --silent --location .\.azurite
```

Azurite must be running when local pass uploads are tested because development storage uses `UseDevelopmentStorage=true`.

### 3. Apply Database Migrations

```powershell
dotnet ef database update `
  --project src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

### 4. Start API

```powershell
dotnet run `
  --project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj `
  --launch-profile LoyaltyCloud.API
```

Open:

```text
http://localhost:55131/swagger/index.html
```

### 5. Start Admin

In a second terminal:

```powershell
dotnet run `
  --project src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj `
  --launch-profile LoyaltyCloud.Admin
```

Open:

```text
http://localhost:55130/login
```

Login options in Development:

| Method | Details |
| --- | --- |
| Normal login | `owner / dev-password` |
| Development login | Click `Login for Dev` on the login page. |

## Correct Startup Flow

Recommended local startup order:

```text
1. Start Azurite
2. Apply migrations if needed
3. Start API
4. Start Admin
5. Open Admin and use Login for Dev
```

API and Admin can run simultaneously. Admin does not call API over HTTP for its dashboard; it uses Application + Infrastructure in-process. API is still needed for external REST/API workflows and Swagger validation.

Admin runs the Development demo seeder on startup when `ASPNETCORE_ENVIRONMENT=Development`.

## Development-Only Features

These features are intentionally restricted to Development.

| Feature | Where | Safety boundary |
| --- | --- | --- |
| `Login for Dev` button | `LoyaltyCloud.Admin/Pages/Login.razor` | Rendered only when `IHostEnvironment.IsDevelopment()` is true. |
| `/admin/dev-login` endpoint | `LoyaltyCloud.Admin/Program.cs` | Endpoint is mapped only inside `if (app.Environment.IsDevelopment())`. |
| Mock `.pkpass` generator | `DevelopmentPassGeneratorService` | Registered only in Development through DI. Non-dev uses real `PassGeneratorService`. |
| Demo data seeding | `DevelopmentDataSeeder` | Runs only from Admin in Development and skips EF InMemory provider. |
| Azurite storage | `UseDevelopmentStorage=true` | Local-only storage emulator. Production must use Azure Blob Storage. |

### Development Login

The dev login endpoint signs the same cookie scheme used by normal Admin auth and redirects to `/dashboard`.

```text
POST /admin/dev-login -> 302 /dashboard
```

It avoids Blazor interactive login problems because it is a normal HTTP POST endpoint and works with Static SSR.

### Development Pass Generation

In Development, `IPassGeneratorService` resolves to `DevelopmentPassGeneratorService`.

It creates an unsigned but valid ZIP-shaped `.pkpass` containing:

```text
pass.json
manifest.json
README-dev.txt
icon.png
icon@2x.png
icon@3x.png
```

This keeps registration, storage upload, URLs, dashboard flows and local journeys working without Key Vault or Apple certificates. It is not valid for Apple Wallet production import.

### Demo Seeding

`DevelopmentDataSeeder` creates or ensures:

| Data | Count |
| --- | --- |
| Demo customers | 20 |
| Demo loyalty cards | 20 |
| Demo rewards | 5 |
| Demo point transactions | 100+ |
| Demo redemptions | 9: 3 pending, 3 confirmed, 3 cancelled |
| Program config keys | 14 canonical keys |

Idempotency rules:

- Uses sentinel email `demo.customer.001@kbeauty.local`.
- Uses reward names as stable demo identifiers.
- Checks existing demo redemptions before creating them.
- Runs inside a SQL transaction via EF Core execution strategy.
- Skips `Microsoft.EntityFrameworkCore.InMemory` so integration tests stay isolated.

The real domain currently supports only these levels:

```text
Mist
Glow
Radiance
```

Do not seed or use extra level names unless they are first added to `LoyaltyConstants.Levels` and domain logic.

## Database and Migrations

### Apply Migrations

```powershell
dotnet ef database update `
  --project src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

### Create a Migration

```powershell
dotnet ef migrations add <MigrationName> `
  --project src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj `
  --output-dir Persistence\Migrations
```

### Program Config Seed

`ProgramConfigSeed` seeds canonical configuration keys through EF model seeding. These keys are used by `ProgramConfigSnapshot`.

Important keys:

| Key | Meaning |
| --- | --- |
| `points_per_peso_unit` | Purchase amount to points conversion. |
| `welcome_bonus_points` | Registration bonus. |
| `referral_bonus_points` | Referral bonus. |
| `birthday_multiplier` | Birthday month multiplier. |
| `level_mist_min` | Mist threshold. |
| `level_glow_min` | Glow threshold. |
| `level_radiance_min` | Radiance threshold. |
| `radiance_requalification_points` | Annual Radiance requalification. |
| `reward_*_points` | Configurable reward point costs. |

## Build and Test Commands

```powershell
dotnet build .\LoyaltyCloud.sln -v minimal
dotnet test .\LoyaltyCloud.sln -v minimal --no-build
```

Useful run commands:

```powershell
dotnet run --project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj --launch-profile LoyaltyCloud.API
dotnet run --project src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj --launch-profile LoyaltyCloud.Admin
```

## Troubleshooting

### DLL or EXE Locked During Build

Symptoms:

```text
MSB3027
MSB3021
The process cannot access the file ... because it is being used by another process.
```

Cause: API or Admin is still running and has locked the compiled executable.

Fix:

```powershell
netstat -ano | Select-String ':55128|:55131|:55129|:55130'
Stop-Process -Id <pid> -Force
dotnet build .\LoyaltyCloud.sln -v minimal
```

### Login Redirect Loop / Long ReturnUrl

Symptoms:

```text
/login?ReturnUrl=/login?ReturnUrl=...
414 URI Too Long
```

Cause: login page was not anonymous or SSR redirect used client navigation at the wrong time.

Fix in current code:

- `Login.razor` has `[AllowAnonymous]`.
- Login uses Static SSR.
- Successful login redirects through `HttpContext.Response.Redirect(...)`.

### Static Blazor Assets Return HTML

Symptoms:

```text
/_framework/blazor.web.js returns text/html
SyntaxError: Unexpected token '<'
Blazor interactivity does not start
```

Cause: fallback authorization challenged framework/static asset endpoints and returned login HTML.

Fix in current code:

```csharp
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();
```

Page access remains protected by `[Authorize]` and `AuthorizeRouteView`.

### Missing LocalDB Database

Symptoms:

```text
Cannot open database 'KBeautyLoyalty' requested by the login.
```

Fix:

```powershell
dotnet ef database update `
  --project src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj `
  --startup-project src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

### Azurite Missing or Not Running

Symptoms:

```text
Pass upload fails
UseDevelopmentStorage=true cannot connect
```

Fix:

```powershell
$env:APPDATA\npm\azurite.cmd --silent --location .\.azurite
```

Validate ports:

```powershell
netstat -ano | Select-String ':10000|:10001|:10002'
```

### Key Vault Missing in Development

Development can run with:

```json
"Azure": {
  "KeyVaultUri": ""
}
```

This is safe locally because Development DI uses `DevelopmentPassGeneratorService` for `.pkpass` generation. Production must configure Key Vault and real Apple/Azure secrets.

### EF Core Retry Strategy and Manual Transactions

Symptoms:

```text
The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated transactions.
```

Fix: wrap manual transactions inside:

```csharp
var strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    ...
    await tx.CommitAsync();
});
```

`DevelopmentDataSeeder` already follows this pattern.

### Development Seeder Duplicates

The seeder is idempotent. If duplicated demo data appears, check whether data was manually copied or the sentinel email was modified:

```text
demo.customer.001@kbeauty.local
```

Admin startup logs should show either inserted counts or all zeros:

```text
Development demo seed completed. Config: 0, Rewards: 0, Customers: 0, Transactions: 0, Redemptions: 0.
```

## Apple Wallet Production Notes

Production `.pkpass` generation requires:

- Apple Pass Type ID.
- `.p12` Pass certificate.
- Certificate password.
- APN `.p8` private key.
- APN Key ID.
- Apple Team ID.
- Azure Key Vault access.
- Azure Blob Storage connection string.

Do not rely on `DevelopmentPassGeneratorService` outside local development.

## Recent Development Improvements

- Added `DevelopmentPassGeneratorService` for local unsigned `.pkpass` ZIP generation.
- Registered mock pass generation only in Development.
- Added `Login for Dev` button and `/admin/dev-login` endpoint.
- Added `DevelopmentDataSeeder` with 20 customers, rewards, transactions and redemptions.
- Fixed Blazor static asset authorization issue where `blazor.web.js` returned HTML.
- Fixed Static SSR login redirect behavior and login loop risk.
- Confirmed Admin/API local ports: API `55131/55128`, Admin `55130/55129`.
- Confirmed Azurite local storage ports: `10000/10001/10002`.

## Maintenance Rule

`README.md` is the official technical and operational context for this solution.

Whenever code changes affect architecture, startup, ports, configuration, auth, services, mocks, seeds, endpoints, dependencies, troubleshooting, build, test or runtime behavior, update this README in the same change.

Do not leave behavior documented here that no longer matches the implementation.

## License

LoyaltyCloud MVP for KBeauty MX.
