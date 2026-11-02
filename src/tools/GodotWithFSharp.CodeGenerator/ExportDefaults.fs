namespace GodotWithFSharp.CodeGenerator

open System
open System.Reflection

module internal ExportDefaults =
  let private attribute name (prop: PropertyInfo) =
    prop.GetCustomAttributesData()
    |> Seq.tryFind (fun a -> a.AttributeType.FullName = name)

  let rec private unwrap (value: CustomAttributeTypedArgument) =
    match value.Value with
    | :? CustomAttributeTypedArgument as nested -> unwrap nested
    | raw -> raw

  let private sameType (a: Type) (b: Type) =
    a.FullName = b.FullName
    && a.Assembly.GetName().Name = b.Assembly.GetName().Name

  let private provider (prop: PropertyInfo) (attr: CustomAttributeData) =
    let typ = attr.ConstructorArguments.[0].Value :?> Type
    let name = attr.ConstructorArguments.[1].Value :?> string

    let flags =
      BindingFlags.Public
      ||| BindingFlags.Static

    if
      isNull typ
      || not typ.IsVisible
      || typ.ContainsGenericParameters
      || String.IsNullOrWhiteSpace name
    then
      Error "Default providers require a public closed type and a member name."
    else
      let properties =
        typ.GetProperties(flags)
        |> Array.filter (fun p ->
          p.Name = name
          && p.GetIndexParameters().Length = 0
          && not (isNull p.GetMethod)
          && p.GetMethod.IsPublic
          && sameType p.PropertyType prop.PropertyType
        )

      let methods =
        typ.GetMethods(flags)
        |> Array.filter (fun m ->
          m.Name = name
          && not m.IsGenericMethod
          && m.GetParameters().Length = 0
          && sameType m.ReturnType prop.PropertyType
        )

      if properties.Length + methods.Length <> 1 then
        Error
          $"Default provider '{typ.FullName}.{name}' must be one public static property or parameterless method returning {prop.PropertyType}."
      else
        Ok(StaticProvider(TypeReference.ofType typ, name, methods.Length = 1))

  let read (prop: PropertyInfo) =
    let constant = attribute "GodotWithFSharp.GodotExportDefault" prop
    let factory = attribute "GodotWithFSharp.GodotExportDefaultProvider" prop

    match constant, factory with
    | None, None ->
      Error "Export requires GodotExportDefault or GodotExportDefaultProvider; defaults must be declared explicitly."
    | Some _, Some _ -> Error "Use exactly one default declaration per Export."
    | None, Some attr -> provider prop attr
    | Some attr, None when attr.ConstructorArguments.Count <> 1 ->
      Error "Use the single-value GodotExportDefault constructor; type converters are not executed during generation."
    | Some attr, None ->
      let raw = unwrap attr.ConstructorArguments.[0]

      let validNull =
        isNull raw
        && not prop.PropertyType.IsValueType

      let validValue =
        not (isNull raw)
        && ((prop.PropertyType.IsEnum
             && prop.PropertyType.GetEnumUnderlyingType().FullName = raw.GetType().FullName)
            || prop.PropertyType.FullName = raw.GetType().FullName)

      if validNull || validValue then
        Ok(Constant raw)
      else
        Error $"GodotExportDefault must match the exported property type {prop.PropertyType}."
