# Roadmap

## Estado Actual

LoyaltyCloud esta en RC1 / UAT real.

La base activa de produccion/UAT es `LoyaltyCloudFree`.

## DONE

- Apple Wallet firmado y descargable.
- APNs y Device Registration.
- Web Service PassKit `/v1/*`.
- Registro publico tenant-aware `/{tenantSlug}/join`.
- API publica `POST /api/public/{tenantSlug}/join`.
- Admin tenant-aware `/{tenantSlug}/login`.
- Platform Admin `/platform/login` y `/platform/tenants`.
- Multi-tenant foundation sin tenant KBeauty especial.
- Eliminacion del seed productivo de KBeauty.
- Provisioning de tenants desde Platform Admin.
- Tenant Admin por tenant.
- TenantContext en Blazor Interactive Server.
- Guardrail: `/platform/*` sin TenantContext.
- Hard delete tenant desde Platform Admin.
- Sesion Tenant Admin persistente de 168 horas con sliding expiration.
- Dashboard / Analytics.
- Customer Detail.
- Customer Detail avanzado para auditoria de puntos.
- Sumar puntos con QR/camara.
- Canjear puntos con QR/camara.
- Reward Catalog API y Admin.
- Canjes, historial, confirmacion y cancelacion con restauracion FIFO.
- PointLots, FIFO, PointLotConsumptions y expiracion de puntos.
- Niveles dinamicos por tenant.
- Recalculo de niveles por tenant.
- Campanas de puntos.
- Producto del mes.
- Mensajes personalizados Apple Wallet.
- Motor base de notificaciones.
- Notificaciones visibles Apple Wallet con `changeMessage`.
- LevelChanged, PointsAdded, PointsExpiring, MonthlyProductStarted, BirthdayBenefitStarted, PointCampaignStarted y Custom.
- Prioridad temporal de eventos visibles recientes.
- Scheduler de mantenimiento cada 12 horas.
- Processor de notificaciones cada 60 segundos.
- Quick Help `/quick-help`.
- QR imprimible de registro con QRCoder.
- Guardrail contra hostname Admin antiguo `loyaltycloud-admin-894839`.
- Branding tenant-aware en Admin.
- Logo por tenant para Apple Wallet.
- Fallback grafico neutral de Wallet sin texto `LC`.
- Wallet pass con fondo claro, valores negros y labels en PrimaryColor.

## RC1 / UAT

- Crear tenants reales desde Platform Admin.
- Configurar KBeauty como tenant UAT, no como seed.
- Subir logo real de KBeauty desde Platform Admin.
- Validar alta publica, pass Wallet real, puntos, canjes y notificaciones en `LoyaltyCloudFree`.
- Validar deploy API Linux con ZIP creado por `tar -a`.
- Validar deploy Admin Windows con `Compress-Archive`.

## TODO

- Revisar ruido de logs diagnosticos temporales antes de GA.
- Confirmar configuracion final de CORS/App Settings para Admin oficial.
- Agregar refresh tenant-wide si se requiere que cambios de branding/logo disparen APNs inmediato.
- Definir prefijo neutral/configurable de seriales.
- Definir estrategia Apple Pass Type ID/certificados para SaaS GA.
- Actualizar defaults de provisioning para no crear `Mist/Glow/Radiance` como plantilla generica.

## DEFERRED

- Canales externos: email, SMS, WhatsApp, mobile push.
- Plantillas por canal.
- A/B testing.
- Journeys automatizados.
- Reportes avanzados.
- Inventario/stock de recompensas.
- Sucursales/stores.
