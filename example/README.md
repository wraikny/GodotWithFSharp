# Example

[Documentation home](../README.md) | [Get Started](../docs/get-started.md) | [Runtime API](../docs/runtime.md)

This example moves a rectangle with F#, bounces it at the window edges, and displays its speed and position. The **Reset position** button returns it to its initial position. It uses no external image assets.

```text
example/
  apps/Godot/                      Godot application and scenes
    scripts/generated/             Generated C# node scripts
  libs/Example.Implementation/      F# node behavior
```

## Build and run

Use a .NET 8 SDK, version 8.0.100 or later, and Godot 4.6.2 **.NET edition**. Run from the repository root:

```shell
dotnet build example/apps/Godot/Example.csproj
```

The equivalent FAKE target is `dotnet fsi build.fsx --target Example.Build`. The build compiles the F# implementation, generates C# scripts, and compiles the Godot application.

Open `example/apps/Godot/project.godot` in Godot and press F5. If the engine executable is available as `godot`, you can also run:

```shell
godot --path example/apps/Godot --editor
godot --path example/apps/Godot
```

Use `dotnet fsi build.fsx --target Example.Gen` to build references and generate scripts without compiling the Godot application.

## What to inspect

- [BouncerImpl.fs](libs/Example.Implementation/BouncerImpl.fs) declares an exported Speed default of `180.0f` and maps `Update` to Godot's `_Process` callback.
- [Main.tscn](apps/Godot/scenes/Main.tscn) saves a Speed override of `240` on the Bouncer node.
- [MainImpl.fs](libs/Example.Implementation/MainImpl.fs) obtains child implementations, connects the reset button, and displays status.
- [Example.csproj](apps/Godot/Example.csproj) marks the F# implementation reference and imports the generation targets.

Change the F# implementations and use a normal build, including the Godot editor's build, to regenerate scripts. Edit F# rather than the generated C# files. Follow [Get Started](../docs/get-started.md) to use this structure in another project.

## Headless smoke test

Replace `godot` with your .NET engine executable:

```shell
godot --headless --path example/apps/Godot --editor --import
godot --headless --path example/apps/Godot --quit-after 120 -- --smoke-test
```

Success prints `EXAMPLE_SMOKE_TEST_OK` after 30 frames and exits with code 0. The test checks the scene export value, movement, implementation lookup, and button-triggered reset. Check both the marker and the exit code; `--quit-after` bounds the run if the expected callback is never reached.
