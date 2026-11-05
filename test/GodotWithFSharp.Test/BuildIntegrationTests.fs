module GodotWithFSharp.Test.BuildIntegrationTests

open System
open System.IO
open System.Security
open NUnit.Framework
open GodotWithFSharp.Test

let private repository =
  Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

let private xml value = SecurityElement.Escape value

[<TestCase("Project"); TestCase("Solution"); TestCase("ReferenceProperties"); TestCase("Unresolved")>]
let ``ordinary Godot builds use resolved configurations and update generated sources`` mode =
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

    let referenceProperties =
      match mode with
      | "ReferenceProperties" -> " AdditionalProperties=\"Configuration=Release\""
      | "Unresolved" -> " ReferenceOutputAssembly=\"false\""
      | _ -> ""

    File.WriteAllText(
      Path.Combine(app, "BuildProbe.csproj"),
      $"""<Project Sdk="Godot.NET.Sdk/4.6.2"><PropertyGroup><TargetFramework>net8.0</TargetFramework><GodotWithFSharpNamespace>BuildProbe.Generated</GodotWithFSharpNamespace></PropertyGroup><ItemGroup><ProjectReference Include="../Implementation/Implementation.fsproj" GodotWithFSharpImplementation="true"{referenceProperties}/></ItemGroup><Import Project="{targets}"/></Project>"""
    )

    let solution = Path.Combine(root, "Consumer.sln")

    if mode = "Solution" then
      File.WriteAllText(
        solution,
        """
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "Implementation", "Implementation/Implementation.fsproj", "{11111111-1111-1111-1111-111111111111}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "BuildProbe", "Godot/BuildProbe.csproj", "{22222222-2222-2222-2222-222222222222}"
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Release|Any CPU
    {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Release|Any CPU
    {22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {22222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
  EndGlobalSection
EndGlobal
"""
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

    if
      mode = "Solution"
      || mode = "ReferenceProperties"
    then
      // A stale Debug DLL must never substitute for the resolved Release implementation.
      File.WriteAllText(source, content [ "WrongConfigurationNode" ])

      let code, output =
        Processes.run
          180000
          root
          "dotnet"
          [ "build"
            Path.Combine(implementation, "Implementation.fsproj")
            "--configuration"
            "Debug"
            "--nologo"
            "-m:1" ]

      Assert.That(code, Is.Zero, output)

    File.WriteAllText(source, content [ "FreshNode"; "StaleNode" ])

    let buildTarget =
      if mode = "Solution" then
        solution
      else
        Path.Combine(app, "BuildProbe.csproj")

    if mode = "Unresolved" then
      let code, output =
        Processes.run 180000 root "dotnet" [ "build"; buildTarget; "--nologo"; "-m:1" ]

      Assert.That(code, Is.Not.Zero, output)
      Assert.That(output, Does.Contain("GFS206"), output)
      Assert.That(Directory.Exists(Path.Combine(app, "scripts/generated")), Is.False)
    else
      let build extra =
        let code, output =
          Processes.run
            180000
            root
            "dotnet"
            ([ "build"; buildTarget; "--nologo"; "--verbosity"; "minimal"; "-m:1" ]
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
      Assert.That(File.Exists(Path.Combine(generated, "WrongConfigurationNode.cs")), Is.False)
      let modified = File.GetLastWriteTimeUtc fresh
      build [ "--no-restore" ] |> ignore
      Assert.That(File.GetLastWriteTimeUtc fresh, Is.EqualTo modified)
      File.WriteAllText(source, content [ "FreshNode"; "AddedNode" ])
      build [ "--no-restore" ] |> ignore
      Assert.That(File.Exists stale, Is.False)
      Assert.That(File.Exists(Path.Combine(generated, "AddedNode.cs")), Is.True)
      File.AppendAllText(fresh, "// manual edit\n")

      let code, output =
        Processes.run 180000 root "dotnet" [ "build"; buildTarget; "--no-restore"; "--nologo"; "-m:1" ]

      Assert.That(code, Is.Not.Zero)
      Assert.That(output, Does.Contain("GFS203"))
      Assert.That(File.ReadAllText fresh, Does.EndWith("// manual edit\n"))
  finally
    Directory.Delete(root, true)
