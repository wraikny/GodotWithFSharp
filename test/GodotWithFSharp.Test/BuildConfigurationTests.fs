module GodotWithFSharp.Test.BuildConfigurationTests

open System
open System.IO
open System.Text.Json
open NUnit.Framework
open GodotWithFSharp.Test

let private repository =
  Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

let private withConsumer action =
  let root =
    Path.Combine(Path.GetTempPath(), "GodotWithFSharp.Configuration", Guid.NewGuid().ToString("N"))

  let moduleRoot = Path.Combine(root, "libs", "GodotWithFSharp")
  let libraryRoot = Path.Combine(moduleRoot, "src", "libs", "GodotWithFSharp")

  Directory.CreateDirectory(libraryRoot)
  |> ignore

  Directory.CreateDirectory(Path.Combine(moduleRoot, "build"))
  |> ignore

  File.Copy(Path.Combine(repository, "Directory.Build.props"), Path.Combine(moduleRoot, "Directory.Build.props"))

  File.Copy(
    Path.Combine(repository, "build", "GodotWithFSharp.props"),
    Path.Combine(moduleRoot, "build", "GodotWithFSharp.props")
  )

  let project = Path.Combine(libraryRoot, "Probe.fsproj")

  File.WriteAllText(
    project,
    """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="GodotSharp" Version="$(GodotVersion)" /></ItemGroup></Project>"""
  )

  try
    action root project
  finally
    Directory.Delete(root, true)

let private evaluate root project extra =
  let code, output =
    Processes.run
      60000
      root
      "dotnet"
      ([ "msbuild"
         project
         "-nologo"
         "-getProperty:GodotVersion,TargetFramework,ConsumerMarker"
         "-getItem:PackageReference" ]
       @ extra)

  Assert.That(code, Is.Zero, output)
  JsonDocument.Parse output

[<Test>]
let ``submodules share explicit configuration without importing parent build policy`` () =
  withConsumer (fun root project ->
    let parent =
      """<Project><PropertyGroup><GodotVersion>4.5.1</GodotVersion><TargetFramework>net10.0</TargetFramework><ConsumerMarker>inherited</ConsumerMarker></PropertyGroup><ItemGroup><PackageReference Include="ParentOnlyPolicy" Version="1.0" /></ItemGroup></Project>"""

    File.WriteAllText(Path.Combine(root, "Directory.Build.props"), parent)
    use standalone = evaluate root project []

    Assert.That(
      standalone.RootElement
        .GetProperty("Properties")
        .GetProperty("GodotVersion")
        .GetString(),
      Is.EqualTo("4.6.2")
    )

    Assert.That(
      standalone.RootElement
        .GetProperty("Properties")
        .GetProperty("ConsumerMarker")
        .GetString(),
      Is.Empty
    )

    Assert.That(standalone.RootElement.ToString(), Does.Not.Contain("ParentOnlyPolicy"))
    let configuration = Path.Combine(root, "GodotWithFSharp.props")

    File.WriteAllText(
      configuration,
      """<Project><PropertyGroup><GodotVersion>4.7.2</GodotVersion></PropertyGroup></Project>"""
    )

    use shared = evaluate root project []

    Assert.That(
      shared.RootElement
        .GetProperty("Properties")
        .GetProperty("GodotVersion")
        .GetString(),
      Is.EqualTo("4.7.2")
    )

    Assert.That(
      shared.RootElement
        .GetProperty("Properties")
        .GetProperty("TargetFramework")
        .GetString(),
      Is.EqualTo("net8.0")
    )

    Assert.That(shared.RootElement.ToString(), Does.Not.Contain("ParentOnlyPolicy"))
    use overridden = evaluate root project [ "-p:GodotVersion=4.6.2" ]

    Assert.That(
      overridden.RootElement
        .GetProperty("Properties")
        .GetProperty("GodotVersion")
        .GetString(),
      Is.EqualTo("4.6.2")
    )

    File.Delete configuration
    use legacy = evaluate root project [ "-p:GodotWithFSharpImportParentProps=true" ]

    Assert.That(
      legacy.RootElement
        .GetProperty("Properties")
        .GetProperty("GodotVersion")
        .GetString(),
      Is.EqualTo("4.5.1")
    )

    Assert.That(
      legacy.RootElement
        .GetProperty("Properties")
        .GetProperty("ConsumerMarker")
        .GetString(),
      Is.EqualTo("inherited")
    )
  )

[<Test>]
let ``Godot SDK version mismatches fail before compilation`` () =
  let project = Path.Combine(repository, "example", "apps", "Godot", "Example.csproj")

  let arguments =
    [ "msbuild"; project; "-nologo"; "-t:ValidateGodotWithFSharpVersion" ]

  let code, output = Processes.run 60000 repository "dotnet" arguments
  Assert.That(code, Is.Zero, output)

  let code, output =
    Processes.run 60000 repository "dotnet" (arguments @ [ "-p:GodotVersion=4.7.2" ])

  Assert.That(code, Is.Not.Zero)
  Assert.That(output, Does.Contain("GFS200"))
