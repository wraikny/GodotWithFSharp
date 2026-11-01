namespace GodotWithFSharp.Test

open Godot
open GodotWithFSharp

module InvalidFixtures =
  [<GenerateGodotNode("MissingContractNode", typeof<Node>)>]
  type MissingContract(_node: Node) =
    class
    end

  [<GenerateGodotNode("MissingConstructorNode", typeof<Node>)>]
  type MissingConstructor() =
    interface Impl<Node>

  [<GenerateGodotNode("ReadOnlyNode", typeof<Node>)>]
  type ReadOnlyExport(_node: Node) =
    interface Impl<Node>

    [<Export>]
    member _.Speed = 1.0

  [<GenerateGodotNode("InvalidOverrideNode", typeof<Node>)>]
  type InvalidOverride(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride>]
    member _.MissingVirtualMethod() = ()

  [<GenerateGodotNode("Impl", typeof<Node>)>]
  type ReservedClassName(_node: Node) =
    interface Impl<Node>

  [<GenerateGodotNode("InvalidNamedNode", typeof<Node>)>]
  type InvalidNamedOverride(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride("MissingVirtualMethod")>]
    member _.Update() = ()

  [<GenerateGodotNode("MismatchedNamedNode", typeof<Node>)>]
  type MismatchedNamedOverride(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride("_Process")>]
    member _.Update(_delta: int) = ()

  [<GenerateGodotNode("DuplicateOverrideNode", typeof<Node>)>]
  type DuplicateOverrides(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride>]
    member _._Ready() = ()

    [<GenerateGodotOverride("_Ready")>]
    member _.OnReady() = ()

  [<GenerateGodotNode("EmptyOverrideNode", typeof<Node>)>]
  type EmptyOverrideName(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride("")>]
    member _.OnReady() = ()
