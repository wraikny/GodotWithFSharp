open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Text

let repository = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let configuration =
  fsi.CommandLineArgs
  |> Array.tryItem 1
  |> Option.defaultValue "Debug"

let temporary =
  Path.Combine(
    Path.GetTempPath(),
    "GodotWithFSharp Distribution "
    + Guid.NewGuid().ToString("N")
  )

let run executable arguments environment =
  let start = ProcessStartInfo(executable)
  start.WorkingDirectory <- repository
  start.UseShellExecute <- false
  start.CreateNoWindow <- true
  start.RedirectStandardOutput <- true
  start.RedirectStandardError <- true
  start.StandardOutputEncoding <- Encoding.UTF8
  start.StandardErrorEncoding <- Encoding.UTF8

  for argument in arguments do
    start.ArgumentList.Add argument

  for name, value in environment do
    start.Environment[name] <- value

  use child = Process.Start start
  let output = child.StandardOutput.ReadToEndAsync()
  let errors = child.StandardError.ReadToEndAsync()

  if not (child.WaitForExit(180000)) then
    child.Kill(true)
    child.WaitForExit()
    failwith $"Distribution check timed out: {executable}\n{output.Result}\n{errors.Result}"

  let message = output.Result + errors.Result

  if child.ExitCode <> 0 then
    failwith message

  message

Directory.CreateDirectory temporary
|> ignore

try
  let project =
    Path.Combine(repository, "src/tools/GodotWithFSharp.CodeGenerator/GodotWithFSharp.CodeGenerator.fsproj")

  let version =
    (run "dotnet" [ "msbuild"; project; "-nologo"; "-getProperty:PackageVersion" ] [])
      .Trim()

  let packages = Path.Combine(temporary, "packages")

  run
    "dotnet"
    [ "pack"
      project
      "--no-restore"
      "--configuration"
      configuration
      "--output"
      packages
      "--nologo" ]
    []
  |> printfn "%s"

  let checkPackage () =
    use package =
      ZipFile.OpenRead(
        Directory.GetFiles(packages, "*.nupkg")
        |> Array.exactlyOne
      )

    for entry in package.Entries do
      if
        entry.Name = "GodotSharp.dll"
        || entry.Name = "GodotWithFSharp.dll"
      then
        failwith $"The CLI package unexpectedly contains runtime contracts: {entry.FullName}"

  checkPackage ()
  let config = Path.Combine(temporary, "NuGet.Config")

  File.WriteAllText(
    config,
    $"""<configuration><packageSources><clear/><add key="local" value="{System.Security.SecurityElement.Escape packages}"/></packageSources></configuration>"""
  )

  let tools = Path.Combine(temporary, "tools")

  run
    "dotnet"
    [ "tool"
      "install"
      "GodotWithFSharp.CodeGenerator"
      "--version"
      version
      "--tool-path"
      tools
      "--configfile"
      config ]
    [ "NUGET_PACKAGES", Path.Combine(temporary, "tool-cache") ]
  |> printfn "%s"

  let executable =
    Path.Combine(
      tools,
      "godot-fsharp-gen"
      + (if OperatingSystem.IsWindows() then ".exe" else "")
    )

  let fixture =
    Path.Combine(
      repository,
      $"test/GodotWithFSharp.Test/Fixtures/bin/{configuration}/net8.0/GodotWithFSharp.Test.Fixtures.dll"
    )

  let output = Path.Combine(temporary, "generated")

  let arguments =
    [ "--assembly"
      fixture
      "--namespace"
      "Distribution.Generated"
      "--output"
      output ]

  run executable arguments []
  |> printfn "%s"

  let sources = Directory.GetFiles(output, "*.cs")

  if
    sources.Length <> 2
    || not (File.Exists(Path.Combine(output, ".godotwithfsharp.manifest.json")))
  then
    failwith "Installed tool did not generate the expected fixture batch."

  let modified =
    sources
    |> Array.map File.GetLastWriteTimeUtc

  let planned = run executable (arguments @ [ "--dry-run" ]) []

  if
    planned.Contains("WRITE ")
    || planned.Contains("DELETE ")
  then
    failwith planned

  if
    (sources
     |> Array.map File.GetLastWriteTimeUtc)
    <> modified
  then
    failwith "Dry run modified generated files."

  printfn "DISTRIBUTION_OK: packed and installed tool %s generated the fixture batch." version
finally
  Directory.Delete(temporary, true)
