# Godot with F#

Write Godot node behavior in F# and generate the C# node scripts through the normal Godot build.

The library provides the runtime contracts and attributes. The generator creates thin C# nodes that forward exported properties and Godot callbacks to your F# implementation.

## Get Started

**[Use GodotWithFSharp in your project](docs/get-started.md)**

The guide covers adding the submodule, sharing the Godot version, writing an F# implementation, connecting the Godot project, and attaching your first generated script.

To try the bundled example, run this from the repository root:

```shell
dotnet build example/apps/Godot/Example.csproj
```

Then open `example/apps/Godot/project.godot` with Godot .NET. See the [example guide](example/README.md) for running and testing it.

## Requirements

| Component | Minimum supported version |
| --- | --- |
| .NET runtime | 8.0 |
| .NET SDK | 8.0.100; repository builds use .NET 8 SDKs |
| Godot | 4.6.2, .NET edition |

Projects target `net8.0`. Keep the Godot engine, GodotSharp, and Godot.NET.Sdk versions aligned. See [configuration and compatibility](docs/build-and-generation.md#configuration-and-compatibility) for the version policy.

## Documentation

| Guide | Use it to... |
| --- | --- |
| [Get Started](docs/get-started.md) | Integrate the library into a Godot project |
| [Runtime API](docs/runtime.md) | Declare nodes, exports, callbacks, and implementation lifetimes |
| [Build and generation](docs/build-and-generation.md) | Configure MSBuild, use the CLI, and manage generated files |
| [Development and testing](docs/development.md) | Build the repository, run tests, and verify tool packages |
| [Design](docs/design.md) | Understand the architecture and review its contracts |

## AI attribution

The implementation of the libraries, generator, tests, and example includes code generated with **OpenAI Codex**, using **GPT-6.1 Sol (`gpt-6.1-sol`)**.

## License

[Apache License 2.0](LICENSE).
