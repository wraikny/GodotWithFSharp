#!dotnet fsi

#r "netstandard"
#r "nuget: MSBuild.StructuredLogger"
#r "nuget: Fake.Core"
#r "nuget: Fake.Core.Target"
#r "nuget: Fake.IO.FileSystem"
#r "nuget: Fake.DotNet.Cli"
#r "nuget: FSharp.Json"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.Globbing.Operators

// Boilerplate
System.Environment.GetCommandLineArgs()
|> Array.skip 2 // skip fsi.exe; build.fsx
|> Array.toList
|> Context.FakeExecutionContext.Create false __SOURCE_FILE__
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

module private Params =
  /// オートフォーマッターを使って自動整形する場合の対象を指定する
  module FormatTargets =
    let csharp = !! "*.csproj"

    let fsharp =
      !! "libs/**/*.fs" ++ "build.fsx"
      -- "libs/*/obj/**/*.fs"
      -- "libs/*/bin/**/*.fs"

open Fake.DotNet
open FSharp.Json

[<AutoOpen>]
module private Utils =
  let getConfiguration (input: string option) =
    input
    |> Option.map (fun s -> s.ToLower())
    |> function
      | Some "debug" -> DotNet.BuildConfiguration.Debug
      | Some "release" -> DotNet.BuildConfiguration.Release
      | Some c -> failwithf "Invalid configuration '%s'" c
      | None -> DotNet.BuildConfiguration.Debug

  let dotnet' proj cmd =
    Printf.kprintf (fun arg ->
      let res = DotNet.exec proj cmd arg

      if not res.OK then
        let msg = res.Messages |> String.concat "\n"

        failwithf "Failed to run 'dotnet %s %s' due to: %A" cmd arg msg
    )

  let dotnet cmd = dotnet' id cmd

  let shell (dir: string option) cmd =
    Printf.kprintf (fun arg ->
      let dir = dir |> Option.defaultValue "."

      let res = Shell.Exec(cmd, arg, dir)

      if res <> 0 then
        failwithf "Failed to run '%s %s' at '%A'" cmd arg dir
    )

Target.initEnvironment ()

let args = Target.getArguments ()

Target.create "Build" (fun _ ->
  let conf =
    args
    |> Option.bind Array.tryHead
    |> getConfiguration

  !! "./*.*proj"
  ++ "libs/**/*.*proj"
  |> Seq.iter (DotNet.build (fun p -> { p with Configuration = conf }))
)

Target.create
  "Format.CSharp"
  (fun _ ->
    Params.FormatTargets.csharp
    |> Seq.iter (fun proj -> dotnet "format" "%s -v diag" proj)
  )

Target.create
  "Format.Check.CSharp"
  (fun _ ->
    Params.FormatTargets.csharp
    |> Seq.iter (fun proj -> dotnet "format" "%s -v diag --verify-no-changes" proj)
  )

Target.create
  "Format.FSharp"
  (fun _ ->
    Params.FormatTargets.fsharp
    |> String.concat " "
    |> dotnet "fantomas" "%s"
  )

Target.create
  "Format.Check.FSharp"
  (fun _ ->
    Params.FormatTargets.fsharp
    |> String.concat " "
    |> dotnet "fantomas" "--check %s"
  )

Target.create "Format" ignore

Target.create "Format.Check" ignore

"Format.CSharp" ==> "Format"
"Format.Check.CSharp" ==> "Format.Check"
"Format.FSharp" ==> "Format"
"Format.Check.FSharp" ==> "Format.Check"

(* localにインストールされている.NET CLI Toolのバージョンをまとめて更新する *)
Target.create
  "Tool.Update"
  (fun _ ->
    let content = File.readAsString ".config/dotnet-tools.json"

    let manifest: {| tools: Map<string, {| version: string |}> |} =
      Json.deserialize content

    for x in manifest.tools do
      dotnet "tool" "update %s" x.Key
  )

Target.runOrDefaultWithArguments "Build"
