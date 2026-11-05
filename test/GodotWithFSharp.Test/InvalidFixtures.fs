namespace GodotWithFSharp.Test

open System
open Godot
open GodotWithFSharp

module InvalidFixtures =
  type ExportWarningBase() =
    inherit Node()
    member val private Secret = 0 with get, set

  [<GenerateGodotNode("ShadowingExportsNode", typeof<ExportWarningBase>)>]
  type ShadowingExports(_node: ExportWarningBase) =
    interface Impl<ExportWarningBase>

    [<Export; GodotExportDefault(0)>]
    member val _Notification = 0 with get, set

    [<Export; GodotExportDefault("")>]
    member val Name = "" with get, set

    [<Export; GodotExportDefault(0)>]
    member val NotificationPredelete = 0 with get, set

    [<Export; GodotExportDefault(0)>]
    member val TreeEntered = 0 with get, set

    [<Export; GodotExportDefault(0)>]
    member val Secret = 0 with get, set

  [<GenerateGodotNode("BorrowedShadowingNode", typeof<Node>, DisposeImplementation = false)>]
  type BorrowedShadowing(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

    [<Export; GodotExportDefault(0)>]
    member val _Notification = 0 with get, set

  [<GenerateGodotNode("_Notification", typeof<Node>)>]
  type AutomaticClassCollision(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

  [<GenerateGodotNode("AutomaticPropertyCollisionNode", typeof<Node>)>]
  type AutomaticPropertyCollision(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

    [<Export; GodotExportDefault(0)>]
    member val _Notification = 0 with get, set

  [<GenerateGodotNode("_godotImplementationDisposed", typeof<Node>)>]
  type AutomaticFieldCollision(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

  [<GenerateGodotNode("_Notification", typeof<Node>, DisposeImplementation = false)>]
  type BorrowedNotificationName(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

  type PrivateAnnotatedBase(_node: Node) =
    interface Impl<Node>

    [<Export; GodotExportDefault(0)>]
    member val private Hidden = 0 with get, set

    [<GenerateGodotOverride("_Ready")>]
    member private _.Initialize() = ()

  [<GenerateGodotNode("PrivateMembersNode", typeof<Node>)>]
  type PrivateMembers(node: Node) =
    inherit PrivateAnnotatedBase(node)

    [<Export; GodotExportDefault(0)>]
    member val private Speed = 0 with get, set

    [<GenerateGodotOverride("_Ready")>]
    member private _.Ready() = ()

  [<GenerateGodotNode("CompatibilityNode", typeof<Node>)>]
  type GenericExport<'value>(_node: Node) =
    interface Impl<Node>

    [<Export; GodotExportDefault(null)>]
    member val Value = Unchecked.defaultof<'value> with get, set

  [<GenerateGodotNode("LiteralDefaultsNode", typeof<Node>)>]
  type LiteralDefaults(_node: Node) =
    interface Impl<Node>

    [<Export; GodotExportDefault(null)>]
    member val Text: string = null with get, set

    [<Export; GodotExportDefault(true)>]
    member val Flag = false with get, set

    [<Export; GodotExportDefault('x')>]
    member val Letter = ' ' with get, set

    [<Export; GodotExportDefault(255uy)>]
    member val Byte = 0uy with get, set

    [<Export; GodotExportDefault(-128y)>]
    member val SByte = 0y with get, set

    [<Export; GodotExportDefault(-32768s)>]
    member val Int16 = 0s with get, set

    [<Export; GodotExportDefault(65535us)>]
    member val UInt16 = 0us with get, set

    [<Export; GodotExportDefault(-2147483648)>]
    member val Int32 = 0 with get, set

    [<Export; GodotExportDefault(4294967295u)>]
    member val UInt32 = 0u with get, set

    [<Export; GodotExportDefault(-9223372036854775808L)>]
    member val Int64 = 0L with get, set

    [<Export; GodotExportDefault(18446744073709551615UL)>]
    member val UInt64 = 0UL with get, set

    [<Export; GodotExportDefault(1.25f)>]
    member val Single = 0.0f with get, set

    [<Export; GodotExportDefault(2.5)>]
    member val Double = 0.0 with get, set

    [<Export; GodotExportDefault(PropertyHint.Range)>]
    member val Enum = PropertyHint.None with get, set

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

  [<GenerateGodotNode("_Ready", typeof<Node>)>]
  type ClassCollision(_node: Node) =
    interface Impl<Node>

    [<GenerateGodotOverride("_Ready")>]
    member _.OnReady() = ()

  [<GenerateGodotNode("PropertyCollisionNode", typeof<Node>)>]
  type PropertyCollision(_node: Node) =
    interface Impl<Node>

    [<Export>]
    member val _Ready = 0 with get, set

    [<GenerateGodotOverride("_Ready")>]
    member _.OnReady() = ()

  [<GenerateGodotNode("UnsupportedExportNode", typeof<Node>)>]
  type UnsupportedExport(_node: Node) =
    interface Impl<Node>

    [<Export>]
    member val Value = DateTime.MinValue with get, set

  [<GenerateGodotNode("MissingDefaultNode", typeof<Node>)>]
  type MissingDefault(_node: Node) =
    interface Impl<Node>

    [<Export>]
    member val Speed = 180.0f with get, set

  [<GenerateGodotNode("WrongDefaultNode", typeof<Node>)>]
  type WrongDefault(_node: Node) =
    interface Impl<Node>

    [<Export; GodotExportDefault("wrong")>]
    member val Speed = 180.0f with get, set

  type ExplodingDefaultProvider =
    static member Value: float32 = failwith "Default providers must not run in the generator."

  [<GenerateGodotNode("MetadataOnlyDefaultNode", typeof<Node>)>]
  type MetadataOnlyDefault(_node: Node) =
    interface Impl<Node>

    [<Export; GodotExportDefaultProvider(typeof<ExplodingDefaultProvider>, "Value")>]
    member val Speed = 0.0f with get, set

  [<GenerateGodotNode("OwnedNode", typeof<Node>, Tool = true)>]
  type OwnedImplementation(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()

    [<GenerateGodotOverride("_Notification")>]
    member _.Notify(_what: int) = ()

  [<GenerateGodotNode("BorrowedNode", typeof<Node>, DisposeImplementation = false)>]
  type BorrowedImplementation(_node: Node) =
    interface Impl<Node>

    interface IDisposable with
      member _.Dispose() = ()
