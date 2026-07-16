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

## Niveles automaticos - Fase 3.4

Los niveles se calculan con puntos positivos elegibles de los ultimos 12 meses. La fuente de verdad es `PointTransactions`.

Tipos que cuentan para nivel:

- `Purchase`
- `BonusWelcome`
- `BonusBirthday`
- `BonusReferral`

Tipos que no cuentan:

- `Redemption`
- `RedemptionReversal`
- `Expired`
- `Expiry`

Endpoint manual de recalculo:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:55128/api/admin/levels/recalculate" `
  -Headers @{ "X-Operator-Id" = "dev-admin" }
```

Validacion SQL sugerida antes de recalcular:

```sql
DECLARE @WindowStart datetime2 = DATEADD(month, -12, SYSUTCDATETIME());

SELECT
    lc.SerialNumber,
    lc.Level AS StoredLevel,
    SUM(CASE
        WHEN pt.Points > 0
         AND pt.Type IN (
             'Purchase',
             'BonusWelcome',
             'BonusBirthday',
             'BonusReferral'
         )
         AND pt.CreatedAt >= @WindowStart
        THEN pt.Points ELSE 0 END) AS RollingEligiblePoints
FROM LoyaltyCards lc
LEFT JOIN PointTransactions pt ON pt.LoyaltyCardId = lc.Id
WHERE lc.IsActive = 1
GROUP BY lc.SerialNumber, lc.Level
ORDER BY lc.SerialNumber;
```

Escenarios manuales recomendados:

1. Crear una clienta nueva y confirmar que inicia en Mist.
2. Agregar compras suficientes para llegar a Glow y ejecutar `POST /api/admin/levels/recalculate`.
3. Confirmar que `Level` cambio, `LastActivityAt` se actualizo y Wallet recibe APNs si el pass esta registrado.
4. Crear un canje y confirmar que no reduce el progreso de nivel.
5. Cancelar el canje y confirmar que la reversa positiva no aumenta progreso de nivel.
6. Insertar o ajustar en desarrollo una transaccion elegible fuera de la ventana de 12 meses, recalcular y confirmar que el nivel puede bajar.

No hay scheduler diario implementado todavia. Mientras tanto, el recalculo puede ejecutarse manualmente con el endpoint administrativo.

## Mantenimiento diario - Fase 3.5

La API hospeda `LoyaltyMaintenanceBackgroundService`.

El servicio ejecuta automaticamente, una vez al dia:

```text
ExpirePointsCommand
  ↓
RecalculateLevelsCommand
```

No duplica reglas de expiracion, FIFO ni niveles. Solo crea un scope de DI, resuelve `ISender` y manda los commands ya existentes.

Configuracion local:

```json
"LoyaltyMaintenance": {
  "Enabled": true,
  "RunOnStartup": false,
  "RunAtLocalTime": "02:00",
  "TimeZoneId": "America/Tijuana"
}
```

`RunAtLocalTime` se interpreta en la zona horaria configurada. En Windows, `America/Tijuana` puede resolverse con fallback a `Pacific Standard Time (Mexico)`.

### Prueba A - RunOnStartup

Para validar una corrida inmediata en Development, cambiar temporalmente:

```json
"LoyaltyMaintenance": {
  "Enabled": true,
  "RunOnStartup": true,
  "RunAtLocalTime": "02:00",
  "TimeZoneId": "America/Tijuana"
}
```

Al iniciar la API debe verse en logs:

- ejecucion de mantenimiento al inicio;
- resultado de expiracion;
- resultado de recalculo de niveles;
- duracion total;
- siguiente ejecucion diaria programada.

No dejar `RunOnStartup=true` en configuracion compartida.

### Prueba B - Hora proxima

Cambiar temporalmente `RunAtLocalTime` a unos minutos despues de la hora actual local.

Ejemplo:

```json
"RunAtLocalTime": "14:35"
```

Iniciar API antes de esa hora y verificar:

- log de siguiente ejecucion en hora local y UTC;
- ejecucion en la hora indicada;
- expiracion de puntos;
- recalculo de niveles;
- nueva programacion para el dia siguiente.

No dejar esa hora temporal en commit.

### Fallos y advertencias

Si expiracion falla inesperadamente, el servicio registra el error y continua con recalculo de niveles porque ambos procesos son independientes.

Si recalculo falla, el error se registra y el hosted service espera la siguiente ejecucion programada.

El servicio asume una sola instancia del host. Para despliegues multi-instancia se debe agregar un distributed lock antes de habilitarlo en todas las instancias.

## Customer Detail avanzado - Fase 3.6

La pantalla Admin:

```text
/customers/{customerId}
```

incluye informacion de auditoria de puntos para soporte.

Validacion manual sugerida:

1. Abrir listado de clientas.
2. Entrar al detalle de una clienta con Wallet.
3. Confirmar que se muestran saldo disponible, rolling points, lifetime points, nivel y fecha de nivel.
4. Confirmar la seccion de proxima expiracion.
5. Validar tabla de lotes:
   - ganado;
   - expira;
   - monto original;
   - monto disponible;
   - estado.
6. Validar tabla de consumo FIFO en una clienta con canjes o expiraciones.
7. Confirmar que clientes sin lotes o sin consumos muestran mensajes amigables.
8. Confirmar que el historial de puntos muestra balance despues de cada movimiento visible.

La fase es solo lectura. No modifica FIFO, expiracion, Wallet, APNs, scheduler ni comandos existentes.

## Producto del mes - Fase 3.7

El catalogo de recompensas permite marcar una recompensa como Producto del mes.

Reglas:

- `IsMonthlyProduct=true` requiere `ValidFrom` y `ValidTo`.
- Solo puede existir un Producto del mes activo con vigencia traslapada.
- La vigencia se evalua en UTC.
- Un Producto del mes inactivo puede existir como borrador.
- Al activarlo se valida traslape.
- No se desactiva automaticamente ningun producto existente.

### Escenario 1 - Crear producto del mes

```http
POST /api/rewards
Content-Type: application/json
```

```json
{
  "name": "Producto del mes julio",
  "description": "Recompensa especial",
  "pointsCost": 500,
  "minLevel": "Mist",
  "isMonthlyProduct": true,
  "validFrom": "2026-07-01T00:00:00Z",
  "validTo": "2026-07-31T23:59:59Z",
  "isActive": true
}
```

Verificar que aparece como vigente en `/rewards`, aparece en catalogo para clientas elegibles y puede canjearse.

### Escenario 2 - Traslape

Intentar crear otro Producto del mes activo:

```json
{
  "name": "Otro producto julio",
  "description": "Debe fallar",
  "pointsCost": 400,
  "minLevel": "Mist",
  "isMonthlyProduct": true,
  "validFrom": "2026-07-15T00:00:00Z",
  "validTo": "2026-08-15T23:59:59Z",
  "isActive": true
}
```

Debe devolver error:

```text
Ya existe un Producto del mes activo con una vigencia que se traslapa.
```

### Escenario 3 - Producto futuro

Crear un Producto del mes para agosto sin traslapar. Debe mostrarse como `Programado` y no aparecer como canjeable antes de `ValidFrom`.

## Campanas de puntos - Fase 3.8

Las campanas se administran desde `/campaigns` y aplican automaticamente a `POST /api/points`.

La UI captura fecha/hora local de Tijuana y el backend persiste UTC. Las campanas no requieren scheduler: `IsActive` es el interruptor administrativo y la vigencia se evalua al momento de la compra.

Endpoints administrativos:

- `GET /api/campaigns`
- `GET /api/campaigns/{id}`
- `POST /api/campaigns`
- `PUT /api/campaigns/{id}`
- `PUT /api/campaigns/{id}/activate`
- `PUT /api/campaigns/{id}/deactivate`

Request ejemplo:

```json
{
  "name": "Triple puntos julio",
  "description": "Campana 3x para compras de julio",
  "multiplier": 3,
  "minimumPurchaseAmount": 500,
  "levelEligibility": "All",
  "startsAtUtc": "2026-07-01T07:00:00Z",
  "endsAtUtc": "2026-08-01T06:59:59Z",
  "isActive": true
}
```

`POST /api/points` no cambia su request:

```json
{
  "serialNumber": "KB-JHYL7KK",
  "purchaseAmount": 500
}
```

La respuesta ahora incluye datos compatibles adicionales: `basePoints`, `campaignBonusPoints`, `appliedMultiplier`, `campaignId` y `campaignName`.

Escenarios manuales:

1. Compra sin campana: `appliedMultiplier = 1`, `campaignId = null`, `pointsAdded = basePoints`.
2. Campana 2x vigente para todos: `pointsAdded = basePoints * 2`, una transaccion y un lote.
3. Monto minimo: compra de `499` no aplica; compra de `500` aplica si el minimo es `500`.
4. Nivel elegible: campana Glow no aplica a Mist y si aplica a Glow.
5. Campanas traslapadas: gana la de mayor multiplicador.
6. Cumpleanos mayor que campana: gana cumpleanos y `campaignId = null`.
7. Campana mayor que cumpleanos: gana campana y se guarda `campaignId`.
8. Empate cumpleanos/campana: se registra campana y `BirthdayBonusApplied = false`.
9. Campana futura: estado `Programada`, no aplica.
10. Campana vencida: estado `Vencida`, no aplica.
11. Campana inactiva: no aplica aunque la fecha sea vigente.
12. Admin: crear, editar, activar, desactivar y revisar hora Tijuana.
13. Auditoria SQL: revisar `PointTransactions.CampaignId`, `BasePoints`, `AppliedMultiplier`, `Points`.
14. Nivel rolling: confirmar que puntos finales cuentan para nivel.
15. FIFO/expiracion: confirmar que el lote de puntos finales expira normalmente.

Migracion:

- `AddPointCampaignsAndTransactionAudit`.
- No aplicar automaticamente; usar `dotnet ef database update` solo durante validacion local.
- No hay backfill historico.

### Escenario 4 - Producto vencido

Mover vigencia al pasado. Debe mostrarse como `Vencido` y no aparecer como canjeable.

### Escenario 5 - Activacion con conflicto

Crear un Producto del mes traslapado con `isActive=false`. La creacion puede guardarse como borrador. Al ejecutar:

```http
PUT /api/rewards/{id}/activate
```

debe fallar por traslape.

### Escenario 6 - Recompensa normal

Crear o editar una recompensa con `isMonthlyProduct=false`. Debe comportarse igual que antes.

### Escenario 7 - Edicion

Editar el Producto del mes vigente conservando su `Id`. La validacion excluye la misma recompensa y debe permitir guardar si no traslapa con otra.

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

Estado Fase 2.1:

- ✅ API CRUD de recompensas.
- ✅ Commands.
- ✅ Queries.
- ✅ Validators.
- ✅ DTOs.
- ✅ Repository actualizado.

## Reward Administration

Fase 2.2 agrega administracion visual de recompensas en el panel Admin.

Pagina:

```text
/rewards
```

Desde esta pantalla es posible:

- Listar recompensas.
- Crear recompensas.
- Editar recompensas.
- Activar recompensas.
- Desactivar recompensas.

La pagina usa MediatR in-process desde `KBeauty.Loyalty.Admin` y reutiliza los Commands/Queries de Fase 2.1. No accede directo a `AppDbContext`.

Estado Fase 2.2:

- ✅ Administracion visual de recompensas.
- ✅ Crear.
- ✅ Editar.
- ✅ Activar.
- ✅ Desactivar.

## Redemption Administration

Fase 2.3 agrega cancelacion de canjes pendientes desde el panel Admin.

Pagina:

```text
/redemptions
```

Desde esta pantalla es posible:

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

Estado Fase 2.3:

- ✅ Cancelacion de canjes.
- ✅ Restauracion automatica de puntos.
- ✅ Reversa de transacciones.
- ✅ Actualizacion automatica de Wallet despues de cancelar.

Estado Fase 2.4:

- ✅ Historial de canjes.
- ✅ Filtros por estado.
- ✅ Vista de fecha de solicitud.
- ✅ Vista de fecha de resolucion.
- ✅ Vista de operador.
- ✅ Vista de notas.

Estado Fase 2.5:

- ✅ Pulido visual y UX del Admin.
- ✅ Buscador por nombre/descripcion en `/rewards`.
- ✅ Filtro `Todas` / `Activas` / `Inactivas` en `/rewards`.
- ✅ Badges de estado y nivel en `/rewards`.
- ✅ Producto del mes visual en `/rewards`.
- ✅ Buscador por cliente, serial o recompensa en `/redemptions`.
- ✅ Filtros `Todos` / `Pendientes` / `Confirmados` / `Cancelados` en `/redemptions`.
- ✅ Badges de estado en `/redemptions`.
- ✅ Notas truncadas con tooltip cuando aplica.

## Dashboard / Analytics

Fase 3.1 agrega un Dashboard analitico en el Admin.

Pagina:

```text
/dashboard
```

Muestra KPIs de:

- Clientes.
- Puntos.
- Canjes.
- Recompensas.
- Actividad reciente.

Criterios usados:

- Clientes con Wallet emitida: clientes con `LoyaltyCard` existente.
- Clientes activos: clientes con al menos una transaccion de puntos o un canje.
- Puntos otorgados: suma de transacciones positivas.
- Puntos canjeados: suma absoluta de transacciones negativas de tipo `Redemption`.
- Balance total: suma de `LoyaltyCard.CurrentPoints`.

Prueba manual:

1. Levantar API/Admin como normalmente.
2. Abrir Admin.
3. Entrar a `Dashboard`.
4. Confirmar que las cards cargan.
5. Confirmar que no falla sin datos.
6. Crear o usar cliente existente.
7. Crear puntos, canje o recompensa.
8. Refrescar Dashboard y validar que las metricas cambian.

## Customer Detail

Fase 3.2 agrega una pantalla principal de detalle de cliente en el Admin.

Pagina:

```text
/customers/{customerId}
```

Desde el listado de clientes se accede con `Ver detalle`.

Muestra:

- Informacion general del cliente.
- Tarjeta Loyalty / Wallet.
- Estadisticas rapidas.
- Historial de puntos.
- Historial de canjes.
- Dispositivos Wallet registrados.

Consultas implementadas:

- `GetCustomerDetailQuery`.
- `GetCustomerDetailHandler`.
- `ICustomerDetailReadService`.
- `CustomerDetailReadService`.

Criterios usados:

- Wallet emitida: existe `LoyaltyCard`.
- Fecha de emision: `Customer.CreatedAt`, porque la tarjeta se crea junto con el cliente.
- Ultima actualizacion: `LoyaltyCard.LastActivityAt`.
- Ultimo push enviado: no existe dato persistido, se muestra `No disponible`.
- Balance despues del movimiento: no existe snapshot persistido, se muestra `No disponible`.

Prueba manual:

1. Abrir listado de clientes.
2. Entrar al detalle.
3. Validar informacion general.
4. Validar estadisticas.
5. Validar historial de puntos.
6. Validar historial de canjes.
7. Probar un cliente nuevo sin movimientos.
8. Confirmar que la pagina no genera errores.

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

## Expiracion de puntos con FIFO

Fase 3.3 agrega expiracion de puntos por lotes.

Configuracion en `ProgramConfig`:

- `points_expiration_enabled`: activa o desactiva el proceso.
- `points_expire_after_months`: meses de vigencia de cada lote. Debe ser mayor a `0`. Default actual: `12`.

Cada movimiento positivo crea un `PointLot`:

- Registro con bono de bienvenida.
- Compra/acumulacion de puntos.
- Bono de referido.

Cada canje consume lotes FIFO y crea `PointLotConsumption`. Si el canje se cancela, los consumos se marcan reversados y se restauran los puntos al lote original.

Endpoint manual para ejecutar expiracion:

```text
POST /api/admin/points/expire
```

Header recomendado:

```text
X-Operator-Id: admin
```

Respuesta esperada:

```json
{
  "runAt": "2026-07-10T00:00:00Z",
  "enabled": true,
  "clientsProcessed": 1,
  "clientsAffected": 1,
  "lotsExpired": 2,
  "pointsExpired": 150,
  "walletsNotified": 1,
  "warnings": []
}
```

La expiracion actualiza `LoyaltyCard.LastActivityAt` y envia APNs best-effort, por lo que Wallet debe refrescar igual que con compras, canjes y cancelaciones.

Migracion:

- `20260710090000_AddPointLotsForExpiration`

No aplicar automaticamente. Cuando se aplique manualmente con `dotnet ef database update`, la migracion:

1. Crea `PointLots`.
2. Crea `PointLotConsumptions`.
3. Inserta las claves de configuracion.

KBeauty Loyalty esta en etapa inicial. En Fase 3.3 no existe estrategia de migracion historica: no se reconstruyen lotes desde transacciones anteriores, no se aplican negativos historicos y no se validan saldos existentes. Para desarrollo local se recomienda borrar y recrear la base.

Prueba manual sugerida:

1. Borrar la base local si existe.
2. Aplicar migraciones en una base nueva.
3. Crear un cliente nuevo.
4. Agregar puntos.
5. Verificar que se crea un `PointLot`.
6. Crear un canje y confirmar que consume el lote mas antiguo.
7. Cancelar el canje y confirmar que el lote recupera `RemainingAmount`.
8. Ajustar temporalmente datos locales para tener un lote vencido.
9. Ejecutar `POST /api/admin/points/expire`.
10. Confirmar que se crea `PointTransaction` tipo `Expired`.
11. Confirmar que `CurrentPoints` baja y `LastActivityAt` cambia.
12. Confirmar en logs que APNs intenta notificar Wallet.
13. Ejecutar el endpoint de nuevo y confirmar que no duplica expiraciones.

Casos manuales recomendados:

- Lote de 100 pts con 12 meses cumplidos expira 100.
- Lote parcialmente consumido de 100 con 40 restantes expira 40.
- Dos lotes vencidos de la misma tarjeta generan una sola transaccion `Expired` por tarjeta.
- Canje consume primero el lote mas antiguo.
- Cancelacion de canje restaura puntos al lote original.
- Endpoint con `points_expiration_enabled=false` no modifica datos.
- Reejecutar expiracion no duplica descuento.
- Wallet recibe refresh porque `LastActivityAt` cambia y se envia APNs.

Regla futura no implementada en Fase 3.3:

- El nivel se calculara con puntos positivos ganados en una ventana movil de 12 meses.
- Los canjes no reduciran progreso de nivel.
- La expiracion no reducira progreso directamente.
- El nivel podra bajar cuando puntos antiguos salgan de la ventana.
- El recalculo ocurrira al otorgar puntos y en el proceso diario.

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

`CancelRedemptionHandler`:

1. Busca el canje por id.
2. Valida que este en estado `Pending`.
3. Cambia `Redemption` a `Cancelled`.
4. Aplica `LoyaltyCard.RestorePoints(...)`.
5. Crea `PointTransaction` positiva de tipo `RedemptionReversal`.
6. `RestorePoints` actualiza `LastActivityAt`.
7. Guarda cambios.
8. Busca registrations del serial.
9. Llama `IApnService.SendPassUpdateAsync`.
10. `ApnService` envia push HTTP/2 a APNs.

El push es best-effort: si APNs falla, la transaccion de puntos o canje no se revierte.

## Probar actualizacion automatica

Flujo esperado:

```text
POST /api/points, POST /api/redemptions o PUT /api/redemptions/{id}/cancel
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

Para cancelacion de canjes, el flujo completo es:

```text
Cancelacion
  ↓
Redemption Cancelled
  ↓
PointTransaction positiva de reversa
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

El mecanismo unico para detectar cambios es `LoyaltyCard.LastActivityAt`; compras, canjes y cancelaciones de canjes lo actualizan antes de enviar APNs.

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
- Las cancelaciones de canjes actualizan `LastActivityAt` mediante `LoyaltyCard.RestorePoints(...)` al restaurar puntos.
- Apple Wallet puede refrescar automaticamente despues de compras, canjes y cancelaciones de canjes.

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

## Motor de notificaciones - Fase 5.1

La API puede procesar notificaciones logicas de lealtad con Apple Wallet como unico canal real en esta fase.

Configuracion local:

```json
"LoyaltyNotifications": {
  "Enabled": true,
  "RunOnStartup": false,
  "PollIntervalSeconds": 60,
  "BatchSize": 25,
  "MaxAttempts": 3
}
```

Para validar manualmente:

1. Aplicar la migracion de Fase 5.1 en una base local de desarrollo.
2. Levantar Azurite, API, Admin y ngrok como en el flujo Wallet normal.
3. Instalar un pass en iPhone y confirmar que existe `DeviceRegistration`.
4. Abrir Admin en `/notifications`.
5. Crear una notificacion manual para el serial del pass.
6. Confirmar que la notificacion se procesa y registra metricas de Apple Wallet.
7. Confirmar que APNs despierta Wallet y que Wallet descarga el pass actualizado.
8. Abrir el reverso del pass y verificar la seccion `NOVEDADES`.

Tambien se puede validar el origen automatico:

1. Usar una clienta Mist con pass instalado.
2. Agregar puntos suficientes para subir a Glow.
3. Confirmar que se crea una notificacion `LevelChanged`.
4. Confirmar que Wallet refresca el pass y muestra la novedad activa.

Importante:

- APNs no transporta el texto de la notificacion.
- El texto se obtiene cuando Wallet descarga el nuevo `.pkpass`.
- La notificacion activa se lee con `IWalletNotificationReadService`.
- Si no hay devices registrados, la entrega queda como `NoRecipients`; la notificacion queda procesada pero no hubo push real.

## changeMessage visible - Fase 5.2

Fase 5.2 agrega `changeMessage` al campo estable de nivel del pass para validar alertas visibles de Apple Wallet en subidas de nivel.

El campo usado es:

```json
{
  "key": "level",
  "label": "NIVEL",
  "value": "Glow ✨",
  "changeMessage": "🎉 Ahora eres cliente %@"
}
```

La alerta visible depende de Apple Wallet. El sistema solo garantiza:

- APNs PassKit silencioso;
- cambio real del campo `level`;
- `changeMessage` con `%@`;
- pass regenerado con firma valida.

### Prueba manual

1. Instalar un pass en Mist.
2. Confirmar que existe `DeviceRegistration`.
3. Descargar o inspeccionar el pass inicial y confirmar:

```text
key = level
value = Mist ✨
```

4. Otorgar puntos suficientes para subir a Glow.
5. Revisar logs y confirmar:
   - `Creating LevelChanged notification`;
   - `Apple Wallet APNs summary`;
   - `Apple Wallet requested updated serials`;
   - `Apple Wallet downloading pass`;
   - `changeMessageIncluded=True`.
6. Confirmar que el pass actualizado contiene:

```text
key = level
value = Glow ✨
changeMessage = 🎉 Ahora eres cliente %@
```

7. Verificar si iOS muestra alerta visible.
8. Abrir Wallet y confirmar que el nivel visible es Glow.
9. Repetir un refresh sin cambio de nivel y confirmar que no se crea otra notificacion `LevelChanged`.
10. Probar downgrade y confirmar que no se incluye `changeMessage` de felicitacion.

No se debe intentar enviar texto dentro del payload APNs. El texto siempre vive dentro del `.pkpass`.

## Aviso de puntos por expirar - Fase 5.3

El mantenimiento diario ahora busca clientas con puntos que expiran exactamente dentro de 15 dias calendario en `America/Tijuana`.

El flujo es:

```text
LoyaltyMaintenanceBackgroundService
  ↓
ExpirePointsCommand
  ↓
RecalculateLevelsCommand
  ↓
CreatePointExpirationNotificationsCommand
  ↓
LoyaltyNotification PointsExpiring
  ↓
AppleWalletNotificationChannelProcessor
  ↓
APNs PassKit
  ↓
Wallet descarga pass actualizado
```

Preview administrativo:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "https://localhost:55128/api/admin/points/expiration-notification-candidates?daysAhead=15&timeZoneId=America/Tijuana"
```

El preview no crea notificaciones ni envia APNs.

Campo esperado en el pass cuando aplica:

```json
{
  "key": "points_expiring",
  "label": "POR EXPIRAR",
  "value": "250 pts",
  "changeMessage": "⚠️ %@ vencerán pronto."
}
```

### Escenarios manuales

Caso 1 - Crear aviso:

1. Preparar un `PointLot` con `RemainingAmount > 0`.
2. Ajustar `ExpiresAt` para que caiga dentro del dia local de Tijuana exactamente 15 dias adelante.
3. Ejecutar mantenimiento diario o usar `RunOnStartup=true` temporalmente.
4. Confirmar:
   - una `LoyaltyNotification` tipo `PointsExpiring`;
   - una `NotificationDelivery`;
   - APNs PassKit;
   - Wallet descarga el pass;
   - `points_expiring` aparece en el pass.

Caso 2 - Idempotencia:

1. Ejecutar mantenimiento otra vez el mismo dia.
2. Confirmar que no se crea otra notificacion para el mismo serial y fecha.

Caso 3 - Canje antes de expirar:

1. Consumir todos los puntos de los lotes proximos a expirar.
2. Refrescar Wallet.
3. Confirmar que `points_expiring` desaparece.

Caso 4 - Expiracion:

1. Dejar que los puntos expiren.
2. Ejecutar mantenimiento.
3. Confirmar que `points_expiring` desaparece despues de `ExpirePointsCommand`.

Caso 5 - Lotes multiples:

1. Crear dos lotes del mismo cliente que expiren el mismo dia local.
2. Confirmar que se crea una sola notificacion con la suma.

No se implementan email, SMS, WhatsApp, campanas ni cumpleanos en esta fase.

## Producto del mes visible - Fase 5.4

Fase 5.4 agrega notificacion automatica Apple Wallet cuando existe un Producto del mes activo y vigente.

Flujo:

```text
LoyaltyMaintenanceBackgroundService
  ->
CreateMonthlyProductStartedNotificationsCommand
  ->
LoyaltyNotification MonthlyProductStarted
  ->
AppleWalletNotificationChannelProcessor
  ->
APNs PassKit
  ->
Wallet descarga pass actualizado
```

Preview administrativo:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "https://localhost:55128/api/admin/rewards/monthly-product-notification-candidates?timeZoneId=America/Tijuana"
```

El preview no crea notificaciones ni envia APNs.

Campo esperado en el pass cuando aplica:

```json
{
  "key": "monthly_product",
  "label": "PRODUCTO DEL MES",
  "value": "Centella Ampoule",
  "changeMessage": "🎁 Nuevo Producto del mes: %@"
}
```

Detalle esperado en reverso:

```text
PRODUCTO DEL MES

Centella Ampoule

700 pts

Disponible hasta 31 jul 2026
```

### Escenarios manuales

Caso 1 - Producto vigente:

1. Crear un reward con `IsMonthlyProduct = true`, `IsActive = true`, `ValidFrom <= nowUtc` y `ValidTo >= nowUtc`.
2. Confirmar que al menos una tarjeta activa tenga `DeviceRegistration`.
3. Ejecutar preview.
4. Confirmar que aparece producto vigente y tarjetas candidatas.
5. Activar `LoyaltyMaintenance:RunOnStartup=true` temporalmente o esperar scheduler.
6. Confirmar:
   - una `LoyaltyNotification` tipo `MonthlyProductStarted` por tarjeta elegible;
   - una `NotificationDelivery` Apple Wallet;
   - APNs PassKit;
   - Wallet descarga el pass;
   - el campo `monthly_product` aparece.

Caso 2 - Idempotencia:

1. Ejecutar scheduler otra vez.
2. Confirmar `NotificationsCreated = 0`.
3. Confirmar `AlreadyNotified > 0`.
4. Confirmar que no hay duplicados por `CorrelationId`.

Caso 3 - Producto futuro:

1. Crear Producto del mes activo con `ValidFrom` futuro.
2. Ejecutar preview.
3. Confirmar que no hay producto vigente ni candidatos.
4. Confirmar que el pass no muestra `monthly_product`.

Caso 4 - Producto vencido:

1. Ajustar `ValidTo` al pasado.
2. Refrescar Wallet.
3. Confirmar que `monthly_product` desaparece.

Caso 5 - Producto inactivo:

1. Desactivar el Producto del mes.
2. Refrescar Wallet.
3. Confirmar que `monthly_product` desaparece.

Caso 6 - Cambio de producto:

1. Terminar Producto A.
2. Crear o activar Producto B vigente.
3. Ejecutar scheduler.
4. Confirmar nueva notificacion con correlation del reward B.
5. Confirmar que el value del campo cambia y Wallet puede mostrar alerta.

Caso 7 - Sin DeviceRegistration:

1. Crear cliente activo sin pass instalado.
2. Ejecutar preview.
3. Confirmar que no aparece como candidato.
4. Documentar que no se crea notificacion porque no hay canal Apple Wallet real.

Caso 8 - Pass:

1. Descargar `.pkpass`.
2. Confirmar manifest y firma.
3. Confirmar que los endpoints `/v1/*` siguen intactos.
4. Confirmar que la fecha del reverso usa fecha local de Tijuana.
5. Confirmar que el frente no se rediseno.

## Beneficio de cumpleanos visible - Fase 5.5

Fase 5.5 agrega notificacion automatica Apple Wallet cuando una clienta entra en su mes de cumpleanos.

La regla existente se conserva:

- el beneficio aplica durante todo el mes de cumpleanos;
- el multiplicador viene de `ProgramConfig:birthday_multiplier`;
- una compra usa el mayor multiplicador entre cumpleanos y campana;
- los multiplicadores no se acumulan.

Flujo:

```text
LoyaltyMaintenanceBackgroundService
  ->
CreateBirthdayBenefitStartedNotificationsCommand
  ->
LoyaltyNotification BirthdayBenefitStarted
  ->
AppleWalletNotificationChannelProcessor
  ->
APNs PassKit
  ->
Wallet descarga pass actualizado
```

Preview administrativo:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "https://localhost:55128/api/admin/customers/birthday-notification-candidates?timeZoneId=America/Tijuana"
```

El preview no crea notificaciones ni envia APNs.

Campo esperado en el pass cuando aplica y no hay campo temporal de mayor prioridad:

```json
{
  "key": "birthday_benefit",
  "label": "CUMPLEAÑOS",
  "value": "Puntos x2",
  "changeMessage": "🎂 Tu beneficio de cumpleaños está activo: %@"
}
```

Prioridad visual de campos temporales:

Primero se evalua si existe un evento visible reciente dentro de `LoyaltyNotifications:VisibleEventPriorityHours`.

Prioridad de eventos recientes:

1. `LevelChanged`;
2. `BirthdayBenefitStarted`;
3. `PointsExpiring`;
4. `MonthlyProductStarted`;
5. `PointCampaignStarted`;
6. `Custom`.

Cuando no hay evento reciente, se usa la prioridad permanente:

1. `points_expiring`;
2. `birthday_benefit`;
3. `point_campaign`.
4. `monthly_product`.

El fallback permanente no agrega `changeMessage`; asi se evita una segunda alerta cuando el frente vuelve de `birthday_benefit` a `points_expiring`.

### Escenarios manuales

Caso 1 - Cliente en mes de cumpleanos:

1. Configurar `DateOfBirth` del cliente con el mes local actual.
2. Confirmar que tiene pass instalado y `DeviceRegistration`.
3. Ejecutar preview.
4. Confirmar candidato con `AlreadyNotified=false` y multiplicador correcto.
5. Ejecutar mantenimiento diario o usar `RunOnStartup=true` temporalmente.
6. Confirmar:
   - `LoyaltyNotification` tipo `BirthdayBenefitStarted`;
   - `NotificationDelivery` Apple Wallet;
   - APNs;
   - refresh del pass;
   - campo `birthday_benefit`;
   - backField de beneficio de cumpleanos.

Caso 2 - Idempotencia:

1. Ejecutar scheduler otra vez.
2. Confirmar `NotificationsCreated=0`.
3. Confirmar `AlreadyNotified=1`.
4. Confirmar que no hay APNs duplicado.

Caso 3 - Fuera del mes:

1. Cambiar `DateOfBirth` a otro mes.
2. Refrescar pass.
3. Confirmar que `birthday_benefit` desaparece.

Caso 4 - Sin DeviceRegistration:

1. Cliente activo sin pass instalado.
2. Ejecutar preview.
3. Confirmar que no aparece como candidato.

Caso 5 - Campana y cumpleanos:

1. Crear campana con multiplicador mayor.
2. Confirmar que Wallet sigue mostrando beneficio de cumpleanos.
3. Confirmar que una compra usa el multiplicador mayor y no suma multiplicadores.

Caso 6 - Cambio de ano:

1. Confirmar que el correlation del ano siguiente cambia.
2. Confirmar que permite nueva notificacion anual.

Caso 7 - Visual:

1. Probar cumpleanos solo.
2. Probar cumpleanos + puntos por expirar.
3. Probar cumpleanos + Producto del mes.
4. Probar los tres simultaneamente.
5. Confirmar prioridad temporal del frente.

Caso 8 - Fin de prioridad reciente:

1. Simular `ProcessedAt` anterior a `VisibleEventPriorityHours`.
2. Refrescar pass.
3. Confirmar que vuelve el estado permanente mas relevante.
4. Confirmar que no se crea otra `LoyaltyNotification`.
5. Confirmar que no se crea otro `NotificationDelivery`.
6. Confirmar que no se envia APNs adicional.

Antes de commit final, dejar:

```json
"LoyaltyMaintenance": {
  "RunOnStartup": false
}
```

## Campana de puntos visible - Fase 5.6

Fase 5.6 agrega notificacion automatica Apple Wallet cuando existe una campana de puntos vigente y aplicable a una clienta con pass instalado.

Reglas:

- solo `PointCampaign.IsActive = true`;
- `StartsAtUtc <= nowUtc`;
- `EndsAtUtc >= nowUtc`;
- customer y loyalty card activos;
- al menos un `DeviceRegistration`;
- se usa el nivel almacenado en `LoyaltyCard.Level`;
- `Mist` aplica a Mist, Glow y Radiance;
- `Glow` aplica a Glow y Radiance;
- `Radiance` aplica solo a Radiance;
- `All` aplica a todos.

Si varias campanas aplican, se notifica solo la mejor:

1. mayor multiplicador;
2. menor compra minima;
3. inicio mas reciente;
4. `Id`.

El monto minimo no excluye del aviso porque depende de una compra futura.

Preview administrativo:

```powershell
Invoke-RestMethod `
  -Method Get `
  -Uri "https://localhost:55128/api/admin/campaigns/notification-candidates?timeZoneId=America/Tijuana"
```

El preview no modifica base ni envia APNs.

Flujo scheduler:

```text
LoyaltyMaintenanceBackgroundService
  ->
CreatePointCampaignStartedNotificationsCommand
  ->
LoyaltyNotification PointCampaignStarted
  ->
AppleWalletNotificationChannelProcessor
  ->
APNs PassKit
  ->
Wallet descarga pass actualizado
```

Campo esperado en el pass cuando el evento reciente gana:

```json
{
  "key": "point_campaign",
  "label": "PROMOCION",
  "value": "Puntos x3",
  "changeMessage": "🔥 ¡Promoción activa! %@"
}
```

Reverso esperado:

```text
CAMPAÑA ACTIVA

Nombre de la campaña

Puntos x3

Vigente hasta 31 jul 2026
```

Si existe monto minimo:

```text
Compra minima: $500 MXN
```

### Escenarios manuales

Caso 1 - Campana All vigente:

1. Crear campana 3x activa, vigente y `LevelEligibility=All`.
2. Ejecutar preview.
3. Confirmar que aparecen tarjetas activas con DeviceRegistration.
4. Ejecutar scheduler.
5. Confirmar `LoyaltyNotification.Type=PointCampaignStarted`.
6. Confirmar `NotificationDelivery.Channel=AppleWallet`.
7. Confirmar APNs y refresh del pass.
8. Confirmar alerta: `🔥 ¡Promoción activa! Puntos x3`.

Caso 2 - Idempotencia:

1. Ejecutar scheduler otra vez.
2. Confirmar `NotificationsCreated=0`.
3. Confirmar `AlreadyNotified > 0`.
4. Confirmar que no hay duplicados por `CorrelationId`.

Caso 3 - Jerarquia de niveles:

1. Campana Mist: validar Mist, Glow y Radiance.
2. Campana Glow: validar Glow y Radiance.
3. Campana Radiance: validar solo Radiance.

Caso 4 - Dos campanas vigentes:

1. Crear campana A 2x.
2. Crear campana B 5x.
3. Confirmar que se notifica solo B para tarjetas elegibles.
4. Confirmar `value = Puntos x5`.

Caso 5 - Monto minimo:

1. Crear campana 4x con minimo 500.
2. Confirmar que se notifica.
3. Confirmar que el reverso muestra compra minima.
4. Confirmar que `AddPointsHandler` solo aplica la campana si la compra cumple el minimo.

Caso 6 - Cumpleanos contra campana:

1. Cumpleanos 5x y campana 3x: la compra usa 5x.
2. Cumpleanos 2x y campana 4x: la compra usa 4x y registra `CampaignId`.
3. Confirmar que los multiplicadores no se acumulan.

Caso 7 - Evento reciente simultaneo:

1. Crear `BirthdayBenefitStarted` y `PointCampaignStarted`.
2. Confirmar que gana el mas reciente.
3. Solo ante empate aplica prioridad por tipo.

Caso 8 - Fin de prioridad temporal:

1. Mover `ProcessedAt` fuera de `VisibleEventPriorityHours`.
2. Refrescar pass.
3. Confirmar fallback permanente.
4. Confirmar que no se crea otra notificacion ni APNs.

Caso 9 - Campana vencida o inactiva:

1. Vencer o desactivar campana.
2. Refrescar pass.
3. Confirmar que desaparece el campo.
4. Confirmar que no se genera aviso de finalizacion.

Caso 10 - Sin DeviceRegistration:

1. Crear cliente activo sin pass instalado.
2. Ejecutar preview.
3. Confirmar que no aparece como candidato.

Antes de commit final, dejar:

```json
"LoyaltyMaintenance": {
  "RunOnStartup": false
}
```
