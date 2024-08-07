#if TOOLS
using Godot;

[Tool]
public partial class PPPlugin : EditorPlugin
{
    private PPInspectorButtonPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        Script ppRootNodeScript = GD.Load<Script>("res://addons/planetary_processing_csharp/PPRootNode.cs");
        Script ppEntityNode = GD.Load<Script>("res://addons/planetary_processing_csharp/PPEntityNode.cs");
        AddCustomType("PPRootNode", "Node", ppRootNodeScript, GD.Load<Texture2D>("res://addons/planetary_processing_csharp/pp_logo.png"));
        AddCustomType("PPEntityNode", "Node", ppEntityNode, GD.Load<Texture2D>("res://addons/planetary_processing_csharp/pp_logo.png"));
        if (Engine.IsEditorHint())
        {
            _inspectorPlugin = new PPInspectorButtonPlugin();
            AddInspectorPlugin(_inspectorPlugin);
            EditorSettings settings = EditorInterface.Singleton.GetEditorSettings();
            Variant textfileExtensions = settings.GetSetting("docks/filesystem/textfile_extensions");
            settings.SetSetting("docks/filesystem/textfile_extensions", $"{textfileExtensions},lua,csproj");
        }
    }

    public override void _ExitTree()
    {
        RemoveCustomType("PPRootNode");
        RemoveCustomType("PPEntityNode");
        if (Engine.IsEditorHint())
        {
            RemoveInspectorPlugin(_inspectorPlugin);
        }
    }
}
#endif