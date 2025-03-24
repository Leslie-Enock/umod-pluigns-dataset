using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Stash Blocker", "Orange/Dana", "1.1.0")]
    [Description("Restricts the placement of stash containers.")]
    public class StashBlocker : RustPlugin
    {
        #region Fields

        private Configuration config { get; set; }

        private const string permissionIgnore = "stashblocker.ignore";
        private const string stashPrefab = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";

        #endregion

        #region Configuration

        private class Configuration
        {
            [JsonProperty(PropertyName = "Cannot Place Overall")]
            public bool CannotPlaceOverall { get; set; }

            [JsonProperty(PropertyName = "Cannot Place Outside Building Privilege Range")]
            public bool CannotPlaceOutsideBuildingPrivilegeRange { get; set; }

            [JsonProperty(PropertyName = "Cannot Place Nearby Entity")]
            public bool CannotPlaceNearbyEntity { get; set; }

            [JsonProperty(PropertyName = "Entity Detection Radius")]
            public float EntityDetectionRadius { get; set; }

            [JsonProperty(PropertyName = "Entities")]
            public List<string> Entities { get; set; }

            [JsonProperty(PropertyName = "Send Game Tip")]
            public bool SendGameTip { get; set; }

            [JsonProperty(PropertyName = "Game Tip Show Duration")]
            public float GameTipShowDuration { get; set; }
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                CannotPlaceOverall = false,
                CannotPlaceOutsideBuildingPrivilegeRange = true,
                CannotPlaceNearbyEntity = false,
                EntityDetectionRadius = 2f,
                Entities = new List<string>()
                {
                    "sleepingbag_leather_deployed",
                    "small_stash_deployed"
                },
                SendGameTip = true,
                GameTipShowDuration = 3f,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permissionIgnore, this);
        }

        /// <summary>
        /// Hook: Called when the player tries to build or deploy something.
        /// </summary>
        /// <param name="planner"> The building planner held by the player. </param>
        /// <param name="construction"> Contains information about construction that's being built. </param>
        /// <param name="target"></param>
        /// <returns> Returning true or false overrides default behavior. </returns>
        private object CanBuild(Planner planner, Construction construction, Construction.Target target)
        {
            // Obtain the deploying player from the planner.
            BasePlayer ownerPlayer = planner?.GetOwnerPlayer();
            if (ownerPlayer == null)
                return null;

            // Skip if the construction being built isn't a stash container.
            if (construction.fullName != stashPrefab)
                return null;

            if (HasPermission(ownerPlayer, permissionIgnore))
                return null;

            // Proceed if 'CannotPlaceOverall' is enabled.
            if (config.CannotPlaceOverall)
            {
                // Block building.
                SendMessage(ownerPlayer, Message.CannotPlaceOverall);
                return true;
            }

            // Proceed if the player isn't building authorized and 'CannotPlaceOutsideBuildingPrivilegeRange' is enabled.
            if (config.CannotPlaceOutsideBuildingPrivilegeRange && !ownerPlayer.IsBuildingAuthed())
            {
                // Block building.
                SendMessage(ownerPlayer, Message.CannotPlaceBuildingPrivilege);
                return true;
            }

            // Obtain the deploying position of the stash container.
            Vector3 stashPosition = target.entity?.transform ? target.GetWorldPosition() : target.position;
            // Proceed if the position where the stash is to be built is nearby one of the blacklisted entities and 'CannotPlaceNearbyEntity' is enabled.
            if (config.CannotPlaceNearbyEntity && config.Entities.Count > 0 && HasEntityNearby(stashPosition))
            {
                // Block building.
                SendMessage(ownerPlayer, Message.CannotPlaceNearbyEntity);
                return true;
            }

            // Allow building.
            return null;
        }

        #endregion

        #region Functions

        /// <summary>
        /// Determines whether the given position is found nearby other entities.
        /// </summary>
        /// <param name="position"> The given world position of the entity. </param>
        /// <returns> </returns>
        private bool HasEntityNearby(Vector3 position)
        {
            // Capture all entities within range and save them to the declared entities list.
            List<BaseEntity> nearbyEntities = Pool.GetList<BaseEntity>();
            Vis.Entities(position, config.EntityDetectionRadius, nearbyEntities, LayerMask.GetMask("Construction", "Deployable", "Deployed"), QueryTriggerInteraction.Ignore);

            // Proceed if the entities list isn't empty.
            if (nearbyEntities.Any())
            {
                foreach (BaseEntity entity in nearbyEntities)
                {
                    if (config.Entities.Contains(entity.ShortPrefabName))
                    {
                        Pool.FreeList(ref nearbyEntities);
                        return true;
                    }
                }
            }

            Pool.FreeList(ref nearbyEntities);
            return false;
        }

        #endregion

        #region Helper Functions

        private void SendGameTip(BasePlayer player, string message)
        {
            if (player != null)
            {
                message = Formatter.ToPlaintext(message);

                player.Command("gametip.showgametip", message);
                timer.Once(config.GameTipShowDuration, () => player?.Command("gametip.hidegametip"));
            }
        }

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        #endregion

        #region Localization

        private class Message
        {
            public const string CannotPlaceOverall = "CannotPlace.Overall";
            public const string CannotPlaceBuildingPrivilege = "CannotPlace.BuildingPrivilege";
            public const string CannotPlaceNearbyEntity = "CannotPlace.NearbyEntity";
        }

        /// <summary>
        /// Registers and populates the language file with the default messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Message.CannotPlaceOverall] = "Placing stashes is prohibited",
                [Message.CannotPlaceBuildingPrivilege] = "Cannot place stash outside building privilege range",
                [Message.CannotPlaceNearbyEntity] = "Cannot place stash too close to constructions or deployables",
            }, this, "en");
        }

        /// <summary>
        /// Gets the localized and translated message from the localization file.
        /// </summary>
        /// <param name="messageKey"> The message key. </param>
        /// <param name="playerId"> To obtain the selected language of the player. </param>
        /// <param name="args"> Any additional arguments given in the message. </param>
        /// <returns> The localized message for the stated key. </returns>
        private string GetMessage(string messageKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerId), args);
        }

        /// <summary>
        /// Sends the localized and formatted message to the specified player.
        /// </summary>
        /// <param name="player"> The player to whom the message is to be sent. </param>
        /// <param name="messageKey"> The message key. </param>
        /// <param name="args"> Any additional arguments given in the message. </param>
        private void SendMessage(BasePlayer player, string messageKey, object[] args = null)
        {
            string message = GetMessage(messageKey, player.UserIDString);
            if (args != null && args.Length > 0)
                message = string.Format(message, args);

            SendReply(player, message);

            if (config.SendGameTip)
                SendGameTip(player, message);
        }

        #endregion
    }
}