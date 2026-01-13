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
    [switch]$Publish,
    
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

# Publish mode: create user-friendly directory structure
if ($Publish) {
    Write-Host ""
    Write-Host "=== Publishing Simitone ===" -ForegroundColor Cyan
    
    # Check for Native AOT prerequisites (Visual Studio C++ build tools)
    Write-Host "Checking for Native AOT prerequisites..." -ForegroundColor Gray
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $hasVcTools = $false
    
    if (Test-Path $vsWhere) {
        $vcToolsPath = & $vsWhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
        if ($vcToolsPath) {
            $hasVcTools = $true
            Write-Host "  Found Visual Studio C++ tools" -ForegroundColor Green
        }
    }
    
    if (-not $hasVcTools) {
        Write-Host ""
        Write-Host "ERROR: Native AOT requires Visual Studio C++ build tools." -ForegroundColor Red
        Write-Host ""
        Write-Host "Install with:" -ForegroundColor Yellow
        Write-Host "  winget install Microsoft.VisualStudio.2022.BuildTools --override `"--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended`"" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Or install Visual Studio 2022 with the 'Desktop development with C++' workload." -ForegroundColor Yellow
        exit 1
    }
    
    $publishDir = "publish\win-x64"
    $finalDir = "publish\Simitone-Windows"
    
    # Clean previous publish
    if (Test-Path $finalDir) {
        Remove-Item -Path $finalDir -Recurse -Force
    }
    
    # Step 1: Publish the main application
    Write-Host ""
    Write-Host "Step 1: Publishing main application..." -ForegroundColor Gray
    dotnet publish Client\Simitone\Simitone.Windows\Simitone.Windows.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -o $publishDir `
        /p:TreatWarningsAsErrors=false `
        /p:WarningsAsErrors=""
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to publish main application" -ForegroundColor Red
        exit 1
    }
    
    # Step 2: Build the launcher stub with Native AOT
    Write-Host ""
    Write-Host "Step 2: Building launcher stub (Native AOT)..." -ForegroundColor Gray
    dotnet publish Client\Simitone\Simitone.Launcher\Simitone.Launcher.csproj `
        -c $Configuration `
        -r win-x64 `
        -o "publish\launcher-win"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build launcher stub" -ForegroundColor Red
        exit 1
    }
    
    # Step 3: Create final directory structure
    Write-Host ""
    Write-Host "Step 3: Creating final directory structure..." -ForegroundColor Gray
    
    New-Item -ItemType Directory -Path $finalDir -Force | Out-Null
    New-Item -ItemType Directory -Path "$finalDir\lib" -Force | Out-Null
    
    # Copy launcher to root
    Copy-Item -Path "publish\launcher-win\Simitone.exe" -Destination "$finalDir\Simitone.exe"
    
    # Move all published files to lib/
    Get-ChildItem -Path $publishDir | ForEach-Object {
        Move-Item -Path $_.FullName -Destination "$finalDir\lib\" -Force
    }
    
    # Clean up temporary directories
    Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "publish\launcher-win" -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "=== Publish Complete! ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output directory: $((Resolve-Path $finalDir).Path)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Directory structure:" -ForegroundColor Gray
    Write-Host "  Simitone-Windows/" -ForegroundColor White
    Write-Host "    Simitone.exe      <- Run this!" -ForegroundColor Green
    Write-Host "    lib/              <- Game files (don't modify)" -ForegroundColor DarkGray
    exit 0
}

Write-Host ""
Write-Host "=== Build Complete! ===" -ForegroundColor Green
$exePath = "Client\Simitone\Simitone.Windows\bin\$Configuration\net9.0-windows\Simitone.exe"
Write-Host "Executable: $exePath" -ForegroundColor Cyan
Write-Host ""
Write-Host "TIP: Use -Publish to create a distributable package with clean directory structure." -ForegroundColor Yellow

if ($Run) {
    Write-Host ""
    Write-Host "Launching Simitone..." -ForegroundColor Cyan
    & $exePath
}
