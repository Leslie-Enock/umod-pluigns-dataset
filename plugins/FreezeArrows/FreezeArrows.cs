using Facepunch;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("FreezeArrows", "Colon Blow", "1.1.2")]
    class FreezeArrows : RustPlugin
    {
        // fix for 5/4 Rust update.

        #region Loadup

        List<ulong> FrozenEntityList = new List<ulong>();
        Dictionary<ulong, string> GuiInfo = new Dictionary<ulong, string>();
        Dictionary<ulong, ShotArrowData> loadArrow = new Dictionary<ulong, ShotArrowData>();

        class ShotArrowData
        {
            public BasePlayer player;
            public int arrows;
            public bool arrowenabled;
        }

        void Loaded()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!loadArrow.ContainsKey(player.userID))
                {
                    loadArrow.Add(player.userID, new ShotArrowData
                    {
                        player = player,
                        arrows = config.FreezeArrowConfig.StartingArrowCount,
                        arrowenabled = false
                    });
                }
            }
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission("freezearrows.allowed", this);
            permission.RegisterPermission("freezearrows.unlimited", this);
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public GlobalArrowSettings FreezeArrowConfig { get; set; }

            public class GlobalArrowSettings
            {
                [JsonProperty(PropertyName = "Time - How long player is frozen when hit.")] public float FreezeTime { get; set; }
                [JsonProperty(PropertyName = "Time - Cooldown for freezing same player again.")] public float ReFreezeCooldown { get; set; }
                [JsonProperty(PropertyName = "Radius - The distance from impact players are effeted.")] public float FreezeRadius { get; set; }
                [JsonProperty(PropertyName = "Overlay - How long frozen overlay is shown when player is frozen")] public float FreezeOverlayTime { get; set; }
                [JsonProperty(PropertyName = "Arrows - Number of arrows on startup per player.")] public int StartingArrowCount { get; set; }
                [JsonProperty(PropertyName = "Arrows - Automatically reload another freeze arrow if player has them? (false will toggle freeze arrow off after shooting one) ")] public bool toggleFreezeReload { get; set; }
                [JsonProperty(PropertyName = "Overlay - Show freeze overlay when player is frozen ")] public bool useFreezeOverlay { get; set; }
                [JsonProperty(PropertyName = "Effects - Show hit explosion effect ?")] public bool showHitExplosionFX { get; set; }
                [JsonProperty(PropertyName = "Targets - Arrows will freeze players ?")] public bool freezePlayers { get; set; }
                [JsonProperty(PropertyName = "Targets - Arrows will freeze NPCs ?")] public bool freezeNPCs { get; set; }
                [JsonProperty(PropertyName = "Targets - Arrows will freeze Ridable Horses ?")] public bool freezeRideHorses { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                FreezeArrowConfig = new PluginConfig.GlobalArrowSettings
                {
                    FreezeTime = 10f,
                    ReFreezeCooldown = 120f,
                    FreezeRadius = 5,
                    FreezeOverlayTime = 10f,
                    StartingArrowCount = 1,
                    useFreezeOverlay = true,
                    showHitExplosionFX = true,
                    freezePlayers = true,
                    freezeNPCs = true,
                    freezeRideHorses = true,
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["onnextshottxt"] = "Your next shot will be a Freeze Arrow",
            ["arrowsleft"] = "Arrows remaining : ",
            ["arrowadded"] = "You have added a freeze arrow to your quiver.",
            ["offnextshottxt"] = "Your next shot will a Normal Arrow",
            ["yourfrozetxt"] = "You are frozen in place....",
            ["nofreezearrows"] = "You have no freeze arrows left",
            ["unlimitedfreezearrows"] = "You have unlimited freeze arrows.. have fun :)",
            ["notoggle"] = "You have not toggled Freeze Arrows ye",
            ["unfrozetxt"] = "You are now unfrozen.... "
        };

        #endregion

        #region Commands

        [ChatCommand("freezearrow")]
        void cmdChatfreezearrow(BasePlayer player, string command, string[] args, int arrows, bool arrowenabled)
        {
            if (!HasPermission(player, "freezearrows.allowed")) return;

            if (!loadArrow.ContainsKey(player.userID))
            {
                loadArrow.Add(player.userID, new ShotArrowData
                {
                    player = player,
                    arrows = config.FreezeArrowConfig.StartingArrowCount,
                    arrowenabled = true
                });
                SendReply(player, msg("onnextshottxt", player.UserIDString));
                if (HasPermission(player, "freezearrows.unlimited")) return;
                SendReply(player, msg("arrowsleft", player.UserIDString) + (loadArrow[player.userID].arrows));
                return;
            }
            if (loadArrow[player.userID].arrowenabled)
            {
                loadArrow[player.userID].arrowenabled = false;
                SendReply(player, msg("offnextshottxt", player.UserIDString));
                return;
            }

            if (HasPermission(player, "freezearrows.unlimited"))
            {
                loadArrow[player.userID].arrowenabled = true;
                SendReply(player, msg("onnextshottxt", player.UserIDString));
                return;
            }
            if (loadArrow[player.userID].arrows <= 0)
            {
                SendReply(player, msg("nofreezearrows", player.UserIDString));
                return;
            }
            if (loadArrow[player.userID].arrows >= 1)
            {
                loadArrow[player.userID].arrowenabled = true;
                SendReply(player, msg("onnextshottxt", player.UserIDString));
                if (HasPermission(player, "freezearrows.unlimited")) return;
                SendReply(player, msg("arrowsleft", player.UserIDString) + (loadArrow[player.userID].arrows));
                return;
            }
            return;
        }

        [ChatCommand("freezecount")]
        void cmdChatfreezecount(BasePlayer player, string command, string[] args, int arrows)
        {
            if (!HasPermission(player, "freezearrows.allowed")) return;
            if (HasPermission(player, "freezearrows.unlimited"))
            {
                SendReply(player, msg("unlimitedfreezearrows", player.UserIDString));
                return;
            }
            if (!loadArrow.ContainsKey(player.userID))
            {
                SendReply(player, msg("notoggle", player.UserIDString));
                return;
            }
            if (loadArrow[player.userID].arrows <= 0)
            {
                SendReply(player, msg("nofreezearrows", player.UserIDString));
                return;
            }
            if (loadArrow[player.userID].arrows >= 1)
            {
                SendReply(player, msg("arrowsleft", player.UserIDString) + (loadArrow[player.userID].arrows));
                return;
            }
            return;
        }

        #endregion

        #region Freeze Overlay

        void FrozenGui(BasePlayer player)
        {
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);

            var elements = new CuiElementContainer();
            GuiInfo[player.userID] = CuiHelper.GetGuid();

            elements.Add(new CuiElement
            {
                Name = GuiInfo[player.userID],
                Parent = "Overlay",
                Components =
                    {
                        new CuiRawImageComponent { Sprite = "assets/content/ui/overlay_freezing.png" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
            });

            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Hooks

        void OnPlayerAttack(BasePlayer player, HitInfo hitInfo)
        {
            if (!HasPermission(player, "freezearrows.allowed")) return;
            if (!loadArrow[player.userID].arrowenabled) return;

            if (usingCorrectWeapon(player))
            {
                if (hitInfo.ProjectilePrefab.ToString().Contains("arrow_"))
                {
                    if (loadArrow[player.userID].arrows <= 0)
                    {
                        SendReply(player, msg("nofreezearrows", player.UserIDString));
                        return;
                    }
                    findTarget(player, hitInfo);
                    if (config.FreezeArrowConfig.showHitExplosionFX)
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_03.prefab", hitInfo.HitPositionWorld);
                    }
                    if (!config.FreezeArrowConfig.toggleFreezeReload) loadArrow[player.userID].arrowenabled = !loadArrow[player.userID].arrowenabled;
                    if (HasPermission(player, "freezearrows.unlimited")) return;
                    loadArrow[player.userID].arrows = loadArrow[player.userID].arrows - 1;
                }
            }
            return;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo, int arrows)
        {
            if (hitInfo == null) return;
            if (!(hitInfo.Initiator is BasePlayer)) return;
            if (entity is BaseNpc || entity is BasePlayer)
            {
                var player = (BasePlayer)hitInfo.Initiator;

                if (!HasPermission(player, "freezearrows.allowed")) return;
                if (!loadArrow.ContainsKey(player.userID)) return;
                if (HasPermission(player, "freezearrows.unlimited")) return;

                loadArrow[player.userID].arrows = loadArrow[player.userID].arrows + 1;
                SendReply(player, msg("arrowadded", player.UserIDString));
                SendReply(player, msg("arrowsleft", player.UserIDString) + (loadArrow[player.userID].arrows));
            }
            return;
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                FrozenEntityList.Remove(player.userID);
                string guiInfo;
                if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!loadArrow.ContainsKey(player.userID))
            {
                loadArrow.Add(player.userID, new ShotArrowData
                {
                    player = player,
                    arrows = config.FreezeArrowConfig.StartingArrowCount,
                    arrowenabled = false
                });
            }
            DestroyCui(player);
            FrozenEntityList.Remove(player.userID);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            DestroyCui(player);
            FrozenEntityList.Remove(player.userID);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyCui(player);
            FrozenEntityList.Remove(player.userID);
        }

        #endregion

        #region Helpers

        bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        bool usingCorrectWeapon(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname == "crossbow") return true;
            if (activeItem != null && activeItem.info.shortname == "bow.hunting") return true;
            if (activeItem != null && activeItem.info.shortname == "bow.compound") return true;
            return false;
        }

        public void findTarget(BasePlayer player, HitInfo hitInfo)
        {
            var hitPoint = hitInfo.HitPositionWorld;
            List<BaseEntity> plist = Pool.GetList<BaseEntity>();
            Vis.Entities<BaseEntity>(hitPoint, config.FreezeArrowConfig.FreezeRadius, plist);
            foreach (BaseEntity foundEntity in plist)
            {
                var currentpos = foundEntity.transform.position;
                if (config.FreezeArrowConfig.freezePlayers)
                {
                    var isPlayer = foundEntity.GetComponent<BasePlayer>();
                    if (isPlayer && !FrozenEntityList.Contains(isPlayer.userID) && isPlayer != player)
                    {
                        if (config.FreezeArrowConfig.useFreezeOverlay) FrozenGui(isPlayer);
                        timer.Once(config.FreezeArrowConfig.FreezeOverlayTime, () => DestroyCui(isPlayer));
                        timer.Repeat(0.1f, Convert.ToInt32(config.FreezeArrowConfig.FreezeTime) * 10, () => freezeposition(isPlayer, currentpos));
                        FrozenEntityList.Add(isPlayer.userID);
                        timer.Once(config.FreezeArrowConfig.ReFreezeCooldown, () => FrozenEntityList.Remove(player.userID));
                    }
                }
                if (config.FreezeArrowConfig.freezeNPCs)
                {
                    var isBNPC = foundEntity.GetComponent<BaseNpc>();
                    if (isBNPC) timer.Repeat(0.1f, Convert.ToInt32(config.FreezeArrowConfig.FreezeTime) * 10, () => StopNPCMovement(isBNPC));
                }
                if (config.FreezeArrowConfig.freezeRideHorses)
                {
                    var isRideHorse = foundEntity.GetComponent<BaseRidableAnimal>();
                    if (isRideHorse) timer.Repeat(0.1f, Convert.ToInt32(config.FreezeArrowConfig.FreezeTime) * 10, () => isRideHorse.currentRunState = BaseRidableAnimal.RunState.stopped);
                }
            }
            Pool.FreeList<BaseEntity>(ref plist);
        }

        void StopNPCMovement(BaseNpc npc)
        {
            if (npc == null) return;
            npc.StopMoving();
        }

        void freezeposition(BasePlayer player, Vector3 newPos)
        {
            if (player == null) return;
            newPos = player.transform.position;
            ForcePlayerPosition(player, newPos);
        }

        void DestroyCui(BasePlayer player)
        {
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
        }

        #endregion

    }

}