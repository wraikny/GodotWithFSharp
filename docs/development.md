# Development and testing

[Documentation home](../README.md) | [Get Started](get-started.md) | [Design](design.md)

Run the commands on this page from the repository root. Use a .NET 8 SDK, version 8.0.100 or later. Engine tests and the example use Godot 4.6.2 .NET edition.

## Build, test, and format

```shell
dotnet restore GodotWithFSharp.sln --locked-mode
dotnet build GodotWithFSharp.sln --no-restore
dotnet test GodotWithFSharp.sln --no-restore
dotnet tool restore
dotnet fsi build.fsx --target Format.Check
```

`dotnet fsi build.fsx --target Format` formats F# sources and handwritten C# whitespace/encoding. Generated C# is formatted by Roslyn and managed by the output manifest. Build and Test also have FAKE wrappers; append `-- Release` to choose the Release configuration.

| Target | Purpose |
| --- | --- |
| `Build` / `Test` | Build or test the solution |
| `Format` / `Format.Check` | Format or check F# sources and handwritten C# whitespace/encoding |
| `Example.Build` | Build the example through normal Godot/MSBuild generation |
| `Example.Gen` | Build references and generate example scripts without compiling the Godot app |
| `Integration.Build` | Build the engine integration project |
| `Gen` | Forward arguments to the generator CLI |
| `Distribution.Verify` | Build the solution and verify the packaged CLI |

## What the tests cover

| Location | Purpose |
| --- | --- |
| `test/GodotWithFSharp.Test` | Generator validation, syntax, output ownership/recovery, configuration, separate CLI processes, and real C# compilation |
| `test/GodotWithFSharp.Test/Fixtures` | A separately compiled F# input assembly for DLL inspection and generation tests |
| `test/GodotWithFSharp.Integration` | F# implementations, generated C# scripts, and Godot lifecycle/runtime-helper checks |
| `test/GodotWithFSharp.Test/Godot` | Engine-side checks of example export defaults and serialized scene values |

The standard suite also creates a fresh consumer project to check first-build generation, incremental output, additions/removals, and protection against manual edits.

## Run Godot engine tests

Set `GODOT_BIN` to the absolute path of the Godot 4.6.2 .NET executable. On Windows, use the console executable:

```powershell
$env:GODOT_BIN = 'C:/Tools/Godot/Godot_v4.6.2-stable_mono_win64_console.exe'
dotnet test GodotWithFSharp.sln --filter TestCategory=Godot
```

With `GODOT_BIN` unset, the engine test is skipped. An invalid path or incompatible engine version fails the test. Run `dotnet test GodotWithFSharp.sln` with the variable set to include engine coverage.

Engine checks cover headless editor import, simple and Vector2 defaults, scene overrides, example movement/reset, tree reentry, RequestReady, final disposal, and implementation lookup. They require successful exit codes and success markers, and reject engine errors or timeouts.

## Default-value regression coverage

The ordinary suite compiles generated C# and checks negative numeric constants, negative enum values, and finite fractional defaults. Floating-point assertions compare the IEEE bits as well as numeric equality, including positive and negative zero for both `float32` and `double`. These cases run without NUnit `Explicit` exclusions.

## Verify the CLI package

```shell
dotnet fsi build.fsx --target Distribution.Verify
```

After building, this target packs the CLI, checks that GodotSharp and the runtime library have not been bundled into it, and installs it using a temporary local feed and a separate package cache. The installed tool generates the fixture batch and performs a dry run; generated files must remain unchanged by that dry run. Temporary files are removed on completion.

This verifies local packaging and installation. It does not publish packages to an external feed or register a global tool.

## Dependency maintenance

FSharp.Core is fixed at 8.0.100 in the repository's internal settings. Direct and transitive NuGet dependencies are recorded in project lock files. Godot app locks are configuration-specific because their package sets differ.

When updating a dependency, restore the affected projects and configurations, review their lock-file changes, and verify restoration with `--locked-mode`. Keep the Godot engine, GodotSharp, and Godot.NET.Sdk aligned.

The [Verify workflow](../.github/workflows/verify.yml) covers Windows/Linux x64 with SDK 8.0.100 and the latest 8.0 SDK. It restores locked dependencies, runs formatting and engine tests, builds ExportDebug/ExportRelease, verifies the tool package, and checks for generated-source or lock-file changes.

## Contributing with Codex

Follow [AGENTS.md](../AGENTS.md). Before a Codex commit, the user must approve both the complete staged diff and the exact commit message. Changes to either require renewed approval. Include the required co-author trailer with the actual Codex model name.
