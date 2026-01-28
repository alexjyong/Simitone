# IFF Exporter - Detailed IFF Format Guide

This document provides comprehensive information about The Sims IFF (Interchange File Format) file structure, based on community documentation and object hacking guides. This information helps understand what the IffExporter is extracting and how to interpret the exported JSON.

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

**Critical Fields:**
- **GUID** (Global Unique ID) — 32-bit hex identifier. Must be unique across all objects. Generated from creator's "magic cookie".
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

**Tree Structure:**
```
BHAV #4096 "init"
  Header: Declares #parameters, #local variables
  Line 0: [Function 2] Param1, Param2, Param3, Param4 → True:1, False:Error
  Line 1: [Function 36] ... → True:True, False:Error
```

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

Complex objects (like tables) have elaborate SLOT definitions. Simple objects (statues) have minimal slots.

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

Modern alternative to the old OBJD function table. Maps standard actions to BHAV trees:

- **init** — Initialization (run once on placement)
- **main** — Main loop (runs continuously)
- **cleanup** — Clean up before deletion
- **placement** — Called when user places object
- **user_pickup** — Called when user moves object
- **load** — Called when lot loads
- **room_changed** — Called when room score changes
- Many more...

Set to specific BHAV tree numbers (e.g., init → 4097, main → 4096).

### PALT — Color Palette

256-color palette (RGB values) used by sprites. Sprites reference palette index numbers, PALT converts to actual colors.

### BCON — Constants

Numeric constant values that BHAV code can reference. Allows centralized "tuning" values.

Example: Balloon lifespan, attraction radius, need change rates.

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

**Language IDs (in order):**
1. US English
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