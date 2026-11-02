#!dotnet fsi

#r "nuget: Fake.Core.Target, 6.2.0"
#r "nuget: Fake.DotNet.Cli, 6.2.0"
#r "nuget: Fake.IO.FileSystem, 6.2.0"

open System
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO.Globbing.Operators

// Boilerplate shared with other FAKE build scripts.
Environment.GetCommandLineArgs()
|> Array.skip 2
|> Array.toList
|> Context.FakeExecutionContext.Create false __SOURCE_FILE__
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

Target.initEnvironment ()

let args =
  Target.getArguments ()
  |> Option.defaultValue [||]

let root = __SOURCE_DIRECTORY__
let solution = IO.Path.Combine(root, "GodotWithFSharp.sln")

let generator =
  IO.Path.Combine(root, "src/tools/GodotWithFSharp.CodeGenerator/GodotWithFSharp.CodeGenerator.fsproj")

let configuration () =
  match
    args
    |> Array.tryHead
    |> Option.map _.ToLowerInvariant()
  with
  | None
  | Some "debug" -> DotNet.BuildConfiguration.Debug
  | Some "release" -> DotNet.BuildConfiguration.Release
  | Some value -> invalidArg "configuration" value

let runDotnet command arguments =
  let result =
    DotNet.exec
      id
      command
      (arguments
       |> Arguments.ofList
       |> Arguments.toWindowsCommandLine)

  if not result.OK then
    failwithf "dotnet %s failed (exit %d)." command result.ExitCode

// Use the CLI directly so newer SDK binary logs do not depend on FAKE's bundled log reader.
Target.create "Build" (fun _ -> runDotnet "build" [ solution; "--configuration"; string (configuration ()) ])
Target.create "Test" (fun _ -> runDotnet "test" [ solution; "--configuration"; string (configuration ()) ])

Target.create
  "Gen"
  (fun _ ->
    runDotnet
      "run"
      ([ "--project"; generator; "--" ]
       @ Array.toList args)
  )

let sources () =
  !!(IO.Path.Combine(root, "src/**/*.fs"))
  ++ (IO.Path.Combine(root, "test/**/*.fs"))
  ++ (IO.Path.Combine(root, "example/**/*.fs"))
  ++ (IO.Path.Combine(root, "build.fsx"))
  -- (IO.Path.Combine(root, "**/obj/**/*.fs"))
  -- (IO.Path.Combine(root, "**/bin/**/*.fs"))
  |> Seq.toList

let exampleImplementation =
  IO.Path.Combine(root, "example/libs/Example.Implementation/Example.Implementation.fsproj")

let exampleApp = IO.Path.Combine(root, "example/apps/Godot/Example.csproj")

Target.create
  "Example.Implementation"
  (fun _ -> runDotnet "build" [ exampleImplementation; "--configuration"; string (configuration ()) ])

Target.create
  "Example.Gen"
  (fun _ ->
    runDotnet
      "msbuild"
      [ exampleApp
        "-restore"
        "-target:GenerateGodotWithFSharp"
        $"-property:Configuration={configuration ()}" ]
  )

Target.create "Example.Build" (fun _ -> runDotnet "build" [ exampleApp; "--configuration"; string (configuration ()) ])

Target.create
  "Integration.Build"
  (fun _ ->
    runDotnet
      "build"
      [ IO.Path.Combine(root, "test/GodotWithFSharp.Integration/Godot/Integration.csproj")
        "--configuration"
        string (configuration ()) ]
  )

Target.create "Format.FSharp" (fun _ -> runDotnet "fantomas" (sources ()))
Target.create "Format.Check.FSharp" (fun _ -> runDotnet "fantomas" ("--check" :: sources ()))
Target.create "Format" ignore
Target.create "Format.Check" ignore
"Format.FSharp" ==> "Format"
"Format.Check.FSharp" ==> "Format.Check"
Target.runOrDefaultWithArguments "Build"
