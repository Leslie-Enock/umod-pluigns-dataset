using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Item Void", "Default", "1.0.3")] 
    [Description("Transport items to a chest via the void")]

    class ItemVoid : RustPlugin
    {
        #region Declarations
        private static ItemVoid instance;
        private const string permissionName = "itemvoid.use";
        private Dictionary<string, ItemContainer> voidList = new Dictionary<string, ItemContainer>();
        //private static Dictionary<ulong, ItemVoid> _itemVoid = new Dictionary<ulong, ItemVoid>();
        private bool pluginready = false;

        [PluginReference] private Plugin Economics;

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            instance = this;
            permission.RegisterPermission(permissionName, this);
            cmd.AddChatCommand(_config.linkCommand, this, nameof(LinkChestCMD));
            cmd.AddChatCommand(_config.voidCommand, this, nameof(CreateVoidTeleportCMD));
        }

        void OnServerInitialized()
        {
            if (_config.eco.economicsSupport && Economics == null)
            {
                PrintWarning("You have Economics enabled in the configuration but Economics plugin is missing. Please load Economics and reload this plugin.");
                return;
            }
            if (_config.eco.economicsSupport && Economics.IsLoaded && Economics != null)
            {
                PrintWarning("Economic support loaded.");
                pluginready = true;
                return;
            }
            pluginready = true;
        }

        void Unload()
        {
            instance = null;
            var gameObjects = UnityEngine.Object.FindObjectsOfType<TeleportVoid>();
            if (gameObjects.Length > 0)
            {
                foreach (var objects in gameObjects)
                {
                    objects.entity.Kill(BaseNetworkable.DestroyMode.None);
                    UnityEngine.Object.Destroy(objects);
                }
            }
        }

        void OnEntityKill(StorageContainer container)
        {
            BasePlayer owner = FindPlayer(container.OwnerID);
            if (owner != null && voidList.ContainsKey(owner.UserIDString)) 
            {
                voidList.Remove(owner.UserIDString);
                PrintToChat(owner, lang.GetMessage("Destroyed", this, owner.UserIDString));
                return;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["Linked"] = "This chest has been linked",
                ["Invalid"] = "This is either not a box, or a box not owned by you!",
                ["NotFound"] = "No box was found, please look at a box.",
                ["Created"] = "You have created a void!",
                ["NotReady"] = "The plugin is not currently fully loaded. Please check your console for more information.",
                ["Destroyed"] = "Your linked chest has been destroyed!",
                ["NoVoid"] = "You currently do not have a linked chest. Use {0} to link one."
                //["PaidTransport"] = "You have teleported {0}x {1} for a cost of ${2}",
                //["NoMoney"] = "You lack the required money to transport {0}\nYou Require ${1}"
            }, this);
        }

        #endregion

        #region Chat commands

        //[ChatCommand("void")]
        private void CreateVoidTeleportCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                PrintToChat(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            if (!voidList.ContainsKey(player.UserIDString)) 
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoVoid", this, player.UserIDString), _config.linkCommand));
                return;
            }

            /*if (!pluginready) 
            {
                PrintToChat(player, lang.GetMessage("NotReady", this, player.UserIDString));
                return;
            }*/
            CreateVoid(player);
            PrintToChat(player, lang.GetMessage("Created", this, player.UserIDString));

        }

        //[ChatCommand("linkchest")]
        private void LinkChestCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                PrintToChat(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            /*if (!pluginready)
            {
                PrintToChat(player, lang.GetMessage("NotReady", this, player.UserIDString));
                return;
            }*/
            RaycastHit RayHit;
            var flag1 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 20f);
            BaseEntity hitEntity = flag1 ? RayHit.GetEntity() : null;
            if (hitEntity == null)
            {
                PrintToChat(player, lang.GetMessage("NotFound", this, player.UserIDString));
                return;
            }
            StorageContainer storageContainer = hitEntity.GetComponent<StorageContainer>();
            if (storageContainer == null || hitEntity.OwnerID != player.userID)
            {
                PrintToChat(player, lang.GetMessage("Invalid", this, player.UserIDString));
                return;
            }
            ItemContainer container = storageContainer.inventory;
            if (voidList.ContainsKey(player.UserIDString))
                voidList[player.UserIDString] = container;
            else
                voidList.Add(player.UserIDString, container);
            PrintToChat(player, lang.GetMessage("Linked", this, player.UserIDString));

        }

        #endregion

        #region Void handling

        private BaseEntity CreateVoid(BasePlayer player)
        {
            BaseEntity ent = GameManager.server.CreateEntity("assets/prefabs/deployable/rug/rug.deployed.prefab", player.transform.position);
            if (ent == null) 
            {
                PrintWarning("Error when creating a void. Please contact the plugin author.");
                return null;
            }
            ent.skinID = _config.skin;    // 1277571149   my portal - 1277211259
            ent.Spawn();
            ent.gameObject.AddComponent<TeleportVoid>().ownerPlayer = player;
            return ent;
        }

        public class TeleportVoid : MonoBehaviour
        {
            private ItemContainer linkedContainer;
            public BaseEntity entity;
            private BoxCollider collider;
            public BasePlayer ownerPlayer;

            private float initTime;
            private float destroyTime;
            private string t;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                collider = entity.GetComponent<BoxCollider>();
                collider.isTrigger = true;
                initTime = Time.time;
                destroyTime = initTime + instance._config.voidLifeLength;
                instance.NextTick(() =>
                {
                    linkedContainer = instance.voidList[ownerPlayer.UserIDString];
                });
            }

            private void Update()
            {
                if (Time.time >= destroyTime)
                {
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                    Destroy(this);
                }
                transform.Rotate(0, 0.2f, 0);
                entity.SendNetworkUpdateImmediate();
            }

            private void OnTriggerEnter(Collider col)
            {
                DroppedItem droppedItem = col.GetComponentInParent<DroppedItem>();
                if (droppedItem == null)
                    return;
                if (linkedContainer != null)
                    if (ContainerHasSpace(linkedContainer))
                        TransportThroughVoid(droppedItem);
            }

            private void TransportThroughVoid(DroppedItem droppedItem)
            {
                int itemamount = droppedItem.item.amount;
                string itemname = droppedItem.item.info.shortname;
                if (instance._config.eco.economicsSupport && instance._config.eco.itemTable.ContainsKey(itemname)) 
                {
                    double itemcost = instance._config.eco.itemTable[itemname] * itemamount;
                    if (instance.EnoughMoney(droppedItem.OwnerID.ToString(), itemcost)) 
                    {
                        instance.Withdraw(droppedItem.OwnerID.ToString(), itemcost);
                        droppedItem.item.MoveToContainer(linkedContainer);
                        DoVanishEffect(droppedItem.transform.position);
                        return;
                    }
                }
                droppedItem.item.MoveToContainer(linkedContainer);
                DoVanishEffect(droppedItem.transform.position);
                //removalItems.Add(droppedItem);
            }

            private void DoVanishEffect(Vector3 pos)
            {
                t = instance._config.effectUsed;
                Effect.server.Run(t, pos, gameObject.transform.position.normalized);
            }

            private bool ContainerHasSpace(ItemContainer container)
            {
                if (container.itemList.Count < container.capacity)
                    return true;
                return false;
            }
        }

        #endregion

        #region Config handling

        public ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Time until the void despawns")]
            public float voidLifeLength = 300.0f;
            [JsonProperty("Skin to use for the void (Default skin is: 1277211259")]
            public ulong skin = 1277211259;
            [JsonProperty("Effect to use when an item is transferred to a linked container")]
            public string effectUsed = "assets/bundled/prefabs/fx/water/playerjumpinwater.prefab";
            [JsonProperty("Command to create a void")]
            public string voidCommand = "void";
            [JsonProperty("Command to link a chest")]
            public string linkCommand = "linkchest";
            [JsonProperty("Economics")]
            public ItemVoidEconomics eco = new ItemVoidEconomics();

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigFile();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Economics handling

        public class ItemVoidEconomics
        {
            [JsonProperty("Enable economics support?")]
            public bool economicsSupport = false;
            [JsonProperty("Cost to link a container")]
            public double linkCost = 500;
            [JsonProperty("Cost to create a void")]
            public double voidCost = 1000;
            [JsonProperty("Item ID and costs")]
            public Dictionary<string, double> itemTable = new Dictionary<string, double>
            {
                {"stones", 100 },
                {"wood", 50 }
            };
        }

        #endregion

        #region Helpers

        private void Withdraw(string playerId, double amount) 
        {
            Economics?.Call("Withdraw", playerId, amount);
        }

        private bool EnoughMoney(string playerId, double requiredAmount) 
        {
            double playerBalance = (double)(Economics?.Call("Balance", playerId));
            if (playerBalance >= requiredAmount)
                return true;
            return false;
        }

        public static BasePlayer FindPlayer(ulong userId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == userId)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.userID == userId)
                    return sleepingPlayer;
            }
            return null;
        }

        #endregion
    }

}
