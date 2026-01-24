open System
open System.IO
open Snippets.Api
open Snippets.LspProtocol
open Snippets.JsonRpc
open Snippets.MessageHandler

/// Log error messages to stderr (always shown)
let logError (msg: string) = eprintfn "[Snippets] %s" msg

/// Log informational messages to stderr (only when debug is enabled)
let logInfo (debug: bool) (msg: string) =
  if debug then
    eprintfn "[Snippets] %s" msg

/// Main server loop
let rec serverLoop (state: ServerState) (input: Stream) (output: Stream) =
  async {
    match! readMessage input with
    | None ->
      logInfo state.config.debug "EOF reached, exiting"
      return 0
    | Some msg ->
      try
        match handleMessage state msg with
        | Response responseMsg ->
          do! writeMessage output responseMsg
          return! serverLoop state input output
        | NoResponse -> return! serverLoop state input output
        | Shutdown ->
          logInfo state.config.debug "Shutting down cleanly"
          return 0
      with ex ->
        logError $"Error handling message: {ex.Message}"
        // Continue processing
        return! serverLoop state input output
  }

[<EntryPoint>]
let main argv =
  try
    // Load configuration first to check debug flag
    let configResult = loadConfig None

    let config =
      match configResult with
      | Ok cfg ->
        // Check for debug environment variable
        let debug =
          match Environment.GetEnvironmentVariable("SNIPPETS_DEBUG") with
          | null
          | "" -> cfg.debug
          | "1"
          | "true"
          | "True"
          | "TRUE" -> true
          | _ -> cfg.debug

        let finalConfig = { cfg with debug = debug }
        logInfo finalConfig.debug $"Config loaded: {finalConfig.snippetsPath}"
        finalConfig
      | Error err ->
        // Use default config, check debug environment variable
        let debug =
          match Environment.GetEnvironmentVariable("SNIPPETS_DEBUG") with
          | null
          | "" -> defaultConfig.debug
          | "1"
          | "true"
          | "True"
          | "TRUE" -> true
          | _ -> defaultConfig.debug

        let finalConfig = { defaultConfig with debug = debug }
        logInfo finalConfig.debug $"Config error: {err}, using defaults"
        finalConfig

    logInfo config.debug "Snippets Language Server v0.1.0"
    logInfo config.debug "Starting server..."

    // Load snippets
    let snippetsResult = loadSnippets config

    let snippets =
      match snippetsResult with
      | Ok snips ->
        logInfo config.debug $"Loaded {snips.Count} snippets"
        snips
      | Error err ->
        logError $"Snippets error: {err}, using empty map"
        Map.empty

    let state = createServerState snippets config

    logInfo config.debug "Server initialized and listening on stdin/stdout..."

    // Open stdin/stdout as streams
    let input = Console.OpenStandardInput()
    let output = Console.OpenStandardOutput()

    // Run async server loop
    serverLoop state input output |> Async.RunSynchronously

  with ex ->
    logError $"Fatal error: {ex.Message}\n{ex.StackTrace}"
    1
