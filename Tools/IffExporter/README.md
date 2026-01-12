# IFF Exporter

A command-line tool to export binary IFF (Interchange File Format) files used by The Sims and FreeSO/Simitone to human-readable and LLM-friendly JSON format.

## Overview

IFF files store game data in a binary chunk-based format. This tool reads these binary files and exports them as structured JSON, making the data accessible for:

- **Debugging** - Inspect object definitions, behaviors, and strings without opening the IDE
- **LLM Analysis** - Feed structured data to language models for understanding game mechanics
- **Documentation** - Generate human-readable documentation of game objects
- **Modding** - Understand existing objects to create new ones
- **Version Control** - Text-based diff-friendly format for tracking changes

## Features

- ✅ Supports all 44+ IFF chunk types (BHAV, OBJD, STR#, SPR2, DGRP, etc.)
- ✅ Three export levels: metadata, full, and deep
- ✅ Four binary data modes: skip, checksum, base64, external
- ✅ BHAV disassembly with opcode names
- ✅ Multi-language string support (20+ languages)
- ✅ Chunk filtering by type and ID
- ✅ Pretty-printed JSON output
- ✅ Summary mode for quick inspection

## Installation

### Prerequisites

- .NET 9.0 SDK

### Building

```bash
cd /workspaces/Simitone/Tools/IffExporter
dotnet build
```

## Usage

### Basic Usage

```bash
# Export with defaults (Level 2, checksum binary mode)
dotnet run -- myobject.iff

# Show summary only (no file output)
dotnet run -- myobject.iff --summary

# Specify output file
dotnet run -- myobject.iff -o output.json

# Verbose output
dotnet run -- myobject.iff -v
```

### Export Levels

#### Level 1: Metadata Only
Fast inspection of chunk inventory without parsing chunk data.

```bash
dotnet run -- myobject.iff -l 1
```

**Output includes:**
- File header and version
- Chunk count and types
- Chunk IDs, labels, sizes

**Use case:** Quick file inspection, understanding file structure

#### Level 2: Full Structured Export (Default)
Complete parsing of all chunk properties and data.

```bash
dotnet run -- myobject.iff -l 2
```

**Output includes:**
- All chunk properties via reflection
- BHAV bytecode with opcode names
- Multi-language strings
- Object definitions with all 80+ fields
- Binary data as checksums (by default)

**Use case:** Debugging, LLM analysis, understanding object behavior

#### Level 3: Deep Export
Level 2 plus full binary data extraction.

```bash
dotnet run -- myobject.iff -l 3
```

**Output includes:**
- Everything from Level 2
- Full sprite pixel data (size varies)
- Z-buffer and alpha channel data
- Complete palette arrays

**Use case:** Complete extraction, asset ripping

### Binary Data Handling

#### Skip Mode
Omit all binary data from output (smallest files).

```bash
dotnet run -- myobject.iff -b skip
```

#### Checksum Mode (Default, Recommended)
Include SHA256 checksums of binary data with metadata.

```bash
dotnet run -- myobject.iff -b checksum
```

**Output example:**
```json
"pixelData": {
  "checksum": "a3b2c1d4...",
  "size": 2048,
  "width": 64,
  "height": 64,
  "paletteId": 1
}
```

#### Base64 Mode
Embed binary data as base64 strings (very large files).

```bash
dotnet run -- myobject.iff -b base64
```

#### External Mode
Save binary data to separate files.

```bash
dotnet run -- myobject.iff -b external --output-dir ./extracted
```

### Filtering

#### Filter by Chunk Type
Export only specific chunk types.

```bash
# Export only behaviors and strings
dotnet run -- myobject.iff -c BHAV,STR

# Export only object definitions
dotnet run -- myobject.iff -c OBJD
```

#### Filter by Chunk ID
Export only specific chunk IDs.

```bash
dotnet run -- myobject.iff -i 4096,4097,4098
```

### Advanced Options

```bash
# Disable BHAV opcode name resolution (faster)
dotnet run -- myobject.iff --no-opcode-names

# Export only primary language (English US) from STR chunks
dotnet run -- myobject.iff --primary-language-only

# Combine options
dotnet run -- myobject.iff -l 2 -b checksum -c BHAV,OBJD -v -o output.json
```

## Command-Line Reference

```
Usage: IffExporter <input.iff> [options]

Options:
  -o, --output <file>          Output file (default: input.json)
  -l, --level <1|2|3>          Export level (default: 2)
                               1 = Metadata only
                               2 = Full structured export
                               3 = Deep with binary extraction
  -b, --binary <mode>          Binary handling (default: checksum)
                               skip | checksum | base64 | external
  -c, --chunks <types>         Filter chunk types (comma-separated)
                               Example: BHAV,STR,OBJD
  -i, --ids <ids>              Filter chunk IDs (comma-separated)
                               Example: 4096,4097
  -p, --pretty                 Pretty-print JSON (default: true)
  -v, --verbose                Verbose output
  --summary                    Print summary only (no file output)
  --no-opcode-names            Skip BHAV opcode name resolution
  --primary-language-only      Export only primary language (English US)
  --output-dir <dir>           Directory for external files
  -h, --help                   Show help

Examples:
  IffExporter chair.iff
  IffExporter chair.iff -l 1
  IffExporter chair.iff -b external -o chair.json
  IffExporter chair.iff -c BHAV,STR --summary
```

## Output Format

### File Structure

```json
{
  "file": "object.iff",
  "version": "2.5",
  "exportLevel": "Full",
  "exportDate": "2026-01-12T17:00:00Z",
  "chunkCount": 45,
  "chunks": {
    "OBJD": [ /* Object definitions */ ],
    "BHAV": [ /* Behavior bytecode */ ],
    "STR#": [ /* String tables */ ],
    "SPR2": [ /* Sprites */ ],
    "DGRP": [ /* Drawing groups */ ]
  }
}
```

### Chunk Examples

#### OBJD (Object Definition)
```json
{
  "chunkId": 128,
  "chunkLabel": "Dining Chair",
  "chunkType": "OBJD",
  "objectVersion": 142,
  "GUID": 1600211886,
  "price": 80,
  "objectType": "Normal",
  "baseGraphicID": 129,
  "ratings": { "comfort": 7, "fun": 0 }
}
```

#### BHAV (Behavior - Disassembled)
```json
{
  "chunkId": 4096,
  "chunkLabel": "Main",
  "chunkType": "BHAV",
  "instructionCount": 2,
  "instructions": [
    {
      "index": 0,
      "opcode": 2,
      "opcodeName": "Expression",
      "truePointer": 1,
      "falsePointer": 0,
      "operand": "0B 00 FF FF 00 05 00 07",
      "operandBytes": "CwD//wAFAAc="
    }
  ]
}
```

#### STR# (String Table)
```json
{
  "chunkId": 0,
  "chunkLabel": "Object Names",
  "chunkType": "STR#",
  "length": 2,
  "languageSets": [
    {
      "languageCode": 1,
      "language": "English (US)",
      "strings": [
        { "value": "Dining Chair" },
        { "value": "A comfortable place to sit." }
      ]
    }
  ]
}
```

## Supported Chunk Types

- **BHAV** - Behavior (SimAntics bytecode)
- **OBJD** - Object Definition
- **STR#** - String Table
- **SPR2** - Sprite (paletted with z-buffer/alpha)
- **DGRP** - Drawing Group
- **TTAB** - Tree Table (interactions)
- **BCON** - Behavior Constants
- **SLOT** - Slot Definitions
- **PALT** - Color Palettes
- **FWAV** - Sound Events
- **GLOB** - Semi-Global References
- **TPRP** - Tree Properties
- **OBJf** - Object Functions
- **CTSS** - Catalog Strings
- **TTAs** - Tree Table Attributes
- **BMP_** - Bitmap Images
- **PNG_** - PNG Images
- **THMB** - Thumbnails
- And 25+ more...

## Technical Details

### Architecture

The tool leverages the existing FreeSO `tso.files` library for binary IFF parsing, then:

1. **Reflection-based extraction** - Automatically extracts all public properties from parsed chunks
2. **Chunk-specific processors** - Special handling for complex chunks (BHAV, STR, OBJD, SPR2)
3. **Binary handler strategy** - Pluggable binary data handling (skip/checksum/base64/external)
4. **JSON serialization** - Uses Newtonsoft.Json for output

### Dependencies

- **FSO.Files** - IFF file parsing (from FreeSO submodule)
- **FSO.Common** - Common utilities (from FreeSO submodule)
- **Newtonsoft.Json** - JSON serialization

### Performance

- **Small objects** (~20 chunks): < 1 second
- **Large objects** (~100+ chunks): 1-5 seconds
- **Sprite files** (graphics-heavy): Varies by binary mode

## Use Cases

### For Developers
```bash
# Quick inspection of an object
dotnet run -- mystery_object.iff --summary

# Debug behavior code
dotnet run -- object.iff -c BHAV -o behaviors.json

# Extract all strings for translation
dotnet run -- object.iff -c STR -o strings.json
```

### For LLMs
```bash
# Export for LLM analysis (recommended settings)
dotnet run -- object.iff -l 2 -b checksum -o object.json

# Then copy object.json contents to Claude/ChatGPT for analysis
```

### For Modding
```bash
# Document an existing object
dotnet run -- base_object.iff -o documentation.json

# Compare two versions
dotnet run -- object_v1.iff -o v1.json
dotnet run -- object_v2.iff -o v2.json
diff v1.json v2.json
```

## Troubleshooting

### "Invalid IFF file" Error
- Ensure the file is a valid IFF file (starts with "IFF FILE 2.5" or "IFF FILE 2.0")
- Check file is not corrupted

### Large Output Files
- Use `-b checksum` instead of `-b base64` for sprite-heavy files
- Use `-l 1` for metadata-only inspection
- Filter specific chunks with `-c` to reduce output

### Missing Opcode Names
- Some BHAV opcodes may show as "Unknown_XXX" if not in the registry
- Subroutines (opcode >= 256) show as "SubRoutine_XXXX"

## Contributing

This tool is part of the Simitone project. Issues and improvements welcome!

## License

Same license as Simitone/FreeSO project.

## Credits

- Built on top of the excellent FreeSO `tso.files` IFF parsing library
- Developed as a debugging and documentation tool for the Simitone community
