#if TOOLS
using Godot;

public partial class PPInspectorButtonPlugin : EditorInspectorPlugin
{
    public string ButtonText;

    public override bool _CanHandle(GodotObject @object)
    {
        return true;
    }

    public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
    {
        if (name.StartsWith("pp_button_"))
        {
            string s = name.Split("pp_button_")[1];
            s = s.Replace("_", " ");
            s = s.Capitalize();
            AddCustomControl( new PPInspectorButton(@object, s) );
            return true;
        }
            
        return false;
    }     
}
#endif