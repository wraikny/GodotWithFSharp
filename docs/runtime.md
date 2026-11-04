# Runtime API

[Documentation home](../README.md) | [Get Started](get-started.md) | [Build and generation](build-and-generation.md)

Use `open GodotWithFSharp` to access the attributes, runtime contracts, and extension methods. A complete first implementation is shown in [Get Started](get-started.md#3-write-the-f-implementation).

## Nodes and callbacks

`[<GenerateGodotNode("PlayerNode", typeof<Node2D>)>]` declares a generated public partial C# node. The implementation must be a public, non-abstract, non-generic type with a public constructor accepting the declared Godot base type, and implement `Impl<Node2D>` for that same type.

The generated constructor creates the implementation and exposes it through its `Impl` property. Generated nodes implement `RuntimeObject<Node2D, PlayerImpl>`, which also provides the single-parameter `RuntimeObject<PlayerImpl>` contract.

| Declaration | Behavior |
| --- | --- |
| `[<GenerateGodotOverride>]` | Override the Godot method with the same name as the implementation method |
| `[<GenerateGodotOverride("_Process")>]` | Override `_Process` and forward to an implementation method such as `Update` |
| `GenerateGodotNode(..., Tool = true)` | Mark the generated node as a Godot tool script |

Annotated callbacks must be public instance methods. Their parameters and return types must exactly match a public virtual method on the Godot base type. Duplicate targets, generic methods, and ref/out parameters are rejected. Attributes on unsupported nonpublic members, including inherited private members, produce diagnostics rather than being silently ignored. Generated identifiers use ASCII letters, digits, and `_`; C# keywords are escaped.

## Export defaults

An exported F# property needs public instance getters and setters, a supported Godot Variant type, and exactly one default declaration.

An Export that hides an accessible inherited member produces warning `GFS018`. For example, an exported `_Notification` property hides Godot's notification method even when the implementation does not implement `IDisposable`. Generation continues, but renaming the property avoids the hiding. A collision with a member generated in the same class, such as the automatic disposal callback, remains a fatal `GFS015` diagnostic.

```fsharp
[<Export(PropertyHint.Range, "0,600,1")>]
[<GodotExportDefault(180.0f)>]
member val Speed = 180.0f with get, set
```

`GodotExportDefault` supplies both the generated node's editor default and the implementation's initial exported value. The constant must match the property type: use `180.0f` for `float32` and `180.0` for `double`. Missing, incompatible, or duplicate declarations are generation errors.

For a value that cannot be expressed as an attribute constant, use a public static property or a public static method with no parameters:

```fsharp
type Defaults =
  static member Offset = Vector2(12.0f, 34.0f)

// Inside an implementation class:
[<Export; GodotExportDefaultProvider(typeof<Defaults>, "Offset")>]
member val Offset = Vector2.Zero with get, set
```

The provider must return the exact property type. Keep it deterministic and free of side effects; create fresh mutable values rather than sharing them between nodes. The generator reads its metadata. Godot evaluates the provider when obtaining defaults or constructing a node.

Initialization follows this order:

```text
Construct the F# implementation
    -> apply declared defaults to its exported properties
    -> apply values saved in the scene
    -> EnterTree / Ready
```

Use `Ready` for initialization that depends on scene values or child nodes. Later assignments remain ordinary assignments: assigning `defaultof<_>` does not restore the declared default or a scene value. Generated getters reflect implementation-side changes, and generated setters forward values to the implementation.

## Implementation lifetime

Each node creates its implementation once in its constructor. Entering and leaving the scene tree does not recreate it. Godot's `EnterTree`, `ExitTree`, `Ready`, and `RequestReady()` behavior is preserved; scene initialization belongs in a forwarded `_Ready` method.

If the implementation implements `IDisposable`, the node owns its final disposal by default. The generated predelete notification calls `Dispose` once, after the implementation's own notification callback, using `finally` for cleanup. Leaving the tree does not dispose the implementation.

Set `GenerateGodotNode(..., DisposeImplementation = false)` to opt out of automatic disposal. An implementation's `Dispose` must not free its owning node again.

Automatic disposal requires an overridable public `_Notification(int)` on the base node. The generated notification callback and disposal field are included in name-collision validation; a sealed notification override or a conflicting class/property name fails before files are written.

## Find implementations and instantiate scenes

Use the declared Godot base type with the implementation type in two-parameter helpers. For the Get Started example, the pair is `Node2D, PlayerImpl`.

```fsharp
let player = root.GetRuntimeNode<Node2D, PlayerImpl>("Player")
let implementation = root.GetRuntimeImpl<Node2D, PlayerImpl>("Player")
let optionalImplementation = root.TryGetRuntimeImpl<Node2D, PlayerImpl>("Player")
```

| Method | Result |
| --- | --- |
| `node.GetUnsafeImpl<Implementation>()` | The implementation, or an exception if unavailable |
| `node.TryGetImpl<Implementation>()` | `Some` for a live, matching implementation; otherwise `None` |
| `node.GetNodeImpl<Implementation>(path)` | A child node's implementation, or an exception |
| `node.TryGetNodeImpl<Implementation>(path)` | An optional child implementation |
| `node.GetRuntimeNode<NodeType, Implementation>(path)` | A node whose type and runtime contract match |
| `node.GetRuntimeImpl<NodeType, Implementation>(path)` | The implementation after checking both types |
| `node.TryGetRuntimeNode<NodeType, Implementation>(path)` | The optional matching node |
| `node.TryGetRuntimeImpl<NodeType, Implementation>(path)` | The optional matching implementation |
| `scene.InstantiateWithImpl<NodeType, Implementation>()` | A new node and its implementation as a pair |
| `GD.LoadNodeWithImpl<NodeType, Implementation>(path)` | Load a PackedScene and instantiate the pair |

The `Try` lookup methods handle missing, freed, or incompatible nodes with `None`. The throwing lookup methods report a contract failure. `GetRuntimeNode` checks the `RuntimeObject<NodeType, Implementation>` contract as well as the node type.

Scene helpers require `Implementation :> Impl<NodeType>`. If the instantiated root does not satisfy the requested contract, the created root is freed before the exception is raised.
