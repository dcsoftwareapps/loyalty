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
