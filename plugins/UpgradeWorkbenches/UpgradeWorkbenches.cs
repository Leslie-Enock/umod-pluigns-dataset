using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("UpgradeWorkbenches", "MJSU", "2.1.3")]
    [Description("Lets players upgrade workbenches")]
    public class UpgradeWorkbenches : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        
        private readonly List<int> _workbenchItemIds = new List<int>();

        private const string UpgradePermission = "upgradeworkbenches.upgrade";
        private const string DowngradePermission = "upgradeworkbenches.downgrade";
        private const string RefundPermission = "upgradeworkbenches.refund";
        private const string AccentColor = "#de8732";
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UpgradePermission, this);
            permission.RegisterPermission(DowngradePermission, this);
            permission.RegisterPermission(RefundPermission, this);

            if (!_pluginConfig.DisplayPlacementMessage)
            {
                Unsubscribe(nameof(OnEntityBuilt));
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Chat] = $"<color=#bebebe>[<color={AccentColor}>{Title}</color>] {{0}}</color>",
                [LangKeys.InfoMessage] = $"Workbenches can be upgraded!\nTo upgrade type <color={AccentColor}>/{{0}}</color> while looking at a workbench and drag a workbench item into the workbench's inventory!",
                [LangKeys.UpgradeNotAllowed] = "You're not allowed to upgrade the workbench",
                [LangKeys.DowngradeNotAllow] = "You're not allowed to downgrade the workbench",
                [LangKeys.NotAllowed] = "You do not have permission to use this command",
                [LangKeys.NotLookingAt] = "You're not looking at a workbench",
                [LangKeys.ChatCommand] = "wbu",
            }, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }

        private void OnServerInitialized()
        {
            List<string> commands = Pool.GetList<string>();
            foreach (string language in lang.GetLanguages())
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                if (messages != null && messages.ContainsKey(LangKeys.ChatCommand))
                {
                    string command = messages[LangKeys.ChatCommand].ToLower();
                    if (!commands.Contains(command))
                    {
                        cmd.AddChatCommand(command, this, WorkbenchUpgradeChatCommand);
                        commands.Add(command);
                    }
                }
            }
            
            Pool.FreeList(ref commands);
            
            _workbenchItemIds.Add(ItemManager.itemDictionaryByName["workbench1"].itemid);
            _workbenchItemIds.Add(ItemManager.itemDictionaryByName["workbench2"].itemid);
            _workbenchItemIds.Add(ItemManager.itemDictionaryByName["workbench3"].itemid);
        }
        #endregion
        
        #region Chat Command
        private void WorkbenchUpgradeChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player, UpgradePermission) && !HasPermission(player, DowngradePermission))
            {
                Chat(player, LangKeys.NotAllowed);
                return;
            }

            Workbench wb = RaycastAll<Workbench>(player, 5f);
            if (wb == null)
            {
                Chat(player, LangKeys.NotLookingAt);
                return;
            }

            timer.In(.25f, () =>
            {
                wb.PlayerOpenLoot(player, "lantern");
            });
        }
        #endregion

        #region Hooks
        private object CanMoveItem(Item movedItem, PlayerInventory playerInventory, ItemContainerId targetContainerId)
        {
            if (!_workbenchItemIds.Contains(movedItem.info.itemid))
            {
                return null;
            }

            Workbench oldBench = playerInventory.FindContainer(targetContainerId)?.entityOwner as Workbench;
            if (oldBench == null)
            {
                return null;
            }
            
            int newBenchLevel = int.Parse(movedItem.info.shortname.Replace("workbench", ""));
            if (newBenchLevel == oldBench.Workbenchlevel)
            {
                return null;
            }

            BasePlayer player = playerInventory._baseEntity;
            if (newBenchLevel > oldBench.Workbenchlevel && !HasPermission(player, UpgradePermission))
            {
                Chat(player, LangKeys.UpgradeNotAllowed);
                return null;
            }

            if (newBenchLevel < oldBench.Workbenchlevel && !HasPermission(player, DowngradePermission))
            {
                Chat(player, LangKeys.DowngradeNotAllow);
                return null;
            }

            Planner planner = movedItem.GetHeldEntity() as Planner;
            Deployable deployable = planner?.GetDeployable();
            if (deployable == null)
            {
                return null;
            }

            Workbench newBench = GameManager.server.CreateEntity(deployable.fullName, oldBench.transform.position, oldBench.transform.rotation) as Workbench;
            if (newBench == null)
            {
                return null;
            }

            newBench.OwnerID = oldBench.OwnerID;
            newBench.Spawn();
            newBench.AttachToBuilding(oldBench.buildingID);

            if (deployable.placeEffect.isValid)
            {
                Effect.server.Run(deployable.placeEffect.resourcePath, newBench.transform.position, Vector3.up);
            }

            movedItem.UseItem();

            if (HasPermission(player, RefundPermission))
            {
                player.GiveItem(ItemManager.CreateByName($"workbench{oldBench.Workbenchlevel}"));
            }

            player.EndLooting();
            oldBench.Kill();
            newBench.PlayerOpenLoot(player);
            return true;
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            if (!HasPermission(player, UpgradePermission) && !HasPermission(player, DowngradePermission))
            {
                return;
            }

            Workbench workbench = go.GetComponent<Workbench>();
            if (workbench == null)
            {
                return;
            }

            Chat(player, LangKeys.InfoMessage, Lang(LangKeys.ChatCommand, player));
        }
        #endregion

        #region Helper Methods
        private T RaycastAll<T>(BasePlayer player, float distance) where T : BaseEntity
        {
            RaycastHit[] hits = Physics.RaycastAll(player.eyes.HeadRay(), distance);
            GamePhysics.Sort(hits);
            return hits
                .Where(h => h.GetEntity() is T)
                .Select(h => h.GetEntity() as T)
                .FirstOrDefault();
        }
        
        private void Chat(BasePlayer player, string key, params object[] args) => PrintToChat(player, Lang(LangKeys.Chat, player, Lang(key, player, args)));

        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        public class PluginConfig
        {
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Display Placement Message")]
            public bool DisplayPlacementMessage { get; set; }
        }
        
        private class LangKeys
        {
            public const string Chat = nameof(Chat);
            public const string InfoMessage =  nameof(InfoMessage) + "V1";
            public const string UpgradeNotAllowed =  nameof(UpgradeNotAllowed);
            public const string DowngradeNotAllow =  nameof(DowngradeNotAllow);
            public const string NotAllowed =  nameof(NotAllowed);
            public const string NotLookingAt =  nameof(NotLookingAt);
            public const string ChatCommand =  nameof(ChatCommand);
        }
        #endregion
    }
}