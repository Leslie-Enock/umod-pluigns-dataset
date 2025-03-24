using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;



namespace Oxide.Plugins
{
    [Info("Loot Hacker", "Zeeuss", "0.1.2")]
    [Description("Unlocks codelocks on storages")]
    public class LootHacker : RustPlugin
    {

        #region Init
        private const string PermissionUseCmnd = "loothacker.cmnd";
        private const string PermissionUseItem = "loothacker.item";

        private void Init()
        {
            permission.RegisterPermission(PermissionUseCmnd, this);
            permission.RegisterPermission(PermissionUseItem, this);
            if(!LoadConfigVariables())
            {
                return;
            }
        }

        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //FORMAT: {0} = storage short prefab name, {1} = seconds to unlock
                ["MustLookAtStorage"] = "You must be looking at a locked storage container to hack it!",
                ["NoPerms"] = "You don't have permission to use this!",
                ["UnlocksIn"] = "{0} will unlock in {1} secs",
                ["Unlocked"] = "Unlocked {0}"

            }, this);
        }
        #endregion

        #region Oxide Hook

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input == null)
            {
                return;
            }
            if (player == null)
            {
                return;
            }
            if(player.GetActiveItem() == null)
            {
                return;
            }
            // Check if player pressed PRIMARY hotkey & has targeting computer selected in hotbar
            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY) && player.GetActiveItem().info.name == "targeting_computer.item")
            {

                if (!permission.UserHasPermission(player.UserIDString, PermissionUseItem))
                {
                    return;
                }
                
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 3f, Layers))
                {
                    var storageContainer = hit.GetEntity()?.GetComponent<BaseEntity>() as StorageContainer;
                    if (storageContainer != null)
                    {

                        var codeLock = storageContainer.GetComponentInChildren<CodeLock>();

                        if (codeLock != null && codeLock.IsLocked())
                        {

                            SendReply(player, String.Format(lang.GetMessage("UnlocksIn", this, player.UserIDString), storageContainer.ShortPrefabName, configData.unlockTime.ToString()));
                            if(configData.consumeComp == true)
                            {
                                player.GetActiveItem().UseItem(1);
                            }
                            timer.Once(configData.unlockTime, () => {

                                codeLock.SetFlag(BaseEntity.Flags.Locked, false);
                                SendReply(player, String.Format(lang.GetMessage("Unlocked", this, player.UserIDString), storageContainer.ShortPrefabName));

                            });
                            

                        }
 
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    SendReply(player, lang.GetMessage("MustLookAtStorage", this, player.UserIDString));
                }
            }
            else
            {
                return;
            }
        }
        #endregion

        #region ChatCommand
        [ChatCommand("loothack")]
        private void UnlockCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUseCmnd))
            {
                SendReply(player, lang.GetMessage("NoPerms", this, player.UserIDString));
                return;
            }

            // Check if player is looking at a locked storage container
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 3f, Layers))
            {
                var storageContainer = hit.GetEntity()?.GetComponent<BaseEntity>() as StorageContainer;
                if (storageContainer != null)
                {
                    
                    var codeLock = storageContainer.GetComponentInChildren<CodeLock>();
                    if (codeLock != null && codeLock.IsLocked())
                    {
                        codeLock.SetFlag(BaseEntity.Flags.Locked, false);

                    }

                    SendReply(player, String.Format(lang.GetMessage("Unlocked", this, player.UserIDString), storageContainer.ShortPrefabName));
                }
            }
            else
            {
                SendReply(player, lang.GetMessage("MustLookAtStorage", this, player.UserIDString));
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Seconds until storage unlocks")]
            public float unlockTime = 5f;

            [JsonProperty(PropertyName = "Consume Targeting Computer on use")]
            public bool consumeComp = true;
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Helper
        private static int Layers = LayerMask.GetMask("Construction", "Deployed");
        #endregion
    }

}