using System;
using System.Collections.Generic;
using UnityEngine;
using Rust;
using Oxide.Core;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("RadPockets", "k1lly0u", "2.0.4")]
    [Description("Turn your server into a irradiated wasteland")]
    class RadPockets : RustPlugin
    {
        #region Fields  
        private StoredData storedData;
        private DynamicConfigFile data;
                
        private List<RadiationZone> radiationZones = new List<RadiationZone>();

        private const int PLAYER_MASK = 131072;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("radpockets_data");
            data.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter() };

            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("radpockets.use", this);
        }

        private void OnServerInitialized()
        {
            LoadData();

            ConVar.Server.radiation = true;

            if (storedData.radData.Count == 0)
                CreateNewZones();
            else
            {
                foreach (var zone in storedData.radData)
                    CreateZone(zone);
                Puts($"Re-initalized {storedData.radData.Count} RadPockets");
            }
        }

        private void Unload()
        {
            DestroyAllZones();
        }
        #endregion

        #region Functions
        private void DestroyAllZones()
        {
            for (int i = 0; i < radiationZones.Count; i++)
                UnityEngine.Object.Destroy(radiationZones[i].gameObject);

            radiationZones.Clear();

            RadiationZone[] components = UnityEngine.Object.FindObjectsOfType<RadiationZone>();
            if (components != null)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    UnityEngine.Object.Destroy(components[i].gameObject);
                }
            }
        }

        private void CreateNewZones()
        {
            int amountToCreate = UnityEngine.Random.Range(configData.Count_Min, configData.Count_Max);

            for (int i = 0; i < amountToCreate; i++)
            {
                CreateZone(new PocketData
                {
                    amount = UnityEngine.Random.Range(configData.Radiation_Min, configData.Radiation_Max),
                    position = GetRandomPos(),
                    radius = UnityEngine.Random.Range(configData.Radius_Min, configData.Radius_Max)
                }, true);
            }

            SaveData();
            Puts($"Successfully created {amountToCreate} radiation pockets");
        }

        private void CreateZone(PocketData zone, bool isNew = false, bool save = false)
        {
            RadiationZone radiationZone = new GameObject().AddComponent<RadiationZone>();
            radiationZone.Activate(zone);
            radiationZones.Add(radiationZone);

            if (isNew)
                storedData.radData.Add(zone);

            if (save)
                SaveData();            
        }

        private Vector3 GetRandomPos()
        {
            int mapSize = Convert.ToInt32((TerrainMeta.Size.x / 2) - 600);

            int X = UnityEngine.Random.Range(-mapSize, mapSize);
            int Y = UnityEngine.Random.Range(-mapSize, mapSize);

            return new Vector3(X, TerrainMeta.HeightMap.GetHeight(new Vector3(X, 0, Y)), Y);            
        }        
        #endregion

        #region Chat Commands
        [ChatCommand("rp")]
        private void cmdRP(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "radpockets.use"))
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, $"<color=#00CC00>{Title}  </color><color=#939393>v </color><color=#00CC00>{Version}</color>");
                    SendReply(player, $"<color=#00CC00>/rp showall</color> - {Msg("showallsyn", player.UserIDString, true)}");
                    SendReply(player, $"<color=#00CC00>/rp shownear <opt:radius></color> - {Msg("shownearsyn", player.UserIDString, true)}");
                    SendReply(player, $"<color=#00CC00>/rp removeall</color> - {Msg("removeallsyn", player.UserIDString, true)}");
                    SendReply(player, $"<color=#00CC00>/rp removenear <opt:radius></color> - {Msg("removenearsyn", player.UserIDString, true)}");
                    SendReply(player, $"<color=#00CC00>/rp tpnear</color> - {Msg("tpnearsyn", player.UserIDString, true)}");
                    SendReply(player, $"<color=#00CC00>/rp create <radius> <radiation></color> - {Msg("createsyn", player.UserIDString, true)}");
                    return;
                }

                switch (args[0].ToLower())
                {
                    case "showall":
                        for (int i = 0; i < radiationZones.Count; i++)                        
                            player.SendConsoleCommand("ddraw.box", 10f, Color.green, radiationZones[i].data.position, 1f);
                        return;

                    case "shownear":
                        {
                            float distance = 0;
                            if (args.Length >= 2 && !float.TryParse(args[1], out distance))
                                distance = 10f;

                            for (int i = 0; i < radiationZones.Count; i++)
                            {
                                RadiationZone radiationZone = radiationZones[i];

                                if (Vector3.Distance(radiationZone.data.position, player.transform.position) <= distance)
                                    player.SendConsoleCommand("ddraw.box", 10f, Color.green, radiationZone.data.position, 1f);
                            }
                        }
                        return;

                    case "removeall":
                        DestroyAllZones();
                        storedData.radData.Clear();
                        SaveData();
                        SendReply(player, Msg("removedall", player.UserIDString));
                        return;

                    case "removenear":
                        {
                            float distance = 0;
                            if (args.Length >= 2 && !float.TryParse(args[1], out distance))
                                distance = 10f;

                            int destCount = 0;

                            for (int i = 0; i < radiationZones.Count; i++)
                            {
                                RadiationZone radiationZone = radiationZones[i];

                                if (Vector3.Distance(radiationZone.data.position, player.transform.position) <= distance)
                                {       
                                    UnityEngine.Object.Destroy(radiationZone.gameObject);
                                    radiationZones.Remove(radiationZone);

                                    storedData.radData.Remove(radiationZone.data);

                                    destCount++;

                                }
                            }
                            SendReply(player, Msg("zonesdestroyed", player.UserIDString, true).Replace("{count}", $"</color><color=#00CC00>{destCount}</color><color=#939393>"));
                        }
                        return;

                    case "tpnear":
                        object closestPosition = null;
                        float closestDistance = 4000;

                        for (int i = 0; i < radiationZones.Count; i++)
                        {
                            RadiationZone radiationZone = radiationZones[i];
                            float distance = Vector3.Distance(radiationZone.data.position, player.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestPosition = radiationZone.data.position;
                            }
                        }

                        if (closestPosition is Vector3)
                            player.MovePosition((Vector3)closestPosition);
                        return;

                    case "create":
                        if (args.Length >= 3)
                        {
                            float distance = 0;
                            float radAmount = 0;
                            if (!float.TryParse(args[1], out distance))
                            {
                                SendReply(player, string.Format(Msg("notanumber", player.UserIDString, true), "distance"));
                                return;
                            }

                            if (!float.TryParse(args[2], out radAmount))
                            {
                                SendReply(player, string.Format(Msg("notanumber", player.UserIDString, true), "radiation amount"));
                                return;
                            }

                            CreateZone(new PocketData
                            {
                                amount = radAmount,
                                position = player.transform.position,
                                radius = distance
                            }, true, true);

                            SendReply(player, Msg("createsuccess", player.UserIDString, true)
                                .Replace("{radius}", $"</color><color=#00CC00>{distance}</color><color=#939393>")
                                .Replace("{radamount}", $"</color><color=#00CC00>{radAmount}</color><color=#939393>")
                                .Replace("{pos}", $"</color><color=#00CC00>{player.transform.position}</color>"));
                            return;
                        }
                        else SendReply(player, $"<color=#00CC00>/rp create <radius> <radiation></color> - {Msg("createsyn", player.UserIDString, true)}");
                        return;

                    default:
                        break;
                }
            }
        }
        #endregion

        #region Radiation Control
        private class PocketData
        {
            public Vector3 position;
            public float radius;
            public float amount;
        }

        private class RadiationZone : MonoBehaviour
        {
            public PocketData data;  

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = $"radpocket_{UnityEngine.Random.Range(1, 9999)}";

                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }

            public void Activate(PocketData data)
            {
                this.data = data;
                
                transform.position = data.position;

                SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = data.radius;

                TriggerRadiation triggerRadiation = gameObject.GetComponent<TriggerRadiation>() ?? gameObject.AddComponent<TriggerRadiation>();
                triggerRadiation.RadiationAmountOverride = data.amount;
                triggerRadiation.interestLayers = PLAYER_MASK;
                triggerRadiation.enabled = true;

                gameObject.SetActive(true);
                enabled = true;
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty("Minimum zone radius")]
            public int Radius_Min { get; set; }

            [JsonProperty("Maximum zone radius")]
            public int Radius_Max { get; set; }

            [JsonProperty("Minimum amount of zones to create")]
            public int Count_Min { get; set; }

            [JsonProperty("Maximum amount of zones to create")]
            public int Count_Max { get; set; }

            [JsonProperty("Minimum amount of radiation")]
            public int Radiation_Min { get; set; }

            [JsonProperty("Maximum amount of radiation")]
            public int Radiation_Max { get; set; }

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
                Count_Max = 30,
                Count_Min = 15,
                Radiation_Max = 25,
                Radiation_Min = 2,
                Radius_Max = 60,
                Radius_Min = 15,
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
        private void SaveData() => data.WriteObject(storedData);

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            public List<PocketData> radData = new List<PocketData>();
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
        #endregion

        #region Localization
        private string Msg(string key, string playerid = null, bool color = false)
        {
            if (color)
                return $"<color=#939393>{lang.GetMessage(key, this, playerid)}</color>";
            else return lang.GetMessage(key, this, playerid);
        }

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"showallsyn", "Shows all RadPockets" },
            {"shownearsyn", "Shows nearby RadPockets within optional radius (default 10)" },
            {"removeallsyn", "Removes all RadPockets" },
            {"removenearsyn", "Removes RadPockets within optional radius (default 10)" },
            {"tpnearsyn", "Teleport to the closest RadPocket" },
            {"createsyn", "Create a new RadPocket on your location, requires a radius and radiation amount" },
            {"zonesdestroyed", "Destroyed {count} pockets" },
            {"notanumber", "You must enter a number value for {0}" },
            {"createsuccess", "You have successfully created a new RadPocket with a radius of {radius}, radiation amount of {radamount}, and position of {pos}" },
            {"removedall", "Successfully removed all Radiation Pockets" }
        };
        #endregion
    }
}
