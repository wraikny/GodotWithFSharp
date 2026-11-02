namespace GodotWithFSharp

open System

[<Sealed; AllowNullLiteral; AttributeUsage(AttributeTargets.Class)>]
type GenerateGodotNode(name: string, ty: Type) =
  inherit Attribute()
  member _.GetGodotName() = name
  member _.GetGodotType() = ty
  member val Tool = false with get, set
  member val DisposeImplementation = true with get, set

/// Forwards a Godot virtual method to this implementation method.
/// Omit the name to override a Godot method with the same name.
///
/// [Overridable functions - Godot Docs](https://docs.godotengine.org/en/stable/tutorials/scripting/overridable_functions.html)
[<Sealed; AllowNullLiteral; AttributeUsage(AttributeTargets.Method)>]
type GenerateGodotOverride(methodName: string) =
  inherit Attribute()
  new() = GenerateGodotOverride(null)
  member _.GetGodotName() = methodName

open Godot

/// A public static property or parameterless method that returns a fresh, deterministic default.
/// The generator reads metadata only; the provider runs when Godot evaluates defaults or creates a node.
[<Sealed; AttributeUsage(AttributeTargets.Property)>]
type GodotExportDefaultProvider(declaringType: Type, memberName: string) =
  inherit Attribute()
  member _.DeclaringType = declaringType
  member _.MemberName = memberName

[<Interface>]
type Impl =
  interface
  end

[<Interface>]
type Impl<'node> =
  inherit Impl

[<Interface>]
type RuntimeObject<'impl when 'impl :> Impl> =
  abstract Impl: 'impl with get

[<Interface>]
type RuntimeObject<'node, 'impl when 'node :> Node and 'impl :> Impl<'node>> =
  inherit RuntimeObject<'impl>

[<AutoOpen>]
module Extension =
  type Node with

    member this.GetUnsafeImpl<'impl when 'impl :> Impl>() =
      this
      |> unbox<RuntimeObject<'impl>>
      |> _.Impl

    member this.GetNodeImpl<'impl when 'impl :> Impl>(path: NodePath) =
      this.GetNode<Node>(path).GetUnsafeImpl<'impl>()

    member this.GetRuntimeNode<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      this.GetNode<'node>(path)

    member this.GetRuntimeImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      this.GetNode<'node>(path)
      |> unbox<RuntimeObject<'node, 'impl>>
      |> _.Impl

  type PackedScene with

    member this.InstantiateWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl>() =
      let node = this.Instantiate() |> unbox<'node>
      node, node.GetUnsafeImpl<'impl>()

  type GD with

    static member LoadNodeWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl>
      (path: NodePath)
      =
      GD.Load<PackedScene>(path).InstantiateWithImpl<'node, 'impl>()
