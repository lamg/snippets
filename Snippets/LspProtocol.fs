module Snippets.LspProtocol

open System
open System.Collections.Generic
open System.Text.Json
open Snippets.Types
open Snippets.CompletionProvider
open Ionide.LanguageServerProtocol.Types

/// Conditional debug logging
let logDebug (config: Config) (msg: string) =
  if config.debug then
    eprintfn "[Snippets] %s" msg

/// Server state with document tracking
type ServerState =
  { snippets: Map<string, Snippet>
    config: Config
    documents: Dictionary<string, string[]> }

/// Create initial server state
let createServerState (snippets: Map<string, Snippet>) (config: Config) : ServerState =
  { snippets = snippets
    config = config
    documents = Dictionary<string, string[]>() }

/// Create initialize result
let createInitializeResult () : InitializeResult =
  { Capabilities =
      { PositionEncoding = None
        NotebookDocumentSync = None
        // Use simple sync kind instead of full options for better compatibility
        TextDocumentSync = Some(U2.C2 TextDocumentSyncKind.Full)
        HoverProvider = None
        CompletionProvider =
          Some
            { ResolveProvider = Some false
              TriggerCharacters = Some [| ":"; "^"; "_" |]
              AllCommitCharacters = None
              CompletionItem = None
              WorkDoneProgress = None }
        SignatureHelpProvider = None
        DefinitionProvider = None
        TypeDefinitionProvider = None
        ImplementationProvider = None
        ReferencesProvider = None
        DocumentHighlightProvider = None
        DocumentSymbolProvider = None
        WorkspaceSymbolProvider = None
        CodeActionProvider = None
        CodeLensProvider = None
        DocumentFormattingProvider = None
        DocumentRangeFormattingProvider = None
        DocumentOnTypeFormattingProvider = None
        RenameProvider = None
        DocumentLinkProvider = None
        ColorProvider = None
        FoldingRangeProvider = None
        DeclarationProvider = None
        ExecuteCommandProvider = None
        Workspace = None
        SelectionRangeProvider = None
        SemanticTokensProvider = None
        CallHierarchyProvider = None
        MonikerProvider = None
        LinkedEditingRangeProvider = None
        InlayHintProvider = None
        InlineValueProvider = None
        TypeHierarchyProvider = None
        DiagnosticProvider = None
        Experimental = None }
    ServerInfo =
      Some
        { Name = "Snippets"
          Version = Some "0.1.0" } }

/// Handle textDocument/didOpen notification
let handleDidOpen (state: ServerState) (p: DidOpenTextDocumentParams) : unit =
  let uri = p.TextDocument.Uri
  let text = p.TextDocument.Text

  // Split by \n and keep empty lines (don't use RemoveEmptyEntries)
  // Also handle \r\n by trimming \r from each line
  let lines = text.Split('\n') |> Array.map (fun s -> s.TrimEnd('\r'))

  state.documents.[uri] <- lines
  logDebug state.config $"Opened document: {uri} ({lines.Length} lines)"

/// Handle textDocument/didChange notification
let handleDidChange (state: ServerState) (p: DidChangeTextDocumentParams) : unit =
  let uri = p.TextDocument.Uri
  logDebug state.config $"didChange received for: {uri}"
  // Using TextDocumentSyncKind.Full, so we get full content
  match p.ContentChanges |> Array.tryHead with
  | Some change ->
    // Handle U2 union type for change events
    let text =
      match change with
      | U2.C1 evt -> evt.Text
      | U2.C2 evt -> evt.Text

    logDebug state.config $"Change text length: {text.Length}"

    // Split by \n and keep empty lines, trim \r for Windows line endings
    let lines = text.Split('\n') |> Array.map (fun s -> s.TrimEnd('\r'))

    state.documents.[uri] <- lines
    logDebug state.config $"Updated document: {uri} ({lines.Length} lines)"
  | None -> logDebug state.config "No content changes in didChange"

/// Handle textDocument/didClose notification
let handleDidClose (state: ServerState) (p: DidCloseTextDocumentParams) : unit =
  let uri = p.TextDocument.Uri
  state.documents.Remove(uri) |> ignore
  logDebug state.config $"Closed document: {uri}"

/// Get line content from tracked documents
let getLineContent (state: ServerState) (uri: string) (line: int) : string =
  match state.documents.TryGetValue(uri) with
  | true, lines when line >= 0 && line < lines.Length -> lines.[line]
  | _ -> ""

/// Handle completion request
let handleCompletion (state: ServerState) (p: CompletionParams) : CompletionList option =
  let line = p.Position.Line
  let character = p.Position.Character

  // Get trigger character from context if available
  let triggerChar =
    p.Context
    |> Option.bind (fun ctx -> ctx.TriggerCharacter)
    |> Option.defaultValue ""

  logDebug state.config $"Completion at line {line}, char {character}, trigger: '{triggerChar}'"

  // Only return snippets when triggered by a trigger character
  let filteredSnippets =
    if triggerChar = "" then
      // No trigger character - return empty (don't pollute normal typing)
      [||]
    else
      // Triggered by character - only return snippets starting with that char
      state.snippets
      |> Map.toArray
      |> Array.filter (fun (key, _) -> key.StartsWith triggerChar)

  logDebug state.config $"Filtered to {filteredSnippets.Length} snippets"

  // Create completion items
  // Include trigger char in insertText so selecting replaces properly
  let items =
    filteredSnippets
    |> Array.map (fun (key, snippet) ->
      { Label = key
        Kind = Some CompletionItemKind.Snippet
        Detail = Some snippet.expansion
        Documentation = None
        SortText = Some "0"
        FilterText = Some key
        // Insert the expansion - Helix should replace from the trigger char
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

  logDebug state.config $"Returning {items.Length} completion items"

  Some
    { IsIncomplete = false
      Items = items
      ItemDefaults = None }
