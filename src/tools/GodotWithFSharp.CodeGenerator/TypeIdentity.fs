namespace GodotWithFSharp.CodeGenerator

open System

/// Compares types across execution and metadata-only reflection contexts.
module internal TypeIdentity =
  let same (a: Type) (b: Type) =
    a.FullName = b.FullName
    && a.Assembly.GetName().Name = b.Assembly.GetName().Name
