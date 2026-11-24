#!/bin/bash
# Build script for Simitone on Linux/macOS/WSL
# Usage: ./build-mac-linux.sh [debug|release] [--publish]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Default values
CONFIG="Release"
PUBLISH=""

# Parse arguments (order-independent)
for arg in "$@"; do
    case "$arg" in
        --publish)
            PUBLISH="--publish"
            ;;
        debug|Debug)
            CONFIG="Debug"
            ;;
        release|Release)
            CONFIG="Release"
            ;;
    esac
done

echo "========================================"
echo "  Simitone Linux/macOS Build Script"
echo "========================================"
echo ""
echo "Configuration: $CONFIG"
if [[ "$PUBLISH" == "--publish" ]]; then
    echo "Mode: Publish (self-contained with launcher)"
fi
echo ""

# Detect package manager
detect_package_manager() {
    if command -v apt-get &> /dev/null; then
        echo "apt"
    elif command -v dnf &> /dev/null; then
        echo "dnf"
    elif command -v pacman &> /dev/null; then
        echo "pacman"
    elif command -v brew &> /dev/null; then
        echo "brew"
    else
        echo "unknown"
    fi
}

# Update package manager index (run once before installations)
update_package_index() {
    local pm=$(detect_package_manager)
    
    case "$pm" in
        apt)
            echo "Updating package index..."
            sudo apt-get update
            ;;
        dnf)
            echo "Updating package index..."
            sudo dnf check-update || true
            ;;
        pacman)
            echo "Updating package index..."
            sudo pacman -Sy
            ;;
        brew)
            echo "Updating package index..."
            brew update
            ;;
    esac
}

# Install a package using the detected package manager
install_package() {
    local pkg_apt="$1"
    local pkg_dnf="$2"
    local pkg_pacman="$3"
    local pkg_brew="$4"
    local pm=$(detect_package_manager)
    
    case "$pm" in
        apt)
            echo "Installing $pkg_apt..."
            sudo apt-get install -y "$pkg_apt"
            ;;
        dnf)
            echo "Installing $pkg_dnf..."
            sudo dnf install -y "$pkg_dnf"
            ;;
        pacman)
            echo "Installing $pkg_pacman..."
            sudo pacman -S --noconfirm "$pkg_pacman"
            ;;
        brew)
            echo "Installing $pkg_brew..."
            brew install "$pkg_brew"
            ;;
        *)
            echo "Unknown package manager. Please install manually."
            return 1
            ;;
    esac
}

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found."
    echo ""
    
    if [[ "$(uname)" == "Darwin" ]]; then
        echo "Install .NET 9.0 SDK with:"
        echo "  brew install dotnet-sdk"
        read -p "Install now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            brew install dotnet-sdk
        else
            exit 1
        fi
    else
        echo "Install .NET 9.0 SDK using the official Microsoft install script."
        echo ""
        read -p "Would you like to install now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            echo "Downloading and running Microsoft .NET install script..."
            curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
            chmod +x /tmp/dotnet-install.sh
            /tmp/dotnet-install.sh --channel 9.0 --install-dir "$HOME/.dotnet"
            rm /tmp/dotnet-install.sh
            
            # Add to PATH for this session
            export DOTNET_ROOT="$HOME/.dotnet"
            export PATH="$DOTNET_ROOT:$PATH"
            
            echo ""
            echo "NOTE: To make .NET 9.0 permanent, add these lines to your ~/.bashrc or ~/.profile:"
            echo "  export DOTNET_ROOT=\"\$HOME/.dotnet\""
            echo "  export PATH=\"\$DOTNET_ROOT:\$PATH\""
            echo ""
        else
            exit 1
        fi
    fi
fi

# Check .NET SDK version (need 9.0+)
DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d'.' -f1)
if [[ "$DOTNET_VERSION" -lt 9 ]]; then
    echo "WARNING: .NET SDK 9.0 or higher is required (found: $(dotnet --version))"
    echo ""
    
    if [[ "$(uname)" == "Darwin" ]]; then
        echo "Update with: brew upgrade dotnet-sdk"
        read -p "Would you like to update now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            brew upgrade dotnet-sdk || brew install dotnet-sdk
        else
            exit 1
        fi
    else
        echo "Install .NET 9.0 SDK using the official Microsoft install script."
        echo ""
        read -p "Would you like to install now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            echo "Downloading and running Microsoft .NET install script..."
            curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
            chmod +x /tmp/dotnet-install.sh
            /tmp/dotnet-install.sh --channel 9.0 --install-dir "$HOME/.dotnet"
            rm /tmp/dotnet-install.sh
            
            # Add to PATH for this session
            export DOTNET_ROOT="$HOME/.dotnet"
            export PATH="$DOTNET_ROOT:$PATH"
            
            echo ""
            echo "NOTE: To make .NET 9.0 permanent, add these lines to your ~/.bashrc or ~/.profile:"
            echo "  export DOTNET_ROOT=\"\$HOME/.dotnet\""
            echo "  export PATH=\"\$DOTNET_ROOT:\$PATH\""
            echo ""
        else
            exit 1
        fi
    fi
    
    # Verify installation
    DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d'.' -f1)
    if [[ "$DOTNET_VERSION" -lt 9 ]]; then
        echo "ERROR: Failed to install .NET 9.0 SDK"
        exit 1
    fi
fi

echo "Using .NET SDK: $(dotnet --version)"
echo ""

# Check and install dependencies on Linux
if [[ "$(uname)" == "Linux" ]]; then
    MISSING_DEPS=()
    
    # Check for SDL2 (required for MonoGame)
    if ! ldconfig -p | grep -q libSDL2-2.0.so.0; then
        MISSING_DEPS+=("libsdl2")
    else
        echo "✓ SDL2 found"
    fi
    
    # Check for OpenAL (required for audio)
    if ! ldconfig -p | grep -q libopenal; then
        MISSING_DEPS+=("libopenal")
    else
        echo "✓ OpenAL found"
    fi
    
    # Check for GTK3 (required for Eto.Forms GUI)
    if ! ldconfig -p | grep -q libgtk-3.so.0; then
        MISSING_DEPS+=("libgtk3")
    else
        echo "✓ GTK3 found"
    fi
    
    # Offer to install missing dependencies
    if [[ ${#MISSING_DEPS[@]} -gt 0 ]]; then
        echo ""
        echo "Missing dependencies: ${MISSING_DEPS[*]}"
        echo ""
        read -p "Would you like to install them now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            update_package_index
            for dep in "${MISSING_DEPS[@]}"; do
                case "$dep" in
                    libsdl2)
                        install_package "libsdl2-2.0-0" "SDL2" "sdl2" "sdl2"
                        ;;
                    libopenal)
                        install_package "libopenal1" "openal-soft" "openal" "openal-soft"
                        ;;
                    libgtk3)
                        install_package "libgtk-3-0" "gtk3" "gtk3" "gtk+3"
                        ;;
                esac
            done
            echo ""
        else
            echo ""
            echo "WARNING: Building without dependencies. Runtime errors may occur."
            echo ""
        fi
    fi
fi

# macOS needs no extra packages: universal SDL2 and OpenAL dylibs come from the
# MonoGame.Library.SDL / MonoGame.Library.OpenAL NuGet packages.
if [[ "$(uname)" == "Darwin" ]]; then
    echo "✓ SDL2 and OpenAL (bundled via NuGet)"
fi

echo ""

# Check if submodules are initialized
if [[ ! -f "$SCRIPT_DIR/FreeSO/TSOClient/tso.client/FSO.Client.csproj" ]]; then
    echo "Git submodules not initialized (FreeSO is missing)"
    read -p "Would you like to initialize them now? (Y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        echo "Initializing git submodules..."
        git -C "$SCRIPT_DIR" submodule update --init --recursive
        echo "✓ Submodules initialized"
    else
        echo "ERROR: Cannot build without FreeSO submodule."
        exit 1
    fi
else
    echo "✓ Git submodules found"
fi

echo ""
echo "Step 1: Restoring dependencies..."
cd "$SCRIPT_DIR"
dotnet restore Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj /p:TreatWarningsAsErrors=false /p:WarningsAsErrors=""

echo ""
echo "Step 2: Building Simitone.Desktop ($CONFIG)..."
dotnet build Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj -c "$CONFIG" --no-restore

if [[ "$PUBLISH" == "--publish" ]]; then
    echo ""
    echo "Step 3: Publishing self-contained build with launcher..."
    
    # Check for C compiler (required for the native launcher, which only Linux uses;
    # macOS ships Simitone.app instead)
    if [[ "$(uname)" != "Darwin" ]]; then
        if ! command -v gcc &> /dev/null; then
            echo ""
            echo "ERROR: gcc not found. Required for building the native launcher."
            echo ""
            echo "Install with:"
            echo "  Ubuntu/Debian: sudo apt install build-essential"
            echo "  Fedora:        sudo dnf install gcc"
            echo "  Arch:          sudo pacman -S gcc"
            echo ""
            exit 1
        fi
        CC="gcc"
        echo "✓ C compiler found: $CC"
    fi
    
    # Detect architecture (SIMITONE_ARCH=x64|arm64 overrides, e.g. to build Intel on Apple Silicon)
    ARCH="${SIMITONE_ARCH:-$(uname -m)}"
    if [[ "$ARCH" == "x86_64" || "$ARCH" == "x64" ]]; then
        RID_ARCH="x64"
    elif [[ "$ARCH" == "aarch64" || "$ARCH" == "arm64" ]]; then
        RID_ARCH="arm64"
    else
        RID_ARCH="x64"
    fi
    
    # Detect OS
    if [[ "$(uname)" == "Darwin" ]]; then
        RID="osx-$RID_ARCH"
        PLATFORM_NAME="macOS"
    else
        RID="linux-$RID_ARCH"
        PLATFORM_NAME="Linux"
    fi
    
    echo "Runtime Identifier: $RID"
    
    TEMP_PUBLISH_DIR="publish/$RID"
    FINAL_DIR="publish/Simitone-$PLATFORM_NAME-$RID_ARCH"
    
    # Clean previous publish
    rm -rf "$FINAL_DIR"
    
    # Step 3a: Publish the main application
    echo ""
    echo "Step 3a: Publishing main application..."
    dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -o "$TEMP_PUBLISH_DIR" \
        /p:TreatWarningsAsErrors=false \
        /p:WarningsAsErrors=""
    
    # Remove files Simitone doesn't need from a published directory ($1)
    prune_published_files() {
        local LIB="$1"
        # Remove unused TSO/server content (not needed in TS1/Simitone mode)
        echo "  Removing unused TSO content files..."
        # TSO object/patch data — Simitone uses TS1 game FAR files instead
        rm -rf "$LIB/Content/Objects"
        rm -rf "$LIB/Content/Patch"
        # TSO avatar IFFs — TS1 avatar data comes from game FAR files
        rm -rf "$LIB/Content/Avatar"
        # FreeSO mesh overrides for TSO objects
        rm -rf "$LIB/Content/MeshReplace"
        # OpenGL ES2 shaders — desktop always uses GLVer 3+
        rm -rf "$LIB/Content/iOS"
        # TSO online city data
        rm -rf "$LIB/Content/Cities"
        # FreeSO lot blueprint
        rm -rf "$LIB/Content/Blueprints"
        # Server-only
        rm -rf "$LIB/DatabaseScripts"
        rm -rf "$LIB/MailTemplates"
        # SimAntics visual debugger resources
        rm -rf "$LIB/IDERes"
        # .NET locale satellite assemblies (Roslyn/IDE strings — not game content)
        for locale in ru ja fr de it pl ko es pt-BR tr cs zh-Hans zh-Hant sv ro hu; do
            rm -rf "$LIB/$locale"
        done
        # Keep only target-platform native runtimes — drop android, iOS, and other-platform dirs
        if [ -d "$LIB/runtimes" ]; then
            for d in "$LIB/runtimes/"/*/; do
                dirname="$(basename "$d")"
                if [[ "$PLATFORM_NAME" == "Linux" ]]; then
                    [[ "$dirname" == linux* || "$dirname" == unix* ]] || rm -rf "$d"
                elif [[ "$PLATFORM_NAME" == "macOS" ]]; then
                    [[ "$dirname" == osx* || "$dirname" == unix* ]] || rm -rf "$d"
                fi
            done
        fi
    }

    # Sign a macOS app bundle. SIMITONE_SIGN_IDENTITY selects the certificate (e.g.
    # "Developer ID Application: Name (TEAMID)"); without it the app is signed ad hoc.
    # Real identities get Hardened Runtime and a secure timestamp, as notarization requires.
    # Ad hoc builds can't use Hardened Runtime: its library validation rejects ad hoc signed
    # libraries ("different Team IDs"), so the .NET host couldn't load its own runtime.
    sign_mac_app() {
        local app="$1"
        local identity="${SIMITONE_SIGN_IDENTITY:--}"
        local entitlements="$SCRIPT_DIR/Client/Simitone/Simitone.Desktop/Mac/Simitone.entitlements"
        local opts=(--force --sign "$identity")
        [[ "$identity" != "-" ]] && opts+=(--options runtime --timestamp)

        echo "  Signing with: $([[ "$identity" == "-" ]] && echo "ad hoc" || echo "$identity")"
        # every nested file first (dylibs, managed .dlls, .json), then the bundle and its executable
        find "$app/Contents/MacOS" -type f ! -path "$app/Contents/MacOS/Simitone" -print0 \
            | xargs -0 -n 40 codesign "${opts[@]}" 2>&1 | grep -v "replacing existing signature" || true
        codesign "${opts[@]}" --entitlements "$entitlements" "$app"
        codesign --verify --deep --strict "$app"
        echo "✓ Signature verified"
    }

    # macOS: Eto.Platform.Mac64 has already wrapped the self-contained publish in Simitone.app
    # (Eto.Mac only works from inside an .app bundle), so ship that instead of launcher + lib/.
    if [[ "$PLATFORM_NAME" == "macOS" ]]; then
        echo ""
        echo "Step 3b: Packaging Simitone.app..."
        APP_DIR="$TEMP_PUBLISH_DIR/Simitone.app"
        if [[ ! -d "$APP_DIR" ]]; then
            echo "ERROR: $APP_DIR was not created by the publish step"
            exit 1
        fi
        mkdir -p "$FINAL_DIR"
        mv "$APP_DIR" "$FINAL_DIR/"
        # Eto's publish step copies the half-built bundle into itself; drop the nested copy
        rm -rf "$FINAL_DIR/Simitone.app/Contents/MacOS/Simitone.app"
        prune_published_files "$FINAL_DIR/Simitone.app/Contents/MacOS"
        rm -rf "$TEMP_PUBLISH_DIR"

        # A signed bundle may only hold code in Contents/MacOS, so move the game data to
        # Contents/Resources (Program.cs switches the working directory there at startup).
        APP="$FINAL_DIR/Simitone.app"
        rm -f "$APP/Contents/Resources/Content" # development builds symlink this instead
        for item in Content Resources version.txt dao.xml; do
            if [[ -e "$APP/Contents/MacOS/$item" ]]; then mv "$APP/Contents/MacOS/$item" "$APP/Contents/Resources/"; fi
        done
        # 3D mode creates this if missing, which would modify the signed bundle
        mkdir -p "$APP/Contents/Resources/Content/MeshReplace"

        # Homebrew's source-built .NET (brew install dotnet) ships runtime libraries linked against
        # Homebrew (e.g. brotli), which only exist on this Mac. Refuse to package those.
        local_deps=""
        for lib in "$APP/Contents/MacOS/"*.dylib; do
            own_id="$(otool -D "$lib" | tail -n +2)"
            bad="$(otool -L "$lib" | tail -n +2 | awk '{print $1}' | grep -v -x -F "$own_id" | grep -E '^/opt/homebrew/|^/usr/local/(opt|Cellar)/' || true)"
            [[ -n "$bad" ]] && local_deps+="  $(basename "$lib") -> $bad"$'\n'
        done
        if [[ -n "$local_deps" ]]; then
            echo ""
            echo "ERROR: the bundled .NET runtime depends on Homebrew libraries and won't run on other Macs:"
            echo -n "$local_deps"
            echo "Build with Microsoft's .NET SDK instead (https://dotnet.microsoft.com/download), e.g.:"
            echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0"
            echo "  export DOTNET_ROOT=\$HOME/.dotnet PATH=\$HOME/.dotnet:\$PATH"
            exit 1
        fi

        # Clear extended attributes before signing: codesign stores the signatures of the
        # non-Mach-O files in Contents/MacOS (managed .dlls, .json) in extended attributes.
        xattr -cr "$APP" 2>/dev/null || true
        sign_mac_app "$APP"
        echo "✓ Simitone.app ($(du -sh "$APP" | cut -f1))"

        echo ""
        echo "========================================"
        echo "  Publish Complete!"
        echo "========================================"
        echo ""
        echo "Output: $SCRIPT_DIR/$FINAL_DIR/Simitone.app"
        echo ""
        echo "To run: open \"$FINAL_DIR/Simitone.app\""
        echo "   or:  \"$FINAL_DIR/Simitone.app/Contents/MacOS/Simitone\" -path\"/path/to/The Sims/\""
        echo ""
        if [[ -z "$SIMITONE_SIGN_IDENTITY" ]]; then
            echo "NOTE: this build is signed ad hoc. After downloading it, macOS asks to approve it once"
            echo "      (System Settings > Privacy & Security > Open Anyway)."
        fi
        exit 0
    fi

    # Step 3b: Build the native C launcher
    echo ""
    echo "Step 3b: Building native launcher..."
    LAUNCHER_SRC="Client/Simitone/Simitone.Launcher/launcher.c"
    LAUNCHER_OUT="publish/launcher-native/Simitone"
    mkdir -p "publish/launcher-native"
    
    $CC -O2 -s "$LAUNCHER_SRC" -o "$LAUNCHER_OUT" 2>/dev/null || \
    $CC -O2 "$LAUNCHER_SRC" -o "$LAUNCHER_OUT"  # macOS doesn't support -s
    
    if [[ ! -f "$LAUNCHER_OUT" ]]; then
        echo "ERROR: Failed to compile native launcher"
        exit 1
    fi
    echo "✓ Native launcher built ($(du -h "$LAUNCHER_OUT" | cut -f1))"
    
    # Step 3c: Create final directory structure
    echo ""
    echo "Step 3c: Creating final directory structure..."
    
    mkdir -p "$FINAL_DIR/lib"
    
    # Copy launcher to root
    cp "$LAUNCHER_OUT" "$FINAL_DIR/Simitone"
    chmod +x "$FINAL_DIR/Simitone"
    
    # Move all published files to lib/
    mv "$TEMP_PUBLISH_DIR"/* "$FINAL_DIR/lib/"

    prune_published_files "$FINAL_DIR/lib"

    # Copy .desktop file for Linux
    if [[ "$(uname)" != "Darwin" ]]; then
        cp "Client/Simitone/Simitone.Launcher/simitone.desktop" "$FINAL_DIR/"
        chmod +x "$FINAL_DIR/simitone.desktop"
        
        # Convert icon to PNG if imagemagick is available
        if command -v convert &> /dev/null; then
            echo "Converting icon to PNG..."
            convert "Client/Simitone/Simitone.Windows/Icon.ico" -resize 256x256 "$FINAL_DIR/lib/Resources/icon.png" 2>/dev/null || true
        elif [[ -f "Client/Simitone/Simitone.Shared/Resources/Icon.bmp" ]]; then
            # Fallback: copy BMP if PNG conversion fails
            cp "Client/Simitone/Simitone.Shared/Resources/Icon.bmp" "$FINAL_DIR/lib/Resources/icon.bmp" 2>/dev/null || true
        fi
    fi
    
    # Clean up temporary directories
    rm -rf "$TEMP_PUBLISH_DIR"
    rm -rf "publish/launcher-native"
    
    echo ""
    echo "========================================"
    echo "  Publish Complete!"
    echo "========================================"
    echo ""
    echo "Output directory: $SCRIPT_DIR/$FINAL_DIR"
    echo ""
    echo "Directory structure:"
    echo "  Simitone-$PLATFORM_NAME-$RID_ARCH/"
    echo "    Simitone              <- Run this!"
    if [[ "$(uname)" != "Darwin" ]]; then
        echo "    simitone.desktop      <- For Linux desktop integration"
    fi
    echo "    lib/                  <- Game files (don't modify)"
    echo ""
    echo "To run:"
    echo "  cd $FINAL_DIR"
    echo "  ./Simitone"
    echo ""
    if [[ "$(uname)" != "Darwin" ]]; then
        echo "For Linux desktop integration:"
        echo "  1. Copy the folder to your desired location"
        echo "  2. Edit simitone.desktop to set the correct paths"
        echo "  3. Copy simitone.desktop to ~/.local/share/applications/"
        echo ""
    fi
    echo "NOTE: GTK3 is required on Linux for the installation selector GUI."
    echo "      Most desktop Linux distributions include GTK3 by default."
    exit 0
fi

echo ""
echo "========================================"
echo "  Build Complete!"
echo "========================================"
echo ""
echo "Output directory:"
echo "  $SCRIPT_DIR/Client/Simitone/Simitone.Desktop/bin/$CONFIG/net9.0/"
echo ""
echo "To run Simitone:"
echo "  cd Client/Simitone/Simitone.Desktop/bin/$CONFIG/net9.0/"
echo "  ./Simitone -path\"/path/to/The Sims/\""
echo ""
echo "TIP: Use --publish to create a distributable package with clean directory structure."
echo ""
echo "Example paths for The Sims 1 installation:"
echo "  Steam Play/Proton: ~/.steam/steam/steamapps/common/The Sims/"
echo "  Wine:              ~/.wine/drive_c/Program Files/Maxis/The Sims/"
echo "  WSL (Windows):     /mnt/c/Program Files (x86)/Maxis/The Sims/"
echo ""
