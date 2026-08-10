# LoyaltyCloud STG Setup

Este documento reconstruye el procedimiento real seguido para crear el ambiente STG de LoyaltyCloud. No es una guia generica: refleja los recursos, scripts, comandos finales, ajustes y problemas encontrados durante la preparacion del ambiente.

## 1. Objetivo

Crear un ambiente STG completamente separado de PROD para validar LoyaltyCloud antes de cambios productivos.

El ambiente STG debe tener recursos propios para:

- API.
- Admin.
- SQL Server y SQL Database.
- Storage.
- Key Vault.
- Managed Identities.
- RBAC.
- App Settings.
- Secrets.

El ambiente STG no debe reutilizar bases, Web Apps, Key Vault ni Storage de PROD.

## 2. Recursos Creados

Los recursos reales usados para STG son:

| Recurso | Nombre |
| --- | --- |
| Resource Group | `rg-loyaltycloud-stg` |
| API App Service Plan Linux | `asp-loyaltycloud-api-stg-01` |
| API Linux App Service | `loyaltycloud-api-stg-01` |
| Admin App Service Plan Windows | `asp-loyaltycloud-admin-stg-01` |
| Admin Windows App Service | `loyaltycloud-admin-stg-01` |
| SQL Server | `sql-loyaltycloud-stg-01` |
| SQL Database | `LoyaltyCloudStg` |
| Storage Account | `stloyaltycloudstg01` |
| Blob Container | `passes` |
| Key Vault | `kv-loyaltycloud-stg-01` |
| API Managed Identity | System-assigned identity de `loyaltycloud-api-stg-01` |
| Admin Managed Identity | System-assigned identity de `loyaltycloud-admin-stg-01` |
| API URL | `https://loyaltycloud-api-stg-01.azurewebsites.net` |
| Admin URL | `https://loyaltycloud-admin-stg-01.azurewebsites.net` |

RBAC configurado:

- `Key Vault Secrets User` para la Managed Identity de API sobre `kv-loyaltycloud-stg-01`.
- `Key Vault Secrets User` para la Managed Identity de Admin sobre `kv-loyaltycloud-stg-01`.

## 3. Comandos Azure CLI

El procedimiento final se ejecuta mediante scripts PowerShell del repositorio:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -Execute
```

Para permitir migraciones o validacion desde una IP local especifica:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -DeveloperIp <public-ip> -Execute
```

Para permitir conexiones desde servicios Azure:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -AllowAzureServices -Execute
```

Para configurar secretos manuales, ejecutar solo el bloque que corresponda.

Admin API:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureAdminApi -Execute
```

SuperAdmin:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureSuperAdmin -Execute
```

Apple Wallet:

```powershell
.\infra\configure-stg-secrets.ps1 -Suffix 01 -ConfigureAppleWallet -Execute
```

Google Wallet:

```powershell
.\infra\configure-stg-secrets.ps1 `
  -Suffix 01 `
  -ConfigureGoogleWallet `
  -GoogleWalletServiceAccountJsonPath "C:\secure\google-wallet-stg.json" `
  -Execute
```

Los comandos efectivos finales que ejecuta el script de infraestructura son:

```powershell
az group create --name rg-loyaltycloud-stg --location westus3 -o none
```

```powershell
az appservice plan create --resource-group rg-loyaltycloud-stg --name asp-loyaltycloud-api-stg-01 --location westus3 --sku B1 --is-linux -o none
```

```powershell
az appservice plan create --resource-group rg-loyaltycloud-stg --name asp-loyaltycloud-admin-stg-01 --location westus3 --sku B1 -o none
```

```powershell
az sql server create --resource-group rg-loyaltycloud-stg --name sql-loyaltycloud-stg-01 --location westus3 --admin-user loyaltysqladmin --admin-password <prompted-securely> -o none
```

```powershell
az sql db create --resource-group rg-loyaltycloud-stg --server sql-loyaltycloud-stg-01 --name LoyaltyCloudStg --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 1 --min-capacity 0.5 --auto-pause-delay 60 --max-size 32GB --backup-storage-redundancy Local --zone-redundant false -o none
```

```powershell
az storage account create --resource-group rg-loyaltycloud-stg --name stloyaltycloudstg01 --location westus3 --sku Standard_LRS --kind StorageV2 --https-only true --min-tls-version TLS1_2 --allow-blob-public-access false -o none
```

```powershell
az storage container create --name passes --connection-string <stg-storage-connection-string> --public-access off -o none
```

```powershell
az keyvault create --resource-group rg-loyaltycloud-stg --name kv-loyaltycloud-stg-01 --location westus3 --enable-rbac-authorization true --retention-days 90 -o none
```

API Linux Web App:

```powershell
az webapp create --resource-group rg-loyaltycloud-stg --plan asp-loyaltycloud-api-stg-01 --name loyaltycloud-api-stg-01 --runtime "DOTNETCORE|9.0" -o none
```

Admin Windows Web App:

```powershell
az webapp create --resource-group rg-loyaltycloud-stg --plan asp-loyaltycloud-admin-stg-01 --name loyaltycloud-admin-stg-01 -o none
```

```powershell
az webapp config set --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-01 --net-framework-version v9.0 -o none
```

Configuracion comun de Web Apps:

```powershell
az webapp update --resource-group rg-loyaltycloud-stg --name <web-app-name> --https-only true -o none
```

```powershell
az webapp config set --resource-group rg-loyaltycloud-stg --name <web-app-name> --ftps-state Disabled --always-on false -o none
```

Managed Identity:

```powershell
az webapp identity assign --resource-group rg-loyaltycloud-stg --name <web-app-name> -o json
```

RBAC de Key Vault:

```powershell
az role assignment create --assignee-object-id <principal-id> --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope /subscriptions/<subscription-id>/resourceGroups/rg-loyaltycloud-stg/providers/Microsoft.KeyVault/vaults/kv-loyaltycloud-stg-01 -o none
```

Secrets automaticos:

```powershell
az keyvault secret set --vault-name kv-loyaltycloud-stg-01 --name loyaltycloud-sql-connection-string --value <stg-sql-connection-string> -o none
```

```powershell
az keyvault secret set --vault-name kv-loyaltycloud-stg-01 --name loyaltycloud-storage-connection-string --value <stg-storage-connection-string> -o none
```

App Settings:

```powershell
az webapp config appsettings set --resource-group rg-loyaltycloud-stg --name loyaltycloud-api-stg-01 --settings <settings> -o none
```

```powershell
az webapp config appsettings set --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-01 --settings <settings> -o none
```

## 4. App Settings

### API

App Settings configurados para `loyaltycloud-api-stg-01`:

| Setting | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `DOTNET_ENVIRONMENT` | `Staging` |
| `ConnectionStrings__DefaultConnection` | Key Vault reference a `loyaltycloud-sql-connection-string` |
| `Azure__KeyVaultUri` | `https://kv-loyaltycloud-stg-01.vault.azure.net/` |
| `Azure__BlobStorage__ConnectionString` | Key Vault reference a `loyaltycloud-storage-connection-string` |
| `Azure__BlobStorage__PassContainer` | `passes` |
| `Azure__BlobStorage__SasExpirationMinutes` | `15` |
| `AdminApi__SharedSecret` | Key Vault reference a `loyaltycloud-admin-api-shared-secret` |
| `Apple__WebServiceURL` | `https://loyaltycloud-api-stg-01.azurewebsites.net` |
| `Apple__PassTypeIdentifier` | `pass.com.kbeautymx.loyalty` |
| `Apple__TeamIdentifier` | `HS2XCFGQ75` |
| `Apple__OrganizationName` | `KBeauty MX` |
| `Apple__ApnHost` | `https://api.push.apple.com` |
| `Wallet__UseRealPassSigning` | `true` |
| `Wallet__UseRealApns` | `true` |
| `Cors__AllowedOrigins` | `https://loyaltycloud-admin-stg-01.azurewebsites.net` |
| `LoyaltyMaintenance__Enabled` | `true` |
| `LoyaltyMaintenance__RunOnStartup` | `false` |
| `LoyaltyMaintenance__IntervalHours` | `12` |
| `LoyaltyMaintenance__RunAtLocalTime` | `02:00` |
| `LoyaltyMaintenance__TimeZoneId` | `America/Tijuana` |
| `LoyaltyNotifications__Enabled` | `true` |
| `LoyaltyNotifications__RunOnStartup` | `false` |
| `LoyaltyNotifications__PollIntervalSeconds` | `43200` |
| `LoyaltyNotifications__BatchSize` | `25` |
| `LoyaltyNotifications__MaxAttempts` | `3` |
| `LoyaltyNotifications__VisibleEventPriorityHours` | `24` |
| `CustomNotificationCampaigns__BatchSize` | `50` |
| `GoogleWallet__Enabled` | `false` |
| `GoogleWallet__IssuerId` | empty |
| `GoogleWallet__ClassSuffix` | `loyalty` |
| `GoogleWallet__ObjectIdPrefix` | `member` |
| `GoogleWallet__ProgramName` | `KBeauty Loyalty` |
| `GoogleWallet__IssuerName` | `KBeauty MX` |
| `GoogleWallet__LogoUri` | empty |
| `GoogleWallet__HeroImageUri` | empty |
| `GoogleWallet__HexBackgroundColor` | `#FFFFFF` |
| `GoogleWallet__ServiceAccountJson` | Key Vault reference a `loyaltycloud-google-wallet-service-account-json` |
| `Provisioning__TrialDays` | `14` |
| `Billing__GracePeriodDays` | `7` |

### Admin

App Settings configurados para `loyaltycloud-admin-stg-01`:

| Setting | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `DOTNET_ENVIRONMENT` | `Staging` |
| `ConnectionStrings__DefaultConnection` | Key Vault reference a `loyaltycloud-sql-connection-string` |
| `Azure__KeyVaultUri` | `https://kv-loyaltycloud-stg-01.vault.azure.net/` |
| `Azure__BlobStorage__ConnectionString` | Key Vault reference a `loyaltycloud-storage-connection-string` |
| `Azure__BlobStorage__PassContainer` | `passes` |
| `Azure__BlobStorage__SasExpirationMinutes` | `15` |
| `Admin__ApiBaseUrl` | `https://loyaltycloud-api-stg-01.azurewebsites.net` |
| `Admin__Auth__SessionHours` | `168` |
| `AdminApi__SharedSecret` | Key Vault reference a `loyaltycloud-admin-api-shared-secret` |
| `SuperAdmin__Username` | Key Vault reference a `loyaltycloud-superadmin-username` |
| `SuperAdmin__PasswordHash` | Key Vault reference a `loyaltycloud-superadmin-password-hash` |
| `SuperAdmin__SessionHours` | `8` |
| `Apple__WebServiceURL` | `https://loyaltycloud-api-stg-01.azurewebsites.net` |
| `Apple__PassTypeIdentifier` | `pass.com.kbeautymx.loyalty` |
| `Apple__TeamIdentifier` | `HS2XCFGQ75` |
| `Apple__OrganizationName` | `KBeauty MX` |
| `Apple__ApnHost` | `https://api.push.apple.com` |
| `Wallet__UseRealPassSigning` | `true` |
| `Wallet__UseRealApns` | `true` |
| `GoogleWallet__Enabled` | `false` |
| `GoogleWallet__IssuerId` | empty |
| `GoogleWallet__ClassSuffix` | `loyalty` |
| `GoogleWallet__ObjectIdPrefix` | `member` |
| `GoogleWallet__ProgramName` | `KBeauty Loyalty` |
| `GoogleWallet__IssuerName` | `KBeauty MX` |
| `GoogleWallet__LogoUri` | empty |
| `GoogleWallet__HeroImageUri` | empty |
| `GoogleWallet__HexBackgroundColor` | `#FFFFFF` |
| `GoogleWallet__ServiceAccountJson` | Key Vault reference a `loyaltycloud-google-wallet-service-account-json` |
| `Provisioning__TrialDays` | `14` |
| `Billing__GracePeriodDays` | `7` |

## 5. Key Vault

Key Vault STG:

```text
kv-loyaltycloud-stg-01
```

### Secretos Creados Automaticamente

| Secret | Contenido |
| --- | --- |
| `loyaltycloud-sql-connection-string` | Connection string completa hacia `sql-loyaltycloud-stg-01.database.windows.net`, DB `LoyaltyCloudStg` |
| `loyaltycloud-storage-connection-string` | Connection string del Storage Account `stloyaltycloudstg01` |

### Secretos Configurados Manualmente

| Secret | Contenido |
| --- | --- |
| `loyaltycloud-admin-api-shared-secret` | Secreto compartido para autenticacion HMAC Admin -> API |
| `loyaltycloud-superadmin-username` | Usuario de Platform Admin |
| `loyaltycloud-superadmin-password-hash` | Hash de password de Platform Admin |
| `kbeauty-pass-certificate` | Certificado Apple Wallet `.p12` codificado en Base64 |
| `kbeauty-pass-certificate-password` | Password del certificado `.p12` |
| `kbeauty-wwdr-certificate` | Certificado WWDR codificado en Base64, opcional; la implementacion actual primero usa el WWDR incluido en el `.p12` o el certificado bundleado `Certificates/AppleWWDRCAG4.cer` |
| `kbeauty-apn-private-key` | Contenido PEM de la llave APNs `.p8` |
| `kbeauty-apn-key-id` | Key ID de APNs |
| `kbeauty-apn-team-id` | Team ID de APNs |
| `loyaltycloud-google-wallet-service-account-json` | JSON de service account de Google Wallet, solo si se habilita Google Wallet |

Los secretos no se guardan en el repositorio. Las Web Apps los consumen mediante Key Vault references y Managed Identity.

## Copiar secretos Apple Wallet desde PROD hacia STG

Para STG se puede copiar exclusivamente la allowlist de secretos Apple Wallet desde PROD hacia STG con:

```powershell
.\infra\copy-apple-wallet-secrets-to-stg.ps1
```

Ese comando es dry-run. No lee valores sensibles y no modifica Azure. Solo valida Key Vaults, revisa que los secretos existan y muestra que copiaria o actualizaria.

Para ejecutar la copia real:

```powershell
.\infra\copy-apple-wallet-secrets-to-stg.ps1 -Execute
```

La ejecucion pide confirmacion exacta:

```text
COPY APPLE WALLET SECRETS TO STG
```

Secretos obligatorios incluidos:

- `kbeauty-pass-certificate`
- `kbeauty-pass-certificate-password`
- `kbeauty-apn-private-key`
- `kbeauty-apn-key-id`
- `kbeauty-apn-team-id`

Secretos opcionales:

- `kbeauty-wwdr-certificate`

`kbeauty-wwdr-certificate` no existe actualmente en `kv-loyaltycloud-894839` y PROD firma Apple Wallet passes correctamente sin ese secreto. En el codigo actual, `PassGeneratorService` busca el certificado WWDR G4 en este orden:

1. Dentro del `.p12`.
2. En el bundle de la aplicacion: `Certificates/AppleWWDRCAG4.cer`.
3. En `IAppleWalletSecretsProvider.GetWwdrCertificateBytesAsync`.

Por eso el script lo trata como opcional: solo lo copia si existe en origen y no aborta si falta.

El script no copia secretos SQL, Storage, SuperAdmin ni Admin API.

Advertencia:

STG reutiliza temporalmente los mismos certificados/APNs de PROD. `Apple__WebServiceURL` sigue apuntando al API STG:

```text
https://loyaltycloud-api-stg-01.azurewebsites.net
```

## 6. SQL

SQL Server STG:

```text
sql-loyaltycloud-stg-01.database.windows.net
```

Database STG:

```text
LoyaltyCloudStg
```

Creacion final:

```powershell
az sql server create --resource-group rg-loyaltycloud-stg --name sql-loyaltycloud-stg-01 --location westus3 --admin-user loyaltysqladmin --admin-password <prompted-securely> -o none
```

```powershell
az sql db create --resource-group rg-loyaltycloud-stg --server sql-loyaltycloud-stg-01 --name LoyaltyCloudStg --edition GeneralPurpose --compute-model Serverless --family Gen5 --capacity 1 --min-capacity 0.5 --auto-pause-delay 60 --max-size 32GB --backup-storage-redundancy Local --zone-redundant false -o none
```

Firewall para Azure services:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -AllowAzureServices -Execute
```

Firewall para IP local de migraciones:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -DeveloperIp <public-ip> -Execute
```

Migraciones EF:

```powershell
dotnet ef migrations list --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

```powershell
dotnet ef database update --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

El objetivo de STG es iniciar con una base separada y limpia. Las migraciones crean el esquema y dejan la base lista para provisionar tenants.

## 7. Deploy

### API

Publish:

```powershell
dotnet publish .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj -c Release -o .\artifacts\api
```

Empaquetado:

```powershell
tar -a -c -f .\artifacts\api.zip -C .\artifacts\api .
```

Deploy:

```powershell
az webapp deploy --resource-group rg-loyaltycloud-stg --name loyaltycloud-api-stg-01 --src-path .\artifacts\api.zip --type zip
```

### Admin

Publish:

```powershell
dotnet publish .\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj -c Release -o .\artifacts\admin
```

Empaquetado:

```powershell
Compress-Archive -Path .\artifacts\admin\* -DestinationPath .\artifacts\admin.zip -Force
```

Deploy:

```powershell
az webapp deploy --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-01 --src-path .\artifacts\admin.zip --type zip
```

## 8. Validaciones

Validaciones realizadas o previstas para confirmar que STG quedo operativo:

### Platform Admin

Abrir:

```text
https://loyaltycloud-admin-stg-01.azurewebsites.net/platform/login
```

Validar:

- La pantalla carga en STG.
- Platform Admin puede iniciar sesion con las credenciales configuradas en Key Vault.
- `/platform/tenants` abre correctamente tras login.

### Conexion SQL

Validar:

```powershell
az sql db show --resource-group rg-loyaltycloud-stg --server sql-loyaltycloud-stg-01 --name LoyaltyCloudStg
```

Validar que las migraciones EF apuntan a `LoyaltyCloudStg` y no a PROD.

### Key Vault

Validar secretos:

```powershell
az keyvault secret list --vault-name kv-loyaltycloud-stg-01
```

Validar que API y Admin tienen Managed Identity y rol `Key Vault Secrets User`.

### App Settings

API:

```powershell
az webapp config appsettings list --resource-group rg-loyaltycloud-stg --name loyaltycloud-api-stg-01
```

Admin:

```powershell
az webapp config appsettings list --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-01
```

Validar especialmente:

- `ASPNETCORE_ENVIRONMENT=Staging`
- `ConnectionStrings__DefaultConnection`
- `Azure__KeyVaultUri`
- `Admin__ApiBaseUrl`
- `Apple__WebServiceURL`
- `Wallet__UseRealPassSigning=true`
- `Wallet__UseRealApns=true`

### Web Apps

API:

```powershell
az webapp show --resource-group rg-loyaltycloud-stg --name loyaltycloud-api-stg-01
```

Admin:

```powershell
az webapp show --resource-group rg-loyaltycloud-stg --name loyaltycloud-admin-stg-01
```

Validar:

- HTTPS only.
- FTPS disabled.
- Managed Identity habilitada.
- API en Linux.
- Admin en Windows.

### Migraciones

```powershell
dotnet ef database update --project .\src\LoyaltyCloud.Infrastructure\LoyaltyCloud.Infrastructure.csproj --startup-project .\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj
```

Confirmar que la base STG queda con el esquema vigente.

### Tenant Vacio

Validar desde Platform Admin que STG puede iniciar sin tenants operativos y que el flujo normal de provisioning crea tenants desde cero.

La base STG no debe copiar datos operativos de PROD.

## 9. Lessons Learned

### PowerShell 5.1 Incompatibilities

Problema:

El script fallo con:

```text
The property 'ArgumentList' cannot be found on this object.
```

Causa:

`ProcessStartInfo.ArgumentList` no existe en Windows PowerShell 5.1 porque corre sobre .NET Framework. El script se ejecuta desde Visual Studio Developer PowerShell / Windows PowerShell 5.1, no necesariamente PowerShell 7.

Solucion definitiva:

`Invoke-AzProcess` se ajusto para usar APIs compatibles con Windows PowerShell 5.1: `ProcessStartInfo.Arguments`, captura explicita de stdout/stderr/exit code y ejecucion compatible sin depender de `ArgumentList`.

### Azure CLI Warnings por stderr

Problema:

`configure-stg-secrets.ps1` trataba cualquier stderr de Azure CLI como error fatal. Azure CLI puede imprimir warnings como actualizaciones disponibles aunque `ExitCode=0`.

Causa:

El wrapper usaba un patron tipo `& az ... 2>&1`, mezclando stderr con errores reales.

Solucion definitiva:

El wrapper robusto captura stdout, stderr y exit code por separado. Solo falla si `ExitCode != 0`. Si `ExitCode == 0`, ignora stderr.

### ResourceGroupNotFound durante Dry Run

Problema:

El dry-run fallaba cuando el Resource Group `rg-loyaltycloud-stg` aun no existia.

Causa:

El script planeaba crear el Resource Group, pero luego intentaba consultar recursos hijos dentro de un Resource Group inexistente. Azure CLI devolvia `ResourceGroupNotFound`.

Solucion definitiva:

El dry-run reconoce `ResourceGroupNotFound` / `ResourceNotFound` / mensajes equivalentes como recurso inexistente esperado para comandos de lectura. Si el Resource Group no existe, los recursos hijos se reportan como `[PLAN] Will create ...` sin abortar.

### Key Vault Not Found

Problema:

Cuando ya existian varios recursos pero faltaba Key Vault, el script fallo en:

```text
ERROR: The Vault 'kv-loyaltycloud-stg-01' not found within subscription.
```

Causa:

La logica generica de not-found no reconocia el mensaje especifico de Key Vault.

Solucion definitiva:

Se agregaron patrones de not-found para Key Vault:

- `The Vault`
- `not found within subscription`
- `VaultNotFound`
- `vault was not found`

Solo se tratan como recurso inexistente esperado en comandos de lectura/show.

### Runtime Linux vs Windows

Problema:

La creacion del Admin fallo con:

```text
Linux Runtime 'dotnet|9' is not supported.
```

Causa:

Se estaba reutilizando la logica de creacion Linux para el Web App Windows. Azure CLI interpreto el comando con `--runtime dotnet|9` como runtime Linux incompatible.

Solucion definitiva:

La creacion quedo separada:

- API Linux: `az webapp create ... --runtime "DOTNETCORE|9.0"`
- Admin Windows: `az webapp create ...` sin runtime y luego `az webapp config set ... --net-framework-version v9.0`

### App Service Plan Linux por Default

Problema:

Azure CLI puede inferir comportamiento Linux/Windows segun plan y parametros. Mezclar runtime Linux/Windows produce errores confusos.

Causa:

El script no distinguia claramente la ruta de creacion para Web Apps Linux y Windows.

Solucion definitiva:

El script maneja explicitamente:

- Linux App Service Plan con `--is-linux`.
- Windows App Service Plan sin `--is-linux`.
- Web App Linux con runtime Linux.
- Web App Windows con configuracion posterior de framework.

### Caracter Pipe en Runtime

Problema:

Los runtimes de Azure CLI tienen valores como:

```text
DOTNETCORE|9.0
dotnet|9
```

Causa:

En shell, `|` puede interpretarse como pipe si no se pasa correctamente como argumento.

Solucion definitiva:

El wrapper de procesos quotea argumentos de forma segura para que `DOTNETCORE|9.0` y `dotnet|9` se pasen como identificadores completos, no como operadores de shell.

### SQL Firewall

Problema:

Las migraciones EF o validaciones locales no pueden conectarse a Azure SQL si la IP no esta permitida.

Causa:

Azure SQL bloquea conexiones externas salvo que exista regla de firewall.

Solucion definitiva:

El script soporta:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -DeveloperIp <public-ip> -Execute
```

Y para servicios Azure:

```powershell
.\infra\create-stg.ps1 -Suffix 01 -AllowAzureServices -Execute
```

### Idempotencia y Password SQL

Problema:

Una segunda ejecucion parcial no debe pedir ni resetear password SQL si el servidor ya existe.

Causa:

El password SQL solo debe solicitarse cuando se va a crear realmente el SQL Server.

Solucion definitiva:

El prompt de password SQL queda dentro del flujo de creacion del SQL Server. Si `sql-loyaltycloud-stg-01` ya existe, el script no pide password ni intenta recrearlo.

### Key Vault RBAC

Problema:

API/Admin necesitan leer secretos desde Key Vault usando Managed Identity.

Causa:

Crear la Managed Identity no concede acceso automaticamente al Key Vault.

Solucion definitiva:

El script asigna `Key Vault Secrets User` a cada Managed Identity sobre `kv-loyaltycloud-stg-01`, con logica idempotente.

### Bicep Reemplazado por PowerShell

Problema:

Se habia explorado Bicep, pero el flujo real elegido para STG fue PowerShell + Azure CLI.

Causa:

Para esta etapa se necesitaba un script operativo, incremental, dry-run first e idempotente.

Solucion definitiva:

El procedimiento oficial de STG usa:

```powershell
.\infra\create-stg.ps1
.\infra\configure-stg-secrets.ps1
```

El Bicep queda fuera del procedimiento real de STG.

## 10. No Modificar Codigo

Este documento describe el proceso real de creacion de STG.

Para esta documentacion:

- No se debe modificar codigo productivo.
- No se debe ejecutar build.
- No se debe ejecutar tests.
- No se debe ejecutar deploy.
- No se deben crear recursos Azure.
- No se debe ejecutar database update.
- No se debe hacer commit.

El unico archivo creado para esta tarea es:

```text
docs/STAGING_SETUP.md
```
