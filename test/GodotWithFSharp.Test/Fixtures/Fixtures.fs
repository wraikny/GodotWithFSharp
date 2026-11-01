namespace GodotWithFSharp.Test.Fixtures

open Godot
open GodotWithFSharp

[<GenerateGodotNode("ExampleNode", typeof<Node>)>]
type ExampleImpl(_node: Node) =
  interface Impl<Node>

  [<Export(PropertyHint.Range, "0,10,0.5,quote\"\\\n\t\u0000")>]
  member val Speed = 1.0 with get, set

  [<Export>]
  member val ``event`` = "" with get, set

  [<GenerateGodotOverride>]
  member _._Ready() = ()

  [<GenerateGodotOverride("_Process")>]
  member _.Update(_delta: double) = ()

  [<GenerateGodotOverride("_GetConfigurationWarnings")>]
  member _.GetWarnings() = [| "Example warning" |]

  member _.NotForwarded() = ()

[<GenerateGodotNode("class", typeof<Node2D>)>]
type KeywordImpl(_node: Node2D) =
  interface Impl<Node2D>

// Unannotated implementation types are deliberately ignored.
type PlainImpl() =
  class
  end
