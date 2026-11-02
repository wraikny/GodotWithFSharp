module GodotWithFSharp.Test.GodotIntegrationTests

open System
open System.IO
open NUnit.Framework
open GodotWithFSharp.Test

let private repository =
  Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

[<Test; Category("Godot")>]
let ``Godot editor defaults scene values lifecycle and runtime helpers work together`` () =
  let engine = Environment.GetEnvironmentVariable "GODOT_BIN"

  if String.IsNullOrWhiteSpace engine then
    Assert.Ignore "Set GODOT_BIN to a Godot 4.6.2 .NET executable to run engine integration tests."

  Assert.That(File.Exists engine, Is.True, "GODOT_BIN must point to an executable.")
  let code, version = Processes.run 30000 repository engine [ "--version" ]
  Assert.That(code, Is.Zero, version)
  Assert.That(version.Trim(), Does.StartWith("4.6.2.stable.mono"), version)

  let logs =
    Path.Combine(
      Path.GetTempPath(),
      "GodotWithFSharp Engine "
      + Guid.NewGuid().ToString("N")
    )

  Directory.CreateDirectory logs |> ignore

  try
    let run app name extra marker =
      let code, output =
        Processes.run
          120000
          repository
          engine
          ([ "--headless"; "--path"; app; "--log-file"; Path.Combine(logs, name + ".log") ]
           @ extra)

      Assert.That(code, Is.Zero, output)
      Assert.That(output, Does.Not.Contain("ERROR:"), output)

      if not (String.IsNullOrEmpty marker) then
        Assert.That(output, Does.Contain marker, output)

      TestContext.Progress.WriteLine output

    let build path =
      let code, output =
        Processes.run 180000 repository "dotnet" [ "build"; path; "--nologo"; "--verbosity"; "minimal"; "-m:1" ]

      Assert.That(code, Is.Zero, output)

    let example = Path.Combine(repository, "example/apps/Godot")
    build (Path.Combine(example, "Example.csproj"))
    run example "example-import" [ "--editor"; "--import" ] ""

    run
      example
      "export-defaults"
      [ "--script"
        Path.Combine(__SOURCE_DIRECTORY__, "Godot/ExportDefaults.gd")
        "--quit-after"
        "120" ]
      "EXPORT_DEFAULTS_OK"

    run example "example-smoke" [ "--quit-after"; "120"; "--"; "--smoke-test" ] "EXAMPLE_SMOKE_TEST_OK"
    let integration = Path.Combine(repository, "test/GodotWithFSharp.Integration/Godot")
    build (Path.Combine(integration, "Integration.csproj"))
    run integration "integration-import" [ "--editor"; "--import" ] ""
    run integration "lifecycle" [ "--script"; "res://Lifecycle.gd"; "--quit-after"; "120" ] "LIFECYCLE_OK"
  finally
    Directory.Delete(logs, true)
