module GodotWithFSharp.CodeGenerator.Program

open System
open System.IO
open System.Reflection

let private usage =
  """Usage: godot-fsharp-gen --assembly <implementation.dll> [--assembly <other.dll> ...]
                        --namespace <Generated.Namespace> --output <directory>
Use --reference <dependency.dll> for dependencies outside the build output.
Paths are relative to the caller's working directory. Build implementation assemblies first.
Use one dedicated output directory per owner. Options:
  --owner <id>             Stable generation owner (defaults to namespace).
  --dry-run                Validate and print changes without writing.
  --adopt-existing         Migrate legacy generated files into a manifest.
  --allow-empty            Explicitly allow removing all owned generated files.
  --allow-input-change     Explicitly accept a changed input assembly set."""

let run (args: string[]) =
  if
    args = [| "--help" |]
    || args = [| "-h" |]
  then
    printfn "%s" usage
    0
  else
    try
      let references = ResizeArray<string>()
      let mutable options = Output.defaults
      let mutable owner = None
      let mutable dryRun = false

      let rec parse assemblies ns output =
        function
        | "--owner" :: value :: rest ->
          owner <- Some value
          parse assemblies ns output rest
        | "--allow-empty" :: rest ->
          options <- { options with AllowEmpty = true }
          parse assemblies ns output rest
        | "--allow-input-change" :: rest ->
          options <- { options with AllowInputChange = true }
          parse assemblies ns output rest
        | "--adopt-existing" :: rest ->
          options <- { options with AdoptExisting = true }
          parse assemblies ns output rest
        | "--dry-run" :: rest ->
          dryRun <- true
          parse assemblies ns output rest
        | "--reference" :: value :: rest ->
          references.Add(Path.GetFullPath value)
          parse assemblies ns output rest
        | "--assembly" :: value :: rest -> parse (Path.GetFullPath(value) :: assemblies) ns output rest
        | "--namespace" :: value :: rest when Option.isNone ns -> parse assemblies (Some value) output rest
        | "--output" :: value :: rest when Option.isNone output ->
          parse assemblies ns (Some(Path.GetFullPath(value))) rest
        | [] when
          not (List.isEmpty assemblies)
          && Option.isSome ns
          && Option.isSome output
          ->
          List.rev assemblies, Option.get ns, Option.get output
        | _ -> invalidArg "args" usage

      let paths, ns, output = parse [] None None (Array.toList args)

      for path in paths do
        if not (File.Exists path) then
          raise (FileNotFoundException("Implementation assembly not found. Build it first.", path))

      let analysis = Metadata.readWithWarnings paths references

      for warning in analysis.Warnings do
        eprintfn "warning %s: %s: %s" warning.Code warning.Target warning.Message

      let files = Generator.generateDefinitions ns analysis.Nodes

      options <-
        { options with
            Owner = Option.defaultValue ns owner
            Inputs =
              paths
              |> List.map Path.GetFileName
              |> List.distinct
              |> List.sort
              |> List.toArray }

      let changes =
        if dryRun then
          Output.plan options output files
        else
          Output.write options output files

      for name in changes.Write do
        printfn "WRITE %s" name

      for name in changes.Delete do
        printfn "DELETE %s" name

      printfn "%s %d file(s) in %s" (if dryRun then "Planned" else "Generated") files.Length output
      0
    with error ->
      eprintfn "%s" error.Message

      match error with
      | :? ReflectionTypeLoadException as loadError ->
        for loaderError in loadError.LoaderExceptions do
          if not (isNull loaderError) then
            eprintfn "%s" loaderError.Message
      | _ -> ()

      1

[<EntryPoint>]
let main args = run args
