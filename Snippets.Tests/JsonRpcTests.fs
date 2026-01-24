module Snippets.Tests.JsonRpcTests

open Xunit
open System.IO
open System.Text
open Snippets.JsonRpc

[<Fact>]
let ``writeMessage formats header correctly`` () =
  async {
    let msg =
      { jsonrpc = "2.0"
        id = Some 1
        ``method`` = Some "test"
        ``params`` = None
        result = None
        error = None }

    use memStream = new MemoryStream()
    do! writeMessage memStream msg

    memStream.Position <- 0L
    let reader = new StreamReader(memStream)
    let header = reader.ReadLine()

    Assert.StartsWith("Content-Length: ", header)
  }
  |> Async.RunSynchronously

[<Fact>]
let ``roundtrip message write and read`` () =
  async {
    let msg =
      { jsonrpc = "2.0"
        id = Some 42
        ``method`` = Some "testMethod"
        ``params`` = None
        result = None
        error = None }

    use memStream = new MemoryStream()
    do! writeMessage memStream msg

    memStream.Position <- 0L
    let! readMsg = readMessage memStream

    match readMsg with
    | Some m ->
      Assert.Equal("2.0", m.jsonrpc)
      Assert.Equal(Some 42, m.id)
      Assert.Equal(Some "testMethod", m.``method``)
    | None -> Assert.Fail "Failed to read message"
  }
  |> Async.RunSynchronously

[<Fact>]
let ``readMessage handles empty stream`` () =
  async {
    use memStream = new MemoryStream()
    let! result = readMessage memStream
    Assert.True result.IsNone
  }
  |> Async.RunSynchronously

[<Fact>]
let ``createResponse creates valid response`` () =
  let response = createResponse 123 "test result"
  Assert.Equal("2.0", response.jsonrpc)
  Assert.Equal(Some 123, response.id)
  Assert.True response.result.IsSome
  Assert.True response.``method``.IsNone

[<Fact>]
let ``createError creates valid error response`` () =
  let errorResp = createError 456 -32600 "Invalid Request"
  Assert.Equal("2.0", errorResp.jsonrpc)
  Assert.Equal(Some 456, errorResp.id)
  Assert.True errorResp.error.IsSome
  Assert.True errorResp.result.IsNone
