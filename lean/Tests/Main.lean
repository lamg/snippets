import SnippetsLean
import Lean.Data.Lsp.Communication

open SnippetsLean

private def containsSubstr (s sub : String) : Bool :=
  (s.splitOn sub).length > 1

/-- Minimal test harness: prints failures and returns the failure count. -/
structure Harness where
  failures : Nat := 0
  total : Nat := 0

private def check (h : Harness) (name : String) (cond : Bool) : IO Harness := do
  if cond then
    IO.println s!"ok - {name}"
    return { h with total := h.total + 1 }
  else
    IO.eprintln s!"FAIL - {name}"
    return { failures := h.failures + 1, total := h.total + 1 }

private def expectParse (h : Harness) (name content : String)
    (test : Std.HashMap String Snippet → Bool) : IO Harness := do
  match parseContent content with
  | Except.ok snippets => check h name (test snippets)
  | Except.error msg =>
    IO.eprintln s!"  parse error: {msg}"
    check h name false

private def expectError (h : Harness) (name content expected : String) : IO Harness := do
  match parseContent content with
  | Except.ok _ => check h name false
  | Except.error msg => check h name (containsSubstr msg expected)

def parserTests (h : Harness) : IO Harness := do
  let h ← expectParse h "parse simple key=value pair" "forloop=for i in 0..10 do" fun s =>
    s.size == 1 && (s.get? "forloop").map (·.expansion) == some "for i in 0..10 do"
  let h ← expectParse h "parse multiple snippets" "key1=value1\nkey2=value2\nkey3=value3" fun s =>
    s.size == 3
      && (s.get? "key1").map (·.expansion) == some "value1"
      && (s.get? "key2").map (·.expansion) == some "value2"
      && (s.get? "key3").map (·.expansion) == some "value3"
  let h ← expectParse h "ignore comments and blank lines"
      "# comment\n\nkey=value\n\n# another comment" fun s =>
    s.size == 1 && (s.get? "key").map (·.expansion) == some "value"
  let h ← expectError h "error on missing equals" "invalidline" "Missing '=' separator"
  let h ← expectError h "error on empty key" "=value" "Empty key"
  let h ← expectParse h "handle whitespace around key and value" "  key  =  value  " fun s =>
    s.size == 1
      && (s.get? "key").map (·.key) == some "key"
      && (s.get? "key").map (·.expansion) == some "value"
  expectParse h "parse empty file" "" fun s => s.size == 0

private def parseMessage (s : String) : Option Lean.JsonRpc.Message :=
  (Lean.Json.parse s).toOption.bind fun j =>
    (Lean.fromJson? j : Except String Lean.JsonRpc.Message).toOption

private def sampleSnippets : Std.HashMap String Snippet :=
  Std.HashMap.ofList
    [ (":eq", { key := ":eq", expansion := "≡" }),
      (":forall", { key := ":forall", expansion := "∀" }),
      (":and", { key := ":and", expansion := "∧" }),
      ("^2", { key := "^2", expansion := "²" }) ]

private def labelsOf (j : Lean.Json) : Array String :=
  match j.getObjVal? "items" with
  | Except.ok items =>
    match items.getArr? with
    | Except.ok arr =>
      arr.filterMap fun item => (item.getObjVal? "label").toOption.bind (·.getStr?.toOption)
    | Except.error _ => #[]
  | Except.error _ => #[]

def protocolTests (h : Harness) : IO Harness := do
  let cfg : Config := { snippetsPath := "", caseSensitive := false, debug := false }
  let state : ServerState :=
    { snippets := sampleSnippets, config := cfg, documents := {} }
  let h ← check h "untriggered completion returns no items"
    (labelsOf (completionItems sampleSnippets 0 5 "") == #[])
  let h ← check h "triggered completion returns matching keys sorted"
    (labelsOf (completionItems sampleSnippets 0 1 ":") == #[":and", ":eq", ":forall"])
  let rangeStartEnd : Option (Nat × Nat) := do
    let items ← (completionItems sampleSnippets 2 7 ":").getObjVal? "items" |>.toOption
    let arr ← items.getArr?.toOption
    let item ← arr[0]?
    let edit ← (item.getObjVal? "textEdit").toOption
    let range ← (edit.getObjVal? "replace").toOption
    let start ← (range.getObjVal? "start").toOption
    let ending ← (range.getObjVal? "end").toOption
    let startCh ← (start.getObjVal? "character").toOption.bind (·.getNat?.toOption)
    let endCh ← (ending.getObjVal? "character").toOption.bind (·.getNat?.toOption)
    return (startCh, endCh)
  let h ← check h "completion replaces exactly one character" (rangeStartEnd == some (6, 7))
  let syncKind :=
    (createInitializeResult.getObjVal? "capabilities").toOption
      |>.bind fun caps => (caps.getObjVal? "textDocumentSync").toOption
      |>.bind fun v => v.getNat?.toOption
  let h ← check h "initialize advertises full sync and trigger characters" (syncKind == some 1)
  let state2 := handleDidOpen state "file:///tmp/a.md" "one\r\ntwo"
  let h ← check h "didOpen splits lines and strips carriage returns"
    (state2.documents.get? "file:///tmp/a.md" == some #["one", "two"])
  let state3 := handleDidClose state2 "file:///tmp/a.md"
  let h ← check h "didClose removes the document"
    (state3.documents.get? "file:///tmp/a.md" == none)
  let h ← do
    match parseMessage
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}" with
    | some msg =>
      let (_, result) ← handleMessage state msg
      check h "initialize request gets a response" (
        match result with
        | MessageResult.response (Lean.JsonRpc.Message.response _ _) => true
        | _ => false)
    | none => check h "initialize request gets a response" false
  let h ← do
    match parseMessage "{\"jsonrpc\":\"2.0\",\"method\":\"exit\"}" with
    | some msg =>
      let (_, result) ← handleMessage state msg
      check h "exit notification shuts down" (
        match result with
        | MessageResult.shutdown => true
        | _ => false)
    | none => check h "exit notification shuts down" false
  match parseMessage "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"unknown/thing\",\"params\":{}}" with
  | some msg =>
    let (_, result) ← handleMessage state msg
    check h "unknown method is ignored without a response" (
      match result with
      | MessageResult.noResponse => true
      | _ => false)
  | none => check h "unknown method is ignored without a response" false

def framingTests (h : Harness) : IO Harness := do
  let path : System.FilePath := ".lake/framing-test.tmp"
  let original : Lean.JsonRpc.Message :=
    Lean.JsonRpc.Message.response (Lean.JsonRpc.RequestID.num 42)
      (Lean.Json.mkObj [("ok", Lean.toJson true)])
  let writeHandle ← IO.FS.Handle.mk path IO.FS.Mode.write
  Lean.IO.FS.Stream.writeLspMessage (IO.FS.Stream.ofHandle writeHandle) original
  writeHandle.flush
  let readHandle ← IO.FS.Handle.mk path IO.FS.Mode.read
  let roundTripped ← Lean.IO.FS.Stream.readLspMessage (IO.FS.Stream.ofHandle readHandle)
  IO.FS.removeFile path
  check h "LSP framing round-trips a response message"
    (Lean.toJson roundTripped == Lean.toJson original)

def main : IO UInt32 := do
  let h ← parserTests {}
  let h ← protocolTests h
  let h ← framingTests h
  IO.println s!"{h.total - h.failures}/{h.total} tests passed"
  return if h.failures == 0 then 0 else 1
