#!/bin/bash
# Build script for Simitone on Linux/macOS/WSL
# Usage: ./build-linux.sh [debug|release] [--publish]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Release}"
PUBLISH="${2:-}"

# Normalize configuration name
if [[ "${CONFIG,,}" == "debug" ]]; then
    CONFIG="Debug"
else
    CONFIG="Release"
fi

echo "========================================"
echo "  Simitone Linux/macOS Build Script"
echo "========================================"
echo ""
echo "Configuration: $CONFIG"
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
    
    # Check for OpenAL (required for audio)
    if ! ldconfig -p | grep -q libopenal; then
        MISSING_DEPS+=("libopenal")
    else
        echo "✓ OpenAL found"
    fi
    
    # Offer to install missing dependencies
    if [[ ${#MISSING_DEPS[@]} -gt 0 ]]; then
        echo ""
        echo "Missing dependencies: ${MISSING_DEPS[*]}"
        echo ""
        read -p "Would you like to install them now? (Y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Nn]$ ]]; then
            for dep in "${MISSING_DEPS[@]}"; do
                case "$dep" in
                    libopenal)
                        install_package "libopenal1" "openal-soft" "openal" "openal-soft"
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
dotnet restore Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj

echo ""
echo "Step 2: Building Simitone.Desktop ($CONFIG)..."
dotnet build Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj -c "$CONFIG" --no-restore

if [[ "$PUBLISH" == "--publish" ]]; then
    echo ""
    echo "Step 3: Publishing self-contained build..."
    
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
    else
        RID="linux-$RID_ARCH"
    fi
    
    echo "Runtime Identifier: $RID"
    
    dotnet publish Client/Simitone/Simitone.Desktop/Simitone.Desktop.csproj \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -o "publish/$RID"
    
    echo ""
    echo "Published to: $SCRIPT_DIR/publish/$RID"
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
echo "Example paths for The Sims 1 installation:"
echo "  Steam Play/Proton: ~/.steam/steam/steamapps/common/The Sims/"
echo "  Wine:              ~/.wine/drive_c/Program Files/Maxis/The Sims/"
echo "  WSL (Windows):     /mnt/c/Program Files (x86)/Maxis/The Sims/"
echo ""
