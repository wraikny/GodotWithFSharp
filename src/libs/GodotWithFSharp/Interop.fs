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
type Impl<'node when 'node :> Node> =
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

    /// None also covers freed nodes and custom runtime objects returning a null implementation.
    member this.TryGetImpl<'impl when 'impl :> Impl>() : 'impl option =
      if not (GodotObject.IsInstanceValid this) then
        None
      else
        match box this with
        | :? RuntimeObject<'impl> as runtime when not (isNull (box runtime.Impl)) -> Some runtime.Impl
        | _ -> None

    member this.GetUnsafeImpl<'impl when 'impl :> Impl>() =
      match this.TryGetImpl<'impl>() with
      | Some impl -> impl
      | None -> invalidOp $"Node does not expose a live implementation of {typeof<'impl>.FullName}."

    member this.TryGetNodeImpl<'impl when 'impl :> Impl>(path: NodePath) =
      if not (GodotObject.IsInstanceValid this) then
        None
      else
        let child = this.GetNodeOrNull<Node>(path)
        if isNull child then None else child.TryGetImpl<'impl>()

    member this.GetNodeImpl<'impl when 'impl :> Impl>(path: NodePath) =
      match this.TryGetNodeImpl<'impl>(path) with
      | Some impl -> impl
      | None -> invalidOp $"Node at '{path}' does not expose an implementation of {typeof<'impl>.FullName}."

    member this.TryGetRuntimeNode<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      : 'node option =
      if not (GodotObject.IsInstanceValid this) then
        None
      else
        match box (this.GetNodeOrNull<Node>(path)) with
        | :? 'node as child when
          (box child :? RuntimeObject<'node, 'impl>)
          && GodotObject.IsInstanceValid child
          && Option.isSome (child.TryGetImpl<'impl>())
          ->
          Some child
        | _ -> None

    member this.GetRuntimeNode<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      match this.TryGetRuntimeNode<'node, 'impl>(path) with
      | Some node -> node
      | None ->
        invalidOp
          $"Node at '{path}' does not implement RuntimeObject<{typeof<'node>.FullName}, {typeof<'impl>.FullName}>."

    member this.TryGetRuntimeImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      this.TryGetRuntimeNode<'node, 'impl>(path)
      |> Option.bind (fun node -> node.TryGetImpl<'impl>())

    member this.GetRuntimeImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      this.GetRuntimeNode<'node, 'impl>(path).GetUnsafeImpl<'impl>()

  type PackedScene with

    member this.InstantiateWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>() =
      let instance = this.Instantiate()

      try
        match box instance with
        | :? RuntimeObject<'node, 'impl> as runtime ->
          match box instance with
          | :? 'node as node when not (isNull (box runtime.Impl)) -> node, runtime.Impl
          | _ -> invalidOp $"Scene root is not {typeof<'node>.FullName}."
        | _ ->
          invalidOp $"Scene root does not expose RuntimeObject<{typeof<'node>.FullName}, {typeof<'impl>.FullName}>."
      with _ ->
        if GodotObject.IsInstanceValid instance then
          instance.Free()

        reraise ()

  type GD with

    static member LoadNodeWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      GD.Load<PackedScene>(path).InstantiateWithImpl<'node, 'impl>()
