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

# Check dependencies on macOS
if [[ "$(uname)" == "Darwin" ]]; then
    # Check for SDL2 (required for MonoGame)
    if ! brew list sdl2 &> /dev/null; then
        echo ""
        echo "SDL2 not found. SDL2 is required for MonoGame."
        read -p "Would you like to install SDL2 now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            brew install sdl2
        fi
    else
        echo "✓ SDL2 found"
    fi
    
    # OpenAL is included with macOS
    echo "✓ OpenAL (built-in on macOS)"
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
    
    # Check for C compiler (required for native launcher)
    if [[ "$(uname)" == "Darwin" ]]; then
        if ! command -v clang &> /dev/null; then
            echo ""
            echo "ERROR: clang not found. Required for building the native launcher."
            echo ""
            echo "Install Xcode Command Line Tools with:"
            echo "  xcode-select --install"
            echo ""
            exit 1
        fi
        CC="clang"
    else
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
    fi
    echo "✓ C compiler found: $CC"
    
    # Detect architecture
    ARCH="$(uname -m)"
    if [[ "$ARCH" == "x86_64" ]]; then
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

    # Remove unused TSO/server content (not needed in TS1/Simitone mode)
    echo "  Removing unused TSO content files..."
    # TSO object/patch data — Simitone uses TS1 game FAR files instead
    rm -rf "$FINAL_DIR/lib/Content/Objects"
    rm -rf "$FINAL_DIR/lib/Content/Patch"
    # TSO avatar IFFs — TS1 avatar data comes from game FAR files
    rm -rf "$FINAL_DIR/lib/Content/Avatar"
    # FreeSO mesh overrides for TSO objects
    rm -rf "$FINAL_DIR/lib/Content/MeshReplace"
    # OpenGL ES2 shaders — desktop always uses GLVer 3+
    rm -rf "$FINAL_DIR/lib/Content/iOS"
    # TSO online city data
    rm -rf "$FINAL_DIR/lib/Content/Cities"
    # FreeSO lot blueprint
    rm -rf "$FINAL_DIR/lib/Content/Blueprints"
    # Server-only
    rm -rf "$FINAL_DIR/lib/DatabaseScripts"
    rm -rf "$FINAL_DIR/lib/MailTemplates"
    # SimAntics visual debugger resources
    rm -rf "$FINAL_DIR/lib/IDERes"
    # .NET locale satellite assemblies (Roslyn/IDE strings — not game content)
    for locale in ru ja fr de it pl ko es pt-BR tr cs zh-Hans zh-Hant sv ro hu; do
        rm -rf "$FINAL_DIR/lib/$locale"
    done

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
