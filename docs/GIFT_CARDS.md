# Gift Cards

## Alcance

Gift Cards es un módulo de valor almacenado por tenant. LoyaltyCloud registra la emisión y el saldo, pero no procesa el cobro ni usa Stripe. El comercio debe registrar una venta realizada externamente o una emisión promocional/manual.

El feature toggle se guarda en `GiftCardConfigurations.IsEnabled` y empieza deshabilitado. Cuando está apagado no aparece en la navegación y las operaciones comerciales son rechazadas.

## Modelo e invariantes

- `GiftCard`: saldo actual, beneficiario, vigencia, estado y token de claim hasheado.
- `GiftCardTransaction`: ledger auditable de emisión, canje, ajuste, expiración y cancelación.
- `GiftCardWallet`: vínculo separado para Apple/Google; no reutiliza `MemberDigitalWallet`.
- `GiftCardConfiguration` y `GiftCardDenomination`: reglas y branding por tenant.
- Todas las entidades son `ITenantOwned`, tienen filtros EF globales y validación tenant al guardar.
- El saldo nunca puede quedar negativo.
- El canje parcial respeta la configuración del tenant.
- Canjes, ajustes y cancelaciones requieren idempotency key; el índice único evita duplicados concurrentes.
- `RowVersion` aplica concurrencia optimista al saldo.
- El claim público guarda únicamente SHA-256 del token y deja de funcionar al cancelar.

## Flujo operativo

1. Habilitar el módulo en `/giftcards/settings`.
2. Configurar moneda, expiración, denominaciones y branding.
3. Emitir en `/giftcards/issue`. La pantalla deja claro que el cobro ocurrió fuera de LoyaltyCloud.
4. Compartir el claim seguro o enviar email cuando SMTP está habilitado.
5. Consultar/canjear en `/giftcards/redeem` usando código, lector tipo teclado o cámara real (jsQR + MediaStream).
6. Revisar ledger en `/giftcards/cards/{id}` y KPIs en `/giftcards/reports`.

## Wallet

Google Wallet usa `genericClass` por tenant y `genericObject` por Gift Card. Los IDs incluyen TenantId y no comparten clases de membresía. Saldo, estado y vigencia se actualizan después de cada movimiento; un fallo del proveedor queda en `GiftCardWallet` y nunca revierte el canje.

Apple Wallet genera un `.pkpass` `storeCard` específico por Gift Card con saldo, vigencia, destinatario y QR. Reutiliza la configuración de certificados y APNs, pero mantiene serial, authentication token hasheable/aleatorio, estado de sync y `GiftCardDeviceRegistration` separados de membresía. Los endpoints PassKit existentes soportan descarga, registro, unregister y consulta de cambios. Tras canje, ajuste, cancelación o expiración se solicita actualización; un error de APNs se registra y nunca revierte el ledger.

La cámara reutiliza `jsQR` y Web Media APIs. Permite cámara trasera, cambio de dispositivo y entrada manual. Detiene todos los `MediaStreamTrack` al detectar, cancelar, navegar o disponer el componente.

## Expiración

El job `LoyaltyMaintenanceBackgroundService` llama `ExpireDueAsync` dentro del scope de cada tenant operativo. Cambia el estado una sola vez y crea un movimiento `Expired` auditable con actor de sistema.

## Base de datos

Aplicar las migraciones `AddGiftCards` y `AddGiftCardAppleWallet` antes de habilitar el módulo:

```powershell
dotnet ef database update --project .\src\LoyaltyCloud.Infrastructure --startup-project .\src\LoyaltyCloud.API
```

No se debe ejecutar esta instrucción automáticamente contra STG/PROD.

## Configuración externa

- SMTP es opcional y reutiliza la configuración de email transaccional existente.
- Google Wallet requiere `GoogleWallet:IssuerId` y Service Account mediante secretos externos.
- Apple requiere Pass Type ID, Team ID, certificados y endpoints mediante secretos externos.
- Ninguna credencial se persiste en las tablas Gift Cards.

## Checklist QA

- Confirmar que el módulo está oculto/deshabilitado inicialmente.
- Habilitarlo en un tenant QA y comprobar que otro tenant sigue oculto y no puede consultar sus datos.
- Emitir denominación fija y monto personalizado según configuración.
- Verificar claim, email opcional, Google Wallet y branding tenant-aware.
- Ejecutar canje parcial, reintentar con la misma idempotency key y confirmar un solo movimiento.
- Intentar sobregiro, tarjeta cancelada y tarjeta expirada.
- Confirmar actualización Wallet posterior al canje.
- Validar dashboard, filtros, ledger y responsive.


## Tenant QA local

En Development el seeder crea de forma idempotente `giftcards-qa` (`LoyaltyCloud Gift Cards QA`), configuración habilitada, MXN, expiración a 12 meses, canje parcial, montos personalizados, denominaciones 250/500/1000 y dos miembros. Incluye cinco tarjetas:

- `GC-QA-ACTIVE-500`: 500 / saldo 500 / Active.
- `GC-QA-PARTIAL-1000`: 1000 / saldo 750 / Active.
- `GC-QA-FULL-250`: 250 / saldo 0 / FullyRedeemed.
- `GC-QA-EXPIRED-500`: 500 / saldo no usado 500 / Expired.
- `GC-QA-EXTERNAL-1000`: 1000 / saldo 600 / receptor externo.

El seeder no contiene contraseña. Para habilitar login, configurar `DevelopmentTenants:GiftCardsQA:AdminUsername` y `DevelopmentTenants:GiftCardsQA:AdminPassword` mediante User Secrets o variables de entorno y reiniciar Admin. Si faltan, el tenant y los datos se crean, pero el usuario se omite deliberadamente.

## Email, idempotencia y concurrencia

Con notificaciones email desactivadas, la emisión/claim siguen funcionando y no se llama al proveedor. Al activarlas, el mensaje incluye remitente, mensaje personal y claim URL; los campos de usuario se codifican para evitar XSS y los tests usan un sender falso.

Cada mutación exige una idempotency key y existe un índice único `(TenantId, IdempotencyKey)`. `GiftCard.RowVersion` protege operaciones concurrentes: solo una escritura puede confirmar una versión; el saldo y ledger se validan con SQL Server LocalDB en tests.

## Seguridad y límites conocidos

- Código público aleatorio de 48 bits para búsqueda operativa; no concede acceso de claim.
- Claim token aleatorio de 256 bits; solo se persiste SHA-256.
- Saldos `decimal(18,2)`, redondeo a dos decimales, sin `float`/`double`.
- Filtros globales y guard de escritura por tenant; endpoints Wallet resuelven y validan tenant operativo.
- Mensajes/nombres se codifican al generar HTML; URLs de claim se derivan de configuración, sin redirect suministrado por usuario.
- La cámara requiere HTTPS o localhost y permiso del navegador.
- Development genera paquete Apple no firmado para QA estructural. STG/PROD requieren certificado/pass type/APNs externos y paquete firmado real.
- La entrega del proveedor Wallet es eventual; la base LoyaltyCloud siempre es autoridad.