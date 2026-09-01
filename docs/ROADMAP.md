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
- Reportes avanzados tenant-aware con filtros independientes, KPIs, gráficas y detalle:
  - Clientes en riesgo (`/reports/inactive-customers`).
  - Recompensas más canjeadas (`/reports/top-rewards`).
  - Mejores clientes (`/reports/top-customers`).
  - Frecuencia de visita (`/reports/visit-frequency`).
  - Clientes nuevos y recurrentes (`/reports/returning-customers`).
  - Actividad mensual y tendencias (`/reports/activity-trends`).
  - Distribución actual por nivel (`/reports/level-distribution`).
- Indicadores reutilizables: clientes nuevos/activos, puntos emitidos/canjeados/vencidos, canjes, compras, monto registrado, total de clientes y saldo actual de puntos.
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
- Mensajes personalizados con notificación corta y detalle largo para Apple Wallet y Google Wallet.
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
- Tamaño visual del logo de Apple Wallet configurable por tenant.
- Contenido principal configurable de Apple Wallet: nombre del cliente o imagen de portada.
- Fallback grafico neutral de Wallet sin texto `LC`.
- Wallet pass con fondo claro, valores negros y labels en PrimaryColor.
- Colores del pass en Google Wallet / Android usando branding tenant-aware.
- Google Wallet aprobado para produccion.
- Google Wallet Save Link validado en STG.
- Prevencion de clientes duplicados por telefono y recuperacion segura de tarjeta existente cuando nombre/apellido coinciden.
- Gift Cards con entrega por email mediante SMTP provider-neutral, enlace publico seguro y rotacion de token al reenviar.

### Estado de Reportes

**Completado**

- Clientes en riesgo, recompensas más canjeadas, mejores clientes, frecuencia de visita, clientes recurrentes/nuevos, actividad mensual y distribución actual por nivel.

**Backend reutilizable disponible**

- Clientes nuevos y activos; puntos emitidos, canjeados y vencidos; cantidad de canjes; compras y monto registrado; total de clientes y saldo actual de puntos.

**Pendiente / evolución futura**

- Exportación CSV/PDF e históricos que requieran snapshots. No se presentan métricas como revenue, LTV o crecimiento sin una fuente suficiente.
## RC1 / UAT

- Crear tenants reales desde Platform Admin.
- Configurar KBeauty como tenant UAT, no como seed.
- Subir logo real de KBeauty desde Platform Admin.
- Validar alta publica, pass Wallet real, puntos, canjes y notificaciones en `LoyaltyCloudFree`.
- Validar deploy API Linux con ZIP creado por `tar -a`.
- Validar deploy Admin Windows con `Compress-Archive`.

## TODO

- Terminar pagos recurrentes
- Dashboard de valor
- Vista de clientes eliminados
- Restaurar cliente
- Hard delete permanente de cliente

## DEFERRED

- Canales externos masivos: SMS, WhatsApp, mobile push.
- Historial avanzado de entrega de emails, reintentos en background y webhooks de proveedor.
- Plantillas por canal.
- A/B testing.
- Journeys automatizados.
- Exportación de reportes avanzados (CSV/PDF) y análisis históricos que requieran snapshots.
- Inventario/stock de recompensas.
- Sucursales/stores.

## Definiciones de Reportes

- **Visita:** día calendario único en el que un cliente tuvo una transacción de puntos o un canje no cancelado. Varias operaciones del mismo cliente el mismo día cuentan como una visita.
- **Cliente nuevo:** cliente cuya primera actividad registrada ocurrió dentro del periodo analizado.
- **Cliente recurrente:** cliente activo en el periodo cuya primera actividad ocurrió antes de ese periodo.
- **Nuevos vs recurrentes:** se integra en Clientes recurrentes para evitar duplicar análisis.
- **Distribución por nivel:** representa el estado actual; no se presenta histórico porque no existen snapshots de nivel.
- **Monto registrado:** suma de `PurchaseAmount` en transacciones reales de tipo compra; no se presenta como revenue ni LTV.
