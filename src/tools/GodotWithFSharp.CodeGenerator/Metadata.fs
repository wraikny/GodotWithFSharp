namespace GodotWithFSharp.CodeGenerator

open System
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Text.Json

/// Assembly inspection is isolated from the CLI's execution context and never executes input code.
module Metadata =
  let private tryProperty (name: string) (element: JsonElement) =
    let mutable value = Unchecked.defaultof<JsonElement>

    if element.TryGetProperty(name, &value) then
      Some value
    else
      None

  let private properties name element =
    tryProperty name element
    |> Option.map (fun value -> value.EnumerateObject() |> Seq.toArray)
    |> Option.defaultValue [||]

  let private packageDirectory () =
    match Environment.GetEnvironmentVariable("NUGET_PACKAGES") with
    | value when not (String.IsNullOrWhiteSpace value) -> value
    | _ -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")

  let private assetPaths packages packagePath (library: JsonProperty) =
    [| "runtime"; "compile" |]
    |> Array.collect (fun kind -> properties kind library.Value)
    |> Array.map (fun asset ->
      Path.Combine(packages, packagePath, asset.Name.Replace('/', Path.DirectorySeparatorChar))
    )
    |> Array.filter (fun path ->
      path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
      && File.Exists path
    )

  let private packagePaths packages libraries (library: JsonProperty) =
    tryProperty library.Name libraries
    |> Option.bind (tryProperty "path")
    |> Option.map (fun path -> assetPaths packages (path.GetString()) library)
    |> Option.defaultValue [||]

  let private dependencyPaths (assemblyPath: string) =
    let deps = Path.ChangeExtension(assemblyPath, ".deps.json")

    if not (File.Exists deps) then
      [||]
    else
      use document = JsonDocument.Parse(File.ReadAllText deps)

      match tryProperty "libraries" document.RootElement with
      | None -> [||]
      | Some libraries ->
        let packages = packageDirectory ()

        properties "targets" document.RootElement
        |> Array.collect (fun target ->
          target.Value.EnumerateObject()
          |> Seq.toArray
        )
        |> Array.collect (packagePaths packages libraries)
        |> Array.distinct

  /// Resolve inputs and explicit references first, then their restored dependencies and framework files.
  let read (assemblyPaths: string seq) (referencePaths: string seq) =
    let inputs =
      assemblyPaths
      |> Seq.map Path.GetFullPath
      |> Seq.distinct
      |> Seq.toArray

    if inputs.Length = 0 then
      invalidArg "assemblyPaths" "At least one implementation assembly is required."

    for path in inputs do
      if not (File.Exists path) then
        raise (FileNotFoundException("Implementation assembly not found. Build it first.", path))

    let explicitPaths =
      referencePaths
      |> Seq.map Path.GetFullPath
      |> Seq.toArray

    for path in explicitPaths do
      if not (File.Exists path) then
        raise (FileNotFoundException("Metadata reference not found.", path))

    let candidates =
      seq {
        yield! inputs
        yield! explicitPaths

        for path in inputs do
          yield! Directory.GetFiles(Path.GetDirectoryName path, "*.dll")
          yield! dependencyPaths path
      }
    // Reject conflicting versions instead of silently binding to whichever input happened to be first.
    let selected =
      Collections.Generic.Dictionary<string, string * AssemblyName>(StringComparer.OrdinalIgnoreCase)

    for path in candidates |> Seq.distinct do
      let name =
        try
          Some(AssemblyName.GetAssemblyName path)
        with :? BadImageFormatException when
            not (
              Array.contains path inputs
              || Array.contains path explicitPaths
            ) ->
          None

      match name with
      | None -> ()
      | Some name ->
        match selected.TryGetValue name.Name with
        | true, (previousPath, previous) when previous.FullName <> name.FullName ->
          raise (
            GenerationException
              [| { Code = "GFS101"
                   Target = name.Name
                   Message =
                     $"Conflicting metadata references: {previousPath} ({previous.Version}) and {path} ({name.Version}). Pass one consistent dependency set." } |]
          )
        | true, _ -> ()
        | _ -> selected.Add(name.Name, (path, name))

    for path in Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll") do
      try
        let name = AssemblyName.GetAssemblyName path

        if not (selected.ContainsKey name.Name) then
          selected.Add(name.Name, (path, name))
      with :? BadImageFormatException ->
        ()

    try
      let resolver = PathAssemblyResolver(selected.Values |> Seq.map fst)
      use context = new MetadataLoadContext(resolver, "System.Private.CoreLib")

      let types =
        inputs
        |> Seq.collect (fun path -> context.LoadFromAssemblyPath(path).ExportedTypes)

      Analysis.analyzeTypes types
    with
    | :? GenerationException -> reraise ()
    | error ->
      raise (
        GenerationException
          [| { Code = "GFS100"
               Target = String.concat ", " inputs
               Message =
                 $"Cannot inspect implementation metadata: {error.Message} Use --reference for dependencies that are not in the build output or NuGet cache." } |]
      )
