# Desarrollo local

Esta guia describe el estado actual para probar Apple Wallet real en local con `.p12`, `.p8`, ngrok, Azurite y SQL LocalDB.

## Modo local real

En `Development`, el proyecto puede correr con pases reales firmados si:

```json
"Wallet": {
  "UseRealPassSigning": true
}
```

Con esa configuracion, Dependency Injection registra:

- `IAppleWalletSecretsProvider` -> `LocalAppleWalletSecretsProvider`
- `IPassGeneratorService` -> `PassGeneratorService`
- `IApnService` -> `ApnService`

No requiere `Azure:KeyVaultUri`.

Si `Wallet:UseRealPassSigning` es `false` en Development:

- `IPassGeneratorService` -> `DevelopmentPassGeneratorService`
- `IApnService` -> `NoOpApnService`

Ese modo genera `.pkpass` mock/no firmados y no sirve para instalar pases reales en iPhone.

Fuera de Development, el proyecto usa:

- `IAppleWalletSecretsProvider` -> `KeyVaultAppleWalletSecretsProvider`
- `IPassGeneratorService` -> `PassGeneratorService`
- `IApnService` -> `ApnService`
- `Azure:KeyVaultUri` obligatorio

## Certificados locales

Para modo local real se usan archivos locales:

```text
C:\Secrets\KBeauty\kbeauty-wallet-pass.p12
C:\Secrets\KBeauty\AuthKey_68Z2745848.p8
```

El certificado `.p12` es el certificado Pass Type ID para:

```text
pass.com.kbeautymx.loyalty
```

La llave `.p8` se usa para APNs.

El certificado WWDR G4 se carga desde el bundle del proyecto si existe:

```text
Certificates/AppleWWDRCAG4.cer
```

Tambien puede configurarse por `Apple:WwdrCertificatePath`.

## User-secrets

Configurar los secrets en el proyecto API:

```powershell
cd C:\repos\Loyalty\loyalty

dotnet user-secrets set "Apple:PassCertificatePath" "C:\Secrets\KBeauty\kbeauty-wallet-pass.p12" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:PassCertificatePassword" "KBeautyWallet2026!" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:ApnPrivateKeyPath" "C:\Secrets\KBeauty\AuthKey_68Z2745848.p8" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:ApnKeyId" "68Z2745848" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:TeamIdentifier" "HS2XCFGQ75" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:PassTypeIdentifier" "pass.com.kbeautymx.loyalty" --project src\KBeauty.Loyalty.API
dotnet user-secrets set "Apple:WebServiceURL" "https://goosepimply-quiana-unrudely.ngrok-free.dev" --project src\KBeauty.Loyalty.API
```

Valores no sensibles actuales en `src/KBeauty.Loyalty.API/appsettings.Development.json`:

```json
{
  "Wallet": {
    "UseRealPassSigning": true
  },
  "Apple": {
    "PassTypeIdentifier": "pass.com.kbeautymx.loyalty",
    "TeamIdentifier": "HS2XCFGQ75",
    "ApnKeyId": "68Z2745848",
    "WebServiceURL": "https://goosepimply-quiana-unrudely.ngrok-free.dev",
    "OrganizationName": "KBeauty MX",
    "ApnHost": "https://api.push.apple.com"
  }
}
```

## SQL LocalDB

Development usa:

```text
Server=(localdb)\MSSQLLocalDB;Database=KBeautyLoyalty;Trusted_Connection=True;TrustServerCertificate=True;
```

Aplicar migraciones cuando sea necesario:

```powershell
cd C:\repos\Loyalty\loyalty

dotnet ef database update `
  --project src\KBeauty.Loyalty.Infrastructure\KBeauty.Loyalty.Infrastructure.csproj `
  --startup-project src\KBeauty.Loyalty.API\KBeauty.Loyalty.API.csproj
```

## Azurite

Azure Blob Storage local se emula con Azurite.

Comando correcto en PowerShell:

```powershell
cd C:\repos\Loyalty\loyalty
& "$env:APPDATA\npm\azurite.cmd" --silent --location .\.azurite
```

Azurite es necesario para el flujo `POST /api/customers` cuando se sube el `.pkpass` generado a Blob Storage local con:

```text
Azure:BlobStorage:ConnectionString = UseDevelopmentStorage=true
```

El endpoint `GET /api/dev/passes/{serialNumber}` genera el pass directamente y no depende del blob previamente subido.

## ngrok

Apple Wallet necesita una URL HTTPS publica para llamar los endpoints del web service.

Comando actual:

```powershell
ngrok http --url=goosepimply-quiana-unrudely.ngrok-free.dev https://localhost:55128
```

El valor de `Apple:WebServiceURL` debe coincidir con la URL publica:

```text
https://goosepimply-quiana-unrudely.ngrok-free.dev
```

`pass.json` incluye ese valor en `webServiceURL`. Si no coincide con ngrok, Wallet no podra registrar device ni refrescar.

## Levantar el proyecto

Terminal 1: Azurite

```powershell
cd C:\repos\Loyalty\loyalty
& "$env:APPDATA\npm\azurite.cmd" --silent --location .\.azurite
```

Terminal 2: API

```powershell
cd C:\repos\Loyalty\loyalty
dotnet run --project src\KBeauty.Loyalty.API\KBeauty.Loyalty.API.csproj --launch-profile KBeauty.Loyalty.API
```

La API usa HTTPS local:

```text
https://localhost:55128
```

Terminal 3: ngrok

```powershell
ngrok http --url=goosepimply-quiana-unrudely.ngrok-free.dev https://localhost:55128
```

## Crear un Customer

Endpoint:

```text
POST /api/customers
```

El controller envia `RegisterCustomerCommand`.

El handler:

- valida email unico
- crea `Customer`
- crea `LoyaltyCard`
- aplica bono de bienvenida
- guarda cambios
- genera `.pkpass`
- sube a Blob Storage
- devuelve `PassDownloadUrl`

El shape exacto del request esta definido por `RegisterCustomerCommand`.

## Reward Catalog API

Fase 2.1 agrega CRUD API administrativo para `RewardCatalogItem`.

`RewardCatalogItem` es la fuente de verdad del catalogo de recompensas administrable.

Endpoints disponibles:

- `GET /api/rewards`: lista recompensas para administracion, activas e inactivas. Soporta `activeOnly`, `includeExpired` y `minLevel`.
- `GET /api/rewards/{id}`: obtiene el detalle de una recompensa.
- `POST /api/rewards`: crea una recompensa.
- `PUT /api/rewards/{id}`: edita una recompensa existente.
- `PUT /api/rewards/{id}/activate`: activa una recompensa.
- `PUT /api/rewards/{id}/deactivate`: desactiva una recompensa.

El flujo implementado es:

```text
RewardCatalogItem
  ↓
Application (CQRS)
  ↓
API
  ↓
Admin (pendiente)
```

Estado Fase 2.1:

- ✅ API CRUD de recompensas.
- ✅ Commands.
- ✅ Queries.
- ✅ Validators.
- ✅ DTOs.
- ✅ Repository actualizado.

## Descargar un pass

Para pruebas directas en iPhone durante Development:

```text
GET https://goosepimply-quiana-unrudely.ngrok-free.dev/api/dev/passes/{serialNumber}
```

Ejemplo:

```text
https://goosepimply-quiana-unrudely.ngrok-free.dev/api/dev/passes/KB-XXXXX
```

Este endpoint:

- solo funciona en `Development`
- busca `LoyaltyCard` por serial
- busca `Customer`
- genera `.pkpass` real con `PassGeneratorService`
- responde `application/vnd.apple.pkpass`

## Instalarlo en iPhone

1. Abrir la URL de descarga de ngrok en Safari.
2. iOS descarga el `.pkpass`.
3. Wallet muestra la vista de agregar.
4. Tocar `Add`.
5. Wallet llama el endpoint de registro de device.

Si el pass ya estaba instalado y se quiere revisar diseno visual, eliminar el pass anterior de Wallet e instalarlo de nuevo.

## Probar APNs

Requisitos:

- Pass instalado en iPhone.
- Device registrado por Wallet.
- `DeviceRegistration` persistido en SQL.
- `Apple:ApnPrivateKeyPath` apunta al `.p8`.
- `Apple:ApnKeyId` configurado.
- `Apple:TeamIdentifier` configurado.
- `Apple:PassTypeIdentifier` configurado.
- `Apple:ApnHost` configurado como `https://api.push.apple.com`.

El flujo APNs se dispara cuando se agregan puntos o cuando se realiza un canje:

```text
POST /api/points
POST /api/redemptions
```

`AddPointsHandler`:

1. Busca la card por serial.
2. Calcula puntos.
3. Aplica `LoyaltyCard.EarnPoints`.
4. `EarnPoints` actualiza `LastActivityAt`.
5. Guarda cambios.
6. Busca registrations del serial.
7. Llama `IApnService.SendPassUpdateAsync`.
8. `ApnService` envia push HTTP/2 a APNs.

`RedeemRewardHandler`:

1. Busca la card por serial.
2. Valida recompensa, vigencia, nivel y saldo.
3. Aplica `LoyaltyCard.RedeemPoints`.
4. Crea `Redemption` en estado `Pending`.
5. Crea `PointTransaction` negativa.
6. Llama `LoyaltyCard.Touch(...)` para actualizar `LastActivityAt`.
7. Guarda cambios.
8. Busca registrations del serial.
9. Llama `IApnService.SendPassUpdateAsync`.
10. `ApnService` envia push HTTP/2 a APNs.

El push es best-effort: si APNs falla, la transaccion de puntos o canje no se revierte.

## Probar actualizacion automatica

Flujo esperado:

```text
POST /api/points o POST /api/redemptions
  ↓
APNs 200
  ↓
Wallet llama GET /v1/devices/{device}/registrations/pass.com.kbeautymx.loyalty
  ↓
API devuelve serialNumbers y lastUpdated
  ↓
Wallet llama GET /v1/passes/pass.com.kbeautymx.loyalty/{serial}
  ↓
API devuelve .pkpass actualizado
  ↓
Wallet actualiza el pass
```

Para canjes, el flujo completo es:

```text
Canje
  ↓
Redemption Pending
  ↓
PointTransaction negativa
  ↓
Actualizacion de LastActivityAt
  ↓
APNs
  ↓
Apple solicita seriales modificados
  ↓
Apple descarga el nuevo pass
  ↓
Wallet se actualiza automaticamente
```

Si Wallet llama:

```text
GET /v1/devices/{device}/registrations/{passType}
```

sin `Authorization`, la API lo acepta. Esto fue necesario porque Apple puede consultar ese endpoint agrupado sin token especifico de un pass.

El mecanismo unico para detectar cambios es `LoyaltyCard.LastActivityAt`; compras y canjes lo actualizan antes de enviar APNs.

## Problemas conocidos

### LocalAppleWalletSecretsProvider

Problema resuelto: el proyecto necesitaba probar Wallet real en local sin Azure Key Vault.

Estado actual:

- `LocalAppleWalletSecretsProvider` lee `.p12` y `.p8` desde rutas locales configuradas.
- Se activa en Development con `Wallet:UseRealPassSigning=true`.

### Key Vault solo en produccion

Problema resuelto: antes, el generador real dependia de `SecretClient`/Key Vault y fallaba localmente si `Azure:KeyVaultUri` estaba vacio.

Estado actual:

- En Development + `Wallet:UseRealPassSigning=true`, no se requiere Key Vault.
- Fuera de Development, `Azure:KeyVaultUri` es obligatorio para Wallet real.

### DeviceRegistration

Wallet necesita registrar el device antes de recibir pushes.

Estado actual:

- `POST /v1/devices/{device}/registrations/{passType}/{serial}` guarda o actualiza `DeviceRegistration`.
- `DELETE /v1/devices/{device}/registrations/{passType}/{serial}` elimina la registration de forma idempotente.

### Middleware Authentication

Problema resuelto: el middleware rechazaba con `401` el endpoint agrupado de seriales:

```text
GET /v1/devices/{device}/registrations/{passType}
```

Estado actual:

- Rutas con serial requieren `Authorization: ApplePass <token>`.
- La consulta agrupada acepta Authorization ausente.
- Authorization mal formado sigue devolviendo `401`.

### Apple sin Authorization en GET registrations

Apple Wallet puede llamar:

```text
GET /v1/devices/{device}/registrations/{passType}
```

sin Authorization. Esto ahora se acepta para permitir el refresh despues de APNs.

### Wallet refresh despues de canjes

Problema resuelto: los canjes enviaban APNs, pero necesitaban marcar la tarjeta como modificada para que Apple detectara el pass como actualizado.

Estado actual:

- Las compras actualizan `LastActivityAt` mediante `LoyaltyCard.EarnPoints`.
- Los canjes actualizan `LastActivityAt` mediante `LoyaltyCard.Touch(...)` despues de descontar puntos.
- Apple Wallet puede refrescar automaticamente despues de compras y canjes.

### Limitaciones visuales de Wallet

Durante el diseno se comprobo:

- Apple controla layout y tamanos.
- No hay control fino de tipografia.
- Los saltos de linea en campos del frente no son confiables.
- `strip.png` puede alterar el layout y no fue parte del diseno final.
- Microajustar `pass.json` no reemplaza buenos assets.

### storeCard

Se mantiene `storeCard`.

Motivo:

- Es el tipo correcto para tarjetas de lealtad/puntos.
- `generic` no describe tan bien el caso de uso.
- `coupon` comunica descuento/cupon, no membresia.

### Endpoint de descarga dev

Para pruebas locales se usa:

```text
GET /api/dev/passes/{serialNumber}
```

El metodo `GetPassDownloadUrlAsync` de los generadores devuelve actualmente una ruta `/api/customers/{serialNumber}/pass`, pero el endpoint real de descarga directa implementado para Development es `/api/dev/passes/{serialNumber}`.
