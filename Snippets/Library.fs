module Snippets.Api

open Snippets.Types
open Snippets.TomlParser
open System.IO

/// Default configuration
let defaultConfig: Config =
  { snippetsPath = Path.Combine(
      System.Environment.GetFolderPath System.Environment.SpecialFolder.UserProfile,
      ".config/helix/snippets.toml"
    )
    caseSensitive = false
    debug = true }

/// Load configuration from optional path
/// Tries multiple locations: specified path, workspace, global
let loadConfig (configPath: string option) : Result<Config, string> =
  let tryPaths =
    match configPath with
    | Some path -> [ path ]
    | None ->
      [ Path.Combine(
          System.Environment.GetFolderPath System.Environment.SpecialFolder.UserProfile,
          ".config/helix/snippets.toml"
        )
        "snippets.toml" ]

  let existingPath = tryPaths |> List.tryFind File.Exists

  match existingPath with
  | Some path ->
    Ok
      { defaultConfig with
          snippetsPath = path }
  | None ->
    let pathsList = String.concat ", " tryPaths
    Error $"No snippets file found. Tried: {pathsList}"

/// Load snippets from config
let loadSnippets (config: Config) : Result<Map<string, Snippet>, string> = parseFile config.snippetsPath
