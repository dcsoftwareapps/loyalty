# Google Wallet

This document describes the first vertical slice of Google Wallet support in KBeauty Loyalty.

The implementation is intentionally minimal and disabled by default. It prepares the system to create Google Wallet Loyalty Cards when valid Google Wallet issuer credentials are provided, while keeping Apple Wallet unchanged.

## Architecture

```text
API
  -> CreateGoogleWalletSaveLinkCommand
  -> IGoogleWalletService
      -> IMemberWalletDataService
      -> IMemberDigitalWalletRepository
      -> GoogleWalletIdGenerator
      -> GoogleWalletObjectMapper
      -> IGoogleWalletClient
      -> GoogleWalletJwtFactory
  -> SQL Server / Google Wallet API
```

Google Wallet is implemented as a separate provider from Apple Wallet.

Apple still uses:

- `.pkpass`
- `PassGeneratorService`
- `ApplePassAuthMiddleware`
- `DeviceRegistration`
- APNs

Google uses:

- `MemberWalletData`
- `MemberDigitalWallet`
- `LoyaltyClass`
- `LoyaltyObject`
- signed Save to Google Wallet JWT URL
- REST calls through `IGoogleWalletClient`

## Components

| Component | File | Responsibility |
| --- | --- | --- |
| `MemberWalletData` | `src/LoyaltyCloud.Application/Common/Wallet/MemberWalletData.cs` | Provider-neutral wallet projection. |
| `IMemberWalletDataService` | `src/LoyaltyCloud.Application/Common/Interfaces/IMemberWalletDataService.cs` | Builds wallet data from current customer/card state. |
| `IGoogleWalletService` | `src/LoyaltyCloud.Application/Common/Interfaces/IGoogleWalletService.cs` | Application-facing Google Wallet coordinator contract. |
| `MemberDigitalWallet` | `src/LoyaltyCloud.Domain/Entities/MemberDigitalWallet.cs` | Persists local link to an external wallet object. |
| `IMemberDigitalWalletRepository` | `src/LoyaltyCloud.Domain/Repositories/IMemberDigitalWalletRepository.cs` | Repository contract for wallet provider links. |
| `GoogleWalletOptions` | `src/LoyaltyCloud.Infrastructure/Configuration/GoogleWalletOptions.cs` | Strongly typed Google Wallet configuration. |
| `GoogleWalletIdGenerator` | `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleWalletIdGenerator.cs` | Deterministic Class ID and Object ID generation. |
| `GoogleWalletObjectMapper` | `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleWalletObjectMapper.cs` | Pure mapper to Google class/object payloads. |
| `GoogleWalletJwtFactory` | `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleWalletJwtFactory.cs` | RS256 JWT creation for OAuth assertion and save URL. |
| `GoogleWalletClient` | `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleWalletClient.cs` | Centralized Google Wallet REST client. |
| `GoogleWalletService` | `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleWalletService.cs` | Coordinates projection, persistence, Google calls and save link creation. |
| `WalletsController` | `src/LoyaltyCloud.API/Controllers/WalletsController.cs` | Exposes the save-link endpoint. |

## Save-Link Flow

```text
POST /api/customers/{serialNumber}/wallets/google/save-link
  -> CreateGoogleWalletSaveLinkCommand
  -> GoogleWalletService.GetOrCreateSaveLinkAsync
  -> MemberWalletDataService.GetBySerialNumberAsync
  -> find/create MemberDigitalWallet
  -> generate deterministic class/object IDs
  -> ensure LoyaltyClass
  -> create/update LoyaltyObject
  -> persist sync status
  -> sign Save to Google Wallet JWT
  -> return save URL
```

Response:

```json
{
  "saveUrl": "https://pay.google.com/gp/v/save/{jwt}",
  "objectId": "issuer.member-kb-...",
  "classId": "issuer.loyalty",
  "lastSynchronizedAt": "2026-07-15T00:00:00Z"
}
```

The JWT is not stored in the database and is not logged.

## Public Join Flow

`/{tenantSlug}/join` is the customer-facing entry point. The page first creates
or reuses the membership through the existing public join endpoint, then chooses
the wallet path using the returned `SerialNumber`.

```text
Blazor Join page
  -> POST /api/public/{tenantSlug}/join
  -> PublicJoinResponse.SerialNumber
  -> WalletPlatformDetector
      -> Apple: navigate to PassDownloadUrl
      -> Google: POST /api/customers/{serialNumber}/wallets/google/save-link
      -> Unknown/Desktop: show manual choice
```

Important behavior:

- the join form creates or reuses the customer once;
- Apple Wallet continues to use the existing `.pkpass` `PassDownloadUrl`;
- Android uses the Google save-link endpoint and then redirects to `saveUrl`;
- desktop and unknown browsers show manual Add to Apple Wallet / Add to Google Wallet buttons;
- when `GoogleWallet:Enabled=false`, the public join response marks Google as unavailable and the UI hides the Google option;
- the browser never receives service account credentials, private keys or configuration internals;
- the full Google Save JWT must not be logged.

Platform detection is intentionally conservative. It reads browser `userAgent`,
`platform`, vendor and touch-point hints through `wwwroot/js/wallet-platform.js`,
then classifies in testable C# code. iPhone, iPad, iPod and iPadOS desktop-mode
Safari with touch are Apple. Android is Google. Everything else is unknown and
falls back to manual choice.

## Update Flow

The first implemented business update is `AddPoints`.

```text
POST /api/points
  -> AddPointsHandler
  -> update LoyaltyCard + PointTransaction + PointLot
  -> SaveChanges
  -> Apple APN best-effort
  -> GoogleWalletService.SynchronizeBySerialNumberIfExistsAsync
```

The Google sync call:

- does nothing when `GoogleWallet:Enabled=false`;
- does nothing when the member has no `MemberDigitalWallet` row yet;
- updates the `LoyaltyObject` when a Google wallet link exists;
- records `LastSynchronizedAt` on success;
- records `LastSynchronizationError` on failure;
- never rolls back the points transaction.

This is a simple post-commit best-effort call. A future outbox should be added before high-volume production use.

## Persistence

Migration:

```text
src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20260715175457_AddMemberDigitalWallets.cs
```

Table:

```text
MemberDigitalWallets
```

Important constraints:

- unique `(LoyaltyCardId, Provider)`;
- unique `(Provider, ExternalObjectId)`;
- index `(CustomerId, Provider)`;
- index `(Provider, Status)`.

The table stores provider IDs and sync status only. It does not store JWTs, service account JSON, private keys or customer PII beyond local foreign keys.

## Configuration

Google Wallet is disabled by default in all committed appsettings files.

```json
{
  "GoogleWallet": {
    "Enabled": false,
    "IssuerId": "",
    "ClassSuffix": "loyalty",
    "ObjectIdPrefix": "member",
    "ProgramName": "KBeauty Loyalty",
    "IssuerName": "KBeauty MX",
    "LogoUri": "",
    "HeroImageUri": "",
    "HexBackgroundColor": "#FFFFFF",
    "Origins": []
  }
}
```

Sensitive values must come from user-secrets, environment variables or Azure Key Vault:

| Key | Sensitive | Description |
| --- | --- | --- |
| `GoogleWallet:ServiceAccountJson` | yes | Full Google service account JSON. |
| `GoogleWallet:ServiceAccountJsonPath` | yes-ish | Local path outside the repo to the service account JSON. |
| `GoogleWallet:LogoUri` | no | Public HTTPS logo used as `programLogo`; required by Google when creating the `LoyaltyClass`. |

When `GoogleWallet:Enabled=true`, startup validates:

- `IssuerId`
- `ClassSuffix`
- `ObjectIdPrefix`
- `ProgramName`
- `IssuerName`
- service account JSON or path
- `HexBackgroundColor` format when provided

Google Wallet also rejects real `LoyaltyClass` creation when `programLogo` is
missing. Set `GoogleWallet:LogoUri` to a publicly reachable HTTPS PNG/JPG before
the first real save-link test. Localhost, private files and Azurite development
URLs are not valid because Google must fetch the image from its backend.

## Local Development

Keep Google Wallet disabled until real issuer data exists:

```powershell
dotnet user-secrets set "GoogleWallet:Enabled" "true" --project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
dotnet user-secrets set "GoogleWallet:IssuerId" "<issuer-id>" --project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
dotnet user-secrets set "GoogleWallet:LogoUri" "https://<public-api-host>/api/wallet-assets/apple/logo@3x.png" --project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
dotnet user-secrets set "GoogleWallet:ServiceAccountJsonPath" "C:\secure\google-wallet-service-account.json" --project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

Use a path outside the repository. Do not commit the JSON key.

The API serves the existing bundled Apple Wallet logo through:

```text
GET /api/wallet-assets/apple/logo@3x.png
```

Use the public HTTPS API host, for example the current ngrok host, when setting
`GoogleWallet:LogoUri`. Do not point Google Wallet at local files, `localhost` or
Azurite development URLs.

## Azure Key Vault

Recommended secrets:

```text
GoogleWallet--IssuerId
GoogleWallet--ServiceAccountJson
GoogleWallet--ClassSuffix
GoogleWallet--ObjectIdPrefix
GoogleWallet--ProgramName
GoogleWallet--IssuerName
GoogleWallet--LogoUri
GoogleWallet--HeroImageUri
GoogleWallet--HexBackgroundColor
```

Production should prefer `GoogleWallet--ServiceAccountJson` from Key Vault and Managed Identity access to Key Vault.

## IDs

`GoogleWalletIdGenerator` builds deterministic IDs:

```text
Class ID:  {issuerId}.{classSuffix}
Object ID: {issuerId}.{objectIdPrefix}-{serialNumber}
```

Values are normalized to avoid spaces and unsupported characters. Object IDs are based on the loyalty serial, not customer name or email.

## Testing

Automated tests do not call Google Wallet.

Coverage added:

- ID generation.
- Google class/object mapping.
- Save URL JWT shape.
- `MemberWalletData` projection.
- Save-link endpoint.
- `MemberDigitalWallet` persistence/idempotency.
- AddPoints post-commit Google sync.

Integration tests use:

- `FakeGoogleWalletClient`
- `FakeGoogleWalletCredentialsProvider`
- EF InMemory
- generated in-memory RSA key

## Security

Current limitation: the API does not have global authentication for `/api/*` endpoints. The Google save-link endpoint follows the current API routing style but must not be exposed publicly in production until endpoint authentication/authorization is added.

Do not log:

- service account JSON;
- private key;
- access token;
- save JWT;
- raw Google credential payloads.

## Troubleshooting

### Google Wallet Disabled

Symptom:

```text
Google Wallet esta deshabilitado.
```

Fix: set `GoogleWallet:Enabled=true` and provide all required config through user-secrets or Key Vault.

### Missing Credentials

Symptom:

```text
Faltan credenciales Google Wallet.
```

Fix: configure either `GoogleWallet:ServiceAccountJson` or `GoogleWallet:ServiceAccountJsonPath`.

### Invalid Issuer ID

Symptom:

```text
GoogleWallet:IssuerId es requerido.
```

Fix: obtain the Issuer ID from Google Pay & Wallet Console and configure it securely.

### Missing Loyalty Class Logo

Symptom:

```text
LoyaltyClass cannot be created without a program logo.
```

Fix: configure `GoogleWallet:LogoUri` with a public HTTPS image URL reachable by
Google, then retry the save-link endpoint.

### Save Link Works in Tests But Not Android

Automated tests only validate internal behavior and JWT shape. Android validation requires:

- real Issuer ID;
- service account added as Developer in Google Pay & Wallet Console;
- enabled Google Wallet API;
- valid LoyaltyClass;
- Android device with Google Wallet;
- account allowed for test/pre-launch.

## Human Actions Pending

- Create or access Google Wallet Issuer Account.
- Obtain Issuer ID.
- Create/select Google Cloud Project.
- Enable Google Wallet API.
- Create Service Account.
- Add Service Account as Developer in Google Pay & Wallet Console.
- Create service account credentials and store them outside the repo.
- Store production credentials in Azure Key Vault.
- Provide public HTTPS logo and optional hero image.
- Prepare Android test device and Google test account.
- Complete Google production approval before public launch.

## Current Limitations

- Implemented but not tested against Google real credentials.
- No Android real-device validation yet.
- No outbox/retry worker yet.
- Only AddPoints triggers Google sync in this first slice.
- No Admin UI button yet.
- No production API authentication was added.
- No Smart Tap/NFC.
- No advanced Google notification messages.

