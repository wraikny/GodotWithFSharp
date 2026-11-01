module GodotWithFSharp.CodeGenerator.Program

open System
open System.IO
open System.Reflection
open System.Runtime.Loader

let private usage =
  """Usage: godot-fsharp-gen --assembly <implementation.dll> [--assembly <other.dll> ...]
                        --namespace <Generated.Namespace> --output <directory>
Paths are relative to the caller's working directory. Build implementation assemblies first.
Use a dedicated output directory; stale generated files there are removed."""

let run (args: string[]) =
  if
    args = [| "--help" |]
    || args = [| "-h" |]
  then
    printfn "%s" usage
    0
  else
    try
      let rec parse assemblies ns output =
        function
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

      let resolvers =
        paths
        |> List.map AssemblyDependencyResolver

      let handler =
        Func<AssemblyLoadContext, AssemblyName, Assembly>(fun context name ->
          let resolved =
            resolvers
            |> List.tryPick (fun resolver ->
              match resolver.ResolveAssemblyToPath(name) with
              | null -> None
              | path -> Some path
            )

          let path =
            resolved
            |> Option.orElseWith (fun () ->
              paths
              |> List.tryPick (fun path ->
                let candidate = Path.Combine(Path.GetDirectoryName path, name.Name + ".dll")
                if File.Exists candidate then Some candidate else None
              )
            )

          match path with
          | Some path -> context.LoadFromAssemblyPath(path)
          | None -> null
        )

      AssemblyLoadContext.Default.add_Resolving (handler)

      try
        let assemblies =
          paths
          |> List.map AssemblyLoadContext.Default.LoadFromAssemblyPath

        let files = Generator.generate ns assemblies
        Generator.writeFiles output files
        printfn "Generated %d file(s) in %s" files.Length output
        0
      finally
        AssemblyLoadContext.Default.remove_Resolving (handler)
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
