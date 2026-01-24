module Snippets.SnippetMatcher

open Snippets.Types
open System

/// Calculate match quality for a prefix
/// Returns Some quality (1.0 = exact, 0.8 = prefix, 0.5 = contains)
/// or None if no match
let private rankMatch (prefix: string) (key: string) (caseSensitive: bool) : float option =
  let prefixCmp = if caseSensitive then prefix else prefix.ToLowerInvariant()
  let keyCmp = if caseSensitive then key else key.ToLowerInvariant()

  if keyCmp = prefixCmp then Some 1.0 // Exact match
  elif keyCmp.StartsWith(prefixCmp) then Some 0.8 // Prefix match
  elif keyCmp.Contains(prefixCmp) then Some 0.5 // Contains match
  else None // No match

/// Match snippets by prefix and return ranked results
let matchPrefix (prefix: string) (snippets: Map<string, Snippet>) (caseSensitive: bool) : MatchResult list =
  if String.IsNullOrWhiteSpace(prefix) then
    []
  else
    snippets
    |> Map.toList
    |> List.choose (fun (_, snippet) ->
      rankMatch prefix snippet.key caseSensitive
      |> Option.map (fun quality ->
        { snippet = snippet
          matchQuality = quality }))
    |> List.sortByDescending (fun r -> r.matchQuality)
