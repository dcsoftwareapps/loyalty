# Apple Wallet

## Arquitectura

La implementacion de Apple Wallet vive principalmente en:

- `KBeauty.Loyalty.API`
  - `PassesController`
  - `ApplePassAuthMiddleware`
- `KBeauty.Loyalty.Application`
  - comandos/queries de devices
  - `RegisterCustomerHandler`
  - `AddPointsHandler`
- `KBeauty.Loyalty.Infrastructure`
  - `PassGeneratorService`
  - `DevelopmentPassGeneratorService`
  - `ApnService`
  - `BlobStorageService`
  - `LocalAppleWalletSecretsProvider`
  - `KeyVaultAppleWalletSecretsProvider`

El backend genera pases `.pkpass` reales con `PassGeneratorService`. Este servicio construye `pass.json`, carga assets, genera `manifest.json`, firma el manifest con PKCS#7 detached y empaqueta todo en un ZIP `.pkpass`.

La aplicacion usa Clean Architecture. Application depende de interfaces (`IPassGeneratorService`, `IApnService`, `IStorageService`) y Infrastructure provee las implementaciones concretas para Apple Wallet, APNs, Key Vault y Blob Storage.

## Estado actual

Apple Wallet esta completamente funcional para el flujo actual del proyecto:

- Generacion de `.pkpass` real firmado.
- Instalacion del pass en iPhone.
- Registro del dispositivo en el Web Service de Apple Wallet.
- APNs para despertar Wallet.
- Actualizacion automatica despues de compras/acumulacion de puntos.
- Actualizacion automatica despues de canjes.

El mecanismo unico para informar cambios al Web Service de Apple Wallet es `LoyaltyCard.LastActivityAt`. Las acumulaciones de puntos lo actualizan mediante `LoyaltyCard.EarnPoints()`. Los canjes tambien lo actualizan mediante `LoyaltyCard.Touch(...)` despues de `LoyaltyCard.RedeemPoints(...)`.

## Flujo desde Customer hasta Wallet

1. El cliente se registra con `POST /api/customers`.
2. `CustomersController` envia `RegisterCustomerCommand` a MediatR.
3. `RegisterCustomerHandler` crea `Customer` y `LoyaltyCard`.
4. Se aplica el bono de bienvenida segun `ProgramConfigSnapshot`.
5. Se guarda la transaccion en SQL Server mediante `AppDbContext`.
6. `RegisterCustomerHandler` llama `IPassGeneratorService.GeneratePassAsync`.
7. En modo real, DI resuelve `PassGeneratorService`.
8. `PassGeneratorService` genera el `.pkpass` firmado.
9. `RegisterCustomerHandler` sube el pase a Blob Storage mediante `IStorageService.UploadPassAsync`.
10. La respuesta incluye `SerialNumber`, `PassDownloadUrl`, `CurrentPoints` y `Level`.
11. En desarrollo tambien existe `GET /api/dev/passes/{serialNumber}` para descargar un pass real directamente desde el iPhone.
12. Al instalar el pass, Apple Wallet llama el endpoint de registro de device.
13. Cuando cambian puntos o se realiza un canje, el caso de uso actualiza `LastActivityAt` y envia APNs best-effort a los devices registrados.
14. Wallet recibe el push, consulta seriales actualizados y despues descarga el pass actualizado.

## Controllers involucrados

### `CustomersController`

- `POST /api/customers`
  - Crea cliente y tarjeta.
  - Genera y sube el `.pkpass`.
  - Devuelve URL de descarga cuando el upload fue exitoso.

### `PointsController`

- `POST /api/points`
  - Acredita puntos.
  - Recalcula nivel.
  - Dispara push APNs a Wallet si hay device registrations.

### `PassesController`

Expone los endpoints requeridos por Apple Wallet:

- `GET /v1/passes/{passTypeIdentifier}/{serialNumber}`
- `GET /api/dev/passes/{serialNumber}`
- `POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}`
- `DELETE /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}`
- `GET /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}`
- `POST /v1/log`

## Services involucrados

### `PassGeneratorService`

Genera pases reales firmados.

Hace lo siguiente:

- Valida configuracion requerida de Apple.
- Construye `pass.json`.
- Carga assets desde `Assets/AppleWallet`.
- Calcula SHA-1 de `pass.json` y assets.
- Genera `manifest.json`.
- Firma `manifest.json` con PKCS#7 detached.
- Incluye certificado Pass Type ID y certificado Apple WWDR G4.
- Agrega atributo S/MIME `signingTime`.
- Empaqueta `pass.json`, `manifest.json`, `signature` y assets en `.pkpass`.

### `DevelopmentPassGeneratorService`

Genera un `.pkpass` mock/no firmado para Development cuando `Wallet:UseRealPassSigning` es `false`.

No sirve para instalar pases reales en iPhone.

### `ApnService`

Envia push background a APNs usando HTTP/2.

- Genera JWT ES256 con la llave `.p8`.
- Usa `Apple:PassTypeIdentifier` como `apns-topic`.
- Envia cuerpo `{}`.
- No lanza excepciones hacia el caso de uso; el push es best-effort.

### `BlobStorageService`

Sube `.pkpass` a Azure Blob Storage o Azurite.

- Usa `Azure:BlobStorage:ConnectionString`.
- Usa `Azure:BlobStorage:PassContainer`.
- Genera SAS URL si el cliente puede generar SAS.

### Secrets providers

`IAppleWalletSecretsProvider` abstrae la lectura de secretos Apple.

`LocalAppleWalletSecretsProvider`:

- Lee `.p12` desde `Apple:PassCertificatePath`.
- Lee password desde `Apple:PassCertificatePassword`.
- Lee `.p8` desde `Apple:ApnPrivateKeyPath`.
- Lee APN Key ID desde `Apple:ApnKeyId`.
- Lee Team ID desde `Apple:TeamIdentifier`.
- Puede leer WWDR desde `Apple:WwdrCertificatePath`.

`KeyVaultAppleWalletSecretsProvider`:

- Lee secretos desde Azure Key Vault:
  - `kbeauty-pass-certificate`
  - `kbeauty-pass-certificate-password`
  - `kbeauty-wwdr-certificate`
  - `kbeauty-apn-private-key`
  - `kbeauty-apn-key-id`
  - `kbeauty-apn-team-id`

## Middleware

`ApplePassAuthMiddleware` protege rutas Apple Wallet.

Rutas con serial concreto requieren:

```text
Authorization: ApplePass <authenticationToken>
```

Aplica a:

- `GET /v1/passes/{passTypeIdentifier}/{serialNumber}`
- `POST /v1/devices/{device}/registrations/{passType}/{serial}`
- `DELETE /v1/devices/{device}/registrations/{passType}/{serial}`

El endpoint agrupado:

```text
GET /v1/devices/{device}/registrations/{passType}
```

permite `Authorization` ausente porque Apple puede consultar seriales actualizados sin token de un pass especifico. Si llega Authorization mal formado, responde `401`. Si llega token valido pero no matchea una card registrada, registra warning y deja pasar la request para no bloquear a Wallet.

El middleware valida `passTypeIdentifier` contra configuracion. Si no coincide, responde `404`.

## Pass Type

El pass actual usa `storeCard`.

Se usa `storeCard` porque Apple lo define para tarjetas de tienda, tarjetas de puntos, tarjetas de regalo y tarjetas de lealtad. KBeauty Loyalty es una tarjeta de lealtad por puntos, por lo que `storeCard` es el tipo semantico correcto.

No se usa `generic` porque es mas apropiado para tarjetas generales/identificacion y no representa especificamente un programa de lealtad por puntos.

No se usa `coupon` porque semanticamente representa un cupon/oferta, no una membresia permanente de lealtad.

## Assets utilizados

El pass actual incluye:

- `icon.png` `29x29`
- `icon@2x.png` `58x58`
- `icon@3x.png` `87x87`
- `logo.png` `160x50`
- `logo@2x.png` `320x100`
- `logo@3x.png` `480x150`

`PassGeneratorService` valida que existan y que sus dimensiones sean las esperadas. Todos se incluyen en `manifest.json` y quedan cubiertos por la firma.

`storeCard` admite `logo`, `icon` y `strip`. El proyecto probo `strip.png`, pero actualmente no lo utiliza.

Assets que no utiliza el `storeCard` actual:

- `strip.png`
- `strip@2x.png`
- `strip@3x.png`
- `thumbnail.png`
- `footer.png`
- `background.png`

`thumbnail`, `footer` y `background` no forman parte del set actual del pase KBeauty. `footer` aplica a otros estilos como boarding pass, y `thumbnail/background` no se estan usando en esta implementacion.

## Diseno

El diseno actual aprobado esta implementado en `PassGeneratorService.BuildPassJson`.

### Frente

Tipo:

```text
storeCard
```

Valores base:

- `backgroundColor`: `rgb(250,248,244)`
- `foregroundColor`: `rgb(28,28,28)`
- `labelColor`: `rgb(132,124,120)`

Contenido:

- Logo de K-Beauty mediante `logo.png`.
- Nombre corto del cliente en `primaryFields`.
- `secondaryFields` vacio.
- Tres `auxiliaryFields`:
  - `PUNTOS`: ejemplo `290 pts`
  - `NIVEL`: `Mist ✨`, `Glow ✨` o `Radiance ✨`
  - `PRÓXIMO`: `Glow`, `Radiance` o `Máximo ✨`
- QR funcional.
- Texto del QR: `Presenta este código en caja`.

El nombre visible en el frente se calcula con `GetWalletDisplayName(Customer customer)`:

- Si hay un solo nombre, usa ese nombre.
- Si hay varios nombres/apellidos, usa el primer nombre.
- Si viene vacio, usa `Cliente K-Beauty`.

### Reverso

`backFields` actuales:

1. `Beneficios`

```text
• Acumula puntos en cada compra.

• Desbloquea recompensas exclusivas.

• Accede a beneficios según tu nivel.
```

2. `Progreso`

```text
Nivel actual
Mist ✨

Próximo nivel
Glow

Puntos restantes
710 pts
```

Para Radiance:

```text
Nivel actual
Radiance ✨

Próximo nivel
⭐ Máximo

Puntos restantes
0 pts
```

3. Contacto sin titulo visible (`label = string.Empty`)

```text
@kbeauty_mx

kbeautymx.com

+52 646 238 6962
```

No existe campo final tipo `K-Beauty / Tu piel merece recompensas`.

## Limitaciones reales descubiertas

- Apple Wallet controla el layout final del frente.
- No se pueden fijar manualmente tamanos de fuente.
- No se pueden controlar posiciones exactas de campos.
- `primaryFields` puede crecer visualmente mas de lo deseado.
- Apple Wallet no respeto de forma util el salto de linea intentado dentro de `primaryFields`.
- El salto de linea en `auxiliaryFields` no es una base confiable para diseno fino.
- `storeCard` no provee un divisor visual nativo entre campos y QR.
- `strip.png` se puede incluir en `storeCard`, pero Apple decide su render/recorte/posicion y no fue util para el layout final aprobado.
- No vale la pena intentar microajustes visuales desde `pass.json`; la mejora visual debe venir principalmente de buenos assets y pocos campos.

## APNs

Wallet registra el device cuando instala el pass:

```text
POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
```

El body contiene:

```json
{
  "pushToken": "<token>"
}
```

`RegisterDeviceHandler` guarda o actualiza un `DeviceRegistration`.

Cuando se agregan puntos:

1. `POST /api/points` llega a `PointsController`.
2. `AddPointsHandler` aplica puntos con `LoyaltyCard.EarnPoints`.
3. `EarnPoints` actualiza `LastActivityAt`.
4. Se guarda el cambio.
5. El handler busca devices registrados por serial.
6. Llama `IApnService.SendPassUpdateAsync` por cada push token.
7. `ApnService` manda push background a APNs.
8. Wallet consulta seriales actualizados.
9. Wallet descarga el pass actualizado.

Cuando se realiza un canje:

1. `POST /api/redemptions` llega a `RedemptionsController`.
2. `RedeemRewardHandler` descuenta puntos con `LoyaltyCard.RedeemPoints`.
3. Se crea `Redemption` en estado `Pending`.
4. Se crea `PointTransaction` negativa.
5. El handler llama `LoyaltyCard.Touch(...)` para actualizar `LastActivityAt`.
6. Se guarda el cambio.
7. El handler busca devices registrados por serial.
8. Llama `IApnService.SendPassUpdateAsync` por cada push token.
9. `ApnService` manda push background a APNs.
10. Wallet consulta seriales actualizados.
11. Wallet descarga el pass actualizado.

Flujo resumido de refresh por canje:

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

El push APNs no contiene datos del pass. Solo despierta a Wallet para que vuelva a consultar el web service.

## Endpoints Wallet

### Descargar pass actualizado

```text
GET /v1/passes/{passTypeIdentifier}/{serialNumber}
```

Uso:

- Lo llama Apple Wallet al instalar o refrescar.
- Requiere `Authorization: ApplePass <token>`.
- Devuelve `application/vnd.apple.pkpass`.
- Regenera el pass desde `LoyaltyCard` + `Customer`.

### Descargar pass en Development

```text
GET /api/dev/passes/{serialNumber}
```

Uso:

- Solo responde en `Development`.
- Permite descargar un `.pkpass` desde navegador/iPhone.
- No es un endpoint Apple oficial.
- Devuelve `application/vnd.apple.pkpass`.

### Registrar device

```text
POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
```

Uso:

- Lo llama Wallet cuando se agrega el pass.
- Requiere `Authorization: ApplePass <token>`.
- Body: `{ "pushToken": "..." }`.
- Devuelve `201 Created` si es nuevo.
- Devuelve `200 OK` si ya existia.

### Eliminar device registration

```text
DELETE /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
```

Uso:

- Lo llama Wallet cuando el usuario elimina el pass.
- Requiere `Authorization: ApplePass <token>`.
- Es idempotente.
- Devuelve `200 OK`.

### Consultar seriales actualizados

```text
GET /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}?passesUpdatedSince={lastUpdated}
```

Uso:

- Lo llama Wallet despues de un APN push.
- `Authorization` puede estar ausente.
- Devuelve `204 No Content` si no hay seriales actualizados.
- Devuelve `200 OK` con:

```json
{
  "serialNumbers": ["KB-..."],
  "lastUpdated": "..."
}
```

### Logs de Wallet

```text
POST /v1/log
```

Uso:

- Recibe diagnosticos enviados por Apple Wallet.
- Sanitiza saltos de linea.
- Trunca mensajes largos.
- Devuelve `200 OK`.

## Flujo completo

```text
Customer
  ↓
POST /api/customers
  ↓
Customer + LoyaltyCard
  ↓
PassGeneratorService
  ↓
.pkpass firmado
  ↓
Blob Storage / endpoint dev
  ↓
Wallet
  ↓
Device Registration
  ↓
POST /api/points o POST /api/redemptions
  ↓
APNs
  ↓
GET /v1/devices/{device}/registrations/{passType}
  ↓
GET /v1/passes/{passType}/{serial}
  ↓
Wallet actualizado
```

En ambos casos, la deteccion de cambios para Apple se basa en `LoyaltyCard.LastActivityAt`: compras/acumulaciones lo actualizan en `EarnPoints`; canjes lo actualizan con `Touch` despues de descontar puntos.
