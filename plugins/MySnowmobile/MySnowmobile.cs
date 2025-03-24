using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("My Snowmobile", "MasterSplinter", "1.0.3")]
    [Description("Let players purchase Tomaha or skin to via command, also allows for Snowmobile to go fast anywhere!")]
    class MySnowmobile : RustPlugin
    {
        private bool debug = false;
        private string debugversion = "0.0.2";

        string Prefix = "My Snowmobile: ";       // CHAT PLUGIN PREFIX
        string PrefixColor = "#FF8552";          // CHAT PLUGIN PREFIX COLOR
        ulong SteamIDIcon = 76561199242793911;   //  STEAMID created for this plugin 76561199133165664

        private readonly string UseSkinPerm = "mysnowmobile.useskin_command";
        private readonly string UseBuyPerm = "mysnowmobile.usebuy_command";
        private List<VehicleCache> vehicles = new List<VehicleCache>();
        private List<BasePlayer> cooldown = new List<BasePlayer>();

        #region DataFile/Classes

        public class VehicleCache
        {
            public BaseEntity entity;
        }

        private class StoredData
        {
            public Dictionary<ulong, int> CurrentFuel = new Dictionary<ulong, int>();

            public StoredData()
            {
            }
        }

        StoredData storedData;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}\\{this.Name}_Data", storedData);

        #endregion

        #region Config

         private class ConfigFile
         {
             [JsonProperty(PropertyName = "Main Settings")]
             public MainSettings Main = new MainSettings();
             [JsonProperty(PropertyName = "Advance Settings")]
             public AdvanceSettings Advance = new AdvanceSettings();

         }

         public class MainSettings
         {
             [JsonProperty("Allow Snowmobile's to goes fast on all terrain types")]
             public bool AllTerrain = false;
             [JsonProperty("Allow Snowmobile's to have more power")]
             public bool IncreasedPower = false;
             [JsonProperty("The amount of power to give in KW (59KW is default)")]
             public int PowerKW = 59;
             [JsonProperty("The amount of Scrap to charge for a Snowmobile")]
             public int BuyCost = 500;
             [JsonProperty("The amount of fuel to give on purchase")]
             public int FuelOnBuy = 50;
            [JsonProperty("The amount of seconds in between purchase (0 = Instant)")]
            public int CooldownTime = 30;
         }

        public class AdvanceSettings
        {
            [JsonProperty("Tomaha Location")]
            public string Tomaha = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab";
            [JsonProperty("Spray Cloud Location")]
            public string SprayCloudEffect = "assets/prefabs/tools/spraycan/reskineffect.prefab";
            [JsonProperty("Spray Sound Location")]
            public string SpraySoundEffect = "assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab";
        }

        private ConfigFile config;

         protected override void LoadDefaultConfig()
         {
             PrintWarning("Creating new config file");
             config = new ConfigFile();
             SaveConfig();
         }

         #endregion

        #region Setup

        private void OnServerInitialized(bool isStartup) 
        {
            RegisterCommands();
            RegisterPermissions();
            storedData.CurrentFuel.Clear();
            SaveData();
            ProcessExistingSnowmobile();

            if (debug) PrintWarning($"Debug Version: {debugversion}");
        }

        private void RegisterCommands()
        {
            foreach (var command in Commands)
                AddCovalenceCommand(command.Key, command.Value);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(UseBuyPerm, this);
            permission.RegisterPermission(UseSkinPerm, this);
        }

        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"{this.Name}\\{this.Name}_Data");
            config = Config.ReadObject<ConfigFile>();
            NextTick(() => Config.WriteObject(config));
        }

        private void Unload()
        {
            RevertExistingSnowmobile();
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Not_Enough_Scrap", "[#e0e0e0]You don't have enough scrap to purchase a Tomaha. Cost:[/#]"},
                {"Buy_succes_tomaha", "[#e0e0e0]You have successfully bought a[/#] [#ffe479]Tomaha.[/#]"},
                {"Buy_cooldown_tomaha", "[#e0e0e0]You have to wait before buying another Tomaha. Wait Time:[/#]"},
                {"notlooking_item", "[#e0e0e0]You need to look at a Snowmobile to skin![/#]"},
                {"mysnowmobile_help", "\n[#eeeeee]<size=14>Available Commands:</size>[/#]\n[#ffe479]/skinsnowmobile[/#] [#e0e0e0]- Display Help[/#]\n[#ffe479]/buysnowmobile <Available Skins>[/#] [#e0e0e0]- buy a Snowmobile[/#]\n[#ffe479]/skinsnowmobile <Available Skins>[/#] [#e0e0e0]- Skin a Snowmobile[/#]\n \n[#ffd479]<size=14>Available Skins:</size>[/#]\n[#ffe479]\"tomaha\"[/#] [#e0e0e0]- Tomaha Snowmobile[/#]\n \n[#73c2fa]<size=14>Example:</size>[/#]\n[#ffe479]/skinsnowmobile tomaha[/#] [#e0e0e0]- This will skin to a Tomaha[/#]"},
                {"Skin_succes_tomaha", "[#e0e0e0]You have successfully skinned to a[/#] [#ffe479]Tomaha.[/#]"},
                {"already_tomaha", "[#e0e0e0]This Snowmobile is already skinned to a[/#] [#ffe479]Tomaha![/#]"},
                {"snowmobile_damage", "[#e0e0e0]This Snowmobile is damaged! Repair it to skin[/#]"},
                {"NoPermMsg", "[#e0e0e0]You are not allowed to use this command[/#]"},

            }, this);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(BaseMountable entity)
        {
            var snowmobile = entity.GetComponentInParent<Snowmobile>() ?? null;

            if (snowmobile == null) return;

            if (snowmobile != null)
            {
                if (config.Main.AllTerrain == true) AllTerrain(snowmobile);

                if (config.Main.IncreasedPower == true) IncreasedPower(snowmobile);
            }
        }

        #endregion

        #region Methods

        #region Fuel

        public int GetFuelAmount(VehicleCache vehicle)
        {
            if (vehicle.entity is Snowmobile)
            {
                return (vehicle.entity as Snowmobile)?.GetFuelSystem()?.GetFuelAmount() ?? 0;
            }

            return 0;
        }

        private void LogFuelAmount(BasePlayer player, VehicleCache vehicle)
        {
            if (player == null) return;

            storedData.CurrentFuel.Add(player.userID, GetFuelAmount(vehicle));
            SaveData();
        }

        private void RemoveLoggedFuel(BasePlayer player, VehicleCache vehicle)
        {
            if (player == null) return;

            timer.Once(1, () =>
            {
                storedData.CurrentFuel.Remove(player.userID);
                SaveData();
            });
        }

        private void AddFuel(Snowmobile snowmobile, string userId)
        {
            if (snowmobile is Snowmobile)
            {
                ulong UserID = Convert.ToUInt64(userId);
                var fuelAmount = storedData.CurrentFuel[UserID];

                StorageContainer fuelContainer = snowmobile.GetFuelSystem().GetFuelContainer();
                if (fuelAmount < 0)
                {
                    fuelAmount = fuelContainer.allowedItem.stackable;
                }
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);

            }
        }

        private void AddFuelOnBuy(Snowmobile snowmobile)
        {
            if (snowmobile is Snowmobile)
            {
                var fuelAmount = config.Main.FuelOnBuy;

                StorageContainer fuelContainer = snowmobile.GetFuelSystem().GetFuelContainer();
                if (fuelAmount < 0)
                {
                    fuelAmount = fuelContainer.allowedItem.stackable;
                }
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);

            }
        }

        #endregion

        #region Skinning / Buying

        private BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 10f)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var ray = player.eyes.HeadRay();
            if (Physics.Raycast(ray, out hit, maxDist))
            {
                var ent = hit.GetEntity() ?? null;
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }

            return null;
        }

        private void SkinSnowmobile(IPlayer p, VehicleCache vehicle, Vector3 customPosition = default(Vector3), bool useCustomPosition = false)
        {
            BasePlayer player = p.Object as BasePlayer;

            var position = useCustomPosition ? customPosition : GetPosition(player);
            var rotation = useCustomPosition ? Quaternion.identity : GetRotation(player);

            Snowmobile snowmobile = GameManager.server.CreateEntity(config.Advance.Tomaha, position, rotation) as Snowmobile;

            if (snowmobile == null) return;

            snowmobile.Spawn();
            SprayEffect(player);

            AddFuel(snowmobile, player.UserIDString);

            RemoveLoggedFuel(player, vehicle);

            Player.Message(player, $"{lang.GetMessage("Skin_succes_tomaha", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
        }

        private void BuyTomaha(IPlayer p, Vector3 customPosition = default(Vector3), bool useCustomPosition = false)
        {
            BasePlayer player = p.Object as BasePlayer;

            var position = useCustomPosition ? customPosition : GetPosition(player);
            var rotation = useCustomPosition ? Quaternion.identity : GetRotation(player);

            Snowmobile snowmobile = GameManager.server.CreateEntity(config.Advance.Tomaha, position, rotation) as Snowmobile;

            if (snowmobile == null) return;

            snowmobile.Spawn();
            SprayEffect(player);

            AddFuelOnBuy(snowmobile);

            cooldown.Add(player);
            timer.Once(config.Main.CooldownTime, () =>
            {
                cooldown.Remove(player);
            });

            Player.Message(player, $"{lang.GetMessage("Buy_succes_tomaha", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
        }

        void SprayEffect(BasePlayer player, Vector3 customPosition = default(Vector3), bool useCustomPosition = false)
        {
            if (player != null)
            {
                var position = useCustomPosition ? customPosition : GetPosition(player);

                Effect.server.Run(config.Advance.SprayCloudEffect, position, new Vector3());
                Effect.server.Run(config.Advance.SpraySoundEffect, player.transform.position, new Vector3());
            }
        }

        private bool IsDamagedEntity(BaseEntity entity)
        {
            var baseCombatEntity = entity as BaseCombatEntity;
            if (baseCombatEntity == null || !baseCombatEntity.repair.enabled)
            {
                return false;
            }
            if (baseCombatEntity.healthFraction * 100f >= 100)
            {
                return false;
            }
            return true;
        }

        private Vector3 GetPosition(BasePlayer player)
        {
            Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
            forward.y = 0;
            return player.transform.position + forward.normalized * 3f + Vector3.up * 2f;
        }

        private Quaternion GetRotation(BasePlayer player) =>
            Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 135, 0);

        #endregion

        #region Snowmobile Attributes

        private void IncreasedPower(Snowmobile snowmobile)
        {
            snowmobile.engineKW = config.Main.PowerKW;
        }

        private void AllTerrain(Snowmobile snowmobile)
        {
            Snowmobile.allTerrain = true;
        }

        private void RevertToStock(Snowmobile snowmobile)
        {
            Snowmobile.allTerrain = false;
            snowmobile.engineKW = 59;
        }

        private void ProcessExistingSnowmobile()
        {
            var SnowmobileList = BaseNetworkable.serverEntities.OfType<Snowmobile>();
            foreach (var snowmobile in SnowmobileList)
            {
                if (config.Main.AllTerrain == true) AllTerrain(snowmobile);

                if (config.Main.IncreasedPower == true) IncreasedPower(snowmobile);
            }
        }

        private void RevertExistingSnowmobile()
        {
            var SnowmobileList = BaseNetworkable.serverEntities.OfType<Snowmobile>();
            foreach (var snowmobile in SnowmobileList)
            {
                RevertToStock(snowmobile);
            }
        }

        #endregion

        #endregion

        #region Chat Command

        private Dictionary<string, string> Commands = new Dictionary<string, string>
        {
            { "skinsnowmobile", "SkinSnowmobileCmd" },
            { "buysnowmobile", "BuySnowmobileCmd" }
        };

        private void SkinSnowmobileCmd(IPlayer p, string command, string[] args)
        {
            BasePlayer player = p.Object as BasePlayer;
            BaseEntity hitEnt = GetLookAtEntity(player);
            VehicleCache vehicle = new VehicleCache
            {
                entity = hitEnt,
            };

            if (player == null) return;

            if (p.HasPermission(UseSkinPerm) == false)
            {
                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Title}</color>", SteamIDIcon);
                return;
            }

            if (args.Length < 1)
            {
                Player.Message(player, $"{lang.GetMessage("mysnowmobile_help", this, player.UserIDString)}", $"<color={PrefixColor}>{Title}</color>", SteamIDIcon);
                return;
            }

            switch (args[0].ToLower())
            {
                case "tomaha":
                    {
                        if (hitEnt == null)
                        {
                            p.Reply(lang.GetMessage("notlooking", this, p.Id));
                            return;
                        }
                        else if (vehicle.entity.name.Contains("tomaha"))
                        {
                            Player.Message(player, $"{lang.GetMessage("already_tomaha", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }
                        else if (vehicle.entity is Snowmobile && IsDamagedEntity(vehicle.entity))
                        {
                            Player.Message(player, $"{lang.GetMessage("snowmobile_damage", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }
                        else if (vehicle.entity is Snowmobile)
                        {
                            vehicles.Add(vehicle);
                            LogFuelAmount(player, vehicle);
                            vehicle.entity.Kill();
                            SkinSnowmobile(p, vehicle);
                        }
                        return;
                    }
            }
        }

        private void BuySnowmobileCmd(IPlayer p, string command, string[] args)
        {
            BasePlayer player = p.Object as BasePlayer;

            if (player == null) return;

            if (p.HasPermission(UseBuyPerm) == false)
            {
                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Title}</color>", SteamIDIcon);
                return;
            }

            if (args.Length < 1)
            {
                Player.Message(player, $"{lang.GetMessage("mysnowmobile_help", this, player.UserIDString)}", $"<color={PrefixColor}>{Title}</color>", SteamIDIcon);
                return;
            }

            switch (args[0].ToLower())
            {
                case "tomaha":
                    {
                        if (player.inventory.GetAmount(-932201673) >= config.Main.BuyCost)
                        {
                            if (cooldown.Contains(player))
                            {
                                Player.Message(player, $"{lang.GetMessage("Buy_cooldown_tomaha", this, player.UserIDString)} <color=#ffe479>{config.Main.CooldownTime}</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            player.inventory.Take(null, -932201673, config.Main.BuyCost);
                            BuyTomaha(p);
                            return;
                        }
                        else
                        {
                            Player.Message(player, $"{lang.GetMessage("Not_Enough_Scrap", this, player.UserIDString)} <color=#ffe479>{config.Main.BuyCost}</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            return;
                        }
                    }
            }
        }

        #endregion
    }
}
