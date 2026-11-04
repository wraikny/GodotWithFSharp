namespace GodotWithFSharp.CodeGenerator

open System
open System.Reflection
open System.Collections.Generic

module Analysis =
  let private attributeName = "GodotWithFSharp.GenerateGodotNode"
  let private attributes (memberInfo: MemberInfo) = memberInfo.GetCustomAttributesData()

  let internal attribute name memberInfo =
    attributes memberInfo
    |> Seq.tryFind (fun a -> a.AttributeType.FullName = name)

  let private argument index (attr: CustomAttributeData) = attr.ConstructorArguments.[index].Value

  let private accessibleMethod (method: MethodBase) =
    not (isNull method)
    && (method.IsPublic
        || method.IsFamily
        || method.IsFamilyOrAssembly)

  let private accessibleMember (candidate: MemberInfo) =
    match candidate with
    | :? MethodBase as method -> accessibleMethod method
    | :? FieldInfo as field ->
      field.IsPublic
      || field.IsFamily
      || field.IsFamilyOrAssembly
    | :? PropertyInfo as prop ->
      prop.GetAccessors(true)
      |> Array.exists (fun method -> accessibleMethod method)
    | :? EventInfo as event ->
      [| event.AddMethod; event.RemoveMethod; event.RaiseMethod |]
      |> Array.exists (fun method -> accessibleMethod method)
    | :? Type as typ ->
      typ.IsNestedPublic
      || typ.IsNestedFamily
      || typ.IsNestedFamORAssem
    | _ -> false

  let private memberFlags =
    BindingFlags.Instance
    ||| BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic
    ||| BindingFlags.FlattenHierarchy

  let private notificationSignature (method: MethodInfo) =
    let parameters = method.GetParameters()

    method.Name = "_Notification"
    && not method.IsGenericMethod
    && method.ReturnType.FullName = "System.Void"
    && parameters.Length = 1
    && parameters.[0].ParameterType.FullName = "System.Int32"

  let private validateAutomaticMembers name (baseType: Type) autoDispose (memberNames: HashSet<string>) report =
    if autoDispose then
      if memberNames.Contains "_Notification" then
        report
          "GFS015"
          ""
          "Automatic disposal callback '_Notification' conflicts with a generated class or property name."

      if name = "_godotImplementationDisposed" then
        report
          "GFS015"
          ""
          "Automatic disposal field '_godotImplementationDisposed' conflicts with the generated class name."

      if not (isNull baseType) then
        let overridable =
          baseType.GetMethods(
            BindingFlags.Instance
            ||| BindingFlags.Public
          )
          |> Array.exists (fun method ->
            notificationSignature method
            && method.IsVirtual
            && not method.IsFinal
          )

        if not overridable then
          report "GFS017" "" "Automatic disposal requires an overridable public _Notification(int) method."

  let rec internal inherits name (typ: Type) =
    not (isNull typ)
    && (typ.FullName = name
        || inherits name typ.BaseType)

  let private scalars =
    set
      [ "System.Boolean"
        "System.Byte"
        "System.SByte"
        "System.Int16"
        "System.UInt16"
        "System.Int32"
        "System.UInt32"
        "System.Int64"
        "System.UInt64"
        "System.Single"
        "System.Double"
        "System.Char"
        "System.String" ]

  let private values =
    set
      [ "Godot.Vector2"
        "Godot.Vector2I"
        "Godot.Rect2"
        "Godot.Rect2I"
        "Godot.Vector3"
        "Godot.Vector3I"
        "Godot.Transform2D"
        "Godot.Vector4"
        "Godot.Vector4I"
        "Godot.Plane"
        "Godot.Quaternion"
        "Godot.Aabb"
        "Godot.Basis"
        "Godot.Transform3D"
        "Godot.Projection"
        "Godot.Color"
        "Godot.StringName"
        "Godot.NodePath"
        "Godot.Rid"
        "Godot.Callable"
        "Godot.Signal"
        "Godot.Variant"
        "Godot.Collections.Array"
        "Godot.Collections.Dictionary" ]

  let private packedElements =
    set
      [ "System.Byte"
        "System.Int32"
        "System.Int64"
        "System.Single"
        "System.Double"
        "System.String"
        "Godot.Vector2"
        "Godot.Vector3"
        "Godot.Vector4"
        "Godot.Color" ]

  let rec internal variantCompatible (typ: Type) =
    if
      typ.IsEnum
      || scalars.Contains typ.FullName
      || values.Contains typ.FullName
      || inherits "Godot.GodotObject" typ
    then
      true
    elif typ.IsArray then
      let element = typ.GetElementType()

      typ.GetArrayRank() = 1
      && (inherits "Godot.GodotObject" element
          || packedElements.Contains element.FullName)
    elif typ.IsGenericType then
      let name = typ.GetGenericTypeDefinition().FullName

      (name = "Godot.Collections.Array`1"
       || name = "Godot.Collections.Dictionary`2")
      && (typ.GetGenericArguments()
          |> Array.forall variantCompatible)
    else
      false

  let private validateImplementation (typ: Type) (baseType: Type) report =
    if
      isNull baseType
      || not (inherits "Godot.Node" baseType)
      || baseType.IsSealed
      || baseType.ContainsGenericParameters
      || not baseType.IsVisible
    then
      report "GFS004" "" "The generated base type must be a public inheritable Godot Node type."

    if
      not typ.IsClass
      || not typ.IsVisible
      || typ.IsAbstract
      || typ.ContainsGenericParameters
    then
      report "GFS005" "" "Implementation types must be public, concrete and closed classes."

    if not (isNull baseType) then
      if
        not (
          typ.GetInterfaces()
          |> Array.exists (fun i ->
            i.IsGenericType
            && i.GetGenericTypeDefinition().FullName = "GodotWithFSharp.Impl`1"
            && TypeIdentity.same (i.GetGenericArguments().[0]) baseType
          )
        )
      then
        report "GFS006" "" $"Implement Impl<{baseType.FullName}>."

      if
        not (
          typ.GetConstructors()
          |> Array.exists (fun c ->
            let ps = c.GetParameters() in

            ps.Length = 1
            && ps.[0].ParameterType.IsAssignableFrom baseType
          )
        )
      then
        report "GFS007" "" "A public constructor accepting the generated node is required."

      let baseCtor =
        baseType.GetConstructor(
          BindingFlags.Instance
          ||| BindingFlags.Public
          ||| BindingFlags.NonPublic,
          null,
          Type.EmptyTypes,
          null
        )

      if
        isNull baseCtor
        || not (
          baseCtor.IsPublic
          || baseCtor.IsFamily
          || baseCtor.IsFamilyOrAssembly
        )
      then
        report "GFS008" "" "The base node needs an accessible parameterless constructor."

  let private analyzeExports (typ: Type) (baseType: Type) (memberNames: HashSet<string>) report warn checkName isValid =
    let exports = ResizeArray<ExportDefinition>()

    for prop in
      typ.GetProperties()
      |> Array.sortBy _.Name do
      match attribute "Godot.ExportAttribute" prop with
      | None -> ()
      | Some export ->
        checkName ("." + prop.Name) prop.Name

        if not (memberNames.Add prop.Name) then
          report "GFS009" ("." + prop.Name) $"Reserved or duplicate property name: {prop.Name}."

        if
          prop.GetIndexParameters().Length <> 0
          || isNull prop.GetMethod
          || isNull prop.SetMethod
          || not prop.GetMethod.IsPublic
          || not prop.SetMethod.IsPublic
          || prop.GetMethod.IsStatic
          || prop.SetMethod.IsStatic
          || (prop.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
              |> Array.exists (fun t -> t.FullName = "System.Runtime.CompilerServices.IsExternalInit"))
        then
          report "GFS010" ("." + prop.Name) "Exported properties must have public instance get/set accessors."

        if not (variantCompatible prop.PropertyType) then
          report "GFS011" ("." + prop.Name) $"Unsupported Godot Export type: {prop.PropertyType}."

        let defaultValue = ExportDefaults.read prop

        match defaultValue with
        | Error message -> report "GFS020" ("." + prop.Name) message
        | Ok _ -> ()

        let fieldName = "_godotExport_" + prop.Name

        if not (memberNames.Add fieldName) then
          report "GFS015" ("." + prop.Name) $"Generated backing field '{fieldName}' conflicts with another member."

        if isValid () then
          if not (isNull baseType) then
            baseType.GetMember(prop.Name, memberFlags)
            |> Array.tryFind accessibleMember
            |> Option.iter (fun inherited ->
              warn
                "GFS018"
                ("." + prop.Name)
                $"Export hides inherited member '{inherited.DeclaringType.FullName}.{inherited.Name}'. Rename the exported property to avoid hiding a base member."
            )

          let hint =
            if export.ConstructorArguments.Count > 0 then
              Convert.ToInt32(argument 0 export)
            else
              0

          let hintString =
            if export.ConstructorArguments.Count > 1 then
              argument 1 export :?> string
            else
              ""

          exports.Add
            { Name = prop.Name
              Type = TypeReference.ofType prop.PropertyType
              Hint = hint
              HintString = if isNull hintString then "" else hintString
              Default =
                match defaultValue with
                | Ok value -> value
                | Error _ -> failwith "Validated default missing." }

    exports.ToArray()

  let private analyzeOverrides (typ: Type) (baseType: Type) (memberNames: HashSet<string>) report checkName isValid =
    let overrides = ResizeArray<OverrideDefinition>()
    let signatures = HashSet<string>()

    for method in
      typ.GetMethods()
      |> Array.sortBy (fun m -> m.ToString()) do
      match attribute "GodotWithFSharp.GenerateGodotOverride" method with
      | None -> ()
      | Some overrideAttr ->
        let target =
          if
            overrideAttr.ConstructorArguments.Count = 0
            || isNull (argument 0 overrideAttr)
          then
            method.Name
          else
            argument 0 overrideAttr :?> string

        checkName ("." + method.Name) target
        checkName ("." + method.Name) method.Name

        let ps =
          method.GetParameters()
          |> Array.map _.ParameterType

        if
          method.IsStatic
          || method.IsGenericMethod
          || (ps
              |> Array.exists (fun p ->
                p.IsByRef
                || p.IsPointer
                || p.ContainsGenericParameters
              ))
          || method.ReturnType.IsByRef
          || method.ReturnType.IsPointer
        then
          report "GFS012" ("." + method.Name) "Unsupported override signature."

        let matched =
          if isNull baseType then
            None
          else
            baseType.GetMethods(
              BindingFlags.Instance
              ||| BindingFlags.Public
            )
            |> Array.tryFind (fun m ->
              m.Name = target
              && m.IsVirtual
              && not m.IsFinal
              && not m.IsGenericMethod
              && TypeIdentity.same m.ReturnType method.ReturnType
              && (let bp =
                    m.GetParameters()
                    |> Array.map _.ParameterType in

                  bp.Length = ps.Length
                  && Array.forall2 TypeIdentity.same bp ps)
            )

        if Option.isNone matched then
          report
            "GFS013"
            ("." + method.Name)
            $"No matching public virtual Godot method: {target} (implementation: {method.Name})."

        let signature =
          target
          + "("
          + (ps
             |> Array.map _.FullName
             |> String.concat ",")
          + ")"

        if not (signatures.Add signature) then
          report "GFS014" ("." + method.Name) $"Duplicate override target: {target}."

        if memberNames.Contains target then
          report
            "GFS015"
            ("." + method.Name)
            $"Override target '{target}' conflicts with a generated class or property name."

        if isValid () then
          overrides.Add
            { Name = target
              ImplementationName = method.Name
              ReturnType = TypeReference.ofType method.ReturnType
              Parameters = ps |> Array.map TypeReference.ofType }

    overrides.ToArray()

  let private validateAbstractMembers (baseType: Type) autoDispose (overrides: OverrideDefinition array) report =
    if not (isNull baseType) then
      for required in
        baseType.GetMethods(
          BindingFlags.Instance
          ||| BindingFlags.Public
          ||| BindingFlags.NonPublic
        )
        |> Array.filter _.IsAbstract do
        if
          not (
            (autoDispose
             && notificationSignature required)
            || (overrides
                |> Seq.exists (fun m ->
                  m.Name = required.Name
                  && m.Parameters = (required.GetParameters()
                                     |> Array.map (fun p -> TypeReference.ofType p.ParameterType))
                ))
          )
        then
          report "GFS016" "" $"Abstract base member must be implemented: {required.Name}."

  /// Validates a whole batch and returns nonfatal Export hiding warnings alongside portable definitions.
  /// Invalid batches raise GenerationException and cannot produce output.
  let analyzeTypesWithWarnings (types: seq<Type>) : AnalysisResult =
    let diagnostics = ResizeArray<GenerationDiagnostic>()
    let warnings = ResizeArray<GenerationDiagnostic>()
    let nodes = ResizeArray<NodeDefinition>()
    let names = HashSet<string>(StringComparer.OrdinalIgnoreCase)

    for typ in types do
      match attribute attributeName typ with
      | None -> ()
      | Some attr ->
        let count = diagnostics.Count

        let flag name fallback =
          attr.NamedArguments
          |> Seq.tryFind (fun value -> value.MemberName = name)
          |> Option.map (fun value -> value.TypedValue.Value :?> bool)
          |> Option.defaultValue fallback

        let disposeImplementation = flag "DisposeImplementation" true

        let implementsDisposable =
          typ.GetInterfaces()
          |> Array.exists (fun contract -> contract.FullName = "System.IDisposable")

        let autoDispose =
          disposeImplementation
          && implementsDisposable

        let report code target message =
          diagnostics.Add
            { Code = code
              Target = typ.FullName + target
              Message = message }

        let warn code target message =
          warnings.Add
            { Code = code
              Target = typ.FullName + target
              Message = message }

        let checkName target name =
          try
            CSharpSyntax.identifier name |> ignore
          with :? ArgumentException as error ->
            report "GFS001" target error.Message

        let name = argument 0 attr :?> string
        let baseType = argument 1 attr :?> Type
        checkName "" name

        if name = "Impl" then
          report "GFS002" "" "The generated class name 'Impl' conflicts with the runtime property."

        if not (names.Add name) then
          report "GFS003" "" $"Duplicate generated filename: {name}.cs."

        validateImplementation typ baseType report
        let memberNames = HashSet<string>(StringComparer.Ordinal)
        memberNames.Add "Impl" |> ignore

        memberNames.Add "_godotImplementationDisposed"
        |> ignore

        memberNames.Add name |> ignore


        let isValid () = diagnostics.Count = count
        let exports = analyzeExports typ baseType memberNames report warn checkName isValid
        validateAutomaticMembers name baseType autoDispose memberNames report
        let overrides = analyzeOverrides typ baseType memberNames report checkName isValid
        validateAbstractMembers baseType autoDispose overrides report

        if diagnostics.Count = count then
          nodes.Add
            { Name = name
              Implementation = TypeReference.ofType typ
              BaseType = TypeReference.ofType baseType
              Exports = exports
              Overrides = overrides
              Tool = flag "Tool" false
              DisposeImplementation = disposeImplementation
              ImplementsDisposable = implementsDisposable }

    if diagnostics.Count > 0 then
      raise (GenerationException(diagnostics.ToArray()))

    { Nodes = nodes.ToArray()
      Warnings = warnings.ToArray() }

  /// Returns validated definitions only. Use analyzeTypesWithWarnings to inspect nonfatal diagnostics.
  let analyzeTypes (types: seq<Type>) = (analyzeTypesWithWarnings types).Nodes
