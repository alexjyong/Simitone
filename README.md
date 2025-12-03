![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

# Note: This is a forked version of Simitone with some updates and minor bug fixes that are not in the current version at the time of writing.
# This is not affliated with the Simitone Team!!

[Latest Pre-release](https://github.com/alexjyong/Simitone/releases/latest/) | [Download](https://github.com/alexjyong/Simitone/releases/download/v0.8.12-forked/SimitoneWindows-Release.zip)

Alternative C# Windows Frontend for The Sims 1, based off of FreeSO. http://freeso.org 
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection)

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Currently does not support the entire Fame career track, saving on vacation and a few other important things. Buy mode on community lots is also not functioning. While all objects run, many of them have bugs that can make certain lot types unplayable. For current development progress, see [this issue.](https://github.com/riperiperi/Simitone/issues/8)

*Only for Desktop Windows.* Other platforms cannot be officially supported.

(For Mac, and Linux, [you might be able to get this to work with Wine](https://appdb.winehq.org/objectManager.php?sClass=version&iId=8696), but you're on your own for support. (although if someone has a good guide, feel free to share)

# Purpose

On modern operating systems, The Sims has a few nagging issues that make it less than playable. Simitone can be seen as a tool to improve the playability of your existing installation of The Sims. Here are some features:

- Playable in 3D - using a combination of models generated from the 2D sprites and created by the community.
  - [Download the mesh pack here!](http://forum.freeso.org/forums/3d-remeshing.40/)
- Custom user interface that works at modern resolutions. Working on a more desktop oriented interface.
- Improved graphical performance, support for high resolutions and refresh rates.
- Custom lighting - directional lights with smooth falloffs and shadows using generated 3D meshes.
- *Volcanic*, a program which allows you to examine, modify and create new game objects. (from FreeSO)

# How to Install

## Prerequisites
- **The Sims: Complete Collection** or **The Sims: Legacy Collection**
  - You will also have to play through the lots first on vanilla TS1 in order for certain things like taxi menu to load properly on the phones. Not too sure why this is happening.
- Windows (I've only tested this with 10/11)
- [.NET 9.0 Runtime and ASP.Net Core runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0). (Note if this isn't installed, Windows will prompt you to download them with a link.)

## Quick Install

1. **Download** the [latest release](https://github.com/alexjyong/Simitone/releases/latest/)
2. **Extract** the ZIP to your preferred location
3. **Run** `Simitone.exe`

Simitone will automatically detect your Sims 1 installation by checking:
- Relative path (`../The Sims/`)
- Windows Registry (`HKEY_LOCAL_MACHINE\SOFTWARE\Maxis\The Sims`)
- Steam libraries (reads Steam registry, parses libraryfolders.vdf and app manifests)
- Default install location (`C:\Program Files (x86)\Maxis\The Sims\`)

If none of these are options for you or working, see "Manual Path Configuration" below.

## Manual Path Configuration

If auto-detection fails or you have a custom install location:

**Command-line override example:**
```bash
Simitone.exe -path"C:\Your\Custom\Path\The Sims\"
```

Portable installation:
Place The Sims files in ../The Sims/ relative to Simitone.exe (e.g., if Simitone is in D:\Games\Simitone\, put The Sims in D:\Games\The Sims\)

User data location:
Configuration and save files are stored in:
%USERPROFILE%\Documents\Simitone\

Optional Command-Line Arguments

```pwsh
-dx / -gl # Force DirectX or OpenGL rendering
-3d # Enable 3D mode in the game (can be toggled on or off with F12)
-jit # Enable JIT compilation for SimAntics (can elpthe game run faster at the expense of more start up time)
-ide # Launch Volcanic object editor
-lang <code> # Set language
-hz <rate> # Set refresh rate
-nosound # Disable audio
```

# Building from Source

Want to compile Simitone yourself? Follow these steps.

## Easy Mode: Use GitHub Actions

Don't want to mess with command prompt and stuff? Just fork this repo and run the build workflow:

1. Fork this repository on GitHub
2. Go to the **Actions** tab in your fork (you may be prompted to activate actions)
3. Click **"Build Simitone"** in the left sidebar
4. Click **"Run workflow"** -> Choose **Release** -> Click **"Run workflow"**
5. Wait ~4-5 minutes or so, refresh the job page, and you should see the release artifact.


## Local Build (Windows Only)

### Quick Build (Automated Script)

For a streamlined build experience, use the included PowerShell script:

**Note**: If the script opens in an editor instead of running, Windows is blocking PowerShell scripts. Use one of these methods:

```pwsh
# Option 1: Bypass policy **just** for this one script
powershell -ExecutionPolicy Bypass -File .\build.ps1

# Option 2: Unblock the script file once (then use .\build.ps1 normally)
Unblock-File .\build.ps1
```

```pwsh
# Basic build (this will use release configuration!!)
.\build.ps1
```

List of options you can use with the script
```pwsh
.\build.ps1 -Configuration Debug     # Build in Debug mode
.\build.ps1 -Clean                   # Clean Simitone artifacts before building (obvs, you won't need this on first run)
.\build.ps1 -CleanAll                # Deep clean (includes FreeSO submodule and its submodules!)
.\build.ps1 -Run                     # Build and run immediately
.\build.ps1 -Clean -Run              # Clean, build, and run

# Advanced options
.\build.ps1 -SkipSubmodules          # Skip submodule initialization
.\build.ps1 -SkipRestore             # Skip dependency restoration
```

The script will:
1. Check if prerequisites are installed (Git, .NET SDK)
2. Initialize submodules (if needed)
3. Restore dependencies
4. Build the project
5. Show you where the executable is located

### Manual Build (Step-by-Step)

If you'd rather do it by hand to understand each step, or for whatever reason, read the instructions below:

#### Prerequisites

1. **Git** (version 2.13 or later)
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

2. **.NET 9.0 SDK**
   - Download: https://dotnet.microsoft.com/en-us/download/dotnet/9.0
   - Get the "SDK x64" installer (not just the runtime)
   - Verify: `dotnet --info` (should show 9.0.x)

3. **Windows OS** (Windows 10/11 recommended)

4. **The Sims: Complete Collection** or **The Sims: Legacy Collection** (to run the compiled game)

### Build Steps

##### 1. Clone and Initialize Submodules

```pwsh
git clone https://github.com/alexjyong/Simitone.git
cd Simitone
git submodule update --init --recursive
```

The submodule step downloads FreeSO (the core simulation engine). This may take a few minutes.

##### 2. Run Protobuild (Optional)

```pwsh
cd FreeSO\Other\libs\FSOMonoGame\
.\Protobuild.exe --generate
cd ..\..\..\..
```

**Note**: This step may fail on some systems, but that's okay, the build can still succeed without it.

##### 3. Restore Dependencies

```pwsh
# Restore Simitone dependencies (required)
dotnet restore Client\Simitone\Simitone.sln

# Restore FreeSO dependencies (recommended)
dotnet restore FreeSO\TSOClient\FreeSO.sln

# Restore Roslyn/JIT dependencies (optional, for -jit support)
cd FreeSO\TSOClient\FSO.SimAntics.JIT.Roslyn\
dotnet restore
cd ..\..\..
```

##### 4. Build

```pwsh
# Release build (optimized, recommended)
dotnet build Client\Simitone\Simitone.sln -c Release --no-restore

# Or Debug build (includes debugging symbols)
dotnet build Client\Simitone\Simitone.sln -c Debug --no-restore
```

##### 5. Run

Find your compiled executable at:
```pwsh
Client\Simitone\Simitone.Windows\bin\Release\net9.0-windows\Simitone.exe
```

Or run directly:
```pwsh
dotnet run --project Client\Simitone\Simitone.Windows\Simitone.Windows.csproj -c Release
```

Note on first run, it may take a few moments to fire up.

### Cleaning Build Artifacts

To start fresh and remove all build artifacts:

```pwsh
# Clean using the build script
.\build.ps1 -Clean

# Or manually delete build folders
Remove-Item -Recurse -Force Client\Simitone\Simitone.Windows\bin, Client\Simitone\Simitone.Windows\obj
Remove-Item -Recurse -Force Client\Simitone\Simitone.Client\bin, Client\Simitone\Simitone.Client\obj

# For a complete clean (including FreeSO projects)
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

This is useful when:
- Switching between Debug and Release builds
- Troubleshooting build issues
- Build artifacts are corrupted
- You want to verify a clean build works

## Troubleshooting

**"FreeSO folder is empty"**

```pwsh
rm -rf FreeSO
git submodule update --init --recursive --force
```
**"The type or namespace name 'FSO' could not be found"**
The FreeSO submodule wasn't initialized. Check that `FreeSO/TSOClient/` has content, then re-run step 1.

**"Unable to locate the .NET SDK"**
Install the .NET 9.0 SDK (not just runtime), restart your terminal, and verify with `dotnet --info`.

You should see something like this:
```pwsh
dotnet --info
.NET SDK:  #this is the important part!!!!!
 Version:           9.0.307
 Commit:            3bc3012cf9
 Workload version:  9.0.300-manifests.7913fe7a
 MSBuild version:   17.14.28+09c1be848

Runtime Environment:
 OS Name:     Windows
 OS Version:  10.0.26100
 OS Platform: Windows
 RID:         win-x64
 Base Path:   C:\Program Files\dotnet\sdk\9.0.307\

.NET workloads installed:
There are no installed workloads to display.
Configured to use loose manifests when installing new manifests.

Host:
  Version:      9.0.11
  Architecture: x64
  Commit:       fa7cdded37

.NET SDKs installed:
  9.0.307 [C:\Program Files\dotnet\sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.App 9.0.10 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 9.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 9.0.10 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.NETCore.App 9.0.11 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
  Microsoft.WindowsDesktop.App 9.0.10 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
  Microsoft.WindowsDesktop.App 9.0.11 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]

Other architectures found:
  None

Environment variables:
  Not set

global.json file:
  Not found

Learn more:
  https://aka.ms/dotnet/info

Download .NET:
  https://aka.ms/dotnet/download

```

# Contributing

If you do wanna contribute, I do recommend sending your PRs to [upstream](https://github.com/riperiperi/Simitone). They will be brought down here once merged.

If upstream is ever dead (but this is still active) and/or you can't/won't contribute to upstream for whatever reason, make all your PRs against the [alex-main](https://github.com/alexjyong/Simitone/tree/alex-main) branch, as this branch is just for gathering up changes and updates that aren't available in upstream and packaging them for folks to enjoy. Don't make changes from this branch please.

# Why is it called Simitone?

Simitone -> Semitone -> musical term -> C# -> a note

Further questions can be directed at my PR manager, uh, ... burglar cop.
