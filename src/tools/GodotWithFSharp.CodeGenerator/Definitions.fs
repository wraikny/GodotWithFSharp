namespace GodotWithFSharp.CodeGenerator

open System

type GeneratedFile = { Name: string; Content: string }

/// Portable type information; definitions never retain loaded assemblies or reflection objects.
type TypeReference =
  | Void
  | Named of TypeNamePart array
  | Array of TypeReference * rank: int

and TypeNamePart =
  { Name: string
    Arguments: TypeReference array }

module TypeReference =
  let named (name: string) arguments =
    let parts =
      name.Split('.')
      |> Microsoft.FSharp.Collections.Array.map (fun part -> { Name = part; Arguments = [||] })

    parts.[parts.Length - 1] <-
      { parts.[parts.Length - 1] with
          Arguments = arguments }

    Named parts

  let rec ofType (typ: Type) =
    if typ.FullName = "System.Void" then
      Void
    elif
      typ.IsByRef
      || typ.IsPointer
      || typ.ContainsGenericParameters
    then
      invalidArg "typ" $"Unsupported C# type: {typ}."
    elif typ.IsArray then
      Array(ofType (typ.GetElementType()), typ.GetArrayRank())
    else
      let arguments =
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

      definition.FullName.Split('+')
      |> Microsoft.FSharp.Collections.Array.collect (fun part -> part.Split('.'))
      |> Microsoft.FSharp.Collections.Array.map (fun part ->
        let segments = part.Split('`')
        let count = if segments.Length = 1 then 0 else int segments.[1]

        let values =
          arguments
          |> Microsoft.FSharp.Collections.Array.skip index
          |> Microsoft.FSharp.Collections.Array.take count
          |> Microsoft.FSharp.Collections.Array.map ofType

        index <- index + count

        { Name = segments.[0]
          Arguments = values }
      )
      |> Named

/// Attribute literals supported by C# generation; no boxed values escape metadata inspection.
[<RequireQualifiedAccess>]
type DefaultConstant =
  | Null
  | Boolean of bool
  | String of string
  | Character of char
  | Byte of byte
  | SByte of sbyte
  | Int16 of int16
  | UInt16 of uint16
  | Int32 of int
  | UInt32 of uint32
  | Int64 of int64
  | UInt64 of uint64
  | Single of float32
  | Double of double

/// The static member to access when Godot constructs a node or evaluates defaults.
[<RequireQualifiedAccess>]
type DefaultProviderMember =
  | Property of name: string
  | Method of name: string

type DefaultExpression =
  | Constant of DefaultConstant
  | StaticProvider of declaringType: TypeReference * providerMember: DefaultProviderMember

type ExportDefinition =
  { Name: string
    Type: TypeReference
    Hint: int
    HintString: string
    Default: DefaultExpression }

type OverrideDefinition =
  { Name: string
    ImplementationName: string
    ReturnType: TypeReference
    Parameters: TypeReference array }

type NodeDefinition =
  { Name: string
    Implementation: TypeReference
    BaseType: TypeReference
    Exports: ExportDefinition array
    Overrides: OverrideDefinition array
    Tool: bool
    DisposeImplementation: bool
    ImplementsDisposable: bool }

type GenerationDiagnostic =
  { Code: string
    Target: string
    Message: string }

type GenerationException(diagnostics: GenerationDiagnostic array) =
  inherit
    ArgumentException(
      diagnostics
      |> Microsoft.FSharp.Collections.Array.map (fun d -> $"{d.Code} {d.Target}: {d.Message}")
      |> String.concat "\n"
    )

  member _.Diagnostics = diagnostics
