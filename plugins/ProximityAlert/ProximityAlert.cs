using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Proximity Alert", "PaiN", "0.3.7")]
    [Description("Displays a UI warning message when players get within a set radius of the player")]
    class ProximityAlert : RustPlugin
    {
        #region Fields
        [PluginReference]
        private Plugin Clans, EventManager, Friends, HumanNPC;

        static ProximityAlert ins;
        private const string proxUI = "ProximityAlertUI";
        private const string permUse = "proximityalert.use";
        #endregion

        #region Functions
        private void OnServerInitialized()
        {
            ins = this;
            LoadVariables();
            RegisterPermissions();
            CheckDependencies();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<ProximityPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, proxUI);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse)) return;
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2, () => OnPlayerConnected(player));
                return;
            }
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
                UnityEngine.Object.DestroyImmediate(proxPlayer);

            NextTick(() => player.gameObject.AddComponent<ProximityPlayer>());
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, proxUI);
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
                UnityEngine.Object.Destroy(proxPlayer);
        }

        private void OnEntityDeath(BasePlayer victim, HitInfo info) => HandleVictim(victim, info);

        private void OnPlayerTeleported(BasePlayer player, Vector3 oldPos, Vector3 newPos)
        {
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null && proxPlayer.isEnabled)
            {
                proxPlayer.inProximity.Clear();
                CuiHelper.DestroyUi(player, proxUI);
            }
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission("proximityalert.use", this);
            if (configData.UseCustomPermissions)
            {
                foreach (var perm in configData.CustomPermissions)
                {
                    permission.RegisterPermission(perm.Key, this);
                }
            }
        }

        private void CheckDependencies()
        {
            if (!Friends) PrintWarning("Friends could not be found! Unable to use friends feature");
            if (!Clans) PrintWarning("Clans could not be found! Unable to use clans feature");
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            var playerProx = player?.GetComponent<ProximityPlayer>();
            if (playerProx != null && playerProx.isEnabled && playerProx.justDied)
                playerProx.justDied = false;
        }

        private void HandleVictim(BasePlayer victim, HitInfo info)
        {
            if (!DoChecks(victim)) return;

            if (victim.userID.IsSteamId())
            {
                var vicProx = victim?.GetComponent<ProximityPlayer>();

                if (vicProx != null && vicProx.isEnabled)
                {
                    vicProx.justDied = true;
                    vicProx.inProximity.Clear();
                    CuiHelper.DestroyUi(victim, proxUI);
                }
            }

            foreach (var obj in UnityEngine.Object.FindObjectsOfType<ProximityPlayer>())
            {
                if (!obj.isEnabled) continue;
                if (obj.inProximity.Contains(victim.userID))
                {
                    obj.inProximity.Remove(victim.userID);
                    if (obj.inProximity.Count == 0)
                    {
                        ProxCollisionLeave(obj.player);
                        timer.In(5, () => CuiHelper.DestroyUi(obj.player, proxUI));
                    }
                }
            }
        }

        private bool DoChecks(BasePlayer enemy)
        {
            if (!configData.DetectSleepers && enemy.IsSleeping()) return false;
            if (!configData.DetectScientists && IsScientist(enemy)) return false;
            if (!configData.DetectTunnelNPCs && IsTunnelNPC(enemy)) return false;
            if (!configData.DetectBanditNPCs && IsBanditNPC(enemy)) return false;
            if (!configData.DetectHumanNPCs && HumanNPC)
            {
                if (enemy.userID >= 41234564 && enemy.userID <= 11474836478)
                    return false;
            }

            return true;
        }

        private void ProxCollisionEnter(BasePlayer player)
        {
            var UI = CreateUI(lang.GetMessage("warning", this, player.UserIDString));
            CuiHelper.DestroyUi(player, proxUI);
            CuiHelper.AddUi(player, UI);
        }

        private void ProxCollisionLeave(BasePlayer player)
        {
            var UI = CreateUI(lang.GetMessage("clear", this, player.UserIDString));
            CuiHelper.DestroyUi(player, proxUI);
            CuiHelper.AddUi(player, UI);
        }

        private float GetPlayerRadius(BasePlayer player)
        {
            foreach (var perm in configData.CustomPermissions)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                    return perm.Value;
            }
            if (permission.UserHasPermission(player.UserIDString, permUse))
                return configData.TriggerRadius;
            return 0f;
        }

        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
                if (playerTag == friendTag) return true;
            return false;
        }

        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends) return false;
            bool isFriend = (bool)Friends?.Call("IsFriend", playerID, friendID);
            return isFriend;
        }

        private bool IsTeam(BasePlayer player, BasePlayer target) => (player.currentTeam != 0UL && target.currentTeam != 0UL && player.currentTeam == target.currentTeam) ? true : false;

        private bool IsScientist(BaseNetworkable networkable) => networkable is ScientistNPC;

        private bool IsTunnelNPC(BaseNetworkable networkable) => networkable is TunnelDweller;

        private bool IsBanditNPC(BaseNetworkable networkable) => networkable.ShortPrefabName.Contains("bandit");

        private bool IsPlaying(BasePlayer player) => (EventManager?.Call("IsEventPlayer", player) != null) ? true : false;

        private void JoinedEvent(BasePlayer player)
        {
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                proxPlayer.isEnabled = false;
            }
        }

        private void LeftEvent(BasePlayer player)
        {
            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                proxPlayer.isEnabled = true;
            }
        }
        #endregion

        #region UI
        public CuiElementContainer CreateUI(string text)
        {
            var container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = "0 0 0 0"},
                            RectTransform = {AnchorMin = $"{ins.configData.GUI_X_Pos} {ins.configData.GUI_Y_Pos}", AnchorMax = $"{ins.configData.GUI_X_Pos + ins.configData.GUI_X_Dim} {ins.configData.GUI_Y_Pos + ins.configData.GUI_Y_Dim}"},
                            CursorEnabled = false
                        },
                        new CuiElement().Parent = "Hud",
                        proxUI
                    }
                };
            container.Add(new CuiLabel
            {
                Text = { FontSize = ins.configData.FontSize, Align = TextAnchor.MiddleCenter, Text = text },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }

            },
            proxUI,
            CuiHelper.GetGuid());
            return container;
        }
        #endregion

        #region Chat Command
        [ChatCommand("prox")]
        private void cmdProx(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse)) return;

            var proxPlayer = player.GetComponent<ProximityPlayer>();
            if (proxPlayer != null)
            {
                CuiHelper.DestroyUi(player, proxUI);
                if (proxPlayer.isEnabled)
                {
                    proxPlayer.isEnabled = false;
                    SendReply(player, lang.GetMessage("deactive", this, player.UserIDString));
                }
                else
                {
                    proxPlayer.isEnabled = true;
                    SendReply(player, lang.GetMessage("active", this, player.UserIDString));
                }
            }
        }
        #endregion

        #region Player Class
        private class ProximityPlayer : MonoBehaviour
        {
            public BasePlayer player;
            private Timer destroyTimer;
            public List<ulong> inProximity = new List<ulong>();
            public bool isEnabled;
            public bool justDied;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();

                var child = gameObject.CreateChild();
                var collider = child.AddComponent<SphereCollider>();
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = ins.GetPlayerRadius(player);
                collider.isTrigger = true;

                isEnabled = true;
                justDied = false;
            }

            private void OnTriggerEnter(Collider col)
            {
                var enemy = col.GetComponentInParent<BasePlayer>();
                if (enemy != null && enemy != player)
                {
                    if (enemy.GetComponent<ProximityPlayer>()?.justDied == true) return;
                    if (inProximity.Contains(enemy.userID)) return;

                    if (ins.IsTeam(player, enemy)) return;
                    if (ins.IsFriend(player.userID, enemy.userID)) return;
                    if (ins.IsClanmate(player.userID, enemy.userID)) return;
                    if (ins.IsPlaying(enemy)) return;

                    if(!ins.DoChecks(enemy)) return;

                    if (inProximity.Count == 0 && isEnabled)
                    {
                        if (destroyTimer != null)
                            destroyTimer.Destroy();
                        ins.ProxCollisionEnter(player);
                    }
                    inProximity.Add(enemy.userID);
                }
            }

            private void OnTriggerExit(Collider col)
            {
                var enemy = col.GetComponentInParent<BasePlayer>();
                if (enemy != null && enemy != player && inProximity.Contains(enemy.userID))
                {
                    inProximity.Remove(enemy.userID);
                    if (inProximity.Count == 0 && isEnabled)
                    {
                        ins.ProxCollisionLeave(player);
                        destroyTimer = ins.timer.In(5, () => CuiHelper.DestroyUi(player, proxUI));
                    }
                }
            }
        }
        #endregion

        #region Config
        private ConfigData configData;

        private class ConfigData
        {
            public bool DetectSleepers { get; set; }
            public bool DetectHumanNPCs { get; set; }
            public bool DetectTunnelNPCs { get; set; }
            public bool DetectScientists { get; set; }
            public bool DetectBanditNPCs { get; set; }

            public float GUI_X_Pos { get; set; }
            public float GUI_X_Dim { get; set; }
            public float GUI_Y_Pos { get; set; }
            public float GUI_Y_Dim { get; set; }
            public int FontSize { get; set; }
            public float TriggerRadius { get; set; }
            public Dictionary<string, float> CustomPermissions { get; set; }
            public bool UseCustomPermissions { get; set; }
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                CustomPermissions = new Dictionary<string, float>
                {
                    { "proximityalert.vip1", 50f },
                    { "proximityalert.vip2", 75f },
                },
                DetectHumanNPCs = false,
                DetectTunnelNPCs = false,
                DetectSleepers = false,
                DetectScientists = false,
                DetectBanditNPCs = false,
                FontSize = 18,
                GUI_X_Pos = 0.2f,
                GUI_X_Dim = 0.6f,
                GUI_Y_Pos = 0.1f,
                GUI_Y_Dim = 0.16f,
                TriggerRadius = 25f,
                UseCustomPermissions = true
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"warning", "<color=#cc0000>Caution!</color> There are players nearby!" },
                {"clear", "<color=#ffdb19>Clear!</color>" },
                {"active", "You have activated ProximityAlert" },
                {"deactive", "You have deactivated ProximityAlert" }
            }, this);
        }
        #endregion
    }
}
