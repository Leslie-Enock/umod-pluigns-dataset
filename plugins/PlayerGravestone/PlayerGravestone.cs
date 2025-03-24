using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Player Gravestone", "Lincoln", "1.2.2")]
    [Description("Spawns a gravestone at a player's death location")]
    class PlayerGravestone : RustPlugin
    {
        private const string PermUse = "playergravestone.use";
        private const string GravestonePrefab = "assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab";
        private const string GravestoneShortName = "gravestone.stone.deployed";

        private Dictionary<ulong, KillInfo> killInfos = new Dictionary<ulong, KillInfo>();

        private PluginConfig config;

        #region Configuration
        private class PluginConfig
        {
            public int GraveStoneDespawnTimeInSeconds = 60;
            public int KillInfoPersistenceTimeInSeconds = 300; // New config option
            public bool SpawnGraveStoneInAuthZone = false;
            public bool OnlyOwnerCanDamageGraveStone = false;
            public bool BroadcastDeathMessageToServer = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config, true);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            LoadConfig();
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (!player.userID.IsSteamId() || hitInfo == null || !permission.UserHasPermission(player.UserIDString, PermUse))
                return null;

            if (!config.SpawnGraveStoneInAuthZone && player.IsBuildingAuthed())
                return null;

            RemoveExistingGravestones(player.userID);
            SpawnGravestone(player, hitInfo);

            return null;
        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity.ShortPrefabName != GravestoneShortName || !config.OnlyOwnerCanDamageGraveStone)
                return;

            if (entity.OwnerID != 0uL && entity.OwnerID != info.InitiatorPlayer?.userID)
                info.damageTypes.ScaleAll(0f);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse) || !input.WasJustPressed(BUTTON.USE))
                return;

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity?.ShortPrefabName == GravestoneShortName)
                {
                    DisplayDeathMessage(player, entity.OwnerID);
                }
            }
        }
        #endregion

        #region Helper Methods
        private void RemoveExistingGravestones(ulong playerID)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var gravestone = entity as BaseEntity;
                if (gravestone?.ShortPrefabName == GravestoneShortName && gravestone.OwnerID == playerID)
                {
                    gravestone.Kill();
                }
            }
        }

        private void SpawnGravestone(BasePlayer player, HitInfo hitInfo)
        {
            Vector3 gravestonePos = player.transform.position + (player.transform.forward * -1f);
            var grave = GameManager.server.CreateEntity(GravestonePrefab, gravestonePos, Quaternion.identity, true);

            if (grave != null && !player.IsNpc && hitInfo.Initiator != null)
            {
                grave.OwnerID = player.userID;
                grave.Spawn();

                string attackerName = ExtractAttackerName(hitInfo.Initiator.ToString());
                killInfos[player.userID] = new KillInfo { VictimName = player.displayName, AttackerName = attackerName };

                if (config.BroadcastDeathMessageToServer)
                {
                    rust.BroadcastChat(null, $" <color=#ff6666>R.I.P</color>: <color=#ffc34d>{player.displayName}</color> was killed by a wild <color=orange>{attackerName}</color>");
                }

                // Schedule removal of kill info
                timer.Once(config.KillInfoPersistenceTimeInSeconds, () => killInfos.Remove(player.userID));

                // Schedule despawn of gravestone
                timer.Once(config.GraveStoneDespawnTimeInSeconds, () => grave.Kill());
            }
        }

        private string ExtractAttackerName(string attackerString)
        {
            return Regex.Replace(attackerString, @"\[|\]|\d", "");
        }

        private void DisplayDeathMessage(BasePlayer player, ulong gravestoneOwnerID)
        {
            if (killInfos.TryGetValue(gravestoneOwnerID, out KillInfo killInfo))
            {
                ChatMessage(player, "DeathMessage", killInfo.VictimName, killInfo.AttackerName);
            }
            else
            {
                ChatMessage(player, "UnknownDeathMessage");
            }
        }
        #endregion

        #region Localization
        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(lang.GetMessage(messageName, this, player.UserIDString), args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DeathMessage"] = "<color=#ff6666>R.I.P</color>\n<color=#ffc34d>{0}</color> was killed by a wild <color=orange>{1}</color>.",
                ["UnknownDeathMessage"] = "<color=#ff6666>R.I.P</color>\nThe details of this death have been lost to time."
            }, this);
        }
        #endregion

        private class KillInfo
        {
            public string VictimName { get; set; }
            public string AttackerName { get; set; }
        }
    }
}