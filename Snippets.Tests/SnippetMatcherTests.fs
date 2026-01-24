module Snippets.Tests.SnippetMatcherTests

open Xunit
open Snippets.Types
open Snippets.SnippetMatcher

let createSnippet key expansion = { key = key; expansion = expansion }

[<Fact>]
let ``exact match ranks highest`` () =
  let snippets =
    Map.ofList [ ("forloop", createSnippet "forloop" "for i in 0..10 do") ]

  let matches = matchPrefix "forloop" snippets false
  Assert.Equal(1, matches.Length)
  Assert.Equal(1.0, matches.[0].matchQuality)
  Assert.Equal("forloop", matches.[0].snippet.key)

[<Fact>]
let ``prefix match ranks lower than exact`` () =
  let snippets =
    Map.ofList [ ("forloop", createSnippet "forloop" "for i in 0..10 do") ]

  let matches = matchPrefix "for" snippets false
  Assert.Equal(1, matches.Length)
  Assert.True(matches.[0].matchQuality < 1.0)
  Assert.Equal(0.8, matches.[0].matchQuality)

[<Fact>]
let ``contains match ranks lowest`` () =
  let snippets = Map.ofList [ ("forloop", createSnippet "forloop" "for loop") ]
  let matches = matchPrefix "loo" snippets false
  Assert.Equal(1, matches.Length)
  Assert.Equal(0.5, matches.[0].matchQuality)

[<Fact>]
let ``case insensitive matching`` () =
  let snippets = Map.ofList [ ("ForLoop", createSnippet "ForLoop" "for loop") ]
  let matches = matchPrefix "for" snippets false
  Assert.Equal(1, matches.Length)
  Assert.Equal("ForLoop", matches.[0].snippet.key)

[<Fact>]
let ``case sensitive matching`` () =
  let snippets = Map.ofList [ ("ForLoop", createSnippet "ForLoop" "for loop") ]
  let matches = matchPrefix "for" snippets true
  Assert.Equal(0, matches.Length)

[<Fact>]
let ``no match returns empty list`` () =
  let snippets =
    Map.ofList [ ("forloop", createSnippet "forloop" "for i in 0..10 do") ]

  let matches = matchPrefix "xyz" snippets false
  Assert.Equal(0, matches.Length)

[<Fact>]
let ``empty prefix returns empty list`` () =
  let snippets =
    Map.ofList [ ("forloop", createSnippet "forloop" "for i in 0..10 do") ]

  let matches = matchPrefix "" snippets false
  Assert.Equal(0, matches.Length)

[<Fact>]
let ``multiple matches sorted by quality`` () =
  let snippets =
    Map.ofList
      [ ("for", createSnippet "for" "for exact")
        ("forloop", createSnippet "forloop" "for loop")
        ("loopfor", createSnippet "loopfor" "loop for") ]

  let matches = matchPrefix "for" snippets false
  Assert.Equal(3, matches.Length)
  // Exact match first
  Assert.Equal("for", matches.[0].snippet.key)
  Assert.Equal(1.0, matches.[0].matchQuality)
  // Prefix match second
  Assert.Equal("forloop", matches.[1].snippet.key)
  Assert.Equal(0.8, matches.[1].matchQuality)
  // Contains match third
  Assert.Equal("loopfor", matches.[2].snippet.key)
  Assert.Equal(0.5, matches.[2].matchQuality)
