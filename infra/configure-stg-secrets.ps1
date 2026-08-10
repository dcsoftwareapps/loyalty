<#
.SYNOPSIS
Loads manual LoyaltyCloud staging secrets into the staging Key Vault.

.DESCRIPTION
This script never prints secret values. It only writes to the STAGING Key Vault
derived from -Suffix unless -KeyVaultName is explicitly provided.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9]+$')]
    [string]$Suffix,

    [string]$KeyVaultName = "kv-loyaltycloud-stg-$Suffix",

    [string]$SubscriptionId,

    [switch]$ConfigureAdminApi,

    [switch]$ConfigureAppleWallet,

    [switch]$ConfigureGoogleWallet,

    [switch]$ConfigureSuperAdmin,

    [string]$PassCertificatePath,

    [string]$WwdrCertificatePath,

    [string]$ApnPrivateKeyPath,

    [string]$GoogleWalletServiceAccountJsonPath,

    [switch]$Execute
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Kind, [string]$Message)
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

    Write-Step ($(if ($Write) { "UPDATE" } else { "CHECK" })) $display
    $result = Invoke-AzProcess -Arguments $Arguments
    $output = if ($null -eq $result.StdOut) { '' } else { $result.StdOut.Trim() }
    $errorText = if ($null -eq $result.StdErr) { '' } else { $result.StdErr.Trim() }
    if ($result.ExitCode -ne 0) {
        $details = if ([string]::IsNullOrWhiteSpace($errorText)) { $output } else { $errorText }
        throw "Azure CLI command failed: $display`n$details"
    }
    return $output
}

function Test-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI is not installed or not in PATH. Run: az login after installing it."
    }
    Invoke-AzCli @("--version") | Out-Null
    Invoke-AzCli @("keyvault", "secret", "set", "--help") | Out-Null
}

function Select-SubscriptionIfRequested {
    if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
        return
    }
    Invoke-AzCli @("account", "set", "--subscription", $SubscriptionId) | Out-Null
}

function Test-KeyVaultName {
    if ($KeyVaultName -match '(?i)prod') {
        throw "Production guard triggered: Key Vault name contains 'prod': $KeyVaultName"
    }
    if ($KeyVaultName -eq "kv-loyaltycloud-894839") {
        throw "Production guard triggered: refusing to write to production Key Vault."
    }
}

function Confirm-Execution {
    Write-Step "PLAN" "TARGET ENVIRONMENT: STAGING"
    Write-Step "PLAN" "Key Vault: $KeyVaultName"
    if (-not $Execute) {
        Write-Step "PLAN" "Dry-run only. Re-run with -Execute to write secrets."
        return
    }

    $answer = Read-Host "Type CONFIGURE STAGING SECRETS to continue"
    if ($answer -ne "CONFIGURE STAGING SECRETS") {
        throw "Confirmation did not match. Aborting."
    }
}

function Read-SecretPlainText {
    param([string]$Prompt)

    $secure = Read-Host $Prompt -AsSecureString
    try {
        return [System.Net.NetworkCredential]::new("", $secure).Password
    }
    finally {
        $secure.Dispose()
    }
}

function Set-KeyVaultSecret {
    param(
        [string]$Name,
        [string]$Value
    )

    Invoke-AzCli @("keyvault", "secret", "set", "--vault-name", $KeyVaultName, "--name", $Name, "--value", $Value, "-o", "none") -Write -Sensitive | Out-Null
}

function Set-KeyVaultSecretFromFileText {
    param(
        [string]$Name,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        throw "File not found for secret '$Name': $Path"
    }
    $value = Get-Content -LiteralPath $Path -Raw
    Set-KeyVaultSecret -Name $Name -Value $value
    $value = $null
}

function Set-KeyVaultSecretFromFileBase64 {
    param(
        [string]$Name,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        throw "File not found for secret '$Name': $Path"
    }
    $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
    $value = [Convert]::ToBase64String($bytes)
    Set-KeyVaultSecret -Name $Name -Value $value
    [Array]::Clear($bytes, 0, $bytes.Length)
    $value = $null
}

function Configure-SuperAdminSecrets {
    if (-not $ConfigureSuperAdmin) {
        Write-Step "SKIP" "Super Admin secrets not requested."
        return
    }

    $username = Read-Host "Super Admin username"
    $passwordHash = Read-SecretPlainText "Super Admin password hash"
    Set-KeyVaultSecret -Name "loyaltycloud-superadmin-username" -Value $username
    Set-KeyVaultSecret -Name "loyaltycloud-superadmin-password-hash" -Value $passwordHash
    $passwordHash = $null
}

function Configure-AdminApiSecret {
    if (-not $ConfigureAdminApi) {
        Write-Step "SKIP" "Admin API secret not requested."
        return
    }

    $sharedSecret = Read-SecretPlainText "Admin API shared secret"
    Set-KeyVaultSecret -Name "loyaltycloud-admin-api-shared-secret" -Value $sharedSecret
    $sharedSecret = $null
}

function Configure-AppleWalletSecrets {
    if (-not $ConfigureAppleWallet) {
        Write-Step "SKIP" "Apple Wallet secrets not requested."
        return
    }

    Set-KeyVaultSecretFromFileBase64 -Name "kbeauty-pass-certificate" -Path $PassCertificatePath
    $passPassword = Read-SecretPlainText "Apple pass certificate password"
    Set-KeyVaultSecret -Name "kbeauty-pass-certificate-password" -Value $passPassword
    $passPassword = $null

    if (-not [string]::IsNullOrWhiteSpace($WwdrCertificatePath)) {
        Set-KeyVaultSecretFromFileBase64 -Name "kbeauty-wwdr-certificate" -Path $WwdrCertificatePath
    }

    Set-KeyVaultSecretFromFileText -Name "kbeauty-apn-private-key" -Path $ApnPrivateKeyPath
    $apnKeyId = Read-SecretPlainText "Apple APNs Key ID"
    $apnTeamId = Read-SecretPlainText "Apple APNs Team ID"
    Set-KeyVaultSecret -Name "kbeauty-apn-key-id" -Value $apnKeyId
    Set-KeyVaultSecret -Name "kbeauty-apn-team-id" -Value $apnTeamId
    $apnKeyId = $null
    $apnTeamId = $null
}

function Configure-GoogleWalletSecrets {
    if (-not $ConfigureGoogleWallet) {
        Write-Step "SKIP" "Google Wallet secrets not requested."
        return
    }

    Set-KeyVaultSecretFromFileText -Name "loyaltycloud-google-wallet-service-account-json" -Path $GoogleWalletServiceAccountJsonPath
}

try {
    Test-KeyVaultName
    Test-AzCli
    Select-SubscriptionIfRequested
    Invoke-AzCli @("account", "show", "-o", "table") | Out-Host
    Invoke-AzCli @("keyvault", "show", "--name", $KeyVaultName, "-o", "none") | Out-Null
    Confirm-Execution

    if (-not $Execute) {
        if ($ConfigureAdminApi) {
            Write-Step "PLAN" "Would configure: loyaltycloud-admin-api-shared-secret"
        }
        if ($ConfigureSuperAdmin) {
            Write-Step "PLAN" "Would configure: loyaltycloud-superadmin-username, loyaltycloud-superadmin-password-hash"
        }
        if ($ConfigureAppleWallet) {
            Write-Step "PLAN" "Would configure: kbeauty-pass-certificate, kbeauty-pass-certificate-password, optional kbeauty-wwdr-certificate, kbeauty-apn-private-key, kbeauty-apn-key-id, kbeauty-apn-team-id"
        }
        if ($ConfigureGoogleWallet) {
            Write-Step "PLAN" "Would configure: loyaltycloud-google-wallet-service-account-json"
        }
        return
    }

    Configure-AdminApiSecret
    Configure-SuperAdminSecrets
    Configure-AppleWalletSecrets
    Configure-GoogleWalletSecrets

    Write-Step "PLAN" "Secret configuration completed for STAGING Key Vault '$KeyVaultName'."
}
finally {
    [System.GC]::Collect()
}
