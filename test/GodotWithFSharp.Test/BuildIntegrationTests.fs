module GodotWithFSharp.Test.BuildIntegrationTests

open System
open System.IO
open System.Security
open NUnit.Framework
open GodotWithFSharp.Test

let private repository =
  Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

let private xml value = SecurityElement.Escape value

[<Test>]
let ``ordinary Godot builds generate fresh sources and remove stale sources`` () =
  let root =
    Path.Combine(
      Path.GetTempPath(),
      "GodotWithFSharp Build "
      + Guid.NewGuid().ToString("N")
    )

  let implementation = Path.Combine(root, "Implementation")
  let app = Path.Combine(root, "Godot")

  Directory.CreateDirectory implementation
  |> ignore

  Directory.CreateDirectory app |> ignore

  try
    let sdk = Path.Combine(repository, "global.json")

    if File.Exists sdk then
      File.Copy(sdk, Path.Combine(root, "global.json"))

    let runtime =
      xml (Path.Combine(repository, "src/libs/GodotWithFSharp/GodotWithFSharp.fsproj"))

    let targets = xml (Path.Combine(repository, "build/GodotWithFSharp.targets"))

    File.WriteAllText(
      Path.Combine(implementation, "Implementation.fsproj"),
      $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Impl.fs"/><ProjectReference Include="{runtime}"/></ItemGroup></Project>"""
    )

    File.WriteAllText(
      Path.Combine(app, "BuildProbe.csproj"),
      $"""<Project Sdk="Godot.NET.Sdk/4.6.2"><PropertyGroup><TargetFramework>net8.0</TargetFramework><GodotWithFSharpNamespace>BuildProbe.Generated</GodotWithFSharpNamespace></PropertyGroup><ItemGroup><ProjectReference Include="../Implementation/Implementation.fsproj" GodotWithFSharpImplementation="true"/></ItemGroup><Import Project="{targets}"/></Project>"""
    )

    File.WriteAllText(
      Path.Combine(app, "Probe.cs"),
      "public static class Probe { public static System.Type NodeType => typeof(BuildProbe.Generated.FreshNode); }"
    )

    File.WriteAllText(
      Path.Combine(app, "project.godot"),
      "config_version=5\n[application]\nconfig/name=\"BuildProbe\"\n"
    )

    let source = Path.Combine(implementation, "Impl.fs")

    let node name =
      $"""
[<GenerateGodotNode("{name}", typeof<Node>)>]
type {name}Impl(node: Node) =
  interface Impl<Node>
"""

    let content names =
      "namespace BuildProbe.Implementation\nopen Godot\nopen GodotWithFSharp\n"
      + (names
         |> List.map node
         |> String.concat "\n")

    File.WriteAllText(source, content [ "FreshNode"; "StaleNode" ])

    let build extra =
      let code, output =
        Processes.run
          180000
          root
          "dotnet"
          ([ "build"
             Path.Combine(app, "BuildProbe.csproj")
             "--nologo"
             "--verbosity"
             "minimal"
             "-m:1" ]
           @ extra)

      Assert.That(code, Is.Zero, output)
      Assert.That(output, Does.Not.Contain("CS2002"), output)
      output

    build [] |> ignore
    let generated = Path.Combine(app, "scripts/generated")
    let fresh = Path.Combine(generated, "FreshNode.cs")
    let stale = Path.Combine(generated, "StaleNode.cs")
    Assert.That(File.Exists fresh, Is.True)
    Assert.That(File.Exists stale, Is.True)
    let modified = File.GetLastWriteTimeUtc fresh
    build [ "--no-restore" ] |> ignore
    Assert.That(File.GetLastWriteTimeUtc fresh, Is.EqualTo modified)
    File.WriteAllText(source, content [ "FreshNode"; "AddedNode" ])
    build [ "--no-restore" ] |> ignore
    Assert.That(File.Exists stale, Is.False)
    Assert.That(File.Exists(Path.Combine(generated, "AddedNode.cs")), Is.True)
    File.AppendAllText(fresh, "// manual edit\n")

    let code, output =
      Processes.run
        180000
        root
        "dotnet"
        [ "build"
          Path.Combine(app, "BuildProbe.csproj")
          "--no-restore"
          "--nologo"
          "-m:1" ]

    Assert.That(code, Is.Not.Zero)
    Assert.That(output, Does.Contain("GFS203"))
    Assert.That(File.ReadAllText fresh, Does.EndWith("// manual edit\n"))
  finally
    Directory.Delete(root, true)
