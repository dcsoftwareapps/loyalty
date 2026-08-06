<#
.SYNOPSIS
Creates LoyaltyCloud staging infrastructure with Azure CLI.

.DESCRIPTION
Dry-run by default. Use -Execute to create or update Azure resources.
This script is intentionally scoped to STAGING and contains explicit guards
against production resource names.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9]+$')]
    [string]$Suffix,

    [string]$Location = "westus3",

    [string]$ResourceGroup = "rg-loyaltycloud-stg",

    [string]$SubscriptionId,

    [string]$SqlAdminUser = "loyaltysqladmin",

    [string]$LinuxPlanSku = "B1",

    [string]$WindowsPlanSku = "B1",

    [string]$LinuxRuntime = "DOTNETCORE:9.0",

    [string]$WindowsRuntime = "DOTNET:9.0",

    [string]$SqlEdition = "GeneralPurpose",

    [string]$SqlFamily = "Gen5",

    [double]$SqlMinCapacity = 0.5,

    [int]$SqlMaxCapacity = 1,

    [int]$SqlAutoPauseDelayMinutes = 60,

    [string]$SqlMaxSize = "32GB",

    [switch]$AllowAzureServices,

    [string]$DeveloperIp,

    [switch]$Execute
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ProdResourceNames = @(
    "rg-loyaltycloud-prod",
    "loyaltycloud-api-894839",
    "loyaltycloud-admin",
    "sql-loyaltycloud-894839",
    "LoyaltyCloudFree",
    "kv-loyaltycloud-894839",
    "stloyaltycloud894839"
)

$ApiPlanName = "asp-loyaltycloud-api-stg-$Suffix"
$ApiAppName = "loyaltycloud-api-stg-$Suffix"
$AdminPlanName = "asp-loyaltycloud-admin-stg-$Suffix"
$AdminAppName = "loyaltycloud-admin-stg-$Suffix"
$SqlServerName = "sql-loyaltycloud-stg-$Suffix"
$SqlDatabaseName = "LoyaltyCloudStg"
$KeyVaultName = "kv-loyaltycloud-stg-$Suffix"
$StorageAccountName = ("stloyaltycloudstg$Suffix").ToLowerInvariant() -replace '[^a-z0-9]', ''
$ApiUrl = "https://$ApiAppName.azurewebsites.net"
$AdminUrl = "https://$AdminAppName.azurewebsites.net"
$KeyVaultUri = "https://$KeyVaultName.vault.azure.net/"
$PassContainerName = "passes"
$plainSqlPassword = $null
$securePassword = $null
$script:PlainSqlPassword = $null
$script:ResourceGroupExists = $false
$script:DetectedLinuxRuntime = $LinuxRuntime
$script:DetectedWindowsRuntime = $WindowsRuntime

$AutomaticSecretNames = @{
    SqlConnectionString = "loyaltycloud-sql-connection-string"
    StorageConnectionString = "loyaltycloud-storage-connection-string"
}

$ManualSecretNames = @(
    "loyaltycloud-admin-api-shared-secret",
    "loyaltycloud-superadmin-username",
    "loyaltycloud-superadmin-password-hash",
    "kbeauty-pass-certificate",
    "kbeauty-pass-certificate-password",
    "kbeauty-wwdr-certificate",
    "kbeauty-apn-private-key",
    "kbeauty-apn-key-id",
    "kbeauty-apn-team-id",
    "loyaltycloud-google-wallet-service-account-json"
)

function Write-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Kind,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host "[$Kind] $Message"
}

function ConvertTo-CommandLineArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    if ($Value -notmatch '[\s"|&<>()^|]') {
        return $Value
    }

    $escaped = $Value -replace '\\(?=")', '\\' -replace '"', '\"'
    return '"' + $escaped + '"'
}

function Show-PowerShellRuntime {
    $edition = if ($PSVersionTable.ContainsKey('PSEdition')) { $PSVersionTable.PSEdition } else { 'Desktop' }
    Write-Step "CHECK" "PowerShell Edition: $edition"
    Write-Step "CHECK" "PowerShell Version: $($PSVersionTable.PSVersion)"
}

function Invoke-AzProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $azCommand = Get-Command az -ErrorAction Stop
    $azPath = $azCommand.Source
    $quotedAzPath = ConvertTo-CommandLineArgument $azPath
    $quotedArguments = @($Arguments | ForEach-Object { ConvertTo-CommandLineArgument $_ })
    $azCommandLine = (@($quotedAzPath) + $quotedArguments) -join ' '

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $env:ComSpec
    $startInfo.Arguments = "/d /s /c ""$azCommandLine"""
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = $stdout
        StdErr = $stderr
    }
}

function Invoke-AzCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$Write,
        [switch]$AsJson,
        [switch]$Sensitive
    )

    $display = "az $($Arguments -join ' ')"
    if ($Sensitive) {
        $display = "az <sensitive command hidden>"
    }

    if ($Write -and -not $Execute) {
        Write-Step "PLAN" $display
        return $null
    }

    Write-Step ($(if ($Write) { "CREATE" } else { "CHECK" })) $display
    $result = Invoke-AzProcess -Arguments $Arguments
    $output = if ($null -eq $result.StdOut) { '' } else { $result.StdOut.Trim() }
    $errorText = if ($null -eq $result.StdErr) { '' } else { $result.StdErr.Trim() }
    if ($result.ExitCode -ne 0) {
        $details = if ([string]::IsNullOrWhiteSpace($errorText)) { $output } else { $errorText }
        throw "Azure CLI command failed: $display`n$details"
    }

    if ($AsJson -and $output) {
        return ($output | ConvertFrom-Json)
    }

    return $output
}

function Test-AzExpectedNotFound {
    param(
        [string]$Message,
        [string[]]$Arguments = @()
    )

    $isReadCommand = Test-AzReadCommand -Arguments $Arguments
    $isKeyVaultNotFound = $isReadCommand -and (
        $Message -match '(?i)VaultNotFound' `
        -or $Message -match '(?i)vault was not found' `
        -or ($Message -match '(?i)The Vault' -and $Message -match '(?i)not found within subscription')
    )

    return $Message -match '(?i)ResourceGroupNotFound' `
        -or $Message -match '(?i)ResourceNotFound' `
        -or $Message -match '(?i)ResourceNotFoundError' `
        -or $Message -match '(?i)was not found' `
        -or $Message -match '(?i)could not be found' `
        -or $Message -match '(?i)does not exist' `
        -or $isKeyVaultNotFound
}

function Test-AzReadCommand {
    param([string[]]$Arguments)

    return $Arguments -contains "show" `
        -or $Arguments -contains "list" `
        -or $Arguments -contains "exists"
}

function Test-AzFatalError {
    param([string]$Message)

    return $Message -match '(?i)AuthenticationFailed' `
        -or $Message -match '(?i)AuthorizationFailed' `
        -or $Message -match '(?i)InvalidAuthenticationToken' `
        -or $Message -match '(?i)ExpiredAuthenticationToken' `
        -or $Message -match '(?i)Please run.*az login' `
        -or $Message -match '(?i)az login' `
        -or $Message -match '(?i)SubscriptionNotFound' `
        -or $Message -match '(?i)InvalidSubscription' `
        -or $Message -match '(?i)unrecognized arguments' `
        -or $Message -match '(?i)invalid choice' `
        -or $Message -match '(?i)is misspelled or not recognized'
}

function Test-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI is not installed or not in PATH. Install it first, then run: az login"
    }

    Invoke-AzCli @("--version") | Out-Null
    Invoke-AzCli @("help") | Out-Null
    Invoke-AzCli @("appservice", "plan", "create", "--help") | Out-Null
    Invoke-AzCli @("webapp", "create", "--help") | Out-Null
    Invoke-AzCli @("sql", "db", "create", "--help") | Out-Null
    Invoke-AzCli @("storage", "account", "create", "--help") | Out-Null
    Invoke-AzCli @("keyvault", "create", "--help") | Out-Null
}

function Test-AzLogin {
    $account = Invoke-AzCli @("account", "show", "-o", "json") -AsJson
    if (-not $account) {
        throw "Azure CLI is not logged in. Run: az login"
    }

    return $account
}

function Select-SubscriptionIfRequested {
    if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
        return
    }

    Invoke-AzCli @("account", "set", "--subscription", $SubscriptionId) | Out-Null
}

function Test-StagingNames {
    $allNames = @(
        $ResourceGroup,
        $ApiPlanName,
        $ApiAppName,
        $AdminPlanName,
        $AdminAppName,
        $SqlServerName,
        $SqlDatabaseName,
        $KeyVaultName,
        $StorageAccountName
    )

    foreach ($name in $allNames) {
        if ($name -match '(?i)prod') {
            throw "Production guard triggered: resource name contains 'prod': $name"
        }
        if ($ProdResourceNames -contains $name) {
            throw "Production guard triggered: resource name matches production: $name"
        }
    }

    if ($SqlDatabaseName -eq "LoyaltyCloudFree") {
        throw "Production guard triggered: staging database cannot be LoyaltyCloudFree."
    }
    if ($StorageAccountName.Length -lt 3 -or $StorageAccountName.Length -gt 24) {
        throw "Storage account name must be 3-24 characters: $StorageAccountName"
    }
    if ($StorageAccountName -notmatch '^[a-z0-9]+$') {
        throw "Storage account name must be lowercase alphanumeric only: $StorageAccountName"
    }
}

function Confirm-Execution {
    if (-not $Execute) {
        Write-Step "PLAN" "Dry-run only. Re-run with -Execute to create or update STAGING resources."
        return
    }

    Write-Host ""
    Write-Step "WARNING" "TARGET ENVIRONMENT: STAGING"
    Write-Step "WARNING" "This will create/update resources in '$ResourceGroup'. Production guards are active."
    $answer = Read-Host "Type CREATE STAGING to continue"
    if ($answer -ne "CREATE STAGING") {
        throw "Confirmation did not match. Aborting."
    }
}

function Get-ResourceIdOrNull {
    param([string[]]$Arguments)

    $result = Invoke-AzProcess -Arguments $Arguments
    $stdout = if ($null -eq $result.StdOut) { '' } else { $result.StdOut.Trim() }
    $stderr = if ($null -eq $result.StdErr) { '' } else { $result.StdErr.Trim() }
    $text = (@($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n"

    if ($result.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($stdout)) {
        return ($stdout | ConvertFrom-Json)
    }

    if ($result.ExitCode -eq 0) {
        return $null
    }

    if (Test-AzFatalError $text) {
        throw "Azure CLI command failed with a fatal error: az $($Arguments -join ' ')`n$text"
    }

    if (Test-AzExpectedNotFound -Message $text -Arguments $Arguments) {
        return $null
    }

    throw "Azure CLI command failed while checking resource state: az $($Arguments -join ' ')`n$text"
}

function Ensure-ResourceGroup {
    $existing = Get-ResourceIdOrNull @("group", "show", "--name", $ResourceGroup, "-o", "json")
    if ($existing) {
        Write-Step "SKIP" "Resource group already exists: $ResourceGroup"
        $script:ResourceGroupExists = $true
        return
    }

    $script:ResourceGroupExists = $false
    Invoke-AzCli @("group", "create", "--name", $ResourceGroup, "--location", $Location, "-o", "none") -Write | Out-Null
    if ($Execute) {
        $script:ResourceGroupExists = $true
    }
}

function Ensure-AppServicePlan {
    param(
        [string]$Name,
        [string]$Sku,
        [switch]$Linux
    )

    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create App Service Plan: $Name"
        return
    }

    $existing = Get-ResourceIdOrNull @("appservice", "plan", "show", "--resource-group", $ResourceGroup, "--name", $Name, "-o", "json")
    if ($existing) {
        Write-Step "SKIP" "App Service Plan already exists: $Name"
        return
    }

    $args = @("appservice", "plan", "create", "--resource-group", $ResourceGroup, "--name", $Name, "--location", $Location, "--sku", $Sku)
    if ($Linux) {
        $args += "--is-linux"
    }
    $args += @("-o", "none")
    Invoke-AzCli $args -Write | Out-Null
}

function Resolve-WebAppRuntime {
    param(
        [string]$Runtime,
        [string]$OsType
    )

    $runtimes = Invoke-AzCli @("webapp", "list-runtimes", "--os-type", $OsType, "-o", "tsv")
    $runtimeLines = @($runtimes -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    Write-Step "CHECK" "Available App Service runtimes for $OsType from Azure CLI:"
    foreach ($line in $runtimeLines) {
        Write-Host "  $line"
    }

    $dotnet9 = @($runtimeLines |
        ForEach-Object {
            $identifier = Get-RuntimeIdentifier $_
            [pscustomobject]@{
                Line = $_
                Identifier = $identifier
            }
        } |
        Where-Object {
            $_.Identifier -match '(?i)^(DOTNET|DOTNETCORE)[|:]?9(\.0)?$' `
                -or ($_.Identifier -match '(?i)^(DOTNET|DOTNETCORE)' -and $_.Identifier -match '9(\.0)?')
        })

    if ($dotnet9.Count -gt 0) {
        Write-Step "CHECK" "Detected .NET 9 runtime for ${OsType}: $($dotnet9[0].Identifier)"
        return $dotnet9[0].Identifier
    }

    $configuredRuntimeExists = @($runtimeLines | ForEach-Object { Get-RuntimeIdentifier $_ }) -contains $Runtime
    if ($configuredRuntimeExists) {
        Write-Step "WARNING" "Could not auto-detect .NET 9 for $OsType, but configured runtime '$Runtime' exists."
        return $Runtime
    }

    Write-Step "WARNING" "Could not detect a .NET 9 runtime for $OsType. Configured value '$Runtime' was not found. Review Azure CLI runtime names before using -Execute."
    return $Runtime
}

function Get-RuntimeIdentifier {
    param([string]$RuntimeLine)

    if ([string]::IsNullOrWhiteSpace($RuntimeLine)) {
        return ''
    }

    return (($RuntimeLine.Trim() -split '\s+')[0]).Trim()
}

function ConvertTo-WindowsDotNetFrameworkVersion {
    param([string]$Runtime)

    if ($Runtime -match '(\d+)(?:\.0)?') {
        return "v$($Matches[1]).0"
    }

    Write-Step "WARNING" "Could not derive Windows net framework version from runtime '$Runtime'. Falling back to v9.0."
    return "v9.0"
}

function Ensure-WebApp {
    param(
        [string]$Name,
        [string]$Plan,
        [string]$Runtime,
        [string]$OsType
    )

    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create Web App: $Name"
        Write-Step "PLAN" "Will configure HTTPS only, FTPS disabled, Always On false and system-assigned identity for: $Name"
        return
    }

    $existing = Get-ResourceIdOrNull @("webapp", "show", "--resource-group", $ResourceGroup, "--name", $Name, "-o", "json")
    if (-not $existing) {
        if ($OsType -eq "linux") {
            Invoke-AzCli @("webapp", "create", "--resource-group", $ResourceGroup, "--plan", $Plan, "--name", $Name, "--runtime", $Runtime, "-o", "none") -Write | Out-Null
        }
        else {
            Invoke-AzCli @("webapp", "create", "--resource-group", $ResourceGroup, "--plan", $Plan, "--name", $Name, "-o", "none") -Write | Out-Null
        }
    }
    else {
        Write-Step "SKIP" "Web App already exists: $Name"
    }

    Invoke-AzCli @("webapp", "update", "--resource-group", $ResourceGroup, "--name", $Name, "--https-only", "true", "-o", "none") -Write | Out-Null
    Invoke-AzCli @("webapp", "config", "set", "--resource-group", $ResourceGroup, "--name", $Name, "--ftps-state", "Disabled", "--always-on", "false", "-o", "none") -Write | Out-Null
    if ($OsType -eq "windows") {
        $netFrameworkVersion = ConvertTo-WindowsDotNetFrameworkVersion -Runtime $Runtime
        Invoke-AzCli @("webapp", "config", "set", "--resource-group", $ResourceGroup, "--name", $Name, "--net-framework-version", $netFrameworkVersion, "-o", "none") -Write | Out-Null
    }
    Invoke-AzCli @("webapp", "identity", "assign", "--resource-group", $ResourceGroup, "--name", $Name, "-o", "json") -Write -AsJson | Out-Null
}

function Ensure-SqlServer {
    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create SQL Server: $SqlServerName"
        return
    }

    $existing = Get-ResourceIdOrNull @("sql", "server", "show", "--resource-group", $ResourceGroup, "--name", $SqlServerName, "-o", "json")
    if ($existing) {
        Write-Step "SKIP" "SQL Server already exists: $SqlServerName"
        return
    }

    if ($Execute) {
        $securePassword = Read-Host "SQL admin password for STAGING" -AsSecureString
        $script:PlainSqlPassword = [System.Net.NetworkCredential]::new("", $securePassword).Password
        $securePassword.Dispose()

        if ([string]::IsNullOrWhiteSpace($script:PlainSqlPassword)) {
            throw "SQL password cannot be empty."
        }
    }
    else {
        $script:PlainSqlPassword = "<dry-run-sql-password>"
    }

    Invoke-AzCli @("sql", "server", "create", "--resource-group", $ResourceGroup, "--name", $SqlServerName, "--location", $Location, "--admin-user", $SqlAdminUser, "--admin-password", $script:PlainSqlPassword, "-o", "none") -Write -Sensitive | Out-Null
}

function Ensure-SqlDatabase {
    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create SQL Database: $SqlDatabaseName"
        return
    }

    $existing = Get-ResourceIdOrNull @("sql", "db", "show", "--resource-group", $ResourceGroup, "--server", $SqlServerName, "--name", $SqlDatabaseName, "-o", "json")
    if ($existing) {
        Write-Step "SKIP" "SQL Database already exists: $SqlDatabaseName"
        return
    }

    Invoke-AzCli @(
        "sql", "db", "create",
        "--resource-group", $ResourceGroup,
        "--server", $SqlServerName,
        "--name", $SqlDatabaseName,
        "--edition", $SqlEdition,
        "--compute-model", "Serverless",
        "--family", $SqlFamily,
        "--capacity", "$SqlMaxCapacity",
        "--min-capacity", "$SqlMinCapacity",
        "--auto-pause-delay", "$SqlAutoPauseDelayMinutes",
        "--max-size", $SqlMaxSize,
        "--backup-storage-redundancy", "Local",
        "--zone-redundant", "false",
        "-o", "none"
    ) -Write | Out-Null
}

function Ensure-SqlFirewallRules {
    if ($AllowAzureServices) {
        Invoke-AzCli @("sql", "server", "firewall-rule", "create", "--resource-group", $ResourceGroup, "--server", $SqlServerName, "--name", "AllowAllWindowsAzureIps", "--start-ip-address", "0.0.0.0", "--end-ip-address", "0.0.0.0", "-o", "none") -Write | Out-Null
    }
    else {
        Write-Step "SKIP" "AllowAzureServices not requested."
    }

    if (-not [string]::IsNullOrWhiteSpace($DeveloperIp)) {
        if ($DeveloperIp -notmatch '^\d{1,3}(\.\d{1,3}){3}$') {
            throw "DeveloperIp must be a single IPv4 address."
        }

        Write-Step "WARNING" "DeveloperIp firewall rule will allow only $DeveloperIp. Confirm this IP before using it for migrations."
        Invoke-AzCli @("sql", "server", "firewall-rule", "create", "--resource-group", $ResourceGroup, "--server", $SqlServerName, "--name", "DeveloperIp", "--start-ip-address", $DeveloperIp, "--end-ip-address", $DeveloperIp, "-o", "none") -Write | Out-Null
    }
}

function Ensure-StorageAccount {
    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create Storage Account: $StorageAccountName"
        return
    }

    $existing = Get-ResourceIdOrNull @("storage", "account", "show", "--resource-group", $ResourceGroup, "--name", $StorageAccountName, "-o", "json")
    if (-not $existing) {
        Invoke-AzCli @("storage", "account", "create", "--resource-group", $ResourceGroup, "--name", $StorageAccountName, "--location", $Location, "--sku", "Standard_LRS", "--kind", "StorageV2", "--https-only", "true", "--min-tls-version", "TLS1_2", "--allow-blob-public-access", "false", "-o", "none") -Write | Out-Null
    }
    else {
        Write-Step "SKIP" "Storage account already exists: $StorageAccountName"
    }
}

function Get-StorageConnectionString {
    if (-not $Execute) {
        return "<dry-run-storage-connection-string>"
    }

    return Invoke-AzCli @("storage", "account", "show-connection-string", "--resource-group", $ResourceGroup, "--name", $StorageAccountName, "--query", "connectionString", "-o", "tsv") -Sensitive
}

function Ensure-BlobContainer {
    param([string]$Name)

    $connectionString = Get-StorageConnectionString
    Invoke-AzCli @("storage", "container", "create", "--name", $Name, "--connection-string", $connectionString, "--public-access", "off", "-o", "none") -Write -Sensitive | Out-Null
}

function Ensure-KeyVault {
    if (-not $Execute -and -not $script:ResourceGroupExists) {
        Write-Step "PLAN" "Will create Key Vault: $KeyVaultName"
        return
    }

    $existing = Get-ResourceIdOrNull @("keyvault", "show", "--name", $KeyVaultName, "-o", "json")
    if (-not $existing) {
        Invoke-AzCli @("keyvault", "create", "--resource-group", $ResourceGroup, "--name", $KeyVaultName, "--location", $Location, "--enable-rbac-authorization", "true", "--retention-days", "90", "-o", "none") -Write | Out-Null
    }
    else {
        if ($existing.resourceGroup -ne $ResourceGroup) {
            throw "Key Vault '$KeyVaultName' exists outside target resource group '$ResourceGroup'."
        }
        Write-Step "SKIP" "Key Vault already exists: $KeyVaultName"
    }
}

function Get-WebAppPrincipalId {
    param([string]$Name)

    if (-not $Execute) {
        return "<dry-run-principal-$Name>"
    }

    return Invoke-AzCli @("webapp", "identity", "show", "--resource-group", $ResourceGroup, "--name", $Name, "--query", "principalId", "-o", "tsv")
}

function Ensure-KeyVaultRole {
    param(
        [string]$PrincipalId,
        [string]$DisplayName
    )

    $scope = "/subscriptions/$((Invoke-AzCli @("account", "show", "--query", "id", "-o", "tsv")))/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
    if (-not $Execute) {
        Write-Step "PLAN" "az role assignment create --assignee-object-id <principal:$DisplayName> --role 'Key Vault Secrets User' --scope $scope"
        return
    }

    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Invoke-AzCli @("role", "assignment", "create", "--assignee-object-id", $PrincipalId, "--assignee-principal-type", "ServicePrincipal", "--role", "Key Vault Secrets User", "--scope", $scope, "-o", "none") -Write | Out-Null
            return
        }
        catch {
            if ($attempt -eq 6) {
                throw
            }
            Write-Step "WARNING" "Role assignment for $DisplayName not ready yet. Waiting before retry $($attempt + 1)/6."
            Start-Sleep -Seconds 15
        }
    }
}

function Set-KeyVaultSecret {
    param(
        [string]$Name,
        [string]$Value
    )

    Invoke-AzCli @("keyvault", "secret", "set", "--vault-name", $KeyVaultName, "--name", $Name, "--value", $Value, "-o", "none") -Write -Sensitive | Out-Null
}

function Ensure-DerivedSecrets {
    $storageConnectionString = Get-StorageConnectionString

    if ([string]::IsNullOrWhiteSpace($script:PlainSqlPassword)) {
        Write-Step "WARNING" "SQL Server already existed, so SQL password was not requested. Skipping automatic secret '$($AutomaticSecretNames.SqlConnectionString)'. Configure it manually if it does not already exist."
    }
    else {
        $sqlConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminUser;Password=$($script:PlainSqlPassword);MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        Set-KeyVaultSecret -Name $AutomaticSecretNames.SqlConnectionString -Value $sqlConnectionString
        $sqlConnectionString = $null
    }

    Set-KeyVaultSecret -Name $AutomaticSecretNames.StorageConnectionString -Value $storageConnectionString

    $storageConnectionString = $null
}

function Get-KeyVaultReference {
    param([string]$SecretName)
    return "@Microsoft.KeyVault(SecretUri=$KeyVaultUri/secrets/$SecretName)"
}

function Ensure-AppSettings {
    param(
        [string]$AppName,
        [hashtable]$Settings
    )

    $arguments = @("webapp", "config", "appsettings", "set", "--resource-group", $ResourceGroup, "--name", $AppName, "--settings")
    foreach ($key in $Settings.Keys) {
        $arguments += "$key=$($Settings[$key])"
    }
    $arguments += @("-o", "none")

    Invoke-AzCli $arguments -Write -Sensitive | Out-Null
}

function Show-Plan {
    $account = Test-AzLogin

    Write-Host ""
    Write-Step "PLAN" "TARGET ENVIRONMENT: STAGING"
    Write-Step "PLAN" "Subscription: $($account.name) ($($account.id))"
    Write-Step "PLAN" "Tenant: $($account.tenantId)"
    Write-Step "PLAN" "Resource Group: $ResourceGroup"
    Write-Step "PLAN" "Location: $Location"
    Write-Step "PLAN" "API Plan: $ApiPlanName ($LinuxPlanSku, Linux)"
    Write-Step "PLAN" "API App: $ApiAppName"
    Write-Step "PLAN" "Admin Plan: $AdminPlanName ($WindowsPlanSku, Windows)"
    Write-Step "PLAN" "Admin App: $AdminAppName"
    Write-Step "PLAN" "SQL Server: $SqlServerName"
    Write-Step "PLAN" "SQL Database: $SqlDatabaseName"
    Write-Step "PLAN" "Key Vault: $KeyVaultName"
    Write-Step "PLAN" "Storage Account: $StorageAccountName"
    Write-Step "PLAN" "Blob containers: $PassContainerName"
    Write-Step "PLAN" "API URL: $ApiUrl"
    Write-Step "PLAN" "Admin URL: $AdminUrl"
}

function Show-FinalSummary {
    Write-Host ""
    Write-Step "PLAN" "Final summary"
    Write-Host "Subscription ID: $((Invoke-AzCli @("account", "show", "--query", "id", "-o", "tsv")))"
    Write-Host "Resource Group: $ResourceGroup"
    Write-Host "API URL: $ApiUrl"
    Write-Host "Admin URL: $AdminUrl"
    Write-Host "SQL Server: $SqlServerName.database.windows.net"
    Write-Host "DB name: $SqlDatabaseName"
    Write-Host "Key Vault URI: $KeyVaultUri"
    Write-Host "Storage Account: $StorageAccountName"
    Write-Host "Automatic secrets: $($AutomaticSecretNames.Values -join ', ')"
    Write-Host "Manual secrets pending: $($ManualSecretNames -join ', ')"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. .\infra\configure-stg-secrets.ps1 -Suffix $Suffix -ConfigureSuperAdmin -ConfigureAppleWallet"
    Write-Host "2. Apply EF migrations manually to $SqlDatabaseName."
    Write-Host "3. Publish and deploy API Linux package."
    Write-Host "4. Publish and deploy Admin Windows package."
    Write-Host "5. Run smoke tests, Apple Wallet test and Google Wallet test if enabled."

    if (-not $Execute) {
        Write-Step "PLAN" "Dry-run completed successfully."
        Write-Step "PLAN" "No Azure resources were created or modified."
    }
}

try {
    Show-PowerShellRuntime
    Write-Step "CHECK" "Validating staging names and production guards."
    Test-StagingNames
    Test-AzCli
    Select-SubscriptionIfRequested
    Show-Plan
    $script:DetectedLinuxRuntime = Resolve-WebAppRuntime -Runtime $LinuxRuntime -OsType "linux"
    $script:DetectedWindowsRuntime = Resolve-WebAppRuntime -Runtime $WindowsRuntime -OsType "windows"
    Confirm-Execution

    Ensure-ResourceGroup
    Ensure-AppServicePlan -Name $ApiPlanName -Sku $LinuxPlanSku -Linux
    Ensure-AppServicePlan -Name $AdminPlanName -Sku $WindowsPlanSku
    Ensure-SqlServer
    Ensure-SqlDatabase
    Ensure-SqlFirewallRules
    Ensure-StorageAccount
    Ensure-BlobContainer -Name $PassContainerName
    Ensure-KeyVault
    Ensure-WebApp -Name $ApiAppName -Plan $ApiPlanName -Runtime $script:DetectedLinuxRuntime -OsType "linux"
    Ensure-WebApp -Name $AdminAppName -Plan $AdminPlanName -Runtime $script:DetectedWindowsRuntime -OsType "windows"

    $apiPrincipalId = Get-WebAppPrincipalId -Name $ApiAppName
    $adminPrincipalId = Get-WebAppPrincipalId -Name $AdminAppName
    Ensure-KeyVaultRole -PrincipalId $apiPrincipalId -DisplayName $ApiAppName
    Ensure-KeyVaultRole -PrincipalId $adminPrincipalId -DisplayName $AdminAppName
    Ensure-DerivedSecrets

    $commonSettings = @{
        "ASPNETCORE_ENVIRONMENT" = "Staging"
        "DOTNET_ENVIRONMENT" = "Staging"
        "ConnectionStrings__DefaultConnection" = Get-KeyVaultReference $AutomaticSecretNames.SqlConnectionString
        "Azure__KeyVaultUri" = $KeyVaultUri
        "Azure__BlobStorage__ConnectionString" = Get-KeyVaultReference $AutomaticSecretNames.StorageConnectionString
        "Azure__BlobStorage__PassContainer" = $PassContainerName
        "Azure__BlobStorage__SasExpirationMinutes" = "15"
        "AdminApi__SharedSecret" = Get-KeyVaultReference "loyaltycloud-admin-api-shared-secret"
        "Apple__PassTypeIdentifier" = "pass.com.kbeautymx.loyalty"
        "Apple__TeamIdentifier" = "HS2XCFGQ75"
        "Apple__OrganizationName" = "KBeauty MX"
        "Apple__ApnHost" = "https://api.push.apple.com"
        "Wallet__UseRealPassSigning" = "true"
        "Wallet__UseRealApns" = "true"
        "GoogleWallet__Enabled" = "false"
        "GoogleWallet__IssuerId" = ""
        "GoogleWallet__ClassSuffix" = "loyalty"
        "GoogleWallet__ObjectIdPrefix" = "member"
        "GoogleWallet__ProgramName" = "KBeauty Loyalty"
        "GoogleWallet__IssuerName" = "KBeauty MX"
        "GoogleWallet__LogoUri" = ""
        "GoogleWallet__HeroImageUri" = ""
        "GoogleWallet__HexBackgroundColor" = "#FFFFFF"
        "GoogleWallet__ServiceAccountJson" = Get-KeyVaultReference "loyaltycloud-google-wallet-service-account-json"
        "Provisioning__TrialDays" = "14"
        "Billing__GracePeriodDays" = "7"
    }

    $apiSettings = $commonSettings.Clone()
    $apiSettings["Apple__WebServiceURL"] = $ApiUrl
    $apiSettings["Cors__AllowedOrigins"] = $AdminUrl
    $apiSettings["LoyaltyMaintenance__Enabled"] = "true"
    $apiSettings["LoyaltyMaintenance__RunOnStartup"] = "false"
    $apiSettings["LoyaltyMaintenance__IntervalHours"] = "12"
    $apiSettings["LoyaltyMaintenance__RunAtLocalTime"] = "02:00"
    $apiSettings["LoyaltyMaintenance__TimeZoneId"] = "America/Tijuana"
    $apiSettings["LoyaltyNotifications__Enabled"] = "true"
    $apiSettings["LoyaltyNotifications__RunOnStartup"] = "false"
    $apiSettings["LoyaltyNotifications__PollIntervalSeconds"] = "43200"
    $apiSettings["LoyaltyNotifications__BatchSize"] = "25"
    $apiSettings["LoyaltyNotifications__MaxAttempts"] = "3"
    $apiSettings["LoyaltyNotifications__VisibleEventPriorityHours"] = "24"
    $apiSettings["CustomNotificationCampaigns__BatchSize"] = "50"

    $adminSettings = $commonSettings.Clone()
    $adminSettings["Admin__ApiBaseUrl"] = $ApiUrl
    $adminSettings["Admin__Auth__SessionHours"] = "168"
    $adminSettings["SuperAdmin__Username"] = Get-KeyVaultReference "loyaltycloud-superadmin-username"
    $adminSettings["SuperAdmin__PasswordHash"] = Get-KeyVaultReference "loyaltycloud-superadmin-password-hash"
    $adminSettings["SuperAdmin__SessionHours"] = "8"
    $adminSettings["Apple__WebServiceURL"] = $ApiUrl

    Ensure-AppSettings -AppName $ApiAppName -Settings $apiSettings
    Ensure-AppSettings -AppName $AdminAppName -Settings $adminSettings

    Show-FinalSummary
}
finally {
    $script:PlainSqlPassword = $null
    $plainSqlPassword = $null
    if ($securePassword) {
        $securePassword.Dispose()
    }
    [System.GC]::Collect()
}
