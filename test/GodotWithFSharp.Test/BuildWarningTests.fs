module GodotWithFSharp.Test.BuildWarningTests

open System
open System.IO
open System.Security
open NUnit.Framework
open GodotWithFSharp.Test

[<Test>]
let ``Godot builds report hiding exports as native MSBuild warnings`` () =
  let repository = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

  let root =
    Path.Combine(
      Path.GetTempPath(),
      "GodotWithFSharp Warnings "
      + Guid.NewGuid().ToString("N")
    )

  let implementation = Path.Combine(root, "Implementation")
  let app = Path.Combine(root, "Godot")

  Directory.CreateDirectory implementation
  |> ignore

  Directory.CreateDirectory app |> ignore

  try
    File.Copy(Path.Combine(repository, "global.json"), Path.Combine(root, "global.json"))

    let runtime =
      SecurityElement.Escape(Path.Combine(repository, "src/libs/GodotWithFSharp/GodotWithFSharp.fsproj"))

    let targets =
      SecurityElement.Escape(Path.Combine(repository, "build/GodotWithFSharp.targets"))

    File.WriteAllText(
      Path.Combine(implementation, "Implementation.fsproj"),
      $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Implementation.fs"/><ProjectReference Include="{runtime}"/></ItemGroup></Project>"""
    )

    File.WriteAllText(
      Path.Combine(implementation, "Implementation.fs"),
      """
namespace WarningProbe
open Godot
open GodotWithFSharp
[<GenerateGodotNode("WarningNode", typeof<Node>)>]
type WarningImpl(_node: Node) =
  interface Impl<Node>
  [<Export; GodotExportDefault(0)>]
  member val _Notification = 0 with get, set
"""
    )
    // Suppress the C# warning: the generator must still provide its own native MSBuild warning.
    File.WriteAllText(
      Path.Combine(app, "Warnings.csproj"),
      $"""<Project Sdk="Godot.NET.Sdk/4.6.2"><PropertyGroup><TargetFramework>net8.0</TargetFramework><NoWarn>$(NoWarn);CS0108</NoWarn><GodotWithFSharpNamespace>WarningProbe.Generated</GodotWithFSharpNamespace></PropertyGroup><ItemGroup><ProjectReference Include="../Implementation/Implementation.fsproj" GodotWithFSharpImplementation="true"/></ItemGroup><Import Project="{targets}"/></Project>"""
    )

    File.WriteAllText(Path.Combine(app, "project.godot"), "config_version=5\n[application]\nconfig/name=\"Warnings\"\n")
    let warningLog = Path.Combine(root, "warnings.log")

    let code, output =
      Processes.run
        180000
        root
        "dotnet"
        [ "build"
          Path.Combine(app, "Warnings.csproj")
          "--nologo"
          "--verbosity"
          "minimal"
          "-m:1"
          $"-flp:LogFile={warningLog};WarningsOnly;Verbosity=normal" ]

    Assert.That(code, Is.Zero, output)
    Assert.That(output, Does.Contain("warning GFS018:"))
    let warnings = File.ReadAllText warningLog
    Assert.That(warnings, Does.Contain("GFS018").And.Contain("WarningProbe.WarningImpl._Notification"))
    Assert.That(warnings, Does.Not.Contain("CS0108"))
    Assert.That(File.Exists(Path.Combine(app, "scripts/generated/WarningNode.cs")), Is.True)
  finally
    Directory.Delete(root, true)
