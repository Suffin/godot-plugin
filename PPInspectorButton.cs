using Godot;
public partial class PPInspectorButton : MarginContainer
{
    public PPInspectorButton(GodotObject obj, string text)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Button button = new();
        AddChild(button);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Text = text;
        button.Connect("button_down",  Callable.From(() => {obj.Call("_on_button_pressed", text);}));
    }
}