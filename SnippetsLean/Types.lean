namespace SnippetsLean

/-- Configuration for the snippet server. Mirrors `Snippets.Types.Config`. -/
structure Config where
  snippetsPath : String
  caseSensitive : Bool
  debug : Bool
  deriving Repr

/-- A snippet loaded from configuration. Mirrors `Snippets.Types.Snippet`. -/
structure Snippet where
  key : String
  expansion : String
  deriving Repr, BEq

end SnippetsLean
