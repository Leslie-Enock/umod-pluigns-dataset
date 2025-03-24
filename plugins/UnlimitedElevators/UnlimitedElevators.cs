using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Unlimited Elevators", "k1lly0u", "0.1.3")]
    [Description("Allow players to extend elevators past the 6 high limit")]
    class UnlimitedElevators : RustPlugin
    {
        #region Fields
        private int _elevatorItemId;
        private RaycastHit raycastHit;

        private RaycastHit[] _rayBuffer = new RaycastHit[256];

        private const string USE_PERM = "unlimitedelevators.use";
        private const string SOCKET_FEMALE = "elevator/sockets/elevator-female";
        private const string SOCKET_MALE = "elevator/sockets/elevator-male";

        private const int RAYCAST_LAYERS = 1 << 8 | 1 << 10 | 1 << 13 | 1 << 15 | 1 << 16 | 1 << 17 | 1 << 21 | 1 << 23;
        #endregion

        #region Oxide Hooks        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(USE_PERM, this);

            _elevatorItemId = ItemManager.FindItemDefinition("elevator").itemid;
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);
        #endregion

        #region Functions
        private Elevator FindEntity(BasePlayer player)
        {
            if (Physics.Raycast(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out raycastHit, 3f))            
                return raycastHit.GetEntity() as Elevator;
            
            return null;
        }

        private int CountFloorDifference(Elevator topFloor, int targetFloors)
        {
            int currentFloors = CountFloorsToGround(topFloor);

            return targetFloors - currentFloors;
        }

        private bool HasEnoughItems(BasePlayer player, int extensions) => player.inventory.GetAmount(_elevatorItemId) >= extensions;

        private void TakeItems(BasePlayer player, int extensions) => player.inventory.Take(null, _elevatorItemId, extensions);

        private void RefundItems(BasePlayer player, int subtractions)
        {
            for (int i = 0; i < subtractions; i++)
            {
                player.GiveItem(ItemManager.CreateByItemID(_elevatorItemId));
            }
        }

        private Elevator FindTopFloor(Elevator root)
        {
            Elevator previousFloor = root;
            for (int i = 0; i < int.MaxValue; i++)
            {
                List<EntityLink> list = previousFloor.FindLink(SOCKET_FEMALE)?.connections;
                if (list.Count > 0 && (list[0].owner as Elevator) != null)
                    previousFloor = list[0].owner as Elevator;
                else return previousFloor;
            }

            return previousFloor;
        }

        private int CountFloorsToGround(Elevator topFloor)
        {
            Elevator previousFloor = topFloor;
            for (int i = 0; i < int.MaxValue; i++)
            {
                List<EntityLink> list = previousFloor.FindLink(SOCKET_MALE)?.connections;
                if (list.Count > 0 && (list[0].owner as Elevator) != null)
                    previousFloor = list[0].owner as Elevator;
                else return i + 1;
            }
            return 1;
        }

        private Elevator GetNextFloor(Elevator currentFloor, Elevator.Direction direction)
        {
            List<EntityLink> list = currentFloor.FindLink(direction == Elevator.Direction.Down ? SOCKET_MALE : SOCKET_FEMALE)?.connections;
            if (list.Count > 0 && (list[0].owner as Elevator) != null)
                return list[0].owner as Elevator;

            return null;
        }

        private void AddFloors(Elevator topFloor, int extensions)
        {
            Elevator lastFloor = topFloor;
            lastFloor.SetFlag(BaseEntity.Flags.Reserved1, false, false, true);

            for (int i = 0; i < extensions; i++)
            {
                Elevator floor = GameManager.server.CreateEntity(lastFloor.PrefabName, lastFloor.transform.position + lastFloor.transform.up * 3f, lastFloor.transform.rotation) as Elevator;
                floor.OwnerID = topFloor.OwnerID;
                floor.GetEntityLinks(true);
                floor.Spawn();
                
                floor.OnDeployed(null, null, null);

                lastFloor = floor;
            }

            lastFloor.SetFlag(BaseEntity.Flags.Reserved1, true, false, true);
        }

        private void SubtractFloors(Elevator topFloor, int subtractions)
        {
            Elevator nextFloor = topFloor;

            for (int i = 0; i < subtractions; i++)
            {
                if (nextFloor == null)
                    return;

                Elevator currentFloor = nextFloor;

                nextFloor = GetNextFloor(nextFloor, Elevator.Direction.Down);
                nextFloor?.SetFlag(BaseEntity.Flags.Reserved1, true, false, true);

                currentFloor.Kill(BaseNetworkable.DestroyMode.None);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("setfloors")]
        private void cmdSetFloors(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, USE_PERM))
            {
                player.ChatMessage(msg("Error.NoPermissions", player));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(msg("Message.Title", player));
                player.ChatMessage(msg("Chat.Help1", player));
                return;
            }

            int floors;
            if (!int.TryParse(args[0], out floors))
            {
                player.ChatMessage(msg("Error.NoNumberEntered", player));
                return;
            }

            if (floors > configData.Limit)
            {
                player.ChatMessage(string.Format(msg("Error.PastMaximum", player), configData.Limit));
                return;
            }

            Elevator elevator = FindEntity(player);
            if (elevator == null)
            {
                player.ChatMessage(msg("Error.NoElevator", player));
                return;
            }

            Elevator topFloor = FindTopFloor(elevator);

            int difference = CountFloorDifference(topFloor, floors);

            if (difference == 0)
            {
                player.ChatMessage(msg("Error.NoDifference", player));
                return;
            }

            if (difference > 0)
            {
                if (Physics.BoxCastNonAlloc(topFloor.transform.position + (Vector3.up * 4.35f), Vector3.one * 1.25f, Vector3.up, _rayBuffer, topFloor.transform.rotation, (difference - 1) * 3f, RAYCAST_LAYERS, QueryTriggerInteraction.Ignore) > 0)
                {

                    player.ChatMessage(msg("Error.NotEnoughSpace", player));
                    return;
                }

                if (configData.RequireItem && !HasEnoughItems(player, difference))
                {
                    player.ChatMessage(msg("Error.NotEnoughItems", player));
                    return;
                }
                else TakeItems(player, difference);

                AddFloors(topFloor, difference);
                
                player.ChatMessage(string.Format(msg("Success.Extended", player), difference));
            }
            else
            {
                difference = Mathf.Abs(difference);
                SubtractFloors(topFloor, difference);

                if (configData.RefundIfSubtracting)
                    RefundItems(player, difference);

                player.ChatMessage(string.Format(msg("Success.Subtracted", player), difference));
            }
        }

        #endregion

        #region Config        
        private ConfigData configData;
        private class ConfigData
        {            
            [JsonProperty(PropertyName = "Maximum number of floors allowed")]
            public int Limit { get; set; }

            [JsonProperty(PropertyName = "Require the user has the correct number of elevator items in their inventory to pay for the extension")]
            public bool RequireItem { get; set; }

            [JsonProperty(PropertyName = "Refund items if setting floors to a number lower than what currently exists")]
            public bool RefundIfSubtracting { get; set; }

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
                RequireItem = true,
                Limit = 20,
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

        #region Localization
        public string msg(string key, BasePlayer player) => lang.GetMessage(key, this, player.UserIDString);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Message.Title"] = "<color=#ce422b>[ Unlimited Elevators ]</color> ",

            ["Chat.Help1"] = "<color=#ce422b>/setfloors <number></color> - Set the number of floors for the elevator you are looking at",

            ["Error.NoPermissions"] = "<color=#ce422b>You do not have permission to use this command</color>",
            ["Error.NoNumberEntered"] = "You must enter a number of floors!",
            ["Error.PastMaximum"] = "You are only allowed to set a max of <color=#ce422b>{0}</color> floors!",
            ["Error.NoElevator"] = "<color=#ce422b>No elevator found!</color>",
            ["Error.NoDifference"] = "<color=#ce422b>This elevator already has that number of floors</color>",
            ["Error.NotEnoughItems"] = "<color=#ce422b>You do not have enough elevators in your inventory to build that high!</color>",
            ["Error.NotEnoughSpace"] = "<color=#ce422b>There is not enough space to build that high</color>",

            ["Success.Extended"] = "You have extended this elevator by <color=#ce422b>{0} floor(s)</color>",
            ["Success.Subtracted"] = "You have removed <color=#ce422b>{0} floor(s)</color> from this elevator",
        };
        #endregion
    }
}