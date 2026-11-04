module GodotWithFSharp.Test.CliIntegrationTests

open System
open System.IO
open NUnit.Framework
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open GodotWithFSharp
open GodotWithFSharp.CodeGenerator
open GodotWithFSharp.Test

[<Test>]
let ``fresh CLI processes resolve explicit dependencies without executing input code`` () =
  let root =
    Path.Combine(
      Path.GetTempPath(),
      "GodotWithFSharp CLI "
      + Guid.NewGuid().ToString("N")
    )

  Directory.CreateDirectory root |> ignore

  try
    let sentinel = Path.Combine(root, "INPUT_WAS_EXECUTED")

    let source =
      """
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Godot;
using GodotWithFSharp;
public static class Initialization {
    [ModuleInitializer] public static void Initialize() { File.WriteAllText(SENTINEL, "executed"); }
}
public sealed class SideEffectAttribute : Attribute {
    public SideEffectAttribute() { File.WriteAllText(SENTINEL, "attribute"); }
}
public static class Defaults {
    public static float Value => throw new Exception("Provider must not execute during generation.");
}
[SideEffect, GenerateGodotNode("IsolatedNode", typeof(Node))]
public class IsolatedImpl : Impl<Node> {
    public IsolatedImpl(Node node) { throw new Exception("Constructor must not execute during generation."); }
    [Export, GodotExportDefaultProvider(typeof(Defaults), "Value")]
    public float Speed { get; set; }
    [Export, GodotExportDefault(0)]
    public int _Notification { get; set; }
}
"""

    let source = source.Replace("SENTINEL", SymbolDisplay.FormatLiteral(sentinel, true))

    let references =
      (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string)
        .Split(Path.PathSeparator)
      |> Array.append [| typeof<Impl>.Assembly.Location; typeof<Godot.Node>.Assembly.Location |]
      |> Array.distinct
      |> Array.map (fun path -> MetadataReference.CreateFromFile(path) :> MetadataReference)

    let compilation =
      CSharpCompilation.Create(
        "Isolated.Implementation",
        [| CSharpSyntaxTree.ParseText source |],
        references,
        CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
      )

    let input = Path.Combine(root, "Isolated.Implementation.dll")
    use stream = File.Create input
    let emitted = compilation.Emit stream
    Assert.That(emitted.Success, Is.True, String.Join("\n", emitted.Diagnostics))
    stream.Dispose()
    let output = Path.Combine(root, "generated")

    let arguments =
      [ typeof<NodeDefinition>.Assembly.Location
        "--assembly"
        input
        "--namespace"
        "Isolated.Generated"
        "--output"
        output ]

    let code, message = Processes.run 60000 root "dotnet" arguments
    Assert.That(code, Is.Not.Zero)
    Assert.That(message, Does.Contain("GFS100").And.Contain("--reference"))
    Assert.That(Directory.Exists output, Is.False)

    let dependencies =
      [ typeof<Impl>.Assembly.Location
        typeof<Godot.Node>.Assembly.Location
        typeof<unit>.Assembly.Location ]
      |> List.collect (fun path -> [ "--reference"; path ])

    let code, message = Processes.run 60000 root "dotnet" (arguments @ dependencies)
    Assert.That(code, Is.Zero, message)
    Assert.That(message, Does.Contain("warning GFS018:").And.Contain("IsolatedImpl._Notification"))
    let file = Path.Combine(output, "IsolatedNode.cs")
    Assert.That(File.ReadAllText file, Does.Contain("global::Defaults.Value"))

    Assert.That(
      File.Exists sentinel,
      Is.False,
      "Input module initializers, attributes, constructors, and providers must not run."
    )

    let modified = File.GetLastWriteTimeUtc file

    let code, message =
      Processes.run
        60000
        root
        "dotnet"
        (arguments
         @ dependencies
         @ [ "--dry-run" ])

    Assert.That(code, Is.Zero, message)
    Assert.That(message, Does.Contain("warning GFS018:"))
    Assert.That(File.GetLastWriteTimeUtc file, Is.EqualTo modified)
    Assert.That(File.Exists sentinel, Is.False)
  finally
    Directory.Delete(root, true)
