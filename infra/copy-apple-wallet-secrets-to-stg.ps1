<#
.SYNOPSIS
Copies only Apple Wallet secrets from production Key Vault to staging Key Vault.

.DESCRIPTION
Dry-run by default. Use -Execute to copy the allowlisted Apple Wallet secrets.
The script never prints secret values and refuses to copy non-Apple Wallet
secrets such as SQL, Storage, SuperAdmin or Admin API credentials.
#>
[CmdletBinding()]
param(
    [string]$SourceVault = "kv-loyaltycloud-894839",
    [string]$TargetVault = "kv-loyaltycloud-stg-01",
    [switch]$Execute,
    [switch]$AllowSourceVaultOverride
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedSourceVault = "kv-loyaltycloud-894839"
$RequiredConfirmation = "COPY APPLE WALLET SECRETS TO STG"

$RequiredAppleWalletSecretNames = @(
    "kbeauty-pass-certificate",
    "kbeauty-pass-certificate-password",
    "kbeauty-apn-private-key",
    "kbeauty-apn-key-id",
    "kbeauty-apn-team-id"
)

$OptionalAppleWalletSecretNames = @(
    "kbeauty-wwdr-certificate"
)

$AppleWalletSecretNames = @($RequiredAppleWalletSecretNames + $OptionalAppleWalletSecretNames)

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
        [switch]$Sensitive
    )

    $display = "az $($Arguments -join ' ')"
    if ($Sensitive) {
        $display = "az <sensitive command hidden>"
    }

    Write-Step "CHECK" $display
    $result = Invoke-AzProcess -Arguments $Arguments
    $output = if ($null -eq $result.StdOut) { '' } else { $result.StdOut.Trim() }
    $errorText = if ($null -eq $result.StdErr) { '' } else { $result.StdErr.Trim() }

    if ($result.ExitCode -ne 0) {
        $details = if ([string]::IsNullOrWhiteSpace($errorText)) { $output } else { $errorText }
        throw "Azure CLI command failed: $display`n$details"
    }

    return $output
}

function Invoke-AzCliOrNull {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$Sensitive
    )

    $display = "az $($Arguments -join ' ')"
    if ($Sensitive) {
        $display = "az <sensitive command hidden>"
    }

    Write-Step "CHECK" $display
    $result = Invoke-AzProcess -Arguments $Arguments
    $stdout = if ($null -eq $result.StdOut) { '' } else { $result.StdOut.Trim() }
    $stderr = if ($null -eq $result.StdErr) { '' } else { $result.StdErr.Trim() }
    $text = (@($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n"

    if ($result.ExitCode -eq 0) {
        return $stdout
    }

    if (Test-AzFatalError $text) {
        throw "Azure CLI command failed with a fatal error: $display`n$text"
    }

    if (Test-AzExpectedNotFound $text) {
        return $null
    }

    throw "Azure CLI command failed: $display`n$text"
}

function Test-AzExpectedNotFound {
    param([string]$Message)

    return $Message -match '(?i)ResourceGroupNotFound' `
        -or $Message -match '(?i)ResourceNotFound' `
        -or $Message -match '(?i)ResourceNotFoundError' `
        -or $Message -match '(?i)SecretNotFound' `
        -or $Message -match '(?i)VaultNotFound' `
        -or $Message -match '(?i)was not found' `
        -or $Message -match '(?i)could not be found' `
        -or $Message -match '(?i)does not exist' `
        -or ($Message -match '(?i)The Vault' -and $Message -match '(?i)not found within subscription')
}

function Test-AzFatalError {
    param([string]$Message)

    return $Message -match '(?i)AuthenticationFailed' `
        -or $Message -match '(?i)AuthorizationFailed' `
        -or $Message -match '(?i)Forbidden' `
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
    Invoke-AzCli @("account", "show", "-o", "none") | Out-Null
    Invoke-AzCli @("keyvault", "secret", "show", "--help") | Out-Null
    Invoke-AzCli @("keyvault", "secret", "set", "--help") | Out-Null
}

function Test-SafetyGuards {
    if ($SourceVault -eq $TargetVault) {
        throw "SourceVault and TargetVault cannot be the same."
    }

    if ($TargetVault -match '(?i)prod') {
        throw "Production guard triggered: TargetVault contains 'prod': $TargetVault"
    }

    if ($TargetVault -notmatch '(?i)stg') {
        throw "Staging guard triggered: TargetVault must contain 'stg': $TargetVault"
    }

    if ($SourceVault -ne $ExpectedSourceVault -and -not $AllowSourceVaultOverride) {
        throw "SourceVault must be '$ExpectedSourceVault'. Use -AllowSourceVaultOverride only for an intentional non-production source."
    }
}

function Confirm-Execution {
    Write-Step "PLAN" "Source Key Vault: $SourceVault"
    Write-Step "PLAN" "Target Key Vault: $TargetVault"
    Write-Step "PLAN" "Required Apple Wallet secrets: $($RequiredAppleWalletSecretNames -join ', ')"
    Write-Step "PLAN" "Optional Apple Wallet secrets: $($OptionalAppleWalletSecretNames -join ', ')"

    if (-not $Execute) {
        Write-Step "PLAN" "Dry-run only. Re-run with -Execute to copy Apple Wallet secrets."
        return
    }

    Write-Step "WARNING" "This will copy only allowlisted Apple Wallet secrets into STAGING."
    $answer = Read-Host "Type $RequiredConfirmation to continue"
    if ($answer -ne $RequiredConfirmation) {
        throw "Confirmation did not match. Aborting."
    }
}

function Test-KeyVaultExists {
    param([string]$VaultName)

    $result = Invoke-AzCliOrNull @("keyvault", "show", "--name", $VaultName, "-o", "none")
    if ($null -eq $result) {
        throw "Key Vault not found or inaccessible: $VaultName"
    }
}

function Test-SecretExists {
    param(
        [string]$VaultName,
        [string]$SecretName
    )

    $result = Invoke-AzCliOrNull @("keyvault", "secret", "show", "--vault-name", $VaultName, "--name", $SecretName, "--query", "id", "-o", "tsv")
    return $null -ne $result -and -not [string]::IsNullOrWhiteSpace($result)
}

function Get-SecretValue {
    param(
        [string]$VaultName,
        [string]$SecretName
    )

    return Invoke-AzCli @("keyvault", "secret", "show", "--vault-name", $VaultName, "--name", $SecretName, "--query", "value", "-o", "tsv") -Sensitive
}

function Set-SecretValue {
    param(
        [string]$VaultName,
        [string]$SecretName,
        [string]$SecretValue
    )

    Invoke-AzCli @("keyvault", "secret", "set", "--vault-name", $VaultName, "--name", $SecretName, "--value", $SecretValue, "-o", "none") -Sensitive | Out-Null
}

function Get-SecretInventory {
    param([string]$VaultName)

    $inventory = @{}
    foreach ($secretName in $AppleWalletSecretNames) {
        $inventory[$secretName] = Test-SecretExists -VaultName $VaultName -SecretName $secretName
    }
    return $inventory
}

function Show-Inventory {
    param(
        [string]$Title,
        [hashtable]$Inventory
    )

    Write-Step "PLAN" $Title
    foreach ($secretName in $AppleWalletSecretNames) {
        $status = if ($Inventory[$secretName]) { "exists" } else { "missing" }
        Write-Host "  $secretName : $status"
    }
}

function Assert-SourceSecretsComplete {
    param([hashtable]$SourceInventory)

    $missing = @($RequiredAppleWalletSecretNames | Where-Object { -not $SourceInventory[$_] })
    if ($missing.Count -gt 0) {
        throw "Required Apple Wallet secrets are missing in source vault '$SourceVault': $($missing -join ', ')"
    }
}

function Copy-AppleWalletSecrets {
    param(
        [hashtable]$SourceInventory,
        [hashtable]$TargetInventory
    )

    foreach ($secretName in $AppleWalletSecretNames) {
        if (-not $SourceInventory[$secretName]) {
            Write-Step "SKIP" "Optional allowlisted secret is not present in source vault: $secretName"
            continue
        }

        $operation = if ($TargetInventory[$secretName]) { "UPDATE" } else { "COPY" }
        Write-Step $operation "Copying allowlisted secret: $secretName"

        $secretValue = $null
        try {
            $secretValue = Get-SecretValue -VaultName $SourceVault -SecretName $secretName
            Set-SecretValue -VaultName $TargetVault -SecretName $secretName -SecretValue $secretValue
        }
        finally {
            $secretValue = $null
            [System.GC]::Collect()
        }
    }
}

try {
    Show-PowerShellRuntime
    Test-SafetyGuards
    Test-AzCli
    Test-KeyVaultExists -VaultName $SourceVault
    Test-KeyVaultExists -VaultName $TargetVault
    Confirm-Execution

    $sourceInventory = Get-SecretInventory -VaultName $SourceVault
    $targetInventory = Get-SecretInventory -VaultName $TargetVault

    Show-Inventory -Title "Source Apple Wallet secrets in '$SourceVault'" -Inventory $sourceInventory
    Show-Inventory -Title "Target Apple Wallet secrets in '$TargetVault'" -Inventory $targetInventory
    Assert-SourceSecretsComplete -SourceInventory $sourceInventory

    foreach ($secretName in $AppleWalletSecretNames) {
        if (-not $sourceInventory[$secretName]) {
            Write-Step "PLAN" "Would skip optional missing source secret: $secretName"
        }
        elseif ($targetInventory[$secretName]) {
            Write-Step "PLAN" "Would update STG secret: $secretName"
        }
        else {
            Write-Step "PLAN" "Would copy STG secret: $secretName"
        }
    }

    if (-not $Execute) {
        Write-Step "PLAN" "Dry-run completed successfully."
        Write-Step "PLAN" "No secret values were read."
        Write-Step "PLAN" "No Azure resources or secrets were modified."
        return
    }

    Copy-AppleWalletSecrets -SourceInventory $sourceInventory -TargetInventory $targetInventory

    $updatedTargetInventory = Get-SecretInventory -VaultName $TargetVault
    Show-Inventory -Title "Target Apple Wallet secrets after copy in '$TargetVault'" -Inventory $updatedTargetInventory
    Write-Step "DONE" "Apple Wallet secrets copied to STAGING Key Vault '$TargetVault'."
}
finally {
    [System.GC]::Collect()
}
