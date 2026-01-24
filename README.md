# Snippets

[![.NET][dotnet-badge]](https://dotnet.microsoft.com/)
[![F#][fs-badge]](https://fsharp.org/)
[![License: MIT][mit-badge]](https://opensource.org/licenses/MIT)
![Tests][tests]

A Language Server Protocol (LSP) implementation for code snippet completion, designed to integrate with the Helix editor and other LSP-compatible editors.

## Features

- **LSP-Compliant** - Full support for the Language Server Protocol
- **TOML Configuration** - Simple TOML format for snippet definitions
- **Helix Integration** - Optimized for the Helix editor
- **Case-Sensitive Matching** - Configurable snippet matching
- **Debug Logging** - Built-in debugging capabilities
- **Multiple Installation Methods** - Easy install scripts for Linux, macOS, and Windows

## Quick Start

### Installation

**Linux/macOS:**
```bash
cd Snippets
./install.sh
```

**Windows (PowerShell):**
```powershell
cd Snippets
.\install.ps1
```

For development:
```bash
cd Snippets
./install-dev.sh
```

See [INSTALL.md](Snippets/INSTALL.md) for detailed instructions.

### Configuration

The tool looks for snippets at `~/.config/helix/snippets.toml`

Example snippets file:
```toml
forloop=for i in 0..10 do
ifblock=if condition then
  // code
endif
```

### Usage

Run the language server:
```bash
snippets
```

Enable debug logging:
```bash
SNIPPETS_DEBUG=1 snippets
```

## Architecture

The project consists of:

- **Types.fs** - Core data types and configuration
- **TomlParser.fs** - TOML snippet file parsing
- **SnippetMatcher.fs** - Snippet matching and ranking
- **CompletionProvider.fs** - LSP completion item generation
- **LspProtocol.fs** - LSP protocol implementation
- **JsonRpc.fs** - JSON-RPC message handling
- **MessageHandler.fs** - Message routing and processing
- **Program.fs** - Server entry point

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Build Release

```bash
dotnet publish -c Release
```

## Requirements

- .NET 10.0 or later
- F# 8.0 or later

## Testing

The project includes comprehensive tests for:
- TOML parsing
- Snippet matching
- Completion provider logic
- JSON-RPC message handling

Run tests with:
```bash
dotnet test
```

## Environment Variables

- `SNIPPETS_DEBUG` - Set to `1` to enable debug logging (default: depends on config)

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

[dotnet-badge]: https://img.shields.io/badge/.NET-10.0-blue?style=flat-square
[mit-badge]: https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square
[fs-badge]: https://img.shields.io/badge/Language-F%23-blue?style=flat-square
[tests]: https://img.shields.io/github/actions/workflow/status/lamg/snippets/test.yml?style=flat-square&label=tests
