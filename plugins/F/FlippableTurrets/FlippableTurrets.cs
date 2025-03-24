using Oxide.Core;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Flippable Turrets", "Orange", "1.1.0")]
    [Description("Allows users to place turrets how they like")]
    public class FlippableTurrets : RustPlugin
    {
        #region Vars

        private const string prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string permUse = "flippableturrets.use";

        private DynamicConfigFile configFile;
        private Configuration config;

        #endregion

        #region Configuration

        private class Configuration
        {
            public HashSet<ulong> IgnoredSkinIDs = new HashSet<ulong>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configFile = Config;
            try
            {
                config = Config.ReadObject<Configuration>() ?? new Configuration();
                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load configuration. Using default values.");
                config = new Configuration();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();

        #endregion

        #region Language

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TurretSkinBlocked"] = "This turret cannot be flipped due to its skin."
            }, this);
        }

        private string GetMessage(string key, BasePlayer player) => lang.GetMessage(key, this, player?.UserIDString);

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            CheckInput(player, input);
        }

        #endregion

        #region Core

        private void CheckInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                return;

            var heldItem = player.GetActiveItem();
            if (heldItem == null
                || heldItem?.info?.shortname != "autoturret"
                || !permission.UserHasPermission(player.UserIDString, permUse)
                || !player.CanBuild())
                return;

            if (config.IgnoredSkinIDs.Contains(heldItem.skin))
            {
                SendReply(player, GetMessage("TurretSkinBlocked", player));
                return;
            }

            RaycastHit rhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out rhit))
                return;

            var entity = rhit.GetEntity();
            if (entity == null
                || rhit.distance > 5f
                || !entity.ShortPrefabName.Contains("floor")
                || entity.transform.position.y <= player.transform.position.y)
                return;

            AutoTurret turret = GameManager.server.CreateEntity(prefab) as AutoTurret;
            if (turret == null)
                return;

            turret.transform.position = rhit.point;
            turret.transform.LookAt(player.transform);
            turret.transform.rotation = Quaternion.identity;
            turret.transform.Rotate(0, 0, 180);
            turret.OwnerID = player.userID;
            turret.SetParent(entity, true);
            turret.Spawn();

            var groundMissing = turret?.gameObject?.GetComponent<DestroyOnGroundMissing>();
            if (groundMissing != null)
                UnityEngine.Object.DestroyImmediate(groundMissing);

            var groundWatch = turret?.gameObject?.GetComponent<GroundWatch>();
            if (groundWatch != null)
                UnityEngine.Object.DestroyImmediate(groundWatch);

            Interface.CallHook("OnEntityBuilt", heldItem.GetHeldEntity(), turret.gameObject);
            player.inventory.Take(null, heldItem.info.itemid, 1);
        }

        #endregion
    }
}
