import SnippetsLean.Types
import Std.Data.HashMap

namespace SnippetsLean

/-- Trim whitespace from both ends, as F#'s `String.Trim`. -/
def trimStr (s : String) : String :=
  s.trimAscii.copy

/-- Parse snippet content in the simple `key=value` format.
Mirrors `Snippets.TomlParser.parseContent`: blank lines and `#` comments are
skipped, key and value are trimmed, a later duplicate key wins. -/
def parseContent (content : String) : Except String (Std.HashMap String Snippet) := do
  let mut acc : Std.HashMap String Snippet := {}
  let mut lineNum := 0
  for rawLine in content.splitOn "\n" do
    lineNum := lineNum + 1
    let line := trimStr rawLine
    if line == "" || line.startsWith "#" then
      continue
    match line.splitOn "=" with
    | [] => throw s!"Line {lineNum}: Missing '=' separator: {line}"
    | [_] => throw s!"Line {lineNum}: Missing '=' separator: {line}"
    | key :: rest =>
      let key := trimStr key
      if key == "" then
        throw s!"Line {lineNum}: Empty key before '='"
      let value := trimStr ("=".intercalate rest)
      acc := acc.insert key { key, expansion := value }
  return acc

/-- Parse a snippets file. Mirrors `Snippets.TomlParser.parseFile`. -/
def parseFile (path : String) : IO (Except String (Std.HashMap String Snippet)) := do
  if !(← System.FilePath.pathExists path) then
    return Except.error s!"Snippets file not found: {path}"
  try
    let content ← IO.FS.readFile path
    return parseContent content
  catch ex =>
    return Except.error s!"Error reading file {path}: {ex}"

end SnippetsLean
