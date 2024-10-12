//if TOOLS
using System;
using System.Diagnostics;
using Godot;

[Tool]
public partial class PPEntityNode : Node
{
    private static GDScript _utils = GD.Load<GDScript>("res://addons/planetary_processing/pp_utils.gd");

    [Signal]
	public delegate void StateChangedEventHandler(Godot.Collections.Dictionary<string, Variant> newState);
	

    [ExportCategory("Entity Properties")]
    private string _type = "";
    public string Type {
        get { return _GetType(); }
        set { _SetType(value); }
    }
    public string EntityId = "";
    private PPRootNode _ppRootNode;
    private bool _isInstance;

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        Godot.Collections.Array<Godot.Collections.Dictionary> properties = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            new Godot.Collections.Dictionary()
            {
                {"name", "Type"},
                {"type", (int)Variant.Type.String},
                {"usage", _isInstance ? (int)PropertyUsageFlags.Editor : (int)PropertyUsageFlags.Default}
            }
        };
            
        return properties;
    }

    private void _SetType(string newType)
    {
        _type = newType;
    }

    private string _GetType()
    {
        return _type;
    }

    public override void _EnterTree()
    {
        _isInstance = GetTree().EditedSceneRoot != GetParent();
        if (Engine.IsEditorHint())
        {
            if (!_isInstance)
            {
                if (string.IsNullOrEmpty(Type))
                {
                    // set the default type value based on the name
                    Type = GetParent().Name;
                }
            }
                
            return;
        }
            
        _ppRootNode = (PPRootNode)GetTree().CurrentScene.GetNode("PPRootNode");
        if (_ppRootNode == null)
        {
            throw new Exception("PPRootNode not present as direct child of parent scene");
        }
        // connect to events from the root
        _ppRootNode.EntityStateChanged += _OnEntityStateChange;
    }
	
    private void _OnEntityStateChange(string newEntityId, Godot.Collections.Dictionary<string, Variant> newState)
    {
        if (newEntityId == EntityId)
        {
            EmitSignal(SignalName.StateChanged, newState);
        } 
    } 
}
//endif