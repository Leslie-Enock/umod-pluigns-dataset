using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
	[Info("Horn Doors", "imthenewguy", "1.0.1")]
	[Description("Allows players to use the horn from their vehicle to open garage doors.")]
	class HornDoors : RustPlugin
	{
        #region Config       

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Maximum distance that the vehicle can be from the door before we attempt to open it?")]
            public float distance = 15;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                SaveConfig();
            }
            catch
            {
                PrintToConsole($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ToggleOff"] = "You will no longer open doors when you honk your horn.",
                ["ToggleOn"] = "You can now open doors when you honk your horn.",
                ["NoPerms"] = "You do not have permission to use command."
            }, this);
        }

        #endregion

        #region Hooks

        const string perm_use = "horndoors.use";
        const string perm_off = "horndoors.off";

        void Init()
        {
            permission.RegisterPermission(perm_use, this);
            permission.RegisterPermission(perm_off, this);
        }

        Dictionary<BasePlayer, float> HornLastPressed = new Dictionary<BasePlayer, float>();

        void OnVehicleHornPressed(VehicleModuleSeating seat, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_use) || permission.UserHasPermission(player.UserIDString, perm_off)) return;

            float lastPressed;
            if (HornLastPressed.TryGetValue(player, out lastPressed) && lastPressed > Time.time) return;

            var vehicle = seat.GetParentEntity() as ModularCar;
            if (vehicle == null) return;

            HandleDoors(player, vehicle);
        }

        #endregion

        #region Commands

        [ConsoleCommand("togglehorn")]
        void ToggleConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) Toggle(player);
        }

        [ChatCommand("togglehorn")]
        void ToggleChat(BasePlayer player) => Toggle(player);

        void Toggle(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_use))
            {
                PrintToChat(player, lang.GetMessage("NoPerms", this, player.UserIDString));
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, perm_off))
            {
                permission.RevokeUserPermission(player.UserIDString, perm_off);
                PrintToChat(player, lang.GetMessage("ToggleOn", this, player.UserIDString));
                return;
            }

            permission.GrantUserPermission(player.UserIDString, perm_off, this);
            PrintToChat(player, lang.GetMessage("ToggleOff", this, player.UserIDString));
        }

        #endregion

        #region Helpers

        public void HandleDoors(BasePlayer player, ModularCar car)
        {
            var doors = FindEntitiesOfType<Door>(car.transform.position, config.distance);
            doors.Sort((a, b) => (a.transform.position - car.transform.position).sqrMagnitude.CompareTo((b.transform.position - car.transform.position).sqrMagnitude));

            foreach (var door in doors)
            {
                if (!InRange(door.transform.position, car.transform.position, config.distance)) continue;
                if (door.transform.position.y > car.transform.position.y + 2) continue;
                if (!door.GetPlayerLockPermission(player)) continue;
                if (!IsVehicleDoor(door.ShortPrefabName)) continue;
                if (!CanSeeDoor(door, car)) continue;
                HornLastPressed[player] = Time.time + 1f;
                if (door.IsBusy()) return;
                door.SetOpen(!door.IsOpen());
                break;
            }
            Pool.FreeList(ref doors);
        }

        private bool CanSeeDoor(Door door, ModularCar car)
        {
            Vector3 adjustedPos = car.transform.position + car.transform.up * 1.5f;
            Vector3 adjustedDoorPos = door.transform.position + door.transform.up * 1.5f;
            var Distance = (adjustedDoorPos - adjustedPos).magnitude + 0.5f;
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(adjustedPos, (adjustedDoorPos - adjustedPos).normalized, out raycastHit, Distance, Layers.Mask.Construction | Layers.Mask.Deployed);
            var targetEntity = flag ? raycastHit.GetEntity() : null;
            if (targetEntity != null && (targetEntity is BuildingBlock || targetEntity is IceFence || targetEntity is SimpleBuildingBlock)) return false;
            return true;
        }

        bool IsVehicleDoor(string prefab)
        {
            switch (prefab)
            {
                case "wall.frame.garagedoor":
                case "gates.external.high.wood":
                case "gates.external.high.stone":
                    return true;

                default: return false;
            }
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 a, float n, int m = -1) where T : BaseEntity
        {
            int hits = Physics.OverlapSphereNonAlloc(a, n, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = Pool.GetList<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity() as T;
                if (entity != null && !entities.Contains(entity) && !entity.IsDestroyed) entities.Add(entity);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        private static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        #endregion        
    }
}
