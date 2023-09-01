using Godot;

public partial class SampleNode : Node
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GD.Print("Hello Godot from C#!");
        GD.Print(FSLib.Say.hello("Godot"));
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
