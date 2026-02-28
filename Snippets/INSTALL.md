# Installation Guide

Install `Snippets.Tool` as a global .NET CLI tool from this repository.
Run these commands from the repository root (the directory containing `build.fsx`).

## Quick Install

```bash
dotnet fsi build.fsx
```

This runs the default `InstallGlobal` target in `build.fsx`, which:
- Restores/builds/packs the tool into `./nupkg`
- Produces a local AOT package for your current platform (if configured)
- Produces an `any` fallback package
- Produces the pointer package consumed by `dotnet tool install`
- Updates an existing global install, or installs it if missing

## Development Reinstall

For local development and testing:

```bash
dotnet fsi build.fsx --target InstallGlobalDev
```

`InstallGlobalDev` forces a reinstall by uninstalling first, then reinstalling from the local package feed.

## Build Package Only

If you only want a release package:

```bash
dotnet fsi build.fsx --target Pack
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
