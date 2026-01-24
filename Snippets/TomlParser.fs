module Snippets.TomlParser

open Snippets.Types
open System.IO

/// Parse TOML content with simple key=value format
let parseContent (content: string) : Result<Map<string, Snippet>, string> =
  content.Split('\n')
  |> Array.mapi (fun i line -> i + 1, line.Trim())
  |> Array.filter (fun (_, line) -> line <> "" && not (line.StartsWith("#")))
  |> Array.fold
    (fun result (lineNum, line) ->
      match result with
      | Error _ -> result
      | Ok acc ->
        match line.IndexOf('=') with
        | -1 -> Error $"Line {lineNum}: Missing '=' separator: {line}"
        | idx ->
          let key = line.Substring(0, idx).Trim()
          let value = line.Substring(idx + 1).Trim()

          if key = "" then
            Error $"Line {lineNum}: Empty key before '='"
          else
            let snippet = { key = key; expansion = value }
            Ok(Map.add key snippet acc))
    (Ok Map.empty)

/// Parse TOML file from path
let parseFile (path: string) : Result<Map<string, Snippet>, string> =
  try
    if not (File.Exists(path)) then
      Error $"Snippets file not found: {path}"
    else
      let content = File.ReadAllText(path)
      parseContent content
  with ex ->
    Error $"Error reading file {path}: {ex.Message}"
