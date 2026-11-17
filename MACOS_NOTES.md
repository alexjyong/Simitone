# macOS notes

Simitone runs natively on Apple Silicon (no Wine/VM) with the changes on the `macos-fix` branch.

Tested on: MacBook (arm64), macOS 27.0.1, .NET SDK 9.0.122 (Homebrew `dotnet@9`), The Sims Complete
Collection (Windows disc install inside a CrossOver bottle).

## Building and running

```bash
brew install dotnet@9
export DOTNET_ROOT=/opt/homebrew/opt/dotnet@9/libexec PATH=/opt/homebrew/opt/dotnet@9/bin:$PATH

# Development build: bin/Release/net9.0/Simitone (bare exe) and bin/Release/net9.0/Simitone.app
./build-mac-linux.sh
cd Client/Simitone/Simitone.Desktop/bin/Release/net9.0/
./Simitone -path"/abs/path/to/The Sims/"

# Self-contained release: publish/Simitone-macOS-arm64/Simitone.app (~170 MB, no .NET install needed)
./build-mac-linux.sh --publish
open publish/Simitone-macOS-arm64/Simitone.app
```

SDL2 and OpenAL don't need to be installed with Homebrew: the universal (x86_64 + arm64) dylibs come
from the `MonoGame.Library.SDL` / `MonoGame.Library.OpenAL` NuGet packages.

## Fixes

### 1. Build failed: Eto.Platform.Mac64 auto-bundling
`./build-mac-linux.sh` failed with
`BundleDotNetCore.targets(184,5): error : No RuntimeIdentifiers or MacRuntimeIdentifiers have been specified`.
Eto.Platform.Mac64's MSBuild targets try to publish a `.app` on every build when no RID is given.

Fix (`Simitone.Desktop.csproj`): `MacAutoPublishBundle=False` on macOS. Eto then builds a framework-dependent
`Simitone.app` next to the normal output instead of trying to publish one.

### 2. Startup crash: NuGet assemblies and native libs missing from the build output
`System.IO.FileNotFoundException: Could not load file or assembly 'Eto, Version=2.9.0.0'`.
The project sets `CopyLocalLockFileAssemblies=false`, so a plain `dotnet build` copied none of the NuGet
assemblies (Eto, MonoGame) or their `runtimes/osx/native` dylibs (SDL2, OpenAL).

Fix (`Simitone.Desktop.csproj`): `CopyLocalLockFileAssemblies=true` on macOS. After this the game ran,
the neighbourhood rendered, and `libSDL2-2.0.0.dylib` / `libopenal.dylib` were loaded from
`runtimes/osx/native/`. (Linux keeps the old setting, since I couldn't test it there.)

### 3. Window icon: wrong SDL2 library name
`Warning: Failed to set window icon: Unable to load shared library 'libSDL2.dylib'`.
`IconLoader` P/Invoked `libSDL2.dylib`, but both MonoGame and Homebrew ship `libSDL2-2.0.0.dylib`.

Fix (`Simitone.Client/Utils/IconLoader.cs`): use `libSDL2-2.0.0.dylib` on macOS. This also makes the
`fix-mac-sdl2` CI workaround that copies SDL2 under a second name unnecessary.

### 4. Install picker: Eto.Mac only works inside an .app bundle
Without `-path`, the picker dialog failed and the game exited. The swallowed exception was:
`Platform type 'Eto.Mac.Platform, Eto.Mac64' was loaded but is not valid in the current context.
E.g. Mac platforms require to be in an .app bundle to run`.

Fixes:
- `Program.cs`: both Eto `catch` blocks now print the exception instead of swallowing it.
- `Program.cs`: when there's no install and Eto can't start (bare executable), fall back to the native
  AppleScript folder picker (`osascript -e 'choose folder'`), and check the folder for `GameData/Behavior.iff`.
- `Simitone.app` is now a usable bundle: `Info.plist` (name, bundle ID, `LSMinimumSystemVersion` 12.0
  to match .NET 9) and `Mac/Icon.icns`, generated from `Simitone.Windows/Icon.ico`. Inside the bundle the
  Eto dialog works. Tested with two installs detected: pick one, press Select, the dialog closes, and the
  game starts in the same process.

### 5. Running from the bundle: content not found
Running `Simitone.app/Contents/MacOS/Simitone` crashed with
`ContentLoadException: ... Simitone.app/Contents/Resources/Content/OGL/Effects/2DWorldBatch.xnb`.
On macOS, MonoGame's `TitleContainer` roots content at `Contents/Resources` whenever that folder exists.
FSO reads `Content/` relative to the executable in `Contents/MacOS`.

Fix (`Simitone.Desktop.csproj`, target `MacLinkBundleContent`): after Eto finishes a bundle (build or
publish), symlink `Contents/Resources/Content -> ../MacOS/Content`.

### 6. Auto-detect CrossOver installs
`MacOSLocator` only checked Steam, `~/.wine` and relative paths. Most Mac players with a disc copy
install it into CrossOver.

Fix (`MacOSLocator.cs`): scan `~/Library/Application Support/CrossOver/Bottles/*/drive_c/Program Files{, (x86)}/`
for `Maxis/The Sims/` and `Steam/steamapps/common/The Sims/`. The CrossOver path was also added to the
"common locations" help text.

### 7. `--publish` on macOS produces `Simitone.app`
Before, the publish step built the C launcher + `lib/` layout used on Linux. That opens Terminal when
double-clicked, Eto still can't run from it, and Eto's own `Simitone.app` ended up nested inside `lib/`.

Fix (`build-mac-linux.sh`): on macOS, ship `publish/Simitone-macOS-<arch>/Simitone.app` directly.
- The pruning of unused TSO/server files moved into a `prune_published_files` function, shared with Linux.
- Eto's publish step copies the half-built bundle into its own `Contents/MacOS`. The script deletes that
  nested copy.
- Pruning shrinks the bundle from ~460 MB to ~170 MB.
- Tested: `open Simitone.app` with no `DOTNET_ROOT` and no .NET install on the path launches the game.

## Checks on the original suspects

| Suspect | Result |
| --- | --- |
| `MonogameLinker` / `Directory.Exists("/Users")` | Works; MonoGame loads from the output root. The `Monogame/MacOS/` copy is unused but harmless. Native libs fixed by #2. |
| `IconLoader` `libSDL2.dylib` | Real bug, fixed in #3. |
| Eto dialog from a bare exe | Real bug, fixed in #4 and #7. |
| OpenGL 4.1 core / MRT shaders | No problems so far. Runs on Apple's Metal-backed GL (`AppleMetalOpenGLRenderer`) with `UseMRT = true`. The neighbourhood, lot view (walls, floors, pool water, terrain, trees, cutaways) and family-picker heads render correctly. The driver logs once: `UNSUPPORTED (log once): POSSIBLE ISSUE: unit 0 GLD_TEXTURE_INDEX_2D is unloadable` (some pass samples an incomplete texture). I didn't find a visible artefact. |
| Case sensitivity / backslashes | Not a factor on the default case-insensitive APFS. **Untested on a case-sensitive volume.** |

## Not verified / known issues

- **Sims on a live lot**: avatar rendering, animation and gameplay weren't checked on an occupied lot.
  I drove the UI with synthesized clicks and couldn't get a family moved in, so someone needs to play
  a household by hand.
- **Audio**: OpenAL loads and initialises without errors, but I didn't listen to it.
- **Intel Macs**: the natives are universal, but the `osx-x64` build wasn't run.
- **Framework-dependent `Simitone.app` from `bin/`** can't be opened from Finder with Homebrew's keg-only
  `dotnet@9`: "You must install .NET to run this application" (the apphost only looks in
  `/usr/local/share/dotnet` or `DOTNET_ROOT`). Use the `--publish` bundle, or the official .NET pkg.
- **Code signing**: the bundle isn't signed at the bundle level. `codesign` treats every file in
  `Contents/MacOS` (the ~400 managed `.dll`s) as nested code and refuses to seal it. `--deep` also trips
  over `Content/UI/hints/*.dir`. The apphost and native dylibs carry their own signatures, which is
  enough to run a local build. A downloaded copy needs `xattr -cr Simitone.app`. Notarized releases would
  need the managed files moved out of `Contents/MacOS`.
- **CI (`.github/workflows/build.yml`)** still builds its own launcher + `lib/` layout for macOS and
  doesn't use `build-mac-linux.sh`. It should be switched to the `.app` packaging in #7. Not changed
  here because I couldn't test it.
- Once I saw the bundle window captured at 221x174. It didn't reproduce over 30 s of sampling (always
  1024x800), so it was probably an animation in progress.
