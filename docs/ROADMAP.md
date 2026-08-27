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
- Reportes v1 con páginas independientes.
- Customer Detail.
- Customer Detail avanzado para auditoria de puntos.
- Sumar puntos con QR/camara.
- Canjear puntos con QR/camara.
- Canje directo de puntos por descuento en dinero.
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
- Processor de notificaciones cada 12 horas para control de costo Azure SQL Free.
- Quick Help `/quick-help`.
- QR imprimible de registro con QRCoder.
- Guardrail contra el hostname Admin retirado.
- Branding tenant-aware en Admin.
- Logo por tenant para Apple Wallet.
- Fallback grafico neutral de Wallet sin texto `LC`.
- Wallet pass con fondo claro, valores negros y labels en PrimaryColor.
- Google Wallet aprobado para produccion.
- Google Wallet Save Link validado en STG.

## RC1 / UAT

- Crear tenants reales desde Platform Admin.
- Configurar KBeauty como tenant UAT, no como seed.
- Subir logo real de KBeauty desde Platform Admin.
- Validar alta publica, pass Wallet real, puntos, canjes y notificaciones en `LoyaltyCloudFree`.
- Validar deploy API Linux con ZIP creado por `tar -a`.
- Validar deploy Admin Windows con `Compress-Archive`.

## TODO

- Reportes
- Configurar tamaño del logo en el pass
- Colores del pass en Google Wallet / Android
- Terminar pagos recurrentes
- Dashboard de valor

## DEFERRED

- Canales externos: email, SMS, WhatsApp, mobile push.
- Plantillas por canal.
- A/B testing.
- Journeys automatizados.
- Reportes avanzados.
- Inventario/stock de recompensas.
- Sucursales/stores.
