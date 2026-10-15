Estado actualizado: `20261002212826_AddStampRewardShortName` y `20261002212944_AddStampPassEventContext` ya fueron aplicadas manualmente a STG; API/Admin relevantes ya fueron desplegados, según confirmación del responsable. “Sin deploy” y LocalDB describen la ejecución histórica de esta fase. Las cifras siguientes son resultados históricos separados. Falta QA físico MAUI/iPhone para notificaciones, estados de recompensas, expiración, varios dispositivos y continuidad de sesión/operaciones; no se afirman resultados nuevos.

Reporte de la fase de mejoras Stamps — 2 de octubre de 2026

Implementado en `feature/stamps-wallet-prototype`. Sin commit, push, merge ni deploy. Las migrations se probaron únicamente en bases LocalDB temporales de los tests; no se aplicaron a STG/PROD.

1. Archivos modificados y agregados

- [docs/stamps-phase-improvements.md](../docs/stamps-phase-improvements.md)
- [src/LoyaltyCloud.API/Controllers/PublicJoinController.cs](../src/LoyaltyCloud.API/Controllers/PublicJoinController.cs)
- [src/LoyaltyCloud.Admin/Components/Layout/MainLayout.razor](../src/LoyaltyCloud.Admin/Components/Layout/MainLayout.razor)
- [src/LoyaltyCloud.Admin/Pages/Join.razor](../src/LoyaltyCloud.Admin/Pages/Join.razor)
- [src/LoyaltyCloud.Admin/Pages/Stamps.razor](../src/LoyaltyCloud.Admin/Pages/Stamps.razor)
- [src/LoyaltyCloud.Admin/Services/WebStampCashierApi.cs](../src/LoyaltyCloud.Admin/Services/WebStampCashierApi.cs)
- [src/LoyaltyCloud.Admin/Shared/StampCashierPanel.razor](../src/LoyaltyCloud.Admin/Shared/StampCashierPanel.razor)
- [src/LoyaltyCloud.Admin/wwwroot/css/site.css](../src/LoyaltyCloud.Admin/wwwroot/css/site.css)
- [src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerCommand.cs](../src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerCommand.cs)
- [src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerHandler.cs](../src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerHandler.cs)
- [src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerValidator.cs](../src/LoyaltyCloud.Application/Customers/Commands/JoinCustomer/JoinCustomerValidator.cs)
- [src/LoyaltyCloud.Application/Stamps/IStampVisualService.cs](../src/LoyaltyCloud.Application/Stamps/IStampVisualService.cs)
- [src/LoyaltyCloud.Application/Stamps/StampOperations.cs](../src/LoyaltyCloud.Application/Stamps/StampOperations.cs)
- [src/LoyaltyCloud.Application/Stamps/StampProgramCommands.cs](../src/LoyaltyCloud.Application/Stamps/StampProgramCommands.cs)
- [src/LoyaltyCloud.Common/Services/StampCashierFlow.cs](../src/LoyaltyCloud.Common/Services/StampCashierFlow.cs)
- [src/LoyaltyCloud.Domain/Entities/EarnedStampReward.cs](../src/LoyaltyCloud.Domain/Entities/EarnedStampReward.cs)
- [src/LoyaltyCloud.Domain/Entities/StampPassEventContext.cs](../src/LoyaltyCloud.Domain/Entities/StampPassEventContext.cs)
- [src/LoyaltyCloud.Domain/Entities/StampRewardDefinition.cs](../src/LoyaltyCloud.Domain/Entities/StampRewardDefinition.cs)
- [src/LoyaltyCloud.Domain/Enums/PassUpdateReason.cs](../src/LoyaltyCloud.Domain/Enums/PassUpdateReason.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/AppDbContext.cs](../src/LoyaltyCloud.Infrastructure/Persistence/AppDbContext.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampConfigurations.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampConfigurations.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampOperationConfigurations.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampOperationConfigurations.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampPassEventContextConfiguration.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampPassEventContextConfiguration.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212826_AddStampRewardShortName.Designer.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212826_AddStampRewardShortName.Designer.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212826_AddStampRewardShortName.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212826_AddStampRewardShortName.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212944_AddStampPassEventContext.Designer.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212944_AddStampPassEventContext.Designer.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212944_AddStampPassEventContext.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261002212944_AddStampPassEventContext.cs)
- [src/LoyaltyCloud.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs](../src/LoyaltyCloud.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs)
- [src/LoyaltyCloud.Infrastructure/Services/AppleWalletPassRefreshService.cs](../src/LoyaltyCloud.Infrastructure/Services/AppleWalletPassRefreshService.cs)
- [src/LoyaltyCloud.Infrastructure/Services/PassGeneratorService.cs](../src/LoyaltyCloud.Infrastructure/Services/PassGeneratorService.cs)
- [src/LoyaltyCloud.Infrastructure/Services/StampOperationsService.cs](../src/LoyaltyCloud.Infrastructure/Services/StampOperationsService.cs)
- [src/LoyaltyCloud.Infrastructure/Services/StampProgramService.cs](../src/LoyaltyCloud.Infrastructure/Services/StampProgramService.cs)
- [src/LoyaltyCloud.Infrastructure/Services/StampStripRenderer.cs](../src/LoyaltyCloud.Infrastructure/Services/StampStripRenderer.cs)
- [src/LoyaltyCloud.Infrastructure/Services/StampVisualService.cs](../src/LoyaltyCloud.Infrastructure/Services/StampVisualService.cs)
- [src/LoyaltyCloud.Infrastructure/Services/StampWalletProjectionService.cs](../src/LoyaltyCloud.Infrastructure/Services/StampWalletProjectionService.cs)
- [tests/LoyaltyCloud.Tests/Application/StampProgramTests.cs](../tests/LoyaltyCloud.Tests/Application/StampProgramTests.cs)
- [tests/LoyaltyCloud.Tests/Infrastructure/StampPassJsonTests.cs](../tests/LoyaltyCloud.Tests/Infrastructure/StampPassJsonTests.cs)
- [tests/LoyaltyCloud.Tests/Infrastructure/StampStripRendererTests.cs](../tests/LoyaltyCloud.Tests/Infrastructure/StampStripRendererTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/AdminRoutingTests.cs](../tests/LoyaltyCloud.Tests/Integration/AdminRoutingTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/PublicJoinTenantRoutingTests.cs](../tests/LoyaltyCloud.Tests/Integration/PublicJoinTenantRoutingTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampAdminTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampAdminTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampCashierRenderTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampCashierRenderTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampOperationsTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampOperationsTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampPassContextTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampPassContextTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampSignedPassTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampSignedPassTests.cs)
- [tests/LoyaltyCloud.Tests/Integration/StampVisualAssetsTests.cs](../tests/LoyaltyCloud.Tests/Integration/StampVisualAssetsTests.cs)

2. Migrations separadas

- `20261002212826_AddStampRewardShortName`: agrega exclusivamente ShortName a definiciones y snapshots; hace backfill desde el Name de cada registro histórico, con trim y límite de 40.
- `20261002212944_AddStampPassEventContext`: crea exclusivamente StampPassEventContexts, sus índices y FKs. No crea anuncios para operaciones históricas.
- Incluye ambos archivos Designer y el snapshot EF actualizado. `dotnet ef migrations has-pending-model-changes` confirma que no hay diferencias pendientes.

3. Modelo y DB

`StampRewardDefinition.ShortName` y `EarnedStampReward.ShortName` son `nvarchar(40)` no nulos. ShortName se copia al ganar la recompensa y es inmutable en definiciones y snapshots, igual que Name. Un cambio de ShortName produce una nueva versión manteniendo RewardIdentityId; un no-op semántico no produce versión. Delete + add sigue creando otra identity. Los assets continúan asociados a StampRewardIdentity.

`StampPassEventContext` contiene Id, TenantId, SerialNumber, TransactionId, IsRollover, ExpiresAtUtc y AnnouncementEnabled. Hay una fila por tenant/tarjeta y FKs compuestas hacia tarjeta y transacción del mismo tenant. Se agregó el query filter correspondiente y se conserva el guard de escritura tenant-owned. No hay filas/contexto de Points.

4. ShortName

Admin muestra Nombre corto, ayuda de Wallet y límite de 40 caracteres. El tamaño sigue la convención ya existente de ShortMessage en mensajes Wallet; Name sigue limitado a 150 y Description a 1000. La validación hace trim, rechaza valores explícitos vacíos o mayores de 40 y devuelve un error amigable.

Para conservar compatibilidad con clientes anteriores, omitir ShortName en el contrato deriva los primeros 40 caracteres del Name normalizado. Admin lo solicita como requerido. Los datos existentes se completan desde su propio Name, sin copiar desde la definición activa ni alterar las recompensas ganadas históricas.

5. Sidebar

Stamps: Programa → Operaciones (`/cashier`) → Mensajes; Reportes → Clientes; Configuración → Sellos → Diseño y configuración → Suscripción y pagos → Personal → Ayuda. Se conserva el bloque Gift Cards y su reporte bajo sus condiciones existentes. Operaciones usa la misma policy CashierOperations que `/cashier`, para Admin/Cashier autenticados. No se ampliaron permisos. Points conserva sus enlaces y orden operativo; el encabezado compartido ahora dice Configuración.

6. Página `/stamps`

Tiene secciones visualmente separadas para Reglas del programa, Recompensas y Apariencia. El botón Guardar reglas y recompensas está al final del formulario; las imágenes quedan fuera de ese formulario y se aplican inmediatamente. Se mantiene el versionado económico existente y el no-op semántico.

Copy de bienvenida: “Se otorgan una sola vez al registrar al cliente.” Reversal: “Permitir cancelar el último sello”, con No permitir / Administradores / Administradores y cajeros. La autorización de reversal no cambió.

7. Cashier compartido

El mismo StampCashierPanel sirve a Web y MAUI Blazor Hybrid, incluyendo sus estilos. Muestra el nombre, progreso numérico y círculos que se distribuyen en varias filas para 2–12 sellos y mobile. Las disponibles destacan con tarjeta visual y Canjear; las futuras indican Al llegar a N sellos; las utilizadas del ciclo actual muestran un check discreto. No se construyó historial de ciclos ni se muestran headings de recompensas vacíos.

El unlock tiene un bloque prominente con “¡Recompensa desbloqueada!” y el nombre. Escanear otro cliente mantiene el botón principal; Cancelar último sello es secundario y depende de CanReverseLastEarn. Se conservan confirmaciones, almacenamiento de pendientes, scanner gate, búsqueda manual y claves de idempotencia. El DTO añade CycleRewards; AvailableRewards conserva recompensas pendientes de ciclos anteriores y su agrupación existente.

8. Estados Wallet

La proyección obtiene las definiciones de la versión del ciclo mostrado y sus EarnedStampReward por StampCycleId + StampRewardDefinitionId. Available destaca normalmente; Redeemed queda atenuada con check; Voided no se considera disponible ni utilizada. La ausencia de una recompensa efectiva se proyecta como próxima. RewardIdentityId únicamente selecciona la imagen compartida.

9. Check del renderer

StampStripRenderer atenúa/grayscale las próximas sin candado, conserva las disponibles en color y atenúa las utilizadas con un pequeño disco neutral oscuro, borde blanco y check blanco. Se dibuja programáticamente por píxeles con ImageSharp en cada escala 1x/2x/3x. No hay assets adicionales ni colores de branding obligatorios. Se verificaron píxeles, dimensiones, ausencia de overlay en próxima y tamaño acotado del check; también se inspeccionó visualmente el PNG generado.

10. Mensajes Stamps

El frente conserva custom_message, con `label: ""`; el reverso conserva custom_message_detail y título/detalle. Expiración y changeMessage siguen el mecanismo de LoyaltyNotification / NotificationDelivery. El builder Stamps también omite mensajes expirados. El builder Points no se modificó.

La documentación oficial de [PassFieldContent](https://developer.apple.com/documentation/walletpasses/passfieldcontent) describe label como opcional y exige `%@` en changeMessage. Se revisó también el esquema oficial de documentación de Apple para esas propiedades. El aspecto sin label queda pendiente de QA físico; no fue necesario usar MENSAJE en el frente.

11. Cumpleaños

Día y mes opcionales aparecen antes de Registrarme, sin selección por defecto. El alta pública acepta BirthdayDay/BirthdayMonth y envía el cumpleaños al registro inicial usando el año 2000 que ya usa UpdateBirthday. Omitir ambos preserva BirthdayNotCaptured; una fecha parcial o inválida recibe un error amigable. Se eliminó el paso posterior redundante del UI, conservando el endpoint existente de cumpleaños.

La recuperación por teléfono mantiene todas sus comprobaciones, la misma tarjeta y el cumpleaños ya almacenado. El reingreso no sobrescribe la fecha ni crea otro cliente. El registro inicial y sus beneficios/eventos reciben la fecha capturada; no se alteraron reglas de cumpleaños.

12. Notificaciones contextuales y ciclo de vida

El contexto se crea o actualiza dentro de la misma transacción de Earn/Reversal/receipt. Referencia StampTransaction; el tipo se lee del ledger y las recompensas se buscan por SourceEarnTransactionId usando el ShortName del snapshot ganado. Nunca se reconstruye un unlock con la configuración activa. IsRollover se captura al abrir el nuevo ciclo; rollover + Earn usa el mensaje normal, salvo unlock.

La ventana es de **15 minutos desde CreatedAt de la transacción**: suficientemente amplia para la actualización posterior a la compra y sus reintentos, sin mantener un evento anunciable durante horas. No se utiliza LastActivityAt como sustituto. La expiración lógica no necesita un job de limpieza y no se renueva al refrescar o reintentar.

La generación del pass es de solo lectura: no consume, marca entregado ni modifica el contexto. Múltiples GET y dispositivos reciben los mismos campos mientras el contexto siga habilitado/vigente y corresponda al último ledger del ciclo. Cada dispositivo compara contra su propio pass; ningún GET de un iPhone consume el evento de los demás.

Los tres mensajes usan un solo campo estable, `stamps`, cuyo value continúa siendo n / N:

- Earn: “🎉 ¡Conseguiste un sello más! Ahora tienes %@.”
- Unlock: “🎁 ¡Desbloqueaste Café gratis! Ahora tienes %@.”; Café gratis proviene del ShortName histórico. Los porcentajes de los nombres se escapan para no introducir placeholders.
- Reversal: “Se canceló el último sello. Ahora tienes %@.”

Solo ese campo recibe el changeMessage Stamps. El armado agrega los ShortNames si existiesen múltiples snapshots desbloqueados por una operación; no genera varios pushes/notificaciones por reward. Las reglas actuales mantienen un threshold distinto por recompensa.

El lookup del receipt sucede antes de actualizar el contexto. Un retry conserva el mismo evento, su expiry y su estado desactivado; no crea otro contexto ni vuelve a habilitarlo. Un replay antiguo no solicita refresh de un contexto económico más nuevo.

Redeem desactiva AnnouncementEnabled en su propia transacción y solicita el refresh DESPUÉS del commit, usando StampRewardRedeemed. No crea LoyaltyNotification de canje ni un changeMessage de canje. Su retry no desactiva un Earn nuevo.

Los cambios de imágenes desactivan el contexto atómicamente con la persistencia visual. El pipeline existente AppleWalletPassRefreshService también lo desactiva antes de los refreshes no económicos de Stamps: branding/configuración/regeneración y mensajes del canal existente. La invalidación de tenant incluye tarjetas sin dispositivos registrados. La desactivación, lectura coherente y operaciones económicas usan el mismo lock de tenant; la generación verifica además que la transacción referenciada sea la última del ciclo.

Por eso un refresh visual posterior no puede reanunciar un Earn anterior: el pass nuevo conserva su progreso, pero se genera sin changeMessage Stamps. No hay consumo irreversible en GET ni nuevo motor genérico. APNs y el sistema de mensajes/deliveries siguen siendo los existentes. Su presentación real y comparación de valores deben verificarse en iPhone.

13. Tests y resultados

**358 pasaron, 0 fallaron, 0 omitidos**, en la corrida Release final (43 s), con pruebas SQL Server LocalDB y JSON/renderer/componentes. TRX: `tests/LoyaltyCloud.Tests/TestResults/stamps-phase.trx`.

Cobertura contextual A–L:

| Escenario | Evidencia principal |
|---|---|
| A Earn normal | StampPassContextTests + StampPassJsonTests |
| B Prioridad de unlock | Snapshot ShortName y un único campo con changeMessage |
| C Reversal | Mensaje contextual y recompensa Voided vuelve a próxima |
| D Rollover | Teoría con/sin unlock en ciclo nuevo |
| E Retry | Contexto único, expiración sin renovación y concurrent Earn/receipt |
| F Imagen después de Earn | Contexto desactivado incluso al reintentar el Earn |
| G Redeem | Check proyectado, sin evento ni LoyaltyNotification adicional; refresh posterior al commit |
| H Múltiples GET | Proyección y campos JSON consistentes, sin consumo |
| I Múltiples dispositivos | Dos registros, dos pushes y mismo contexto; invalidación para todos |
| J Mensaje manual | LoyaltyNotification Custom independiente, front/back/changeMessage/expiry |
| K Ciclo histórico | Cambio de versión/ShortName conserva el snapshot anterior |
| L Fuera de vigencia | Evento omitido al cumplirse 15 minutos; retry no prolonga |

Además se cubrieron navegación Points/Stamps, roles, orden, duplicados y Gift Cards condicional; versiones/identities y backfill; Cashier Web/mobile para cada total 2–12; heading vacío, próximas/utilizadas y unlock; renderer en tres escalas; Birthday con/sin fecha, API pública y reingreso; guards de tenant y rechazo de escritura cruzada. La selección ampliada incluyó regresiones existentes de Points, Apple Wallet, mensajes y provisioning.

14. Builds y comprobaciones

- API Release: OK, 0 warnings / 0 errores; incluye Domain, Common, Application e Infrastructure.
- Admin Release: OK, 0 warnings / 0 errores.
- MAUI Windows `net9.0-windows10.0.19041.0`, Debug + CashierEnvironment=Staging: OK, 0 warnings / 0 errores.
- MAUI Windows mismo target, Release + CashierEnvironment=Staging: OK, 0 warnings / 0 errores.
- Modelo EF sin cambios pendientes: OK.
- `git diff --check`: OK.

Logs locales: `stamps-test.log`, `stamps-api-release.log`, `stamps-admin-release.log`, `stamps-maui-debug.log`, `stamps-maui-release.log`, `stamps-migrations-check.log`.

15. QA físico pendiente

En iPhone: comprobar próxima sin candado, disponible en color, canjeada con badge sobre imágenes claras/oscuras; tamaño del check con 2, 10 y 12 sellos; frente de Mensajes sin label y detalle en reverso; Earn/unlock/Reversal/rollover con los textos finales; Redeem e imagen/branding/mensaje sin reanuncio Stamps; expiración después de 15 minutos y dos dispositivos instalados en la misma tarjeta. También validar Cashier en pantalla móvil real y la opción de cumpleaños visible antes del alta.

Los PNG de estados y paquetes firmados de prueba están en `artifacts/stamps-wallet-production-tests`; el certificado de test no es un certificado Apple instalable. No hubo QA físico en esta sesión.

16. Decisiones y desviaciones

Se mantuvo ShortName omitible en el contrato para compatibilidad, con fallback determinista; Admin exige capturarlo. La notificación unlock añade “Ahora tienes %@.” para utilizar el campo de progreso estable y cumplir el formato Apple sin depender de introducir otro campo. Son decisiones dentro de las opciones autorizadas, sin cambios económicos.

La persistencia adicional y su migration separada corresponden a la autorización posterior. No se implementaron Google Wallet, recuperación de contraseña, hard delete de tenant, provisioning branding, reports/histories ni cambios económicos de Points/Gift Cards. No se hicieron refactors generales, commit, push, merge ni deploy.
