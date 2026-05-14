# KBeauty Loyalty

Sistema de lealtad por puntos para KBeauty MX — tienda de cosméticos coreanos en Ensenada, México. Backend .NET 9, panel administrativo Blazor Server, integración con Apple Wallet (PKPass + APN) y SQL Server en Azure.

> Mist · Glow · Radiance — niveles editables desde el panel admin, todo el programa es configurable desde DB. Cero hardcoding.

---

## 📁 Diagrama de la solución

```
KBeauty.Loyalty.sln
│
├── src/
│   │
│   ├── KBeauty.Loyalty.Common ──────── utilidades puras
│   │     · Result<T> / PagedResult / PaginationParams
│   │     · DateTimeExtensions / StringExtensions
│   │     · LoyaltyConstants (claves del programa)
│   │     · IDateTimeProvider / ICurrentUserService
│   │
│   ├── KBeauty.Loyalty.Domain ──────── ← Common
│   │     · Entidades: Customer, LoyaltyCard, PointTransaction,
│   │                  Redemption, RewardCatalogItem,
│   │                  ProgramConfig, DeviceRegistration
│   │     · Value objects: Points, Money, MemberLevel,
│   │                      ProgramConfigSnapshot
│   │     · Domain events + excepciones de dominio
│   │     · Interfaces de repositorio (incluye IUnitOfWork)
│   │
│   ├── KBeauty.Loyalty.Application ── ← Domain, Common
│   │     · CQRS con MediatR (Commands + Queries + Handlers)
│   │     · Validators (FluentValidation)
│   │     · Pipeline behaviors: Validation, Logging
│   │     · Interfaces: IPassGeneratorService, IApnService,
│   │                   IStorageService, IDashboardReadService
│   │
│   ├── KBeauty.Loyalty.Infrastructure ← Application, Domain, Common
│   │     · EF Core 9 + SQL Server (AppDbContext = IUnitOfWork)
│   │     · 7 EntityTypeConfigurations Fluent API
│   │     · 7 repositorios + seed de ProgramConfig
│   │     · PassGeneratorService (.pkpass + PKCS#7 signing)
│   │     · ApnService (HTTP/2 + JWT ES256 a Apple)
│   │     · BlobStorageService, KeyVault integration
│   │     · DashboardReadService, CustomerListReadService
│   │
│   ├── KBeauty.Loyalty.API ─────────── ← Application, Infrastructure
│   │     · ASP.NET Core Web API
│   │     · 6 controllers: Points, Customers, Redemptions,
│   │                      Admin, Config, Passes (Apple)
│   │     · Middleware: ApplePassAuth, GlobalExceptionHandler
│   │     · OpenAPI/Swagger en dev
│   │
│   └── KBeauty.Loyalty.Admin ────────── ← Application, Infrastructure
│         · Blazor Server (.NET 9 Blazor Web App)
│         · Páginas: Dashboard, Scan (mobile-first), Customers,
│                    CustomerDetail, Redemptions, Config, Login
│         · Auth cookie básica (usuario en appsettings)
│         · Sistema de diseño KBeauty: Cormorant Garamond + DM Sans
│
└── tests/
    └── KBeauty.Loyalty.Tests ───────── xUnit + Moq
          · Domain: LoyaltyCard + Points VO (~19 tests)
          · Application: AddPoints, RegisterCustomer, RedeemReward (~14)
          · Integration: WebApplicationFactory + InMemory DB (~4)
```

### Reglas de dependencia (todas verificadas por compilación)

```
Common         → ∅
Domain         → Common
Application    → Domain, Common
Infrastructure → Application, Domain, Common
API            → Application, Infrastructure, Common
Admin          → Application, Infrastructure, Common
Tests          → todos
```

---

## 🚀 Setup local paso a paso

### Requisitos previos

- **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **SQL Server LocalDB** (incluido con Visual Studio o SQL Express)
- **Azurite** para emular Azure Blob Storage en local
  ```powershell
  npm install -g azurite
  azurite --silent --location ./.azurite
  ```
- (Opcional) Visual Studio 2022 / Rider / VS Code con C# Dev Kit

### 1. Clonar y restaurar paquetes

```powershell
git clone <repo-url> "C:\repos\K-Beauty - AI"
cd "C:\repos\K-Beauty - AI"
dotnet restore
dotnet build
```

### 2. Crear la base de datos local

Connection string ya está configurada en `appsettings.Development.json` apuntando a LocalDB. Crear la DB y aplicar la migración inicial:

```powershell
# Generar la migración inicial (solo la primera vez)
dotnet ef migrations add Initial `
  -p src/KBeauty.Loyalty.Infrastructure `
  -s src/KBeauty.Loyalty.API `
  -o Persistence/Migrations

# Aplicar a la DB local
dotnet ef database update `
  -p src/KBeauty.Loyalty.Infrastructure `
  -s src/KBeauty.Loyalty.API
```

El seed de `ProgramConfig` (14 claves con sus defaults) se aplica automáticamente en la migración inicial.

### 3. Arrancar la API

```powershell
dotnet run --project src/KBeauty.Loyalty.API
# https://localhost:7000
# Swagger UI en https://localhost:7000/swagger
```

### 4. Arrancar el panel admin

En otra terminal:

```powershell
dotnet run --project src/KBeauty.Loyalty.Admin
# https://localhost:7001
```

**Login local:** `owner` / `dev-password` (configurado en `appsettings.Development.json` del proyecto Admin).

### 5. Probar la API

Abrir `src/KBeauty.Loyalty.API/KBeauty.Loyalty.API.http` en VS Code (con la extensión REST Client) o Rider, ajustar `@host` si hace falta, y ejecutar las requests en orden.

---

## 🔑 Variables requeridas en Azure Key Vault (producción)

El sistema lee secrets de Key Vault automáticamente cuando `Azure:KeyVaultUri` está configurado en `appsettings.json` (puede venir de App Service Configuration o env var). Para deploy a producción crear estos secrets:

| Secret | Para qué | Ejemplo |
|---|---|---|
| `ConnectionStrings--DefaultConnection` | SQL Azure | `Server=tcp:kbeauty.database.windows.net,1433;...` |
| `kbeauty-pass-certificate` | Base64 del `.p12` del Pass Type ID | (binario base64-encoded) |
| `kbeauty-pass-certificate-password` | Password del `.p12` | `<password>` |
| `kbeauty-apn-private-key` | Contenido PEM del `.p8` de APN | `-----BEGIN PRIVATE KEY-----\n...` |
| `kbeauty-apn-key-id` | Key ID (10 chars) que asocia el `.p8` | `ABC123DEF4` |
| `kbeauty-apn-team-id` | Team ID (10 chars) de Apple Developer | `TEAM123ABC` |
| `Azure--BlobStorage--ConnectionString` | Connection string al storage de los `.pkpass` | `DefaultEndpointsProtocol=https;...` |
| `Admin--Auth--Password` | Password del panel admin (override) | `<strong-password>` |

> La convención `--` en el nombre del secret se traduce a `:` en `IConfiguration` (estándar de .NET).

### Cargar el certificado .p12 a Key Vault

```powershell
# Convertir .p12 a base64
$bytes = [IO.File]::ReadAllBytes("kbeauty-pass.p12")
$b64 = [Convert]::ToBase64String($bytes)

# Subir a Key Vault
az keyvault secret set `
  --vault-name kbeauty-kv `
  --name kbeauty-pass-certificate `
  --value $b64

az keyvault secret set `
  --vault-name kbeauty-kv `
  --name kbeauty-pass-certificate-password `
  --value "<password>"
```

### Permisos del Managed Identity de App Service

El App Service de la API y el Admin necesitan los permisos `Get` y `List` sobre los secrets:

```powershell
az keyvault set-policy `
  --name kbeauty-kv `
  --object-id <managed-identity-object-id> `
  --secret-permissions get list
```

---

## 🍎 Cómo crear el Pass Type ID en Apple Developer

1. **Inscribirse a Apple Developer Program** ($99 USD/año si aún no lo están).
2. En [developer.apple.com](https://developer.apple.com), ir a **Certificates, Identifiers & Profiles → Identifiers**.
3. Crear un nuevo Identifier:
   - Tipo: **Pass Type IDs**
   - Description: `KBeauty MX Loyalty Card`
   - Identifier: `pass.com.kbeautymx.loyalty` (este valor va en `Apple:PassTypeIdentifier` de `appsettings.json`)
4. Crear el **Pass Type ID Certificate**:
   - En el identifier creado, **Create Certificate**
   - Subir un CSR generado en Keychain Access (Mac) o vía OpenSSL en Windows:
     ```powershell
     openssl req -new -newkey rsa:2048 -nodes `
       -keyout pass.key -out pass.csr `
       -subj "/CN=KBeauty Loyalty/O=KBeauty MX/C=MX"
     ```
   - Descargar el `.cer` generado.
   - Exportar a `.p12` con la llave privada:
     ```powershell
     openssl x509 -inform DER -in pass.cer -out pass.pem
     openssl pkcs12 -export `
       -inkey pass.key -in pass.pem `
       -out kbeauty-pass.p12 `
       -name "KBeauty Pass" `
       -password pass:<password>
     ```
5. **Crear la llave APN** (push para passes):
   - En **Keys**, crear nueva key.
   - Habilitar **Apple Push Notifications service (APNs)**.
   - Descargar el `.p8` (¡SE DESCARGA UNA SOLA VEZ!).
   - Anotar el **Key ID** (10 chars) y tu **Team ID** (en la esquina del portal).
6. Subir a Key Vault siguiendo las instrucciones de la sección anterior.

### Reemplazar los iconos placeholder

El `PassGeneratorService` usa un PNG 1×1 transparente como placeholder. Para producción, agrega los iconos reales al servicio:

- `icon.png` — 29×29 px
- `icon@2x.png` — 58×58 px
- `icon@3x.png` — 87×87 px
- (opcional) `logo.png`, `logo@2x.png`, `strip.png`

**Opción A — Embed como recursos del assembly**

1. Coloca los PNG en `src/KBeauty.Loyalty.Infrastructure/Assets/Pass/`
2. En el `.csproj`:
   ```xml
   <ItemGroup>
     <EmbeddedResource Include="Assets\Pass\*.png" />
   </ItemGroup>
   ```
3. En `PassGeneratorService` reemplaza `PlaceholderIconPng` por una carga del recurso:
   ```csharp
   private static byte[] LoadIcon(string name)
   {
       using var stream = typeof(PassGeneratorService).Assembly
           .GetManifestResourceStream($"KBeauty.Loyalty.Infrastructure.Assets.Pass.{name}");
       using var ms = new MemoryStream();
       stream!.CopyTo(ms);
       return ms.ToArray();
   }
   ```

**Opción B — Subir a Blob Storage** (mejor para CDN y rotación sin redeploy)

Tener un container `pass-assets/` con los iconos. El servicio descarga al iniciar y los cachea en memoria.

---

## 🧪 Cómo correr los tests

```powershell
# Todos los tests (Domain + Application + Integration)
dotnet test

# Solo unitarios (rápido — sin levantar WebApplicationFactory)
dotnet test --filter "FullyQualifiedName!~Integration"

# Solo Domain
dotnet test --filter "FullyQualifiedName~Domain"

# Con cobertura (genera archivo en TestResults/)
dotnet test --collect:"XPlat Code Coverage"

# Reporte HTML de cobertura (necesita reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator `
  -reports:"tests/KBeauty.Loyalty.Tests/TestResults/*/coverage.cobertura.xml" `
  -targetdir:coverage `
  -reporttypes:Html
# Abre coverage/index.html
```

### Qué cubre cada layer de tests

- **Domain (`Domain/`)** — invariantes puras de `LoyaltyCard` (EarnPoints, RedeemPoints, NeedsLevelRequalification) y `Points` VO.
- **Application (`Application/`)** — handlers aislados con Moq. Verifica que `Result.Fail` se devuelve en flujos esperados (no encontrado, validación, saldo insuficiente, nivel no elegible) y que servicios externos (APN, storage) se llaman cuando corresponde.
- **Integration (`Integration/`)** — flujo end-to-end via HTTP: `POST /api/customers` → `GET /api/customers/{serial}` → `POST /api/points` → `GET /api/redemptions/catalog/{serial}` → `POST /api/redemptions`. Usa `WebApplicationFactory<Program>` con `InMemoryDatabase` y fakes para Pass/APN/Storage.

---

## ⚙️ Reglas del programa (todas configurables desde DB)

Estas son las 14 claves seedeadas inicialmente. **Toda** la lógica del programa lee de aquí — para cambiar el ratio, los umbrales o el costo de un canje, edita en `/config` del panel admin o actualiza la fila en `ProgramConfigs`.

| Clave | Default | Para qué |
|---|---|---|
| `points_per_peso_unit` | `10` | $10 MXN = 1 punto |
| `welcome_bonus_points` | `50` | Bono al registrarse |
| `referral_bonus_points` | `150` | Bono al referir una nueva clienta |
| `birthday_multiplier` | `2` | Multiplicador en mes de cumpleaños |
| `level_mist_min` | `0` | Umbral inicio Mist |
| `level_glow_min` | `1000` | Umbral inicio Glow |
| `level_radiance_min` | `3000` | Umbral inicio Radiance |
| `radiance_requalification_points` | `500` | Puntos anuales para mantener Radiance |
| `reward_mini_product_points` | `300` | Costo: mini producto (Mist+) |
| `reward_fifty_off_points` | `500` | Costo: $50 off (Mist+) |
| `reward_focusskin_points` | `400` | Costo: análisis FocusSkin (Glow+) |
| `reward_monthly_product_points` | `700` | Costo: producto del mes (Glow+) |
| `reward_hundred_off_cabina_points` | `800` | Costo: $100 off cabina (Glow+) |
| `reward_facial_off_points` | `1200` | Costo: $300 off facial (Radiance) |

> Las claves nuevas solo se aceptan si están registradas en `LoyaltyConstants.ConfigKeys` (validación por reflexión en `UpdateProgramConfigHandler`). Para agregar una nueva, primero agrégala al constants file.

---

## 📋 Operaciones comunes

### Sumar puntos manualmente desde tienda

1. Abrir `/scan` en el panel admin (responsivo — funciona en iPad).
2. Pegar o tipear el serial de la clienta (escaneado del QR de su pase).
3. Aparece la clienta con su nivel, puntos y badge 🎂 si cumple este mes.
4. Tipear monto de la compra → preview de puntos.
5. Confirmar.

### Confirmar un canje

1. La clienta inicia el canje desde su lado (o el operador desde `/customers/{id}` → "Iniciar canje").
2. El canje queda en `Pending` y los puntos se descuentan del saldo.
3. En `/redemptions` del panel, el operador entrega el beneficio físicamente y presiona **Confirmar entrega**.

### Cambiar las reglas del programa

1. Ir a `/config` en el panel admin.
2. Editar el valor de la fila correspondiente.
3. **Guardar cambios** → aplica de inmediato a la siguiente operación.

### Re-cualificación anual de Radiance

El método `LoyaltyCard.NeedsLevelRequalification(dt)` evalúa si una tarjeta Radiance no ganó al menos 500 pts en su año vigente. **El reset anual del contador `PointsEarnedThisYear` no está en el MVP** — habrá que correr un job (Azure Function timer o webjob) que:

```sql
UPDATE LoyaltyCards
SET PointsEarnedThisYear = 0
WHERE Level = 'Radiance'
  AND LevelAchievedAt < DATEADD(year, -1, GETUTCDATE());
```

Ejecutar 1× al día. El método del dominio luego marca las que no alcanzaron el mínimo para downgrade manual o automático.

---

## 🏗️ Decisiones de arquitectura

| Decisión | Por qué |
|---|---|
| **Clean Architecture** estricta | Domain no conoce EF; Application no conoce HTTP; el resto se invierte |
| **MediatR + CQRS** | Cada caso de uso aislado, fácil de testear con Moq |
| **`Result<T>` en handlers** | Sin excepciones para flujos esperados — los logs quedan limpios |
| **`DomainException` solo para invariantes** | Si `RedeemPoints` lanza, hubo un bug — el validator debió detectarlo antes |
| **`ProgramConfigSnapshot`** (value object) | Pasar 1 entidad Key/Value al dominio no aporta valor; un snapshot tipado sí |
| **`AppDbContext` = `IUnitOfWork`** | Un solo punto de commit; los repos solo manipulan ChangeTracker |
| **Domain events post-commit** | Si `SaveChanges` falla, los eventos NO se publican — consistencia |
| **Admin habla con Application in-process** (no HTTP a la API) | Menos auth, menos red, menos latencia — la API queda libre para integraciones externas |
| **Static SSR en `/login`** | Es la única forma de poder setear cookie con `SignInAsync` en Blazor Web App |
| **Iconos del pase como placeholder** | El binario PNG real depende de assets de marca — documentado para reemplazar |

---

## 🔒 Notas de seguridad

- **El `.p12` y el `.p8` NUNCA viven en el repositorio.** Solo en Key Vault.
- **`appsettings.Development.json` no debe contener credenciales reales** — usar `dotnet user-secrets` para overrides locales.
- **El password del admin en producción** debe estar en Key Vault como `Admin--Auth--Password`, no en `appsettings.json`.
- **CORS está cerrado por origins** — agregar el dominio del admin en `Cors:AllowedOrigins`.
- **La API `/api/*` no tiene auth en el MVP.** Antes de exponer a internet: agregar App Service Authentication o API key middleware. Los `/v1/*` (Apple) sí tienen su propio middleware de auth contra `LoyaltyCard.AuthenticationToken`.

---

## 📚 Stack completo

| Capa | Tecnología |
|---|---|
| Runtime | .NET 9 |
| Web API | ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| DB | Azure SQL Database (LocalDB en dev) |
| Hosting | Azure App Service |
| Secrets | Azure Key Vault |
| Storage | Azure Blob Storage (Azurite en dev) |
| CQRS | MediatR 12 |
| Validación | FluentValidation 11 |
| UI Admin | Blazor Server (Blazor Web App de .NET 9) |
| Tests | xUnit 2.9 + Moq 4.20 |
| Integration tests | `WebApplicationFactory<Program>` + `Microsoft.EntityFrameworkCore.InMemory` |
| Apple Wallet | `System.Security.Cryptography.Pkcs.SignedCms` (built-in .NET) |
| APN | HttpClient HTTP/2 + JWT ES256 (`ECDsa.ImportFromPem`) |

---

## 🤝 Contribuir

1. Crear una rama desde `main`.
2. Asegurarse que `dotnet test` pasa.
3. PR con descripción del cambio + referencias al spec si aplica.

---

## 📄 Licencia

© KBeauty MX. Uso interno.
