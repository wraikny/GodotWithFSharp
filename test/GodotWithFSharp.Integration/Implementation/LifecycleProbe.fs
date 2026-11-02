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
