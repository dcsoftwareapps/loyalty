<#
.SYNOPSIS
Deploys a LoyaltyCloud branch temporarily to Azure STG.

.DESCRIPTION
Safe by default. Without -Execute, or with -DryRun, this script performs
repository, branch, migration and Azure-login checks, then prints the publish
and deploy actions it would run. It never applies EF migrations, never touches
Azure configuration/secrets, and never targets PROD.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Branch,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Admin", "Api", "Both")]
    [string]$Target,

    [switch]$DryRun,

    [switch]$Execute
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$SolutionPath = Join-Path $RepoRoot "LoyaltyCloud.sln"
$InfrastructureMigrationsPath = $null
$StagingBranchRef = "origin/staging"

$ResourceGroup = "rg-loyaltycloud-stg"
$ApiAppName = "loyaltycloud-api-stg-01"
$AdminAppName = "loyaltycloud-admin-linux-stg-01"
$ApiUrl = "https://loyaltycloud-api-stg-01.azurewebsites.net"
$AdminUrl = "https://loyaltycloud-admin-linux-stg-01.azurewebsites.net"

$ApiProject = ".\src\LoyaltyCloud.API\LoyaltyCloud.API.csproj"
$AdminProject = ".\src\LoyaltyCloud.Admin\LoyaltyCloud.Admin.csproj"
$ApiArtifactDir = ".\artifacts\api"
$ApiZip = ".\artifacts\api.zip"
$AdminArtifactDir = ".\artifacts\admin"
$AdminZip = ".\artifacts\admin.zip"

$ProdResourceNamePatterns = @(
    "rg-loyaltycloud-prod",
    "loyaltycloud-api-894839",
    "loyaltycloud-admin-prod-01",
    "loyaltycloud-admin.azurewebsites.net",
    "sql-loyaltycloud-894839",
    "LoyaltyCloudFree",
    "kv-loyaltycloud-894839",
    "stloyaltycloud894839",
    "api.loyaltycloud.net",
    "admin.loyaltycloud.net"
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

function Invoke-ExternalProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $command = Get-Command $FileName -ErrorAction Stop
    $quotedCommand = ConvertTo-CommandLineArgument $command.Source
    $quotedArguments = @($Arguments | ForEach-Object { ConvertTo-CommandLineArgument $_ })
    $commandLine = (@($quotedCommand) + $quotedArguments) -join ' '

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $env:ComSpec
    $startInfo.Arguments = "/d /s /c ""$commandLine"""
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WorkingDirectory = $RepoRoot

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $result = [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = if ($null -eq $stdout) { "" } else { $stdout.Trim() }
        StdErr = if ($null -eq $stderr) { "" } else { $stderr.Trim() }
        Command = "$FileName $($Arguments -join ' ')"
    }

    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        $details = if ([string]::IsNullOrWhiteSpace($result.StdErr)) { $result.StdOut } else { $result.StdErr }
        throw "Command failed: $($result.Command)`n$details"
    }

    return $result
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    return Invoke-ExternalProcess -FileName "git" -Arguments $Arguments -AllowFailure:$AllowFailure
}

function Invoke-Az {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    return Invoke-ExternalProcess -FileName "az" -Arguments $Arguments -AllowFailure:$AllowFailure
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return Invoke-ExternalProcess -FileName "dotnet" -Arguments $Arguments
}

function Invoke-Tar {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return Invoke-ExternalProcess -FileName "tar" -Arguments $Arguments
}

function Assert-RepoRoot {
    if (-not (Test-Path -LiteralPath $SolutionPath)) {
        throw "This script must run from the LoyaltyCloud repository. Missing: $SolutionPath"
    }

    $gitRoot = (Invoke-Git @("rev-parse", "--show-toplevel")).StdOut
    if ((Resolve-Path $gitRoot).Path -ne $RepoRoot) {
        throw "Unexpected git root. Expected '$RepoRoot', got '$gitRoot'."
    }

    Write-Step "CHECK" "Repository: $RepoRoot"
}

function Test-CleanWorkingTree {
    $status = (Invoke-Git @("status", "--short")).StdOut
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        if ($Execute) {
            throw "Working tree is not clean. Commit, discard, or move local changes before deploying.`n$status"
        }

        Write-Step "CHECK" "Working tree is not clean. A real deploy with -Execute would stop before checkout/deploy."
        Write-Host $status
        return $false
    }

    Write-Step "CHECK" "Working tree is clean."
    return $true
}

function Assert-NoProductionTargets {
    $targets = @($ResourceGroup, $ApiAppName, $AdminAppName, $ApiUrl, $AdminUrl)
    foreach ($targetValue in $targets) {
        foreach ($prodPattern in $ProdResourceNamePatterns) {
            if ($targetValue -like "*$prodPattern*") {
                throw "Production guard triggered. Refusing to use target '$targetValue'."
            }
        }
    }

    Write-Step "CHECK" "Production guard passed. STG targets only."
}

function Resolve-EfMigrationsPath {
    $infrastructureRoot = Join-Path $RepoRoot "src\LoyaltyCloud.Infrastructure"
    $snapshots = @(Get-ChildItem -Path $infrastructureRoot -Recurse -Filter "AppDbContextModelSnapshot.cs" -File)

    if ($snapshots.Count -eq 0) {
        throw "Could not find AppDbContextModelSnapshot.cs under $infrastructureRoot."
    }

    if ($snapshots.Count -gt 1) {
        $paths = ($snapshots | ForEach-Object { $_.FullName }) -join "`n"
        throw "Found multiple EF Core model snapshots. Cannot determine migrations path safely.`n$paths"
    }

    $migrationsDirectory = $snapshots[0].Directory.FullName
    $relative = $migrationsDirectory.Substring($RepoRoot.Length).TrimStart('\', '/')
    $relative = $relative -replace '\\', '/'

    $migrationFiles = @(Get-ChildItem -Path $migrationsDirectory -Filter "*.cs" -File |
        Where-Object { $_.Name -ne "AppDbContextModelSnapshot.cs" })

    if ($migrationFiles.Count -eq 0) {
        throw "Migrations path '$relative' contains no EF Core migration files."
    }

    Write-Step "CHECK" "EF migrations path: $relative"
    return $relative
}

function Test-GitRef {
    param([string]$Ref)

    $result = Invoke-Git @("rev-parse", "--verify", "$Ref^{commit}") -AllowFailure
    return $result.ExitCode -eq 0
}

function Resolve-DeployRef {
    if (Test-GitRef "origin/$Branch") {
        return "origin/$Branch"
    }

    if (Test-GitRef $Branch) {
        return $Branch
    }

    throw "Branch '$Branch' was not found locally or on origin."
}

function Get-CommitInfo {
    param([string]$Ref)

    return [pscustomobject]@{
        Hash = (Invoke-Git @("rev-parse", "$Ref^{commit}")).StdOut
        ShortHash = (Invoke-Git @("rev-parse", "--short", "$Ref^{commit}")).StdOut
        Subject = (Invoke-Git @("log", "-1", "--format=%s", "$Ref^{commit}")).StdOut
    }
}

function Update-BranchForExecute {
    param([string]$ResolvedRef)

    if ($ResolvedRef -eq "origin/$Branch") {
        if (Test-GitRef $Branch) {
            Write-Step "CHECK" "Checking out local branch '$Branch'."
            Invoke-Git @("checkout", $Branch) | Out-Null
            Write-Step "CHECK" "Fast-forwarding '$Branch' from origin."
            Invoke-Git @("merge", "--ff-only", "origin/$Branch") | Out-Null
        }
        else {
            Write-Step "CHECK" "Creating local branch '$Branch' from origin."
            Invoke-Git @("checkout", "-b", $Branch, "origin/$Branch") | Out-Null
        }
    }
    else {
        Write-Step "CHECK" "Checking out local branch '$Branch'."
        Invoke-Git @("checkout", $Branch) | Out-Null
    }
}

function Get-NewMigrationFiles {
    param([string]$Ref)

    if (-not (Test-GitRef $StagingBranchRef)) {
        throw "Cannot compare migrations because '$StagingBranchRef' is missing. Fetch origin and confirm staging exists."
    }

    $diff = (Invoke-Git @(
        "diff",
        "--name-status",
        "--diff-filter=AMR",
        "$StagingBranchRef...$Ref",
        "--",
        $InfrastructureMigrationsPath
    )).StdOut

    if ([string]::IsNullOrWhiteSpace($diff)) {
        return @()
    }

    $changed = @()
    foreach ($line in @($diff -split "(`r`n|`n)" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $parts = @($line -split "`t")
        $status = $parts[0]
        $path = $parts[$parts.Count - 1]
        $name = [System.IO.Path]::GetFileName($path)

        $isSnapshot = $name -eq "AppDbContextModelSnapshot.cs"
        $isDesigner = $name -like "*.Designer.cs"
        $isMigrationCode = $name -match '^\d{14}_.+\.cs$' -and -not $isDesigner

        if ($isMigrationCode) {
            $changed += "$status`t$path"
            continue
        }

        if ($isDesigner) {
            $migrationPath = $path -replace '\.Designer\.cs$', '.cs'
            if ($changed -notcontains "$status`t$migrationPath") {
                $changed += "$status`t$path"
            }
            continue
        }

        if ($isSnapshot) {
            $changed += "$status`t$path (model snapshot changed; review for migration divergence)"
        }
    }

    return $changed
}

function Assert-NoNewMigrations {
    param([string]$Ref)

    $migrations = @(Get-NewMigrationFiles -Ref $Ref)
    if ($migrations.Count -gt 0) {
        Write-Step "BLOCK" "Deploy detenido: se detectaron migraciones nuevas. Verificar y aplicar manualmente en STG antes de continuar."
        foreach ($migration in $migrations) {
            Write-Step "BLOCK" $migration
        }
        throw "New migration files detected relative to $StagingBranchRef."
    }

    Write-Step "CHECK" "No new migration files detected relative to $StagingBranchRef."
}

function Test-AzureCliSession {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        if ($Execute) {
            throw "Azure CLI is not installed or not in PATH. Install Azure CLI and run 'az login' manually."
        }

        Write-Step "WARN" "Azure CLI is not installed or not in PATH. A real deploy with -Execute would stop here."
        return
    }

    $account = Invoke-Az @("account", "show", "-o", "json") -AllowFailure
    if ($account.ExitCode -ne 0) {
        if ($Execute) {
            throw "Azure CLI is not logged in or cannot access a subscription. Run 'az login' manually, then retry."
        }

        Write-Step "WARN" "Azure CLI is not logged in or cannot access a subscription. A real deploy with -Execute would stop here."
        return
    }

    Write-Step "CHECK" "Azure CLI session is available."
}

function Write-CommandPlan {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    Write-Step "PLAN" "$Command $($Arguments -join ' ')"
}

function Invoke-OrPlan {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    if (-not $Execute) {
        Write-CommandPlan -Command $Command -Arguments $Arguments
        return $null
    }

    switch ($Command) {
        "dotnet" { return Invoke-DotNet -Arguments $Arguments }
        "tar" { return Invoke-Tar -Arguments $Arguments }
        "az" { return Invoke-Az -Arguments $Arguments }
        default { return Invoke-ExternalProcess -FileName $Command -Arguments $Arguments }
    }
}

function Remove-ArtifactIfNeeded {
    param([string]$Path)

    $fullPath = Join-Path $RepoRoot ($Path.TrimStart(".\"))
    $artifactsRoot = Join-Path $RepoRoot "artifacts"

    if (-not ($fullPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to clean outside artifacts: $fullPath"
    }

    if (-not $Execute) {
        Write-Step "PLAN" "Would remove artifact path: $Path"
        return
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force -ErrorAction SilentlyContinue
}

function Build-Solution {
    Invoke-OrPlan -Command "dotnet" -Arguments @("build", ".\LoyaltyCloud.sln", "-c", "Release") | Out-Null
}

function Publish-AndDeploy {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Api", "Admin")]
        [string]$DeployTarget
    )

    if ($DeployTarget -eq "Api") {
        $project = $ApiProject
        $artifactDir = $ApiArtifactDir
        $zipPath = $ApiZip
        $appName = $ApiAppName
        $url = $ApiUrl
    }
    else {
        $project = $AdminProject
        $artifactDir = $AdminArtifactDir
        $zipPath = $AdminZip
        $appName = $AdminAppName
        $url = $AdminUrl
    }

    Write-Step "TARGET" "$DeployTarget -> $appName ($url)"

    Remove-ArtifactIfNeeded -Path $artifactDir
    Remove-ArtifactIfNeeded -Path $zipPath

    Invoke-OrPlan -Command "dotnet" -Arguments @("publish", $project, "-c", "Release", "-o", $artifactDir) | Out-Null
    Invoke-OrPlan -Command "tar" -Arguments @("-a", "-c", "-f", $zipPath, "-C", $artifactDir, ".") | Out-Null
    Invoke-OrPlan -Command "az" -Arguments @("webapp", "deploy", "--resource-group", $ResourceGroup, "--name", $appName, "--src-path", $zipPath, "--type", "zip") | Out-Null

    if ($Execute) {
        $state = (Invoke-Az @("webapp", "show", "--resource-group", $ResourceGroup, "--name", $appName, "--query", "state", "-o", "tsv")).StdOut
        Write-Step "CHECK" "$appName state: $state"
    }
    else {
        Write-Step "PLAN" "Would verify App Service is Running: $appName"
    }
}

if ($DryRun -and $Execute) {
    throw "Use either -DryRun or -Execute, not both."
}

if (-not $Execute) {
    $DryRun = $true
}

Write-Step "MODE" ($(if ($Execute) { "Execute" } else { "DryRun" }))
Assert-RepoRoot
Assert-NoProductionTargets
$InfrastructureMigrationsPath = Resolve-EfMigrationsPath
$workingTreeIsClean = Test-CleanWorkingTree
Test-AzureCliSession

Write-Step "CHECK" "Fetching origin."
Invoke-Git @("fetch", "origin") | Out-Null

$resolvedRef = Resolve-DeployRef
if ($Execute) {
    Update-BranchForExecute -ResolvedRef $resolvedRef
    $resolvedRef = "HEAD"
}
else {
    if ($workingTreeIsClean) {
        Write-Step "PLAN" "Would checkout/update branch '$Branch' safely before deploying."
    }
    else {
        Write-Step "PLAN" "Would not checkout/update any branch during this dry-run because local changes are present."
    }
}

$commit = Get-CommitInfo -Ref $resolvedRef
Write-Step "INFO" "Branch: $Branch"
Write-Step "INFO" "Commit: $($commit.Hash)"
Write-Step "INFO" "Short commit: $($commit.ShortHash)"
Write-Step "INFO" "Last commit: $($commit.Subject)"

Assert-NoNewMigrations -Ref $resolvedRef

Write-Step "PLAN" "Target: $Target"
Write-Step "PLAN" "Resource Group: $ResourceGroup"
if ($Target -eq "Api" -or $Target -eq "Both") {
    Write-Step "PLAN" "API STG: $ApiAppName ($ApiUrl)"
    Write-Step "PLAN" "API artifacts: $ApiArtifactDir, $ApiZip"
}
if ($Target -eq "Admin" -or $Target -eq "Both") {
    Write-Step "PLAN" "Admin STG: $AdminAppName ($AdminUrl)"
    Write-Step "PLAN" "Admin artifacts: $AdminArtifactDir, $AdminZip"
}

Build-Solution

if ($Target -eq "Api" -or $Target -eq "Both") {
    Publish-AndDeploy -DeployTarget "Api"
}

if ($Target -eq "Admin" -or $Target -eq "Both") {
    Publish-AndDeploy -DeployTarget "Admin"
}

Write-Step "DONE" "STG deploy workflow completed."
Write-Step "DONE" "Target: $Target"
Write-Step "DONE" "Branch: $Branch"
Write-Step "DONE" "Commit: $($commit.ShortHash)"
if ($Target -eq "Api" -or $Target -eq "Both") {
    Write-Step "DONE" "API URL: $ApiUrl"
}
if ($Target -eq "Admin" -or $Target -eq "Both") {
    Write-Step "DONE" "Admin URL: $AdminUrl"
}
if (-not $Execute) {
    Write-Step "DONE" "Dry-run only. No Azure resources were created or modified, no deploy was executed, and no database changes were made."
}
