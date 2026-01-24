module Snippets.Types

/// Configuration for the snippet server
type Config =
  { snippetsPath: string
    caseSensitive: bool
    debug: bool }

/// A snippet loaded from configuration
type Snippet = { key: string; expansion: string }

/// Result of matching snippets against a prefix
type MatchResult =
  { snippet: Snippet
    matchQuality: float }
