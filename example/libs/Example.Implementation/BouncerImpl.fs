namespace Example.Implementation

open Godot
open GodotWithFSharp

[<GenerateGodotNode("Bouncer", typeof<Node2D>)>]
type BouncerImpl(node: Node2D) =
  let mutable direction = Vector2(1.0f, 0.65f).Normalized()

  interface Impl<Node2D>

  [<Export(PropertyHint.Range, "40,600,10")>]
  [<GodotExportDefault(180.0f)>]
  member val Speed = 180.0f with get, set

  member _.Reset() =
    node.Position <- Vector2(160.0f, 180.0f)
    direction <- Vector2(1.0f, 0.65f).Normalized()

  [<GenerateGodotOverride("_Process")>]
  member this.Update(delta: double) =
    let size = node.GetViewportRect().Size

    let mutable position =
      node.Position
      + direction * this.Speed * float32 delta

    if
      position.X < 24.0f
      || position.X > size.X - 24.0f
    then
      direction.X <- -direction.X

    if
      position.Y < 120.0f
      || position.Y > size.Y - 24.0f
    then
      direction.Y <- -direction.Y

    position.X <- Mathf.Clamp(position.X, 24.0f, size.X - 24.0f)
    position.Y <- Mathf.Clamp(position.Y, 120.0f, size.Y - 24.0f)
    node.Position <- position
