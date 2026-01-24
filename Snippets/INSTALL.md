# Installation Guide

This directory contains installation scripts for the Snippets Language Server.

## Quick Install (Linux/macOS)

```bash
./install.sh
```

## Quick Install (PowerShell/Windows)

```powershell
.\install.ps1
```

## Development Install (Linux/macOS)

For local development and testing:

```bash
./install-dev.sh
```

## Manual Installation

If you prefer to install manually:

```bash
# Build the project
dotnet publish -c Release

# Install as a dotnet global tool
dotnet tool install --global --add-source ./bin/Release/net10.0/publish Snippets.Tool
```

## Configuration

### Default Configuration File

The tool looks for a snippets file at `~/.config/helix/snippets.toml`

Create this file with your snippets in the format:

```toml
snippet_name=snippet expansion text
forloop=for i in 0..10 do
```

### Debug Mode

Enable debug logging by setting the environment variable:

```bash
SNIPPETS_DEBUG=1 snippets
```

## Verification

After installation, verify the tool is working:

```bash
snippets --help
```

The tool should output its name and version.

## Uninstall

To uninstall the tool:

```bash
dotnet tool uninstall --global Snippets.Tool
```
