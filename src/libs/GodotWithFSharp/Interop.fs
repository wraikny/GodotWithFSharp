namespace GodotWithFSharp

open System

/// <summary>Generates a C# Godot node that constructs and forwards calls to this F# implementation.</summary>
/// <param name="name">The generated C# class name, which also determines the script filename.</param>
/// <param name="ty">A public, closed, inheritable Godot Node type with an accessible parameterless constructor.</param>
/// <remarks>The implementation must implement Impl for that node type and have a public constructor accepting it.</remarks>
[<Sealed; AllowNullLiteral; AttributeUsage(AttributeTargets.Class)>]
type GenerateGodotNode(name: string, ty: Type) =
  inherit Attribute()
  /// Gets the generated class name.
  member _.GetGodotName() = name
  /// Gets the generated node's Godot base type.
  member _.GetGodotType() = ty
  /// Adds Godot's Tool attribute to the generated node. Defaults to false.
  member val Tool = false with get, set
  /// Disposes an IDisposable implementation once on final node deletion. Defaults to true; tree exit does not dispose it.
  member val DisposeImplementation = true with get, set

/// <summary>Forwards a public Godot virtual method to this public instance implementation method.</summary>
/// <param name="methodName">The Godot method to override. Null uses the implementation method's name.</param>
/// <remarks>Parameter and return types must match the Godot method. This attribute adds no separate initialization hook.</remarks>
[<Sealed; AllowNullLiteral; AttributeUsage(AttributeTargets.Method)>]
type GenerateGodotOverride(methodName: string) =
  inherit Attribute()
  /// Forwards the Godot method with the same name as the annotated implementation method.
  new() = GenerateGodotOverride(null)
  /// Gets the Godot callback name, or null when the implementation method's name is used.
  member _.GetGodotName() = methodName

open Godot

/// <summary>Declares the generated node's editor default and the implementation's initial exported value.</summary>
/// <param name="value">A supported attribute literal matching the property type, or null for a reference type.</param>
/// <remarks>Defaults are applied after implementation construction and before serialized scene values.
/// Use exactly one default declaration per Export; complex defaults use GodotExportDefaultProvider.</remarks>
[<Sealed; AttributeUsage(AttributeTargets.Property)>]
type GodotExportDefault(value: obj) =
  inherit Attribute()
  /// Gets the declared attribute literal.
  member _.Value = value

/// <summary>Declares a public static property or parameterless method that supplies an Export default.</summary>
/// <param name="declaringType">A public, closed type containing the provider.</param>
/// <param name="memberName">An unambiguous member returning exactly the exported property's type.</param>
/// <remarks>Return a fresh, deterministic value without scene dependencies. The generator reads metadata only;
/// the provider runs when Godot evaluates defaults or creates a node, and may run more than once.</remarks>
[<Sealed; AttributeUsage(AttributeTargets.Property)>]
type GodotExportDefaultProvider(declaringType: Type, memberName: string) =
  inherit Attribute()
  /// Gets the type declaring the static provider.
  member _.DeclaringType = declaringType
  /// Gets the static property or parameterless method name.
  member _.MemberName = memberName

/// Marker shared by all GodotWithFSharp implementation objects.
[<Interface>]
type Impl =
  interface
  end

/// <summary>Associates an implementation with its generated node's base type.</summary>
/// <typeparam name="node">The Godot node base type accepted by the implementation constructor.</typeparam>
/// <remarks>This marker does not expose a node instance or enforce runtime object identity.</remarks>
[<Interface>]
type Impl<'node when 'node :> Node> =
  inherit Impl

/// <summary>Exposes an implementation without specifying its associated node type.</summary>
/// <remarks>Generated nodes expose one instance for their lifetime. Custom implementations should return a stable instance.</remarks>
[<Interface>]
type RuntimeObject<'impl when 'impl :> Impl> =
  /// Gets the implementation. Lookup helpers treat null as unavailable; getter exceptions propagate.
  abstract Impl: 'impl with get

/// <summary>Associates the exposed implementation with a Godot node base type.</summary>
/// <remarks>Runtime lookup helpers also verify that the node instance has the requested node type.</remarks>
[<Interface>]
type RuntimeObject<'node, 'impl when 'node :> Node and 'impl :> Impl<'node>> =
  inherit RuntimeObject<'impl>

/// Helpers for live implementation lookup and typed scene instantiation.
[<AutoOpen>]
module Extension =
  let private tryRuntime<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
    (parent: Node)
    (path: NodePath)
    =
    if not (GodotObject.IsInstanceValid parent) then
      None
    else
      match box (parent.GetNodeOrNull<Node>(path)) with
      | :? 'node as child when GodotObject.IsInstanceValid child ->
        match box child with
        | :? RuntimeObject<'node, 'impl> as runtime ->
          let impl = runtime.Impl

          if isNull (box impl) then
            None
          else
            Some(struct (child, impl))
        | _ -> None
      | _ -> None

  type Node with

    /// <summary>Looks up an implementation using the implementation-only RuntimeObject contract.</summary>
    /// <returns>None for a freed/null node, a missing or incompatible contract, or a null implementation.</returns>
    /// <remarks>This does not verify the node type or track arbitrary disposal of custom implementations.
    /// The getter is read once; getter exceptions propagate.</remarks>
    member this.TryGetImpl<'impl when 'impl :> Impl>() : 'impl option =
      if not (GodotObject.IsInstanceValid this) then
        None
      else
        match box this with
        | :? RuntimeObject<'impl> as runtime ->
          let impl = runtime.Impl
          if isNull (box impl) then None else Some impl
        | _ -> None

    /// <summary>Gets an implementation using the implementation-only contract.</summary>
    /// <exception cref="System.InvalidOperationException">The node is not live or does not expose a nonnull matching implementation.</exception>
    /// <remarks>Use GetRuntimeImpl when the associated node type must also be checked. Getter exceptions propagate.</remarks>
    member this.GetUnsafeImpl<'impl when 'impl :> Impl>() =
      match this.TryGetImpl<'impl>() with
      | Some impl -> impl
      | None -> invalidOp $"Node does not expose a live implementation of {typeof<'impl>.FullName}."

    /// <summary>Looks up a child implementation using the implementation-only contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <returns>None if this node is not live, the path is missing, or the child has no matching live implementation.</returns>
    /// <remarks>Godot path errors and custom getter exceptions propagate.</remarks>
    member this.TryGetNodeImpl<'impl when 'impl :> Impl>(path: NodePath) =
      if not (GodotObject.IsInstanceValid this) then
        None
      else
        let child = this.GetNodeOrNull<Node>(path)
        if isNull child then None else child.TryGetImpl<'impl>()

    /// <summary>Gets a child implementation using the implementation-only contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <exception cref="System.InvalidOperationException">The path has no live node exposing a nonnull matching implementation.</exception>
    member this.GetNodeImpl<'impl when 'impl :> Impl>(path: NodePath) =
      match this.TryGetNodeImpl<'impl>(path) with
      | Some impl -> impl
      | None -> invalidOp $"Node at '{path}' does not expose an implementation of {typeof<'impl>.FullName}."

    /// <summary>Looks up a live child with the requested node type and matching typed RuntimeObject contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <returns>None if the parent/child is unavailable, either type contract fails, or the implementation is null.</returns>
    /// <remarks>The implementation getter is read once. Godot path errors and getter exceptions propagate.</remarks>
    member this.TryGetRuntimeNode<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      : 'node option =
      tryRuntime<'node, 'impl> this path
      |> Option.map (fun struct (node, _) -> node)

    /// <summary>Gets a live child with the requested node type and matching typed implementation contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <exception cref="System.InvalidOperationException">No live child exposes the requested node/implementation contract and a nonnull implementation.</exception>
    member this.GetRuntimeNode<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      match this.TryGetRuntimeNode<'node, 'impl>(path) with
      | Some node -> node
      | None ->
        invalidOp
          $"Node at '{path}' does not implement RuntimeObject<{typeof<'node>.FullName}, {typeof<'impl>.FullName}>."

    /// <summary>Looks up a child's implementation after checking both the node type and typed RuntimeObject contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <returns>None if the parent/child is unavailable, either type contract fails, or the implementation is null.</returns>
    /// <remarks>The implementation getter is read once. Godot path errors and getter exceptions propagate.</remarks>
    member this.TryGetRuntimeImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      tryRuntime<'node, 'impl> this path
      |> Option.map (fun struct (_, impl) -> impl)

    /// <summary>Gets a child's implementation after checking both its node type and typed RuntimeObject contract.</summary>
    /// <param name="path">A Godot node path relative to this node.</param>
    /// <exception cref="System.InvalidOperationException">No live child exposes the requested contract and a nonnull implementation.</exception>
    member this.GetRuntimeImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      match tryRuntime<'node, 'impl> this path with
      | Some struct (_, impl) -> impl
      | None ->
        invalidOp
          $"Node at '{path}' does not implement RuntimeObject<{typeof<'node>.FullName}, {typeof<'impl>.FullName}>."


  type PackedScene with

    /// <summary>Instantiates a scene and validates its root node type and typed implementation contract.</summary>
    /// <returns>The root and its implementation. The caller owns the root and must manage its lifetime.</returns>
    /// <exception cref="System.InvalidOperationException">The instantiated root has an incompatible type/contract or a null implementation.</exception>
    /// <remarks>A failed validation frees the newly created root. Instantiation and getter errors propagate.</remarks>
    member this.InstantiateWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>() =
      let instance = this.Instantiate()

      try
        match box instance with
        | :? RuntimeObject<'node, 'impl> as runtime ->
          let impl = runtime.Impl

          match box instance with
          | :? 'node as node when not (isNull (box impl)) -> node, impl
          | _ -> invalidOp $"Scene root is not {typeof<'node>.FullName}."
        | _ ->
          invalidOp $"Scene root does not expose RuntimeObject<{typeof<'node>.FullName}, {typeof<'impl>.FullName}>."
      with _ ->
        if GodotObject.IsInstanceValid instance then
          instance.Free()

        reraise ()

  type GD with

    /// <summary>Loads a PackedScene resource and instantiates its typed root and implementation.</summary>
    /// <param name="path">A resource path identifying a loadable PackedScene.</param>
    /// <returns>The root and its implementation. The caller owns the root and must manage its lifetime.</returns>
    /// <exception cref="System.InvalidOperationException">The instantiated root fails the requested node/implementation contract.</exception>
    /// <remarks>Loading and instantiation failures propagate. Failed root validation frees the newly created root.</remarks>
    static member LoadNodeWithImpl<'node, 'impl when 'node :> Node and 'node: not struct and 'impl :> Impl<'node>>
      (path: NodePath)
      =
      GD.Load<PackedScene>(path).InstantiateWithImpl<'node, 'impl>()
