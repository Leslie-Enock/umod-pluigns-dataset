using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Shrinking Radiation Zone", "k1lly0u", "0.1.5")]
    [Description("Create shrinking radiation zones for BR style events")]
    class ShrinkingRadZone : RustPlugin
    {
        #region Fields
        private Hash<string, ShrinkZone> activeZones = new Hash<string, ShrinkZone>();
        
        private const string SPHERE_ENTITY = "assets/prefabs/visualization/sphere.prefab";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("shrinkingradzone.use", this);
            lang.RegisterMessages(messages, this);
        }

        private void Unload()
        {
            ShrinkZone[] shrinkZones = UnityEngine.Object.FindObjectsOfType<ShrinkZone>();
            for (int i = 0; i < shrinkZones?.Length ; i++)
            {
                UnityEngine.Object.Destroy(shrinkZones[i]);
            }

            Configuration = null;
        }
        #endregion

        #region API
        private string CreateShrinkZone(Vector3 position, float radius, float time)
        {           
            string zoneId = CuiHelper.GetGuid();
            ShrinkZone zone = new GameObject().AddComponent<ShrinkZone>();
            activeZones[zoneId] = zone;
            zone.CreateZones(zoneId, position, radius, time);            
            return zoneId;
        }

        private void ToggleZoneShrink(string id)
        {
            ShrinkZone shrinkZone;
            if (activeZones.TryGetValue(id, out shrinkZone))            
                shrinkZone.ToggleShrinking();            
        }

        private void DestroyShrinkZone(string id)
        {
            ShrinkZone shrinkZone;
            if (activeZones.TryGetValue(id, out shrinkZone))
            {
                UnityEngine.Object.Destroy(shrinkZone);
                activeZones.Remove(id);
            }
        }
        #endregion

        #region Classes
        private class ShrinkZone : MonoBehaviour
        {
            private string zoneId;
            private float initialRadius;
            private float modifiedRadius;
            private float targetRadius;

            private float timeToTake;
            private float timeTaken;

            private SphereEntity[] innerSpheres;
            private SphereEntity[] outerSpheres;

            private SphereCollider innerCollider;

            private TriggerRadiation radiation;

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "ShrinkZone";
                enabled = false;

                targetRadius = Configuration.FinalZoneSize;
                timeToTake = Configuration.ShrinkTime;
                timeTaken = 0;
            }

            private void OnDestroy()
            {
                foreach (BaseEntity entity in radiation.entityContents)
                    entity.LeaveTrigger(radiation);

                foreach (SphereEntity entity in innerSpheres)
                    entity.Kill();

                foreach (SphereEntity entity in outerSpheres)
                    entity.Kill();

                Destroy(gameObject);
            }

            private void Update()
            {                
                timeTaken = timeTaken + UnityEngine.Time.deltaTime;
                float single = Mathf.InverseLerp(0f, timeToTake, timeTaken);

                modifiedRadius = initialRadius * (1 - single);

                foreach (SphereEntity innerSphere in innerSpheres)
                {
                    innerSphere.currentRadius = (initialRadius * 2) * (1 - single);

                    innerSphere.SendNetworkUpdateImmediate();
                }

                innerCollider.radius = modifiedRadius;

                if (modifiedRadius <= targetRadius)
                {
                    enabled = false;
                    Interface.CallHook("OnRadiationZoneShrunk", zoneId);                    
                }
            }

            private void OnTriggerEnter(Collider obj)
            {
                BasePlayer player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    player.LeaveTrigger(radiation);
                }
            }

            private void OnTriggerExit(Collider obj)
            {
                BasePlayer player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    player.EnterTrigger(radiation);
                }
            }

            public void CreateZones(string zoneId, Vector3 position, float initialRadius, float timeToTake)
            {
                transform.position = position;

                this.zoneId = zoneId;
                this.initialRadius = initialRadius;
                this.timeToTake = timeToTake;

                innerSpheres = new SphereEntity[Configuration.DomeShade];
                outerSpheres = new SphereEntity[Configuration.DomeShade];

                for (int i = 0; i < Configuration.DomeShade; i++)
                {
                    SphereEntity innerSphere = (SphereEntity)GameManager.server.CreateEntity(SPHERE_ENTITY, position, Quaternion.identity, true);
                    innerSphere.currentRadius = initialRadius * 2;
                    innerSphere.lerpSpeed = 0;
                    innerSphere.enableSaving = false;
                    innerSphere.Spawn();

                    innerSpheres[i] = innerSphere;
                }                

                innerCollider = gameObject.AddComponent<SphereCollider>();
                innerCollider.isTrigger = true;
                innerCollider.radius = initialRadius;

                for (int i = 0; i < Configuration.DomeShade; i++)
                {
                    SphereEntity outerSphere = (SphereEntity)GameManager.server.CreateEntity(SPHERE_ENTITY, position, Quaternion.identity, true);
                    outerSphere.currentRadius = (initialRadius * 2) + Configuration.RadiationBuffer;
                    outerSphere.lerpSpeed = 0;
                    outerSphere.enableSaving = false;
                    outerSphere.Spawn();
                    
                    if (i == 0)
                    {
                        SphereCollider outerCollider = outerSphere.gameObject.AddComponent<SphereCollider>();
                        outerCollider.isTrigger = true;
                        outerCollider.radius = 0.5f;

                        radiation = outerSphere.gameObject.AddComponent<TriggerRadiation>();
                        radiation.RadiationAmountOverride = Configuration.RadiationStrength;
                        radiation.interestLayers = 131072;
                        radiation.enabled = true;
                    }

                    outerSpheres[i] = outerSphere;
                }                

                enabled = true;
            }

            public void ToggleShrinking() => enabled = !enabled;
        }
        #endregion

        #region Commands
        [ChatCommand("shrink")]
        private void cmdShrink(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "shrinkingradzone.use"))
                return;

            if (args.Length == 0)
            {
                SendReply(player, $"<color=#00CC00>{Title}</color>  <color=#939393>v</color><color=#00CC00>{Version}</color> <color=#939393>-</color> <color=#00CC00>{Author}</color>");
                SendReply(player, msg("/shrink on me - Starts a shrinking rad zone on your position", player.UserIDString));
                SendReply(player, msg("/shrink on <x> <z> - Starts a shrinking rad zone on the specified position", player.UserIDString));
                SendReply(player, msg("/shrink stop - Destroys all active zones", player.UserIDString));
                SendReply(player, msg("/shrink buffer <## value> - Set the radiation buffer size", player.UserIDString));
                SendReply(player, msg("/shrink startsize <## value> - Set the initial zone size", player.UserIDString));
                SendReply(player, msg("/shrink endsize <## value> - Set the final zone size", player.UserIDString));
                SendReply(player, msg("/shrink strength <## value> - Set the radiation strength (rads per second)", player.UserIDString));
                SendReply(player, msg("/shrink time <## value>  - Set the time it takes to shrink (in seconds)", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    if (args.Length >= 2)
                    {
                        object position = null;
                        if (args[1].ToLower() == "me")                        
                            position = player.transform.position; 
                        else if (args.Length > 2)
                        {
                            float x;
                            float z;
                            if (float.TryParse(args[1], out x) && float.TryParse(args[2], out z))
                            {
                                Vector3 temp = new Vector3(x, 0, z);
                                float height = TerrainMeta.HeightMap.GetHeight((Vector3)temp);
                                position = new Vector3(x, height, z);
                            }
                        }                       
                        if (position is Vector3)
                        {
                            CreateShrinkZone((Vector3) position, Configuration.InitialZoneSize, Configuration.ShrinkTime);
                            SendReply(player, "Zone Created!");
                            return;
                        }                        
                    }
                    else
                    {
                        SendReply(player, "/shrink on me\n/shrink on <x> <z>");
                        return;
                    }
                    return;

                case "stop":
                    foreach (KeyValuePair<string, ShrinkZone> zone in activeZones)
                    {
                        UnityEngine.Object.DestroyImmediate(zone.Value.gameObject);
                        Interface.CallHook("RadzoneEnd", zone.Key);
                    }
                    activeZones.Clear();
                    SendReply(player, msg("All zones destroyed"));
                    return;

                case "buffer":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            Configuration.RadiationBuffer = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Radiation buffer set to : {0}", player.UserIDString), value));                            
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink buffer <##>", player.UserIDString));
                    return;

                case "startsize":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            Configuration.InitialZoneSize = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Initial size set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink startsize <##>", player.UserIDString));
                    return;

                case "endsize":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            Configuration.FinalZoneSize = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Final size set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink endsize <##>", player.UserIDString));
                    return;

                case "strength":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            Configuration.RadiationStrength = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Radiation strength set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink strength <##>", player.UserIDString));
                    return;

                case "time":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            Configuration.ShrinkTime = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Shrink time set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink time <##>", player.UserIDString));
                    return;

                default:
                    break;
            }
        }
        [ConsoleCommand("shrink")]
        private void ccmdShrink(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args.Length == 0)
            {
                SendReply(arg, $"{Title}  v{Version} - {Author}");
                SendReply(arg, "shrink on <x> <z> - Starts a shrinking rad zone on the specified position");
                SendReply(arg, "shrink stop - Destroys all active zones");
                SendReply(arg, "shrink buffer <## value> - Set the radiation buffer size");
                SendReply(arg, "shrink startsize <## value> - Set the initial zone size");
                SendReply(arg, "shrink endsize <## value> - Set the final zone size");
                SendReply(arg, "shrink strength <## value> - Set the radiation strength (rads per second)");
                SendReply(arg, "shrink time <## value>  - Set the time it takes to shrink (in seconds)");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "on":
                    if (arg.Args.Length >= 2)
                    {
                        object position = null;
                        if (arg.Args.Length > 2)
                        {
                            float x;
                            float z;
                            if (float.TryParse(arg.Args[1], out x) && float.TryParse(arg.Args[2], out z))
                            {
                                Vector3 temp = new Vector3(x, 0, z);
                                float height = TerrainMeta.HeightMap.GetHeight((Vector3)temp);
                                position = new Vector3(x, height, z);
                            }
                        }
                        if (position is Vector3)
                        {
                            CreateShrinkZone((Vector3)position, Configuration.InitialZoneSize, Configuration.ShrinkTime);
                            SendReply(arg, "Zone Created!");
                            return;
                        }
                    }
                    else
                    {
                        SendReply(arg, "/shrink on me\n/shrink on <x> <z>");
                        return;
                    }
                    return;

                case "stop":
                    foreach (var zone in activeZones)
                    {
                        UnityEngine.Object.DestroyImmediate(zone.Value.gameObject);
                        Interface.CallHook("RadzoneEnd", zone.Key);
                    }

                    activeZones.Clear();
                    SendReply(arg, msg("All zones destroyed"));
                    return;

                case "buffer":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            Configuration.RadiationBuffer = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Radiation buffer set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink buffer <##>"));
                    return;

                case "startsize":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            Configuration.InitialZoneSize = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Initial size set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink startsize <##>"));
                    return;

                case "endsize":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            Configuration.FinalZoneSize = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Final size set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink endsize <##>"));
                    return;

                case "strength":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            Configuration.RadiationStrength = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Radiation strength set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink strength <##>"));
                    return;

                case "time":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            Configuration.ShrinkTime = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Shrink time set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink time <##>"));
                    return;

                default:
                    break;
            }
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData
        {
            public float RadiationBuffer { get; set; }
            public float FinalZoneSize { get; set; }
            public float InitialZoneSize { get; set; }
            public float ShrinkTime { get; set; }
            public float RadiationStrength { get; set; }
            public int DomeShade { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                FinalZoneSize = 20,
                InitialZoneSize = 150,
                RadiationBuffer = 50,
                RadiationStrength = 40,
                ShrinkTime = 120,
                DomeShade = 4,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (Configuration.Version < new VersionNumber(0, 1, 4))
                Configuration.DomeShade = 4;

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        private Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"Radiation strength set to : {0}","Radiation strength set to : {0}" },
            {"You must enter a number value","You must enter a number value" },
            {"Shrink time set to : {0}","Shrink time set to : {0}" },
            {"Final size set to : {0}","Final size set to : {0}" },
            {"Initial size set to : {0}","Initial size set to : {0}" },
            {"Radiation buffer set to : {0}","Radiation buffer set to : {0}" },
            {"Zone Created!","Zone Created!" },
            {"/shrink on me - Starts a shrinking rad zone on your position","/shrink on me - Starts a shrinking rad zone on your position" },
            {"/shrink on <x> <z> - Starts a shrinking rad zone on the specified position","/shrink on <x> <z> - Starts a shrinking rad zone on the specified position" },
            {"/shrink buffer <## value> - Set the radiation buffer size","/shrink buffer <## value> - Set the radiation buffer size" },
            {"/shrink startsize <## value> - Set the initial zone size","/shrink startsize <## value> - Set the initial zone size" },
            {"/shrink endsize <## value> - Set the final zone size","/shrink endsize <## value> - Set the final zone size" },
            {"/shrink strength <## value> - Set the radiation strength (rads per second)","/shrink strength <## value> - Set the radiation strength (rads per second)" },
            {"/shrink time <## value>  - Set the time it takes to shrink (in seconds)","/shrink time <## value>  - Set the time it takes to shrink (in seconds)" },
            {"All zones destroyed","All zones destroyed" }
        };
        #endregion
    }
}
