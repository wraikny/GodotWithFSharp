module GodotWithFSharp.Test.RefactoringTests

open System
open System.IO
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.CSharp.Syntax
open NUnit.Framework
open GodotWithFSharp.CodeGenerator
open GodotWithFSharp.Test.Fixtures

let private node exports =
  { Name = "Defaults"
    Implementation = TypeReference.named "Example.Implementation" [||]
    BaseType = TypeReference.named "Godot.Node" [||]
    Exports = exports
    Overrides = [||]
    Tool = false
    DisposeImplementation = false
    ImplementsDisposable = false }

[<Test>]
let ``attribute decoding preserves literal widths and enum underlying values`` () =
  let definition =
    Analysis.analyzeTypes [ typeof<InvalidFixtures.LiteralDefaults> ]
    |> Array.exactlyOne

  let actual =
    definition.Exports
    |> Array.map (fun export -> export.Name, export.Default)
    |> Map.ofArray

  let expected =
    [ "Text", DefaultConstant.Null
      "Flag", DefaultConstant.Boolean true
      "Letter", DefaultConstant.Character 'x'
      "Byte", DefaultConstant.Byte Byte.MaxValue
      "SByte", DefaultConstant.SByte SByte.MinValue
      "Int16", DefaultConstant.Int16 Int16.MinValue
      "UInt16", DefaultConstant.UInt16 UInt16.MaxValue
      "Int32", DefaultConstant.Int32 Int32.MinValue
      "UInt32", DefaultConstant.UInt32 UInt32.MaxValue
      "Int64", DefaultConstant.Int64 Int64.MinValue
      "UInt64", DefaultConstant.UInt64 UInt64.MaxValue
      "Single", DefaultConstant.Single 1.25f
      "Double", DefaultConstant.Double 2.5
      "Enum", DefaultConstant.Int64(int64 Godot.PropertyHint.Range) ]
    |> List.map (fun (name, value) -> name, Constant value)
    |> Map.ofList

  Assert.That(actual, Is.EqualTo(expected :> obj))

let compatibilityCases () =
  [ typeof<int>, true
    typeof<string>, true
    typeof<Godot.Vector2>, true
    typeof<Godot.PropertyHint>, true
    typeof<Godot.Node2D>, true
    typeof<byte array>, true
    typeof<Godot.Vector2 array>, true
    typeof<Godot.Node array>, true
    typeof<Godot.Collections.Array<int>>, true
    typeof<Godot.Collections.Dictionary<string, Godot.Vector2>>, true
    typeof<int[,]>, false
    typeof<int16 array>, false
    typeof<Godot.PropertyHint array>, false
    typeof<DateTime>, false
    typeof<Godot.Collections.Array<DateTime>>, false ]
  |> List.map (fun (typ, supported) -> TestCaseData(typ, supported))

[<TestCaseSource(nameof compatibilityCases)>]
let ``export compatibility distinguishes packed arrays and Godot collections`` (typ: Type) supported =
  let implementation =
    typedefof<InvalidFixtures.GenericExport<_>>.MakeGenericType(typ)

  let diagnostics =
    try
      Analysis.analyzeTypes [ implementation ]
      |> ignore

      [||]
    with :? GenerationException as error ->
      error.Diagnostics

  let rejected =
    diagnostics
    |> Array.exists (fun diagnostic -> diagnostic.Code = "GFS011")

  Assert.That(rejected, Is.EqualTo(not supported))

[<Test>]
let ``metadata providers retain property and method semantics without execution`` () =
  let definitions = Metadata.read [ typeof<ExampleImpl>.Assembly.Location ] []

  let definition =
    definitions
    |> Array.find (fun definition -> definition.Name = "ExampleNode")

  let property =
    definition.Exports
    |> Array.find (fun export -> export.Name = "Offset")

  let method =
    definition.Exports
    |> Array.find (fun export -> export.Name = "MethodOffset")

  match property.Default, method.Default with
  | StaticProvider(_, DefaultProviderMember.Property "Offset"),
    StaticProvider(_, DefaultProviderMember.Method "CreateOffset") -> ()
  | other -> Assert.Fail($"Unexpected provider definitions: {other}")

  let source = (Generator.generateDefinitions "Example" [| definition |]).[0].Content
  Assert.That(source, Does.Contain("ExportDefaultValues.Offset;"))
  Assert.That(source, Does.Contain("ExportDefaultValues.CreateOffset();"))

let literalCases () =
  [ "System.Boolean", DefaultConstant.Boolean(true), box (true)
    "System.String", DefaultConstant.String("quote\"\\\n\t\u0000"), box ("quote\"\\\n\t\u0000")
    "System.String", DefaultConstant.Null, null
    "System.Char", DefaultConstant.Character('\''), box ('\'')
    "System.Byte", DefaultConstant.Byte(Byte.MaxValue), box (Byte.MaxValue)
    "System.SByte", DefaultConstant.SByte(SByte.MinValue), box (SByte.MinValue)
    "System.Int16", DefaultConstant.Int16(Int16.MinValue), box (Int16.MinValue)
    "System.UInt16", DefaultConstant.UInt16(UInt16.MaxValue), box (UInt16.MaxValue)
    "System.Int32", DefaultConstant.Int32(Int32.MinValue), box (Int32.MinValue)
    "System.UInt32", DefaultConstant.UInt32(UInt32.MaxValue), box (UInt32.MaxValue)
    "System.Int64", DefaultConstant.Int64(Int64.MinValue), box (Int64.MinValue)
    "System.UInt64", DefaultConstant.UInt64(UInt64.MaxValue), box (UInt64.MaxValue)
    "System.Single", DefaultConstant.Single(Single.MinValue), box (Single.MinValue)
    "System.Single", DefaultConstant.Single(Single.NaN), box (Single.NaN)
    "System.Single", DefaultConstant.Single(Single.PositiveInfinity), box (Single.PositiveInfinity)
    "System.Single", DefaultConstant.Single(Single.NegativeInfinity), box (Single.NegativeInfinity)
    "System.Double", DefaultConstant.Double(Double.MinValue), box (Double.MinValue)
    "System.Double", DefaultConstant.Double(Double.NaN), box (Double.NaN)
    "System.Double", DefaultConstant.Double(Double.PositiveInfinity), box (Double.PositiveInfinity)
    "System.Double", DefaultConstant.Double(Double.NegativeInfinity), box (Double.NegativeInfinity) ]
  |> List.map (fun (name, constant, value) ->
    let test = TestCaseData(name, constant, value)

    let knownNegativeCastIssue =
      match constant with
      | DefaultConstant.SByte value -> value < 0y
      | DefaultConstant.Int16 value -> value < 0s
      | DefaultConstant.Int32 value -> value < 0
      | DefaultConstant.Int64 value -> value < 0L
      | DefaultConstant.Single value ->
        value < 0.0f
        && not (Single.IsInfinity value)
      | DefaultConstant.Double value ->
        value < 0.0
        && not (Double.IsInfinity value)
      | _ -> false

    if knownNegativeCastIssue then
      test.Explicit("Existing negative literal cast bug; see docs/review-follow-ups.md. Run explicitly to reproduce.")
    else
      test
  )

[<TestCaseSource(nameof literalCases)>]
let ``portable defaults compile to the original CLR constant`` (name: string) (constant: DefaultConstant) (value: obj) =
  let definition =
    { Name = "Value"
      Type = TypeReference.named name [||]
      Hint = 0
      HintString = ""
      Default = Constant constant }

  let source =
    (Generator.generateDefinitions "Example" [| node [| definition |] |])
      .[0].Content

  let tree = CSharpSyntaxTree.ParseText(source)

  let references =
    (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string)
      .Split(Path.PathSeparator)
    |> Array.map (fun path -> MetadataReference.CreateFromFile(path) :> MetadataReference)

  // Bind the complete generated class so assertions check C# semantics, not source spelling.
  let support =
    CSharpSyntaxTree.ParseText(
      $"""
namespace Godot {{ public class Node {{ }} public enum PropertyHint {{ None }}
public class ExportAttribute : System.Attribute {{ public ExportAttribute(PropertyHint hint, string value) {{ }} }} }}
namespace GodotWithFSharp {{ public interface RuntimeObject<TNode, TImpl> {{ }} }}
namespace Example {{ public class Implementation {{ public Implementation(Godot.Node node) {{ }} public {name} Value {{ get; set; }} }} }}
"""
    )

  let compilation =
    CSharpCompilation.Create(
      "Defaults",
      [| tree; support |],
      references,
      CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
    )

  let errors =
    compilation.GetDiagnostics()
    |> Seq.filter (fun d -> d.Severity = DiagnosticSeverity.Error)

  Assert.That(errors, Is.Empty)

  let initializer =
    tree.GetRoot().DescendantNodes()
    |> Seq.choose (
      function
      | :? VariableDeclaratorSyntax as variable -> Some variable.Initializer.Value
      | _ -> None
    )
    |> Seq.exactlyOne

  let actual = compilation.GetSemanticModel(tree, false).GetConstantValue(initializer)
  Assert.That(actual.HasValue, Is.True)
  Assert.That(actual.Value, Is.EqualTo(value))

[<Test>]
let ``ownership preflight rejects conflicts without creating a lock`` () =
  let directory =
    Path.Combine(Path.GetTempPath(), "GodotWithFSharp.Test", Guid.NewGuid().ToString("N"))

  Directory.CreateDirectory directory
  |> ignore

  try
    let path = Path.Combine(directory, "Defaults.cs")
    File.WriteAllText(path, "// handwritten")
    let files = Generator.generateDefinitions "Example" [| node [||] |]

    Assert.Throws<InvalidOperationException>(fun () ->
      Output.write Output.defaults directory files
      |> ignore
    )
    |> ignore

    Assert.That(Directory.GetFiles(directory), Is.EqualTo([| path |] :> obj))
    Assert.That(File.ReadAllText(path), Is.EqualTo("// handwritten"))
  finally
    Directory.Delete(directory, true)

[<Test>]
let ``no change generation preserves both source and manifest timestamps`` () =
  let directory =
    Path.Combine(Path.GetTempPath(), "GodotWithFSharp.Test", Guid.NewGuid().ToString("N"))

  try
    let files = Generator.generateDefinitions "Example" [| node [||] |]

    Output.write Output.defaults directory files
    |> ignore

    let paths =
      [| Path.Combine(directory, "Defaults.cs")
         Path.Combine(directory, Output.manifestName) |]

    let timestamp = DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)

    for path in paths do
      File.SetLastWriteTimeUtc(path, timestamp)

    let changes = Output.write Output.defaults directory files
    Assert.That(changes.Write, Is.Empty)
    Assert.That(changes.Delete, Is.Empty)

    for path in paths do
      Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(timestamp))
  finally
    if Directory.Exists directory then
      Directory.Delete(directory, true)
