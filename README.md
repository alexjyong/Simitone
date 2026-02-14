![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

# Note: This is a forked version of Simitone with some updates that are not in the current version at the time of writing.
# This is not affliated with the Simitone Team!!

[Latest Pre-release](https://github.com/alexjyong/Simitone/releases/latest/) | [Download Windows](https://github.com/alexjyong/Simitone/releases/download/v0.8.20-forked/Simitone-Windows-Release.zip) | [Download Linux](https://github.com/alexjyong/Simitone/releases/download/v0.8.20-forked/Simitone-Linux-x64-Release.zip) 

Alternative C# Frontend for The Sims 1, based off of FreeSO. http://freeso.org 
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection)

**Supported Platforms:** Windows, Linux. MacOS builds but DOES NOT WORK. PRs are welcome!

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Currently does not support the entire Fame career track, saving on vacation and a few other important things. Buy mode on community lots is also not functioning. Currently using a rudimentary blur for censoring since I'm having trouble getting the original to work. Custom objects with custom animations may not work properly. While all stock objects run, many of them have bugs that can make certain lot types unplayable. 

For current development progress on the original Simitone, see [this issue on the original repo.](https://github.com/riperiperi/Simitone/issues/8)

For requests on this repo, fill one out [here](https://github.com/alexjyong/Simitone/issues).

# Purpose

On modern operating systems, The Sims has a few nagging issues that make it less than playable. Simitone can be seen as a tool to improve the playability of your existing installation of The Sims. Here are some features:

- Playable in 3D - using a combination of models generated from the 2D sprites and created by the community.
  - [Download the mesh pack here!](http://forum.freeso.org/forums/3d-remeshing.40/)
- Custom user interface that works at modern resolutions. Working on a more desktop oriented interface.
- Improved graphical performance, support for high resolutions and refresh rates.
- Custom lighting - directional lights with smooth falloffs and shadows using generated 3D meshes.
- Quality of life fixes that were available in later installations of the series such as the eyedropper tool. (note eyedropper currently doesn't work with wallpaper) and rotating camera by clicking mouse wheel.
- *Volcanic*, a program which allows you to examine, modify and create new game objects. (from FreeSO, Windows-only)

# How to Install

## Prerequisites
- **The Sims: Complete Collection** or **The Sims: Legacy Collection**
- Windows Or Linux (Tested with Ubuntu and Linux Mint) (MacOS is currently not functional! PRs welcome!)
- For Windows [.NET 9.0 Runtime and ASP.Net Core runtime 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0). (Note if this isn't installed, Windows will prompt you to download them with a link.)

## Quick Install Windows

1. **Download** the [latest release](https://github.com/alexjyong/Simitone/releases/latest/)
2. **Extract** the ZIP to your preferred location
3. **Run** `Simitone.exe`

**Package Structure:**
```
Simitone-Windows/
  Simitone.exe      <- Run this!
  lib/              <- Game files (don't modify)
```

### First-Time Setup

On first launch, Simitone will automatically scan for The Sims 1 installations:

**Automatic Detection Checks:**
- Portable install (relative path `../The Sims/`)
- Windows Registry (`HKEY_LOCAL_MACHINE\SOFTWARE\Maxis\The Sims`)
- Steam installation (via Steam registry and libraryfolders.vdf)
- Default install (`C:\Program Files (x86)\Maxis\The Sims\`)

**Installation Selector:**
- If **one installation** is found → automatically configured
- If **multiple installations** are found → shows a selection dialog with details
- If **no installations** are found → displays prompt to select installation manually

The selector will show:
- Installation type (Registry, Steam, or Portable)
- Full installation path
- Detection of Steam version (important for save file location)

After selection, Simitone creates a configuration dialog showing:
- Game installation path
- Where The Sims 1 save files are located
- Where Simitone save files will be stored

### User Data Location

Configuration and save files are stored in:
```
%USERPROFILE%\Documents\Simitone\
```

This includes:
- `config.ini` - Game settings and installation path
- `UserData/` - Your Simitone save files and families

**Note:** Simitone uses separate save files from The Sims 1. On first launch, it automatically copies your existing TS1 saves to its own directory. This means:
- You can continue playing with your existing neighborhoods, families, and houses
- Changes in Simitone won't affect your original TS1 saves
- Your original saves remain untouched and can still be used in non-Simitone Sims 1

**The Sims 1 Save Locations (imported from):**
- **Steam (Legacy Collection):** `%USERPROFILE%\Saved Games\Electronic Arts\The Sims 25\UserData`
- **CD/DVD/GOG:** `[Installation Path]\UserData`

## Manual Path Configuration

If you need to bypass auto-detection or reconfigure:

**Command-line override:**
```bash
# Specify custom installation path (bypasses auto-detection)
Simitone.exe -path"C:\Your\Custom\Path\The Sims\"
```

**Portable installation:**
Place The Sims files in `../The Sims/` relative to Simitone.exe (e.g., if Simitone is in `D:\Games\Simitone\`, put The Sims in `D:\Games\The Sims\`)

**To reconfigure/change installation:**
Edit `%USERPROFILE%\Documents\Simitone\config.ini` and set:
```ini
TS1InstallationConfigured=false
```
Then restart Simitone to see the installation selector again.

**Manual config.ini settings:**
```ini
TS1HybridPath=C:\Your\Path\To\The Sims\
TS1IsSteamInstall=false
TS1InstallationConfigured=true
```


## Quick Install Linux / macOS (Note at time of writing, macOS is currently untested. HELP WANTED!)

1. **Download** the [latest self-contained release](https://github.com/alexjyong/Simitone/releases/latest/) for your operating system
2. **Extract** the archive to your preferred location
3. **Run** `./Simitone`

**Package Structure:**
```
Simitone-Linux-x64/          (or Simitone-macOS-x64, Simitone-macOS-arm64)
  Simitone              <- Run this!
  simitone.desktop      <- Linux only: for desktop integration
  lib/                  <- Game files (don't modify)
```

### First-Time Setup

On first launch, Simitone will automatically scan for The Sims 1 installations (see Auto-detection section below for paths checked).

**Installation Selector:**
- If **one installation** is found → automatically configured
- If **multiple installations** are found → shows a GUI selection dialog with details
- If **no installations** are found → shows GUI prompt to browse and select manually

**Alternatively, specify path directly:**
```bash
./Simitone -path"/fully/qualified/path/to/The Sims/" # (note: no space between -path and the quote - this is intentional)
```

After configuration, the path is saved to `config.ini` and you won't need to use `-path` again.

## Manual Path Configuration (Linux/macOS)

If you need to bypass auto-detection or reconfigure:

**Command-line override:**
```bash
# Specify custom installation path (bypasses auto-detection)
./Simitone -path"/your/custom/path/The Sims/"
```

**Portable installation:**
Place The Sims files in `../The Sims/` relative to the Simitone executable (e.g., if Simitone is in `/home/user/Simitone/`, put The Sims in `/home/user/The Sims/`)

**To reconfigure/change installation:**
Edit `config.ini` in your user data directory (see below) and set:
```ini
TS1InstallationConfigured=false
```
Then restart Simitone to see the installation selector again.

**Manual config.ini settings:**
```ini
TS1HybridPath=/your/path/to/The Sims/
TS1IsSteamInstall=false
TS1InstallationConfigured=true
```

### User Data Location

Configuration and save files are stored in:
- **Linux:** `~/.local/share/Simitone/` (or `~/Documents/Simitone/` if MyDocuments exists)
- **macOS:** `~/Documents/Simitone/`

This includes:
- `config.ini` - Game settings and installation path
- `UserData/` - Your Simitone save files and families

**Note:** Simitone uses separate save files from The Sims 1. On first launch, it automatically copies your existing TS1 saves to its own directory. This means:
- You can continue playing with your existing neighborhoods, families, and houses
- Changes in Simitone won't affect your original TS1 saves
- Your original saves remain untouched and can still be used in the plain installation for The Sims 1

**The Sims 1 Save Locations (imported from):**
- **Steam (via Proton):** Check `~/.steam/steam/steamapps/compatdata/[appid]/pfx/drive_c/users/steamuser/Saved Games/Electronic Arts/The Sims 25/UserData`
- **Wine:** Typically in the Wine prefix under `drive_c/users/[username]/My Documents/`
- **Native Windows files:** Check the Windows file structure if accessing via WSL

### The Sims 1 Installation
Simitone reads The Sims 1 game files directly - However, the code has been tested with the Windows installation. You can use:
- **Steam Play/Proton:** `~/.steam/steam/steamapps/common/The Sims/`
- **Wine (default prefix):** `~/.wine/drive_c/Program Files/Maxis/The Sims/`
- **Wine (x86 prefix):** `~/.wine/drive_c/Program Files (x86)/Maxis/The Sims/`
- **WSL (accessing Windows):** `/mnt/c/Program Files (x86)/Maxis/The Sims/`
- **Portable install:** Place in `../The Sims/` relative to Simitone executable

### How Path Detection Works

Simitone finds The Sims 1 installation in this priority order:

1. **Command line argument** (highest priority - optional):
   ```bash
   ./Simitone -path"/path/to/The Sims/"
   ```
   Use this to override auto-detection or bypass the GUI selector.

2. **config.ini setting** (saved from previous selection):
   Located in your user data directory. Once you've selected an installation (via GUI or `-path`), this is saved automatically:
   ```ini
   TS1HybridPath=/fully/qualified/path/to/Sims1/installation
   TS1InstallationConfigured=true
   ```

3. **Auto-detection with GUI selector** (first-time setup):
   - **Linux:** Automatically scans these locations in order:
     - Portable install (relative path `../The Sims/`)
     - Steam Proton: `~/.steam/steam/steamapps/common/The Sims/`
     - Wine (default prefix): `~/.wine/drive_c/Program Files/Maxis/The Sims/`
     - Wine (x86 prefix): `~/.wine/drive_c/Program Files (x86)/Maxis/The Sims/`
     - Fallback location: `game1/`

   - **macOS:** Automatically scans these locations in order:
     - Portable install (relative path `../The Sims/`)
     - Steam: `~/Library/Application Support/Steam/steamapps/common/The Sims/`
     - Wine (default prefix): `~/.wine/drive_c/Program Files/Maxis/The Sims/`
     - Wine (x86 prefix): `~/.wine/drive_c/Program Files (x86)/Maxis/The Sims/`
     - Fallback location: `game1/`

   - **Windows:** Automatically scans these locations in order:
     - Portable install (relative path `../The Sims/`)
     - Windows Registry (`HKEY_LOCAL_MACHINE\SOFTWARE\Maxis\The Sims`)
     - Steam installation (via Steam registry and libraryfolders.vdf)
     - Default install: `C:\Program Files (x86)\Maxis\The Sims\`

**Note:** On first run, auto-detection will find installations and show a GUI selector. The `-path` argument is optional and only needed to override auto-detection or for headless environments. Once configured, the path is saved to config.ini automatically.


### Optional Command-Line Arguments (for all platforms)

**Installation & Path:**
```bash
-path"<path>"    # Specify custom Sims 1 installation path (no space between -path and quote)
```

**Graphics & Performance:**
```bash
-dx / -gl        # Force DirectX or OpenGL rendering
-3d              # Enable 3D mode in the game (can be toggled on or off with F12)
-jit             # Enable JIT compilation for SimAntics (faster gameplay, slower startup)
-hz <rate>       # Set refresh rate
```

**Other Options:**
```bash
-ide             # Launch Volcanic object editor
-lang <code>     # Set language
-nosound         # Disable audio
```

# Building from Source

Want to compile Simitone yourself? Follow these steps.

## Easy Mode: Use GitHub Actions

Don't want to mess with command prompt and stuff? Just fork this repo and run the build workflow:

1. Fork this repository on GitHub
2. Go to the **Actions** tab in your fork (you may be prompted to activate actions)
3. Click **"Build Simitone"** in the left sidebar
4. Click **"Run workflow"** -> Choose **Release** -> Pick which platforms you want to build with -> Click **"Run workflow"**
5. Wait ~4-5 minutes or so, refresh the job page, and you should see the release artifact.


## Local Build

### Windows 

#### Quick Build (Automated Script)

For a streamlined build experience, use the included PowerShell script:

**Note**: If the script opens in an editor instead of running, Windows is blocking PowerShell scripts. Use one of these methods:

```pwsh
# Option 1: Bypass policy **just** for this one script
powershell -ExecutionPolicy Bypass -File .\build.ps1

# Option 2: Unblock the script file once (then use .\build.ps1 normally)
Unblock-File .\build.ps1
```

Or in powershell directly
```pwsh
# Basic build (this will use release configuration, not debug configuration.)
.\build.ps1
```

List of options you can use with the script
```pwsh
.\build.ps1 -Configuration Debug     # Build in Debug mode
.\build.ps1 -Clean                   # Clean Simitone artifacts before building (obvs, you won't need this on first run)
.\build.ps1 -CleanAll                # Deep clean (includes FreeSO submodule and its submodules!)
.\build.ps1 -Run                     # Build and run immediately
.\build.ps1 -Clean -Run              # Clean, build, and run
.\build.ps1 -Publish                 # Create distributable package with launcher

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

#### Manual Build (Step-by-Step)

If you'd rather do it by hand to understand each step, or for whatever reason, read the instructions below:

##### Prerequisites

1. **Git** (version 2.13 or later)
   - Download: https://git-scm.com/downloads
   - Verify: `git --version`

2. **.NET 9.0 SDK**
   - Download: https://dotnet.microsoft.com/en-us/download/dotnet/9.0
   - Get the "SDK x64" installer (not just the runtime)
   - Verify: `dotnet --info` (should show 9.0.x)

3. **Windows OS** (Windows 10/11 recommended)

4. **The Sims: Complete Collection** or **The Sims: Legacy Collection** (to run the compiled game)

#### Build Steps

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


### Building from Source (Linux/MacOS) (Note that running the game on MacOS is untested!)
```bash
# Install runtime dependencies
sudo apt install libsdl2-2.0-0 libopenal1  # Ubuntu/Debian/WSL
sudo dnf install SDL2 openal-soft          # Fedora
sudo pacman -S sdl2 openal                 # Arch
brew install sdl2                          # macOS

# Build
./build-mac-linux.sh

# Optional (build a self-contained release, which doesn't rely on the machine having dependencies installed)
./build-mac-linux.sh --publish

# Run (point to your The Sims installation)
cd Client/Simitone/Simitone.Desktop/bin/Release/net9.0/
./Simitone -path"/fully/qualified/path/to/The Sims/"

# If you made a self-contained release, run it like so:
cd publish/Simitone-Linux-x64/   # or Simitone-macOS-x64, Simitone-macOS-arm64
./Simitone -path"/fully/qualified/path/to/The Sims/"
```

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

# Attributions

* Icon for Eyedropper tool: <a target="_blank" href="https://icons8.com/icon/78728/color-dropper">eyedropper</a> icon by <a target="_blank" href="https://icons8.com">Icons8</a>

* Icon for Free Will option: <a target="_blank" href="https://icons8.com/icon/13KBHI5xdOT3/creativity-and-resourcefulness">Creativity And Resourcefulness</a> icon by <a target="_blank" href="https://icons8.com">Icons8</a>
