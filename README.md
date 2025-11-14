![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

# Note: This is a forked version of Simitone with some updates and minor bug fixes that are not in the current version at the time of writing.
# This is not affliated with the Simitone Team

[Latest Pre-release](https://github.com/alexjyong/Simitone/releases/latest/) | [Download](https://github.com/alexjyong/Simitone/releases/download/v0.8.12-forked/SimitoneWindows-Release.zip)

Alternative C# Windows Frontend for The Sims 1, based off of FreeSO. http://freeso.org 
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection)

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Currently does not support the entire Fame career track, saving on vacation and a few other important things. While all objects run, many of them have bugs that can make certain lot types unplayable. For current development progress, see [this issue.](https://github.com/riperiperi/Simitone/issues/8)

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

  - -dx / -gl - Force DirectX or OpenGL rendering
  - -3d - Enable 3D mode in the game (can be toggled on or off with F12)
  - -jit - Enable JIT compilation for SimAntics (can elpthe game run faster at the expense of more start up time)
  - -ide - Launch Volcanic object editor
  - -lang <code> - Set language
  - -hz <rate> - Set refresh rate
  - -nosound - Disable audio

# Building from Source

Want to compile Simitone yourself? Follow these steps.

## Easy Mode: Use GitHub Actions

Don't want to install .NET locally? Just fork this repo and run the build workflow:

1. Fork this repository on GitHub
2. Go to the **Actions** tab in your fork
3. Click **"Build Simitone"** in the left sidebar
4. Click **"Run workflow"** → Choose **Release** → Click **"Run workflow"**
5. Wait ~5 minutes, then download the build artifact from the workflow run

No local setup required! GitHub's runners handle everything.

## Local Build

If you want to build locally:

### Prerequisites

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

#### 1. Clone and Initialize Submodules

```pwsh
git clone https://github.com/alexjyong/Simitone.git
cd Simitone
git submodule update --init --recursive
```

The submodule step downloads FreeSO (the core simulation engine). This may take a few minutes.

#### 2. Run Protobuild (Optional)

```pwsh
cd FreeSO/Other/libs/FSOMonoGame/
./protobuild.exe --generate
cd ../../../..
```

**Note**: This step may fail on some systems - that's okay! The build can still succeed without it.

#### 3. Restore Dependencies

```pwsh
# Restore Simitone dependencies (required)
dotnet restore Client/Simitone/Simitone.sln

# Restore FreeSO dependencies (recommended)
dotnet restore FreeSO/TSOClient/FreeSO.sln

# Restore Roslyn/JIT dependencies (optional, for -jit support)
cd FreeSO/TSOClient/FSO.SimAntics.JIT.Roslyn/
dotnet restore
cd ../../..
```

#### 4. Build

```pwsh
# Release build (optimized, recommended)
dotnet build Client/Simitone/Simitone.sln -c Release --no-restore

# Or Debug build (includes debugging symbols)
dotnet build Client/Simitone/Simitone.sln -c Debug --no-restore
```

#### 5. Run

Find your compiled executable at:
```pwsh
Client/Simitone/Simitone.Windows/bin/Release/net9.0-windows/Simitone.exe
```

Or run directly:
```pwsh
dotnet run --project Client/Simitone/Simitone.Windows/Simitone.Windows.csproj -c Release
```

### Troubleshooting

**"FreeSO folder is empty"**

```pwsh
rm -rf FreeSO
git submodule update --init --recursive --force
```
**"The type or namespace name 'FSO' could not be found"**
The FreeSO submodule wasn't initialized. Check that `FreeSO/TSOClient/` has content, then re-run step 1.

**"Unable to locate the .NET SDK"**
Install the .NET 9.0 SDK (not just runtime), restart your terminal, and verify with `dotnet --version`.

### Development

For code modifications:
- Use Visual Studio 2022 or VS Code with C# extension
- Open `Client/Simitone/Simitone.sln`

# Why is it called Simitone?

Simitone -> Semitone -> musical term -> C# -> a note

Further questions can be directed at my PR manager, uh, ... burglar cop.
