# IFF Exporter - Detailed IFF Format Guide

This document provides comprehensive information about The Sims IFF (Interchange File Format) file structure, based on community documentation and object hacking guides. This information helps understand what the IffExporter is extracting and how to interpret the exported JSON.

## Data Types & Encoding

### Numeric Types

The Sims uses standard binary numeric types with specific purposes:

| Type | Size | Range | Usage |
|------|------|-------|-------|
| u8/byte | 1 byte | 0–255 | Flags, counts, small values |
| u16/word | 2 bytes | 0–65,535 | Resource IDs, small integers |
| u32/dword | 4 bytes | 0–4,294,967,295 | File offsets, GUIDs, large integers |
| f32/float | 4 bytes | IEEE 754 | Coordinates, animation data |

### ⚠️ CRITICAL: Byte Ordering

The Sims uses **different byte orders** for different formats. This is a common source of parsing errors:

**BIG-ENDIAN (Network Byte Order):**
- IFF container headers
- IFF resource headers

**LITTLE-ENDIAN (Intel Byte Order):**
- IFF resource contents (the actual data)
- FAR archives (all parts)
- CMX, BCF, SKN, BMF, BMP formats

When implementing parsers, you **must** swap byte order when transitioning from IFF headers to IFF resource data. This applies to multi-byte values (u16, u32, f32).

### String Encodings

The Sims uses three string encoding methods:

1. **Pascal Strings** — Length byte followed by characters
   - First byte = string length (0-255)
   - Maximum 255 characters
   - No null terminator

2. **C Strings** — Null-terminated character sequences
   - Characters followed by `0x00` byte
   - Variable length

3. **Padded Strings** — String with alignment padding
   - String data followed by `0xA3` padding bytes
   - Padded to specific alignment boundaries (4-byte, 8-byte, etc.)

### File Size Limits

Binary format constraints impose practical limits:

- **FAR archives:** 4 GB maximum (32-bit offset addressing)
- **IFF files:** 4 GB maximum (32-bit size fields)
- **Individual resources:** ~16 MB practical limit (engine constraints)

## What is an IFF File?

IFF files are the primary data format used by The Sims 1 and The Sims Online to store game objects, character data, behaviors, graphics, sounds, and more. Think of an IFF as a structured database file containing everything needed to define an object in the game.

**An IFF file contains:**
- Object metadata (name, price, category, GUID)
- Behavior code (SimAntics bytecode)
- Graphics (sprites, thumbnails, palettes)
- Strings (names, descriptions, menu options in 20+ languages)
- Animations (references to character motions)
- Sound references
- Interaction menus (pie menus that appear when clicking objects)
- Layout data (how sprites are arranged, where Sims can stand)

## IFF File Structure

### Three-Level Hierarchy

```
IFF File
├── Block Type (4-char code, e.g. "BHAV")
│   ├── Resource #0 (numbered entry)
│   ├── Resource #1
│   └── Resource #N
├── Block Type (e.g. "OBJD")
│   └── Resource #16807
└── Block Type (e.g. "STR#")
    ├── Resource #129
    └── Resource #301
```

**Level 1: Blocks (Chunks)**
- Identified by 4-character type codes (`BHAV`, `OBJD`, `STR#`, etc.)
- Acts as a category/namespace for related data
- Multiple blocks of the same type allowed in one IFF

**Level 2: Resources**
- Numbered entries within a block (IDs are 16-bit integers)
- Each resource has:
  - Resource ID (numeric identifier)
  - Optional label/name (string description)
  - Binary data payload

**Level 3: Data**
- The actual content (varies by block type)
- Can be: bytecode, strings, image data, numeric values, structured records

## Container Formats

### FAR Archive Format

FAR archives bundle multiple files into a single `.far` package for distribution and loading efficiency. The format uses a simple header-files-manifest layout with **little-endian** byte ordering throughout.

**Format Characteristics:**
- No compression (files stored as-is)
- Little-endian byte order (all fields)
- Up to 4 GB total size (32-bit offsets)
- Random access via manifest lookup

**FAR Header (16 bytes):**
```
Offset 0-7:   Signature "FAR!byAZ" (8 bytes, ASCII)
Offset 8-11:  Version = 1 (u32, little-endian)
Offset 12-15: Manifest offset (u32, little-endian, byte position of manifest)
```

**File Data Section:**
After the header, file contents are stored back-to-back with no padding. File positions are determined by the manifest.

**Manifest Section:**
Located at the byte offset specified in the header. Contains one entry per file:

**Manifest Entry Format:**
```
Bytes 0-3:   File size in bytes (u32, little-endian)
Bytes 4-7:   File size duplicate (u32, little-endian) — always identical to bytes 0-3
Bytes 8-11:  File data offset (u32, little-endian, byte position from start of FAR)
Bytes 12-15: Filename length (u32, little-endian)
Bytes 16+:   Filename (UTF-8, not null-terminated, length specified)
```

**Entry Size Calculation:**
`entry_size = 16 + filename_length` bytes

**Duplicate Size Field Note:**
Both size fields (bytes 0-3 and 4-7) are always identical. They were likely reserved for future compressed/uncompressed size differentiation, but compression was never implemented.

**FAR Archive Contents:**
FAR archives can contain:
- IFF files (`.iff`, `.flr`, `.wll`, `.spf`)
- XA sound files (`.xa`)
- BMP textures (`.bmp`)
- BMF meshes (`.bmf`)
- BCF animations (`.cmx.bcf`)
- PALT palettes (`.pal`)

**FAR Archives Cannot Contain** (will be ignored):
- ASCII text files (`.cmx`, `.skn` text formats)
- Other FAR archives (no nesting)

**Resource Override Behavior:**
Files in `Downloads/` FAR archives override files in `GameData/` FAR archives when they have matching filenames. This enables modding without replacing base game files.

### IFF File Format

**Binary Structure:**

**IFF Header (64 bytes):**
```
Offset 0-59:  Signature string (60 bytes, null-padded ASCII):
              "IFF FILE 2.5:TYPE FOLLOWED BY SIZE\0 JAMIE DOORNBOS & MAXIS 1"
Offset 60-63: rsmp offset (u32, big-endian)
              0 = no resource map present
              Non-zero = byte offset to rsmp resource
```

**Resource Header (76 bytes, repeated for each resource):**
```
Offset 0-3:   Type code (4 bytes, ASCII, e.g., "BHAV", "OBJD", "STR#")
Offset 4-7:   Total size (u32, big-endian) — includes this 76-byte header + data
Offset 8-9:   Resource ID (u16, big-endian, 0-65535)
Offset 10-11: Flags (u16, big-endian)
              0x0000 = normal resource
              0x0010 = alternative flag (rare, purpose unclear)
Offset 12-75: Name (64 bytes, null-padded ASCII, optional human-readable label)
Offset 76+:   Resource data (variable length, little-endian)
```

**Critical Note:** Resource headers use big-endian byte order, but the resource data immediately following uses little-endian. Parsers must swap byte order at the 76-byte boundary.

### rsmp — Resource Map

The **rsmp** resource provides an indexed lookup table for fast random access to resources within IFF files. It's an optional optimization; not all IFF files include rsmp.

**rsmp Header (20 bytes):**
```
Offset 0-3:   Reserved (u32, big-endian, always 0)
Offset 4-7:   Version (u32, big-endian)
              0 = Sims 1 format
              1 = TSO format (extended resource IDs)
Offset 8-11:  Identifier "rsmp" (4 bytes, ASCII)
Offset 12-15: Total size (u32, big-endian, header + all entries)
Offset 16-19: Type count (u32, big-endian, number of resource types indexed)
```

**Version 0 (Sims 1) List Entry:**
```
Bytes 0-3:    File offset (u32, big-endian, byte position of resource)
Bytes 4-5:    Resource ID (u16, big-endian)
Bytes 6-7:    Flags (u16, big-endian)
Bytes 8+:     Name (null-terminated string, variable length)
```

**Version 1 (TSO) List Entry:**
```
Bytes 0-3:    File offset (u32, big-endian)
Bytes 4-7:    Resource ID (u32, big-endian) — extended to 32-bit for TSO
Bytes 8-9:    Flags (u16, big-endian)
Bytes 10:     String length (u8)
Bytes 11+:    Name (Pascal string, length specified, not null-terminated)
```

**rsmp Exclusions:**
The rsmp does not index:
- Itself (the rsmp resource)
- `XXXX` filler resources (dummy padding resources)

**Usage:**
Parsers can use rsmp for O(1) resource lookup by ID rather than scanning the entire file sequentially. This significantly improves loading performance for large IFF files with 100+ resources.

### Common Resource ID Ranges

By convention (not enforced), resource IDs follow patterns:

| Range | Typical Use |
|-------|-------------|
| `#0-127` | System/utility resources |
| `#128-255` | Object-specific low-level data (SLOT, GLOB, FWAV) |
| `#100-199` | Graphics (SPR2, DGRP, PALT) |
| `#129-130` | Animation strings, menu tables |
| `#301-306` | String tables (dialogs, named trees) |
| `#2000` | Catalog strings (CTSS), catalog thumbnail (BMP_) |
| `#4000+` | Small thought bubble bitmaps (BMP_) |
| `#4096+` | Behavior trees (BHAV) |
| `#6000+` | Large thought bubble bitmaps (BMP_) |

## Major Block Types Reference

### OBJD — Object Definition

The "birth certificate" of every object. Contains all metadata needed for the game to recognize and use the object.

**Structure:**
- **Size:** 216 bytes (version 138 format)
- **Fields:** ~100 fields total
- **Byte Order:** Little-endian (inside IFF resource data)

**Critical Fields:**
- **GUID** (Global Unique ID) — 32-bit hex identifier. **Must be unique across all installed content.** Conflicts cause objects to disappear from catalog. Generated from creator's "magic cookie".
- **Original GUID** — GUID of the object this was cloned from
- **Prices** — Initial price, sale price, depreciation rates, minimum value
- **Category Flags:**
  - Room flag (Kitchen, Bedroom, Bathroom, etc.)
  - Function flag (Seating, Surfaces, Electronics, etc.)
  - Subsort flag (HD+ subcategories within Functions)
  - Downtown/Vacation/Studio/Magic Town flags
- **Ratings** — Catalog display values (Fun, Comfort, Hunger, etc.)
- **Reference IDs:**
  - Base Graphic ID → Points to DGRP resource
  - Tree Table ID → Points to TTAB resource
  - Animation Table ID → Points to STR# animation resource
  - Catalog ID → Points to CTSS resource
  - Slots ID → Points to SLOT resource
  - Initial stack size, graphic resource IDs, state counts

**Multi-Tile Object Architecture:**

Objects spanning multiple tiles (e.g., beds, counters, large tables) require special OBJD structure:

- **Master OBJD** — `sub_index = 0xFFFF`
  - Contains shared settings: pricing, catalog information, master GUID
  - Controls the entire multi-tile object
  - Placed on the "origin" tile

- **Slave OBJDs** — Indexed by tile coordinates
  - Contains tile-specific data: positioning, rotation, slave GUID
  - Each slave has its own **unique GUID** (different from master)
  - References the master OBJD
  - Coordinates are relative to master tile

**Example:** A 2x1 bed needs:
1. Master OBJD at tile (0,0) with sub_index=0xFFFF and price=$3,000
2. Slave OBJD at tile (1,0) with coordinate-based sub_index and price=$0

All slaves must have unique GUIDs. If any slave GUID conflicts with other installed content, the entire multi-tile object fails to appear.

**Byte Positions in OBJD (for hex editing):**
- Byte 9: Build/Buy toggle (4=Buy, 8=Build)
- Byte 14: Disable flag (1=hidden from catalog)
- Bytes 14-15: GUID
- Byte 39: Room type
- Byte 40: Function
- Byte 69: Build type
- Byte 94: Function subsort (HD+)
- Byte 95: Downtown sort
- Byte 97: Vacation sort
- Byte 99: Old Town/Community sort
- Byte 103: Studio Town sort
- Byte 104: Magic Town sort

### BHAV — Behavior (SimAntics Code)

Contains executable behavior trees — the "programs" that make objects work. Each BHAV resource is one tree (subroutine).

**Structure:**
- **Header Size:** 12 bytes
- **Instruction Size:** 12 bytes per instruction
- **Max Instructions:** 1-253 per tree

**BHAV Header (12 bytes):**
```
Bytes 0-1:   Signature (u16, little-endian)
             0x8002 = standard format
             0x8000-0x8003 = version variants
Bytes 2-3:   Instruction count (u16, 1-253)
Bytes 4-5:   Parameter count (u16, 0-4 parameters accepted by tree)
Bytes 6-7:   Local variable count (u16, 0-12 local variables)
Bytes 8-11:  Reserved/flags (4 bytes)
```

**Instruction Format (12 bytes per line):**
```
Bytes 0-1:   Opcode (u16, little-endian)
             0x0000-0x003F = primitive opcodes
             0x0100+ = subroutine calls
Bytes 2-3:   Addresses (u16, little-endian)
             High byte = True outcome (line or exit code)
             Low byte = False outcome (line or exit code)
Bytes 4-11:  Operands (8 bytes, instruction-specific data)
             Interpreted as four u16 parameters or raw bytes
```

**Tree Structure:**
```
BHAV #4096 "init"
  Header: Declares #parameters, #local variables
  Line 0: [Function 2] Param1, Param2, Param3, Param4 → True:1, False:Error
  Line 1: [Function 36] ... → True:True, False:Error
```

**SimAntics Virtual Machine Architecture:**

The SimAntics VM executes BHAV subroutines through a multi-threaded, cooperative scheduler:
- **Execution Model:** Multi-threaded, cooperative (non-preemptive)
- **Time Scale:** 15 ticks per simulated minute
- **Flow Control:** Two-address system (True/False paths enable non-sequential execution)

**Subroutine Address Space:**
```
0x0000–0x003F:  64 primitive opcodes (built-in operations)
0x0100–0x0FFF:  Global subroutines (Global.iff, shared across all objects)
0x1000–0x1FFF:  Local subroutines (object's own BHAV trees)
0x2000+:        Semi-global subroutines (referenced via GLOB)
```

**Instruction Statistics (from analysis of all base game objects):**
- **Expression (opcode 2):** 57% of all instructions (55,088 of 95,487 total) — overwhelmingly dominant
- **Animation (opcode 44):** 5% of instructions
- **Sound (opcode 45):** 4% of instructions
- Other opcodes: 34% combined

The Expression instruction is the workhorse of SimAntics, handling all arithmetic, comparisons, assignments, and variable manipulation.

**Each line contains:**
- **Function number** (0-255) — Operation type:
  - 2 = Expression (math, comparisons, assignments)
  - 36 = Dialog (show message box)
  - 19 = Create new Sim
  - 31 = Find object by GUID
  - 28 = Run named tree in another object
  - 363 = Call global "Am I a child?"
  - Many more...
- **Four parameters** (8 bytes hex = 4 x 16-bit values decimal)
  - Meaning varies by function type
  - For Expression (function 2):
    - Param1: Left operand
    - Param2: Right operand
    - Param3: Operation/sign (equals, greater than, assign, etc.)
    - Param4: Data type flags
- **Two outcomes** (True/False)
  - Can be: line number, True (254), False (255), Error (253)

**Common BHAV Trees:**
- `#4096` "main" — Continuously runs while object exists. Iterates (last line loops back to start).
- `#4097` "init" — Runs once when object is placed. Sets up initial state.
- `#4100+` — User interactions (from menu clicks)
- `#4200+` — Test trees (determine if menu options should appear)

**Variables and Attributes:**
- **Local Variables** — Temporary storage private to current tree (Local 0, Local 1, etc.)
- **Temp Storage 0** — Shared between trees, acts as return value
- **My Attribute N** — Object's persistent properties (Attribute 0, 1, 2...)
- **Stack Object's Attribute N** — Properties of object currently being processed
- **My Person Data** — For Sim objects (age, gender, motives, etc.)

**Outcome Meanings:**
- **True (254)** — Tree succeeded, exit
- **False (255)** — Tree failed, exit
- **Error (253)** — Tree errored, exit (may cause re-initialization)
- **Line N** — Jump to line N, continue execution

### TTAB — Tree Table (Pie Menu)

Defines interactions that appear in the pie menu when clicking an object. Each entry describes one menu option.

**Fields per interaction:**
- **Menu String** — Text shown in menu (from TTAs block)
- **Action Tree** — BHAV tree number to run when selected
- **Test Tree** — BHAV tree number that determines if option appears
- **Available to:** Adult / Child / Visitor checkboxes
- **Advertisements:**
  - Promised motive changes (Hunger, Comfort, Hygiene, etc.)
  - Min/Max/Delta values
  - Personality weights (Neat, Outgoing, Active, etc.)
  - Attenuation (broadcast range: None, Low, Medium, High)
  - Attenuation Threshold (when Sims autonomously use object)

**Submenu Support:**
Prefix menu strings with "Category/" to create submenus:
- "Opening hours/Morning"
- "Opening hours/Evening"
- "Opening hours/Closed"
→ Creates "Opening hours" submenu with 3 options

**⚠️ Advertisement vs Actual Behavior:**

Important: **Objects often claim motive benefits they don't actually deliver.** The advertised motive deltas in TTAB (which influence autonomous behavior) may not match the actual motive changes implemented in BHAV code.

**Why This Happens:**
- Advertisements are design estimates made early in development
- Actual BHAV behavior may be tuned differently during testing
- Developers sometimes boost advertisements to make objects more appealing to autonomous Sims
- Balance changes affect BHAV code but TTAB ads may not be updated

**Impact:**
- Sims may autonomously choose objects based on inflated promises
- Actual satisfaction may be lower than advertised
- This is intentional game design, not a bug

**Example:** A chair may advertise +50 Comfort but actually provide +30 Comfort when used. Sims will still choose it autonomously based on the +50 advertisement.

### TTAs — Pie Menu Strings

Multi-language strings for TTAB menu options. **Must be in same order as TTAB entries.**

Each TTAs resource corresponds to one TTAB resource (same ID number). Contains strings in all supported languages.

### STR# — String Tables

Multi-purpose string storage. Different resource IDs serve different purposes:

**Animation Strings:**
- `#129` "a2o" — Adult-to-object animations
- `#130` "c2o" — Child-to-object animations
- `#128` "a2a" — Adult-to-adult
- `#131` "c2a" — Child-to-adult
- `#132` "a2c" — Adult-to-child
- `#133` "c2c" — Child-to-child

**Dialog & Code:**
- `#301` "Dialog prim string set" — Text for message boxes (function 36)
  - String #0 is referenced as parameter value 1
  - String #1 as parameter value 2, etc.
- `#303` "Named trees" — Names for remote BHAV calls (function 28)
- `#305` — Snapshot captions
- `#306` — Custom named trees (to avoid conflicts)

**Special Strings:**
- Body strings — Clothing/appearance data for Sims

### CTSS — Catalog Strings

Multi-language name and description for objects in Buy/Build catalog.

**Structure:**
- String #0 — Object name
- String #1 — Catalog description

Used at resource `#2000` typically (linked from OBJD "Catalog ID" field).

**Supports:**
- Multi-language (20+ languages)
- Variable substitution:
  - `$Me` — Sim's name
  - `$Local:N` — Value of local variable N
  - `$Temp:0` — Value of Temporary Storage 0

Also used for Sim bios (character descriptions in relationship panel).

### SPR2 — Sprite Data

Compressed sprite images used in-game. Contains:
- Pixel data (run-length encoded)
- Z-buffer (depth information for layering)
- Alpha channel (transparency)

Referenced by DGRP to build visual representations of objects at different angles and zoom levels.

### DGRP — Draw Group

Describes how to render an object using sprites from SPR2.

**Contains:**
- Sprite references (which SPR2 resource to use)
- Offsets (X, Y, Z positioning)
- Flags (flip horizontal, etc.)
- Zoom level (smallest, medium, largest)
- Rotation (front, back, left, right)

One object typically has multiple DGRP resources for different views and states.

### BMP_ — Bitmaps

Uncompressed bitmap images for:
- **Catalog thumbnails** (`#2000, #2001, ...`) — Grid view in Buy mode
- **Thought bubbles** — Appear when Sims think about objects:
  - Small bubbles: `#4000, #4001, ...`
  - Large bubbles: `#6000, #6001, ...`

**Numbering:**
- If IFF contains 1 object: `#2000`, `#4000`, `#6000`
- If IFF contains 2 objects: Add `#2001`, `#4001`, `#6001`
- And so on...

### SLOT — Slot Definition

Defines where Sims can stand relative to the object to interact with it. Determines:
- Valid approach angles
- Standing positions
- Routing paths
- Object boundaries

**Position Granularity:**
- **1/16-tile precision** — Slots positioned in 16ths of a tile
- Allows fine-grained positioning for tight spaces

**Slot Types (Statistical Distribution):**

Based on analysis of all base game objects:

- **Type 0 (Support Slots)** — 24.7% of all slots
  - Non-routing slots
  - Used for object placement, containment
  - Example: Surface of table, inside refrigerator

- **Type 1 (Specialized)** — 1.6% of all slots (rare)
  - Elevated positions
  - Special-purpose slots
  - Example: Top of stairs, elevated platforms

- **Type 3 (Routing)** — 73.5% of all slots (most common)
  - Character navigation and standing positions
  - Where Sims walk to and stand
  - Example: Front of stove, side of bed

**Slot Properties:**
- Position (X, Y, Z coordinates in 1/16-tile units)
- Preference weights: standing, sitting, lying
- Routing flags: facing direction, coordinate system behavior
- Height offset for elevated interactions

Complex objects (like tables) have elaborate SLOT definitions with 8+ slots. Simple objects (statues) have minimal slots (1-2).

### FWAV — Audio Event References

Maps sound event IDs to .XA sound files.

**Structure:**
- Event ID number
- Filename reference (sound file in `Sounds/` directory or FAR)
- Volume, pitch parameters

Example: "art_consider_vox" for the sound Sims make when admiring art.

### GLOB — Global References

Declares which global IFF files this object needs. The game loads these globals and makes their BHAV trees callable.

**Common globals:**
- `ArtGlobals.iff` — Behavior for decorative objects (view, react, depreciate)
- `PersonGlobals.iff` — Core Sim behavior (needs processing, aging, relationships)
- `PhoneGlobals.iff` — Phone interaction logic

Resource is typically `#128`. Just contains the filename string (e.g., "ArtGlobals.iff").

### OBJf — Object Function Table

Modern alternative to the old OBJD function table. Introduced in later expansions as a cleaner vtable-style entry point system.

**Structure:**
- **31-entry vtable** mapping standard functions to BHAV trees
- **Guard/Action pairs** for interaction validation

**Entry Format — Guard/Action Pairs:**

Each function type has two components:

1. **Guard Function** — Validates if interaction is available
   - Returns: True (available) or False (not available)
   - Example: "Can sit?" checks if chair is unoccupied

2. **Action Function** — Executes the interaction
   - Runs only if guard returned True
   - Example: "Sit" plays sitting animation and updates state

**Function Types (Entry Indices):**

**Lifecycle Events:**
- `init` — Initialization (run once on placement)
- `main` — Main loop (runs continuously while object exists)
- `cleanup` — Cleanup before deletion
- `load` — Called when lot loads from save
- `reset` — Reset object to default state

**Placement Events:**
- `placement` — Called when user places object from catalog
- `user_pickup` — Called when user moves object
- `room_changed` — Called when room boundaries change

**Interaction Events:**
- `sit` — Sit on object
- `cook` — Cook food
- `eat` — Eat food
- `prepare_food` — Food preparation
- Many more (31 total function slots)

**Environmental Events:**
- `wall_adjacency_changed` — Object touches/untouches wall
- `room_score_changed` — Room quality changes
- `on_portal` — Object placed on door/portal

**Example Mapping:**
```
OBJf Resource #256:
  init_guard → BHAV #4097 (always returns True)
  init_action → BHAV #4098 (setup code)
  main_guard → BHAV #4099 (always returns True)
  main_action → BHAV #4096 (main loop)
  sit_guard → BHAV #4150 (check if unoccupied)
  sit_action → BHAV #4151 (sitting interaction)
```

Set to specific BHAV tree numbers or 0 (unused slot).

### PALT — Color Palette

256-color palette (RGB values) used by sprites. Sprites reference palette index numbers, PALT converts to actual colors.

### BCON — Constants

Numeric constant values that BHAV code can reference. Allows centralized "tuning" values.

Example: Balloon lifespan, attraction radius, need change rates.

### CARR — Career System

CARR resources define career tracks with 10 levels per track. Found in career-specific IFF files, CARR blocks contain all information about job progression.

**Structure:**
- **10 levels per track** (Level 1 = entry, Level 10 = top position)
- **Variable-width bit-level compression** for data storage

**Each Career Level Specifies:**

**Skill Requirements:**
- Cooking (0-10)
- Mechanical (0-10)
- Charisma (0-10)
- Body (0-10)
- Logic (0-10)
- Creativity (0-10)
- Friendship points required

**Work Properties:**
- Daily salary ($)
- Work schedule (start time, end time, days per week)
- Vehicle type (carpool, taxi, limo, helicopter, etc.)
- Uniform specification with substitution codes:
  - `$g` — Gender (male/female)
  - `$b` — Body type (fat/fit/skinny)
  - `$c` — Skin color (lgt/med/drk)

**Motive Decay Rates:**
Specifies how quickly motives decay during work hours (different from normal decay):
- Energy decay rate (working is tiring)
- Social decay rate (varies by career)
- Fun decay rate (boring jobs decay faster)
- Etc.

**Career Tracks (Base Game):**
1. Business
2. Entertainment
3. Law Enforcement
4. Life of Crime
5. Medicine
6. Military
7. Politics
8. Pro Athlete
9. Science
10. Xtreme (X-treme)

Expansion packs add additional career tracks (e.g., Slacker, Paranormal, Journalism).

**Example:** Medicine Level 10 (Medical Researcher) requires:
- Body: 7
- Logic: 9
- Charisma: 4
- 8 friends
- Salary: $850/day
- Hours: 9:00 AM - 4:00 PM
- Vehicle: Helicopter
- Uniform: Lab coat variants

### FCNS — Function Constants

FCNS resources in `Global.iff` contain global tuning parameters that affect core game systems. These are distinct from per-object BCON values.

**Structure:**
- 16-byte header
- Variable number of entries
- Each entry contains:
  - Short description string
  - Floating-point value (f32)
  - Long description string

**Tuning Parameters Include:**
- Household starting funds ($20,000 default)
- Motive decay relationships (how motives affect each other)
- Skill gain rates (study efficiency)
- Advertisement volume settings (autonomous behavior sensitivity)
- Social interaction multipliers
- Need satisfaction curves

**Example FCNS Entries:**
- "StartingMoney" = 20000.0 — Initial household funds
- "SkillGainRate" = 1.0 — Multiplier for skill point accumulation
- "MotiveDecayRate" = 1.0 — Global motive decay speed

These values can be modified to create custom gameplay balance (e.g., harder mode with less starting money or faster motive decay).

### TPRP, TRCN — Tree Parameters/Constants

Define parameters and constants that behavior trees accept and use. Helps with tree reusability.

## SimAntics Programming Details

### Expression Syntax (Function 2)

The most common BHAV function. Format:
```
Param1 [Operator] Param2
```

**Operators** (Param3):
- `512` (0x0200) — Equals (==)
- `256` (0x0100) — Less than (<)
- `0` (0x0000) — Greater than (>)
- `768` (0x0300) — Subtract (-=)
- `1024` (0x0400) — Add (+=)
- `1280` (0x0500) — Assign (:=)
- `1536` (0x0600) — Divide (/=)
- `1792` (0x0700) — Multiply (*=)
- `2304` (0x0900) — Modulo (%=)
- Many more...

**Data Types** (Param4):
Common values (hex):
- `1792` (0x0700) — My [attribute]
- `1793` (0x0701) — Stack object's [attribute]
- `1795` (0x0703) — Literal constant
- `1798` (0x0706) — Global (from Simulation)
- `1807` (0x070F) — Stack object's motives
- `1810` (0x0712) — My person data
- `1811` (0x0713) — Stack object's person data
- `1817` (0x0719) — Local variable
- `4618` (0x120A) — My person data neighbor ID

### Dialog Syntax (Function 36)

Shows message boxes, Yes/No prompts, text entry, lot selection, phone books, etc.

**Parameters:**
1. Cancel button text (tri-choice dialogs)
2. Icon name (for param 8 values 6-9)
3. **Main text string** (required, from STR#301)
4. OK/Yes button text
5. No button text
6. **Dialog type** (0x00-0x0F):
   - 0x00 = Message (OK button)
   - 0x01 = Yes/No
   - 0x02 = Yes/No/Cancel
   - 0x03 = Text entry
   - 0x05 = Choose Downtown lot
   - 0x06 = Choose clothes (wardrobe)
   - 0x07 = Choose Vacation lot
   - 0x08 = Choose Community lot
   - 0x09 = Choose pet
   - 0x0A = Phone book
   - 0x0B = Choose Studio lot
   - 0x0C = Spellbook
   - 0x0D = Choose Magic lot
   - 0x0E = Choose Magic outfit
   - 0x0F = Cookbook
7. Title text string
8. **Icon type**:
   - 0 = Auto icon
   - 1 = Auto icon + pause game
   - 2 = No icon
   - 3 = No icon + pause game
   - 4 = Sim icon
   - 5 = Sim icon + pause game
   - 6 = BMP_ #5000+N (N from param 2)
   - 7 = BMP_ #5000+N + pause game
   - 8 = Icon by name (param 2 contains STR#301 string ID)
   - 9 = Icon by name + pause game

**Return Values:**
- Yes/OK → True
- No → False (Temp 0 = 0)
- Cancel → False (Temp 0 = 1)
- Lot selection → Temp 0 = lot number
- Phone book → Temp 0 = Sim GUID

### Variable Substitution in Strings

Strings in STR#301, CTSS, and other text blocks can use variables:

- `$Me` — Name of current Sim
- `$Neighbor` — Name of target Sim
- `$Local:0` — Value of Local Variable 0
- `$Local:1` — Value of Local Variable 1
- `$Temp:0` — Value of Temporary Storage 0
- `$$Local:0` — Monetary value (adds currency symbol)

Example: "Welcome home! You brought home $$Local:0 today."

### Version Detection

Check which expansion packs are installed:

```
Function 2
Param1: 14 (0x0E)
Param2: 3 (expansion pack number: 0=TS1, 1=LL, 2=HP, 3=HD, 4=Vac, 5=Deluxe, 6=UL, 7=SS, 8=MM)
Param3: 0 (operator: 0=>, 2===, 8=is flag set)
Param4: 1798 (0x0706) Global (from Simulation) Game Edition
```

Common checks:
- Is HD+ installed? → `Game Edition > 2`
- Is UL installed? → `Game Edition Flag Set? 6`

## Multi-Language String Handling

The Sims supports 20+ installation languages. All displayed text must have translations.

### String Encoding Formats

The Sims uses **five distinct string encoding formats** for multi-language text storage. Format detection uses the first two bytes:

**Format Detection (first two bytes determine format):**

1. **Simple Counted Format** (`≥ 0x0000`) — Basic format
   - Used when first two bytes form a valid count
   - Language strings stored sequentially

2. **Null-Terminated Format** (`0xFFFF`) — Legacy format
   - First two bytes = `0xFFFF`
   - Strings separated by null terminators

3. **Paired Strings Format** (`0xFEFF`) — Dual-language
   - First two bytes = `0xFEFF`
   - String pairs (e.g., name + description)

4. **Language-Coded Format** (`0xFDFF`) — Explicit language IDs
   - First two bytes = `0xFDFF`
   - Each string prefixed with language ID byte

5. **Dynamic Length Format** (`0xFCFF`) — TSO+ extended
   - First two bytes = `0xFCFF`
   - Variable-length encoding for large string sets
   - Used in TSO for expanded content

**Language Indexing:**
- Languages indexed 1-20 (not 0-19)
- Language 1 = US English (fallback language)
- If translation missing, game shows US English string or "*" error marker

### Language IDs (in order)

1. US English (fallback)
2. UK English
3. French
4. German
5. Italian
6. Spanish
7. Dutch
8. Danish
9. Swedish
10. Norwegian
11. Finnish
12. Portuguese
13. Japanese
14. Polish
15. Russian (Cyrillic)
16. Greek
17. Hebrew
18. Traditional Chinese
19. Simplified Chinese
20. Korean
21. Thai

**Important Notes:**
- IFF Pencil's "Set for all languages" skips the last 4 (Traditional Chinese, Simplified Chinese, Korean, Thai)
- These must be filled manually (copy/paste)
- If missing translations, game shows asterisk + English or error message
- Catalog descriptions especially important for international users

## Object Creation Workflow

To understand what IffExporter extracts, here's how objects are typically created:

1. **Clone existing object** (using T-Mog) → Generates new GUID
2. **Edit OBJD** → Set prices, categories, ratings, reference IDs
3. **Add/edit BHAV trees** → Write behavior code
4. **Create TTAB menu** → Define interactions
5. **Add TTAs strings** → Multi-language menu text
6. **Add CTSS strings** → Catalog name/description
7. **Import graphics** → SPR2, DGRP, PALT, BMP_ blocks
8. **Add SLOT** → Interaction positions
9. **Set OBJf** → Link init/main to BHAV numbers
10. **Test in game** → Iterate until working

IffExporter helps with steps 2-9 by showing the structure in readable JSON.

## IFFSnooper Workflows

**IFFSnooper** is a community-developed IFF editing tool. Understanding its capabilities helps when working with IFF files:

### Editing Capabilities by Resource Type

**Full Editing Support:**
- OBJD (Object Definition) — All fields editable
- CTSS (Catalog Strings) — Multi-language name/description
- STR# (String Tables) — All string tables
- TTAs (Pie Menu Strings) — Menu text
- CARR (Career System) — Career level data

**Partial Editing Support:**
- TTAB (Tree Table / Pie Menu) — Flags and motive advertisements only
  - Cannot edit BHAV tree references (view-only)
  - Can edit: autonomy flags, attenuation, motive deltas

**Read-Only (View Only):**
- BHAV (Behavior) — View bytecode but cannot edit
- SLOT (Slot Definition) — View positions but cannot edit
- TREE — View tree structure
- OBJf (Object Functions) — View function table

**Import/Export Support:**
- Sprites (SPR2, SPR#) — Export to BMP, import from BMP
- Resources — Export individual resources, import replacements
- Full disassembly — Export entire IFF structure to folder

### Sprite System Architecture

IFFSnooper works with a three-channel sprite system:

**1. P-Sprite (RGB Color Channel)**
- Visible pixel colors
- Standard RGB bitmap
- File naming: `{object}_large_ne_p.bmp`

**2. Z-Buffer (Depth Channel)**
- Depth mapping for layering
- Dark values (0-50) = closer to camera = drawn in front
- Bright values (200-255) = farther from camera = drawn behind
- File naming: `{object}_large_ne_z.bmp`

**3. Alpha Channel (Transparency)**
- Controls pixel opacity
- 0 = fully transparent (invisible)
- 255 = fully opaque (solid)
- Partial transparency: 1-254 (used for glass, shadows, smoke)
- File naming: `{object}_large_ne_a.bmp`

**Sprite Naming Convention:**
`{object}_{size}_{direction}_{channel}.bmp`
- Size: `small`, `medium`, `large` (zoom levels)
- Direction: `ne`, `nw`, `se`, `sw` (rotations)
- Channel: `p` (color), `z` (depth), `a` (alpha)

Example: `chair_large_ne_p.bmp` = chair, largest zoom, northeast view, color channel

### Object Creation Workflow (Disassemble → Modify → Reassemble)

IFFSnooper's primary workflow for creating custom objects:

**Step 1: Find Similar Object**
Locate an existing IFF file that's similar to what you want to create (use as template).

**Step 2: Disassemble**
- File → Export → Disassemble
- Exports entire IFF structure to a folder
- Creates separate files for each resource type
- Sprites exported as BMP images (P/Z/Alpha channels)

**Step 3: Modify Resources**
Edit the disassembled files:
- OBJD: Change prices, categories, GUID
- Sprites: Edit P/Z/Alpha BMP files in image editor (Photoshop, GIMP)
- CTSS: Update name and description
- TTAs: Change menu text
- BHAV: View-only, cannot edit directly

**Step 4: Reassemble**
- File → Import → Assemble or Import → Resources
- Reconstructs IFF from modified files
- Sprite encoding: Automatically generates SPR2 + PALT from P/Z/Alpha BMPs

**Step 5: Test In-Game**
- Place modified IFF in `Downloads/`
- Load game and verify object appears correctly

### GUID Management

**Automatic GUID Handling (IFFSnooper 1.1.3+):**
- Clone operation automatically generates new GUIDs
- Updates BHAV references to match new GUIDs
- Updates multi-tile slave GUIDs

**Manual GUID Editing:**
For older versions or manual control:
- Edit OBJD GUID field directly
- For multi-tile objects: Edit master + all slave GUIDs
- GUID format: 32-bit hex (e.g., `0x12345678`)
- Middle 2 bytes = "magic cookie" (creator identifier)

**Critical Rule:** Every object and every slave tile must have a unique GUID across all installed content. Conflicts cause objects to vanish from catalog.

### Minimum Required Resources

Different object types require different minimum resource sets:

**Basic Decorative Object:**
- OBJD (object definition)
- DGRP (draw group)
- SPR2 (sprites)
- PALT (palette)
- CTSS (catalog strings)

**Interactive Object:**
- All basic resources, plus:
- TTAB (pie menu interactions)
- TTAs (menu text)
- BHAV trees (behavior code)
- SLOT (standing positions)

**Multi-Tile Object:**
- All interactive resources, plus:
- Multiple OBJDs (master + slaves with unique GUIDs)

**Character (Sim):**
- OBJD (type=2, person)
- STR# #200 (body strings)
- CTSS (name and bio)
- BMP_ (portraits #2002-2007)
- BHAV #4096+ (behavior)

## Character Data Structure (Sims)

Character IFF files (`user000XX.iff`) and `neighborhood.iff` contain Sim data.

### Character IFF (user000XX.iff)

**CTSS Block:**
- String #0 — Sim's first name
- String #1 — Bio text

**STR# Block - Bodystring (String #200):**
Contains appearance data with numbered sub-strings:
- #1: Skeleton (adult/child mesh)
- #2: CMX/skin for normal clothes
- #3: Head CMX/skin
- #12: Gender (male/female)
- #13: Age (27 for adult, 9 for child)
- #14: Skin tone (lgt/med/drk)
- #15: Nude CMX/skin
- #16: Underwear/swimsuit CMX/skin
- #17-18: Open hand CMX/skin
- #19-20: Pointing hand CMX/skin
- #21-22: Closed hand CMX/skin
- #27: Formal CMX/skin
- #28-29: Formal gloves (open hand only)
- #30: Buyable formal (HD+)
- #31: Buyable swimsuit (HD+)
- #32: Buyable lingerie (HD+)
- #33: Buyable winter wear (Vacation+)
- #34: Buyable high fashion (SS+)

### Neighborhood.iff Character Data

Located by searching for `user000XX.iff` string, then counting bytes:

**Stats Block** (starts at byte 14 after character IFF name, 34 bytes total):
- **Personality Traits** (0-10 scale, hex-encoded):
  - Bytes 14-15: Nice
  - Bytes 16-17: Active
  - Bytes 20-21: Playful
  - Bytes 22-23: Outgoing
  - Bytes 24-25: Neat
- **Skills** (0-10 scale, hex-encoded):
  - Bytes 30-31: Cooking
  - Bytes 32-33: Charisma
  - Bytes 34-35: Mechanical
  - Bytes 36-37: Exercise (UL+)
  - Bytes 38-39: Food (UL+)
  - Bytes 40-41: Creativity
  - Bytes 42-43: Parties (UL+)
  - Bytes 44-45: Body
  - Bytes 46-47: Logic
  - Bytes 50-51: Style (UL+)

**Interests Block** (starts 54 bytes after stats, 20 bytes total):
- **Pre-HD (0-20 scale):**
  - Politics, Weather, Sports, Leisure, Sci-Fi, Travel, Flower Power, Babies, Beauty, Spooky Stuff
- **HD+ (0-10 scale, hex-encoded):**
  - Travel, Money, Politics, 60's, Weather, Sports, Music, Outdoors, Technology, Romance
  - Additional interests in stats block: Exercise, Food, Parties, Style, Hollywood (byte 15-16 of the 54-byte gap)

**Career Data** (immediately after interests):
- Bytes 0-1: Career number (FF FF = child, 00 00 = unemployed, 01 00 = business, etc.)
- Bytes 2-3: Career level/grade (00 00 = highest, lower number = better grade)

**Age Data** (byte 125 from character IFF name):
- Bytes 0-1: Age (09 00 = child/9, 1B 00 = adult/27)

**Gender Data** (byte 140 from character IFF name):
- Bytes 0-1: Gender (00 00 = male, 01 00 = female, 02 00 = cat, 03 00 = dog)

**Skin Tone Data** (byte 127-128 from character IFF name):
- 01 00 = Light
- 02 00 = Medium
- 03 00 = Dark

**Fame Score** (byte 268 from character IFF name, SS+ only):
- Byte value: 0-0A (0-10, hex-encoded)

### Hex Encoding for Stats/Skills/Interests

Values 0-10 are encoded as double bytes (little-endian):
```
0  = 00 00    6  = 58 02
1  = 64 00    7  = BC 02
2  = C8 00    8  = 20 03
3  = 2C 01    9  = 84 03
4  = 90 01    10 = E8 03
5  = F4 01
```

Intermediate values indicate skill-building in progress when saved.

### Motives (Runtime Values)

Motives are **not stored in IFF files** — they're runtime values manipulated by BHAV code during gameplay.

**VMMotive ID Mapping (FreeSO/TSOClient/tso.simantics/Model/VMMotive.cs):**
```
ID  Name              Notes
--  ----------------  ----------------------------------------
0   HappyLife         (TSO/internal)
1   HappyWeek         (TSO/internal)
2   HappyDay          (TSO/internal)
3   Mood              (Internal composite)
4   UnusedPhysical    (Unused)
5   Energy            Sleep/rest need ⭐
6   Comfort           Physical comfort need ⭐
7   Hunger            Food need ⭐
8   Hygiene           Cleanliness need ⭐
9   Bladder           Bathroom need ⭐
10  UnusedMental      (Unused)
11  SleepState        (Internal sleep tracker)
12  UnusedStress      (Unused)
13  Room              Environment quality score ⭐
14  Social            Social interaction need ⭐
15  Fun               Entertainment/pleasure need ⭐
```

**The 8 Core Player-Facing Motives (⭐):**
- **Energy (5), Comfort (6), Hunger (7), Hygiene (8), Bladder (9), Room (13), Social (14), Fun (15)**

**BHAV Expression Access** (opcode 2):
- **Parameter 4** = `1807` (hex `0F 07`) for "Stack object's motives"
- **Parameter 1** = Motive ID (0-15 from table above)
- **Parameter 2** = Value to change by (or assign)
- **Parameter 3** = Operator (common values):
  - `768` (0x0300) = Decrease (-=)
  - `800` (0x0320) = Increase variant (+=)
  - `1024` (0x0400) = Increase (+=)
  - `1280` (0x0500) = Assign (:=)

**Value Ranges:**
- Motive values range -100 to 100 (normally)
- Can exceed 100 with "overfill" on special lot types (TSO feature)
- Objects also advertise motive changes in TTAB blocks to influence autonomous behavior

### Practical Examples: Reading Motive Modifications in IFF Files

When you see BHAV Expression instructions in IFF Pencil or IffExporter JSON, here's how to decode motive modifications:

#### Example 1: Assign Bladder to 100
```
IFF Pencil display:
  Function: Expression
  Operand: 09 00 64 00 00 05 0F 07

IffExporter JSON:
  "opcode": 2,
  "opcodeName": "Expression",
  "operand": "09 00 64 00 00 05 0F 07"

Breaking down the operand (4 double-byte parameters, little-endian):
  Param1: 09 00 → 0x0009 = 9        (Motive ID: Bladder)
  Param2: 64 00 → 0x0064 = 100      (Value: 100)
  Param3: 00 05 → 0x0500 = 1280     (Operator: Assign :=)
  Param4: 0F 07 → 0x070F = 1807     (Data type: Stack object's motives)

Translation: Bladder := 100
(Sets Sim's Bladder motive to maximum value)
```

#### Example 2: Increase Energy by 1
```
IFF Pencil display:
  Function: Expression
  Operand: 05 00 01 00 20 03 0F 07

IffExporter JSON:
  "opcode": 2,
  "opcodeName": "Expression",
  "operand": "05 00 01 00 20 03 0F 07"

Breaking down the operand:
  Param1: 05 00 → 0x0005 = 5        (Motive ID: Energy)
  Param2: 01 00 → 0x0001 = 1        (Value: 1)
  Param3: 20 03 → 0x0320 = 800      (Operator: Increase variant +=)
  Param4: 0F 07 → 0x070F = 1807     (Data type: Stack object's motives)

Translation: Energy += 1
(Increases Sim's Energy motive by 1 point)
```

#### Example 3: Increase Fun by 1
```
IFF Pencil display:
  Function: Expression
  Operand: 0F 00 01 00 20 03 0F 07

IffExporter JSON:
  "opcode": 2,
  "opcodeName": "Expression",
  "operand": "0F 00 01 00 20 03 0F 07"

Breaking down the operand:
  Param1: 0F 00 → 0x000F = 15       (Motive ID: Fun)
  Param2: 01 00 → 0x0001 = 1        (Value: 1)
  Param3: 20 03 → 0x0320 = 800      (Operator: Increase variant +=)
  Param4: 0F 07 → 0x070F = 1807     (Data type: Stack object's motives)

Translation: Fun += 1
(Increases Sim's Fun motive by 1 point)
```

### Alternative: SetMotiveChange (Opcode 29)

Some objects use a different mechanism: **SetMotiveChange** instead of direct Expression modifications. This sets up motive *deltas* (rates of change over time) rather than instant modifications.

#### Example 4: Toilet - SetMotiveChange
```
IFF Pencil display:
  Function: SetMotiveChange
  Opcode: 29
  Operand: 1A 1A 09 00 82 00 83 00

IffExporter JSON:
  "opcode": 29,
  "opcodeName": "SetMotiveChange",
  "operand": "1A 1A 09 00 82 00 83 00"

Breaking down the operand (different format than Expression):
  Param1: 1A 1A → Advertisement strength parameters
  Param2: 09 00 → 0x0009 = 9        (Motive ID: Bladder)
  Param3: 82 00 → 0x0082 = 130      (BCON reference for rate)
  Param4: 83 00 → 0x0083 = 131      (BCON reference for target)

How it works:
- Toilets don't instantly change Bladder motive
- Instead, they set up a gradual change over the interaction duration
- The rate and target values come from BCON (constants) blocks
- This is why using a toilet takes time and you see the motive bar fill gradually

Multiple SetMotiveChange calls in sequence:
- Line 1-3: Set Bladder (09) changes at different rates
- Line 4: Set Comfort (06) changes

This approach is common for objects with duration-based interactions (toilets,
showers, beds, bathtubs) where motives change gradually during use, rather than
instant modifications like environmental effects (plants, TVs).
```

## IFF Exporter Usage

The IffExporter in this directory reads binary IFF files and exports them to JSON for human/LLM readability.

**Export Levels:**
- Level 0 (Summary) — Just block types and resource IDs
- Level 1 (Metadata) — + resource names and sizes
- Level 2 (Full) — + full structured data (strings, OBJD fields, etc.)
- Level 3 (Deep) — + binary data (sprites, z-buffers) as base64/external

**Binary Handling Modes:**
- Skip — Don't export binary data
- Checksum — Export SHA256 hash only
- Base64 — Inline binary as base64 string
- External — Save binary to separate files

**Filtering:**
- By chunk type: `-c BHAV,OBJD,STR` (only these blocks)
- By resource ID: Not currently supported

See [README.md](README.md) for command-line usage.
