using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Breakable CCTV", "Clearshot", "1.1.0")]
    [Description("Shoot monument CCTV cameras to temporarily disable them")]
    class BreakableCCTV : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");
        private Dictionary<string, int> _cctvHealth = new Dictionary<string, int>();

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        private void OnServerInitialized()
        {
            bool added = false;
            foreach (var cctv in UnityEngine.Object.FindObjectsOfType<CCTV_RC>().OrderBy(x => x.GetIdentifier()))
            {
                string id = cctv.GetIdentifier();
                string netID = cctv.net.ID.ToString();
                if (id.Contains(netID))
                {
                    id = id.Replace(netID, "");
                }

                if (cctv != null && cctv.isStatic && cctv.OwnerID == 0 && !_config.cctvConfig.ContainsKey(id))
                {
                    Puts("Adding missing CCTVConfig: " + id);
                    _config.cctvConfig.Add(id, new CCTVConfig());
                    added = true;
                }
            }

            if (added)
                Config.WriteObject(_config);
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || !(entity is CCTV_RC) || info == null || info.InitiatorPlayer == null) return null;
            CCTV_RC cctv = entity as CCTV_RC;
            if (cctv == null || !cctv.isStatic || cctv.OwnerID != 0 || cctv.limitNetworking) return null;

            string cctvID = cctv.GetIdentifier(), cctvIDModified = cctvID;
            string netID = cctv.net.ID.ToString();
            if (cctvIDModified.Contains(netID))
            {
                cctvIDModified = cctvIDModified.Replace(netID, "");
            }

            CCTVConfig cctvCfg;
            if (!_config.cctvConfig.TryGetValue(cctvIDModified, out cctvCfg)) return null;
            if (!cctvCfg.enabled) return null;

            BasePlayer pl = info.InitiatorPlayer;
            int dmg = UnityEngine.Random.Range(_config.damagePerHitMin, _config.damagePerHitMax);
            int health = _cctvHealth[cctvID] = Math.Max((_cctvHealth.ContainsKey(cctvID) ? _cctvHealth[cctvID] : cctvCfg.health) - dmg, 0);
            string msg = string.Format(lang.GetMessage("CameraHealth", this, pl.UserIDString), health);

            if (health < 1)
            {
                cctv.limitNetworking = true;
                Effect.server.Run(_config.destroyEffect, cctv.transform.position);

                float respawnTime = cctvCfg.respawnTime > 0 ? cctvCfg.respawnTime : _config.globalRespawnTime;
                timer.Once(respawnTime, () => {
                    if (cctv != null)
                    {
                        cctv.limitNetworking = false;
                        _cctvHealth.Remove(cctvID);
                        Effect.server.Run(_config.restoreEffect, cctv.transform.position);
                    }
                });

                msg = string.Format(lang.GetMessage("CameraDisabled", this, pl.UserIDString), respawnTime);
            }

            SendChatMsg(pl, msg);
            return null;
        }

        private void Unload()
        {
            foreach (var cctv in UnityEngine.Object.FindObjectsOfType<CCTV_RC>())
            {
                if (cctv != null && cctv.isStatic && cctv.OwnerID == 0 && cctv.limitNetworking)
                {
                    cctv.limitNetworking = false;
                }
            }
        }

        #region Config

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatPrefix"] = "<color=#00a7fe>[Breakable CCTV]</color>",
                ["CameraHealth"] = "Camera HP: <color=#cb3f2a>{0}</color>",
                ["CameraDisabled"] = "Camera disabled for <color=#87b33a>{0}s</color>"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
        }

        private class PluginConfig
        {
            public string chatIconID = "0";
            public int globalRespawnTime = 60;
            public int damagePerHitMin = 10;
            public int damagePerHitMax = 30;
            public string destroyEffect = "assets/bundled/prefabs/fx/item_break.prefab";
            public string restoreEffect = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
            public Dictionary<string, CCTVConfig> cctvConfig = new Dictionary<string, CCTVConfig>();
        }

        private class CCTVConfig
        {
            public bool enabled = true;
            public int health = 100;
            public int respawnTime = 0;
        }

        #endregion
    }
}
