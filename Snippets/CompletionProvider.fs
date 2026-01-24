module Snippets.CompletionProvider

open Snippets.Types
open Snippets.SnippetMatcher
open Ionide.LanguageServerProtocol.Types

/// Convert MatchResult to LSP CompletionItem
/// typedPrefix is what the user has typed so far
let toCompletionItem (typedPrefix: string) (result: MatchResult) : CompletionItem =
  { Label = result.snippet.key
    Kind = Some CompletionItemKind.Snippet
    Detail = Some result.snippet.expansion
    Documentation = None
    SortText = Some(sprintf "%f" (1.0 - result.matchQuality))
    // Use typed prefix for filtering so Helix matches what user typed
    FilterText = Some typedPrefix
    InsertText = Some result.snippet.expansion
    InsertTextFormat = Some InsertTextFormat.PlainText
    TextEdit = None
    TextEditText = None
    AdditionalTextEdits = None
    Command = None
    Data = None
    Deprecated = None
    Preselect = None
    CommitCharacters = None
    Tags = None
    InsertTextMode = None
    LabelDetails = None }

/// Extract the word being typed at the cursor position
let private extractPrefix (line: string) (character: int) : string =
  if character = 0 || line.Length = 0 then
    ""
  else
    let endIdx = min character line.Length
    let beforeCursor = line.Substring(0, endIdx)
    let lastSpace = beforeCursor.LastIndexOfAny([| ' '; '\t'; '\n' |])

    if lastSpace = -1 then
      beforeCursor
    else
      beforeCursor.Substring(lastSpace + 1)

/// Provide completions for a given position
let provideCompletions
  (line: string)
  (character: int)
  (snippets: Map<string, Snippet>)
  (caseSensitive: bool)
  : CompletionItem array =
  let prefix = extractPrefix line character
  let compare = if caseSensitive then System.StringComparison.Ordinal else System.StringComparison.OrdinalIgnoreCase

  snippets
  |> Map.toArray
  |> Array.filter (fun (key, _) -> key.StartsWith(prefix, compare))
  |> Array.map (fun (key, snippet) ->
    { Label = key
      Kind = Some CompletionItemKind.Snippet
      Detail = Some snippet.expansion
      Documentation = None
      SortText = Some "0"
      FilterText = Some key
      InsertText = Some snippet.expansion
      InsertTextFormat = Some InsertTextFormat.PlainText
      TextEdit = None
      TextEditText = None
      AdditionalTextEdits = None
      Command = None
      Data = None
      Deprecated = None
      Preselect = None
      CommitCharacters = None
      Tags = None
      InsertTextMode = None
      LabelDetails = None })
