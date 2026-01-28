# Simitone

Simitone is an alternative C# frontend for The Sims 1, built on top of the FreeSO engine. It allows The Sims 1 to run on modern operating systems (Windows, Linux, macOS) with improvements like 3D rendering, high-resolution support, custom lighting, and quality-of-life fixes.

Requires a legitimate copy of The Sims: Complete Collection or The Sims: Legacy Collection.

## Project Structure

- `Client/Simitone/` — Main Simitone frontend (solution: `Simitone.sln`)
  - `Simitone.Client/` — Core game logic (library, .NET 9.0)
  - `Simitone.Windows/` — Windows executable (net9.0-windows, DirectX)
  - `Simitone.Desktop/` — Linux/macOS executable (net9.0, OpenGL)
  - `Simitone.Shared/` — Shared code (game locator, UI utilities)
  - `Simitone.Launcher/` — Launcher application
- `FreeSO/` — Git submodule; the core simulation engine (reimplementation of The Sims Online)
  - `TSOClient/tso.files/` — File format parsing (IFF, sprites, etc.)
  - `TSOClient/tso.simantics/` — SimAntics VM (behavior scripting bytecode interpreter)
  - `TSOClient/tso.client/` — Game client core
  - `TSOClient/tso.world/` — Lot view and world rendering
  - `TSOClient/FSO.UI/` — UI framework
  - `TSOClient/FSO.IDE/` — Volcanic (object editor/IDE)
- `Tools/` — Utility tools
  - `IffExporter/` — Exports binary IFF files to JSON for analysis
  - `IffPatcher/` — Patches IFF files

## Building

**Windows (PowerShell):**
```
.\build.ps1                    # Release build
.\build.ps1 -Configuration Debug
.\build.ps1 -Publish           # Create distributable package
.\build.ps1 -Run               # Build and run
.\build.ps1 -Clean             # Clean artifacts
```

**Linux/macOS (Bash):**
```
./build-mac-linux.sh           # Release build
./build-mac-linux.sh debug     # Debug build
./build-mac-linux.sh --publish # Self-contained release
```

Both scripts handle submodule init, NuGet restore, and building. Manual steps: `git submodule update --init --recursive`, then `dotnet restore` and `dotnet build` on both `Simitone.sln` and `FreeSO.sln`.

**Build Output Location:**
- **Windows:** `Client\Simitone\Simitone.Windows\bin\Release\net9.0-windows\`
  - `Simitone.exe` — Main game executable
  - `Volcanic.exe` — Object editor/IDE (built from FreeSO/FSO.IDE)
- **Linux/macOS:** `Client/Simitone/Simitone.Desktop/bin/Release/net9.0/`

**Running Volcanic (Windows Only):**

⚠️ **Important:** Volcanic is **Windows-only** (uses Windows Forms). It won't build or run on Linux/macOS.

When running on Windows, Volcanic.exe uses `Path.GetFullPath()` to locate Simitone.exe, which resolves relative to the **current working directory** (not the executable's location). This means:

1. **Run from the build output directory:**
   ```powershell
   cd Client\Simitone\Simitone.Windows\bin\Release\net9.0-windows
   .\Volcanic.exe
   ```

2. **Or copy/symlink Simitone.exe to your working directory** if you want to run Volcanic from elsewhere.

If you see "Failed to find FreeSO or Simitone," it means Volcanic couldn't find `Simitone.exe` in the current directory. This is a known limitation of the current implementation - it should ideally look in the executable's directory rather than the current working directory.

## Tech Stack

- **Language:** C#
- **Runtime:** .NET 9.0
- **Rendering:** MonoGame 3.8.x (DirectX on Windows, OpenGL on Linux/macOS)
- **Key dependencies:** Newtonsoft.Json, SixLabors.ImageSharp, Eto.Platform.Wpf (Windows UI)
- **CI/CD:** Azure Pipelines (`azure-pipelines.yml`)

## Submodule Structure

```
Simitone
└── FreeSO (github.com/alexjyong/FreeSO)
    ├── FSOMonoGame (custom MonoGame fork for rendering)
    ├── FSOMina.NET (network protocol)
    └── assimp-net (3D model loading)
```

## Key Concepts

- **IFF (Interchange File Format):** Binary format used by The Sims for storing game objects, behaviors, strings, sprites, and more. Parsed by `FreeSO/TSOClient/tso.files/Formats/IFF/`. Supports 44+ chunk types (BHAV, OBJD, STR#, SPR2, DGRP, TTAB, SLOT, etc.). See detailed IFF documentation below.
- **SimAntics:** The Sims' behavior scripting system. A bytecode VM that drives all object interactions and game logic. Implemented in `tso.simantics/`. Uses hex-encoded bytecode instructions.
- **BHAV:** Behavior chunks in IFF files containing SimAntics bytecode. These are "trees" of instructions with line numbers starting at 0, each line having True/False outcomes.
- **OBJD:** Object Definition chunks defining properties of game objects (prices, categories, ratings, GUIDs, etc.).
- **PIFF:** Patch IFF format for applying modifications to IFF files without replacing them.
- **Volcanic:** FreeSO's object editor/IDE for inspecting and editing game objects (`FSO.IDE`).

## Understanding IFF Files

### IFF Structure

IFF files are container files organized into **blocks** (also called "chunks"). Each block contains **numbered resources** (sub-blocks) that hold specific data. Think of an IFF as a file cabinet where:
- **Blocks** are drawers (identified by 4-character type codes like `BHAV`, `OBJD`)
- **Resources** are folders inside drawers (identified by numeric IDs, usually starting at specific ranges)
- Each resource contains the actual data (code, strings, images, etc.)

### Critical IFF Block Types

**Object Data & Metadata:**
- **OBJD** (Object Definition) — Core object properties: GUID, price, catalog categories, room/function flags, ratings, depreciation, graphic IDs, animation IDs. This is the "birth certificate" of every object.
- **OBJf** (Object Function Table) — Maps common actions to BHAV tree numbers (init, main, cleanup, placement, etc.). Used in newer objects; replaces the old function table in OBJD.
- **GUID** — Globally Unique IDentifier for the object. Must be unique to prevent conflicts. Generated using "magic cookies".

**Behavior & Logic (SimAntics):**
- **BHAV** (Behavior) — Contains SimAntics bytecode "trees" (programs). Each tree has numbered lines of instructions. Each line has:
  - A **function** (type of operation: Expression=2, Dialog=36, etc.)
  - Four **parameters** (stored as 8 hex bytes / 4 decimal values)
  - Two **outcomes** (True/False paths: line number, or True/False/Error to exit)
- **TTAB** (Tree Table / Interaction Menu) — Defines pie menu options that appear when clicking an object. Links to BHAV action/test trees. Includes "advertisements" (promises of need fulfillment like Fun, Energy).
- **GLOB** (Global References) — Declares which global IFF files this object references (e.g., "ArtGlobals.iff", "PersonGlobals.iff").

**Strings & Text:**
- **STR#** (String Table) — Multi-purpose string storage with sub-blocks:
  - `#129` "a2o" — Adult-to-object animations
  - `#130` "c2o" — Child-to-object animations
  - `#301` "Dialog prim string set" — Dialog text for message boxes
  - `#303` "Named trees" — Names for remotely-callable BHAV routines
- **CTSS** (Catalog Strings) — Multi-language name/description for Buy/Build catalog or Sim bios. Resource `#2000` typically used.
- **TTAs** (Pie Menu Strings) — Multi-language text for menu options in TTAB. Must match TTAB order.

**Graphics:**
- **SPR2** (Sprite Data) — Compressed sprite images used in-game. Contains pixel data and z-buffers.
- **DGRP** (Draw Group) — Points to sprites in SPR2, defines how object appears at different rotations/zoom levels.
- **BMP_** (Bitmaps) — Uncompressed bitmaps for catalog thumbnails and thought bubbles:
  - `#2000+` — Catalog thumbnails
  - `#4000+` — Small speech bubble images
  - `#6000+` — Large speech bubble images
- **PALT** (Palette) — 256-color palettes for sprites.

**Object Interaction:**
- **SLOT** (Slot Definition) — Defines where Sims can stand to interact with the object. Determines approach paths and valid interaction positions.
- **FWAV** (Audio Events) — References to sound files (.xa format) that play during interactions.

**Other:**
- **BCON** (Constants) — Numeric constant values referenced by BHAV code via resource IDs.
- **TPRP**, **TRCN** — Tree parameters and constants used by behavior trees.

### IFF Numbering Conventions

IFF resources follow conventional ID ranges:
- **BHAV trees:** Usually start at `#4096` (`0x1000`)
- **TTAB/TTAs menus:** Often `#129` or `#130`
- **SLOT:** Typically `#128`
- **OBJD:** Often matches object GUID last 4 digits or uses `#16807`
- **Catalog (CTSS, BMP_):** `#2000`
- **SPR2/DGRP/PALT graphics:** Often start at `#100`

### SimAntics BHAV Programming

BHAV trees are the "code" that makes objects work. Each tree is a list of instructions starting at line 0:

**Instruction Structure:**
```
Line #N: [Function] Param1, Param2, Param3, Param4 → True: [outcome], False: [outcome]
```

**Function Types:**
- `2` — Expression (comparisons, assignments, math)
- `36` — Dialog (show message boxes, Yes/No prompts)
- `31` — Find object by GUID
- `19` — Make new character
- Many more (50+ function types)

**Outcomes:**
- **Line number** — Jump to that line
- **True** (254) — Exit tree successfully
- **False** (255) — Exit tree as false
- **Error** (253) — Exit with error

**Parameters:** Hex-encoded values that define what the instruction does. The meaning depends on the function type. For Expressions (function 2):
- Param1: Left operand
- Param2: Right operand
- Param3: Operator (equals, greater than, assign, etc.)
- Param4: Data types/flags

**Variables:**
- **Local Variables** — Private to the current tree (Local 0, Local 1, etc.)
- **Temporary Storage** — Shared between trees (acts as return value)
- **Attributes** — Object properties (My Attribute 0, Stack Object's Attribute 6)
- **Person Data** — Sim properties:
  - Stored in `neighborhood.iff`: Personality (Nice, Active, Playful, Outgoing, Neat), Skills (Cooking, Charisma, Mechanical, Creativity, Body, Logic), Interests
  - Runtime values: Motives (Hunger, Comfort, Hygiene, Bladder, Energy, Fun, Room, Social)
  - BHAV parameter `1807` (0x070F) = "Stack object's motives", `1810` (0x0712) = "My person data"

### Multi-Language Support

The Sims supports 20+ languages. Text blocks (CTSS, TTAs, STR#301) store separate strings for each language:
- US English, UK English, French, German, Italian, Spanish, Dutch, Danish, Swedish, Norwegian, Finnish, Portuguese, Japanese, Polish, Russian, Greek, Hebrew, Thai, Korean, Traditional Chinese, Simplified Chinese

When editing, use "Set for all languages" to copy one translation to all slots. Note: IFF Pencil skips the last 4 Asian languages; these must be filled manually.

### Object Categories (Buy/Build Mode)

Objects appear in catalogs based on flags in the OBJD block:

**Room Flags** (where objects are used):
- Kitchen=1, Bedroom=2, Bathroom=4, Study=128, Living=8, Dining=32, Outside=16, Misc=64

**Function Flags** (what objects do):
- Seating=1, Surfaces=2, Appliances=4, Electronics=8, Plumbing=16, Decorative=32, Lighting=128, Misc=64

**Subsort Flags** (HD+ subcategories):
- Binary progression: 1, 2, 4, 8, 16 (for 5 subcategories within each Function)
- Example: Seating → Dining chairs=3, Lounge chairs=1, Sofas=2, Beds=4, Misc=16

**Downtown/Vacation/Studio/Magic Town Sorts:**
- Use additive binary values: 1, 2, 4, 8, 128
- Objects can appear in multiple categories (e.g., 143 = all categories)

### FAR Files

FAR files are archive containers for packaging multiple game files:
- Can contain: IFF objects, XA sounds, BMP textures, BMF meshes, BCF animations, PALT palettes
- Cannot contain (or will ignore): ASCII files like SKN meshes or CMX definitions
- Placed in `Downloads/` or `GameData/UserObjects/`
- Files inside override files in system FAR archives (useful for modding)

### Important Tools

Historical object editing tools (for reference when understanding IFF structure):
- **IFF Pencil 2** — Primary IFF editor for The Sims 1 (compatible through Unleashed)
- **Transmogrifier (T-Mog)** — Object cloner, generates unique GUIDs
- **Object Editor Light** — Simple editor for making objects buyable
- **Hot Date Object Organizer** — Sets HD+ subsort categories
- **Animation Alchemist** — Edit animation strings in STR# blocks

Modern tools:
- **Volcanic** — FreeSO's comprehensive object editor/IDE (`FSO.IDE`)
- **IffExporter** — Exports IFF to human-readable JSON (`Tools/IffExporter/`)
- See `Tools/IffExporter/QWEN.md` for detailed IFF export information