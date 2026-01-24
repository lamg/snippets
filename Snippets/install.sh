#!/bin/bash
# Install script for Snippets Language Server

set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Installing Snippets Language Server..."

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet is not installed or not in PATH"
    exit 1
fi

# Build the project in Release mode
echo "Building project..."
dotnet pack -c Release -o "$SCRIPT_DIR/bin/release-publish" "$SCRIPT_DIR/Snippets.fsproj"

# Install as dotnet tool
echo "Installing as dotnet tool..."
dotnet tool install --global --add-source "$SCRIPT_DIR/bin/release-publish" Snippets.Tool

echo "✓ Snippets Language Server installed successfully!"
echo "✓ You can now run: snippets"
echo ""
echo "Configuration:"
echo "  Default snippets file: ~/.config/helix/snippets.toml"
echo "  Debug mode: Set SNIPPETS_DEBUG=1 environment variable"
