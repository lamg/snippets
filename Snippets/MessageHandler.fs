module Snippets.MessageHandler

open System.Text.Json
open Ionide.LanguageServerProtocol.Types
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

/// Deserialize JSON params to specific type
let private deserializeParams<'T> (paramsJson: JsonElement) : 'T option =
  try
    Some(JsonSerializer.Deserialize<'T>(paramsJson.GetRawText(), jsonOptions))
  with ex ->
    eprintfn "[MessageHandler] Failed to deserialize params: %s" ex.Message
    None

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
      match deserializeParams<DidOpenTextDocumentParams> paramsJson with
      | Some p ->
        handleDidOpen state p
        NoResponse
      | None -> NoResponse
    | None -> NoResponse

  | Some "textDocument/didChange" ->
    logDebug state "Received didChange notification"

    match msg.``params`` with
    | Some paramsJson ->
      match deserializeParams<DidChangeTextDocumentParams> paramsJson with
      | Some p ->
        handleDidChange state p
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
      match deserializeParams<DidCloseTextDocumentParams> paramsJson with
      | Some p ->
        handleDidClose state p
        NoResponse
      | None -> NoResponse
    | None -> NoResponse

  | Some "textDocument/completion" ->
    match msg.id, msg.``params`` with
    | Some id, Some paramsJson ->
      logDebug state "Handling completion request"
      logDebug state $"Raw params: {paramsJson.GetRawText()}"

      match deserializeParams<CompletionParams> paramsJson with
      | Some p ->
        match handleCompletion state p with
        | Some result ->
          let response = createResponse id result
          // Log the actual JSON response being sent
          let responseJson =
            System.Text.Json.JsonSerializer.Serialize(response.result, jsonOptions)

          logDebug state $"Response JSON: {responseJson}"
          Response response
        | None -> Response(createResponse id null)
      | None -> Response(createResponse id null)
    | _ ->
      logDebug state "Completion request missing id or params"
      NoResponse

  | Some "shutdown" ->
    match msg.id with
    | Some id ->
      logDebug state "Shutdown requested"
      Response(createResponse id null)
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
