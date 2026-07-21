# Apple Wallet

## Arquitectura

La implementacion de Apple Wallet vive principalmente en:

- `LoyaltyCloud.API`
  - `PassesController`
  - `ApplePassAuthMiddleware`
- `LoyaltyCloud.Application`
  - comandos/queries de devices
  - `RegisterCustomerHandler`
  - `AddPointsHandler`
- `LoyaltyCloud.Infrastructure`
  - `PassGeneratorService`
  - `DevelopmentPassGeneratorService`
  - `ApnService`
  - `BlobStorageService`
  - `LocalAppleWalletSecretsProvider`
  - `KeyVaultAppleWalletSecretsProvider`

El backend genera pases `.pkpass` reales con `PassGeneratorService`. Este servicio construye `pass.json`, carga assets, genera `manifest.json`, firma el manifest con PKCS#7 detached y empaqueta todo en un ZIP `.pkpass`.

La aplicacion usa Clean Architecture. Application depende de interfaces (`IPassGeneratorService`, `IApnService`, `IStorageService`) y Infrastructure provee las implementaciones concretas para Apple Wallet, APNs, Key Vault y Blob Storage.

El catalogo de recompensas se administra con la entidad existente `RewardCatalogItem`. Para Fase 2.2, el flujo de administracion queda:

```text
Admin UI
  ↓
MediatR
  ↓
Commands / Queries
  ↓
Repository
  ↓
SQL Server
```

El CRUD existe a nivel API mediante Commands, Queries, Validators, DTOs y repositorio. Tambien existe una pantalla administrativa en `/rewards` que usa MediatR in-process desde `LoyaltyCloud.Admin`.

## Estado actual

Apple Wallet esta completamente funcional para el flujo actual del proyecto:

- Generacion de `.pkpass` real firmado.
- Instalacion del pass en iPhone.
- Registro del dispositivo en el Web Service de Apple Wallet.
- APNs para despertar Wallet.
- Actualizacion automatica despues de compras/acumulacion de puntos.
- Actualizacion automatica despues de canjes.

Fase 2.1:

- ✅ API CRUD de recompensas.
- ✅ Commands.
- ✅ Queries.
- ✅ Validators.
- ✅ DTOs.
- ✅ Repository actualizado.

Fase 2.2:

- ✅ Administracion visual de recompensas.
- ✅ Crear.
- ✅ Editar.
- ✅ Activar.
- ✅ Desactivar.

Fase 2.3:

- ✅ Cancelacion de canjes.
- ✅ Restauracion automatica de puntos.
- ✅ Reversa de transacciones.
- ✅ Actualizacion automatica de Wallet despues de cancelar.

Fase 2.4:

- ✅ Historial de canjes.
- ✅ Filtros por estado.
- ✅ Vista de fechas de solicitud/resolucion.
- ✅ Vista de operador y notas.

Fase 2.5:

- ✅ Pulido visual y UX del Admin.
- ✅ Buscadores simples.
- ✅ Filtros visuales.
- ✅ Badges de estado/nivel.
- ✅ Tablas mas limpias.

Fase 3.1:

- ✅ Dashboard / Analytics.
- ✅ KPIs de clientes, puntos, canjes y recompensas.
- ✅ Actividad reciente.
- ✅ Lecturas agregadas con `IDashboardReadService`.

Fase 3.2:

- ✅ Customer Detail.
- ✅ Informacion general, Loyalty/Wallet y estadisticas.
- ✅ Historial de puntos.
- ✅ Historial de canjes.
- ✅ Lectura agregada con `ICustomerDetailReadService`.

Fase 3.3:

- ✅ Expiracion de puntos con FIFO.
- ✅ `PointLot` y `PointLotConsumption`.
- ✅ Canjes consumen lotes FIFO.
- ✅ Cancelaciones restauran lotes originales.
- ✅ `POST /api/admin/points/expire`.
- ✅ Wallet refresh despues de expiraciones.

El mecanismo unico para informar cambios al Web Service de Apple Wallet es `LoyaltyCard.LastActivityAt`. Las acumulaciones de puntos lo actualizan mediante `LoyaltyCard.EarnPoints()`. Los canjes tambien lo actualizan mediante `LoyaltyCard.Touch(...)` despues de `LoyaltyCard.RedeemPoints(...)`. Las expiraciones lo actualizan mediante `LoyaltyCard.ExpirePoints(...)`.

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

### `RewardsController`

Expone el CRUD administrativo de `RewardCatalogItem`:

- `GET /api/rewards`
- `GET /api/rewards/{id}`
- `POST /api/rewards`
- `PUT /api/rewards/{id}`
- `PUT /api/rewards/{id}/activate`
- `PUT /api/rewards/{id}/deactivate`

El controller usa MediatR y no accede directamente a `AppDbContext`.

# Fase 2.1 - Reward Catalog API

El sistema cuenta con un CRUD administrativo para `RewardCatalogItem`.

`RewardCatalogItem` es la fuente de verdad actual del catalogo de recompensas para:

- `Name`
- `Description`
- `PointsCost`
- `MinLevel`
- `IsActive`
- `IsMonthlyProduct`
- `ValidFrom`
- `ValidTo`

Los costos del catalogo administrable viven en `RewardCatalogItem.PointsCost`; no dependen de las claves legacy `reward_*_points` de `ProgramConfig`.

## Endpoints

### `GET /api/rewards`

Lista las recompensas para administracion.

Incluye recompensas activas e inactivas. Soporta filtros por query string:

- `activeOnly`
- `includeExpired`
- `minLevel`

Devuelve una lista de `RewardAdminDto`.

### `GET /api/rewards/{id}`

Obtiene el detalle de una recompensa por id.

Devuelve `404 Not Found` si la recompensa no existe.

### `POST /api/rewards`

Crea una recompensa.

Valida:

- `Name` obligatorio.
- `Description` obligatorio.
- `PointsCost` mayor a `0`.
- `MinLevel` valido: `Mist`, `Glow` o `Radiance`.
- `ValidTo` no menor que `ValidFrom`.

Devuelve `201 Created` con `RewardAdminDto`.

### `PUT /api/rewards/{id}`

Edita una recompensa existente.

Actualiza:

- `Name`
- `Description`
- `PointsCost`
- `MinLevel`
- `IsActive`
- `IsMonthlyProduct`
- `ValidFrom`
- `ValidTo`

Devuelve `404 Not Found` si la recompensa no existe.

### `PUT /api/rewards/{id}/activate`

Activa una recompensa existente.

Devuelve `RewardAdminDto`.

### `PUT /api/rewards/{id}/deactivate`

Desactiva una recompensa existente.

Devuelve `RewardAdminDto`.

No existe DELETE fisico para recompensas.

# Fase 2.2 - Reward Administration

El proyecto cuenta con una interfaz administrativa para `RewardCatalogItem` en `LoyaltyCloud.Admin`.

La pagina disponible es:

```text
/rewards
```

Desde esta pantalla ya es posible:

- Listar recompensas.
- Crear recompensas.
- Editar recompensas.
- Activar recompensas.
- Desactivar recompensas.

La pagina muestra una tabla con:

- Nombre.
- Descripcion.
- Puntos.
- Nivel minimo.
- Producto del mes.
- Vigencia.
- Estado.
- Acciones.

El boton `Nueva recompensa` abre un formulario inline con:

- Nombre.
- Descripcion.
- Puntos requeridos.
- Nivel minimo.
- Producto del mes.
- Activa.
- Vigente desde.
- Vigente hasta.

La pantalla usa MediatR directamente y reutiliza los casos de uso de Fase 2.1:

- `ListRewardsQuery`
- `CreateRewardCommand`
- `UpdateRewardCommand`
- `ActivateRewardCommand`
- `DeactivateRewardCommand`

No accede directo a `AppDbContext`.

Flujo de administracion:

```text
Admin UI
  ↓
MediatR
  ↓
Commands / Queries
  ↓
Repository
  ↓
SQL Server
```

La pantalla de Canjes (`/redemptions`) permite:

- Ver todos los canjes.
- Filtrar por pendientes.
- Filtrar por confirmados.
- Filtrar por cancelados.
- Confirmar canjes pendientes.
- Cancelar canjes pendientes.
- Ver fecha de solicitud.
- Ver fecha de resolucion.
- Ver operador.
- Ver notas.
- Registrar una nota opcional de cancelacion.

# Fase 2.3 - Cancelación de Canjes

El sistema permite cancelar canjes pendientes desde Admin.

Flujo completo:

```text
Cliente solicita canje
  ↓
Se descuentan puntos
  ↓
Se crea PointTransaction negativa
  ↓
Redemption queda Pending
  ↓
Admin puede confirmar o cancelar
```

Si se confirma:

- El canje queda completado.
- No hay cambios adicionales en puntos.

Si se cancela:

- `Redemption` cambia a `Cancelled`.
- Se restauran los puntos del cliente.
- Se crea una `PointTransaction` positiva de reversa.
- Se actualiza `LastActivityAt`.
- Se envia APNs.
- Apple Wallet refresca automaticamente.

Flujo tecnico:

```text
Admin
  ↓
CancelRedemptionCommand
  ↓
Domain
  ↓
RestorePoints
  ↓
PointTransaction (Reversal)
  ↓
LastActivityAt
  ↓
APNs
  ↓
Apple Wallet
```

La restauracion usa `LoyaltyCard.RestorePoints(...)`. Este metodo suma saldo disponible y actualiza `LastActivityAt`, pero no aumenta `LifetimePoints` ni `PointsEarnedThisYear`.

La transaccion positiva de reversa queda registrada como `TransactionType.RedemptionReversal`.

# Fase 2.4 - Historial de canjes

La pantalla Admin `/redemptions` muestra el historial completo de canjes, no solo pendientes.

Permite ver:

- Todos los canjes.
- Canjes pendientes.
- Canjes confirmados.
- Canjes cancelados.

La tabla muestra:

- Cliente.
- Serial.
- Recompensa.
- Puntos.
- Estado.
- Fecha de solicitud.
- Fecha de resolucion.
- Operador.
- Notas.
- Acciones.

Las acciones solo estan disponibles para canjes `Pending`:

- Confirmar.
- Cancelar.

Los canjes `Confirmed` y `Cancelled` no permiten acciones de cambio de estado.

Arquitectura de lectura:

```text
Admin
  ↓
ListRedemptionsQuery
  ↓
IRedemptionHistoryReadService
  ↓
RedemptionHistoryReadService
  ↓
SQL Server
```

`RedemptionHistoryReadService` proyecta el historial con datos de cliente, tarjeta y recompensa para evitar consultas N+1 desde la pagina Admin.

# Fase 2.5 - Pulido Admin

El Admin tiene mejoras visuales y operativas en las pantallas de recompensas y canjes.

## `/rewards`

Mejoras implementadas:

- Buscador por nombre/descripcion.
- Filtro `Todas` / `Activas` / `Inactivas`.
- Badges de estado.
- Badges de nivel.
- Producto del mes visual.
- Acciones compactas.
- Tabla mas limpia.

## `/redemptions`

Mejoras implementadas:

- Buscador por cliente, serial o recompensa.
- Filtros `Todos` / `Pendientes` / `Confirmados` / `Cancelados`.
- Badges de estado.
- Acciones compactas.
- Notas truncadas con tooltip cuando aplica.
- Mejor presentacion visual.

# Fase 3.1 - Dashboard / Analytics

El Admin cuenta con un Dashboard analitico en:

```text
/dashboard
```

Tambien responde en `/`.

## Que se agrego

- `GetDashboardSummaryQuery`.
- `GetDashboardSummaryHandler`.
- `DashboardSummaryDto`.
- DTOs de metricas para clientes, puntos, canjes, recompensas y actividad reciente.
- Extension de `IDashboardReadService`.
- Implementacion de agregaciones en `DashboardReadService`.
- Pantalla Admin con cards KPI y tabla de actividad reciente.

## Metricas

Clientes:

- Total de clientes registrados.
- Clientes nuevos este mes.
- Clientes con Wallet emitida.
- Clientes activos.

Puntos:

- Puntos otorgados.
- Puntos canjeados.
- Balance total de puntos actual.

Canjes:

- Canjes pendientes.
- Canjes confirmados.
- Canjes cancelados.
- Total de canjes.

Recompensas:

- Total de recompensas.
- Recompensas activas.
- Recompensas inactivas.

Actividad reciente:

- Ultimos canjes.
- Ultimas transacciones de puntos.

## Criterios de calculo

- Clientes nuevos este mes: clientes con `CreatedAt` desde el primer dia UTC del mes actual.
- Clientes con Wallet emitida: clientes con `LoyaltyCard` existente.
- Clientes activos: clientes con al menos una `PointTransaction` o al menos un `Redemption`.
- Puntos otorgados: suma de `PointTransaction.Points` positivos.
- Puntos canjeados: suma absoluta de transacciones negativas con `TransactionType.Redemption`.
- Balance total: suma de `LoyaltyCard.CurrentPoints`.
- Actividad reciente: union en memoria de los ultimos canjes y las ultimas transacciones de puntos, tomando los 10 mas recientes.

## Arquitectura

```text
Admin Dashboard
  ↓
GetDashboardSummaryQuery
  ↓
IDashboardReadService
  ↓
DashboardReadService
  ↓
SQL Server
```

`DashboardReadService` usa consultas `AsNoTracking` y agregaciones en SQL Server para evitar cargar tablas completas en memoria.

## Prueba manual

1. Levantar API/Admin como normalmente.
2. Abrir Admin.
3. Entrar a `Dashboard`.
4. Confirmar que las cards cargan.
5. Confirmar que no falla sin datos.
6. Crear o usar cliente existente.
7. Crear puntos, canje o recompensa.
8. Refrescar Dashboard y validar que las metricas cambian.

# Fase 3.2 - Customer Detail

El Admin cuenta con una pantalla de detalle de cliente en:

```text
/customers/{customerId}
```

La ruta legacy por serial (`/customers/{serialNumber}`) redirige al detalle por `customerId` cuando encuentra la tarjeta.

## Que se agrego

- `GetCustomerDetailQuery`.
- `GetCustomerDetailHandler`.
- `CustomerDetailDto`.
- Sub DTOs:
  - `CustomerSummaryDto`
  - `CustomerWalletDto`
  - `CustomerPointHistoryItemDto`
  - `CustomerRedemptionHistoryItemDto`
  - `CustomerStatisticsDto`
- `ICustomerDetailReadService`.
- `CustomerDetailReadService`.
- Componente `DetailItem`.
- Navegacion desde el listado de clientes con accion `Ver detalle`.

## Informacion mostrada

Informacion general:

- Nombre completo.
- Email.
- Telefono, si existe.
- Fecha de registro.
- Estado.
- Nivel actual.
- Wallet emitida.

Tarjeta Loyalty / Wallet:

- Numero de tarjeta.
- Puntos actuales.
- Fecha de emision.
- Ultima actualizacion.
- Cantidad de dispositivos registrados.
- Ultimo push enviado si existiera en modelo.

Estadisticas rapidas:

- Puntos actuales.
- Puntos obtenidos historicamente.
- Puntos canjeados.
- Total de canjes.
- Canjes pendientes.
- Canjes cancelados.
- Canjes confirmados.

Historial de puntos:

- Fecha.
- Tipo.
- Descripcion.
- Puntos.
- Balance despues del movimiento cuando exista.

Historial de canjes:

- Fecha.
- Reward.
- Estado.
- Puntos utilizados.

## Criterios usados

- Wallet emitida: existe `LoyaltyCard` para el cliente.
- Fecha de emision de Wallet: se usa `Customer.CreatedAt`, porque el flujo actual crea `Customer` y `LoyaltyCard` en la misma operacion y `LoyaltyCard` no tiene campo propio de creacion.
- Ultima actualizacion: `LoyaltyCard.LastActivityAt`.
- Ultimo push enviado: no existe campo persistido actualmente, por lo que se muestra `No disponible`.
- Balance despues del movimiento: no existe snapshot persistido por transaccion, por lo que se muestra `No disponible`.

## Arquitectura

```text
Admin Customer Detail
  ↓
GetCustomerDetailQuery
  ↓
ICustomerDetailReadService
  ↓
CustomerDetailReadService
  ↓
SQL Server
```

`CustomerDetailReadService` usa `AsNoTracking`, proyecciones y agregaciones SQL. Los historiales se limitan a los ultimos 50 movimientos/canjes.

## Prueba manual

1. Abrir listado de clientes.
2. Entrar al detalle con `Ver detalle`.
3. Validar informacion general.
4. Validar estadisticas.
5. Validar historial de puntos.
6. Validar historial de canjes.
7. Probar un cliente nuevo sin movimientos.
8. Confirmar que la pagina no genera errores.

# Fase 3.3 - Expiracion de puntos con FIFO

El sistema implementa expiracion de puntos por lotes.

Reglas vigentes:

- Cada acumulacion positiva crea un `PointLot`.
- Cada lote conserva `OriginalAmount`, `RemainingAmount`, `EarnedAt` y `ExpiresAt`.
- `ExpiresAt` se calcula con `points_expire_after_months`; el default actual es `12`.
- Los puntos vencen 12 meses despues de ser otorgados.
- Los canjes consumen puntos FIFO: primero los lotes mas antiguos disponibles.
- Los consumos parciales dejan saldo remanente en el lote.
- Los puntos vencidos no pueden usarse en canjes.
- La expiracion es idempotente: un lote con `RemainingAmount = 0` no vuelve a expirar.
- Nunca se expiran mas puntos que el saldo disponible de la tarjeta.

Arquitectura:

```text
POST /api/admin/points/expire
  ↓
ExpirePointsCommand
  ↓
PointLots vencidos
  ↓
PointTransaction negativa (Expired)
  ↓
LoyaltyCard.ExpirePoints
  ↓
LastActivityAt
  ↓
APNs best-effort
  ↓
Apple Wallet refresh
```

Tablas nuevas:

- `PointLots`: lotes positivos con vencimiento.
- `PointLotConsumptions`: asignaciones FIFO de movimientos negativos contra lotes.

Flujos modificados:

- Registro de cliente: el bono de bienvenida crea `PointTransaction` positiva y `PointLot`.
- Acumulacion de puntos: la compra crea `PointTransaction` positiva y `PointLot`.
- Canje: crea `PointTransaction` negativa, descuenta `LoyaltyCard.CurrentPoints` y consume lotes FIFO.
- Cancelacion de canje: restaura el saldo, marca consumos como reversados y devuelve puntos a los lotes originales sin crear lotes nuevos.
- Expiracion: crea `PointTransaction` negativa de tipo `Expired`, deja los lotes en `RemainingAmount = 0`, actualiza `LastActivityAt` y notifica Wallet.

Configuracion:

- `points_expiration_enabled`: `true` / `false`.
- `points_expire_after_months`: entero mayor a `0`; default `12`.

Todas las fechas de lotes, consumos y ejecucion de expiracion usan UTC mediante el proveedor de tiempo del proyecto.

Endpoint administrativo:

```text
POST /api/admin/points/expire
```

Header:

```text
X-Operator-Id: admin
```

Respuesta:

- `runAt`
- `enabled`
- `clientsProcessed`
- `clientsAffected`
- `lotsExpired`
- `pointsExpired`
- `walletsNotified`
- `warnings`

La migracion `AddPointLotsForExpiration` asume una base nueva de desarrollo. LoyaltyCloud esta en etapa inicial, por lo que no se implementa estrategia de migracion historica en Fase 3.3.

La migracion se limita a:

- Crear `PointLots`.
- Crear `PointLotConsumptions`.
- Crear claves foraneas e indices.
- Insertar las claves `points_expiration_enabled` y `points_expire_after_months`.

Para desarrollo local, la ruta recomendada es borrar y recrear la base en lugar de migrar saldos o canjes anteriores.

Regla futura documentada, no implementada en Fase 3.3:

- El nivel se calculara con puntos positivos ganados en una ventana movil de 12 meses.
- Los canjes no reduciran progreso de nivel.
- La expiracion no reducira progreso directamente.
- El nivel podra bajar cuando puntos antiguos salgan de la ventana.
- El recalculo ocurrira al otorgar puntos y en el proceso diario.

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

Se usa `storeCard` porque Apple lo define para tarjetas de tienda, tarjetas de puntos, tarjetas de regalo y tarjetas de lealtad. LoyaltyCloud implementa una tarjeta de lealtad por puntos para KBeauty MX, por lo que `storeCard` es el tipo semantico correcto.

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

Cuando se cancela un canje pendiente:

1. `PUT /api/redemptions/{id}/cancel` llega a `RedemptionsController`.
2. `CancelRedemptionHandler` valida que el canje exista y este `Pending`.
3. `Redemption.Cancel(...)` cambia el estado a `Cancelled`.
4. `LoyaltyCard.RestorePoints(...)` restaura los puntos gastados.
5. Se crea `PointTransaction` positiva de tipo `RedemptionReversal`.
6. `RestorePoints` actualiza `LastActivityAt`.
7. Se guarda el cambio.
8. El handler busca devices registrados por serial.
9. Llama `IApnService.SendPassUpdateAsync` por cada push token.
10. `ApnService` manda push background a APNs.
11. Wallet consulta seriales actualizados.
12. Wallet descarga el pass actualizado.

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

La deteccion de cambios para Apple se basa en `LoyaltyCard.LastActivityAt`: compras/acumulaciones lo actualizan en `EarnPoints`; canjes lo actualizan con `Touch` despues de descontar puntos; cancelaciones de canjes lo actualizan con `RestorePoints` al restaurar saldo.

# Fase 3.4 - Niveles automaticos con ventana movil de 12 meses

El nivel de lealtad se calcula con puntos positivos elegibles acumulados en una ventana movil de 12 meses:

```text
windowStart = now.AddMonths(-12)
```

La fuente de verdad para el progreso de nivel es `PointTransaction`, no `CurrentPoints`, `LifetimePoints` ni `PointsEarnedThisYear`.

Tipos incluidos:

- `Purchase`
- `BonusWelcome`
- `BonusBirthday`
- `BonusReferral`

Tipos excluidos:

- `Redemption`
- `RedemptionReversal`
- `Expired`
- `Expiry`

Los canjes no reducen progreso de nivel. Las cancelaciones/reversas no aumentan progreso de nivel. La expiracion de puntos no reduce progreso directamente. El nivel puede bajar cuando transacciones elegibles antiguas salen de la ventana movil.

El cambio de nivel se aplica con `LoyaltyCard.ApplyCalculatedLevel(...)`. Cuando el nivel cambia:

- se actualiza `LoyaltyCard.Level`;
- se actualiza `LevelAchievedAt` como fecha de entrada al nivel actual;
- se actualiza `LastActivityAt`;
- se dispara APNs best-effort con `PassUpdateReason.LevelChanged`.

Endpoint administrativo:

```text
POST /api/admin/levels/recalculate
```

Header opcional:

```text
X-Operator-Id: admin
```

Response:

```json
{
  "runAt": "2026-07-13T00:00:00Z",
  "cardsProcessed": 10,
  "cardsChanged": 2,
  "cardsUpgraded": 1,
  "cardsDowngraded": 1,
  "walletsNotified": 2,
  "warnings": []
}
```

Tambien se recalcula el nivel al otorgar puntos en `AddPointsHandler` y al crear bonos de bienvenida/referido en `RegisterCustomerHandler`. El recalculo tambien puede ejecutarse manualmente desde el endpoint administrativo y de forma automatica mediante el proceso diario de mantenimiento.

Nota legacy: `PointsEarnedThisYear` permanece en el modelo por compatibilidad historica. En el DTO legacy de `GetCustomerBySerial`, el campo `PointsEarnedThisYear` devuelve los puntos elegibles de la ventana movil de 12 meses hasta que ese contrato sea renombrado en una fase posterior.

# Fase 3.5 - Proceso diario de mantenimiento

La API hospeda `LoyaltyMaintenanceBackgroundService`, un `BackgroundService` que orquesta procesos diarios ya existentes mediante MediatR.

El servicio vive en `LoyaltyCloud.API` porque la API es el host operativo principal: expone los endpoints manuales, registra Application/Infrastructure, tiene acceso a MediatR, repositorios, APNs y configuracion de Wallet. Admin es un proceso separado y no debe ejecutar mantenimiento para evitar duplicidad.

El hosted service no accede directamente a `AppDbContext` ni inyecta handlers. En cada ejecucion crea un scope con `IServiceScopeFactory`, resuelve `ISender` y ejecuta:

```text
ExpirePointsCommand
  ↓
RecalculateLevelsCommand
```

Configuracion:

```json
{
  "LoyaltyMaintenance": {
    "Enabled": true,
    "RunOnStartup": false,
    "RunAtLocalTime": "02:00",
    "TimeZoneId": "America/Tijuana"
  }
}
```

# Fase 3.6 - Customer Detail avanzado

La pantalla Admin `/customers/{customerId}` ahora funciona tambien como herramienta de auditoria de puntos para soporte.

La informacion se obtiene por lectura mediante `GetCustomerDetailQuery` y `ICustomerDetailReadService`. No modifica puntos, lotes, Wallet, APNs, expiracion ni canjes.

Datos agregados:

- saldo disponible (`CurrentPoints`);
- puntos rolling de 12 meses calculados con la misma regla de niveles;
- lifetime points;
- nivel actual y fecha de entrada al nivel;
- proxima expiracion de puntos;
- tabla de lotes FIFO;
- tabla de consumos FIFO;
- progreso real hacia el siguiente nivel;
- balance despues de cada movimiento visible en historial.

DTOs agregados al Customer Detail:

- `CustomerLoyaltyAuditDto`
- `UpcomingExpirationDto`
- `RollingProgressDto`
- `LotSummaryDto`
- `ConsumptionDto`

Los lotes se ordenan por:

```text
ExpiresAt
EarnedAt
Id
```

Estados de lote:

- `Activo`: lote completo disponible y no vencido.
- `Parcialmente consumido`: tiene saldo disponible, pero ya fue consumido parcialmente.
- `Consumido`: saldo disponible en cero por consumo FIFO.
- `Expirado`: vencido o consumido por expiracion automatica.

La proxima expiracion considera lotes con `RemainingAmount > 0` y `ExpiresAt > now`, agrupando los puntos que vencen en la fecha mas cercana.

El resumen de consumo FIFO solo se carga cuando existen lotes con consumo, para evitar consultas innecesarias en clientes sin canjes/expiraciones.

# Fase 3.7 - Producto del mes

El catalogo de recompensas soporta formalmente Producto del mes usando las columnas existentes:

- `RewardCatalogItem.IsMonthlyProduct`
- `RewardCatalogItem.ValidFrom`
- `RewardCatalogItem.ValidTo`
- `RewardCatalogItem.IsActive`

No hay cambios de esquema para esta fase.

## Regla funcional

Debe existir como maximo un Producto del mes activo y vigente para una fecha determinada.

Una recompensa es Producto del mes vigente cuando:

```text
IsMonthlyProduct = true
IsActive = true
ValidFrom <= nowUtc
ValidTo >= nowUtc
```

La convencion temporal sigue siendo UTC, igual que el resto de vigencias y procesos del backend.

## Vigencia y traslape

Para `IsMonthlyProduct = true`, `ValidFrom` y `ValidTo` son obligatorios.

Dos Productos del mes activos se consideran traslapados si:

```text
existing.ValidFrom <= new.ValidTo
AND existing.ValidTo >= new.ValidFrom
```

El sistema valida traslapes al:

- crear una recompensa activa marcada como Producto del mes;
- editar una recompensa activa marcada como Producto del mes;
- activar una recompensa marcada como Producto del mes.

Durante edicion, la recompensa actual se excluye de la comparacion por `Id`.

Se permite crear un Producto del mes inactivo como borrador aunque su vigencia se traslape. El conflicto se valida al intentar activarlo.

No se desactiva automaticamente ningun Producto del mes existente. Si hay conflicto, el caso de uso devuelve error de negocio.

## Catalogo y canje

`GET /api/redemptions/catalog/{serialNumber}` conserva el DTO existente con `IsMonthlyProduct`.

El catalogo y el canje siguen respetando:

- `IsActive`;
- vigencia;
- nivel minimo;
- saldo disponible;
- puntos no vencidos/FIFO.

Un Producto del mes sin fechas completas no es canjeable.

## Admin

La pantalla `/rewards` muestra:

- tarjeta compacta de Producto del mes actual;
- estado del Producto del mes: `Vigente`, `Programado`, `Vencido` o `Inactivo`;
- vigencia;
- error visual si se marca Producto del mes sin fechas;
- errores de traslape devueltos por Application.

## Apple Wallet

El Producto del mes no se agrega al frente ni al reverso del pass en esta fase. Una fase futura puede evaluar mostrar una promocion dinamica en el reverso, sin cambiar firma ni endpoints `/v1`.

# Fase 3.8 - Campanas de puntos

El sistema implementa campanas promocionales de multiplicador para compras procesadas por `POST /api/points`.

Reglas implementadas:

- Aplican unicamente a compras (`TransactionType.Purchase`).
- No aplican a bienvenida, referidos, canjes, reversas, expiracion ni ajustes tecnicos.
- El multiplicador es entero entre `2` y `5`.
- El calculo base no cambia: `BasePoints = floor(purchaseAmount / PointsPerPesoUnit)`.
- `FinalPoints = BasePoints * EffectiveMultiplier`.
- Se crea una sola `PointTransaction` y un solo `PointLot` con los puntos finales.
- Si hay campanas traslapadas, se elige una sola: mayor multiplicador, menor monto minimo, inicio mas reciente, `Id`.
- La elegibilidad por nivel usa el nivel almacenado actual de `LoyaltyCard`, calculado por rolling points.
- `MinimumPurchaseAmount = null` aplica a cualquier compra; si existe se evalua con `purchaseAmount >= MinimumPurchaseAmount`.
- Cumpleanos y campana no se acumulan. Se usa `MAX(1, BirthdayMultiplier, CampaignMultiplier)`.
- En empate entre cumpleanos y campana, se registra la campana como causa efectiva y `BirthdayBonusApplied = false`.
- Los puntos finales cuentan para saldo, lotes, expiracion FIFO y nivel rolling.

Entidad:

- `PointCampaign`
- Campos: `Name`, `Description`, `Multiplier`, `MinimumPurchaseAmount`, `LevelEligibility`, `StartsAtUtc`, `EndsAtUtc`, `IsActive`, `CreatedAt`, `UpdatedAt`.
- Vigencia efectiva: `IsActive && StartsAtUtc <= nowUtc && EndsAtUtc >= nowUtc`.
- `LevelEligibility`: `All`, `Mist`, `Glow`, `Radiance`.

Auditoria en `PointTransaction`:

- `CampaignId` nullable.
- `BasePoints` nullable.
- `AppliedMultiplier` nullable.
- La FK a `PointCampaigns` usa delete restrict.

API administrativa:

- `GET /api/campaigns`
- `GET /api/campaigns/{id}`
- `POST /api/campaigns`
- `PUT /api/campaigns/{id}`
- `PUT /api/campaigns/{id}/activate`
- `PUT /api/campaigns/{id}/deactivate`

Admin:

- Nueva pagina `/campaigns`.
- Permite listar, crear, editar, activar y desactivar campanas.
- Muestra estados `Vigente`, `Programada`, `Vencida`, `Inactiva`.
- Captura fecha/hora local de Tijuana y persiste UTC.
- Muestra vigencia nuevamente en hora de Tijuana.

Customer Detail:

- El historial de puntos muestra campana y multiplicador cuando la compra tuvo campana efectiva.

Migracion:

- `AddPointCampaignsAndTransactionAudit`.
- Crea `PointCampaigns`.
- Agrega `CampaignId`, `BasePoints`, `AppliedMultiplier` a `PointTransactions`.
- No incluye backfill historico porque el proyecto esta en etapa inicial y la base local puede recrearse.

`RunAtLocalTime` se interpreta en la zona horaria configurada. `America/Tijuana` es la zona funcional de Ensenada/Baja California. En Windows, si el identificador IANA no existe, el servicio intenta usar el equivalente `Pacific Standard Time (Mexico)`.

El servicio calcula la siguiente ejecucion diaria:

- si la API inicia antes de la hora configurada, corre ese mismo dia a esa hora;
- si inicia despues, corre al dia siguiente;
- no ejecuta mantenimiento al iniciar por defecto;
- respeta cambios de horario mediante `TimeZoneInfo`;
- si la hora local cae en una hora invalida por cambio de horario, avanza en bloques cortos hasta una hora valida.

`RunOnStartup=true` permite ejecutar una vez al iniciar para pruebas manuales. Despues de esa corrida, el servicio vuelve a esperar la siguiente ejecucion diaria.

Manejo de errores:

- registra inicio, resultados, warnings y duracion;
- si expiracion falla inesperadamente, registra error y continua con recalculo de niveles;
- si recalculo falla, registra error y espera la siguiente ejecucion;
- una excepcion de mantenimiento no debe detener permanentemente la API.

Limitacion actual:

- se asume una sola instancia del host;
- no hay distributed lock en esta fase;
- antes de escalar a multiples instancias se debe agregar un lock distribuido o mover el mantenimiento a un worker singleton.

Para desactivarlo:

```json
{
  "LoyaltyMaintenance": {
    "Enabled": false
  }
}
```

# Fase 5.1 - Motor base de notificaciones

El proyecto cuenta con un motor base de notificaciones de lealtad implementado y pendiente de validacion manual.

La fase distingue tres conceptos:

- `LoyaltyNotification`: notificacion logica del sistema.
- `NotificationDelivery`: intento de entrega por canal.
- Apple Wallet: canal real disponible en Fase 5.1, basado en refresh del pass.

Apple Wallet no recibe texto arbitrario en APNs. El push APNs de PassKit solo despierta Wallet para que consulte el Web Service y descargue un pass actualizado. El contenido visible se publica en el reverso del pass como un `backField` compacto:

```text
NOVEDADES
<mensaje activo>
```

## Modelo

Entidades:

- `LoyaltyNotification`
  - cliente;
  - tarjeta;
  - tipo;
  - titulo;
  - mensaje;
  - estado;
  - programacion;
  - vigencia visual en Wallet;
  - `CorrelationId` para idempotencia.
- `NotificationDelivery`
  - canal;
  - estado;
  - intentos;
  - dispositivos encontrados;
  - pushes intentados;
  - pushes aceptados;
  - pushes fallidos;
  - error de entrega.

Enums:

- `NotificationType`: `LevelChanged`, `PointsExpiring`, `MonthlyProductStarted`, `PointCampaignStarted`, `BirthdayBenefitStarted`, `Custom`.
- `NotificationStatus`: `Pending`, `Processing`, `Delivered`, `PartiallyDelivered`, `Failed`, `Cancelled`.
- `NotificationChannel`: `AppleWallet`, `Email`, `Sms`, `WhatsApp`, `MobilePush`.
- `NotificationDeliveryStatus`: `Pending`, `Processing`, `Succeeded`, `Failed`, `Cancelled`, `Unsupported`, `NoRecipients`.

Solo `AppleWallet` tiene procesador real en Fase 5.1. Los demas canales existen como modelo futuro y quedan como `Unsupported` si se intentan procesar.

## Arquitectura

```text
Application CQRS
  ↓
ILoyaltyNotificationService
  ↓
INotificationChannelProcessor
  ↓
AppleWalletNotificationChannelProcessor
  ↓
LoyaltyCard.Touch
  ↓
APNs PassKit
  ↓
Wallet descarga pass actualizado
```

Servicios nuevos:

- `ILoyaltyNotificationService`
- `INotificationChannelProcessor`
- `IWalletNotificationReadService`
- `LoyaltyNotificationService`
- `AppleWalletNotificationChannelProcessor`
- `WalletNotificationReadService`

El procesador Apple Wallet:

1. Marca la entrega como `Processing`.
2. Busca la tarjeta.
3. Ejecuta `LoyaltyCard.Touch(...)` para actualizar `LastActivityAt`.
4. Busca `DeviceRegistration` por serial.
5. Envia APNs PassKit best-effort a cada device.
6. Guarda metricas agregadas de entrega.

## Origen automatico real

El origen automatico implementado en Fase 5.1 es cambio positivo de nivel.

Cuando `LoyaltyCard.ApplyCalculatedLevel(...)` cambia de nivel y el nuevo nivel es superior al anterior, se publica una `LoyaltyNotification` de tipo `LevelChanged`.

Las bajadas de nivel no generan mensaje de felicitacion. Siguen actualizando el pass mediante el flujo existente de refresh de Wallet.

## Idempotencia

`LoyaltyNotification.CorrelationId` tiene indice unico filtrado cuando no es `NULL`.

Para cambios de nivel, el correlation id se construye con tarjeta, nuevo nivel y fecha de obtencion del nivel. Si el mismo evento se intenta procesar dos veces, el servicio devuelve la notificacion existente y evita duplicados.

## API

Endpoints administrativos:

- `GET /api/notifications`
- `GET /api/notifications/metrics`
- `GET /api/notifications/{id}`
- `POST /api/notifications`
- `POST /api/notifications/{id}/process`
- `POST /api/notifications/{id}/retry`
- `PUT /api/notifications/{id}/cancel`

Estos endpoints usan MediatR y no acceden directo a `AppDbContext`.

## Admin

Pagina nueva:

```text
/notifications
```

Permite:

- ver metricas;
- listar notificaciones;
- filtrar por tipo y estado;
- crear notificacion manual `Custom` para un serial;
- procesar pendientes;
- reintentar fallidas;
- cancelar pendientes.

Customer Detail tambien muestra historial compacto de notificaciones del cliente.

## Scheduler

La API hospeda `LoyaltyNotificationBackgroundService`.

Configuracion:

```json
"LoyaltyNotifications": {
  "Enabled": true,
  "RunOnStartup": false,
  "PollIntervalSeconds": 60,
  "BatchSize": 25,
  "MaxAttempts": 3
}
```

El scheduler crea un scope, resuelve `ISender` y ejecuta `ProcessPendingNotificationsCommand`.

## Migracion

Fase 5.1 requiere migracion de esquema para:

- crear `LoyaltyNotifications`;
- crear `NotificationDeliveries`;
- crear foreign keys;
- crear indices;
- crear indice unico filtrado sobre `CorrelationId`.

La migracion no debe aplicarse automaticamente. En desarrollo se aplica manualmente con `dotnet ef database update` cuando se vaya a validar.

# Fase 5.2 - Notificaciones visibles Apple Wallet con changeMessage

Fase 5.2 esta implementada y pendiente de validacion manual.

El objetivo de esta fase es validar alertas visibles reales de Apple Wallet para subidas de nivel.

## Regla PassKit

Apple Wallet no muestra texto arbitrario enviado por APNs. El flujo correcto es:

```text
APNs PassKit silencioso
  ↓
Wallet consulta seriales actualizados
  ↓
Wallet descarga nuevo .pkpass
  ↓
Wallet compara campos existentes
  ↓
Si cambio un campo con changeMessage valido, puede mostrar alerta visible
```

El `changeMessage` debe vivir en el campo que cambia y debe contener `%@`.

## Campo usado

Se reutiliza el campo visual actual de nivel:

```json
{
  "key": "level",
  "label": "NIVEL",
  "value": "Glow ✨",
  "changeMessage": "🎉 Ahora eres cliente %@"
}
```

La key `level` es estable. No se mueve entre secciones, no se renombra y no se genera dinamicamente.

## Estrategia

`IWalletNotificationReadService` proyecta la notificacion activa mas reciente para la tarjeta, incluyendo:

- `NotificationType`;
- `Message`;
- `MetadataJson`.

`PassGeneratorService` solo agrega `changeMessage` al campo `level` cuando:

- existe una notificacion activa `LevelChanged`;
- la metadata contiene `isUpgrade = true`;
- `metadata.newLevel` coincide con `LoyaltyCard.Level`;
- el pass se esta regenerando para esa tarjeta.

La metadata de `LevelChanged` tiene esta forma:

```json
{
  "previousLevel": "Mist",
  "newLevel": "Glow",
  "isUpgrade": true
}
```

## Downgrades

Las bajadas de nivel siguen actualizando el pass, pero no generan una notificacion `LevelChanged` positiva y no incluyen `changeMessage` de felicitacion.

Esto evita que un cambio de `Glow` a `Mist` muestre un mensaje tipo:

```text
Ahora eres cliente Mist
```

## Alertas repetidas

No se usan timestamps ni valores artificiales para forzar alertas.

La alerta depende de que el valor estable del campo `level` cambie, por ejemplo:

```text
Mist ✨ → Glow ✨
```

Si Wallet vuelve a descargar el pass sin otro cambio de nivel, el campo conserva la misma key y el mismo value, por lo que no deberia generar otra alerta por el mismo cambio.

## NOVEDADES

La seccion `NOVEDADES` del reverso se conserva como complemento visible.

En Fase 5.2 ya no se usa `changeMessage` en `NOVEDADES`; la alerta visible principal debe venir del campo estable `level`.

## Logs de diagnostico

Se agregaron logs seguros para distinguir:

- creacion de notificacion `LevelChanged`;
- resumen APNs del canal Apple Wallet;
- consulta de seriales actualizados por Wallet;
- descarga del pass actualizado;
- inclusion u omision de `changeMessage` en el campo `level`.

No se registran `authenticationToken` ni `pushToken`.

## Prueba manual

1. Instalar un pass en Mist.
2. Confirmar `DeviceRegistration`.
3. Confirmar que el pass inicial contiene:

```json
{
  "key": "level",
  "value": "Mist ✨"
}
```

4. Otorgar puntos suficientes para subir a Glow.
5. Confirmar que se crea una notificacion logica `LevelChanged`.
6. Confirmar una entrega Apple Wallet.
7. Confirmar un solo APNs del motor de notificaciones.
8. Confirmar que Wallet llama:

```text
GET /v1/devices/{device}/registrations/{passType}
GET /v1/passes/{passType}/{serial}
```

9. Confirmar que el nuevo pass contiene:

```json
{
  "key": "level",
  "value": "Glow ✨",
  "changeMessage": "🎉 Ahora eres cliente %@"
}
```

10. Verificar si iOS muestra alerta visible.
11. Abrir Wallet y confirmar nivel Glow.
12. Ejecutar refresh adicional sin cambio de nivel y confirmar que no se crea otra notificacion de nivel ni otro APNs de nivel.
13. Probar downgrade y confirmar que el pass actualiza nivel, pero no muestra felicitacion.

## Migracion

Fase 5.2 no requiere cambios de esquema ni migracion nueva.

# Fase 5.3 - Aviso de puntos por expirar

Fase 5.3 esta implementada y pendiente de validacion manual.

El objetivo es avisar automaticamente cuando una clienta tenga puntos disponibles que expiran exactamente dentro de 15 dias calendario en la zona `America/Tijuana`.

## Regla

Se crea una notificacion cuando existen `PointLots` que cumplen:

- `RemainingAmount > 0`;
- `ExpiresAt` cae dentro del dia local objetivo en `America/Tijuana`;
- faltan exactamente 15 dias calendario locales;
- no existe una notificacion previa con el mismo `CorrelationId`.

No se crea una notificacion por lote. Los lotes se agrupan por tarjeta y fecha local de expiracion.

Ejemplo:

```text
100 pts + 80 pts + 70 pts = 250 pts
```

La clienta recibe una sola notificacion:

```text
⚠️ 250 puntos vencerán en 15 días.

Úsalos antes de perderlos.
```

## Idempotencia

La idempotencia usa `LoyaltyNotification.CorrelationId`:

```text
points-expiring:{serialNumber}:{expirationDate:yyyyMMdd}
```

Si el scheduler corre varias veces el mismo dia, la segunda ejecucion detecta que ya existe la notificacion y no crea otra.

## Scheduler

Se integra al scheduler diario existente `LoyaltyMaintenanceBackgroundService`.

Orden actual:

```text
1. Expiracion FIFO
2. Recalculo de niveles
3. Avisos de puntos por expirar
```

No se creo otro `BackgroundService`.

## Query administrativa

Endpoint de preview:

```text
GET /api/admin/points/expiration-notification-candidates
```

Query params:

- `daysAhead`: default `15`;
- `timeZoneId`: default `America/Tijuana`;
- `includeAlreadyNotified`: default `false`.

Este endpoint solo consulta candidatos. No crea notificaciones y no envia APNs.

## Apple Wallet

Se reutiliza el mecanismo de Fase 5.2.

Mientras existan puntos proximos a expirar, el pass incluye un campo estable:

```json
{
  "key": "points_expiring",
  "label": "POR EXPIRAR",
  "value": "250 pts",
  "changeMessage": "⚠️ %@ vencerán pronto."
}
```

Reglas:

- la key es fija;
- el value se calcula desde lotes reales actuales;
- `changeMessage` usa `%@`;
- si los puntos se gastan y `RemainingAmount` llega a `0`, el campo desaparece;
- si los puntos expiran, el campo desaparece despues del mantenimiento diario;
- no se usan timestamps ni valores artificiales para repetir alertas.

La seccion `NOVEDADES` del reverso sigue existiendo como complemento visible.

## Customer Detail

La pantalla `/customers/{customerId}` conserva la seccion pequeña de proxima expiracion:

```text
Proxima expiracion
250 puntos
31 Jul 2026
```

Esa lectura se basa en lotes reales con `RemainingAmount > 0`.

## Prueba manual

Caso 1:

1. Crear o ajustar un `PointLot` con `RemainingAmount > 0`.
2. Configurar `ExpiresAt` para que caiga dentro del dia local de Tijuana exactamente 15 dias adelante.
3. Ejecutar el scheduler o activar `RunOnStartup` temporalmente.
4. Verificar:
   - una `LoyaltyNotification` tipo `PointsExpiring`;
   - una `NotificationDelivery`;
   - APNs PassKit;
   - Wallet descarga el pass;
   - el campo `points_expiring` aparece con `changeMessage`.

Caso 2:

1. Ejecutar el scheduler otra vez el mismo dia.
2. Confirmar que no se crea otra notificacion por el mismo serial y fecha.

Caso 3:

1. Consumir todos los puntos de esos lotes con un canje.
2. Actualizar Wallet.
3. Confirmar que `points_expiring` desaparece.

Caso 4:

1. Dejar que los puntos expiren.
2. Ejecutar mantenimiento diario.
3. Confirmar que `points_expiring` desaparece.

Caso 5:

1. Crear dos o mas lotes que expiren el mismo dia local.
2. Confirmar que se crea una sola notificacion con la suma total.

## Migracion

Fase 5.3 no requiere migracion nueva. Reutiliza tablas de Fase 5.1:

- `LoyaltyNotifications`;
- `NotificationDeliveries`;
- `PointLots`.

# Fase 5.4 - Producto del mes visible en Wallet

Fase 5.4 esta implementada y pendiente de validacion manual.

El objetivo es avisar automaticamente cuando exista un Producto del mes activo y vigente, y mostrarlo en Apple Wallet mientras siga vigente.

## Regla

Un Producto del mes vigente cumple:

- `RewardCatalogItem.IsMonthlyProduct = true`;
- `RewardCatalogItem.IsActive = true`;
- `RewardCatalogItem.ValidFrom <= nowUtc`;
- `RewardCatalogItem.ValidTo >= nowUtc`;
- `ValidFrom` y `ValidTo` existen.

La persistencia y la comparacion de vigencia siguen en UTC. Las fechas visibles se muestran como fecha local en `America/Tijuana`.

## Destinatarios

La notificacion automatica se crea solo para tarjetas activas con cliente activo y al menos un `DeviceRegistration`.

Decision tecnica: en esta fase no se crean notificaciones para tarjetas sin dispositivo registrado. El motivo es evitar registros logicos sin posibilidad real de entrega Apple Wallet. Si la clienta instala Wallet despues, el pass mostrara el Producto del mes vigente al descargarse porque la lectura del pass consulta el reward vigente dinamicamente.

## Idempotencia

La idempotencia usa `LoyaltyNotification.CorrelationId`:

```text
monthly-product-started:{rewardId:N}:{serialNumber}
```

Debe existir solo una notificacion por tarjeta y Producto del mes. Reiniciar la API o ejecutar el scheduler varias veces no debe duplicar notificaciones ni APNs.

## Scheduler

Se integra al scheduler diario existente `LoyaltyMaintenanceBackgroundService`.

Orden actual:

```text
1. Expiracion FIFO
2. Recalculo de niveles
3. Avisos de puntos por expirar
4. Avisos de Producto del mes
5. Avisos de beneficio de cumpleanos
```

No se creo otro `BackgroundService`.

## Query administrativa

Endpoint de preview:

```text
GET /api/admin/rewards/monthly-product-notification-candidates
```

Query params:

- `timeZoneId`: default `America/Tijuana`;
- `includeAlreadyNotified`: default `false`.

Este endpoint solo consulta candidatos. No crea notificaciones, no envia APNs y no modifica base.

## Command

El scheduler ejecuta:

```text
CreateMonthlyProductStartedNotificationsCommand
```

Response:

- `RunAtUtc`;
- `MonthlyProductId`;
- `MonthlyProductName`;
- `CardsEligible`;
- `NotificationsCreated`;
- `AlreadyNotified`;
- `Warnings`.

El command crea `LoyaltyNotification` con:

- `Type = MonthlyProductStarted`;
- `Title = Nuevo Producto del mes`;
- `Message = dinamico segun producto, costo y fecha local`;
- `Channels = [AppleWallet]`;
- `Source = loyalty-maintenance`;
- `DisplayUntilUtc = ValidToUtc`;
- `ProcessImmediately = true`.

## Apple Wallet

Se agrega un campo estable al frente del pass cuando existe Producto del mes vigente:

```json
{
  "key": "monthly_product",
  "label": "PRODUCTO DEL MES",
  "value": "Centella Ampoule",
  "changeMessage": "🎁 Nuevo Producto del mes: %@"
}
```

Ubicacion elegida: `auxiliaryFields`.

Motivo: es la seccion mas segura para un `changeMessage` visible porque participa en el frente del pass con key estable. El frente ya contiene `points`, `level`, `next` y puede contener `points_expiring`; por eso se mantiene el detalle completo en el reverso y se evita redisenar el layout.

Reglas:

- la key es fija;
- el campo aparece mientras el producto este vigente;
- el campo desaparece cuando vence o se desactiva;
- no se usa timestamp ni valor artificial;
- Wallet no deberia repetir alerta si vuelve a descargar el mismo valor.

## Reverso

Mientras exista Producto del mes vigente, se agrega:

```text
PRODUCTO DEL MES

Centella Ampoule

700 pts

Disponible hasta 31 jul 2026
```

El reverso usa la fecha local de `America/Tijuana`.

## Lectura dinamica

`IWalletNotificationReadService` ahora devuelve `WalletNotificationContext.MonthlyProduct`.

`WalletNotificationReadService` consulta en tiempo real `RewardCatalogItems` y verifica:

- activo;
- vigente;
- marcado como Producto del mes.

Si el reward vence o se desactiva, el campo no aparece aunque exista una notificacion historica.

## APNs

No se envia APNs directo desde el scheduler. El command crea `LoyaltyNotification` y reutiliza `AppleWalletNotificationChannelProcessor`.

## Migracion

Fase 5.4 no requiere migracion nueva. Reutiliza:

- `RewardCatalogItems`;
- `LoyaltyNotifications`;
- `NotificationDeliveries`;
- `DeviceRegistrations`.

# Fase 5.5 - Beneficio de cumpleanos visible en Wallet

Fase 5.5 esta implementada y pendiente de validacion manual.

El objetivo es comunicar en Apple Wallet el beneficio de cumpleanos que ya existia en la regla de acumulacion de puntos.

## Regla actual preservada

La regla existente vive en `AddPointsHandler`:

- usa `Customer.DateOfBirth`;
- evalua `customer.DateOfBirth.IsBirthMonth(now)`;
- `IsBirthMonth` compara el mes de nacimiento contra el mes actual;
- el beneficio aplica durante todo el mes de cumpleanos;
- el multiplicador viene de `ProgramConfig` con key `birthday_multiplier`;
- si hay campana de puntos, se usa el mayor multiplicador entre cumpleanos y campana;
- no se acumulan multiplicadores.

Fase 5.5 no cambia esa regla. Solo comunica el beneficio.

## Zona horaria

La deteccion de candidatos usa fecha local de tienda en `America/Tijuana`.

La regla es mensual, por lo que una clienta nacida el 29 de febrero conserva febrero como mes de cumpleanos. No se requiere regla especial de dia en anos no bisiestos.

`DisplayUntilUtc` se calcula como el inicio del primer dia del mes siguiente en `America/Tijuana`, convertido a UTC.

## Destinatarios

Se consideran solo:

- `Customer.IsActive = true`;
- `LoyaltyCard.IsActive = true`;
- `DateOfBirth` con mes igual al mes local actual;
- al menos un `DeviceRegistration`.

No se crean notificaciones para clientas sin pass instalado en esta fase.

## Idempotencia

CorrelationId:

```text
birthday-benefit:{serialNumber}:{year}
```

Ejemplo:

```text
birthday-benefit:KB-JHYL7KK:2026
```

Esto permite una sola notificacion por tarjeta y ano.

## Scheduler

El scheduler diario ejecuta ahora:

```text
1. ExpirePointsCommand
2. RecalculateLevelsCommand
3. CreatePointExpirationNotificationsCommand
4. CreateMonthlyProductStartedNotificationsCommand
5. CreateBirthdayBenefitStartedNotificationsCommand
```

No se creo otro `BackgroundService`.

## Query administrativa

Endpoint de preview:

```text
GET /api/admin/customers/birthday-notification-candidates
```

Query params:

- `timeZoneId`: default `America/Tijuana`;
- `includeAlreadyNotified`: default `false`.

El preview devuelve candidatos y no crea notificaciones, no envia APNs y no modifica base.

## Apple Wallet

Campo estable:

```json
{
  "key": "birthday_benefit",
  "label": "CUMPLEAÑOS",
  "value": "Puntos x2",
  "changeMessage": "🎂 Tu beneficio de cumpleaños está activo: %@"
}
```

Politica visual del frente:

1. Campos principales siempre visibles:
   - `points`;
   - `level`;
   - `next`.
2. Solo un campo temporal aparece en `auxiliaryFields`.
3. Si existe un evento visible reciente dentro de `LoyaltyNotifications:VisibleEventPriorityHours`, ese evento gana temporalmente para permitir `changeMessage`.
4. Prioridad de eventos recientes:
   - `LevelChanged`;
   - `BirthdayBenefitStarted`;
   - `PointsExpiring`;
   - `MonthlyProductStarted`;
   - `PointCampaignStarted`;
   - `Custom`.
5. Cuando no hay evento reciente, se usa prioridad permanente:
   - `points_expiring`;
   - `birthday_benefit`;
   - `point_campaign`;
   - `monthly_product`.

Los detalles de beneficio de cumpleanos y Producto del mes siguen disponibles en `backFields` cuando estan activos.

El fallback permanente no agrega `changeMessage`; de esta forma, cuando termina la ventana reciente y el frente vuelve a mostrar `points_expiring`, no se intenta disparar otra alerta visible ni se envia APNs adicional.

Configuracion:

```json
"LoyaltyNotifications": {
  "VisibleEventPriorityHours": 24
}
```

Si la configuracion es invalida, el sistema usa 24 horas y no detiene la API.

## Reverso

Mientras el beneficio este activo:

```text
BENEFICIO DE CUMPLEAÑOS

Este mes obtienes Puntos x2 en todas tus compras.
```

## Lectura dinamica

`IWalletNotificationReadService` devuelve `WalletNotificationContext.BirthdayBenefit`.

`WalletNotificationReadService` verifica en tiempo real:

- tarjeta activa;
- cliente activo;
- mes local de cumpleanos activo;
- notificacion `BirthdayBenefitStarted` activa y procesada;
- multiplicador actual desde `ProgramConfig`.

Si termina el mes, el campo desaparece aunque exista historial.

Si cambia `birthday_multiplier` durante el mes, el pass muestra el multiplicador actual que aplicaria hoy. No se crea una segunda notificacion automaticamente por cambiar la configuracion.

## Migracion

Fase 5.5 no requiere migracion nueva. Reutiliza:

- `Customer.DateOfBirth`;
- `ProgramConfig`;
- `LoyaltyNotifications`;
- `NotificationDeliveries`;
- `NotificationType.BirthdayBenefitStarted`;
- `DeviceRegistrations`.

# Fase 5.6 - Campana de puntos visible en Apple Wallet

Fase 5.6 esta implementada y pendiente de validacion manual.

El objetivo es comunicar en Apple Wallet la mejor campana de puntos vigente aplicable a cada tarjeta instalada.

## Regla funcional

Una campana se considera vigente cuando:

- `PointCampaign.IsActive = true`;
- `StartsAtUtc <= nowUtc`;
- `EndsAtUtc >= nowUtc`.

Solo se consideran `Customer` y `LoyaltyCard` activos y con al menos un `DeviceRegistration`.

## Elegibilidad por nivel

La elegibilidad es jerarquica:

- `All`: todos los niveles;
- `Mist`: Mist, Glow y Radiance;
- `Glow`: Glow y Radiance;
- `Radiance`: solo Radiance.

La regla usa el nivel almacenado en `LoyaltyCard.Level`, calculado por niveles rolling de 12 meses. No usa `CurrentPoints`, `LifetimePoints` ni `PointsEarnedThisYear`.

## Seleccion de campana

Si varias campanas aplican a una tarjeta, se elige solo una:

1. mayor `Multiplier`;
2. menor `MinimumPurchaseAmount`;
3. `StartsAtUtc` mas reciente;
4. `Id` como desempate final.

El monto minimo no excluye a la tarjeta de la notificacion porque depende de una compra futura. Si existe, se muestra como condicion.

## Flujo

```text
LoyaltyMaintenanceBackgroundService
  ->
CreatePointCampaignStartedNotificationsCommand
  ->
IPointCampaignNotificationReadService
  ->
LoyaltyNotification PointCampaignStarted
  ->
AppleWalletNotificationChannelProcessor
  ->
APNs PassKit
  ->
Wallet descarga pass actualizado
```

El scheduler ejecuta ahora:

```text
1. ExpirePointsCommand
2. RecalculateLevelsCommand
3. CreatePointExpirationNotificationsCommand
4. CreateMonthlyProductStartedNotificationsCommand
5. CreateBirthdayBenefitStartedNotificationsCommand
6. CreatePointCampaignStartedNotificationsCommand
```

## Idempotencia

CorrelationId:

```text
point-campaign-started:{campaignId:N}:{serialNumber}
```

Esto permite una sola notificacion por campana y tarjeta. Reejecutar el scheduler no duplica `LoyaltyNotification`, `NotificationDelivery` ni APNs.

## Preview administrativo

Endpoint:

```text
GET /api/admin/campaigns/notification-candidates?timeZoneId=America/Tijuana&includeAlreadyNotified=false
```

Devuelve:

- `CustomerId`;
- `LoyaltyCardId`;
- `CustomerName`;
- `SerialNumber`;
- `Level`;
- `CampaignId`;
- `CampaignName`;
- `Multiplier`;
- `MinimumPurchaseAmount`;
- `StartsAtUtc`;
- `EndsAtUtc`;
- `EndsAtLocal`;
- `CorrelationId`;
- `AlreadyNotified`.

El preview no crea notificaciones, no crea deliveries, no envia APNs y no modifica base.

## Apple Wallet

Campo estable del frente:

```json
{
  "key": "point_campaign",
  "label": "PROMOCION",
  "value": "Puntos x5",
  "changeMessage": "🔥 ¡Promoción activa! %@"
}
```

El campo aparece solo mientras la campana siga vigente, activa, aplicable y sea la mejor campana para la tarjeta. El valor usa formato dinamico `Puntos xN`.

El reverso agrega:

```text
CAMPAÑA ACTIVA

{CampaignName}

Puntos x{Multiplier}

Vigente hasta {EndDateLocal}

Compra minima: ${MinimumPurchaseAmount:N0} MXN
```

La linea de compra minima solo aparece cuando existe `MinimumPurchaseAmount`.

## Prioridad temporal y fallback

La seleccion de eventos recientes usa:

1. evento mas nuevo por `ProcessedAt ?? CreatedAt`;
2. solo ante empate real, prioridad por tipo:
   - `LevelChanged`;
   - `BirthdayBenefitStarted`;
   - `PointsExpiring`;
   - `MonthlyProductStarted`;
   - `PointCampaignStarted`;
   - `Custom`.

Mientras el evento reciente este dentro de `LoyaltyNotifications:VisibleEventPriorityHours`, ocupa el unico campo temporal del frente y puede incluir `changeMessage`.

Cuando no hay evento reciente, el fallback permanente usa:

1. `points_expiring`;
2. `birthday_benefit`;
3. `point_campaign`;
4. `monthly_product`.

El fallback no incluye `changeMessage` para evitar alertas repetidas.

## Relacion con compras

Fase 5.6 no cambia `AddPointsHandler`. Las compras siguen usando el mayor multiplicador entre cumpleanos y campana vigente aplicable. No se acumulan multiplicadores.

## Migracion

Fase 5.6 no requiere migracion nueva. Reutiliza:

- `PointCampaign`;
- `LoyaltyNotifications`;
- `NotificationDeliveries`;
- `NotificationType.PointCampaignStarted`;
- `MetadataJson`;
- `DisplayUntilUtc`;
- `DeviceRegistrations`.
## Fase 5.7 - Centro de mensajes personalizados

El proyecto cuenta con un centro administrativo para crear, previsualizar, enviar y programar mensajes personalizados para clientas con Apple Wallet instalado.

La fase reutiliza el motor existente de notificaciones:

- `LoyaltyNotification`
- `NotificationDelivery`
- `NotificationType.Custom`
- `NotificationChannel.AppleWallet`
- `ILoyaltyNotificationService`
- `LoyaltyNotificationBackgroundService`
- `AppleWalletNotificationChannelProcessor`
- `IWalletNotificationReadService`
- `WalletNotificationReadService`
- `PassGeneratorService`
- `ScheduledAtUtc`
- `DisplayUntilUtc`
- `CorrelationId`

La entidad agregada es `CustomNotificationCampaign`. Guarda el contenido aprobado por administracion: `Title`, `ShortMessage`, `LongMessage`, audiencia, fecha programada, fecha limite de visualizacion, estado y metricas basicas.

El mensaje corto se muestra en el frente del pass como evento reciente:

```json
{
  "key": "custom_message",
  "label": "NOVEDAD",
  "value": "{ShortMessage}",
  "changeMessage": "📣 %@"
}
```

El mensaje largo se muestra en el reverso del pass:

```text
NOVEDAD

{Title}

{LongMessage}
```

El campo frontal `custom_message` solo aparece mientras la notificacion Custom sea un evento reciente dentro de `LoyaltyNotifications:VisibleEventPriorityHours`. El backField se mantiene mientras `DisplayUntilUtc` siga vigente.

Audiencias soportadas:

- `AllWalletUsers`
- `MistAndAbove`
- `GlowAndAbove`
- `RadianceOnly`
- `MinimumPoints`
- `PointsExpiring`

La audiencia se resuelve al momento de procesar la campana. En campanas programadas no se congela la audiencia al crearla.

Endpoints:

- `POST /api/custom-notification-campaigns/preview`
- `GET /api/custom-notification-campaigns`
- `GET /api/custom-notification-campaigns/{id}`
- `POST /api/custom-notification-campaigns`
- `POST /api/custom-notification-campaigns/{id}/send`
- `PUT /api/custom-notification-campaigns/{id}/cancel`

Admin:

- Ruta: `/marketing-notifications`
- Permite capturar titulo, mensaje corto, mensaje largo, audiencia, fecha de envio y fecha limite.
- Incluye contador visual de caracteres.
- Incluye preview de audiencia.
- Incluye vista previa simple del campo Wallet.
- Permite enviar ahora, programar y cancelar antes de procesar.

Migracion:

- `20260716120000_AddCustomNotificationCampaigns`
- Crea `CustomNotificationCampaigns`.
- Agrega `CustomNotificationCampaignId`, `ShortMessage` y `LongMessage` a `LoyaltyNotifications`.
- No se aplica automaticamente.
