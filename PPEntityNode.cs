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

    public string GetSwarmplayRepoDirectory()
    {
        if (Engine.IsEditorHint())
        {
            EditorSettings settings = EditorInterface.Singleton.GetEditorSettings();
            string swarmplayRepoDirectory = settings.GetSetting("user/swarmplay_repo_directory").ToString();
            if (swarmplayRepoDirectory.Trim() == "")
            {
                throw new Exception("The SwarmPlay repo directory path is not set.");
            }
                
            DirAccess dirAccess = DirAccess.Open(swarmplayRepoDirectory);
            if (dirAccess == null)
            {
                throw new Exception($"The SwarmPlay repo directory does not exist: {swarmplayRepoDirectory}");
            }
            return swarmplayRepoDirectory;
        }
           
        return "";
    }
	

    [ExportCategory("Entity Properties")]
    [Export(PropertyHint.MultilineText)]
    public string Data = "";
    [Export]
    public bool Chunkloader = false;
    private string _type = "";
    public string Type {
        get { return _GetType(); }
        set { _SetType(value); }
    }
    private string _luaPath = "";
    public string EntityId = "";
    private PPRootNode _ppRootNode;
    private bool _isInstance;

    private void _on_button_pressed(string text)
    {
        if (string.IsNullOrEmpty(Type))
        {
            throw new Exception("no type provided");
        }
        if (FileAccess.FileExists(_luaPath))
        {
            throw new Exception($"lua file named {Type}.lua already exists");
        }
        _utils.Call("write_string_to_file", _luaPath, @"local function init(e)
end

local function update(e, dt)
end

local function message(e, msg)
end

return {init=init,update=update,message=message}
            "
        );
        _utils.Call("refresh_filesystem");
    }


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
        if (!_isInstance)
        {
            properties.Add(
            new Godot.Collections.Dictionary()
            {
                {"name", "pp_button_generate_lua_skeleton_file"},
                {"type", (int)Variant.Type.String},
                {"usage", (int)PropertyUsageFlags.Default}
            });
            properties.Add(
            new Godot.Collections.Dictionary()
            {
                {"name", "_luaPath"},
                {"type", (int)Variant.Type.String},
                {"usage", (int)PropertyUsageFlags.Editor}
            });
        }
            
        return properties;
    }

    private void _SetType(string newType)
    {
        string basePath = GetSwarmplayRepoDirectory();
        _type = newType;
        if (Engine.IsEditorHint() && IsInsideTree())
        {
            if (string.IsNullOrEmpty(_type))
            {
                _luaPath = "";
            }
            else
            {
                _luaPath = $"{basePath}/entity/{_type}.lua";
                GD.Print($"Lua path set to {_luaPath}");
            }
        }       
    }

    private string _GetType()
    {
        return _type;
    }

    public override void _EnterTree()
    {
        string basePath = GetSwarmplayRepoDirectory();
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
                   
                if (string.IsNullOrEmpty(_luaPath))
                {
                    // select existing lua file for lua path if exists
                    string filepath = $"{basePath}/entity/{Type}.lua";
                    if (FileAccess.FileExists(filepath))
                    {
                        _luaPath = filepath;
                    }
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