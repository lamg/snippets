import Json
import SnippetsLean.Types
import SnippetsLean.TomlParser
import SnippetsLean.JsonRpc
import Std.Data.HashMap

namespace SnippetsLean

/-- Conditional debug logging, mirroring `logDebug` in the F# server. -/
def logDebug (config : Config) (msg : String) : IO Unit := do
  if config.debug then
    IO.eprintln s!"[Snippets] {msg}"

/-- Server state with document tracking. Mirrors `LspProtocol.ServerState`. -/
structure ServerState where
  snippets : Std.HashMap String Snippet
  config : Config
  documents : Std.HashMap String (Array String)

/-- Result of handling a message. Mirrors `MessageHandler.MessageResult`. -/
inductive MessageResult where
  | response (msg : Message)
  | noResponse
  | shutdown

/-- Split text into lines, trimming a trailing `\r` from each.
Mirrors the splitting in `handleDidOpen`/`handleDidChange`. -/
def splitLines (text : String) : Array String :=
  (text.splitOn "\n").toArray.map fun line =>
    (line.dropEndWhile (· == '\r')).copy

/-- The `initialize` result payload. Mirrors `createInitializeResult`. -/
def createInitializeResult : Json :=
  Json.mkObj
    [ ("capabilities",
        Json.mkObj
          [ -- TextDocumentSyncKind.Full = 1
            ("textDocumentSync", Json.ofNat 1),
            ("completionProvider",
              Json.mkObj
                [ ("resolveProvider", Json.bool false),
                  ("triggerCharacters", Json.arr #[Json.str ":", Json.str "^", Json.str "_"]) ]) ]),
      ("serverInfo",
        Json.mkObj [ ("name", Json.str "Snippets"), ("version", Json.str "0.2.0") ]) ]

def handleDidOpen (state : ServerState) (uri text : String) : ServerState :=
  { state with documents := state.documents.insert uri (splitLines text) }

def handleDidChange (state : ServerState) (uri text : String) : ServerState :=
  { state with documents := state.documents.insert uri (splitLines text) }

def handleDidClose (state : ServerState) (uri : String) : ServerState :=
  { state with documents := state.documents.erase uri }

/-- Completion items for a trigger character. Mirrors `handleCompletion`:
no trigger means no items; otherwise every key starting with the trigger
character, in sorted key order, replacing exactly one character. -/
def completionItems (snippets : Std.HashMap String Snippet) (line character : Nat)
    (triggerChar : String) : Json :=
  let items : Array Json :=
    if triggerChar == "" then
      #[]
    else
      let keys := snippets.toList.map (·.1) |>.filter (·.startsWith triggerChar)
      let keys := keys.mergeSort (· ≤ ·)
      keys.toArray.filterMap fun key => do
        let snippet ← snippets.get? key
        let startCharacter := character - 1
        let range (startCh endCh : Nat) : Json :=
          Json.mkObj
            [ ("start", Json.mkObj [ ("line", Json.ofNat line), ("character", Json.ofNat startCh) ]),
              ("end", Json.mkObj [ ("line", Json.ofNat line), ("character", Json.ofNat endCh) ]) ]
        return Json.mkObj
          [ ("label", Json.str key),
            ("kind", Json.ofNat 15),
            ("detail", Json.str snippet.expansion),
            ("sortText", Json.str "0"),
            ("filterText", Json.str key),
            ("insertTextFormat", Json.ofNat 1),
            ("textEdit",
              Json.mkObj
                [ ("newText", Json.str snippet.expansion),
                  ("insert", range startCharacter character),
                  ("replace", range startCharacter character) ]) ]
  Json.mkObj [ ("isIncomplete", Json.bool false), ("items", Json.arr items) ]

private def getProp (j : Json) (name : String) : Option Json :=
  match j.getObjVal? name with
  | Except.ok v => if v.isNull then none else some v
  | Except.error _ => none

private def getStr (j : Json) (name : String) : Option String :=
  (getProp j name).bind fun v => v.getStr?.toOption

private def getNat (j : Json) (name : String) : Option Nat :=
  (getProp j name).bind fun v => v.getNat?.toOption

private def parseDidOpenParams (params : Json) : Option (String × String) := do
  let doc ← getProp params "textDocument"
  let uri ← getStr doc "uri"
  let text ← getStr doc "text"
  return (uri, text)

private def parseDidChangeParams (params : Json) : Option (String × String) := do
  let doc ← getProp params "textDocument"
  let uri ← getStr doc "uri"
  let changes ← (getProp params "contentChanges").bind (·.getArr?.toOption)
  let first ← changes[0]?
  let text ← getStr first "text"
  return (uri, text)

private def parseDidCloseParams (params : Json) : Option String := do
  let doc ← getProp params "textDocument"
  getStr doc "uri"

private def parseCompletionParams (params : Json) : Option (String × Nat × Nat × String) := do
  let doc ← getProp params "textDocument"
  let uri ← getStr doc "uri"
  let pos ← getProp params "position"
  let line ← getNat pos "line"
  let character ← getNat pos "character"
  let triggerChar :=
    match getProp params "context" with
    | some ctx => (getStr ctx "triggerCharacter").getD ""
    | none => ""
  return (uri, line, character, triggerChar)

/-- Dispatch an incoming JSON-RPC message. Mirrors `handleMessage`. -/
def handleMessage (state : ServerState) (msg : Message) :
    IO (ServerState × MessageResult) := do
  match msg with
  | Message.request id method params =>
    match method with
    | "initialize" =>
      return (state, MessageResult.response (Message.response id createInitializeResult))
    | "textDocument/completion" =>
      let result :=
        match parseCompletionParams params with
        | some (_, line, character, triggerChar) =>
          completionItems state.snippets line character triggerChar
        | none => Json.null
      return (state, MessageResult.response (Message.response id result))
    | "shutdown" =>
      return (state, MessageResult.response (Message.response id Json.null))
    | "exit" => return (state, MessageResult.shutdown)
    | _ => return (state, MessageResult.noResponse)
  | Message.notification method params =>
    match method with
    | "textDocument/didOpen" =>
      let state :=
        match parseDidOpenParams params with
        | some (uri, text) => handleDidOpen state uri text
        | none => state
      return (state, MessageResult.noResponse)
    | "textDocument/didChange" =>
      let state :=
        match parseDidChangeParams params with
        | some (uri, text) => handleDidChange state uri text
        | none => state
      return (state, MessageResult.noResponse)
    | "textDocument/didClose" =>
      let state :=
        match parseDidCloseParams params with
        | some uri => handleDidClose state uri
        | none => state
      return (state, MessageResult.noResponse)
    | "exit" => return (state, MessageResult.shutdown)
    | _ => return (state, MessageResult.noResponse)
  | _ =>
    -- Response messages are ignored; the server never sends requests.
    return (state, MessageResult.noResponse)

end SnippetsLean
