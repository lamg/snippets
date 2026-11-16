import SnippetsLean.Types
import SnippetsLean.TomlParser
import Std.Data.HashMap

namespace SnippetsLean

/-- The default snippets location: `~/.config/helix/snippets.toml`. -/
def defaultSnippetsPath (home : String) : String :=
  s!"{home}/.config/helix/snippets.toml"

/-- Default configuration. Mirrors `Snippets.Api.defaultConfig`. -/
def defaultConfig (home : String) : Config :=
  { snippetsPath := defaultSnippetsPath home, caseSensitive := false, debug := true }

/-- Find the snippets file: the global location first, then `./snippets.toml`.
Mirrors `Snippets.Api.loadConfig`. -/
def loadConfig (home : String) : IO (Except String Config) := do
  let tryPaths := [defaultSnippetsPath home, "snippets.toml"]
  let mut existing : Option String := none
  for path in tryPaths do
    if ← System.FilePath.pathExists path then
      existing := some path
      break
  match existing with
  | some path => return Except.ok { defaultConfig home with snippetsPath := path }
  | none =>
    return Except.error s!"No snippets file found. Tried: {", ".intercalate tryPaths}"

/-- Load snippets from the configured file. Mirrors `Snippets.Api.loadSnippets`. -/
def loadSnippets (config : Config) : IO (Except String (Std.HashMap String Snippet)) :=
  parseFile config.snippetsPath

/-- Apply the `SNIPPETS_DEBUG` override, as in the F# entry point. -/
def withDebugOverride (config : Config) (envValue : Option String) : Config :=
  match envValue with
  | some "1" | some "true" | some "True" | some "TRUE" => { config with debug := true }
  | _ => config

end SnippetsLean
