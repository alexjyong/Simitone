# Simitone Build Script for Windows
# This script automates the build process for Simitone

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean,
    
    [Parameter(Mandatory=$false)]
    [switch]$CleanAll,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipSubmodules,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipRestore,
    
    [Parameter(Mandatory=$false)]
    [switch]$Run
)

Write-Host "=== Simitone Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host ""

function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

Write-Host "Checking prerequisites..." -ForegroundColor Cyan
if (-not (Test-Command "git")) {
    Write-Host "ERROR: Git is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

if (-not (Test-Command "dotnet")) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green

if ($CleanAll) {
    Write-Host ""
    Write-Host "Deep cleaning all build artifacts (including FreeSO submodule)..." -ForegroundColor Cyan
    
    $binFolders = Get-ChildItem -Path . -Include bin -Recurse -Directory -ErrorAction SilentlyContinue
    $objFolders = Get-ChildItem -Path . -Include obj -Recurse -Directory -ErrorAction SilentlyContinue
    
    $totalFolders = $binFolders.Count + $objFolders.Count
    Write-Host "  Found $totalFolders folders to remove" -ForegroundColor Gray
    
    foreach ($folder in $binFolders) {
        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed $($folder.FullName -replace [regex]::Escape($PWD.Path + '\'), '')" -ForegroundColor DarkGray
    }
    
    foreach ($folder in $objFolders) {
        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed $($folder.FullName -replace [regex]::Escape($PWD.Path + '\'), '')" -ForegroundColor DarkGray
    }
    
    Write-Host "Deep clean complete!" -ForegroundColor Green
}
elseif ($Clean) {
    Write-Host ""
    Write-Host "Cleaning Simitone build artifacts..." -ForegroundColor Cyan
    
    if (Test-Path "Client\Simitone\Simitone.Windows\bin") {
        Remove-Item -Path "Client\Simitone\Simitone.Windows\bin" -Recurse -Force
        Write-Host "  Removed Simitone.Windows\bin" -ForegroundColor Gray
    }
    
    if (Test-Path "Client\Simitone\Simitone.Windows\obj") {
        Remove-Item -Path "Client\Simitone\Simitone.Windows\obj" -Recurse -Force
        Write-Host "  Removed Simitone.Windows\obj" -ForegroundColor Gray
    }
    
    if (Test-Path "Client\Simitone\Simitone.Client\bin") {
        Remove-Item -Path "Client\Simitone\Simitone.Client\bin" -Recurse -Force
        Write-Host "  Removed Simitone.Client\bin" -ForegroundColor Gray
    }
    
    if (Test-Path "Client\Simitone\Simitone.Client\obj") {
        Remove-Item -Path "Client\Simitone\Simitone.Client\obj" -Recurse -Force
        Write-Host "  Removed Simitone.Client\obj" -ForegroundColor Gray
    }
    
    Write-Host "Clean complete!" -ForegroundColor Green
}

if (-not $SkipSubmodules) {
    Write-Host ""
    Write-Host "Initializing submodules..." -ForegroundColor Cyan
    git submodule update --init --recursive
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to initialize submodules" -ForegroundColor Red
        exit 1
    }
    Write-Host "Submodules initialized!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Running Protobuild (optional)..." -ForegroundColor Cyan
if (Test-Path "FreeSO\Other\libs\FSOMonoGame\Protobuild.exe") {
    Push-Location "FreeSO\Other\libs\FSOMonoGame"
    .\Protobuild.exe --generate 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Protobuild completed successfully" -ForegroundColor Green
    } else {
        Write-Host "  Protobuild failed (this is OK, continuing...)" -ForegroundColor Yellow
    }
    Pop-Location
} else {
    Write-Host "  Protobuild not found (this is OK, continuing...)" -ForegroundColor Yellow
}

if (-not $SkipRestore) {
    Write-Host ""
    Write-Host "Restoring dependencies..." -ForegroundColor Cyan
    
    Write-Host "  Restoring Simitone..." -ForegroundColor Gray
    dotnet restore Client\Simitone\Simitone.sln
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to restore Simitone dependencies" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "  Restoring FreeSO..." -ForegroundColor Gray
    dotnet restore FreeSO\TSOClient\FreeSO.sln
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Failed to restore FreeSO dependencies (continuing...)" -ForegroundColor Yellow
    }
    
    Write-Host "  Restoring Roslyn JIT..." -ForegroundColor Gray
    Push-Location "FreeSO\TSOClient\FSO.SimAntics.JIT.Roslyn"
    dotnet restore 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Roslyn restore failed (optional, continuing...)" -ForegroundColor Yellow
    }
    Pop-Location
    
    Write-Host "Dependencies restored!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Building Simitone ($Configuration)..." -ForegroundColor Cyan
dotnet build Client\Simitone\Simitone.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Build Complete! ===" -ForegroundColor Green
$exePath = "Client\Simitone\Simitone.Windows\bin\$Configuration\net9.0-windows\Simitone.exe"
Write-Host "Executable: $exePath" -ForegroundColor Cyan

if ($Run) {
    Write-Host ""
    Write-Host "Launching Simitone..." -ForegroundColor Cyan
    & $exePath
}
