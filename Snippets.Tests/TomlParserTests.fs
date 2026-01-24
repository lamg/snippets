module Snippets.Tests.TomlParserTests

open Xunit
open Snippets.TomlParser

[<Fact>]
let ``parse simple key=value pair`` () =
  let toml = "forloop=for i in 0..10 do"
  let result = parseContent toml

  match result with
  | Ok snippets ->
    Assert.Equal(1, snippets.Count)
    Assert.Equal("for i in 0..10 do", snippets.["forloop"].expansion)
  | Error msg -> Assert.Fail($"Parse failed: {msg}")

[<Fact>]
let ``parse multiple snippets`` () =
  let toml = "key1=value1\nkey2=value2\nkey3=value3"
  let result = parseContent toml

  match result with
  | Ok snippets ->
    Assert.Equal(3, snippets.Count)
    Assert.Equal("value1", snippets.["key1"].expansion)
    Assert.Equal("value2", snippets.["key2"].expansion)
    Assert.Equal("value3", snippets.["key3"].expansion)
  | Error msg -> Assert.Fail($"Parse failed: {msg}")

[<Fact>]
let ``ignore comments and blank lines`` () =
  let toml = "# comment\n\nkey=value\n\n# another comment"
  let result = parseContent toml

  match result with
  | Ok snippets ->
    Assert.Equal(1, snippets.Count)
    Assert.Equal("value", snippets.["key"].expansion)
  | Error msg -> Assert.Fail($"Parse failed: {msg}")

[<Fact>]
let ``error on missing equals`` () =
  let toml = "invalidline"
  let result = parseContent toml

  match result with
  | Error msg -> Assert.Contains("Missing '=' separator", msg)
  | Ok _ -> Assert.Fail("Should have failed")

[<Fact>]
let ``error on empty key`` () =
  let toml = "=value"
  let result = parseContent toml

  match result with
  | Error msg -> Assert.Contains("Empty key", msg)
  | Ok _ -> Assert.Fail("Should have failed")

[<Fact>]
let ``handle whitespace around key and value`` () =
  let toml = "  key  =  value  "
  let result = parseContent toml

  match result with
  | Ok snippets ->
    Assert.Equal(1, snippets.Count)
    Assert.Equal("key", snippets.["key"].key)
    Assert.Equal("value", snippets.["key"].expansion)
  | Error msg -> Assert.Fail($"Parse failed: {msg}")

[<Fact>]
let ``parse empty file`` () =
  let toml = ""
  let result = parseContent toml

  match result with
  | Ok snippets -> Assert.Equal(0, snippets.Count)
  | Error msg -> Assert.Fail($"Parse failed: {msg}")
