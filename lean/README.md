# Snippets LSP — Lean rewrite

A Lean 4 rewrite of the F# Snippets language server, with strict behavioral
parity to the original (which remains the reference implementation).

## Development environment

The toolchain is pinned in `lean-toolchain` (`leanprover/lean4:v4.35.0`) and
provided through Nix + elan:

```bash
cd lean
nix develop   # drops into a shell with lean/lake at the pinned version
lake build snippets snippets-tests
.lake/build/bin/snippets-tests
```

Note: because this directory lives inside a git repository, Nix flakes only
see tracked or staged files — run `git add lean/` once after adding new files
if `nix develop` reports that it cannot find the flake.

## Production install

```bash
cd lean
nix develop -c lake build snippets snippets-tests
.lake/build/bin/snippets-tests
install -m 755 .lake/build/bin/snippets ~/.local/bin/snippets
strip ~/.local/bin/snippets   # optional, 110 MB -> 76 MB
cp ../Snippets/snippets.toml ~/.config/helix/snippets.toml
```

The binary is statically linked against the Lean libraries, so the installed
copy runs without the Lean toolchain or `LEAN_PATH` set.

## Running

```bash
snippets
```

The server reads snippets from `~/.config/helix/snippets.toml` (falling back
to `./snippets.toml`), exactly like the F# version, and speaks LSP over
stdin/stdout. Set `SNIPPETS_DEBUG=1` for debug logging on stderr.

Example Helix configuration (`~/.config/helix/languages.toml`):

```toml
[language-server.snippets]
command = "/home/lamg/.local/bin/snippets"

[[language]]
name = "markdown"
language-servers = ["snippets"]
```

## Layout

- `SnippetsLean/Types.lean` — `Config` and `Snippet`
- `SnippetsLean/TomlParser.lean` — the simple `key=value` snippets parser
- `SnippetsLean/Config.lean` — config/snippet loading and `SNIPPETS_DEBUG`
- `SnippetsLean/Protocol.lean` — server state, completion, message dispatch
- `Main.lean` — the stdin/stdout server loop
- `Tests/Main.lean` — parser, protocol, and framing tests
