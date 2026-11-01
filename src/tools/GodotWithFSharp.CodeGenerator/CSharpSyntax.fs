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

  /// Renders portable type definitions as Roslyn nodes.
  let rec referenceName reference : TypeSyntax =
    match reference with
    | Void -> SyntaxFactory.PredefinedType(SyntaxFactory.Token SyntaxKind.VoidKeyword) :> TypeSyntax
    | TypeReference.Array(element, rank) ->
      let dimensions =
        Array.init rank (fun _ -> SyntaxFactory.OmittedArraySizeExpression() :> ExpressionSyntax)

      let rankNode =
        SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SeparatedList<ExpressionSyntax>(dimensions))

      match referenceName element with
      | :? ArrayTypeSyntax as array -> array.WithRankSpecifiers(array.RankSpecifiers.Insert(0, rankNode)) :> TypeSyntax
      | element -> SyntaxFactory.ArrayType(element).AddRankSpecifiers(rankNode) :> TypeSyntax
    | Named parts ->
      parts
      |> Array.map (fun part ->
        if part.Arguments.Length = 0 then
          identifierName part.Name :> SimpleNameSyntax
        else
          SyntaxFactory
            .GenericName(identifier part.Name)
            .WithTypeArgumentList(
              SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(
                  part.Arguments
                  |> Array.map referenceName
                )
              )
            )
          :> SimpleNameSyntax
      )
      |> globalName
      :> TypeSyntax

  let typeName typ =
    TypeReference.ofType typ
    |> referenceName

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
