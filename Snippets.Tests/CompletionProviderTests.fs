module Snippets.Tests.CompletionProviderTests

open Xunit
open Snippets.Types
open Snippets.CompletionProvider
open Ionide.LanguageServerProtocol.Types

let createSnippet key expansion = { key = key; expansion = expansion }

[<Fact>]
let ``completion items have correct properties`` () =
  let snippets =
    Map.ofList [ ("forloop", createSnippet "forloop" "for i in 0..10 do") ]

  let items = provideCompletions "for" 3 snippets false
  Assert.Equal(1, items.Length)
  Assert.Equal("forloop", items.[0].Label)
  Assert.Equal("for i in 0..10 do", items.[0].InsertText.Value)
  Assert.Equal(CompletionItemKind.Snippet, items.[0].Kind.Value)

[<Fact>]
let ``extract prefix from middle of line`` () =
  let snippets = Map.ofList [ ("test", createSnippet "test" "test value") ]
  let items = provideCompletions "some te" 7 snippets false
  Assert.Equal(1, items.Length)
  Assert.Equal("test", items.[0].Label)

[<Fact>]
let ``extract prefix at line start`` () =
  let snippets = Map.ofList [ ("test", createSnippet "test" "test value") ]
  let items = provideCompletions "te" 2 snippets false
  Assert.Equal(1, items.Length)

[<Fact>]
let ``no completions for no match`` () =
  let snippets = Map.ofList [ ("test", createSnippet "test" "test value") ]
  let items = provideCompletions "xyz" 3 snippets false
  Assert.Equal(0, items.Length)

[<Fact>]
let ``multiple completions sorted by quality`` () =
  let snippets =
    Map.ofList
      [ ("for", createSnippet "for" "for exact")
        ("forloop", createSnippet "forloop" "for loop")
        ("foreach", createSnippet "foreach" "for each") ]

  let items = provideCompletions "for" 3 snippets false
  Assert.Equal(3, items.Length)
  // Exact match should be first
  Assert.Equal("for", items.[0].Label)
