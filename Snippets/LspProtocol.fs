module Snippets.LspProtocol

open System.Collections.Generic
open System.IO
open System.Text.Json
open Snippets.Types

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

let private createJsonElement (write: Utf8JsonWriter -> unit) : JsonElement =
  use stream = new MemoryStream()
  use writer = new Utf8JsonWriter(stream)
  write writer
  writer.Flush()
  use doc = JsonDocument.Parse(stream.ToArray())
  doc.RootElement.Clone()

/// Create initialize result JSON payload
let createInitializeResult () : JsonElement =
  createJsonElement (fun writer ->
    writer.WriteStartObject()

    writer.WritePropertyName("capabilities")
    writer.WriteStartObject()
    // TextDocumentSyncKind.Full = 1
    writer.WriteNumber("textDocumentSync", 1)

    writer.WritePropertyName("completionProvider")
    writer.WriteStartObject()
    writer.WriteBoolean("resolveProvider", false)
    writer.WritePropertyName("triggerCharacters")
    writer.WriteStartArray()
    writer.WriteStringValue(":")
    writer.WriteStringValue("^")
    writer.WriteStringValue("_")
    writer.WriteEndArray()
    writer.WriteEndObject()

    writer.WriteEndObject()

    writer.WritePropertyName("serverInfo")
    writer.WriteStartObject()
    writer.WriteString("name", "Snippets")
    writer.WriteString("version", "0.2.0")
    writer.WriteEndObject()

    writer.WriteEndObject())

/// Handle textDocument/didOpen notification
let handleDidOpen (state: ServerState) (uri: string) (text: string) : unit =
  // Split by \n and keep empty lines (don't use RemoveEmptyEntries)
  // Also handle \r\n by trimming \r from each line
  let lines = text.Split('\n') |> Array.map (fun s -> s.TrimEnd('\r'))

  state.documents.[uri] <- lines
  logDebug state.config $"Opened document: {uri} ({lines.Length} lines)"

/// Handle textDocument/didChange notification
let handleDidChange (state: ServerState) (uri: string) (text: string) : unit =
  logDebug state.config $"didChange received for: {uri}"
  logDebug state.config $"Change text length: {text.Length}"

  // Split by \n and keep empty lines, trim \r for Windows line endings
  let lines = text.Split('\n') |> Array.map (fun s -> s.TrimEnd('\r'))

  state.documents.[uri] <- lines
  logDebug state.config $"Updated document: {uri} ({lines.Length} lines)"

/// Handle textDocument/didClose notification
let handleDidClose (state: ServerState) (uri: string) : unit =
  state.documents.Remove(uri) |> ignore
  logDebug state.config $"Closed document: {uri}"

/// Get line content from tracked documents
let getLineContent (state: ServerState) (uri: string) (line: int) : string =
  match state.documents.TryGetValue(uri) with
  | true, lines when line >= 0 && line < lines.Length -> lines.[line]
  | _ -> ""

/// Handle completion request
let handleCompletion (state: ServerState) (line: int) (character: int) (triggerChar: string) : JsonElement option =
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
  // Replace the trigger character with the expansion
  let items =
    filteredSnippets
    |> Array.map (fun (key, snippet) ->
      let startCharacter = max 0 (character - 1)
      (key, snippet.expansion, startCharacter, character))

  // Log textEdit details for debugging
  if state.config.debug && items.Length > 0 then
    let _, _, startCharacter, endCharacter = items.[0]
    logDebug state.config $"InsertReplaceEdit range: ({line},{startCharacter})->({line},{endCharacter})"

  logDebug state.config $"Returning {items.Length} completion items"

  Some(
    createJsonElement (fun writer ->
      writer.WriteStartObject()
      writer.WriteBoolean("isIncomplete", false)
      writer.WritePropertyName("items")
      writer.WriteStartArray()

      for key, expansion, startCharacter, endCharacter in items do
        writer.WriteStartObject()
        writer.WriteString("label", key)
        writer.WriteNumber("kind", 15)
        writer.WriteString("detail", expansion)
        writer.WriteString("sortText", "0")
        writer.WriteString("filterText", key)
        writer.WriteNumber("insertTextFormat", 1)

        writer.WritePropertyName("textEdit")
        writer.WriteStartObject()
        writer.WriteString("newText", expansion)

        writer.WritePropertyName("insert")
        writer.WriteStartObject()
        writer.WritePropertyName("start")
        writer.WriteStartObject()
        writer.WriteNumber("line", line)
        writer.WriteNumber("character", startCharacter)
        writer.WriteEndObject()
        writer.WritePropertyName("end")
        writer.WriteStartObject()
        writer.WriteNumber("line", line)
        writer.WriteNumber("character", endCharacter)
        writer.WriteEndObject()
        writer.WriteEndObject()

        writer.WritePropertyName("replace")
        writer.WriteStartObject()
        writer.WritePropertyName("start")
        writer.WriteStartObject()
        writer.WriteNumber("line", line)
        writer.WriteNumber("character", startCharacter)
        writer.WriteEndObject()
        writer.WritePropertyName("end")
        writer.WriteStartObject()
        writer.WriteNumber("line", line)
        writer.WriteNumber("character", endCharacter)
        writer.WriteEndObject()
        writer.WriteEndObject()

        writer.WriteEndObject()
        writer.WriteEndObject()

      writer.WriteEndArray()
      writer.WriteEndObject()))
