import SnippetsLean

open SnippetsLean

/-- Sequential server loop over stdin/stdout, mirroring `Program.serverLoop`:
one message at a time, handler errors are logged and the loop continues. -/
partial def serverLoop (state : ServerState) (input output : IO.FS.Stream) : IO UInt32 := do
  let msg? ←
    try
      readMessage input
    catch _ =>
      logDebug state.config "EOF reached, exiting"
      return 0
  match msg? with
  | none =>
    logDebug state.config "EOF reached, exiting"
    return 0
  | some msg =>
    try
      let (state, result) ← handleMessage state msg
      match result with
      | MessageResult.response response =>
        writeMessage output response
        serverLoop state input output
      | MessageResult.noResponse => serverLoop state input output
      | MessageResult.shutdown =>
        logDebug state.config "Shutting down cleanly"
        return 0
    catch ex =>
      IO.eprintln s!"[Snippets] Error handling message: {ex}"
      serverLoop state input output

def main : IO UInt32 := do
  try
    let home := (← IO.getEnv "HOME").getD ""
    let configResult ← loadConfig home
    let baseConfig ←
      match configResult with
      | Except.ok cfg => pure cfg
      | Except.error err => do
        IO.eprintln s!"[Snippets] Config error: {err}, using defaults"
        pure (defaultConfig home)
    let config := withDebugOverride baseConfig (← IO.getEnv "SNIPPETS_DEBUG")
    logDebug config "Snippets Language Server v0.2.0"
    let snippets ←
      match ← loadSnippets config with
      | Except.ok snippets =>
        logDebug config s!"Loaded {snippets.size} snippets"
        pure snippets
      | Except.error err =>
        IO.eprintln s!"[Snippets] Snippets error: {err}, using empty map"
        pure {}
    let state : ServerState := { snippets, config, documents := {} }
    let input ← IO.getStdin
    let output ← IO.getStdout
    serverLoop state input output
  catch ex =>
    IO.eprintln s!"[Snippets] Fatal error: {ex}"
    return 1
