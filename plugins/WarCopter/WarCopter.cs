using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using WebSocketSharp;
using Physics = UnityEngine.Physics;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("War Copter", "Ryz0r", "1.5.1")]
    [Description("Allows a user with permission to spawn a Minicopter that has a viewable CCTV, or a turret attached.")]
    public class WarCopter : RustPlugin
    {
        // CREDITS
        // Pho3niX90 for help with Search Light Method
        // END CREDITS
        
        //Changed Switch Location
        ///warcopter startup
        //Prevented search light from being moved
        //Code cleanup
        
        #region Config/Localization

        private const string AutoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string CctvPrefab = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab";
        private const string BoxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        private const string LightPrefab =
            "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab";

        private const string MinicopterPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string SearchLightPrefab = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";
        private const string SwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";

        private static readonly int GlobalLayerMask = LayerMask.GetMask("Construction", "Default", "Deployed",
            "Resource", "Terrain", "Water", "World");

        private const string DronePerm = "warcopter.drone";
        private const string FighterPerm = "warcopter.fighter";
        private const string SpawnPerm = "warcopter.spawn";
        private const string CooldownPerm = "warcopter.cooldown";

        private List<ulong> _entityList = new List<ulong>();
        private Dictionary<string, float> _cooldownList = new Dictionary<string, float>();
        private Dictionary<ulong, string> _camList = new Dictionary<ulong, string>();

        protected override void SaveConfig() => Config.WriteObject(_config);
        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Add Back Light")]
            public bool AddBackLight = true;

            [JsonProperty(PropertyName = "Add Search Light")]
            public bool AddSearchLight = true;
            
            [JsonProperty(PropertyName = "Add Storage Box")]
            public bool AddStorageBox = true;

            [JsonProperty(PropertyName = "Cool Down Time")]
            public float CoolDownTime = 30f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration();
        }

        private void Init()
        {
            permission.RegisterPermission(SpawnPerm, this);
            permission.RegisterPermission(FighterPerm, this);
            permission.RegisterPermission(DronePerm, this);
            permission.RegisterPermission(CooldownPerm, this);

            _cooldownList.Clear();
            _entityList.Clear();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BuildingBlockMsg"] = "You are building blocked and may not spawn a Warcopter here!",
                ["NoPerms"] = "You do not have permission to use this command.",
                ["Wrong1"] = "You are using the command wrong! Try: /warcopter {fighter/drone}!",
                ["Wrong2"] = "You are using the command wrong! Try: /warcopter {drone} {camera name}!",
                ["InvalidOption"] = "This is not a valid Warcopter option. Try: /warcopter {fighter/drone}",
                ["CooldownMsg"] = "Sorry, you are on a cooldown for {0} seconds and may not spawn another!",
                ["CooldownOver"] = "Your Warcopter cool down is over. You may spawn another one now.",
                ["Exists"] = "This Warcopter camera identifier exists already. Try again!",
                ["NotYours"] = "This Warcopter does not belong to you. You can't destroy this.",
                ["NoTarget"] = "A valid target could not be located. Try again.",
            }, this);
        }

        #endregion
        #region Commands

        [ChatCommand("warcopter")]
        private void WarcopterCommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsBuildingBlocked())
            {
                player.ChatMessage(lang.GetMessage("BuildingBlockMsg", this, player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, CooldownPerm))
            {
                if (_cooldownList.ContainsKey(player.UserIDString))
                {
                    var diff = (_cooldownList[player.UserIDString] + _config.CoolDownTime) - UnityEngine.Time.time;
                    var fDiff = Mathf.Round(diff);

                    player.ChatMessage(string.Format(lang.GetMessage(("CooldownMsg"), this, player.UserIDString), fDiff));
                    return;
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, SpawnPerm))
            {
                player.ChatMessage(lang.GetMessage("NoPerms", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                player.ChatMessage(lang.GetMessage("Wrong1", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "drone":
                    if (args.Length < 2)
                    {
                        player.ChatMessage(lang.GetMessage("Wrong2", this, player.UserIDString));
                        return;
                    }

                    if (!permission.UserHasPermission(player.UserIDString, DronePerm))
                    {
                        player.ChatMessage(lang.GetMessage("NoPerms", this, player.UserIDString));
                        return;
                    }

                    SpawnDrone(player, args[1]);
                    StartCooldown(player);
                    break;

                case "fighter":

                    if (!permission.UserHasPermission(player.UserIDString, FighterPerm))
                    {
                        player.ChatMessage(lang.GetMessage("NoPerms", this, player.UserIDString));
                        return;
                    }
                    
                    SpawnFighter(player);
                    StartCooldown(player);
                    break;
                
                case "startup":

                    var mountedMini = player.GetMountedVehicle();
                    var mountedTurret = mountedMini.GetComponentInChildren<AutoTurret>();
                    var mountedSwitch = mountedTurret.GetComponentInChildren<ElectricSwitch>();

                    if (mountedTurret.IsOn())
                    {
                        ChangeTurretState(mountedTurret, 0);
                        mountedSwitch.SetSwitch(false);
                    }
                    else
                    {
                        ChangeTurretState(mountedTurret, 1);
                        mountedSwitch.SetSwitch(true);
                    }

                    break;
                
                case "destroy":
                    
                    IdentifyAndDestroy(player);
                    break;

                default:
                    player.ChatMessage(lang.GetMessage("InvalidOption", this, player.UserIDString));
                    break;
            }
        }

        #endregion
        #region Hooks/Methods

        private void StartCooldown(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, CooldownPerm)) return;

            _cooldownList.Add(player.UserIDString, Time.time);

            timer.Once(_config.CoolDownTime, () =>
            {
                _cooldownList.Remove(player.UserIDString);
                Player.Message(player, lang.GetMessage("CooldownOver", this, player.UserIDString));
            });
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!_entityList.Contains(entity.net.ID.Value)) return;
            
            _entityList.Remove(entity.net.ID.Value);
            _camList.Remove(entity.net.ID.Value);

            var ent = BaseNetworkable.serverEntities.Find(entity.net.ID) as StorageContainer;
            if (ent == null) return;
            ent.DropItems();
        }

        private void SpawnDrone(BasePlayer player, string dName)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, GlobalLayerMask)) return;
            
            if (_camList.ContainsValue(dName))
            {
                player.ChatMessage(lang.GetMessage("Exists", this, player.UserIDString));
                return;
            }

            var position = hit.point + Vector3.up * 2f;
            var miniEntity = (BaseVehicle) GameManager.server.CreateEntity(MinicopterPrefab, position);
            if (miniEntity == null) return;
            miniEntity.OwnerID = player.userID;
            miniEntity.Spawn();

            var camEntity = GameManager.server.CreateEntity(CctvPrefab, miniEntity.transform.position) as PoweredRemoteControlEntity;
            if (camEntity == null) return;
            camEntity.rcIdentifier = dName;
            camEntity.SetParent(miniEntity);
            camEntity.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            RemoveColliderProtection(camEntity);
            camEntity.Spawn();
            _entityList.Add(camEntity.net.ID.Value);
            _camList.Add(camEntity.net.ID.Value, dName);

            if (_config.AddSearchLight) AddSearchLight(miniEntity);
            if (_config.AddBackLight) AddBackLight(miniEntity);
            if (_config.AddStorageBox) AddBackBox(miniEntity);
        }

        private void SpawnFighter(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, GlobalLayerMask)) return;
            
            var position = hit.point + Vector3.up * 2f;
            var miniEntity = (BaseVehicle) GameManager.server.CreateEntity(MinicopterPrefab, position);
            if (miniEntity == null) return;
            miniEntity.OwnerID = player.userID;
            miniEntity.Spawn();

            AddTurretAndSwitch(miniEntity, player);
            if (_config.AddBackLight) AddBackLight(miniEntity);
            if (_config.AddStorageBox) AddBackBox(miniEntity);
        }

        private object OnEntityGroundMissing(BaseNetworkable entity)
        {
            if (_entityList.Contains(entity.net.ID.Value)) return false;
            return null;
        }

        private void AddSearchLight(BaseVehicle mini)
        {
            SphereEntity mySphere = (SphereEntity) GameManager.server.CreateEntity(SpherePrefab, mini.transform.position, new Quaternion(0, 0, 0, 0), true);
            RemoveColliderProtection(mySphere);
            mySphere.Spawn();
            mySphere.SetParent(mini);
            mySphere.transform.localPosition = new Vector3(0, -100, 0);

            SearchLight searchLight = GameManager.server.CreateEntity(SearchLightPrefab, mySphere.transform.position) as SearchLight;
            if (searchLight == null) return;

            RemoveColliderProtection(searchLight);
            searchLight.Spawn();
            searchLight.GetComponent<StorageContainer>().isLootable = false;
            searchLight.SetFlag(BaseEntity.Flags.On, true);
            searchLight.SetParent(mySphere);
            
            searchLight.transform.localPosition = new Vector3(0, 0, 0);
            searchLight.transform.localRotation = Quaternion.Euler(new Vector3(20, 0, 180));
            searchLight.transform.localRotation = Quaternion.Euler(new Vector3(20, 0, 0));
            searchLight.transform.localRotation = Quaternion.Euler(new Vector3(20, 0, 180));
            
            searchLight._maxHealth = 99999999f;
            searchLight._health = 99999999f;
            searchLight.pickup.enabled = false;
            searchLight.SendNetworkUpdateImmediate();
            mySphere.transform.localScale += new Vector3(0.9f, 0, 0);
            mySphere.LerpRadiusTo(0.1f, 10f);
            timer.Once(3f, () => { mySphere.transform.localPosition = new Vector3(0, 0.24f, 2.35f); });

            _entityList.Add(searchLight.net.ID.Value);

            mySphere.SendNetworkUpdateImmediate();
        }

        private void AddBackLight(BaseVehicle mini)
        {
            var backLights = GameManager.server.CreateEntity(LightPrefab, mini.transform.position) as FlasherLight;
            if (backLights == null) return;

            backLights.Spawn();
            backLights.SetFlag(BaseEntity.Flags.On, true);
            backLights.SetParent(mini);
            backLights.transform.localPosition = new Vector3(0, 1.2f, -2.0f);
            backLights.transform.localRotation = Quaternion.Euler(new Vector3(33, 180, 0));
            backLights.UpdateHasPower(10, 1);

            backLights.SendNetworkUpdateImmediate();

            _entityList.Add(backLights.net.ID.Value);
        }

        private void AddBackBox(BaseVehicle mini)
        {
            var backBox = GameManager.server.CreateEntity(BoxPrefab, mini.transform.position) as StorageContainer;
            if (backBox == null) return;
            
            backBox.Spawn();
            backBox.transform.localPosition = new Vector3(0f, 0.8f, -1.1f);
            backBox.SetParent(mini);
            backBox.DropItems();
            backBox.SendNetworkUpdateImmediate();
            
            _entityList.Add(backBox.net.ID.Value);

        }

        private void AddTurretAndSwitch(BaseVehicle mini, BasePlayer player)
        {
            var miniTurret = GameManager.server.CreateEntity(AutoTurretPrefab, mini.transform.position) as AutoTurret;
            if (miniTurret == null) return;
                
            miniTurret.SetFlag(BaseEntity.Flags.Reserved8, true);
            miniTurret.SetParent(mini);
            miniTurret.allowedContents = ItemContainer.ContentsType.Generic;
            miniTurret.pickup.enabled = false;
            miniTurret.transform.localPosition = new Vector3(0f, 0.2f, 2.5f);
            RemoveColliderProtection(miniTurret);
            
            var playerId = new PlayerNameID {userid = player.userID, username = player.displayName};
            miniTurret.authorizedPlayers.Add(playerId);
                
            miniTurret.Spawn();
            _entityList.Add(miniTurret.net.ID.Value);
            
            var backTurretSwitch = GameManager.server.CreateEntity(SwitchPrefab, miniTurret.transform.localPosition)?.GetComponent<ElectricSwitch>();
            if (backTurretSwitch == null) return;
            
            backTurretSwitch.transform.localPosition = new Vector3(0f, -0.60f, -0.3f);
            backTurretSwitch.transform.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));
            backTurretSwitch.pickup.enabled = false;
            backTurretSwitch._limitedNetworking = false;
            backTurretSwitch.SetParent(miniTurret);
            RemoveColliderProtection(backTurretSwitch);
                
            backTurretSwitch.Spawn();
            _entityList.Add(backTurretSwitch.net.ID.Value);
        }

        private object OnSwitchToggle(ElectricSwitch s, BasePlayer player)
        {
            var parent = s.GetParentEntity();
            if (parent == null || !parent.PrefabName.Equals(AutoTurretPrefab)) return null;
            var turret = parent as AutoTurret;
            if (turret == null) return null;

            ChangeTurretState(turret, !s.IsOn() ? 1 : 0);
            return null;
        }

        private static void ChangeTurretState(AutoTurret turret, int turretState)
        {
            switch (turretState)
            {
                case 0:
                    turret.SetFlag(BaseEntity.Flags.Reserved8, false);
                    turret.InitiateShutdown();
                    break;
                
                case 1:
                    turret.SetFlag(BaseEntity.Flags.Reserved8, true);
                    turret.InitiateStartup();
                    break;
            }
        }
        
        private static void RemoveColliderProtection(BaseEntity colliderEntity) {
            
            foreach (var meshCollider in colliderEntity.GetComponentsInChildren<MeshCollider>()) {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }
            
            UnityEngine.Object.DestroyImmediate(colliderEntity.GetComponent<GroundWatch>());
        }

        private void IdentifyAndDestroy(BasePlayer player)
        {
            RaycastHit raycastHit;
            if(Physics.Raycast(player.eyes.HeadRay(), out raycastHit))
            {
                var target = raycastHit.GetEntity();
				
                if (!target)
                {
                    player.ChatMessage(lang.GetMessage("NoTarget", this, player.UserIDString));
                    return;
                }

                if (target.OwnerID != player.userID)
                {
                    player.ChatMessage(lang.GetMessage("NotYours", this, player.UserIDString));
                    return;
                }

                if (!(target.GetComponent<BaseVehicle>() is Minicopter)) return;
                
                target.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            else
            {
                player.ChatMessage(lang.GetMessage("NoTarget", this, player.UserIDString));
            }
        }

        #endregion
    }
}