# Build and generation

[Documentation home](../README.md) | [Get Started](get-started.md) | [Development and testing](development.md)

For submodules, use the MSBuild integration described in [Get Started](get-started.md#4-connect-the-godot-project). The CLI is available for build systems that need to invoke generation directly.

## Normal Godot builds

Import `build/GodotWithFSharp.targets` into the Godot C# project. Mark each implementation ProjectReference with `GodotWithFSharpImplementation="true"` and set `GodotWithFSharpNamespace`.

```text
Build implementation projects and generation tools
    -> obtain implementation DLL paths
    -> inspect metadata and validate the batch
    -> generate physical C# scripts
    -> refresh Compile items
    -> compile through Godot.NET.Sdk
```

Generation runs after project references are built and before `CoreCompile`. Refreshing Compile items includes files created on the first build and removes stale entries. DesignTimeBuild skips generation. Every normal generation checks ownership and hashes; unchanged source files retain their modification time.

| Property | Default or purpose |
| --- | --- |
| `GodotWithFSharpNamespace` | Required namespace for generated nodes |
| `GodotWithFSharpOutput` | `scripts/generated` under the Godot C# project; relative overrides use that project as their base |
| `GodotWithFSharpOwner` | The generated namespace |
| `GodotWithFSharpAllowEmpty` | `false`; allow an explicitly empty generation batch |
| `GodotWithFSharpAllowInputChange` | `false`; accept a changed set of input DLL names |
| `GodotWithFSharpAdoptExisting` | `false`; adopt legacy marked generated files |

Enable migration properties only for the intended migration. See [generated file ownership](#generated-file-ownership) before doing so.

## Why the build files are separate

The root `build.fsx` groups developer and CI commands. The `.props` and `.targets` files provide the entry points that MSBuild and the Godot editor can load directly.

| File | Responsibility |
| --- | --- |
| [GodotWithFSharp.props](../build/GodotWithFSharp.props) | Provide `GodotVersion=4.6.2` when no value is set |
| [GodotWithFSharp.Validation.targets](../build/GodotWithFSharp.Validation.targets) | Validate GodotSharp and Godot.SourceGenerators versions against `GodotVersion` during restore/build |
| [GodotWithFSharp.targets](../build/GodotWithFSharp.targets) | Build tools and references, select inputs, invoke generation, and refresh generated Compile items |
| [verify-distribution.fsx](../build/verify-distribution.fsx) | Verify the packaged CLI in a temporary installation; a developer/CI check |

The main targets file imports the default settings and version checks as needed. Within this repository, `Directory.Build.props` and `Directory.Build.targets` also provide shared settings and validation.

The [Generate MSBuild task](../src/tools/GodotWithFSharp.Build/Generate.cs) starts the CLI as a separate process with an argument list, captures UTF-8 output, and handles failures and timeouts. The [generator](../src/tools/GodotWithFSharp.CodeGenerator/) performs metadata analysis and C# generation.

## Generation warnings

`GFS018` warns when an exported property hides an accessible inherited method, property, field, event, or nested type. Inaccessible private base members do not produce this warning. This is independent of whether automatic implementation disposal is enabled.

The CLI prints warnings to standard error using `warning GFS018: <implementation.member>: <message>` and exits successfully if generation otherwise succeeds. It also reports warnings during `--dry-run`. The MSBuild integration forwards these as native build warnings, so they remain visible even when the C# hiding warning `CS0108` is suppressed. Fatal validation errors still stop generation before output.

When embedding the generator, use `Analysis.analyzeTypesWithWarnings` or `Metadata.readWithWarnings`. They return an `AnalysisResult` containing portable `Nodes` and `Warnings`; definition validation errors still raise `GenerationException`. The existing `analyzeTypes` and `read` helpers preserve their definitions-only return values.

```fsharp
let analysis = Metadata.readWithWarnings implementationPaths referencePaths
for warning in analysis.Warnings do
  printfn "%s %s: %s" warning.Code warning.Target warning.Message
let files = Generator.generateDefinitions generatedNamespace analysis.Nodes
```

## Configuration and compatibility

The supported baseline is .NET 8 / SDK 8.0.100 and Godot 4.6.2 .NET edition. Match the engine, GodotSharp, and Godot.NET.Sdk versions. The MSBuild checks compare the SDK's GodotSharp and source-generator package versions with `GodotVersion`; mismatches report `GFS200` or `GFS201`.

CI covers Windows/Linux x64 with the minimum and latest .NET 8 SDKs. Other operating systems, architectures, and Godot versions have not been validated. The engine is used to run generated nodes, the example, and engine integration tests; code generation and non-engine tests use restored managed assemblies.

Keep shared Godot settings in the consumer's root `GodotWithFSharp.props`, and import it from the consumer's `Directory.Build.props`. The submodule searches above its root for the nearest shared file. Set `GodotWithFSharpConfiguration` to choose a different file, or pass `-p:GodotVersion=...` for an explicit version override.

The submodule uses its own project target frameworks and internal dependency policy. Parent `Directory.Build.props` and `Directory.Packages.props` policy is isolated by default. For a temporary migration from the legacy inheritance behavior, pass `-p:GodotWithFSharpImportParentProps=true`; this also brings in parent items and package policy.

The repository's `global.json` selects .NET 8 SDKs, starting at 8.0.100 and rolling forward to the latest installed 8.0 feature band. Each internal F# project uses FSharp.Core 8.0.100. NuGet locks record direct and transitive dependencies; the Godot applications have separate Debug, ExportDebug, and ExportRelease locks. See [dependency maintenance](development.md#dependency-maintenance).

## Run the CLI directly

Build the implementation first. For the layout in Get Started, run these commands from the game repository root:

```shell
dotnet build libs/Game.Implementation/Game.Implementation.fsproj
dotnet run --project libs/GodotWithFSharp/src/tools/GodotWithFSharp.CodeGenerator -- --assembly libs/Game.Implementation/bin/Debug/net8.0/Game.Implementation.dll --namespace Game.Generated --output apps/Godot/scripts/generated
```

Paths are relative to the calling process's working directory. Use the implementation's build output together with its `.deps.json` and dependencies. The CLI resolves dependencies from sibling DLLs and restored NuGet assets described by `.deps.json`. Add `--reference <dependency.dll>` for dependencies elsewhere.

Repeat `--assembly` to generate from several DLLs in one batch. The namespace and output directory are required. The generator reads metadata without constructing Godot nodes, implementation instances, or custom attributes.

The FAKE `Gen` target wraps the same CLI and also accepts paths relative to the caller:

```shell
dotnet fsi libs/GodotWithFSharp/build.fsx --target Gen -- --assembly libs/Game.Implementation/bin/Debug/net8.0/Game.Implementation.dll --namespace Game.Generated --output apps/Godot/scripts/generated
```

## Generated file ownership

Use a dedicated directory for each owner and input batch. Pass all implementation DLLs for that directory in one invocation. `.godotwithfsharp.manifest.json` records the owner, input DLL names, and source hashes. The owner defaults to the namespace and can be set with `--owner`.

Generation rejects owner mismatches, collisions with handwritten files, and manual edits to owned sources. Unchanged files are retained. A nonempty batch can remove stale owned sources; removing the entire batch requires explicit permission through an option.

| CLI option | Purpose |
| --- | --- |
| `--dry-run` | Validate and display planned writes/deletions without writing |
| `--adopt-existing` | Adopt legacy files bearing the generator marker into a manifest |
| `--allow-empty` | Explicitly allow removing all owned generated sources |
| `--allow-input-change` | Explicitly accept a changed set of input DLL names |

Updates use temporary files and a recovery journal. After an interrupted transaction, the next normal generation restores the previous state before regenerating. If files were edited after the interruption, recovery stops and preserves the journal for inspection. Godot-owned `.uid` files are retained.

The CLI can be packaged as a .NET tool. The primary integration remains a submodule with a runtime ProjectReference and targets Import; the CLI package contains the generation tool rather than the MSBuild targets and task. Package verification is described in [development and testing](development.md#verify-the-cli-package).
