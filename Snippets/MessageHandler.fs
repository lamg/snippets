module Snippets.MessageHandler

open System.Text.Json
open Snippets.JsonRpc
open Snippets.LspProtocol

/// Result of handling a message
type MessageResult =
  | Response of JsonRpcMessage
  | NoResponse
  | Shutdown

/// Conditional debug logging
let private logDebug (state: ServerState) (msg: string) =
  if state.config.debug then
    eprintfn "[MessageHandler] %s" msg

let private tryGetProperty (name: string) (element: JsonElement) : JsonElement option =
  if element.ValueKind <> JsonValueKind.Object then
    None
  else
    let mutable value = Unchecked.defaultof<JsonElement>

    if element.TryGetProperty(name, &value) then
      Some value
    else
      None

let private tryGetString (element: JsonElement) : string option =
  if element.ValueKind = JsonValueKind.String then
    Some(element.GetString())
  else
    None

let private tryGetInt (element: JsonElement) : int option =
  let mutable value = 0

  if element.ValueKind = JsonValueKind.Number && element.TryGetInt32(&value) then
    Some value
  else
    None

let private parseDidOpenParams (paramsJson: JsonElement) : (string * string) option =
  match tryGetProperty "textDocument" paramsJson with
  | Some textDocument ->
    match tryGetProperty "uri" textDocument |> Option.bind tryGetString, tryGetProperty "text" textDocument |> Option.bind tryGetString with
    | Some uri, Some text -> Some(uri, text)
    | _ -> None
  | None -> None

let private parseDidChangeParams (paramsJson: JsonElement) : (string * string) option =
  let uri =
    tryGetProperty "textDocument" paramsJson
    |> Option.bind (tryGetProperty "uri")
    |> Option.bind tryGetString

  let text =
    match tryGetProperty "contentChanges" paramsJson with
    | Some contentChanges when contentChanges.ValueKind = JsonValueKind.Array ->
      contentChanges.EnumerateArray()
      |> Seq.tryHead
      |> Option.bind (tryGetProperty "text")
      |> Option.bind tryGetString
    | _ -> None

  match uri, text with
  | Some uri, Some text -> Some(uri, text)
  | _ -> None

let private parseDidCloseParams (paramsJson: JsonElement) : string option =
  tryGetProperty "textDocument" paramsJson
  |> Option.bind (tryGetProperty "uri")
  |> Option.bind tryGetString

let private parseCompletionParams (paramsJson: JsonElement) : (string * int * int * string) option =
  let uri =
    tryGetProperty "textDocument" paramsJson
    |> Option.bind (tryGetProperty "uri")
    |> Option.bind tryGetString

  let line =
    tryGetProperty "position" paramsJson
    |> Option.bind (tryGetProperty "line")
    |> Option.bind tryGetInt

  let character =
    tryGetProperty "position" paramsJson
    |> Option.bind (tryGetProperty "character")
    |> Option.bind tryGetInt

  let triggerChar =
    tryGetProperty "context" paramsJson
    |> Option.bind (tryGetProperty "triggerCharacter")
    |> Option.bind tryGetString
    |> Option.defaultValue ""

  match uri, line, character with
  | Some uri, Some line, Some character -> Some(uri, line, character, triggerChar)
  | _ -> None

/// Dispatch incoming JSON-RPC message
let handleMessage (state: ServerState) (msg: JsonRpcMessage) : MessageResult =
  // Log all incoming methods for debugging
  match msg.``method`` with
  | Some m -> logDebug state $"Received method: {m}"
  | None -> ()

  match msg.``method`` with
  | Some "initialize" ->
    match msg.id with
    | Some id ->
      logDebug state "Handling initialize request"
      let result = createInitializeResult ()
      Response(createResponse id result)
    | None ->
      logDebug state "Initialize missing id"
      NoResponse

  | Some "initialized" ->
    logDebug state "Client initialized"
    NoResponse

  | Some "textDocument/didOpen" ->
    match msg.``params`` with
    | Some paramsJson ->
      match parseDidOpenParams paramsJson with
      | Some(uri, text) ->
        handleDidOpen state uri text
        NoResponse
      | None -> NoResponse
    | None -> NoResponse

  | Some "textDocument/didChange" ->
    logDebug state "Received didChange notification"

    match msg.``params`` with
    | Some paramsJson ->
      match parseDidChangeParams paramsJson with
      | Some(uri, text) ->
        handleDidChange state uri text
        NoResponse
      | None ->
        logDebug state "Failed to deserialize didChange params"
        NoResponse
    | None ->
      logDebug state "didChange has no params"
      NoResponse

  | Some "textDocument/didClose" ->
    match msg.``params`` with
    | Some paramsJson ->
      match parseDidCloseParams paramsJson with
      | Some uri ->
        handleDidClose state uri
        NoResponse
      | None -> NoResponse
    | None -> NoResponse

  | Some "textDocument/completion" ->
    match msg.id, msg.``params`` with
    | Some id, Some paramsJson ->
      logDebug state "Handling completion request"
      logDebug state $"Raw params: {paramsJson.GetRawText()}"

      match parseCompletionParams paramsJson with
      | Some(_uri, line, character, triggerChar) ->
        match handleCompletion state line character triggerChar with
        | Some result ->
          let response = createResponse id result
          Response response
        | None -> Response(createResponse id nullJsonElement)
      | None -> Response(createResponse id nullJsonElement)
    | _ ->
      logDebug state "Completion request missing id or params"
      NoResponse

  | Some "shutdown" ->
    match msg.id with
    | Some id ->
      logDebug state "Shutdown requested"
      Response(createResponse id nullJsonElement)
    | None -> NoResponse

  | Some "exit" ->
    logDebug state "Exit requested"
    Shutdown

  | Some method ->
    logDebug state $"Unknown method: {method}"
    NoResponse

  | None ->
    // Response message (we don't send requests, so ignore)
    NoResponse
