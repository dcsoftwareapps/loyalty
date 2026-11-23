# QA de Google Wallet Stamps en Azure STG

Ronda del 8 de noviembre de 2026, hora de Tijuana. Las solicitudes se registran en UTC.

## Deployment

| Elemento | Evidencia |
|---|---|
| Rama | `feature/google-wallet-stamps` en el worktree dedicado |
| HEAD local y remoto | `13ae62918d6244c61ad558a78de6e112d143130f` |
| Base y `origin/staging` | `8749ef9ab946df0d736723808313f1c914ef1522` |
| Commits de la feature | `bf6c40b` (implementación), `13ae629` (pruebas y documentación) |
| PR #90 | Abierto, borrador y sin merge según la API de GitHub |
| SHA y fecha desplegados | **BLOCKED** para Admin y API. Azure CLI devuelve `Please run 'az login' to setup account.` Sin esa evidencia no se pudo calcular la diferencia contra el HEAD. |

El usuario informó que desplegó la feature directamente. El script `scripts/deploy-stg.ps1` empaqueta Admin y API por separado e imprime el SHA en la consola local. Un ZIP local anterior no acredita el despliegue activo. Consultar el despliegue activo de cada App Service y, si el historial no identifica la revisión, leer `AssemblyInformationalVersion` del ensamblado desplegado. [Registros de despliegue de App Service](https://learn.microsoft.com/en-us/cli/azure/webapp/log/deployment)

## Bugs

| ID | Severidad | Módulo | Error | Causa raíz | Corrección | Estado |
|---|---|---|---|---|---|---|
| STG-TENANT-001 | P0 reportado | Platform Admin | El alta autenticada devolvió errores de campos vacíos aun con los valores visibles. | En el flujo mixto reproducido, los campos de texto visibles aún dependían de `onchange`; los valores editados por el navegador sin ese evento no llegaron al comando. | Los campos de texto actualizan el modelo con `oninput`. El selector mantiene `onchange` y la validación del comando sigue activa. | Reproducido en STG. Corrección local sin desplegar. |
| QA-TENANT-002 | P1 | Platform Admin | Al fallar el logo, el formulario aparenta que el alta falló. Reintentar con el mismo slug puede dar duplicado. | `PlatformTenants.CreateAsync` ejecuta `ProvisionTenantCommand`, que confirma SQL antes de subir el logo opcional. El error de logo se trataba como un fallo del alta. Este camino ya existía en la base. | El formulario se cierra y limpia, la lista se actualiza y Admin enlaza al tenant creado para reintentar el logo. Un rechazo de Blob registra el estado y código del proveedor y muestra un mensaje seguro. | Reproducido localmente con error devuelto. La regresión también cubre un 403 simulado. Asociación con el incidente de STG pendiente. |
| QA-WALLET-003 | P2 | Google Wallet Stamps | «CANJES DEL CICLO» incluye canjes anteriores después del rollover. | `GoogleStampWalletMapper.ToObjectData` contaba el historial completo. | Cuenta los canjes del ciclo vigente y mantiene las recompensas disponibles de ciclos anteriores. | Reproducido localmente. Verificación física pendiente. |

El formulario `/platform/tenants` requiere SuperAdmin. El guardado se ejecuta en el proceso de Admin vía Blazor Server, sin un POST REST de alta en API. Buscar en los logs de Admin los eventos de `TenantProvisioningService.ProvisionAsync`, `CircuitHost` y el error SQL o Blob en el intervalo del intento. Si se adjuntó un logo, buscar primero el slug en el listado.

## Validación

| Comprobación | UTC | Resultado |
|---|---|---|
| Admin STG `GET /platform/tenants` sin sesión | 2026-11-09 04:38:34Z | **PASS**. HTTP 302 al login de Platform con retorno a la ruta solicitada. |
| API STG `GET /` | 2026-11-09 04:38:34Z | HTTP 404. El host respondió por HTTPS. |
| API STG `GET /api/wallet-assets/google/stamps/progress/qa-invalid-token.png` | 2026-11-09 04:40:42Z | **PASS para el rechazo**. HTTP 404 `application/problem+json; charset=utf-8`. La ruta inexistente de control devolvió 404 `application/problem+json`. La diferencia es coherente con el controlador de imágenes y no identifica el SHA desplegado. |
| Alta autenticada y persistencia STG | 2026-11-09 16:58:31Z (observación del resultado) | **FAIL**. La sesión de Platform permitió enviar. El validador rechazó Slug, Nombre y Usuario admin vacíos. El tenant sintético no apareció en el listado. Azure CLI carece de sesión. |
| Configuración Stamps, emisión real, clase/objeto, Save Link, PNG válido, QR, Earn, Redeem, Reversal, rollover, branding y fallo transitorio en STG | — | **BLOCKED** hasta disponer de sesión de prueba, tenant y tarjeta sintéticos y emisor autorizado. |
| Android físico y regresión funcional en STG de Apple Wallet, Points, Gift Cards y Cashier | — | **NOT TESTED**. |

No se crearon registros en STG. No hay datos de esta ronda que limpiar.

### Pruebas locales

Antes de la corrección fallaron los tres casos esperados: formulario aún abierto tras el error del logo y uno/dos canjes donde correspondían cero/uno.

| Validación local final | Resultado |
|---|---|
| Build API, Admin y Tests | **PASS**. Cada proyecto terminó con 0 errores y 7 avisos NuGet conocidos de ImageSharp y System.Security.Cryptography.Pkcs. |
| Regresión focalizada | **PASS**. 20 aprobadas y 0 fallidas, incluidos errores de logo devueltos por el comando, Blob 403 simulado y canjes por ciclo. |
| Regresión ampliada de Admin, provisioning, Stamps, Wallet, Gift Cards, Cashier y aislamiento | **FAIL**. 675 aprobadas, 5 fallidas y 0 omitidas. Son los mismos cinco fallos observados en la base: tres variantes de `StampSignedPassTests` por falta del certificado intermedio Apple WWDR G4 local y dos de navegación en `GiftCardFeatureToggleTests`. |
| `git diff --check` y revisión de secretos | **PASS**. Sin problemas de whitespace ni coincidencias de secretos en las líneas agregadas. |

## Cambios y pendientes

- `src/LoyaltyCloud.Admin/Pages/PlatformTenants.razor` y `tests/LoyaltyCloud.Tests/Integration/TenantProvisioningTests.cs`.
- `src/LoyaltyCloud.Infrastructure/Services/GoogleWallet/GoogleStampWalletMapper.cs` y `tests/LoyaltyCloud.Tests/Infrastructure/GoogleStampWalletMapperTests.cs`.
- `docs/qa/STG_GOOGLE_WALLET_STAMPS_QA.md`.
- Los cambios locales no requieren migración. Para validarlos en STG, desplegar Admin y API de la misma revisión tras autorizar el despliegue. Resolver por separado cualquier problema de configuración o esquema que revelen los logs del incidente.
- Con acceso de lectura, confirmar el SHA y los logs de ambos App Services, el esquema SQL en uso, los permisos Blob si el fallo corresponde al logo, y la configuración de issuer/service account de Google. Comprobar el origen HTTPS del PNG y la persistencia de claves Data Protection en el entorno real. Si se usan varios slots, verificar la estrategia de claves entre ellos. [Data Protection en Azure App Service](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings)
- Antes de dar STG por estable, correlacionar el alta autenticada y probar el ciclo completo con datos sintéticos, el pase físico en Android, el PNG después de un reinicio y la regresión real de Apple Wallet, Points y Gift Cards.

## Reproducción autenticada del 9 de noviembre de 2026

- El navegador integrado abrió el listado con la sesión Platform Admin del usuario y el botón «Crear tenant» abrió el formulario.
- Se preparó un tenant sintético con slug `qa-stamps-stg-20261109-d359c8fd`, programa Sellos, zona `America/Tijuana` y sin logo. Los clics en «Guardar» de las 16:54:43, 16:55:26 y 16:55:55 UTC devolvieron el mismo mensaje visible: «El password inicial debe tener al menos 8 caracteres.» La página permaneció en el formulario y el slug no apareció en el listado. No hubo errores ni advertencias en la consola del navegador durante la comprobación.
- El texto exacto corresponde al guard de `PlatformTenants.CreateAsync` antes de `ISender.Send(ProvisionTenantCommand)`. Es una validación del componente en Blazor Server. Para estos intentos no hay un status HTTP REST de alta ni evidencia de que se haya intentado guardar en SQL. Azure CLI seguía sin sesión.
- Tras escribir manualmente la contraseña, salir del campo y guardar, desapareció el error de contraseña. A las 16:58:31 UTC seguían visibles los textos preparados por automatización, pero el formulario mostró tres errores de `ProvisionTenantValidator`: `'Slug' must not be empty.`, `'Display Name' must not be empty.` y `'Admin Username' must not be empty.`. La consulta del listado confirmó cero filas para el slug y la consola no mostró errores. Este intento confirma el problema con entrada mixta; no establece si ocurre al escribir manualmente todos los campos.
- El cambio en `PlatformTenants.razor` sincroniza los campos de texto en cada evento de entrada, sin esperar a perder el foco. La regresión `PlatformTenantFormTests.Save_uses_text_input_events_even_without_change_or_blur` monta el Razor real, dispara los eventos y comprueba los valores enviados al comando y la navegación de éxito simulada. Falló antes de la corrección porque faltaba el manejador `oninput` en Nombre y pasó después (1 aprobada, 0 fallidas). [Enlace de datos en Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding?view=aspnetcore-9.0)
- API, Admin y Tests compilaron durante la validación del cambio. El filtro final de formulario, provisioning, SuperAdmin y Google Wallet Stamps terminó con 84 aprobadas, 0 fallidas y 0 omitidas. Esta corrección del alta requiere publicar Admin para repetir la prueba en STG.
