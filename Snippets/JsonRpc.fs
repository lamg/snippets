module Snippets.JsonRpc

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Serialization

/// JSON-RPC message envelope
type JsonRpcMessage =
  { jsonrpc: string
    id: int option
    ``method``: string option
    ``params``: JsonElement option
    result: JsonElement option
    error: JsonElement option }

/// JSON serialization options for LSP
let jsonOptions =
  let options = JsonSerializerOptions()
  options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
  options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
  // Use Untagged encoding for union types so U2<A,B> serializes as just A or B
  // without the {"Case":"C1","Fields":[...]} wrapper
  let fsharpOptions = JsonFSharpOptions.Default().WithUnionUntagged()
  options.Converters.Add(JsonFSharpConverter fsharpOptions)
  options

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

            // Decode as UTF-8 string
            let json = Encoding.UTF8.GetString contentBytes

            // Deserialize
            let msg = JsonSerializer.Deserialize<JsonRpcMessage>(json, jsonOptions)

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
      eprintfn "[JsonRpc] Read error: %s" ex.Message
      return None
  }

/// Write one LSP message to stdout
let writeMessage (output: Stream) (msg: JsonRpcMessage) : Async<unit> =
  async {
    try
      // Serialize to JSON
      let json = JsonSerializer.Serialize(msg, jsonOptions)
      let contentBytes = Encoding.UTF8.GetBytes json

      // Create header
      let header = $"Content-Length: {contentBytes.Length}\r\n\r\n"
      let headerBytes = Encoding.UTF8.GetBytes header

      // Write header + content
      do! output.WriteAsync(headerBytes, 0, headerBytes.Length) |> Async.AwaitTask

      do! output.WriteAsync(contentBytes, 0, contentBytes.Length) |> Async.AwaitTask

      do! output.FlushAsync() |> Async.AwaitTask
    with ex ->
      eprintfn "[JsonRpc] Write error: %s" ex.Message
  }

/// Helper: Create response message
let createResponse (id: int) (result: 'T) : JsonRpcMessage =
  { jsonrpc = "2.0"
    id = Some id
    ``method`` = None
    ``params`` = None
    result = Some(JsonSerializer.SerializeToElement(result, jsonOptions))
    error = None }

/// Helper: Create error response
let createError (id: int) (code: int) (message: string) : JsonRpcMessage =
  let errorObj = {| code = code; message = message |}

  { jsonrpc = "2.0"
    id = Some id
    ``method`` = None
    ``params`` = None
    result = None
    error = Some(JsonSerializer.SerializeToElement(errorObj, jsonOptions)) }
