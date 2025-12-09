![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

[Latest Pre-release](https://github.com/riperiperi/Simitone/releases/latest/) | [Download](https://github.com/riperiperi/Simitone/releases/latest/download/SimitoneWindows.zip)

Alternative C# Frontend for The Sims 1, based off of FreeSO. http://freeso.org 
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection)

**Supported Platforms:** Windows, Linux, macOS

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Currently does not support the entire Fame career track, saving on vacation and a few other important things. While all objects run, many of them have bugs that can make certain lot types unplayable. For current development progress, see [this issue.](https://github.com/riperiperi/Simitone/issues/8)

# Purpose

On modern operating systems, The Sims has a few nagging issues that make it less than playable. Simitone can be seen as a tool to improve the playability of your existing installation of The Sims. Here are some features:

- Playable in 3D - using a combination of models generated from the 2D sprites and created by the community.
  - [Download the mesh pack here!](http://forum.freeso.org/forums/3d-remeshing.40/)
- Custom user interface that works at modern resolutions. Working on a more desktop oriented interface.
- Improved graphical performance, support for high resolutions and refresh rates.
- Custom lighting - directional lights with smooth falloffs and shadows using generated 3D meshes.
- *Volcanic*, a program which allows you to examine, modify and create new game objects. (from FreeSO)

# Why is it called Simitone?

Simitone -> Semitone -> musical term -> C# -> a note

Further questions can be directed at my PR manager, uh, ... burglar cop.

# Platform Support

## Windows
Use `Simitone.Windows` - download from [releases](https://github.com/riperiperi/Simitone/releases/latest/).

## Linux / macOS
Use `Simitone.Desktop` which uses OpenGL via MonoGame DesktopGL.

### Quick Start (Linux)
```bash
# Install dependencies
sudo apt install libgdiplus libopenal1  # Ubuntu/Debian

# Build
./build-linux.sh

# Run (point to your The Sims installation)
cd Client/Simitone/Simitone.Desktop/bin/Release/net9.0/
./Simitone -path"/path/to/The Sims/"
```

### The Sims 1 Installation
Simitone reads The Sims 1 game files directly - they're platform-agnostic (IFF, BMP, WAV formats). You can use:
- A Windows installation via Wine/Proton
- Steam Play/Proton: `~/.steam/steam/steamapps/common/The Sims/`
- Wine prefix: `~/.wine/drive_c/Program Files/Maxis/The Sims/`
- WSL (accessing Windows): `/mnt/c/Program Files (x86)/Maxis/The Sims/`

For detailed instructions, see [docs/LINUX_MACOS_SUPPORT.md](docs/LINUX_MACOS_SUPPORT.md).
