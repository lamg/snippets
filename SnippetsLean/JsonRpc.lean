import Json

namespace SnippetsLean

/-- A JSON-RPC message. The request id is kept as raw JSON so it is echoed
back verbatim. Mirrors the subset of LSP messages this server handles. -/
inductive Message where
  | request (id : Json) (method : String) (params : Json)
  | notification (method : String) (params : Json)
  | response (id : Json) (result : Json)
  | responseError (id : Json) (code : Int) (message : String)
  deriving BEq, Repr

def messageToJson : Message → Json
  | Message.request id method params =>
    Json.mkObj
      ([ ("jsonrpc", Json.str "2.0"), ("id", id), ("method", Json.str method) ]
        ++ if params.isNull then [] else [ ("params", params) ])
  | Message.notification method params =>
    Json.mkObj
      ([ ("jsonrpc", Json.str "2.0"), ("method", Json.str method) ]
        ++ if params.isNull then [] else [ ("params", params) ])
  | Message.response id result =>
    Json.mkObj [ ("jsonrpc", Json.str "2.0"), ("id", id), ("result", result) ]
  | Message.responseError id code message =>
    Json.mkObj
      [ ("jsonrpc", Json.str "2.0"), ("id", id),
        ("error", Json.mkObj [ ("code", Json.ofInt code), ("message", Json.str message) ]) ]

private def getField (j : Json) (name : String) : Option Json :=
  (j.getObjVal? name).toOption

/-- Parse a JSON value into a JSON-RPC message, mirroring the classification
the Lean library's `FromJson Message` instance performed. -/
def parseMessage (j : Json) : Option Message := do
  let method := (getField j "method").bind fun m => m.getStr?.toOption
  let params := (getField j "params").getD Json.null
  match getField j "id", method with
  | some id, some method => return Message.request id method params
  | none, some method => return Message.notification method params
  | some id, none =>
    match getField j "result" with
    | some result => return Message.response id result
    | none =>
      let err ← getField j "error"
      let code ← (getField err "code").bind fun c => c.getInt?.toOption
      let message ← (getField err "message").bind fun m => m.getStr?.toOption
      return Message.responseError id code message
  | none, none => none

private def stripLineEnd (line : String) : String :=
  (line.dropEndWhile fun c => c == '\r' || c == '\n').copy

/-- Read header lines up to the blank line. Returns `none` on EOF. -/
private partial def readHeaderLines (stream : IO.FS.Stream) : IO (Option (List String)) := do
  let line ← stream.getLine
  if line == "" then
    return none
  let line := stripLineEnd line
  if line == "" then
    return some []
  match ← readHeaderLines stream with
  | some rest => return some (line :: rest)
  | none => return none

private def contentLength (headers : List String) : Option Nat := do
  let header ← headers.find? fun h => h.startsWith "Content-Length:"
  let value := (header.drop "Content-Length:".length).trimAscii.copy
  value.toNat?

/-- Read exactly `count` bytes, looping over short reads. -/
private partial def readExactly (stream : IO.FS.Stream) (count : Nat)
    (acc : ByteArray) : IO (Option ByteArray) := do
  if acc.size >= count then
    return some acc
  let chunk ← stream.read (count - acc.size).toUSize
  if chunk.isEmpty then
    return none
  readExactly stream count (acc ++ chunk)

/-- Read one framed LSP message. Returns `none` on EOF or on malformed
input, which ends the server loop, as in the previous implementation. -/
def readMessage (stream : IO.FS.Stream) : IO (Option Message) := do
  let some headers ← readHeaderLines stream | return none
  let some length := contentLength headers | return none
  let some bytes ← readExactly stream length ByteArray.empty | return none
  let some text := String.fromUTF8? bytes | return none
  match Json.parse text { duplicateKeys := .allow } with
  | Except.ok json => return parseMessage json
  | Except.error _ => return none

/-- Write one framed LSP message, with the content length in bytes. -/
def writeMessage (stream : IO.FS.Stream) (msg : Message) : IO Unit := do
  let payload := Json.compress (messageToJson msg)
  stream.putStr s!"Content-Length: {payload.toUTF8.size}\r\n\r\n"
  stream.putStr payload
  stream.flush

end SnippetsLean
