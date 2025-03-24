using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;
using System;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Recycle", "nivex", "3.1.7")]
    [Description("Recycle items into their resources")]
    public class Recycle : RustPlugin
    {
        private const string 
            RecyclePrefab = "assets/bundled/prefabs/static/recycler_static.prefab",
            BackpackPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab",
            AdminPermission = "recycle.admin",
            RecyclerPermission = "recycle.use",
            CooldownBypassPermission = "recycle.bypass";

        private readonly Dictionary<ulong, (DroppedItemContainer container, BasePlayer player)> _droppedContainers = new();
        private readonly Dictionary<ulong, (Recycler recycler, BasePlayer player)> _recyclers = new();
        private readonly Dictionary<string, long> _cooldowns = new();
        private ConfigData config;

        #region Hooks

        private void Loaded()
        {
            string recycleCommand = string.IsNullOrEmpty(config.Settings.RecycleCommand) ? "recycle" : config.Settings.RecycleCommand;
            AddCovalenceCommand(recycleCommand, "RecycleCommand");
            AddCovalenceCommand("purgerecyclers", "PurgeRecyclersCommand");
            AddCovalenceCommand("purgebags", "PurgeBagsCommand");
            permission.RegisterPermission(AdminPermission, this);
            permission.RegisterPermission(RecyclerPermission, this);
            permission.RegisterPermission(CooldownBypassPermission, this);
            if (!config.Settings.ToInventory) Unsubscribe(nameof(CanMoveItem));
        }

        private void Unload()
        {
            DestroyRecyclers();
            DestroyBags();
        }

        private void OnLootEntityEnd(BasePlayer player, Recycler recycler)
        {
            if (player != null) DestroyRecycler(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;

            DestroyRecycler(player);

            if (_droppedContainers.Count == 0) return;

            var tmp = Pool.Get<List<KeyValuePair<ulong, (DroppedItemContainer, BasePlayer)>>>();

            tmp.AddRange(_droppedContainers);

            foreach (var (uid, (container, target)) in tmp)
            {
                if (!IsValid(container) || !IsValid(target) || target.userID == player.userID)
                {
                    if (IsValid(container))
                        container.Kill();

                    _droppedContainers.Remove(uid);
                }
            }

            Pool.FreeUnmanaged(ref tmp);
        }

        private object CanMoveItem(Item item, PlayerInventory inv, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            if (targetSlot < 6) return null;

            foreach (ItemContainer container in inv.loot.containers)
            {
                if (container.uid != targetContainerId || container.entityOwner == null) continue;

                if (container.entityOwner is Recycler recycler && IsRecycleBox(recycler)) return false;
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null || !(container.entityOwner is Recycler recycler)) return null;

            if (!IsRecycleBox(recycler)) return null;

            BasePlayer player = PlayerFromRecycler(recycler.net.ID.Value);

            if (player == null) return null;

            if (targetPos < 6)
            {
                string type = Enum.GetName(typeof(ItemCategory), item.info.category);

                if (!config.Settings.RecyclableTypes.Contains(type) || config.Settings.Blacklist.Contains(item.info.shortname))
                {
                    Message(player, "Recycle", "Invalid");
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
                else
                {
                    recycler.Invoke(() =>
                    {
                        if (recycler.IsOn()) return;

                        if (!recycler.HasRecyclable()) return;

                        float time = config.Settings.InstantRecycling ? 0.0625f : recycler.GetRecycleThinkDuration();

                        recycler.InvokeRepeating(recycler.RecycleThink, time, time);
                        recycler.SetFlag(BaseEntity.Flags.On, b: true);
                        recycler.SendNetworkUpdateImmediate();
                    }, 0.0625f);
                }
            }
            else if (config.Settings.ToInventory)
            {
                player.Invoke(() => player.inventory.GiveItem(item), 0f);
            }

            return null;
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            if (container.IsValid() && _droppedContainers.TryGetValue(container.net.ID.Value, out var t) && t.player.userID != player.userID) return true;
            return null;
        }

        private void OnEntityKill(DroppedItemContainer container)
        {
            if (container.IsValid()) _droppedContainers.Remove(container.net.ID.Value);
        }

        protected override void LoadDefaultMessages()
        {
            Func<string, string> youCannot = (thing) => "You cannot recycle while " + thing;

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Recycle -> Reloaded", "Configuration file has been reloaded" },
                { "Recycle -> DestroyedAllBags", "All bags have been destroyed" },
                { "Recycle -> DestroyedAll", "All recyclers have been destroyed" },
                { "Recycle -> Dropped", "You left some items in the recycler!" },
                { "Recycle -> Invalid", "You cannot recycle that!" },
                { "Denied -> Npc Only", "You must use the recycler at specific npcs only" },
                { "Denied -> Permission", "You don't have permission to use that command" },
                { "Denied -> Privilege", "You cannot recycle within someone's building privilege" },
                { "Denied -> Swimming", youCannot("swimming") },
                { "Denied -> Falling", youCannot("falling") },
                { "Denied -> Mounted", youCannot("mounted") },
                { "Denied -> Wounded", youCannot("wounded") },
                { "Denied -> Irradiation", youCannot("irradiated") },
                { "Denied -> Ship", youCannot("on a ship") },
                { "Denied -> Elevator", youCannot("on an elevator") },
                { "Denied -> Balloon", youCannot("on a balloon") },
                { "Denied -> Safe Zone", youCannot("in a safe zone") },
                { "Denied -> Hook Denied", "You can't recycle right now" },
                { "Cooldown -> In", "You need to wait {0} before recycling" },
                { "Timings -> second", "second" },
                { "Timings -> seconds", "seconds" },
                { "Timings -> minute", "minute" },
                { "Timings -> minutes", "minutes" }
            }, this);
        }

        private void OnUseNPC(BasePlayer npc, BasePlayer player) // HumanNPC plugin support
        {
            if (npc == null || !config.Settings.NPCIds.Contains(npc.UserIDString)) return;
            OpenRecycler(player);
        }

        #endregion

        #region Commands

        private void RecycleCommand(IPlayer user, string command, string[] args)
        {
            if (CanManageRecyclers(user) && args.Contains("reloadconfig"))
            {
                LoadConfig();
                Message(user, "Recycle", "Reloaded");
            }

            if (config.Settings.NPCOnly)
                return;

            BasePlayer player = user.Object as BasePlayer;
            
            if (player == null || !CanPlayerOpenRecycler(player)) 
                return;

            OpenRecycler(player);

            if (config.Settings.Cooldown > 0 && !CanBypassCooldown(user)) 
                _cooldowns[player.UserIDString] = DateTimeOffset.Now.ToUnixTimeSeconds() + (long)(config.Settings.Cooldown * 60);
        }

        private void PurgeRecyclersCommand(IPlayer user, string command, string[] args)
        {
            if (CanManageRecyclers(user))
            {
                DestroyRecyclers();
                Message(user, "Recycle", "DestroyedAll");
            }
            else Message(user, "Denied", "Permission");
        }

        private void PurgeBagsCommand(IPlayer user, string command, string[] args)
        {
            if (CanManageRecyclers(user))
            {
                DestroyBags();
                Message(user, "Recycle", "DestroyedAllBags");
            }
            else Message(user, "Denied", "Permission");
        }

        private void Message(IPlayer user, string top, string bottom)
        {
            if (user.Object is BasePlayer player)
            {
                Message(player, top, bottom);
                return;
            }

            string message = GetMessage(top, bottom, user.Id);

            if (string.IsNullOrEmpty(message))
            {
                return; // set a message value to empty to disable that message
            }

            if (user.IsServer)
            {
                Puts(message);
            }
            else
            {
                user.Message(message);
            }
        }

        private void Message(BasePlayer player, string top, string bottom, params object[] args)
        {
            string message = GetMessage(top, bottom, player.UserIDString);
            if (string.IsNullOrEmpty(message)) return;
            PrintToChat(player, args.Length > 0 ? string.Format(message, args) : message);
        }

        public bool IsValid(BaseNetworkable e) => e.IsValid() && !e.IsDestroyed;

        #endregion

        #region Structs

        public class ConfigData
        {
            public class SettingsWrapper
            {
                [JsonProperty("Command To Open Recycler")]
                public string RecycleCommand = "recycle";

                [JsonProperty("Cooldown (in minutes)")]
                public float Cooldown = 5.0f;

                [JsonProperty("Maximum Radiation")] 
                public float RadiationMax = 1f;

                [JsonProperty("Refund Ratio")] 
                public float RefundRatio = 0.5f;

                [JsonProperty("NPCs Only")] 
                public bool NPCOnly;

                [JsonProperty("Allowed In Safe Zones")]
                public bool AllowedInSafeZones = true;

                [JsonProperty("Instant Recycling")] 
                public bool InstantRecycling = false;

                [JsonProperty("Send Recycled Items To Inventory")]
                public bool ToInventory = false;

                [JsonProperty("Send Items To Inventory Before Bag")]
                public bool InventoryBeforeBag = false;

                [JsonProperty("NPC Ids", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
                public List<object> NPCIds = new();

                [JsonProperty("Recyclable Types", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
                public List<object> RecyclableTypes = new();

                [JsonProperty("Blacklisted Items", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
                public List<object> Blacklist = new();
            }

            public SettingsWrapper Settings = new();
            public string VERSION = "3.1.4";
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig()
        {
            config = new()
            {
                Settings =
                {
                    RecyclableTypes = new()
                    {
                        "Ammunition", "Attire", "Common", "Component", "Construction", "Electrical",
                        "Fun", "Items", "Medical", "Misc", "Tool", "Traps", "Weapon"
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<ConfigData>();
                config ??= new();
                config.Settings ??= new();
                config.Settings.NPCIds ??= new();
                canSaveConfig = true;
                SaveConfig();
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
            }
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                config.VERSION = Version.ToString();
                Config.WriteObject(config, true);
            }
        }

        #endregion

        #region Helpers

        private Recycler CreateRecycler(BasePlayer player)
        {
            var recycler = GameManager.server.CreateEntity(RecyclePrefab, player.transform.position.WithY(-5f)) as Recycler;

            if (recycler == null) return null;

            recycler.enableSaving = false;
            recycler.Spawn();

            if (!IsValid(recycler)) return null;

            recycler.radtownRecycleEfficiency = config.Settings.RefundRatio;
            recycler.safezoneRecycleEfficiency = config.Settings.RefundRatio;
            recycler.SetFlag(BaseEntity.Flags.Locked, true);
            recycler.UpdateNetworkGroup();
            recycler.gameObject.layer = 0;
            recycler.SendNetworkUpdateImmediate(true);

            OpenContainer(player, recycler);

            _recyclers.Add(recycler.net.ID.Value, (recycler, player));

            return recycler;
        }

        private void OpenContainer(BasePlayer player, StorageContainer container)
        {
            player.Invoke(() =>
            {
                if (container == null || container.IsDestroyed) return;
                player.EndLooting();
                if (!player.inventory.loot.StartLootingEntity(container, false)) return;
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();
                player.ClientRPC(RpcTarget.Player("RPC_OpenLootPanel", player), container.panelName);
                player.SendNetworkUpdate();
            }, 0.2f);
        }

        private void DropRecyclerContents(Recycler recycler, BasePlayer player)
        {
            if (player == null || player.inventory == null || player.inventory.containerMain == null || player.inventory.containerBelt == null) return;
            if (recycler == null || recycler.inventory == null || recycler.inventory.itemList.IsNullOrEmpty()) return;

            List<Item> items = Pool.Get<List<Item>>();

            items.AddRange(recycler.inventory.itemList);

            if (config.Settings.InventoryBeforeBag)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];

                    if (player.inventory.GiveItem(item))
                    {
                        items.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (items.Count == 0)
            {
                Pool.FreeUnmanaged(ref items);
                return;
            }

            Message(player, "Recycle", "Dropped");

            Pool.FreeUnmanaged(ref items);

            var container = GameManager.server.CreateEntity(BackpackPrefab, player.transform.position + Vector3.up) as DroppedItemContainer;

            if (container == null) return;

            container.enableSaving = false;
            container.lootPanelName = "generic_resizable";
            container.playerSteamID = player.userID;
            container.TakeFrom(new[] { recycler.inventory });
            container.Spawn();

            if (IsValid(container))
            {
                _droppedContainers[container.net.ID.Value] = (container, player);
            }
        }

        private void DestroyRecycler(BasePlayer player)
        {
            Recycler recycler = RecyclerFromPlayer(player.userID);

            if (IsValid(recycler) && _recyclers.TryGetValue(recycler.net.ID.Value, out var t))
            {
                DropRecyclerContents(recycler, t.player);
                _recyclers.Remove(recycler.net.ID.Value);
                recycler.Kill();
            }
        }

        private void DestroyRecyclers()
        {
            if (_recyclers.Count == 0) return;
            var tmp = Pool.Get<List<KeyValuePair<ulong, (Recycler, BasePlayer)>>>();
            tmp.AddRange(_recyclers);
            foreach (var (uid, (recycler, player)) in tmp)
            {
                if (IsValid(recycler))
                {
                    DropRecyclerContents(recycler, player);
                    recycler.Kill();
                }
                _recyclers.Remove(uid);
            }
            Pool.FreeUnmanaged(ref tmp);
        }

        private void DestroyBags()
        {
            if (_droppedContainers.Count == 0) return;
            var tmp = Pool.Get<List<KeyValuePair<ulong, (DroppedItemContainer, BasePlayer)>>>();
            tmp.AddRange(_droppedContainers);
            foreach (var (uid, (container, _)) in tmp)
            {
                if (IsValid(container))
                {
                    container.Kill();
                }
                _droppedContainers.Remove(uid);
            }
            Pool.FreeUnmanaged(ref tmp);
        }

        private string GetMessage(string top, string bottom, string userid)
        {
            return lang.GetMessage(top + " -> " + bottom, this, userid);
        }

        private int[] GetCooldown(string userid)
        {
            if (!_cooldowns.TryGetValue(userid, out var time)) return Array.Empty<int>();

            time += (long)config.Settings.Cooldown * 60;

            long now = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (now > time) return Array.Empty<int>();

            TimeSpan diff = TimeSpan.FromSeconds(time - DateTimeOffset.Now.ToUnixTimeSeconds());

            return new int[] { diff.Minutes, diff.Seconds };
        }

        private string CooldownTimesToString(int[] times, BasePlayer player)
        {
            if (times == null || times.Length != 2) return string.Empty;

            int mins = times[0], secs = times[1];

            return (string.Format(
                mins == 0 ? string.Empty : ("{0} " + GetMessage("Timings", mins == 1 ? "minute" : "minutes", player.UserIDString)), mins) +
                string.Format(" {0} " + GetMessage("Timings", secs == 1 ? "second" : "seconds", player.UserIDString), secs)
            ).Trim();
        }

        #endregion

        #region API

        private BasePlayer PlayerFromRecycler(ulong netID) => _recyclers.TryGetValue(netID, out var t) ? t.player : null;

        private Recycler RecyclerFromPlayer(ulong userid)
        {
            foreach (var (recycler, player) in _recyclers.Values)
                if (player?.userID == userid)
                    return recycler;
            return null;
        }

        private bool IsOnCooldown(IPlayer user) => config.Settings.Cooldown > 0 && !CanBypassCooldown(user) && _cooldowns.ContainsKey(user.Id) && DateTimeOffset.Now.ToUnixTimeSeconds() < _cooldowns[user.Id];

        private bool CanUseRecycler(IPlayer user) => user.HasPermission(RecyclerPermission);

        private bool CanManageRecyclers(IPlayer user) => user.HasPermission(AdminPermission);

        private bool CanBypassCooldown(IPlayer user) => user.HasPermission(CooldownBypassPermission);

        private bool IsRecycleBox(BaseNetworkable e) => IsValid(e) && _recyclers.ContainsKey(e.net.ID.Value);

        private bool CanPlayerOpenRecycler(BasePlayer player)
        {
            if (player == null || !(player.IPlayer is IPlayer user) || !player.IsAlive())
                Message(player, "Denied", "Hook Denied");
            else if (!CanUseRecycler(user) && !CanManageRecyclers(user))
                Message(player, "Denied", "Permission");
            else if (IsOnCooldown(user))
                Message(player, "Cooldown", "In", CooldownTimesToString(GetCooldown(player.UserIDString), player));
            else if (player.IsWounded())
                Message(player, "Denied", "Wounded");
            else if (!player.CanBuild())
                Message(player, "Denied", "Privilege");
            else if (config.Settings.RadiationMax > 0 && player.radiationLevel > config.Settings.RadiationMax)
                Message(player, "Denied", "Irradiation");
            else if (player.IsSwimming())
                Message(player, "Denied", "Swimming");
            else if (!player.IsOnGround() || player.IsFlying || player.isInAir)
                Message(player, "Denied", "Falling");
            else if (player.isMounted || player.GetParentEntity() is BaseMountable)
                Message(player, "Denied", "Mounted");
            else if (player.GetComponentInParent<CargoShip>())
                Message(player, "Denied", "Ship");
            else if (player.GetComponentInParent<HotAirBalloon>())
                Message(player, "Denied", "Balloon");
            else if (player.GetComponentInParent<Lift>())
                Message(player, "Denied", "Elevator");
            else if (!config.Settings.AllowedInSafeZones && player.InSafeZone())
                Message(player, "Denied", "Safe Zone");
            else if (Interface.Call("CanOpenRecycler", player) is object obj && obj != null && (obj is not bool val || !val))
                Message(player, "Denied", obj is string str && str.Length > 0 ? str : "Hook Denied");
            else
                return true;

            return false;
        }

        private void OpenRecycler(BasePlayer player)
        {
            if (player == null) 
                return;

            DestroyRecycler(player);
            CreateRecycler(player);
        }

        private void AddNpc(string id)
        {
            if (config.Settings.NPCIds.Contains(id)) 
                return;

            config.Settings.NPCIds.Add(id);
            SaveConfig();
        }

        private void RemoveNpc(string id)
        {
            if (config.Settings.NPCIds.Remove(id)) 
                SaveConfig();
        }

        #endregion
    }
}