module Snippets.JsonRpc

open System
open System.IO
open System.Text
open System.Text.Json

/// JSON-RPC message envelope
type JsonRpcMessage =
  { jsonrpc: string
    id: int option
    ``method``: string option
    ``params``: JsonElement option
    result: JsonElement option
    error: JsonElement option }

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

let private parseJsonRpcMessageElement (element: JsonElement) : JsonRpcMessage =
  if element.ValueKind <> JsonValueKind.Object then
    raise (JsonException("Expected JSON object for JSON-RPC message."))

  let jsonrpc =
    tryGetProperty "jsonrpc" element |> Option.bind tryGetString |> Option.defaultValue "2.0"

  let id =
    match tryGetProperty "id" element with
    | Some value when value.ValueKind = JsonValueKind.Null -> None
    | Some value -> tryGetInt value
    | None -> None

  let methodName =
    match tryGetProperty "method" element with
    | Some value when value.ValueKind = JsonValueKind.Null -> None
    | Some value -> tryGetString value
    | None -> None

  let paramsValue =
    match tryGetProperty "params" element with
    | Some value when value.ValueKind <> JsonValueKind.Null -> Some(value.Clone())
    | _ -> None

  let resultValue =
    match tryGetProperty "result" element with
    | Some value when value.ValueKind <> JsonValueKind.Null -> Some(value.Clone())
    | _ -> None

  let errorValue =
    match tryGetProperty "error" element with
    | Some value when value.ValueKind <> JsonValueKind.Null -> Some(value.Clone())
    | _ -> None

  { jsonrpc = jsonrpc
    id = id
    ``method`` = methodName
    ``params`` = paramsValue
    result = resultValue
    error = errorValue }

let private writeJsonRpcMessage (writer: Utf8JsonWriter) (msg: JsonRpcMessage) : unit =
  writer.WriteStartObject()
  writer.WriteString("jsonrpc", msg.jsonrpc)

  match msg.id with
  | Some id -> writer.WriteNumber("id", id)
  | None -> ()

  match msg.``method`` with
  | Some methodName -> writer.WriteString("method", methodName)
  | None -> ()

  match msg.``params`` with
  | Some paramsValue ->
    writer.WritePropertyName("params")
    paramsValue.WriteTo(writer)
  | None -> ()

  match msg.result with
  | Some resultValue ->
    writer.WritePropertyName("result")
    resultValue.WriteTo(writer)
  | None -> ()

  match msg.error with
  | Some errorValue ->
    writer.WritePropertyName("error")
    errorValue.WriteTo(writer)
  | None -> ()

  writer.WriteEndObject()

/// Read a single byte from stream, returns None on EOF
let private readByte (stream: Stream) : Async<byte option> =
  async {
    let buffer = Array.zeroCreate<byte> 1
    let! bytesRead = stream.ReadAsync(buffer, 0, 1) |> Async.AwaitTask

    if bytesRead = 0 then
      return None
    else
      return Some buffer.[0]
  }

/// Read header section until \r\n\r\n, returns headers as map
/// Reads bytes directly to avoid buffering issues
let private readHeaders (stream: Stream) : Async<Map<string, string> option> =
  async {
    let headerBytes = ResizeArray<byte>()
    let mutable foundEnd = false
    let mutable eof = false

    // Read until we find \r\n\r\n (end of headers)
    while not foundEnd && not eof do
      let! byteOpt = readByte stream

      match byteOpt with
      | None -> eof <- true
      | Some b ->
        headerBytes.Add b
        let len = headerBytes.Count

        // Check for \r\n\r\n pattern
        if
          len >= 4
          && headerBytes.[len - 4] = 13uy // \r
          && headerBytes.[len - 3] = 10uy // \n
          && headerBytes.[len - 2] = 13uy // \r
          && headerBytes.[len - 1] = 10uy // \n
        then
          foundEnd <- true

    if eof && headerBytes.Count = 0 then
      return None
    elif not foundEnd then
      eprintfn "[JsonRpc] Unexpected EOF while reading headers"
      return None
    else
      // Parse headers (exclude the final \r\n\r\n)
      let headerStr =
        Encoding.ASCII.GetString(headerBytes.ToArray(), 0, headerBytes.Count - 4)

      let headers =
        headerStr.Split([| "\r\n" |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (fun line ->
          match line.Split([| ':' |], 2) with
          | [| key; value |] -> Some(key.Trim(), value.Trim())
          | _ -> None)
        |> Map.ofArray

      return Some headers
  }

/// Read exact number of bytes from stream
let private readContentBytes (stream: Stream) (length: int) : Async<byte[]> =
  async {
    let buffer = Array.zeroCreate<byte> length
    let mutable totalRead = 0

    while totalRead < length do
      let! bytesRead = stream.ReadAsync(buffer, totalRead, length - totalRead) |> Async.AwaitTask

      if bytesRead = 0 then
        failwith "Unexpected EOF while reading content"

      totalRead <- totalRead + bytesRead

    return buffer
  }

/// Read one LSP message from a Stream
/// Format: "Content-Length: N\r\n\r\n<JSON>"
/// Returns None only on true EOF (stream closed)
/// Note: Content-Length specifies bytes, not characters
let readMessage (stream: Stream) : Async<JsonRpcMessage option> =
  async {
    try
      // Read headers (reads bytes directly)
      let! headersOpt = readHeaders stream

      match headersOpt with
      | None ->
        // True EOF - stream closed
        return None
      | Some headers ->
        // Get Content-Length (in bytes)
        match headers.TryFind "Content-Length" with
        | Some lengthStr ->
          match Int32.TryParse lengthStr with
          | true, length when length > 0 ->
            // Read exactly Content-Length bytes
            let! contentBytes = readContentBytes stream length

            use doc = JsonDocument.Parse(contentBytes)
            let msg = parseJsonRpcMessageElement doc.RootElement
            return Some msg
          | true, length ->
            eprintfn "[JsonRpc] Invalid Content-Length (must be > 0): %d" length
            return None
          | false, _ ->
            eprintfn "[JsonRpc] Invalid Content-Length format: %s" lengthStr
            return None
        | None ->
          // Missing Content-Length header - malformed but not EOF
          eprintfn "[JsonRpc] Missing Content-Length header"
          return None
    with ex ->
      let details =
        if isNull ex.InnerException then ex.Message else $"{ex.Message} | Inner: {ex.InnerException.Message}"

      eprintfn "[JsonRpc] Read error: %s" details
      return None
  }

/// Write one LSP message to stdout
let writeMessage (output: Stream) (msg: JsonRpcMessage) : Async<unit> =
  async {
    try
      // Serialize message without runtime reflection.
      use contentStream = new MemoryStream()
      use writer = new Utf8JsonWriter(contentStream)
      writeJsonRpcMessage writer msg
      writer.Flush()
      let contentBytes = contentStream.ToArray()

      // Create header
      let header = $"Content-Length: {contentBytes.Length}\r\n\r\n"
      let headerBytes = Encoding.UTF8.GetBytes header

      // Write header + content
      do! output.WriteAsync(headerBytes, 0, headerBytes.Length) |> Async.AwaitTask

      do! output.WriteAsync(contentBytes, 0, contentBytes.Length) |> Async.AwaitTask

      do! output.FlushAsync() |> Async.AwaitTask
    with ex ->
      let details =
        if isNull ex.InnerException then ex.Message else $"{ex.Message} | Inner: {ex.InnerException.Message}"

      eprintfn "[JsonRpc] Write error: %s" details
  }

/// Helper: Create response message
let createResponse (id: int) (result: JsonElement) : JsonRpcMessage =
  { jsonrpc = "2.0"
    id = Some id
    ``method`` = None
    ``params`` = None
    result = Some result
    error = None }

let private createJsonElement (write: Utf8JsonWriter -> unit) : JsonElement =
  use stream = new MemoryStream()
  use writer = new Utf8JsonWriter(stream)
  write writer
  writer.Flush()
  use doc = JsonDocument.Parse(stream.ToArray())
  doc.RootElement.Clone()

let nullJsonElement : JsonElement = createJsonElement (fun writer -> writer.WriteNullValue())

/// Helper: Create error response
let createError (id: int) (code: int) (message: string) : JsonRpcMessage =
  let errorObj =
    createJsonElement (fun writer ->
      writer.WriteStartObject()
      writer.WriteNumber("code", code)
      writer.WriteString("message", message)
      writer.WriteEndObject())

  { jsonrpc = "2.0"
    id = Some id
    ``method`` = None
    ``params`` = None
    result = None
    error = Some errorObj }
