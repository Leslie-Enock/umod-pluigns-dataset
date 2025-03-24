using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Small Shelves", "Wulf", "1.1.2")]
    [Description("Allow players to place smaller shelves")]
    public class SmallShelves : CovalencePlugin
    {
        #region Initialization

        private readonly List<string> activatedIDs = new List<string>();

        private const string permUse = "smallshelves.use";
        private const string prefab = "assets/scripts/entity/misc/visualstoragecontainer/visualshelvestest.prefab";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand(nameof(CommandSmallShelves));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSmallShelves"] = "smallshelves",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlacementActivated"] = "Small shelf placement activated",
                ["PlacementDeactivated"] = "Small shelf placement de-activated"
            }, this);
        }

        #endregion Initialization

        #region Entity Handling

        private void SpawnSmallShelves(Vector3 pos, Quaternion rot, DecayEntity floor, ulong ownerId = 0uL)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, rot);
            if (entity != null)
            {
                LootContainer lootContainer = entity.GetComponent<LootContainer>();
                lootContainer.destroyOnEmpty = false;
                lootContainer.initialLootSpawn = false;
                lootContainer.SetFlag(BaseEntity.Flags.Locked, true);
                entity.GetComponent<DecayEntity>().AttachToBuilding(floor);
                entity.gameObject.AddComponent<DestroyOnGroundMissing>();
                GroundWatch groundWatch = entity.gameObject.AddComponent<GroundWatch>();
                groundWatch.InvokeRepeating("OnPhysicsNeighbourChanged", 0f, 0.15f);
                entity.OwnerID = ownerId;
                entity.Spawn();
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.ShortPrefabName != "shelves" || !activatedIDs.Contains(entity.OwnerID.ToString()))
            {
                return;
            }

            NextFrame(() => entity.Kill());

            RaycastHit hit;
            Physics.Raycast(entity.transform.position + new Vector3(0, 0.1f, 0), Vector3.down, out hit, 3f);
            DecayEntity decayEntity = hit.GetEntity()?.GetComponent<DecayEntity>();
            if (decayEntity != null)
            {
                SpawnSmallShelves(entity.transform.position - (entity.transform.forward * 0.35f), entity.transform.rotation, decayEntity, entity.OwnerID);
            }
        }

        private object CanAcceptItem(ItemContainer container)
        {
            VisualStorageContainer storageContainer = container?.entityOwner?.GetComponent<VisualStorageContainer>();
            if (storageContainer != null)
            {
                return ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        #endregion Entity Handling

        #region Commands

        private void CommandSmallShelves(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (activatedIDs.Contains(player.Id))
            {
                activatedIDs.Remove(player.Id);
                Message(player, "PlacementDeactivated");
            }
            else
            {
                activatedIDs.Add(player.Id);
                Message(player, "PlacementActivated");
            }
        }

        #endregion Commands

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}
