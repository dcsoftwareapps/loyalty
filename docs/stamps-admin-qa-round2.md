Reporte histórico: las afirmaciones de implementación local/sin deploy describen esta ronda. API/Admin relevantes fueron desplegados posteriormente a STG. La decisión de dejar Agregar sello deshabilitado tras el primer Earn fue sustituida: después de un Earn exitoso se permite otro Earn explícito con nueva intención y nueva idempotency key; Pending y callbacks duplicados siguen protegidos. Las cifras siguientes pertenecen a ejecuciones separadas de esta ronda.

# Segunda ronda de QA: Admin/Cashier y Clientes

Implementación local; sin commit, push, deploy ni migrations. Los cambios previos del workspace se conservaron.

## 1. Archivos modificados en esta ronda

- `src/LoyaltyCloud.Admin/Auth/CashierExperience.cs` (nuevo): decisión compartida por identidad/rol.
- `src/LoyaltyCloud.Admin/Components/Layout/CashierAdaptiveLayout.razor`: utiliza esa decisión.
- `src/LoyaltyCloud.Admin/Pages/CashierLanding.razor`: header condicionado y presentación de Gift Card.
- `src/LoyaltyCloud.Admin/Shared/StampCashierPanel.razor`: jerarquía, feedback y reset del componente compartido.
- `src/LoyaltyCloud.Admin/Shared/WebStampCashier.razor`: obtiene nombres cortos de snapshots ganados para el feedback.
- `src/LoyaltyCloud.Admin/Pages/Customers.razor`: presentación por ProgramType.
- `src/LoyaltyCloud.Admin/Pages/QuickHelp.razor`: instrucciones con el nuevo texto Escanear cliente.
- `src/LoyaltyCloud.Admin/wwwroot/css/site.css`: estilo de títulos y búsqueda alternativa.
- `tests/LoyaltyCloud.Tests/Integration/StampAdminTests.cs`: header por rol y Clientes por programa.
- `tests/LoyaltyCloud.Tests/Integration/StampCashierRenderTests.cs`: presentación web/mobile, feedback, reset y recompensas acumuladas/futuras.
- `tests/LoyaltyCloud.Tests/Integration/StampOperationsTests.cs`: reproducción de rollover con recompensa acumulada y excepción del detalle.
- `docs/stamps-admin-qa-round2.md`: este reporte.

## 2. Admin embebido vs Cashier standalone

`CashierExperience.IsEmbeddedAdmin` requiere identidad autenticada, rol Admin y ausencia del rol Cashier. Layout y header usan la misma decisión. Admin conserva MainLayout/sidebar y omite todo el header propio del Cashier. Cashier conserva EmptyLayout y header, username y logout. No se modificaron políticas ni permisos; sigue existiendo un solo StampCashierPanel.

## 3. Jerarquía de Sellos y Gift Card

Entrada: Sellos → Escanear cliente como acción principal → separación visual → Buscar cliente manualmente → ID → Buscar.

Con cliente: Sellos → CLIENTE → nombre → progreso numérico/visual → Agregar sello → feedback → Recompensas → Escanear cliente → Cancelar último sello, cuando el backend lo permite.

Gift Card usa el mismo h2, card, espaciado y acciones principales del panel Sellos. La búsqueda manual se presenta como alternativa separada. Sus handlers, validación y operaciones no cambiaron.

## 4. Feedback Earn/unlock

El éxito aparece debajo de Agregar sello en un bloque de status, con `role=status`, `aria-live=polite` y `aria-atomic=true`, sin un heading de éxito arriba. Reutiliza `kb-form-success` con estilos locales compatibles con el componente compartido.

El unlock tiene prioridad y muestra la recompensa con más prominencia. Web lee ShortName desde EarnedStampReward por el ID de la recompensa ganada; no lo reconstruye con la definición activa. Si falta el nombre corto o falla esa lectura de presentación, conserva el nombre del resultado, sin invalidar la operación ya confirmada. El panel portable acepta esos nombres como un parámetro opcional y conserva el fallback al nombre recibido.

La sección Recompensas conserva sus acciones de canje. El feedback no incorpora un segundo botón Canjear. En esta ronda Agregar sello quedaba deshabilitado tras consumir el Earn. Esa decisión fue sustituida: ahora un Earn exitoso habilita otro Earn explícito con nueva intención e idempotency key, manteniendo la protección contra callbacks duplicados y Pending. Si además se completó la tarjeta, el feedback de unlock conserva esa información.

## 5. Escanear cliente

No aparece arriba con cliente seleccionado. Se ubica después de recompensas, justo antes de la reversión secundaria. Invoca el handler existente de scanner y PrepareNextScan, limpia cliente, mensajes, unlocks, entrada manual y confirmación, y abre el scanner. Un scanner cancelado deja la entrada inicial disponible. No se permite resetear una operación pendiente o en curso. La UI y Ayuda rápida ya no usan Escanear otro cliente.

## 6. R2 disponible con 2/10

`StampOperationsService.StateAsync` obtiene disponibles desde EarnedStampReward con estado Available de cualquier ciclo de la misma tarjeta. Las próximas provienen de la definición histórica del ciclo actual. El Earn solo desbloquea definiciones cuyo RequiredStamps coincide con CurrentStamps después de sumar el sello.

El test reproduce completar 10/10 y realizar dos Earn del siguiente ciclo: progreso 2/10, sin unlock nuevo, recompensa de 10 disponible ligada a un Earn del primer ciclo, y próximas de 5 y 10. La recompensa acumulada se canjea sin alterar 2/10. Se conservó toda la lógica; la UI agrega Disponible a las acumuladas y mantiene los umbrales en las futuras, sin historial ni referencia al ciclo anterior.

Esta verificación confirma el origen en código y en datos del test. No se consultó el registro concreto de Daniel en STG, por lo que no se atribuye una transaction específica a esa captura.

## 7. Clientes Stamps

Conserva Nombre, ID del cliente, Alta, búsqueda y paginación. Oculta columnas Nivel/Puntos, filtro de nivel y enlace Ver detalle. No se inventaron métricas Stamps. Points mantiene sus columnas, filtro y enlace.

## 8. Causa del Internal Server Error

Reproducida mediante el servicio real de detalle: CustomerDetailReadService.GetByCustomerIdAsync, cuando existe tarjeta, llama a TenantLoyaltyLevelReadService.GetActiveLevelsAsync y LevelProgressService.Calculate aunque el tenant sea Stamps. Los tenants Stamps no tienen niveles Points activos. LevelCalculationService.ValidateAndOrder lanza InvalidOperationException con el mensaje exacto:

`No hay niveles activos configurados para el tenant actual.`

CustomerDetail.OnInitializedAsync envía GetCustomerDetailQuery sin capturar esa excepción, que termina en error del render. No se modificó Customer Detail ni sus servicios: la solución de producto autorizada fue ocultar el enlace de Stamps.

## 9. Validación

- Tests focalizados Release: **277 aprobados, 0 fallidos, 0 omitidos**. Cobertura StampAdmin, StampCashier, StampOperations, AdminRouting, CashierGiftCard, CashierMobileApp y TenantAdminAuth.
- Admin Release: **0 warnings, 0 errores**.
- MAUI Cashier Windows Debug/Staging (`net9.0-windows10.0.19041.0`, `CashierEnvironment=Staging`): **0 warnings, 0 errores**.
- `git diff --check`: limpio.
- Comparación SHA-256 de 625 archivos de Domain, Infrastructure, Application y Common con el inicio de esta ronda: **ninguno cambió**. Incluye Wallet, notificaciones, modelo ShortName, reglas, versionado y migrations.
- La validación fue local mediante tests de render/handlers y builds; no se hizo deploy ni verificación nueva en un dispositivo STG.

## 10. Decisiones pendientes

Queda pendiente definir Customer Detail para Stamps. Ocultar el enlace no rediseña ni habilita la ruta: una URL antigua/directa todavía ejecuta el detalle incompatible. No se requiere una decisión adicional para los ajustes implementados. El registro exacto de la captura de Daniel no fue inspeccionado en STG.
