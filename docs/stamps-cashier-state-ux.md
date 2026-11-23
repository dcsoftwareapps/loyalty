Estado actualizado: “sin deploy” describe el momento de esta ronda; API/Admin relevantes fueron desplegados posteriormente a STG. La comparación de hashes fue evidencia temporal de QA, no una dependencia del producto; el archivo temporal fue eliminado. Se mantiene la decisión final de permitir un nuevo Earn explícito después del éxito con nueva intención/idempotency key. Las cifras siguientes son resultados históricos separados. Falta QA físico MAUI/iPhone.

# Estado y continuidad del Cashier Stamps

Implementación local, sin commit/push/deploy, migrations ni cambios de Wallet o reglas económicas.

## 1. Causa de pérdida de estado

Web tenía una rama `if GiftCard / else if Stamps`: al cambiar tab, Blazor removía WebStampCashier y el StampCashierPanel del árbol, destruyendo su flow y estado local. SwitchSurfaceAsync también reseteaba los dos modos. En MAUI el host conservaba StampCashierFlow, pero removía el panel Stamps: se perdía estado local de entrada/confirmación. GiftCardPanel de MAUI ya permanecía montado.

## 2. Conservación

Web conserva ambos modos montados y oculta el inactivo con `hidden`. MAUI hace lo mismo con Stamps y conserva el comportamiento previo de GiftCardPanel. No hay componentes duplicados ni almacenamiento global. La selección, progreso, recompensas, feedback y datos de Gift Card permanecen en el componente/host de la sesión montada.

La navegación no ejecuta Read/Earn/Redeem, no restaura el flow otra vez y no genera operaciones. Web conserva código, importe, detalle y feedback de Gift Card. Se mantiene el reset existente de Points al cambiar de modo, pero no se borra Gift Card por cambiar de tab.

## 3. Segundo Earn y doble submit

`ScanConsumed` queda reservado a callbacks repetidos del scanner. Ya no deshabilita permanentemente Agregar sello. El botón queda disponible cuando no hay Busy, Pending, scanner o confirmación en curso.

Cada render del botón captura `EarnIntentKey` y usa `BeginEarnAsync(intentKey)`. El gate atómico impide requests concurrentes; una intención vieja se rechaza después de cerrar su Earn. Dos callbacks del mismo botón, incluso con una respuesta inmediata entre ambos, no generan otro Earn. Un click explícito en el botón actualizado usa la nueva intención y puede sumar otro sello al mismo cliente. No hay cooldown.

Esta lógica está en StampCashierFlow y el panel compartido, por lo que Web y MAUI usan la misma protección.

## 4. Idempotencia

La intención del botón es una referencia temporal de UI, separada de la idempotency key económica. Cada operación nueva sigue creando `PendingStampOperation` con `Guid.NewGuid().ToString("N")` en BeginCoreAsync y se guarda antes de enviar.

Retry reutiliza la misma operación y key. Un resultado incierto conserva Pending; no permite otro Earn, cambiar cliente ni abandonar Stamps mediante tabs. La intención solo se renueva después de cerrar el Earn y retirar Pending, o al cambiar realmente de cliente. Si falla el cierre del storage local, Pending sigue bloqueando operaciones nuevas.

## 5. Cambio de cliente

Debajo de recompensas aparece Cambiar cliente con dos acciones secundarias, antes de Cancelar último sello:

- Escanear cliente: limpia cliente, feedback, unlock, selección/confirmación e ID manual y abre scanner.
- Buscar otro cliente: aplica la misma limpieza, vuelve a Buscar cliente manualmente y no abre cámara.

PrepareNextScan invalida la intención anterior. La siguiente operación crea otra key y la consulta del nuevo cliente trae su permiso de reversión. No se reutiliza CanReverseLastEarn del anterior. Pending/Busy impiden ambas salidas.

## 6. Verificación

- **254 tests focalizados aprobados; 0 fallos y 0 omitidos**. StampCashier, StampAdmin, AdminRouting, CashierGiftCard, CashierMobileApp y CashierModeRetention.
- El test de retención monta el CashierLanding y sus componentes reales: selecciona Daniel, hace Earn hasta 3/10, cambia a Gift Card y vuelve; conserva exactamente las instancias de WebStampCashier/StampCashierPanel, estado y feedback, código/importe Gift Card y una sola ejecución Earn. Otro caso verifica el bloqueo de tabs cuando la operación queda incierta.
- Tests del flow: segundo Earn explícito con key nueva; callback duplicado rápido; concurrencia; Pending y retry con misma key; cambio de cliente y rechazo de intención antigua; reversión enviada al cliente correcto.
- Tests de panel: Earn vuelve a estar habilitado; Escanear limpia/abre cámara; Buscar otro cliente limpia estado temporal sin scanner y vuelve a búsqueda manual. Cobertura de render portable web/mobile y contrato de montaje de MAUI.
- Admin Release: **0 warnings, 0 errores**.
- MAUI Windows Debug/Staging, net9.0-windows10.0.19041.0: **0 warnings, 0 errores**.
- `git diff --check`: limpio.
- Domain/Application/Infrastructure: los 614 archivos comparados con el snapshot anterior permanecen iguales. El cambio Common se limita al flow de UI/idempotencia, no a reglas económicas.

## 7. Edge cases y límites

Cambiar tab conserva estado; salir de la página/circuito o recargar completamente puede perder la selección visual. Esto es intencional: no se añadió storage visual. Las operaciones pendientes siguen usando sessionStorage/SecureStorage existentes y su key original.

Escanear cliente es un cambio explícito que limpia antes de abrir cámara. Si se cancela, queda el formulario inicial limpio; no hay Earn y no se arrastran datos del anterior. Los tabs no pueden ocultar un scanner/operación Stamps en curso.

Un clic posterior al render del éxito se considera una nueva intención explícita. Los callbacks del render anterior se descartan, sin introducir una ventana temporal.

Las comprobaciones de MAUI fueron tests portables/contratos del host y compilación Windows; no hubo una nueva prueba interactiva en dispositivo.

## Archivos de esta ronda

- src/LoyaltyCloud.Admin/Pages/CashierLanding.razor
- src/LoyaltyCloud.Admin/Shared/StampCashierPanel.razor
- src/LoyaltyCloud.Admin/wwwroot/css/site.css
- src/LoyaltyCloud.Cashier/Components/Pages/Home.razor
- src/LoyaltyCloud.Common/Services/StampCashierFlow.cs
- tests/LoyaltyCloud.Tests/Integration/StampCashierTests.cs
- tests/LoyaltyCloud.Tests/Integration/StampCashierRenderTests.cs
- tests/LoyaltyCloud.Tests/Integration/CashierMobileAppTests.cs
- tests/LoyaltyCloud.Tests/Integration/CashierModeRetentionTests.cs (nuevo)
- docs/stamps-cashier-state-ux.md (este reporte)

Esta ronda sustituye la decisión anterior de mantener Agregar sello deshabilitado después del primer Earn.
