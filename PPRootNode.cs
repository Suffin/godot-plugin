//if TOOLS
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Godot;

[Tool]
public partial class PPRootNode : Node
{
    private const string _CSHARPERRORMSG = "C// solution not created. Trigger Project > Tools > C// > Create C// Solution";
    private static GDScript _ppHTTPClient = GD.Load<GDScript>("res://addons/planetary_processing_csharp/pp_editor_http_client.gd");
    private static GDScript _utils = GD.Load<GDScript>("res://addons/planetary_processing_csharp/pp_utils.gd");

    [Signal]
	public delegate void EntityStateChangedEventHandler(string entityId, Godot.Collections.Dictionary newState);
    [Signal]
	public delegate void NewPlayerEntityEventHandler(string entityId, Godot.Collections.Dictionary state);
    [Signal]
	public delegate void NewEntityEventHandler(string entityId, Godot.Collections.Dictionary state);
    [Signal]
	public delegate void PlayerAuthenticatedEventHandler(string playerUuid);
    [Signal]
	public delegate void PlayerAuthenticationErrorEventHandler(string err);
    [Signal]
	public delegate void PlayerUnauthenticatedEventHandler();
    [Signal]
	public delegate void PlayerConnectedEventHandler();
    [Signal]
	public delegate void PlayerDisconnectedEventHandler();
    [Signal]
	public delegate void RemoveEntityEventHandler(string entityId);

    [ExportCategory("Game Config")]
    public string GameId = "";

    //public string username = "";
    //public string password = "";
    private bool _loggedIn = false;
    private Godot.Collections.Array<string> _registeredEntities = new Godot.Collections.Array<string>();
    private string _swarmplayRepoDirectory = "";
    public string SwarmplayRepoDirectory {
        get { return _GetSwarmplayRepoDirectory(); }
        set { _SetSwarmplayRepoDirectory(value); }
    }

    private bool _csprojReferenceExists = false;
    private GodotObject _client = (GodotObject)_ppHTTPClient.New();
    private bool _playerIsConnected = false;
    private string _playerUuid = null;
    private Godot.Timer _timer;
    private int _timerWaitInS = 10;
    private SDKNode _sdkNode;

    private void _SetSwarmplayRepoDirectory(string newDir)
    {
        _swarmplayRepoDirectory = newDir;

        if (Engine.IsEditorHint())
        {
            EditorSettings settings = EditorInterface.Singleton.GetEditorSettings();
            settings.SetSetting("user/swarmplay_repo_directory", newDir);
        }
    }


    private string _GetSwarmplayRepoDirectory()
    {
        if (Engine.IsEditorHint())
        {
            EditorSettings settings = EditorInterface.Singleton.GetEditorSettings();
            Variant dir = settings.GetSetting("user/swarmplay_repo_directory");
            return dir.ToString();
        }
            
        return _swarmplayRepoDirectory;
    }  

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }
        if (GameId == "")
        {
            throw new Exception("Planetary Processing Game ID not configured");
        }
        _sdkNode = new SDKNode();
        _sdkNode.SetGameID(ulong.Parse(GameId));
        
        Godot.Timer playerConnectedTimer = new Godot.Timer();
        AddChild(playerConnectedTimer);
        playerConnectedTimer.WaitTime = 1.0;
        playerConnectedTimer.Timeout += _OnPlayerConnectedTimerTimeout;
        playerConnectedTimer.Start();
    }
        

    private void _OnPlayerConnectedTimerTimeout()
    {
        bool newPlayerIsConnected = _sdkNode.GetIsConnected();
        if (!_playerIsConnected && newPlayerIsConnected)
        {
            EmitSignal(SignalName.PlayerConnected);
        }
            
        if (_playerIsConnected && !newPlayerIsConnected)
        {
            EmitSignal(SignalName.PlayerDisconnected);
        }
        _playerIsConnected = newPlayerIsConnected;
    }
       

    public void AuthenticatePlayer(string username, string password)
    {
        Thread thread = new Thread(() => _AuthenticatePlayerThread(username, password));
        thread.Start();
    }
        
    private void _AuthenticatePlayerThread(string username, string password)
    {
        string err = _sdkNode.Connect(username, password);
        if (!string.IsNullOrEmpty(err))
        {
            _playerUuid = null;
            CallDeferred("emit_signal", SignalName.PlayerAuthenticationError, err);
            return;
        }
            
        _playerUuid = _sdkNode.GetUUID();
        CallDeferred("emit_signal", SignalName.PlayerAuthenticated, _playerUuid);
    }
    
    public void message(Godot.Collections.Dictionary<string, Variant> msg)
    {
        _sdkNode.Message(msg);
    }

    public override void _Process(double delta)
	{
        if (Engine.IsEditorHint())
        {
            return;
        }
        _sdkNode.Update();
        // iterate through entities, emit changes
        Godot.Collections.Dictionary<string, Variant> entities = _sdkNode.GetEntities();
        Godot.Collections.Array<string> entityIds = new Godot.Collections.Array<string>(entities.Keys);
        Godot.Collections.Array<string> toRemoveEntityIds = _registeredEntities.Duplicate();
        foreach (string entity_id in entityIds)
        {
            toRemoveEntityIds.Remove(entity_id);
            Variant entityData = entities[entity_id];
            if (_registeredEntities.IndexOf(entity_id) != -1)
            {
                EmitSignal(SignalName.EntityStateChanged, entity_id, entityData);
            }
            else
            {
                if (entity_id == _playerUuid)
                {
                    EmitSignal(SignalName.NewPlayerEntity, _playerUuid, entityData);
                    GD.Print($"Fired new_player_entity: {_playerUuid}");
                }
                else
                {
                    EmitSignal(SignalName.NewEntity, entity_id, entityData);
                    GD.Print($"Fired new_entity: {entity_id}", entityData);
                }
            }  
        }
            
        // remove missing entities
        foreach (string entity_id in toRemoveEntityIds)
        {
            if (entity_id != _playerUuid)
            {
                EmitSignal(SignalName.RemoveEntity, entity_id);
                GD.Print($"Fired remove_entity: {entity_id}");
            }
        }

        _registeredEntities = entityIds;
	}

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        var properties = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            new Godot.Collections.Dictionary()
            {
                {"name", "GameId"},
                {"type", (int)Variant.Type.String},
                {"usage", _loggedIn ? (int)PropertyUsageFlags.ReadOnly : (int)PropertyUsageFlags.Default}
            },
            new Godot.Collections.Dictionary()
            {
                {"name", "SwarmplayRepoDirectory"},
                {"type", (int)Variant.Type.String},
                {"hint", (int)PropertyHint.Dir},
                {"usage", (int)PropertyUsageFlags.Default | (int)PropertyUsageFlags.Editor},
                {"hint_string", ""}
            },
            new Godot.Collections.Dictionary()
            {
                {"name", "pp_button_generate_init.json"},
                {"type", (int)Variant.Type.String},
                {"usage", (int)PropertyUsageFlags.Default}
            },
        };
        if (!_csprojReferenceExists)
        {
            properties.Add(new Godot.Collections.Dictionary()
            {
                {"name", "pp_button_add_csproj_reference"},
                {"type", (int)Variant.Type.String},
                {"usage", (int)PropertyUsageFlags.Default}
            });
        }
            
        return properties;
    }

    private (bool valid, string message) _ValidateFields()
    {
        Regex regex = new Regex("^[0-9]+$");
        if (!regex.IsMatch(GameId))
        {
            return (false, "Game ID should be a numeric value");
        }
        return (true, "");
    }

    private void _on_button_pressed(string text)
    {
        (bool valid, string message) = _ValidateFields();
        if (!valid)
        {
            throw new Exception(message);
        }
        if (text.ToLower() == "add csproj reference")
        {
            _OnCsprojButtonPressed();
            return;
        }
            
        if (text.ToLower() == "generate init.json")
        {
            _OnGenerateInitJsonButtonPressed();
            return;
        }   
    }

    private void _OnCsprojButtonPressed()
    {
        Godot.Collections.Array csprojFiles = (Godot.Collections.Array)_utils.Call("find_files_by_extension", ".csproj");
        if (!csprojFiles.Any())
        {
            throw new Exception(_CSHARPERRORMSG);
        }
        _utils.Call("add_planetary_csproj_ref", $"res://{csprojFiles[0]}");
        NotifyPropertyListChanged();
    }
       

    private void _OnGenerateInitJsonButtonPressed()
    {
        _GenerateInitJson();
    }
        
    private Godot.Collections.Array _GetEntityNodes()
    {
        Godot.Collections.Array entityNodes = new Godot.Collections.Array();
        Node root = GetTree().EditedSceneRoot;

        _RecursiveSceneEntityTraversal(root, entityNodes);
        return entityNodes;
    }

    private void _RecursiveSceneEntityTraversal(Node node, Godot.Collections.Array entityNodes)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is PPEntityNode)
            {
                entityNodes.Add(child);
                break;
            } 
            _RecursiveSceneEntityTraversal(child, entityNodes);
        }
    }
        
    private void _RemoveAllEntityScenes()
    {
        Node root = GetTree().Root;
        _RecursiveSceneEntityRemoval(root);
    }

    private void _RecursiveSceneEntityRemoval(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is PPEntityNode)
            {
                node.QueueFree();
                break;
            }
            _RecursiveSceneEntityRemoval(child);
        }
    }
            
    private void _GenerateInitJson()
    {
        if (SwarmplayRepoDirectory.Trim() == "")
        {
            throw new Exception("The SwarmPlay repo directory path is not set.");
        }

        DirAccess dirAccess = DirAccess.Open(SwarmplayRepoDirectory);
        if (dirAccess == null)
        {
            throw new Exception($"The SwarmPlay repo directory does not exist: {SwarmplayRepoDirectory}");
        }
            
        Godot.Collections.Array entityNodes = _GetEntityNodes();
        
        Godot.Collections.Array<Godot.Collections.Dictionary> entityInitData = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (PPEntityNode entityNode in entityNodes)
        {
            Node sceneInstance = entityNode.GetParent();
            Godot.Collections.Dictionary entityData = new Godot.Collections.Dictionary();
            if (!string.IsNullOrEmpty(entityNode.Data))
            {
                Json json = new Json();
                Error result = json.Parse(entityNode.Data);
                if (result != Error.Ok)
                {
                    throw new Exception($"invalid json found in data field of entity: {sceneInstance.Name}");
                }
                entityData = (Godot.Collections.Dictionary)json.Data;
            }
            
            float originX = 0.0f;
            float originY = 0.0f;
            float originZ = 0.0f;
            if (sceneInstance is Node2D)
            {
                Node2D sceneInstanceNode2D = (Node2D)sceneInstance;
                originX = sceneInstanceNode2D.GlobalTransform.Origin.X;
                originY = sceneInstanceNode2D.GlobalTransform.Origin.Y;
            }
            else if (sceneInstance is Node3D)
            {
                Node3D sceneInstanceNode3D = (Node3D)sceneInstance;
                originX = sceneInstanceNode3D.GlobalTransform.Origin.X;
                originY = sceneInstanceNode3D.GlobalTransform.Origin.Y;
                originZ = sceneInstanceNode3D.GlobalTransform.Origin.Z;
            }

            entityInitData.Add(new Godot.Collections.Dictionary()
                {
                    {"data", entityData},
                    {"chunkloader", entityNode.Chunkloader},
                    {"type", entityNode.Type},
                    {"x", originX},
                    {"z", originY},
                    {"y", originZ}
                }
            );
        }
            
        string data = Json.Stringify(entityInitData);
        _utils.Call("write_string_to_file", $"{SwarmplayRepoDirectory}/init.json", data);
        _utils.Call("refresh_filesystem");
        GD.Print("init.json generated");
    }
        
    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
        {
            _RemoveAllEntityScenes();
            return;
        }

        // check for existence of cs proj / sln files
        Godot.Collections.Array csprojFiles = (Godot.Collections.Array)_utils.Call("find_files_by_extension", ".csproj");
        _csprojReferenceExists = false;
        NotifyPropertyListChanged();
        if (!csprojFiles.Any())
        {
            throw new Exception(_CSHARPERRORMSG);
        }
        Godot.Collections.Array slnFiles = (Godot.Collections.Array)_utils.Call("find_files_by_extension", ".csproj");
        if (!slnFiles.Any())
        {
            throw new Exception(_CSHARPERRORMSG);
        }
        _csprojReferenceExists = (bool)_utils.Call("csproj_planetary_reference_exists", csprojFiles[0]);
        NotifyPropertyListChanged();
        if (!_csprojReferenceExists)
        {
            throw new Exception($"Planetary Processing reference does not exist in {csprojFiles[0]}\nClick \"Add Csproj Reference\" in the PPRootNode inspector to add the reference.");
        }
    }
}
//endif

