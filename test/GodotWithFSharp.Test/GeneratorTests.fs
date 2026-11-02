module GodotWithFSharp.Test.GeneratorTests

open System
open System.IO
open System.Diagnostics
open System.Security
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.CSharp.Syntax
open NUnit.Framework
open GodotWithFSharp
open GodotWithFSharp.CodeGenerator
open GodotWithFSharp.Test.Fixtures

let private generate () =
  Generator.generate "Example.Generated" [ typeof<ExampleImpl>.Assembly ]

let private withDirectory action =
  let path =
    Path.Combine(Path.GetTempPath(), "GodotWithFSharp.Test", Guid.NewGuid().ToString("N"))

  Directory.CreateDirectory(path)
  |> ignore

  try
    action path
  finally
    Directory.Delete(path, true)

[<Test>]
let ``discovers annotated types and forwards exports and virtual methods`` () =
  let files = generate ()
  Assert.That(files |> Array.map _.Name, Is.EqualTo([| "ExampleNode.cs"; "class.cs" |] :> obj))
  let code = files.[0].Content

  for expected in
    [ "namespace Example.Generated"
      "public partial class ExampleNode"
      "global::GodotWithFSharp.RuntimeObject<global::Godot.Node,"
      "Impl = new global::GodotWithFSharp.Test.Fixtures.ExampleImpl(this)" ] do
    Assert.That(code, Does.Contain(expected))

  Assert.That(code, Does.Contain("private global::System.Double Speed"))
  Assert.That(code, Does.Contain("return _godotExport_Speed;"))
  Assert.That(code, Does.Contain("Impl.Speed = value;"))
  Assert.That(code, Does.Contain("global::Godot.PropertyHint)1"))
  Assert.That(code, Does.Contain("private global::System.String @event"))
  Assert.That(code, Does.Contain("public override void _Process(global::System.Double arg0)"))
  Assert.That(code, Does.Contain("Impl._Ready();"))
  Assert.That(code, Does.Contain("Impl.Update(arg0);"))
  Assert.That(code, Does.Contain("return Impl.GetWarnings();"))
  Assert.That(code, Does.Not.Contain("NotForwarded"))

[<Test>]
let ``named overrides declare Godot names and forward to implementation names`` () =
  let syntax = Generator.generateSyntax "Example.Generated" typeof<ExampleImpl>

  let methods =
    syntax.DescendantNodes()
    |> Seq.choose (
      function
      | :? MethodDeclarationSyntax as node -> Some node
      | _ -> None
    )
    |> Seq.toArray

  let names =
    methods
    |> Array.map (fun node -> node.Identifier.ValueText)

  Assert.That(names, Is.EquivalentTo([| "_Ready"; "_Process"; "_GetConfigurationWarnings" |]))

  let processMethod =
    methods
    |> Array.find (fun node -> node.Identifier.ValueText = "_Process")

  let invocation =
    processMethod.DescendantNodes()
    |> Seq.choose (
      function
      | :? InvocationExpressionSyntax as node -> Some node
      | _ -> None
    )
    |> Seq.exactlyOne

  let target = invocation.Expression :?> MemberAccessExpressionSyntax
  Assert.That(target.Name.Identifier.ValueText, Is.EqualTo("Update"))
  Assert.That(invocation.ArgumentList.Arguments.Count, Is.EqualTo(1))

[<Test>]
let ``builds Roslyn syntax and preserves literal values when rendered`` () =
  let syntax = Generator.generateSyntax "Example.Generated" typeof<ExampleImpl>

  let declaration =
    syntax.DescendantNodes()
    |> Seq.choose (
      function
      | :? ClassDeclarationSyntax as node -> Some node
      | _ -> None
    )
    |> Seq.exactlyOne

  Assert.That(declaration.Identifier.ValueText, Is.EqualTo("ExampleNode"))
  Assert.That(declaration.BaseList.Types.Count, Is.EqualTo(2))
  let source = (generate ()).[0].Content
  let parsed = CSharpSyntaxTree.ParseText(source)

  Assert.That(
    parsed.GetDiagnostics()
    |> Seq.filter (fun d -> d.Severity = DiagnosticSeverity.Error),
    Is.Empty
  )

  let hint =
    parsed.GetRoot().DescendantNodes()
    |> Seq.choose (
      function
      | :? LiteralExpressionSyntax as node when node.IsKind(SyntaxKind.StringLiteralExpression) ->
        Some node.Token.ValueText
      | _ -> None
    )
    |> Seq.find (fun value -> value.StartsWith("0,10,0.5"))

  Assert.That(hint, Is.EqualTo("0,10,0.5,quote\"\\\n\t\u0000"))
  let keywordSyntax = Generator.generateSyntax "Example.class" typeof<KeywordImpl>

  let keywordClass =
    keywordSyntax.DescendantNodes()
    |> Seq.choose (
      function
      | :? ClassDeclarationSyntax as node -> Some node
      | _ -> None
    )
    |> Seq.exactlyOne

  Assert.That(keywordClass.Identifier.Text, Is.EqualTo("@class"))
  Assert.That(keywordClass.Identifier.ValueText, Is.EqualTo("class"))

[<Test>]
let ``formats nested generics and array ranks`` () =
  let typ = typeof<Collections.Generic.Dictionary<string, int[,]>.KeyCollection>

  Assert.That(
    Generator.formatTypeName typ,
    Is.EqualTo(
      "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Int32[, ]>.KeyCollection"
    )
  )

  let mixedArray = typeof<int>.MakeArrayType(2).MakeArrayType()
  Assert.That(Generator.formatTypeName mixedArray, Is.EqualTo("global::System.Int32[][, ]"))

[<Test>]
let ``rejects invalid namespaces and duplicate output names`` () =
  Assert.Throws<ArgumentException>(fun () ->
    Generator.generateTypes "Bad/Namespace" [ typeof<ExampleImpl> ]
    |> ignore
  )
  |> ignore

  Assert.Throws<GenerationException>(fun () ->
    Generator.generateTypes "Example" [ typeof<ExampleImpl>; typeof<ExampleImpl> ]
    |> ignore
  )
  |> ignore

[<Test>]
let ``rejects invalid implementation contracts before writing files`` () =
  let assembly = typeof<InvalidFixtures.MissingContract>.Assembly

  for name, expected in
    [ "MissingContract", "Implement Impl"
      "MissingConstructor", "public constructor"
      "ReadOnlyExport", "get/set"
      "InvalidOverride", "virtual Godot method"
      "ReservedClassName", "conflicts with the runtime property"
      "InvalidNamedOverride", "MissingVirtualMethod (implementation: Update)"
      "MismatchedNamedOverride", "_Process (implementation: Update)"
      "DuplicateOverrides", "Duplicate override target: _Ready"
      "EmptyOverrideName", "Invalid C# identifier" ] do
    let typ =
      assembly.GetType(
        "GodotWithFSharp.Test.InvalidFixtures+"
        + name,
        true
      )

    let error =
      Assert.Throws<GenerationException>(fun () ->
        Generator.generateTypes "Example" [ typ ]
        |> ignore
      )

    Assert.That(error.Message, Does.Contain(expected))

[<Test>]
let ``writes incrementally and only removes owned stale files`` () =
  withDirectory (fun dir ->
    let files = generate ()
    Generator.writeFiles dir files
    Assert.That(File.ReadAllText(Path.Combine(dir, Output.manifestName)), Does.Not.Contain("\r"))
    let retained = Path.Combine(dir, files.[0].Name)
    let oldTime = DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    File.SetLastWriteTimeUtc(retained, oldTime)
    let manual = Path.Combine(dir, "Manual.cs")
    File.WriteAllText(manual, "// Handwritten")
    Generator.writeFiles dir [| files.[0] |]
    Assert.That(File.GetLastWriteTimeUtc(retained), Is.EqualTo(oldTime))
    Assert.That(File.Exists(Path.Combine(dir, files.[1].Name)), Is.False)
    Assert.That(File.ReadAllText manual, Is.EqualTo("// Handwritten"))
  )

[<Test>]
let ``refuses overwriting handwritten files before changing any output`` () =
  withDirectory (fun dir ->
    File.WriteAllText(Path.Combine(dir, "ExampleNode.cs"), "// Handwritten")

    Assert.Throws<InvalidOperationException>(fun () -> Generator.writeFiles dir (generate ()))
    |> ignore

    Assert.That(Directory.GetFiles(dir).Length, Is.EqualTo(1))
  )

[<Test>]
let ``rejects unsafe output paths`` () =
  withDirectory (fun dir ->
    let file = (generate ()).[0]

    Assert.Throws<ArgumentException>(fun () -> Generator.writeFiles dir [| { file with Name = "../Outside.cs" } |])
    |> ignore

    Assert.That(Directory.GetFiles(dir), Is.Empty)
  )

[<Test>]
let ``CLI validates arguments and generates from an external assembly`` () =
  Assert.That(Program.run [| "--help" |], Is.Zero)
  Assert.That(Program.run [||], Is.EqualTo(1))
  Assert.That(Program.run [| "--unknown" |], Is.EqualTo(1))

  withDirectory (fun dir ->
    Assert.That(
      Program.run
        [| "--assembly"
           typeof<ExampleImpl>.Assembly.Location
           "--namespace"
           "External.Generated"
           "--owner"
           "GodotWithFSharp"
           "--output"
           dir |],
      Is.Zero
    )

    Assert.That(Directory.GetFiles(dir, "*.cs").Length, Is.EqualTo(2))

    use manifest =
      Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, Output.manifestName)))

    Assert.That(manifest.RootElement.GetProperty("Owner").GetString(), Is.EqualTo("GodotWithFSharp"))

    Assert.That(
      Program.run
        [| "--assembly"
           Path.Combine(dir, "missing.dll")
           "--namespace"
           "Example"
           "--output"
           dir |],
      Is.EqualTo(1)
    )
  )

[<Test>]
let ``generated CSharp compiles against the real runtime contracts`` () =
  withDirectory (fun dir ->
    Generator.writeFiles dir (generate ())

    let assemblies =
      [ typeof<ExampleImpl>.Assembly
        typeof<Impl>.Assembly
        typeof<Godot.Node>.Assembly
        typeof<unit>.Assembly ]

    let references =
      assemblies
      |> List.mapi (fun i asm ->
        $"<Reference Include=\"Reference{i}\"><HintPath>{SecurityElement.Escape asm.Location}</HintPath></Reference>"
      )
      |> String.concat "\n"

    let project =
      $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup>{references}</ItemGroup></Project>"

    File.WriteAllText(Path.Combine(dir, "Generated.csproj"), project)
    let start = ProcessStartInfo("dotnet")
    start.WorkingDirectory <- dir

    for arg in [ "build"; "Generated.csproj"; "--nologo"; "--verbosity"; "quiet" ] do
      start.ArgumentList.Add(arg)

    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true
    use proc = Process.Start(start)
    let stdout = proc.StandardOutput.ReadToEndAsync()
    let stderr = proc.StandardError.ReadToEndAsync()

    if not (proc.WaitForExit(120000)) then
      proc.Kill(true)
      Assert.Fail("Generated C# compilation timed out.")

    Assert.That(proc.ExitCode, Is.Zero, stdout.Result + stderr.Result)
  )

[<Test>]
let ``reports all node collisions before rendering a batch`` () =
  let error =
    Assert.Throws<GenerationException>(fun () ->
      Generator.generateTypes
        "Example"
        [ typeof<InvalidFixtures.ClassCollision>
          typeof<InvalidFixtures.PropertyCollision>
          typeof<InvalidFixtures.UnsupportedExport> ]
      |> ignore
    )

  Assert.That(
    error.Diagnostics
    |> Array.filter (fun d -> d.Code = "GFS015")
    |> Array.length,
    Is.EqualTo(2)
  )

  Assert.That(
    error.Diagnostics
    |> Array.exists (fun d -> d.Code = "GFS011"),
    Is.True
  )

[<Test>]
let ``metadata definitions remain usable after inspection context is disposed`` () =
  let definitions = Metadata.read [ typeof<ExampleImpl>.Assembly.Location ] []
  let actual = Generator.generateDefinitions "Example.Generated" definitions
  Assert.That(actual, Is.EqualTo(generate () :> obj))

  Assert.Throws<FileNotFoundException>(fun () ->
    Metadata.read [ typeof<ExampleImpl>.Assembly.Location ] [ "missing-reference.dll" ]
    |> ignore
  )
  |> ignore

[<Test>]
let ``export defaults are explicit typed and never evaluated by generation`` () =
  for typ in [ typeof<InvalidFixtures.MissingDefault>; typeof<InvalidFixtures.WrongDefault> ] do
    let error =
      Assert.Throws<GenerationException>(fun () ->
        Generator.generateTypes "Example" [ typ ]
        |> ignore
      )

    Assert.That(
      error.Diagnostics
      |> Array.exists (fun d -> d.Code = "GFS020"),
      Is.True
    )

  let files =
    Generator.generateTypes "Example" [ typeof<InvalidFixtures.MetadataOnlyDefault> ]

  Assert.That(files.[0].Content, Does.Contain("ExplodingDefaultProvider.Value"))
  let code = (generate ()).[0].Content

  Assert.That(
    code,
    Does.Contain("_godotExport_Offset = global::GodotWithFSharp.Test.Fixtures.ExportDefaultValues.Offset")
  )

  Assert.That(code, Does.Contain("Impl.Speed = _godotExport_Speed"))

[<Test>]
let ``lifetime generation owns disposal and composes notification callbacks`` () =
  let code =
    (Generator.generateTypes "Example" [ typeof<InvalidFixtures.OwnedImplementation> ])
      .[0].Content

  Assert.That(code, Does.Contain("global::Godot.ToolAttribute"))
  Assert.That(code, Does.Contain("NotificationPredelete"))
  Assert.That(code, Does.Contain("finally"))
  Assert.That(code, Does.Contain("Impl.Notify(arg0)"))
  Assert.That(code, Does.Contain("global::System.IDisposable"))

  let syntax =
    Generator.generateSyntax "Example" typeof<InvalidFixtures.OwnedImplementation>

  let methods =
    syntax.DescendantNodes()
    |> Seq.choose (
      function
      | :? MethodDeclarationSyntax as node -> Some node.Identifier.ValueText
      | _ -> None
    )
    |> Seq.toArray

  Assert.That(
    methods
    |> Array.filter ((=) "_Notification")
    |> Array.length,
    Is.EqualTo(1)
  )

  let borrowed =
    (Generator.generateTypes "Example" [ typeof<InvalidFixtures.BorrowedImplementation> ])
      .[0].Content

  Assert.That(borrowed, Does.Not.Contain("NotificationPredelete"))

[<Test>]
let ``output requires explicit empty generation and preserves Godot uid files`` () =
  withDirectory (fun directory ->
    let files = generate ()
    Generator.writeFiles directory files
    let uid = Path.Combine(directory, files.[0].Name + ".uid")
    File.WriteAllText(uid, "uid://example")

    Assert.Throws<InvalidOperationException>(fun () -> Generator.writeFiles directory [||])
    |> ignore

    Assert.That(Directory.GetFiles(directory, "*.cs").Length, Is.EqualTo(files.Length))

    Output.write
      { Output.defaults with
          AllowEmpty = true }
      directory
      [||]
    |> ignore

    Assert.That(Directory.GetFiles(directory, "*.cs"), Is.Empty)
    Assert.That(File.ReadAllText uid, Is.EqualTo("uid://example"))
  )

[<Test>]
let ``output rejects another owner or edited generated source`` () =
  withDirectory (fun directory ->
    let files = generate ()
    Generator.writeFiles directory files

    Assert.Throws<InvalidOperationException>(fun () ->
      Output.write { Output.defaults with Owner = "Other" } directory files
      |> ignore
    )
    |> ignore

    let path = Path.Combine(directory, files.[0].Name)
    File.AppendAllText(path, "// manual change")

    Assert.Throws<InvalidOperationException>(fun () -> Generator.writeFiles directory files)
    |> ignore

    Assert.That(File.ReadAllText path, Does.EndWith("// manual change"))
  )

[<Test>]
let ``output dry run is read only and legacy adoption is explicit`` () =
  withDirectory (fun directory ->
    let files = generate ()
    let target = Path.Combine(directory, "dry-run")
    let changes = Output.plan Output.defaults target files
    Assert.That(changes.Write.Length, Is.EqualTo(files.Length))
    Assert.That(Directory.Exists target, Is.False)
    File.WriteAllText(Path.Combine(directory, files.[0].Name), files.[0].Content)

    Assert.Throws<InvalidOperationException>(fun () -> Generator.writeFiles directory files)
    |> ignore

    Output.write
      { Output.defaults with
          AdoptExisting = true }
      directory
      files
    |> ignore

    Assert.That(File.Exists(Path.Combine(directory, Output.manifestName)), Is.True)
  )

[<Test>]
let ``output recovers an interrupted transaction before generating`` () =
  withDirectory (fun directory ->
    let files = generate ()
    Generator.writeFiles directory files
    let path = Path.Combine(directory, files.[0].Name)

    let partial =
      files.[0].Content
      + "// interrupted write"

    File.WriteAllText(path, partial)

    let hash =
      Convert.ToHexString(Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes partial))

    let journal =
      {| Version = 1
         Files =
          [| {| Name = files.[0].Name
                Before = files.[0].Content
                Existed = true
                AfterHash = hash |} |] |}

    let journalPath = Path.Combine(directory, ".godotwithfsharp.transaction.json")
    File.WriteAllText(journalPath, Text.Json.JsonSerializer.Serialize(journal))
    Generator.writeFiles directory files
    Assert.That(File.ReadAllText path, Is.EqualTo(files.[0].Content))
    Assert.That(File.Exists journalPath, Is.False)
  )

[<Test>]
let ``input set changes require explicit acknowledgement`` () =
  withDirectory (fun directory ->
    let files = generate ()

    let original =
      { Output.defaults with
          Inputs = [| "Original.dll" |] }

    Output.write original directory files
    |> ignore

    let changed =
      { original with
          Inputs = [| "Different.dll" |] }

    Assert.Throws<InvalidOperationException>(fun () ->
      Output.write changed directory files
      |> ignore
    )
    |> ignore

    Output.write { changed with AllowInputChange = true } directory files
    |> ignore

    Assert.That(File.ReadAllText(Path.Combine(directory, Output.manifestName)), Does.Contain("Different.dll"))
  )

[<Test>]
let ``recovery preserves changes made after an interrupted transaction`` () =
  withDirectory (fun directory ->
    let files = generate ()
    Generator.writeFiles directory files
    let path = Path.Combine(directory, files.[0].Name)
    let partial = files.[0].Content + "// interrupted"

    let hash =
      Convert.ToHexString(Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes partial))

    let journal =
      {| Version = 1
         Files =
          [| {| Name = files.[0].Name
                Before = files.[0].Content
                Existed = true
                AfterHash = hash |} |] |}

    let journalPath = Path.Combine(directory, ".godotwithfsharp.transaction.json")
    File.WriteAllText(journalPath, Text.Json.JsonSerializer.Serialize(journal))
    let manual = partial + "// edited afterwards"
    File.WriteAllText(path, manual)

    Assert.Throws<InvalidOperationException>(fun () -> Generator.writeFiles directory files)
    |> ignore

    Assert.That(File.ReadAllText path, Is.EqualTo(manual))
    Assert.That(File.Exists journalPath, Is.True)
  )
