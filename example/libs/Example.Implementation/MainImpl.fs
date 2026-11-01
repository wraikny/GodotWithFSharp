namespace Example.Implementation

open System
open Godot
open GodotWithFSharp

[<GenerateGodotNode("Main", typeof<Node>)>]
type MainImpl(node: Node) =
  let smokeTest =
    OS.GetCmdlineUserArgs()
    |> Array.contains "--smoke-test"

  let mutable frames = 0
  let mutable initialPosition = Vector2.Zero
  let mutable resetCount = 0

  let bouncer () =
    node.GetNodeImpl<BouncerImpl>("Bouncer")

  let bouncerNode () =
    node.GetRuntimeNode<Node2D, BouncerImpl>("Bouncer")

  let status () = node.GetNode<Label>("UI/Status")
  let resetButton () = node.GetNode<Button>("UI/Reset")

  interface Impl<Node>

  [<GenerateGodotOverride>]
  member _._Ready() =
    initialPosition <- (bouncerNode ()).Position

    (resetButton ())
      .add_Pressed (
        Action(fun () ->
          (bouncer ()).Reset()
          resetCount <- resetCount + 1
        )
      )

  [<GenerateGodotOverride>]
  member _._Process(_delta: double) =
    frames <- frames + 1
    let position = (bouncerNode ()).Position

    (status ()).Text <-
      $"Speed: {(bouncer ()).Speed:F0} px/s   Position: {position.X:F0}, {position.Y:F0}   Resets: {resetCount}"

    if smokeTest && frames = 30 then
      // Check the scene export, forwarded callbacks, runtime helpers and signal wiring.
      let moved = position.DistanceTo(initialPosition) > 0.0f

      (resetButton ()).EmitSignal(Button.SignalName.Pressed)
      |> ignore

      let reset =
        (bouncerNode ()).Position = Vector2(160.0f, 180.0f)
        && resetCount = 1

      if
        moved
        && reset
        && (bouncer ()).Speed = 240.0f
      then
        GD.Print("EXAMPLE_SMOKE_TEST_OK")
        node.GetTree().Quit(0)
      else
        GD.PushError($"Example smoke test failed: moved={moved}, reset={reset}, speed={(bouncer ()).Speed}")
        node.GetTree().Quit(1)
