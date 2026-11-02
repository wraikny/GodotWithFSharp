namespace Integration.Implementation

open System
open Godot
open GodotWithFSharp

type private Counters =
  static member val Disposed = 0 with get, set
  static member val OrderValid = true with get, set

[<GenerateGodotNode("LifecycleProbe", typeof<Node>, Tool = true)>]
type LifecycleProbeImpl(_node: Node) =
  let mutable enters = 0
  let mutable exits = 0
  let mutable readies = 0
  let mutable disposed = false
  interface Impl<Node>

  [<Export; System.ComponentModel.DefaultValue(0)>]
  member _.EnterCount
    with get () = enters
    and set (_value: int) = ()

  [<Export; System.ComponentModel.DefaultValue(0)>]
  member _.ExitCount
    with get () = exits
    and set (_value: int) = ()

  [<Export; System.ComponentModel.DefaultValue(0)>]
  member _.ReadyCount
    with get () = readies
    and set (_value: int) = ()

  [<Export; System.ComponentModel.DefaultValue(0)>]
  member _.DisposedCount
    with get () = Counters.Disposed
    and set (_value: int) = ()

  [<Export; System.ComponentModel.DefaultValue(true)>]
  member _.OrderValid
    with get () = Counters.OrderValid
    and set (_value: bool) = ()

  [<GenerateGodotOverride("_EnterTree")>]
  member _.Enter() = enters <- enters + 1

  [<GenerateGodotOverride("_ExitTree")>]
  member _.Exit() = exits <- exits + 1

  [<GenerateGodotOverride("_Ready")>]
  member _.Ready() = readies <- readies + 1

  [<GenerateGodotOverride("_Notification")>]
  member _.Notify(what: int) =
    if what = int GodotObject.NotificationPredelete then
      Counters.OrderValid <- Counters.OrderValid && not disposed

  interface IDisposable with
    member _.Dispose() =
      if disposed then
        failwith "Dispose was called twice."

      disposed <- true
      Counters.Disposed <- Counters.Disposed + 1

[<GenerateGodotNode("BorrowedProbe", typeof<Node>, DisposeImplementation = false)>]
type BorrowedProbeImpl(_node: Node) =
  interface Impl<Node>

  interface IDisposable with
    member _.Dispose() =
      Counters.Disposed <- Counters.Disposed + 100

type private Expected2DImpl() =
  interface Impl<Node2D>

[<GenerateGodotNode("HelperProbe", typeof<Node>)>]
type HelperProbeImpl(node: Node) =
  let mutable valid = false
  interface Impl<Node>

  [<Export; System.ComponentModel.DefaultValue(false)>]
  member _.Valid
    with get () = valid
    and set (_value: bool) = ()

  [<GenerateGodotOverride("_Ready")>]
  member this.Check() =
    let plain = new Node2D(Name = "Plain")
    node.AddChild plain
    use selfPath = new NodePath(string node.Name)

    valid <-
      Option.isNone (plain.TryGetImpl<HelperProbeImpl>())
      && Option.isNone (node.TryGetNodeImpl<HelperProbeImpl>("Missing"))
      && Option.isNone (node.TryGetRuntimeNode<Node, HelperProbeImpl>("Plain"))
      && Option.isNone (node.TryGetRuntimeNode<Node, HelperProbeImpl>("Missing"))
      && Option.isNone (node.TryGetRuntimeImpl<Node, HelperProbeImpl>("Plain"))
      && Option.isNone (node.TryGetRuntimeNode<Node2D, Expected2DImpl>("."))
      && Option.isNone (node.TryGetImpl<BorrowedProbeImpl>())
      && obj.ReferenceEquals(this, node.GetParent().GetRuntimeImpl<Node, HelperProbeImpl>(selfPath))
      && obj.ReferenceEquals(node, node.GetParent().GetRuntimeNode<Node, HelperProbeImpl>(selfPath))

    try
      node.GetRuntimeNode<Node, HelperProbeImpl>("Plain")
      |> ignore

      valid <- false
    with :? InvalidOperationException ->
      ()

    plain.Free()

    valid <-
      valid
      && Option.isNone (plain.TryGetImpl<HelperProbeImpl>())

    use scene = new PackedScene()
    let root = new Node()
    scene.Pack(root) |> ignore
    root.Free()

    try
      scene.InstantiateWithImpl<Node, HelperProbeImpl>()
      |> ignore

      valid <- false
    with :? InvalidOperationException ->
      ()
