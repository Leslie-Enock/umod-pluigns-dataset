using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("Custom Resource Spawns", "k1lly0u", "0.2.33", ResourceId = 1783)]
    [Description("Creates additional spawn points for resources of your choosing, that re-spawn on a timer")]
    class CustomResourceSpawns : RustPlugin
    {
        #region Fields
        private RaycastHit raycastHit;

        private static readonly RaycastHit[] raycastBuffer = new RaycastHit[256];

        private readonly List<BaseEntity> currentResourceEntities = new List<BaseEntity>();
        private readonly List<Timer> refreshTimers = new List<Timer>();

        private readonly Dictionary<ulong, int> resourceCreators = new Dictionary<ulong, int>();

        private const string ADMIN_PERMISSION = "customresourcespawns.admin";

        private struct Resource
        {
            public static readonly List<Resource> List = new List<Resource>();

            public static readonly HashSet<string> Categories = new HashSet<string>();

            private const string RESOURCES = "assets/bundled/prefabs/autospawn/resource/";
            private const string COLLECTABLES = "assets/bundled/prefabs/autospawn/collectable/";
            private const string LOOT = "loot";

            public int ID { get; private set; }

            public string Category { get; private set; }

            public string Shortname { get; private set; }

            public string ResourcePath { get; private set; }

            public Resource(int id, BaseEntity baseEntity)
            {
                ID = id;
                Category = baseEntity.PrefabName.Replace(RESOURCES, "").Replace(COLLECTABLES, "").Replace(baseEntity.ShortPrefabName, "").Replace(".prefab", "").TrimEnd('/');              
                Shortname = baseEntity.ShortPrefabName;
                ResourcePath = baseEntity.PrefabName;

                Categories.Add(Category);
            }
                       
            public static void Populate()
            {
                Clear();

                int id = 1;
                foreach (string str in FileSystem.Backend.cache.Keys)
                {
                    if ((str.StartsWith(RESOURCES) || str.StartsWith(COLLECTABLES)) && !str.Contains(LOOT))
                    {
                        BaseEntity baseEntity = GameManager.server.FindPrefab(str).GetComponent<BaseEntity>();
                        if (baseEntity != null)
                        {
                            AddResource(id, baseEntity);
                            id++;
                        }
                    }
                }
            }

            public static bool FindByID(int id, out Resource resource)
            {
                for (int i = 0; i < List.Count; i++)
                {
                    resource = List[i];

                    if (resource.ID == id)
                        return true;                    
                }

                resource = default(Resource);
                return false;
            }

            public static bool Exists(int id)
            {
                for (int i = 0; i < List.Count; i++)
                {
                    if (List[i].ID == id)
                        return true;
                }

                return false;
            }

            private static void AddResource(int id, BaseEntity baseEntity) => List.Add(new Resource(id, baseEntity));

            public static void Clear() => List.Clear();
        }
        #endregion Fields

        #region Oxide Hooks        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(ADMIN_PERMISSION, this);
            Resource.Populate();

            LoadData();
            InitializeResourceSpawns();
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);

        private void OnEntityKill(BaseEntity entity)
        {            
            if (entity == null) 
                return;

            if (currentResourceEntities.Contains(entity))
            {
                InitiateRefresh(entity);
                currentResourceEntities.Remove(entity);
            }
        }
        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            BaseEntity ent = entity.GetEntity();
            if (ent != null)
            {
                if (currentResourceEntities.Contains(ent))
                {
                    InitiateRefresh(ent);
                    currentResourceEntities.Remove(ent);
                }
            }
        }
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (resourceCreators.ContainsKey(player.userID))
                if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    int type = resourceCreators[player.userID];
                    AddSpawn(player, type);
                }
        }
        private void Unload()
        {
            for (int i = currentResourceEntities.Count - 1; i >= 0; i--)
                currentResourceEntities[i].Kill();

            currentResourceEntities.Clear();

            for (int i = 0; i < refreshTimers.Count; i++)            
                refreshTimers[i].Destroy();

            refreshTimers.Clear();
        }

        #endregion Oxide Hooks

        #region Resource Control

        private void InitializeResourceSpawns()
        {
            foreach (StoredData.StoredResource resource in storedData.resources)
            {
                InitializeNewSpawn(resource.Type, resource.Position);
            }
        }
        private void InitiateRefresh(BaseEntity resource)
        {
            Vector3 position = resource.transform.position;
            string type = resource.PrefabName;
            refreshTimers.Add(timer.Once(configData.RespawnTimer * 60, () =>
            {
                InitializeNewSpawn(type, position);
            }));
            currentResourceEntities.Remove(resource);
        }
        private void InitializeNewSpawn(string type, Vector3 position)
        {
            BaseEntity entity = InstantiateEntity(type, position);
            entity.enableSaving = false;
            entity.Spawn();
            currentResourceEntities.Add(entity);
        }

        private BaseEntity InstantiateEntity(string type, Vector3 position)
        {
            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(type), position, new Quaternion());
            gameObject.name = type;

            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);

            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        #endregion Resource Control

        #region Resource Spawning

        private void AddSpawn(BasePlayer player, int id)
        {
            if (Resource.FindByID(id, out Resource resource))
            {
                BaseEntity entity = InstantiateEntity(resource.ResourcePath, GetSpawnPosition(player));
                entity.enableSaving = false;
                entity.Spawn();

                storedData.resources.Add(new StoredData.StoredResource { Position = entity.transform.position, Type = resource.ResourcePath });
                currentResourceEntities.Add(entity);

                SaveData();
            }
        }

        #endregion

        #region Helper Methods        
        private Vector3 GetSpawnPosition(BasePlayer player)
        {
            int hits = Physics.RaycastNonAlloc(player.eyes.HeadRay(), raycastBuffer, 20f, -1, QueryTriggerInteraction.Ignore);

            float closestdist = float.MaxValue;
            Vector3 closestHitpoint = player.transform.position;

            for (int i = 0; i < hits; i++)
            {
                RaycastHit raycastHit = raycastBuffer[i];

                if (raycastHit.distance < closestdist)
                {
                    closestdist = raycastHit.distance;
                    closestHitpoint = raycastHit.point;
                }
            }
            
            return closestHitpoint;
        }

        private BaseEntity FindResourceEntity(BasePlayer player) => Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 10f) ? raycastHit.GetEntity() : null;
       
        private BaseEntity FindResourcePosition(Vector3 position)
        {
            foreach (BaseEntity entry in currentResourceEntities)
            {
                if (entry.transform.position == position)
                    return entry;
            }
            return null;
        }

        private void FindInRadius(Vector3 position, float radius, List<Vector3> foundResources)
        {
            foreach (StoredData.StoredResource item in storedData.resources)
            {
                if (Vector3.Distance(position, item.Position) < radius)                
                    foundResources.Add(item.Position);                
            }
        }

        private bool RemoveResource(BaseEntity entity)
        {
            if (currentResourceEntities.Contains(entity))
            {
                RemoveFromData(entity);
                currentResourceEntities.Remove(entity);

                entity.Kill();
                return true;
            }
            return false;
        }

        private bool RemoveFromData(BaseEntity baseEntity)
        {
            foreach (StoredData.StoredResource resource in storedData.resources)
            {
                if (resource.Type.Equals(baseEntity.PrefabName, StringComparison.OrdinalIgnoreCase) && Vector3.Distance(baseEntity.transform.position, resource.Position) < 0.25f)
                {
                    storedData.resources.Remove(resource);
                    return true;
                }
            }
            return false;
        }

        private void ShowResourceCategories(BasePlayer player)
        {
            SendEchoConsole(player.net.connection, "Available resource categories");
            foreach (string category in Resource.Categories)
                SendEchoConsole(player.net.connection, category);
        }

        private void ShowResourcesByCategory(BasePlayer player, string category)
        {
            SendEchoConsole(player.net.connection, string.Format("Showing resources with category : {0}", category));
            foreach (Resource resource in Resource.List)
            {
                if (resource.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    SendEchoConsole(player.net.connection, string.Format("{0} - {1}", resource.ID, resource.Shortname));
                }
            }
        }

        private void ShowCurrentResources(BasePlayer player)
        {
            foreach (StoredData.StoredResource resource in storedData.resources)
                SendEchoConsole(player.net.connection, string.Format("{0} - {1}", resource.Position, resource.Type));
        }

        private void SendEchoConsole(Network.Connection cn, string msg)
        {
            if (Net.sv.IsConnected())
            {
                NetWrite netWrite = Net.sv.StartWrite();
                netWrite.PacketID(Network.Message.Type.ConsoleMessage);
                netWrite.String(msg);
                netWrite.Send(new SendInfo(cn));
            }
        }
        #endregion

        #region Commands
        [ChatCommand("crs")]
        private void chatResourceSpawn(BasePlayer player, string command, string[] args)
        {
            if (!CanSpawnResources(player)) return;
            if (resourceCreators.ContainsKey(player.userID))
            {
                resourceCreators.Remove(player.userID);
                ChatMessage(player, lang.GetMessage("endAdd", this, player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage("synAdd1", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synRem", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synRemNear", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synList", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synResourceCats", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synResource1", this, player.UserIDString));
                SendReply(player, lang.GetMessage("synWipe", this, player.UserIDString));
                return;
            }
            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "add":
                        {
                            if (!int.TryParse(args[1], out int id))
                            {
                                ChatMessage(player, Message("notNum", player.UserIDString));
                                return;
                            }

                            if (Resource.Exists(id))
                            {
                                resourceCreators.Add(player.userID, id);
                                ChatMessage(player, Message("adding", player.UserIDString));
                                return;
                            }

                            ChatMessage(player, Message("notType", player.UserIDString));
                        }
                        return;

                    case "remove":
                        {
                            if (args.Length >= 2 && args[1].ToLower() == "near")
                            {
                                float radius = 10f;

                                if (args.Length == 3) 
                                    float.TryParse(args[2], out radius);

                                List<Vector3> list = Pool.Get<List<Vector3>>();
                                FindInRadius(player.transform.position, radius, list);
                                if (list.Count > 0)
                                {
                                    int totalDestroyed = 0;
                                    foreach (Vector3 position in list)
                                    {
                                        BaseEntity entity = FindResourcePosition(position);
                                        if (entity != null && RemoveResource(entity))
                                            totalDestroyed++;
                                    }

                                    ChatMessage(player, string.Format(Message("removedNear", player.UserIDString), totalDestroyed, radius));
                                    goto FREE_LIST;
                                }
                                else ChatMessage(player, string.Format(Message("noFind", player.UserIDString), radius.ToString()));

                                FREE_LIST:
                                Pool.FreeUnmanaged(ref list);
                                return;
                            }

                            BaseEntity resource = FindResourceEntity(player);
                            if (resource != null)
                            {
                                if (currentResourceEntities.Contains(resource))
                                {
                                    if (RemoveResource(resource))
                                    {
                                        SaveData();
                                        ChatMessage(player, Message("RemovedResource", player.UserIDString));
                                        return;
                                    }
                                }
                                else ChatMessage(player, Message("notReg", player.UserIDString));
                                return;
                            }
                            ChatMessage(player, Message("notBox", player.UserIDString));
                        }
                        return;

                    case "list":
                        ShowCurrentResources(player);
                        ChatMessage(player, Message("checkConsole", player.UserIDString));
                        return;

                    case "categories":
                        ShowResourceCategories(player);
                        ChatMessage(player, Message("checkConsole", player.UserIDString));
                        return;

                    case "resources":
                        if (args.Length >= 2)
                        {
                            ShowResourcesByCategory(player, args[1]);
                            ChatMessage(player, Message("checkConsole", player.UserIDString));
                        }
                        else SendReply(player, lang.GetMessage("synResource1", this, player.UserIDString));
                        return;

                    case "near":
                        {
                            float radius = 10f;
                            if (args.Length == 2) 
                                float.TryParse(args[1], out radius);

                            List<Vector3> list = Pool.Get<List<Vector3>>();
                            FindInRadius(player.transform.position, radius, list);
                            if (list.Count > 0)
                            {
                                ChatMessage(player, string.Format(Message("foundResources", player.UserIDString), list.Count));
                                foreach (Vector3 position in list)
                                    player.SendConsoleCommand("ddraw.box", 30f, Color.magenta, position, 1f);
                            }
                            else ChatMessage(player, string.Format(Message("noFind", player.UserIDString), radius.ToString()));

                            Pool.FreeUnmanaged(ref list);
                        }
                        return;

                    case "wipe":
                        {
                            int count = storedData.resources.Count;

                            for (int i = currentResourceEntities.Count - 1; i >= 0; i--)
                            {
                                currentResourceEntities[i].Kill();
                                currentResourceEntities.RemoveAt(i);
                            }
                            
                            storedData.resources.Clear();

                            SaveData();
                            ChatMessage(player, string.Format(Message("wipedAll1", player.UserIDString), count));
                        }
                        return;

                    default:
                        break;
                }
            }
        }
        private bool CanSpawnResources(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION)) 
                return true;

            ChatMessage(player, Message("noPerms", player.UserIDString));

            return false;
        }

        #endregion Chat Commands

        #region Config
        private ConfigData configData;
        private class ConfigData
        {
            public int RespawnTimer { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                RespawnTimer = 20,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }        
        #endregion

        #region Data Management
        private StoredData storedData;

        private DynamicConfigFile dynamicConfigFile;

        private void SaveData() => dynamicConfigFile.WriteObject(storedData);

        private void LoadData()
        {
            dynamicConfigFile = Interface.Oxide.DataFileSystem.GetFile("CustomSpawns/crs_data");
            dynamicConfigFile.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };

            storedData = dynamicConfigFile.ReadObject<StoredData>();
            if (storedData == null)
                storedData = new StoredData();            
        }

        private class StoredData
        {
            public List<StoredResource> resources = new List<StoredResource>();

            public class StoredResource
            {
                public string Type;
                public Vector3 Position;
            }
        }

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string[] values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                JObject o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion Data Management

        #region Messages
        private void ChatMessage(BasePlayer player, string message) => SendReply(player, $"<color=orange>{Title}:</color> <color=#939393>{message}</color>");

        private string Message(string key, string playerid = null) => lang.GetMessage(key, this, playerid);


        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            ["checkConsole"] = "Check your console for a list of resources",
            ["noPerms"] = "You do not have permission to use this command",
            ["notType"] = "The number you have entered is not on the list",
            ["notNum"] = "You must enter a resource number",
            ["notBox"] = "You are not looking at a resource",
            ["notReg"] = "This is not a custom placed resource",
            ["RemovedResource"] = "Resource deleted",
            ["synAdd1"] = "<color=orange>/crs add <id> </color><color=#939393>- Adds a new resource</color>",
            ["synRem"] = "<color=orange>/crs remove </color><color=#939393>- Remove the resource you are looking at</color>",
            ["synRemNear"] = "<color=orange>/crs remove near <radius> </color><color=#939393>- Removes the resources within <radius> (default 10M)</color>",
            ["synResourceCats"]= "<color=orange>/crs categories </color><color=#939393>- List available resource categories</color>",
            ["synResource1"] = "<color=orange>/crs resources <category> </color><color=#939393>- List available resources in the specified category</color>",
            ["synWipe"] = "<color=orange>/crs wipe </color><color=#939393>- Wipes all custom placed resources</color>",
            ["synList"] = "<color=orange>/crs list </color><color=#939393>- Puts all custom resource details to console</color>",
            ["synNear"] = "<color=orange>/crs near XX </color><color=#939393>- Shows custom resources in radius XX</color>",
            ["wipedAll1"] = "Wiped {0} custom resource spawns",
            ["foundResources"] = "Found {0} resource spawns near you",
            ["noFind"] = "Couldn't find any resources in radius: {0}M",
            ["adding"] = "You have activated the resouce tool. Look where you want to place and press shoot. Type /crs to end",
            ["endAdd"] = "You have de-activated the resouce tool",
            ["removedNear"] = "Removed {0} resources within a {1}M radius of your position"
        };

        #endregion Messaging
    }
}