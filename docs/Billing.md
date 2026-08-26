# Billing & Payments

Billing cobra la suscripción SaaS del `Tenant` y está separado de clientes, puntos y Wallet. `TenantSubscription` conserva la autoridad sobre `PaidThroughUtc`; órdenes, transacciones y webhooks mantienen el historial monetario.

## Rutas

- `/platform/billing-settings`: configuración y planes (SuperAdmin).
- `/platform/billing-transfers`: aprobación de transferencias (SuperAdmin).
- `/{tenantSlug}/billing`: renovación e historial del tenant.
- `POST /api/billing/webhooks/stripe`: webhook firmado.

Una URL de éxito, una transferencia solicitada o un comprobante nunca activan la suscripción. Solo un webhook válido o una aprobación explícita cambia la orden a `Paid`.

## Stripe

Configurar en User Secrets, variables de entorno o Azure Key Vault, tanto en Admin como API:

```text
Stripe__Enabled=true
Stripe__SecretKey=<secret>
Stripe__PublishableKey=<publishable>
Stripe__WebhookSecret=<webhook-signing-secret>
```

El webhook debe apuntar a `https://<api-host>/api/billing/webhooks/stripe`. Si Stripe está deshabilitado o incompleto, la aplicación arranca y el pago con tarjeta no se ofrece.

Para pruebas locales:

```powershell
stripe listen --forward-to https://localhost:<api-port>/api/billing/webhooks/stripe
```

## Puesta en marcha

1. Aplicar la migración `AddBillingPayments`.
2. Configurar moneda, IVA, métodos y transferencia en Billing Settings.
3. Capturar los cuatro precios y activar el plan; no existen precios comerciales predeterminados.
4. Configurar Stripe y probar primero en test mode.

Los tenants suspendidos por impago o trial vencido pueden autenticarse únicamente para Billing. Las páginas operativas continúan bloqueadas.

Antes de crear una orden, la pantalla solicita al backend una cotización con subtotal, IVA, total y moneda. El backend vuelve a ejecutar el mismo cálculo al crear la orden; el cliente nunca envía ni decide importes.

## Validaciones

- La autorización de un tenant suspendido acepta exclusivamente su ruta de Billing, los resultados de pago y logout; cualquier ruta operativa redirige a Billing.
- BillingOrder y PaymentTransaction conservan TenantId, y las consultas de tenant filtran también por ese identificador.
- Los eventos Stripe se deduplican por (Provider, ProviderEventId) y las transacciones por (Provider, ExternalTransactionId).
- Aprobar por segunda vez una transferencia pagada no vuelve a extender la suscripción.
- La vigencia usa comparación estricta contra UTC: al llegar a la fecha de expiración deja de ser operativa.
La carga privada de comprobantes queda pendiente: `IStorageService` actual pertenece exclusivamente a pases Wallet y reutilizarlo mezclaría responsabilidades.

## Renovación automática con Stripe

La vigencia operativa continúa siendo responsabilidad de `TenantSubscription` y solo `invoice.paid` la extiende. Los identificadores y el estado del proveedor viven en `TenantBillingProfile`, uno por tenant. `AutoRenewEnabled` tiene default de dominio y constraint SQL en `true`; la migración `AddRecurringBilling` crea perfiles para tenants existentes con renovación activa.

El perfil conserva exclusivamente datos seguros: `StripeCustomerId`, `StripeSubscriptionId`, status, fin del periodo, `CancelAtPeriodEnd`, periodo/monto recurrente, marca/últimos cuatro y `BillingContactEmail`. No se almacenan PAN, CVC, secretos ni payloads completos.

Con renovación activa, Checkout usa `mode=subscription` y un Price configurable por periodo (1, 3, 6 o 12 meses). Los IDs se guardan en `SubscriptionPlan`; no están hardcodeados. Con renovación desactivada, el pago con tarjeta conserva `mode=payment`. Stripe Customer se reutiliza una vez asociado al perfil.

Al apagar la opción, primero se actualiza Stripe con `cancel_at_period_end=true` y solo después se persiste localmente; el acceso pagado no se corta. Reactivarla antes del vencimiento revierte el flag sobre la misma Subscription. Si Stripe elimina la Subscription, el perfil queda sin renovación, pero `PaidThroughUtc` se conserva.

Eventos requeridos en Stripe Dashboard:

- `checkout.session.completed`
- `invoice.upcoming`
- `invoice.paid`
- `invoice.payment_failed`
- `customer.subscription.updated`
- `customer.subscription.deleted`

Cada evento valida la firma y se deduplica por Event ID. Las facturas pagadas se protegen además con el índice único de transacción por Invoice ID. `invoice.upcoming` genera el aviso previo (configurar Stripe para aproximadamente tres días); `invoice.payment_failed` no extiende vigencia y aplica el GracePeriod; `invoice.paid` crea historial de renovación y extiende una sola vez.

Las notificaciones usan `IBillingNotificationService`. En este slice el adaptador registra/encola el intento de email y omite de forma segura tenants sin `BillingContactEmail`; debe conectarse al proveedor transaccional de Azure antes de producción. El correo se configura en Billing y nunca se deduce del username.

### Configuración

Azure conserva `Stripe__Enabled`, `Stripe__SecretKey`, `Stripe__PublishableKey` y `Stripe__WebhookSecret`. No se agregó ningún secreto. Configure en cada plan los Price IDs de Test/Live correspondientes a 1, 3, 6 y 12 meses.

Para smoke local use Stripe Test Mode y `stripe listen --forward-to https://localhost:55128/api/billing/webhooks/stripe`. Stripe Test Clocks son apropiados para avanzar una Subscription de prueba hasta `invoice.upcoming` y `invoice.paid`; son solo una herramienta de prueba y no forman parte del modelo productivo. El Customer debe crearse bajo un Test Clock antes de iniciar la Subscription. Customer Portal queda como integration point futuro para actualizar tarjeta; LoyaltyCloud no captura tarjetas directamente.

## Email transaccional de Billing

Las notificaciones por email son opcionales y quedan **OFF por default**. Su configuración funcional no sensible vive en el registro singleton de BillingSettings y se administra exclusivamente por SuperAdmin desde /platform/billing-settings: estado, proveedor, remitente y URL pública. Billing, Stripe, renovaciones y webhooks siguen operando cuando email está deshabilitado o incompleto; no se intenta abrir una conexión SMTP.

IBillingNotificationService consulta esta configuración antes de usar ITransactionalEmailSender. El destinatario sigue siendo exclusivamente TenantBillingProfile.BillingContactEmail. Un fallo del proveedor se registra de forma segura y nunca revierte pagos, órdenes, transacciones, vigencia ni eventos procesados.

### Cloudflare Email Service

El proveedor inicial es Cloudflare Email Service mediante SMTP con TLS implícito. Los defaults técnicos versionables son Email__SmtpHost=smtp.mx.cloudflare.net, Email__SmtpPort=465 y Email__Username=api_token.

El único secreto de email es Email__Password. Debe almacenarse en Azure Key Vault, Azure App Settings o User Secrets; nunca se guarda en DB, nunca se muestra en la UI y la aplicación solo expone el booleano CredentialsConfigured.

Para habilitarlo posteriormente:

1. Completar el onboarding del dominio y sus registros SPF/DKIM/bounce en Cloudflare.
2. Crear un API token de mínimo privilegio y guardar Email__Password externamente.
3. En Billing Settings configurar FromAddress, FromName y ApplicationBaseUrl.
4. Usar URL HTTPS fuera de Development y activar el toggle.
5. Verificar el estado “Configurada” y hacer la primera prueba con un destinatario controlado.

El backend impide habilitar email si falta proveedor, remitente, URL válida o credenciales externas. El botón de correo de prueba queda como pendiente deliberado; no se agregó para evitar envíos accidentales mientras el feature está OFF.

### Stripe Price IDs y Checkout

SuperAdmin configura por plan Stripe Price ID para 1, 3, 6 y 12 meses. Con AutoRenew activo, el ID del periodo es obligatorio. Si falta, se rechaza antes de crear BillingOrder o llamar a Stripe. Los Price IDs son configurables y no secretos.

### Checklist STG/PROD

- Aplicar migraciones en orden: AddBillingPayments, AddRecurringBilling, AddWalletCardBranding, AddBillingEmailSettings.
- Configurar cuatro Stripe Price IDs test/live según ambiente.
- Configurar Stripe__Enabled, Stripe__SecretKey, Stripe__PublishableKey y Stripe__WebhookSecret.
- Habilitar exactamente checkout.session.completed, invoice.upcoming, invoice.paid, invoice.payment_failed, customer.subscription.updated y customer.subscription.deleted.
- Mantener email OFF o configurar Cloudflare, Email__Password, Billing Settings y BillingContactEmail.
- Probar Checkout recurrente, toggle OFF/ON, pago exitoso/fallido/upcoming, historial y tarjeta enmascarada.
- Confirmar firma, idempotencia Event/Invoice, aislamiento tenant, suspended billing access y ausencia de Developer Login en Production.
- Ejecutar restore, build, suite completa y git diff --check.

### Test Clocks y limitaciones conocidas

Stripe Test Clocks son la opción preferida para avanzar un Customer de Test Mode por creación, upcoming, invoice paid y siguiente periodo. Requieren credenciales Test, Prices recurrentes y crear el Customer bajo el clock antes de la Subscription; no forman parte del dominio productivo.

Customer Portal sigue pendiente. No hay reintentos propios de email. Email no puede habilitarse sin ApplicationBaseUrl válida; fuera de Development debe usar HTTPS. El smoke real Stripe/Cloudflare depende de credenciales y recursos externos no versionados.
