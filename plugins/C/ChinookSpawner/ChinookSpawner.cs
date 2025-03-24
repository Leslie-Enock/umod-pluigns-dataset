using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chinook Spawner", "ziptie", 1.1)]
    [Description("Spawn and despawn chinooks on command.")]
    public class ChinookSpawner : CovalencePlugin
    {
        // Definitions
        public Dictionary<ulong, bool> hasChinook = new Dictionary<ulong, bool>();
        private PluginConfig config;

        // Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AlreadyHasChinook"] = "You aleady have a chinook spawned!",
                ["SuccessfullySpawned"] = "You spawned a chinook",
                ["NoChinook"] = "You do not have a chinook",
                ["ChinookDestroyed"] = "Your chinook was destroyed!",
                ["CannotSpawn"] = "You cannot spawn a chinook here",
                ["CannotSpawnInBuildingBlock"] = "You cannot spawn a chinook in a building blocked zone",
                ["TooFar"] = "You cannot spawn a chinook that far away"
            }, this);
        }

        //Config
        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                SpawnFireOnCrash = true,
                SpawnGibsOnCrash = true,
                BlockSpawningInBuildingBlock = true,
                SpawnDistance = 10,
            };
        }

        // Commands
        [Command("chinook"), Permission("chinookspawner.spawn")]
        private void SpawnChinook(IPlayer player, string command, string[] args)
        {
            var playerBP = (player.Object as BasePlayer);
            if (playerBP != null)
            {
                if (hasChinook.ContainsKey(playerBP.userID))
                {
                    if (hasChinook[playerBP.userID] == false)
                    {
                        if (CreateChinook(playerBP) == true)
                        {
                            hasChinook[playerBP.userID] = true;
                            player.Reply(lang.GetMessage("SuccessfullySpawned", this, player.Id));
                        }
                    }
                    else
                    {
                        player.Reply(lang.GetMessage("AlreadyHasChinook", this, player.Id));
                    }
                }
                else
                {
                    hasChinook.Add(playerBP.userID, false);
                    SpawnChinook(player, command, args);
                }
            }
        }
        [Command("nochinook"), Permission("chinookspawner.despawn")]
        private void NoChinook(IPlayer player, string command, string[] args)
        {
            var playerBP = (player.Object as BasePlayer);
            if (playerBP != null)
            {
                if (hasChinook.ContainsKey(playerBP.userID))
                {
                    if (hasChinook[playerBP.userID] == true)
                    {
                        if (DespawnChinook(playerBP) == true)
                        {
                            hasChinook[playerBP.userID] = false;
                        }
                    }
                    else
                    {
                        player.Reply(lang.GetMessage("NoChinook", this, player.Id));
                    }
                }
                else
                {
                    hasChinook.Add(playerBP.userID, false);
                    NoChinook(player, command, args);
                }
            }
        }

        // Hooks
        void OnEntityKill(CH47Helicopter entity)
        {
                var ch47 = entity.GetComponent<ChinookIdentifier>();
                if (ch47 != null)
                {
                    players.FindPlayer(ch47.ownerID.ToString()).Reply(lang.GetMessage("ChinookDestroyed", this, ch47.ownerID.ToString()));
                    hasChinook[ch47.ownerID] = false;
                }
        }

        // Methods
        private bool CreateChinook(BasePlayer player)
        {
            Vector3 position;
            if (player != null)
            {
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity,
                    LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
                {
                    player.ChatMessage(lang.GetMessage("CannotSpawn", this, player.UserIDString));
                    return false;
                }

                if (hit.distance > config.SpawnDistance)
                {
                    player.ChatMessage(lang.GetMessage("TooFar", this, player.UserIDString));
                    return false;
                }
                if (config.BlockSpawningInBuildingBlock == true)
                {
                    if (player.IsBuildingBlocked() == true)
                    {
                        player.ChatMessage(lang.GetMessage("CannotSpawnInBuildingBlock", this, player.UserIDString));
                        return false;
                    }
                }
                position = hit.point + Vector3.up * 2f;
                CH47Helicopter ch = GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47.entity.prefab", position, player.eyes.transform.localRotation, true) as CH47Helicopter;
                if (ch != null)
                {
                    if (!config.SpawnGibsOnCrash == true)
                    {
                        ch.serverGibs.guid = null;
                    }

                    if (!config.SpawnFireOnCrash == true)
                    {
                        ch.fireBall.guid = null;
                    }
                    ch.Spawn();
                    ch.gameObject.AddComponent<ChinookIdentifier>().ownerID = player.userID;
                }
                return true;
            }
            else
            {
                Debug.LogError("CreateChinook Failed: BasePlayer is null");
                return false;
            }
        }
        private bool? DespawnChinook(BasePlayer playerBP)
        {
            if (playerBP != null)
            {
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<CH47Helicopter>())
                {
                    if (entity == null) { return false; }
                    ChinookIdentifier chinookIdentifier = entity.GetComponent<ChinookIdentifier>();
                    if (chinookIdentifier != null)
                    {
                        if (chinookIdentifier.ownerID == playerBP.userID)
                        {
                            entity.AdminKill();
                            return true;
                        }
                    }
                }
            }
            return null;
        }
    }

    // ID Class
    public class ChinookIdentifier : MonoBehaviour
    {
        public ulong ownerID = 0;
    }

    // Config
    public class PluginConfig
    {
        public bool SpawnFireOnCrash = true;
        public bool SpawnGibsOnCrash = true;
        public float SpawnDistance = 10;
        public bool BlockSpawningInBuildingBlock = true;
    }
}