# Stamps: imágenes de recompensa y auditoría de migrations

Fecha: 2026-10-16 (America/Tijuana). Branch: `fix/stamps-wallet-reward-layout`, base `staging` / `9cef6ba`. Se conservaron los cambios locales anteriores. No se consultó ni modificó STG/PROD.

## A. Migrations y estrategia

Se inspeccionaron Domain, configuraciones EF, AppDbContext, las seis migrations solicitadas, sus metadatos y el snapshot. Se eligió **B: conservar la cadena y agregar una migration mínima**. No había una cadena SQL defectuosa que justificara consolidar: el defecto estaba en una edición posterior del snapshot. Reescribir seis migrations añadiría trabajo de realineación de STG sin mejorar el resultado del primer despliegue. No se eliminaron ni consolidaron migrations.

| Migration existente | Introduce | Decisión |
|---|---|---|
| `20261001134125_AddTenantProgramType` | `Tenants.ProgramType`, no nullable, default `Points`. La configuración de dominio lo mantiene inmutable. | Conservar. |
| `20261001180152_AddStampProgramFoundation` | `StampProgramVersions`, `StampCycles`, `StampRewardDefinitions`, `StampTransactions`; PKs, FKs por tenant, rowversions, checks de reglas/progreso/delta, unicidad de versión activa, secuencia de ciclos, milestones e idempotencia de transacciones. | Conservar. `StampCycles.Id` ya está correctamente creado aquí. |
| `20261001192907_AddStampOperations` | `StampTransactions.Sequence` (default 0), unicidad por ciclo/secuencia y reversal; `EarnedStampRewards` con snapshots y estados; `StampOperationReceipts` con idempotencia binaria por tenant y response JSON. | Conservar. |
| `20261002010407_AddStampRewardIdentityAndVisualAssets` | `StampRewardIdentities`, `StampVisualConfigurations`, referencia de definición a identidad, FK compuesta e índices. Asigna a cada definición anterior una identidad propia; no adivina continuidad entre versiones. | Conservar. |
| `20261002212826_AddStampRewardShortName` | ShortName en definiciones y rewards ganadas. Backfill independiente desde cada nombre histórico, truncado a 40. | Conservar. |
| `20261002212944_AddStampPassEventContext` | Contexto notificable por tarjeta, transaction, rollover, habilitación y vigencia; FK por tenant a tarjeta y transacción; índice único por tarjeta. No reemplaza ledger ni pipeline de notificaciones. | Conservar. |
| `20261004022555_AddCashierRefreshTokens` | Tabla de refresh tokens, hash único, índice por tenant/usuario, FK compuesta y clave alternativa del usuario. | **Persistent auth, no Stamps. Intacta e independiente.** |

Cadena final del bloque: las seis migrations Stamps originales → Cashier refresh tokens independiente → **`20261016220653_AddStampUpcomingRewardImage`**. Las migrations anteriores y las de otras features permanecen en su orden existente. La nueva migration sólo hace `ADD UpcomingImageBlobName nvarchar(1024) NULL` en `StampRewardIdentities`; Down sólo elimina esa columna.

Estado PROD: la premisa autorizada por el usuario es que Stamps todavía no fue desplegado a PROD. No se consultó `__EFMigrationsHistory` de PROD, por lo que no se afirma haber verificado sus filas reales ni la situación de Cashier auth allí. La decisión de conservar **todas** las migrations históricas evita depender de esa incertidumbre. Se compararon 73 archivos históricos de migration/Designer contra HEAD: todos idénticos.

## B. Snapshot

`StampCycle.Id` existe en Domain, en el mapeo y en la migration fundacional. El snapshot anterior al merge tenía:

```csharp
b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
```

En el merge `924525f` de Subscription, al insertar el bloque `SelfServiceSignupAttempt`, se perdió esa declaración al comienzo del bloque `StampCycle`, aunque quedó `HasKey("Id")`. EF construye el snapshot mediante entidades de metadatos y no puede inferir el tipo de esa propiedad ausente. No era una columna ausente en SQL ni una migration Stamps intermedia defectuosa.

Se restauró la declaración original, sin cambiar el modelo económico ni agregar SQL correctivo para Id. Antes de agregar la nueva propiedad, `has-pending-model-changes` confirmó que snapshot y modelo ya coincidían. Después, EF generó la migration de la imagen opcional. El diff final del snapshot tiene **sólo ocho líneas agregadas**: cuatro para restaurar Id y cuatro para la nueva propiedad. Ningún bloque ajeno a Stamps fue regenerado/cambiado.

La prueba `Clean_local_database_applies_entire_chain_and_snapshot_matches_model` crea una DB temporal LocalDB, aplica todas las migrations desde cero, compara todo el historial, comprueba que no queden migrations ni cambios de modelo pendientes y verifica tipos/nullabilidad. No hay una migration correctiva ficticia para StampCycle.Id.

## C. Modelo final

- `StampRewardIdentity.ImageBlobName`: imagen disponible existente, nullable como antes. No se renombra ni se exige volver a subirla. El fallback predeterminado existente sigue permitido.
- `StampRewardIdentity.UpcomingImageBlobName`: nueva referencia nullable, longitud máxima 1024. Sin default, sin backfill artificial. NULL significa usar la imagen disponible con grayscale/atenuación.
- Las imágenes pertenecen a la identidad visual actual, no al snapshot económico. Las definiciones históricas conservan milestones, nombres y reglas; la identidad permite actualizar visuales en ciclos anteriores.
- Se amplía el contrato visual con `UpcomingReward` y previews de Upcoming. No se crea una tercera imagen ni una nueva entidad.

## D. Admin

Por recompensa se muestra:

1. Nombre de la recompensa.
2. **Imagen disponible**; preview; Cambiar imagen / Usar predeterminada.
3. Ayuda exacta: **Esta es la imagen que verá el cliente cuando desbloquee la recompensa.**
4. **Imagen antes de desbloquear (opcional)**; preview de custom o fallback real; Cambiar imagen.
5. Quitar imagen sólo cuando hay custom. Cuando usa fallback aparece **Vista automática**. No se ofrece Usar predeterminada en el editor de Upcoming.
6. Nota exacta: **Opcional. Si no agregas una imagen, usaremos la imagen disponible en escala de grises hasta que el cliente desbloquee la recompensa.**

Quitar imagen guarda NULL únicamente en UpcomingImageBlobName, conserva ImageBlobName y vuelve al fallback. El store actual no tiene operación de borrado y ya retiene assets reemplazados: se conserva ese patrón, sin borrar blobs que pudieran estar siendo descargados. Puede quedar un blob huérfano; no se agrega un recolector en esta fase.

Se conservan validaciones actuales PNG/JPG, tamaño/dimensiones, normalización técnica y límites por tenant/rol. No se crea ningún blob al usar el fallback. El preview comparte el mismo tratamiento grayscale/contraste/brillo del renderer, no un segundo motor visual.

En Configuración, Stamps conserva ocultos Contenido principal, Nombre del cliente, portada y radios. Guardar envía null para el modo: el servicio existente mantiene el valor almacenado y no elimina el asset. Points conserva sus controles, validaciones y valor enviado.

## E. Wallet

| Estado | Imagen | Tratamiento |
|---|---|---|
| Upcoming personalizada | UpcomingImageBlobName | Colores y alpha originales tras normalización/resize técnico; sin grayscale/atenuación automática. |
| Upcoming sin personalizada | ImageBlobName o su fallback predeterminado | Tratamiento existente: grayscale, contraste .55 y brillo .75. |
| Available | ImageBlobName o predeterminada | Normal, ignora la imagen Upcoming. |
| Redeemed | ImageBlobName o predeterminada | Atenuación existente + check neutral generado por LoyaltyCloud; ignora Upcoming. |

Las actualizaciones visuales guardan primero SQL, suprimen anuncios Stamps anteriores y solicitan refresh de tarjetas instaladas mediante el servicio existente, con APNs best-effort. No crean Earn, rewards, ciclos ni versiones económicas. Una tarjeta conserva los milestones de su ciclo: [10] sólo ocupa posición 10; [5,10] ocupa ambas; el siguiente Earn después de un ciclo completo inicia el ciclo con reglas vigentes. No se inventan milestones ni celdas extra.

## F. Tests

Pruebas nuevas/ampliadas cubren custom Upcoming sin cambios de color a 1x/2x/3x, fallback grayscale, Available/Redeemed independientes de Upcoming, removal sin tocar Available ni crear blobs sintéticos, preview real, reutilización de identidad visual por versiones históricas, [10] y [5,10], reglas históricas al agregar/quitar milestones, copy opcional y acciones de Admin, Configuración por ProgramType y payload de guardado que conserva valores ocultos.

También se validan: cadena limpia desde cero, upgrade de una fila de imagen existente con nueva columna NULL, y que la migration sólo contenga un AddColumn nullable sin default. La prueba de ShortName que siembra un esquema histórico se ajustó para insertar la identidad usando sólo columnas disponibles en aquel punto; no se cambió su migration ni se debilitó su comprobación del backfill.

Resultados finales:

- Focalizados nuevos: **15 aprobados**, 0 fallidos, 0 omitidos.
- Regresión `FullyQualifiedName~Stamp|FullyQualifiedName~TenantBranding|FullyQualifiedName~AdminRouting`: **311 aprobados**, 0 fallidos, 0 omitidos; 1m30s.
- Los **49 failures anteriores del snapshot desaparecieron**. La prueba nueva de ciclo histórico, antes bloqueada por StampCycle.Id, también pasó.
- La regresión incluye las dos pruebas nuevas de cadena EF/upgrade y las pruebas existentes de backfill de identidades/ShortName, aislamiento por tenant, proyección, renderer, pass firmado, contexto notificable y configuración Admin/Points.
- En una primera pasada de la nueva feature hubo un fallo adicional del fixture histórico de ShortName por intentar insertar una columna futura; se corrigió su siembra histórica y la regresión completa quedó verde. Un intento de recompilar tests mientras otro runner usaba la DLL encontró un bloqueo temporal de archivo; la ejecución final se hizo secuencial y terminó sin errores.
- Los runs se solapan y no deben sumarse como tests distintos.

Comando final:

```powershell
dotnet test tests/LoyaltyCloud.Tests -c Release --filter 'FullyQualifiedName~Stamp|FullyQualifiedName~TenantBranding|FullyQualifiedName~AdminRouting' --logger 'trx;LogFileName=stamps-upcoming-regression.trx'
```

## G. Builds y EF

API Release y Admin Release aprobados, sin warnings ni errores. EF `has-pending-model-changes`: sin cambios pendientes. El SQL idempotente revisado desde la última migration anterior a la nueva sólo contiene la nueva columna y su registro de historial, dentro de transacción; no hay Drops, deletes ni operaciones de otras features. `git diff --check` limpio. No se requiere MAUI: no se modificó ese proyecto.

## H. Plan para STG — NO ejecutado

No hace falta resetear datos Stamps ni reescribir `__EFMigrationsHistory`, porque se conservó la cadena.

1. En una intervención futura autorizada, confirmar que la conexión es STG y obtener backup/restauración disponible.
2. Leer `dbo.__EFMigrationsHistory`, comprobar qué migrations del repo están aplicadas y verificar si existe UpcomingImageBlobName. Si el historial y el esquema no corresponden, detener la aplicación del script y diagnosticar; no marcar migrations manualmente ni borrar tablas.
3. Si la cadena existente ya está aplicada hasta `20261004022555_AddCashierRefreshTokens`, generar/revisar el script de **sólo la nueva migration**:

```powershell
dotnet ef migrations script 20261004022555_AddCashierRefreshTokens 20261016220653_AddStampUpcomingRewardImage --idempotent --project src/LoyaltyCloud.Infrastructure --startup-project src/LoyaltyCloud.API --configuration Release
```

4. Aplicar ese script a STG **sólo con autorización posterior**. Si faltan migrations anteriores, revisar primero el rango pendiente real. No se ejecutó ningún database update remoto durante este trabajo.
5. Verificar columna nullable, valores anteriores intactos y nueva fila en historial. La corrección del snapshot no requiere SQL adicional: Id ya existe.
6. Desplegar API/Admin compatibles sólo cuando se autorice: esquema aditivo primero; el código anterior ignora la columna nueva.
7. Smoke test con tarjeta existente y nueva: fallback, upload custom a color, Available, Redeemed con check, removal, refresh en tarjeta histórica y ausencia de un Earn reanunciado. Verificar Contenido principal presente en Points y ausente en Stamps.

No borrar assets/ciclos/rewards de STG es necesario. No se intervino PROD; su historial deberá revisarse en la preparación de su primer release, manteniendo independientes las migrations de otras features.

## I. Git y archivos

Sin commit, push, deploy, merge, PR ni database update STG/PROD. Las migrations se aplicaron exclusivamente a bases temporales de test en SQL Server LocalDB. Sin imágenes generadas con IA, sin cambios de Auth/Subscription/Points/Gift Cards ni migrations históricas.

Archivos del cambio completo (incluye el trabajo anterior conservado):

- `src/LoyaltyCloud.Admin/Components/StampImageUpload.razor`
- `src/LoyaltyCloud.Admin/Pages/Config.razor`
- `src/LoyaltyCloud.Admin/Pages/Stamps.razor`
- `src/LoyaltyCloud.Application/Stamps/IStampVisualService.cs`
- `src/LoyaltyCloud.Domain/Entities/StampRewardIdentity.cs`
- `src/LoyaltyCloud.Infrastructure/Persistence/Configurations/StampConfigurations.cs`
- `src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261016220653_AddStampUpcomingRewardImage.Designer.cs`
- `src/LoyaltyCloud.Infrastructure/Persistence/Migrations/20261016220653_AddStampUpcomingRewardImage.cs`
- `src/LoyaltyCloud.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `src/LoyaltyCloud.Infrastructure/Services/StampStripRenderer.cs`
- `src/LoyaltyCloud.Infrastructure/Services/StampVisualService.cs`
- `src/LoyaltyCloud.Infrastructure/Services/StampWalletProjectionService.cs`
- `tests/LoyaltyCloud.Tests/Infrastructure/StampStripRendererTests.cs`
- `tests/LoyaltyCloud.Tests/Integration/StampMigrationChainTests.cs`
- `tests/LoyaltyCloud.Tests/Integration/StampPassContextTests.cs`
- `tests/LoyaltyCloud.Tests/Integration/StampVisualAssetsTests.cs`
- `tests/LoyaltyCloud.Tests/Integration/StampWalletConfigRenderTests.cs`
- `docs/stamps-upcoming-images-and-migrations.md` (este reporte).

Estado final: 13 archivos tracked modificados y 5 archivos nuevos (migration/Designer, dos archivos de tests y este reporte); ningún archivo eliminado. Todo permanece sin stage/commit. Las verificaciones y logs locales están ignorados por Git.
