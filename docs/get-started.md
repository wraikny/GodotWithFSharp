# Get Started

[Documentation home](../README.md) | [Runtime API](runtime.md) | [Build and generation](build-and-generation.md)

This guide connects an F# implementation project to a Godot C# project using a submodule. At the end, an ordinary Godot build generates a `PlayerNode.cs` script that you can attach to a `Node2D`.

## Before you begin

Install a .NET 8 SDK, version 8.0.100 or later, and **Godot 4.6.2 .NET edition**. Use a Godot C# project for the application.

The examples below assume this layout. Run shell commands from `my-game/`; paths inside project files are relative to those files.

```text
my-game/
  Directory.Build.props
  GodotWithFSharp.props
  global.json
  apps/Godot/
    Game.csproj
    project.godot
  libs/
    GodotWithFSharp/                Git submodule
    Game.Implementation/
      Game.Implementation.fsproj
      PlayerImpl.fs
```

If you want to see a working application first, follow the [bundled example](../example/README.md).

## 1. Add the submodule

```shell
git submodule add https://github.com/wraikny/GodotWithFSharp.git libs/GodotWithFSharp
```

For an existing checkout that already declares the submodule:

```shell
git submodule update --init --recursive
```

Commit the submodule reference in your game repository to pin the revision you use.

## 2. Share the Godot version

Create `GodotWithFSharp.props` at the game repository root:

```xml
<Project>
  <PropertyGroup>
    <GodotVersion>4.6.2</GodotVersion>
  </PropertyGroup>
</Project>
```

Import it from the game's root `Directory.Build.props`. If that file already exists, add the Import to it:

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)GodotWithFSharp.props" />
</Project>
```

The submodule finds the nearest `GodotWithFSharp.props` above its root. This shares the Godot version without importing the game's entire build or package-management policy.

To select a .NET 8 SDK when other major SDK versions are also installed, use this root `global.json`, or merge its SDK policy into your existing file:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

Keep the engine and the `Godot.NET.Sdk` version in the C# project aligned with `GodotVersion`. See [configuration and compatibility](build-and-generation.md#configuration-and-compatibility) for overrides and migration settings.

## 3. Write the F# implementation

Create `libs/Game.Implementation/Game.Implementation.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="PlayerImpl.fs" />
    <ProjectReference Include="../GodotWithFSharp/src/libs/GodotWithFSharp/GodotWithFSharp.fsproj" />
  </ItemGroup>
</Project>
```

Create `libs/Game.Implementation/PlayerImpl.fs`:

```fsharp
namespace Game.Implementation

open Godot
open GodotWithFSharp

[<GenerateGodotNode("PlayerNode", typeof<Node2D>)>]
type PlayerImpl(node: Node2D) =
  interface Impl<Node2D>

  [<Export(PropertyHint.Range, "0,600,1")>]
  [<GodotExportDefault(180.0f)>]
  member val Speed = 180.0f with get, set

  [<GenerateGodotOverride("_Process")>]
  member this.Update(delta: double) =
    node.Position <- node.Position + Vector2(this.Speed * float32 delta, 0.0f)
```

This declares a generated C# `PlayerNode` that inherits `Node2D`. Its exported `Speed` property forwards to the implementation, and Godot's `_Process` callback calls `PlayerImpl.Update`.

Use the same Godot node type in the attribute, constructor, and `Impl<Node2D>` contract. Exported properties require public getters and setters, and an explicit [default declaration](runtime.md#export-defaults).

Keep the constructor lightweight. Initialize state that depends on scene values or child nodes in a method marked `[<GenerateGodotOverride("_Ready")>]`.

## 4. Connect the Godot project

Add the namespace, marked implementation reference, and targets Import to `apps/Godot/Game.csproj`. Preserve any other settings and references your project already needs.

```xml
<Project Sdk="Godot.NET.Sdk/4.6.2">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <GodotWithFSharpNamespace>Game.Generated</GodotWithFSharpNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../libs/Game.Implementation/Game.Implementation.fsproj"
                      GodotWithFSharpImplementation="true" />
  </ItemGroup>
  <Import Project="../../libs/GodotWithFSharp/build/GodotWithFSharp.targets" />
</Project>
```

`GodotWithFSharpImplementation="true"` selects the DLLs to inspect. Mark additional implementation references if you want to generate from several projects in the same batch.

The dependency direction is **Godot C# project -> F# implementation -> GodotWithFSharp runtime library**. Keep this direction to avoid a build cycle.

## 5. Build and attach the node

```shell
dotnet build apps/Godot/Game.csproj
```

The build compiles the implementation and tools, generates C# scripts, and compiles the Godot application. On the first build, it also includes the newly generated files in that same compilation.

Expected generated output:

```text
apps/Godot/scripts/generated/
  PlayerNode.cs
  .godotwithfsharp.manifest.json
```

Open `apps/Godot/project.godot` with Godot .NET. Add a `Node2D` to a scene and attach `res://scripts/generated/PlayerNode.cs` to it. The Inspector exposes `Speed` with a default of `180`; a value saved in the scene overrides that default.

Run the scene. The node moves to the right; add a visual child if you want to see the movement. The [bundled example](../example/README.md) includes a visible moving rectangle and a reset button.

Subsequent builds, including the Godot editor's normal build, regenerate scripts when your implementation changes. Edit the F# source; generated files are managed through their manifest and hashes.

## Where to go next

- [Runtime API](runtime.md): callbacks, default providers, lifecycle, and node lookup.
- [Build and generation](build-and-generation.md): output settings, CLI usage, ownership, and recovery.
- [Development and testing](development.md): repository targets and engine integration tests.
