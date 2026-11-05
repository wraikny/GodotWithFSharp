namespace GodotWithFSharp.CodeGenerator

open System
open System.Reflection
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.CSharp.Syntax
open GodotWithFSharp.CodeGenerator.CSharpSyntax

module Generator =
  let private header = Output.marker

  /// Formats a CLR type using the same Roslyn type nodes as generation.
  let formatTypeName typ =
    (typeName typ).NormalizeWhitespace().ToFullString()

  let private generateType ns (node: NodeDefinition) =
    let name = identifier node.Name
    let implType = referenceName node.Implementation
    let members = Collections.Generic.List<MemberDeclarationSyntax>()

    let implProperty =
      SyntaxFactory
        .PropertyDeclaration(implType, identifier "Impl")
        .WithModifiers(modifiers [| SyntaxKind.PublicKeyword |])
        .AddAccessorListAccessors(
          SyntaxFactory
            .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithSemicolonToken(SyntaxFactory.Token SyntaxKind.SemicolonToken),
          SyntaxFactory
            .AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
            .WithModifiers(modifiers [| SyntaxKind.PrivateKeyword |])
            .WithSemicolonToken(SyntaxFactory.Token SyntaxKind.SemicolonToken)
        )

    members.Add implProperty

    let instance =
      SyntaxFactory
        .ObjectCreationExpression(implType)
        .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()))

    let constructor =
      SyntaxFactory
        .ConstructorDeclaration(name)
        .WithModifiers(modifiers [| SyntaxKind.PublicKeyword |])
        .WithBody(SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(assignment (identifierName "Impl") instance)))

    let defaultAssignments =
      node.Exports
      |> Array.map (fun prop ->
        SyntaxFactory.ExpressionStatement(
          assignment (memberAccess (identifierName "Impl") prop.Name) (identifierName ("_godotExport_" + prop.Name))
        )
        :> StatementSyntax
      )

    members.Add(constructor.WithBody(constructor.Body.AddStatements(defaultAssignments)))

    for prop in node.Exports do
      let hint =
        SyntaxFactory.CastExpression(
          referenceName (TypeReference.named "Godot.PropertyHint" [||]),
          SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(prop.Hint))
        )

      let hintString =
        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(prop.HintString))

      let exportAttribute =
        SyntaxFactory
          .Attribute(referenceName (TypeReference.named "Godot.ExportAttribute" [||]) :?> NameSyntax)
          .AddArgumentListArguments(SyntaxFactory.AttributeArgument(hint), SyntaxFactory.AttributeArgument(hintString))

      let target = memberAccess (identifierName "Impl") prop.Name
      let fieldName = "_godotExport_" + prop.Name
      let field = identifierName fieldName

      let variable =
        SyntaxFactory
          .VariableDeclarator(identifier fieldName)
          .WithInitializer(SyntaxFactory.EqualsValueClause(defaultExpression prop.Type prop.Default))

      members.Add(
        SyntaxFactory
          .FieldDeclaration(
            SyntaxFactory
              .VariableDeclaration(referenceName prop.Type)
              .AddVariables(variable)
          )
          .WithModifiers(modifiers [| SyntaxKind.PrivateKeyword |])
      )
      // A single field return exposes the initializer to Godot's default-value analyzer.
      // Refresh from Impl first so implementation-side writes remain visible immediately.
      let getAccessor =
        SyntaxFactory
          .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
          .WithBody(
            SyntaxFactory.Block(
              SyntaxFactory.ExpressionStatement(assignment field target),
              SyntaxFactory.ReturnStatement(field)
            )
          )

      let setAccessor =
        SyntaxFactory
          .AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
          .WithBody(
            SyntaxFactory.Block(
              SyntaxFactory.ExpressionStatement(assignment target (identifierName "value")),
              SyntaxFactory.ExpressionStatement(assignment field (identifierName "value"))
            )
          )

      let property =
        SyntaxFactory
          .PropertyDeclaration(referenceName prop.Type, identifier prop.Name)
          .WithModifiers(modifiers [| SyntaxKind.PrivateKeyword |])
          .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(exportAttribute)))
          .AddAccessorListAccessors(getAccessor, setAccessor)

      members.Add property

    let autoDispose =
      node.DisposeImplementation
      && node.ImplementsDisposable

    let overrides =
      node.Overrides
      |> Array.filter (fun method ->
        not autoDispose
        || method.Name <> "_Notification"
      )

    for method in overrides do
      let targetIdentifier = identifier method.Name
      let ps = method.Parameters

      let parameters =
        ps
        |> Array.mapi (fun i p -> SyntaxFactory.Parameter(identifier $"arg{i}").WithType(referenceName p))

      let arguments =
        ps
        |> Array.mapi (fun i _ -> SyntaxFactory.Argument(identifierName $"arg{i}"))

      let call =
        SyntaxFactory
          .InvocationExpression(memberAccess (identifierName "Impl") method.ImplementationName)
          .AddArgumentListArguments(arguments)

      let statement: StatementSyntax =
        if method.ReturnType = TypeReference.Void then
          SyntaxFactory.ExpressionStatement(call) :> StatementSyntax
        else
          SyntaxFactory.ReturnStatement(call) :> StatementSyntax

      let declaration =
        SyntaxFactory
          .MethodDeclaration(referenceName method.ReturnType, targetIdentifier)
          .WithModifiers(modifiers [| SyntaxKind.PublicKeyword; SyntaxKind.OverrideKeyword |])
          .AddParameterListParameters(parameters)
          .WithBody(SyntaxFactory.Block(statement))

      members.Add declaration

    if autoDispose then
      let disposed = identifierName "_godotImplementationDisposed"

      let booleanType =
        SyntaxFactory.PredefinedType(SyntaxFactory.Token SyntaxKind.BoolKeyword)

      members.Add(
        SyntaxFactory
          .FieldDeclaration(
            SyntaxFactory
              .VariableDeclaration(booleanType)
              .AddVariables(SyntaxFactory.VariableDeclarator(disposed.Identifier))
          )
          .WithModifiers(modifiers [| SyntaxKind.PrivateKeyword |])
      )

      let disposable =
        SyntaxFactory.ParenthesizedExpression(
          SyntaxFactory.CastExpression(
            referenceName (TypeReference.named "System.IDisposable" [||]),
            identifierName "Impl"
          )
        )

      let disposeCall =
        SyntaxFactory.ExpressionStatement(
          SyntaxFactory
            .InvocationExpression(memberAccess disposable "Dispose")
            .WithArgumentList(SyntaxFactory.ArgumentList())
        )

      let predelete =
        SyntaxFactory.BinaryExpression(
          SyntaxKind.EqualsExpression,
          identifierName "arg0",
          memberAccess (referenceName (TypeReference.named "Godot.GodotObject" [||])) "NotificationPredelete"
        )

      let notDisposed =
        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, disposed)

      let condition =
        SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, predelete, notDisposed)

      let cleanup =
        SyntaxFactory.IfStatement(
          condition,
          SyntaxFactory.Block(
            SyntaxFactory.ExpressionStatement(assignment disposed (constant (DefaultConstant.Boolean true))),
            disposeCall
          )
        )

      let forwarded =
        node.Overrides
        |> Array.tryFind (fun method -> method.Name = "_Notification")
        |> Option.map (fun method ->
          SyntaxFactory.ExpressionStatement(
            SyntaxFactory
              .InvocationExpression(memberAccess (identifierName "Impl") method.ImplementationName)
              .AddArgumentListArguments(SyntaxFactory.Argument(identifierName "arg0"))
          )
          :> StatementSyntax
        )
        |> Option.toArray

      let body =
        SyntaxFactory.TryStatement(
          SyntaxFactory.Block(forwarded),
          SyntaxFactory.List<CatchClauseSyntax>(),
          SyntaxFactory.FinallyClause(SyntaxFactory.Block(cleanup))
        )

      members.Add(
        SyntaxFactory
          .MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token SyntaxKind.VoidKeyword),
            identifier "_Notification"
          )
          .WithModifiers(modifiers [| SyntaxKind.PublicKeyword; SyntaxKind.OverrideKeyword |])
          .AddParameterListParameters(
            SyntaxFactory
              .Parameter(identifier "arg0")
              .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token SyntaxKind.IntKeyword))
          )
          .WithBody(SyntaxFactory.Block(body))
      )

    let runtimeContract =
      TypeReference.named "GodotWithFSharp.RuntimeObject" [| node.BaseType; node.Implementation |]

    let declaration =
      SyntaxFactory
        .ClassDeclaration(name)
        .WithModifiers(modifiers [| SyntaxKind.PublicKeyword; SyntaxKind.PartialKeyword |])
        .AddBaseListTypes(
          SyntaxFactory.SimpleBaseType(referenceName node.BaseType),
          SyntaxFactory.SimpleBaseType(referenceName runtimeContract)
        )
        .AddMembers(members.ToArray())

    let declaration =
      if node.Tool then
        declaration.AddAttributeLists(
          SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
              SyntaxFactory.Attribute(referenceName (TypeReference.named "Godot.ToolAttribute" [||]) :?> NameSyntax)
            )
          )
        )
      else
        declaration

    let nsDeclaration = SyntaxFactory.NamespaceDeclaration(ns).AddMembers(declaration)

    SyntaxFactory
      .CompilationUnit()
      .AddMembers(nsDeclaration)
      .WithLeadingTrivia(SyntaxFactory.Comment(header), SyntaxFactory.LineFeed)

  /// Builds a complete C# syntax tree from validated portable definitions.
  let generateSyntax generatedNamespace (typ: Type) : CompilationUnitSyntax =
    let ns = namespaceName generatedNamespace

    match Analysis.analyzeTypes [ typ ] with
    | [| node |] -> generateType ns node
    | _ -> invalidArg "typ" $"{typ.FullName}: GenerateGodotNode is required."

  let generateDefinitions generatedNamespace (nodes: NodeDefinition array) =
    let ns = namespaceName generatedNamespace

    nodes
    |> Array.map (fun node ->
      { Name = node.Name + ".cs"
        Content = generateType ns node |> render }
    )
    |> Array.sortBy _.Name

  let generateTypes generatedNamespace (types: seq<Type>) =
    namespaceName generatedNamespace
    |> ignore

    Analysis.analyzeTypes types
    |> generateDefinitions generatedNamespace

  let generate generatedNamespace (assemblies: seq<Assembly>) =
    assemblies
    |> Seq.collect _.ExportedTypes
    |> generateTypes generatedNamespace

  /// Writes through the manifest-backed transaction manager.
  let writeFiles outputDirectory files =
    Output.write Output.defaults outputDirectory files
    |> ignore
