namespace GodotWithFSharp.CodeGenerator

open System
open System.Text.RegularExpressions
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.CSharp.Syntax

/// Helpers for composing Roslyn nodes. No C# source fragments are parsed here.
module internal CSharpSyntax =
  let identifier (name: string) =
    if
      String.IsNullOrWhiteSpace name
      || not (Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
    then
      invalidArg "name" $"Invalid C# identifier: '{name}'."

    if
      SyntaxFacts.GetKeywordKind(name)
      <> SyntaxKind.None
      || SyntaxFacts.GetContextualKeywordKind(name)
         <> SyntaxKind.None
    then
      SyntaxFactory.Identifier(SyntaxTriviaList(), SyntaxKind.None, "@" + name, name, SyntaxTriviaList())
    else
      SyntaxFactory.Identifier(name)

  let identifierName name =
    SyntaxFactory.IdentifierName(identifier name)

  let namespaceName (name: string) : NameSyntax =
    if String.IsNullOrWhiteSpace name then
      invalidArg "namespace" "A namespace is required."

    let parts =
      name.Split('.')
      |> Array.map (
        identifierName
        >> fun n -> n :> SimpleNameSyntax
      )

    parts.[1..]
    |> Array.fold
      (fun (left: NameSyntax) right -> SyntaxFactory.QualifiedName(left, right) :> NameSyntax)
      (parts.[0] :> NameSyntax)

  let private globalName (parts: SimpleNameSyntax[]) : NameSyntax =
    let first =
      SyntaxFactory.AliasQualifiedName(SyntaxFactory.IdentifierName("global"), parts.[0]) :> NameSyntax

    parts.[1..]
    |> Array.fold (fun left right -> SyntaxFactory.QualifiedName(left, right) :> NameSyntax) first

  /// Builds a CLR type as syntax, including nested generics and array ranks.
  let rec typeName (typ: Type) : TypeSyntax =
    if typ = typeof<Void> then
      SyntaxFactory.PredefinedType(SyntaxFactory.Token SyntaxKind.VoidKeyword) :> TypeSyntax
    elif
      typ.IsByRef
      || typ.IsPointer
      || typ.ContainsGenericParameters
    then
      invalidArg "typ" $"Unsupported C# type: {typ}."
    elif typ.IsArray then
      let dimensions =
        Array.init (typ.GetArrayRank()) (fun _ -> SyntaxFactory.OmittedArraySizeExpression() :> ExpressionSyntax)

      let rank =
        SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SeparatedList<ExpressionSyntax>(dimensions))

      match typeName (typ.GetElementType()) with
      | :? ArrayTypeSyntax as element ->
        element.WithRankSpecifiers(element.RankSpecifiers.Insert(0, rank)) :> TypeSyntax
      | element -> SyntaxFactory.ArrayType(element).AddRankSpecifiers(rank) :> TypeSyntax
    else
      let args =
        if typ.IsGenericType then
          typ.GetGenericArguments()
        else
          [||]

      let definition =
        if typ.IsGenericType then
          typ.GetGenericTypeDefinition()
        else
          typ

      let mutable index = 0

      let parts =
        definition.FullName.Split('+')
        |> Array.collect (fun part -> part.Split('.'))
        |> Array.map (fun part ->
          let segments = part.Split('`')

          if segments.Length = 1 then
            identifierName segments.[0] :> SimpleNameSyntax
          else
            let count = int segments.[1]

            let values =
              args.[index .. index + count - 1]
              |> Array.map typeName

            index <- index + count

            SyntaxFactory
              .GenericName(identifier segments.[0])
              .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(values)))
            :> SimpleNameSyntax
        )

      globalName parts :> TypeSyntax

  let modifiers kinds =
    kinds
    |> Array.map SyntaxFactory.Token
    |> SyntaxFactory.TokenList

  let memberAccess (target: ExpressionSyntax) name =
    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, identifierName name)

  let assignment (left: ExpressionSyntax) (right: ExpressionSyntax) =
    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right)

  let getter (expression: ExpressionSyntax) =
    SyntaxFactory
      .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
      .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(expression)))

  let setter (target: ExpressionSyntax) =
    SyntaxFactory
      .AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
      .WithBody(SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(assignment target (identifierName "value"))))

  let render (syntax: CompilationUnitSyntax) =
    syntax.NormalizeWhitespace("    ", "\n").ToFullString()
    + "\n"
