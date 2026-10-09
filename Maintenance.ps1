<#
.SYNOPSIS
    Maintenance script with optional switches.

.DESCRIPTION
    -Cleanup       : Removes coverage files from git tracking, moves Backup_Dashboard_* outside project
    -FixEncoding   : Converts 3 Windows-1256 files to UTF-8, opens for review
    -RotateSecrets : Generates new JWT Secret and admin passwords, stores in user-secrets
    -UpdateHealth  : Updates HealthChecks package to 10.0.12 and runs restore

.EXAMPLE
    .\Maintenance.ps1 -Cleanup -FixEncoding -RotateSecrets -UpdateHealth
    .\Maintenance.ps1 -Cleanup -Force
    .\Maintenance.ps1 -RotateSecrets -Force
#>

param(
    [switch]$Cleanup,
    [switch]$FixEncoding,
    [switch]$RotateSecrets,
    [switch]$UpdateHealth,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$csproj     = Join-Path $projectDir "AccountingSystem.csproj"
$settings   = Join-Path $projectDir "appsettings.json"
$parentDir  = Split-Path -Parent $projectDir

function ConfirmAction($msg) {
    if ($Force) { return $true }
    $ans = Read-Host "$msg (y/N)"
    return $ans -eq 'y' -or $ans -eq 'Y'
}

# =========================================
# -Cleanup
# =========================================
if ($Cleanup) {
    Write-Host "=== CLEANUP ===" -ForegroundColor Cyan

    # 1) Remove coverage files from git tracking (keep locally)
    Write-Host "Removing coverage files from git tracking..." -ForegroundColor Green
    $coverageFiles = Get-ChildItem -Recurse -Path $projectDir -Include "*.coverage", "*.cobertura.xml", "*.opencover.xml", "coverage.json", "coverage.lcov" -ErrorAction SilentlyContinue
    $removed = 0
    if ($coverageFiles.Count -gt 0) {
        foreach ($f in $coverageFiles) {
            $relPath = $f.FullName.Substring($projectDir.Length + 1)
            try {
                git rm --cached "$relPath" 2>$null
                if ($LASTEXITCODE -eq 0) { $removed++ }
            } catch { }
        }
        Write-Host "  Removed $removed files from tracking" -ForegroundColor Green
    } else {
        Write-Host "  No coverage files found" -ForegroundColor Yellow
    }

    # 2) Remove TestResults from tracking
    $testResults = Get-ChildItem -Recurse -Path $projectDir -Directory -Name "TestResults" -ErrorAction SilentlyContinue
    foreach ($d in $testResults) {
        $full = Join-Path $projectDir $d
        $relPath = $full.Substring($projectDir.Length + 1)
        try {
            git rm -r --cached "$relPath" 2>$null
        } catch { }
    }

    # 3) Move Backup_Dashboard_* outside project
    Write-Host "Moving Backup_Dashboard_* to $parentDir ..." -ForegroundColor Green
    $backups = Get-ChildItem -Path $projectDir -Directory -Name "Backup_Dashboard_*" -ErrorAction SilentlyContinue
    foreach ($b in $backups) {
        $src = Join-Path $projectDir $b
        $dst = Join-Path $parentDir $b
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Move-Item $src $dst
        Write-Host "  Moved: $b" -ForegroundColor Green
    }

    # 4) Add to .gitignore if not present
    $gitignore = Join-Path $projectDir ".gitignore"
    $lines = @(
        "TestResults/",
        "*.coverage",
        "*.cobertura.xml",
        "*.opencover.xml",
        "coverage.json",
        "coverage.lcov",
        "Backup_Dashboard_*/"
    )
    $existing = Get-Content $gitignore -Raw
    foreach ($l in $lines) {
        if ($existing -notmatch [regex]::Escape($l)) {
            Add-Content $gitignore $l -Encoding UTF8
        }
    }
    Write-Host "Updated .gitignore" -ForegroundColor Green
}

# =========================================
# -FixEncoding
# =========================================
if ($FixEncoding) {
    Write-Host "=== FIX ENCODING ===" -ForegroundColor Cyan

    # Files known to be Windows-1256
    $suspectFiles = @(
        "Controllers\AccountController.cs",
        "Controllers\AdminController.cs",
        "Views\Shared\_Layout.cshtml"
    )

    foreach ($rel in $suspectFiles) {
        $full = Join-Path $projectDir $rel
        if (-not (Test-Path $full)) {
            Write-Host "Missing: $rel" -ForegroundColor Yellow
            continue
        }

        # Try reading as UTF-8 first
        $content = $null
        $isUtf8 = $false
        try {
            $content = Get-Content $full -Raw -Encoding UTF8
            if ($content -match "[\u0600-\u06FF]") {
                Write-Host "Already UTF-8: $rel" -ForegroundColor Green
                continue
            }
        } catch { }

        # Try Windows-1256
        try {
            $enc = [System.Text.Encoding]::GetEncoding(1256)
            $content = [System.IO.File]::ReadAllText($full, $enc)
        } catch {
            Write-Host "Failed to read: $rel" -ForegroundColor Red
            continue
        }

        # Save as UTF-8 with BOM
        [System.IO.File]::WriteAllText($full, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Converted: $rel -> UTF-8" -ForegroundColor Green

        # Open for review
        if (-not $Force) {
            Write-Host "Open and review Arabic: $full" -ForegroundColor Yellow
            if (ConfirmAction "Press y after review to continue") {
                # continue
            }
        }
    }
}

# =========================================
# -RotateSecrets
# =========================================
if ($RotateSecrets) {
    Write-Host "=== ROTATE SECRETS ===" -ForegroundColor Cyan

    # Generate strong values
    $jwtSecret  = [System.Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Max 256 }))
    $adminPass  = -join ((33..126) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
    $gmPass     = -join ((33..126) | Get-Random -Count 24 | ForEach-Object { [char]$_ })

    Write-Host "NEW JWT SecretKey (Base64, 64 bytes):" -ForegroundColor Cyan
    Write-Host $jwtSecret
    Write-Host ""
    Write-Host "NEW Admin Password:" -ForegroundColor Cyan
    Write-Host $adminPass
    Write-Host ""
    Write-Host "NEW GeneralManager Password:" -ForegroundColor Cyan
    Write-Host $gmPass
    Write-Host ""

    if (ConfirmAction "Save to user-secrets and update appsettings.json?") {
        dotnet user-secrets set "JwtSettings:SecretKey"   $jwtSecret   --project $csproj
        dotnet user-secrets set "InitialAdmin:Password"   $adminPass   --project $csproj
        dotnet user-secrets set "InitialGeneralManager:Password" $gmPass --project $csproj

        $json = Get-Content $settings -Raw | ConvertFrom-Json
        $json.JwtSettings.SecretKey           = "__USE_USER_SECRETS__"
        $json.InitialAdmin.Password           = "__USE_USER_SECRETS__"
        $json.InitialGeneralManager.Password  = "__USE_USER_SECRETS__"
        $json | ConvertTo-Json -Depth 10 | Set-Content $settings -Encoding UTF8

        Write-Host "Updated user-secrets and appsettings.json" -ForegroundColor Green
    }
}

# =========================================
# -UpdateHealth
# =========================================
if ($UpdateHealth) {
    Write-Host "=== UPDATE HEALTH CHECKS ===" -ForegroundColor Cyan

    Write-Host "Updating HealthChecks package to 10.0.12..." -ForegroundColor Green
    dotnet add $csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 10.0.12

    Write-Host "Running dotnet restore..." -ForegroundColor Green
    dotnet restore $csproj

    Write-Host "Verifying package load..." -ForegroundColor Green
    dotnet list $csproj package --include-transitive | Select-String "HealthChecks"
}

# =========================================
# Summary
# =========================================
Write-Host "" -ForegroundColor Cyan
Write-Host "=== DONE ===" -ForegroundColor Cyan
if ($Cleanup)    { Write-Host "CLEANUP" -ForegroundColor Green }
if ($FixEncoding){ Write-Host "FIX ENCODING" -ForegroundColor Green }
if ($RotateSecrets){ Write-Host "ROTATE SECRETS" -ForegroundColor Green }
if ($UpdateHealth){ Write-Host "UPDATE HEALTH" -ForegroundColor Green }

if ($Cleanup -or $RotateSecrets) {
    Write-Host ""
    Write-Host "Reminder: commit changes, then clean git history for secrets:" -ForegroundColor Yellow
    Write-Host "  git filter-repo --path appsettings.json --invert-paths" -ForegroundColor Cyan
    Write-Host "  git push origin --force --all" -ForegroundColor Cyan
}