# Design

[Documentation home](../README.md) | [Get Started](get-started.md) | [Build and generation](build-and-generation.md)

## Purpose and boundaries

GodotWithFSharp extracts Godot/F# interop and C# node generation into components that can be reused independently of a game project. Extraction work is scoped to this repository; the original game project is a read-only reference.

F# classes hold behavior and state. Thin generated C# nodes bridge them to Godot's script, export, and callback infrastructure. Physical scripts are compiled through Godot.NET.Sdk and its own source generators.

## Generation pipeline

```text
Implementation DLLs
    -> MetadataLoadContext inspection
    -> portable node definitions and batch validation
    -> Roslyn syntax trees
    -> C# source files and an ownership manifest
```

`Definitions.fs` holds the portable model. `Metadata.fs` isolates input assembly inspection from the tool's execution context. `Analysis.fs` validates a complete batch before output, and `CSharpSyntax.fs` and `Generator.fs` construct C# syntax with Roslyn's `SyntaxFactory`.

`Generator.generateSyntax` exposes a syntax tree before string conversion. Formatting uses `NormalizeWhitespace` and `ToFullString`. Roslyn is a NuGet dependency of the generator; runtime consumers need the runtime library and their F# implementation.

The CLI and generation API use custom-attribute metadata instead of executing attribute constructors. Default providers are inspected but execute only when Godot evaluates defaults or creates a node.

## Runtime contracts

An implementation is constructed once per node. Declared export defaults are then applied to it, followed by Godot's serialized scene values. Scene-dependent initialization uses a forwarded `_Ready` method. Ordinary later assignments are not rewritten into defaults.

Tree callbacks preserve Godot's lifecycle, including reentry and `RequestReady()`. There is no additional initialization callback. An owned `IDisposable` implementation is disposed once at final node deletion, rather than tree exit; the generated callback composes user notifications with cleanup in `finally`. Ownership and tool execution are configurable through node attributes.

Runtime helpers check node validity and implementation contracts. Scene-instantiation failures release the newly created root before raising an exception. See the [runtime API](runtime.md) for usage and constraints.

## Ownership and updates

`Output.fs` manages generation plans, owner/input identity, content hashes, temporary writes, and interrupted-update recovery. Handwritten collisions and manually edited generated sources are rejected. Empty batches, input-set changes, and legacy adoption require explicit options. Godot owns `.uid` files.

Each output directory belongs to one owner and one input batch. All generated files are rendered and validated before writes begin. See [generated file ownership](build-and-generation.md#generated-file-ownership) for operational details.

## Build and configuration boundaries

The Godot C# project references the F# implementation, which references the runtime library. MSBuild builds inputs before generation and adds generated sources before C# compilation. The CLI runs separately from MSBuild and keeps generation available to other build systems.

Consumers share Godot settings through an explicit props file. Internal framework and dependency policy is kept separate from parent project policy. SDK/NuGet version checks, pinned dependencies, project lock files, engine integration tests, and package-install checks make the supported baseline verifiable.

For review, focus on the build connection, export initialization order, lifecycle/disposal, complete-batch validation, and output ownership/recovery. [Development and testing](development.md) maps these contracts to tests and CI.
