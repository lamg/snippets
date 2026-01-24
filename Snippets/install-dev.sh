#!/bin/bash
# Development install script for Snippets Language Server
# Installs the tool locally for development/testing without publishing

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

echo "Installing Snippets Language Server (development mode)..."

# Build the project in Release mode
echo "Building project..."
dotnet publish -c Release "$PROJECT_DIR/Snippets.fsproj"

# Install as dotnet tool from bin/Release/net10.0/publish
PUBLISH_DIR="$PROJECT_DIR/bin/Release/net10.0/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found at $PUBLISH_DIR"
    exit 1
fi

echo "Installing as dotnet tool..."
dotnet tool install --global --add-source "$PUBLISH_DIR" Snippets.Tool

echo "✓ Snippets Language Server installed successfully!"
echo "✓ You can now run: snippets"
echo ""
echo "Configuration:"
echo "  Default snippets file: ~/.config/helix/snippets.toml"
echo "  Debug mode: Set SNIPPETS_DEBUG=1 environment variable"
