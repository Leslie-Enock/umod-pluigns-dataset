using Oxide.Core.Libraries.Covalence;
using System;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using static ConsoleSystem;

namespace Oxide.Plugins
{
    [Info("Deforestation", "Lincoln & redBDGR", "1.0.5")]
    [Description("Make trees fall over like a boss.")]

    class Deforestation : RustPlugin
    {
        private const string permUse = "deforestation.use";
        private const string permRadiusBypass = "deforestation.radiusbypass";
        private float radius;

        #region PluginConfig
        //Creating a config file
        private static PluginConfig config;
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Max Radius: ")] public int maxRadius { get; set; }
            [JsonProperty(PropertyName = "Number Of Tree Spawns: ")] public int numberOfTreesToSpawn { get; set; }


            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                maxRadius = 25,
                numberOfTreesToSpawn = 15
            };

        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
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

        #region Helpers

        bool ValidationChecks(BasePlayer player, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                ChatMessage(player, "NoPerm");
                return true;
            }
            if (args.Length != 1)
            {
                ChatMessage(player, "Syntax");
                return true;
            }
            try
            {
                radius = Convert.ToSingle(args[0]);
            }
            catch
            {
                ChatMessage(player, "InvalidArg");
                return true;
            }
            if (!permission.UserHasPermission(player.UserIDString, permRadiusBypass))
            {
                if (radius > config.maxRadius || radius <= 0)
                {
                    ChatMessage(player, "Radius", config.maxRadius);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Permissions
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permRadiusBypass, this);
        }
        #endregion

        #region Commands

        [ChatCommand("counttrees")]
        private void CountTreesCMD(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player, args)) return;

            int treeCount = 0;
            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(player.transform.position, radius, Vector3.up);
            foreach (RaycastHit hit in hits)
            {
                TreeEntity tree = hit.GetEntity()?.GetComponent<TreeEntity>();
                if (tree) treeCount++;
            }

            ChatMessage(player, "TreeCount", treeCount, radius);
        }

        [ChatCommand("killtree")]
        private void TreeDeforestationCMD(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player, args)) return;


            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(player.transform.position, radius, Vector3.up);
            foreach (RaycastHit hit in hits)
            {
                TreeEntity tree = hit.GetEntity()?.GetComponent<TreeEntity>();
                if (!tree) continue;
                tree.OnDied(new HitInfo() { PointStart = player.transform.position, PointEnd = tree.transform.position });
            }
        }
        [ChatCommand("killbush")]
        private void BushDeforestationCMD(BasePlayer player, string command, string[] args)
        {

            if (ValidationChecks(player, args)) return;

            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(player.transform.position, radius, Vector3.up);
            foreach (RaycastHit hit in hits)
            {
                BushEntity bush = hit.GetEntity()?.GetComponent<BushEntity>();
                if (bush) bush.Kill();
            }
        }

        [ChatCommand("growtree")]
        private void TreeGrowCMD(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player, args)) return;

            float radius;
            if (!float.TryParse(args[0], out radius))
            {
                player.ChatMessage("Invalid radius value.");
                return;
            }

            int numberOfTrees = config.numberOfTreesToSpawn;
            List<Vector3> treePositions = new List<Vector3>();

            for (int i = 0; i < numberOfTrees; i++)
            {
                Vector3 randomPosition = GetRandomPositionWithinRadius(player.transform.position, radius);
                float terrainHeight = TerrainMeta.HeightMap.GetHeight(randomPosition);
                randomPosition.y = terrainHeight + 100f; // Cast the ray from a higher position
                RaycastHit[] hits = UnityEngine.Physics.RaycastAll(randomPosition, Vector3.down, 200f);
                bool suitablePosition = false;

                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Terrain"))
                    {
                        //get terrain type at position
                        TerrainBiome.Enum biome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiome(randomPosition, 3);
                        player.ChatMessage(biome.ToString());

                        randomPosition.y = hit.point.y;
                        suitablePosition = true;
                        break;
                    }
                }

                if (suitablePosition)
                {
                    treePositions.Add(randomPosition);
                }
                else
                {
                    i--;
                }
            }

            foreach (Vector3 position in treePositions)
            {
                SpawnTree(position);
            }
        }

        private Vector3 GetRandomPositionWithinRadius(Vector3 center, float radius)
        {
            float randomAngle = UnityEngine.Random.Range(0f, 360f);
            float randomDistance = UnityEngine.Random.Range(0f, radius);

            Vector3 direction = new Vector3(Mathf.Sin(randomAngle), 0f, Mathf.Cos(randomAngle));
            Vector3 position = center + direction * randomDistance;

            return position;
        }

        private void SpawnTree(Vector3 position)
        {
            string treePrefab = "assets/bundled/prefabs/autospawn/resource/v3_tundra_forestside/pine_b.prefab";
            TreeEntity tree = GameManager.server.CreateEntity(treePrefab, position, new Quaternion(), true) as TreeEntity;
            tree.Spawn();
        }

        #endregion

        #region Localization
        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TreeCount"] = "<color=#ffc34d>Deforestation</color>: There are {0} trees within a {1} meter radius of you.",
                ["Radius"] = "<color=#ffc34d>Deforestation</color>: Invalid radius. Please choose a radius between 1 and {0}",
                ["NoPerm"] = "<color=#ffc34d>Deforestation</color>: You don't have permission to use that.",
                ["Syntax"] = "<color=#ffc34d>Deforestation</color>: Invalid syntax! Type /killtree or /killbush <radius>",
                ["InvalidArg"] = "<color=#ffc34d>Deforestation</color>: Invalid argument! Make sure the radius is a whole number!",

            }, this, "en");
        }
        #endregion

        private void Unload()
        {
            config = null;
        }
    }
}
