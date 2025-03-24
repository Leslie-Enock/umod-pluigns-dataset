using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Random Positions", "Orange", "1.0.3")]
    [Description("Randomized positions for plugins or for players respawn points")]
    public class RandomPositions : RustPlugin
    {
        #region Vars

        private HashSet<Vector3> positions = new HashSet<Vector3>();
        private const int scanHeight = 100;
        private static int getBlockMask => LayerMask.GetMask("Construction", "Prevent Building", "Water");
        private static bool MaskIsBlocked(int mask) => getBlockMask == (getBlockMask | (1 << mask));
        private const string commandName = "randompositions";

        #endregion
        
        #region Oxide Hooks

        private void Init()
        {
            cmd.AddConsoleCommand(commandName, this, nameof(cmdControlConsole));

            if (config.useForPlayerSpawns == false)
            {
                Unsubscribe(nameof(OnPlayerRespawn));
            }
        }

        private void OnServerInitialized()
        {
            GeneratePositions();
        }

        private object OnPlayerRespawn(BasePlayer player)
        {
            var position = GetValidSpawnPoint();
            if (position == new Vector3())
            {
                return null;
            }
            
            return new BasePlayer.SpawnPoint
            {
                pos = position
            };
        }

        private object GetRandomPosition()
        {
            var position = GetValidSpawnPoint();
            if (position == new Vector3())
            {
                return null;
            }
            
            return position;
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
            {
                return;
            }

            var player = arg.Player();
            var action = arg.Args?.Length > 0 ? arg.Args[0].ToLower() : "null";

            switch (action)
            {
                case "check":
                    var startPosition = player.transform.position - new Vector3(0, 0.2f, 0);
                    GetClosestValidPosition(startPosition, player);
                    break;
                
                case "showall":
                    foreach (var position in positions)
                    {
                        player.SendConsoleCommand("ddraw.box", 10, Color.green, position, 2f);
                    }
                    break;
            }
        }

        #endregion

        #region Core

        private void GeneratePositions()
        {
            positions.Clear();
            var generationSuccess = 0;
            var islandSize = ConVar.Server.worldsize / 2;
            for (var i = 0; i < config.maxGenerationAttempts; i++)
            {
                if (generationSuccess >= config.positionsToGenerate)
                {
                    break;
                }
                
                var x = Core.Random.Range(-islandSize, islandSize);
                var z = Core.Random.Range(-islandSize, islandSize);
                var original = new Vector3(x, scanHeight, z);
                var position = GetClosestValidPosition(original);
                if (position != new Vector3())
                {
                    positions.Add(position);
                    generationSuccess++;
                }
            }
        }

        private Vector3 GetClosestValidPosition(Vector3 original, BasePlayer player = null)
        {
            var target = original - new Vector3(0, 200, 0);
            RaycastHit hitInfo;
            if (Physics.Linecast(original, target, out hitInfo) == false)
            {
                // if (player != null)
                // {
                //     player.ChatMessage("No hit");
                // }
                
                return new Vector3();
            }

            var position = hitInfo.point;
            var collider = hitInfo.collider;
            var colliderName = collider?.name;
            var colliderLayer = collider?.gameObject.layer ?? 4;
            
            if (collider == null)
            {
                // if (player != null)
                // {
                //     player.ChatMessage("collider == null");
                // }
                
                return new Vector3();
            }
           
            if (MaskIsBlocked(colliderLayer) || colliderLayer != 23)
            {
                // if (player != null)
                // {
                //     player.ChatMessage($"hitLayer == getBlockMask ({colliderLayer})");
                // }
                
                return new Vector3();
            }

            if (IsValidPosition(position) == false)
            {
                // if (player != null)
                // {
                //     player.ChatMessage("IsValidPosition(position) == false");
                // }
                
                return new Vector3();
            }

            // if (player != null)
            // {
            //     player.ChatMessage("" +
            //         $"Position is valid! ({position})\n" +
            //         $"Layer: {colliderLayer}\n" +
            //         $"Collider: {colliderName}");
            // }
            
            return position;
        }

        private Vector3 GetValidSpawnPoint()
        {
            for (var i = 0; i < 25; i++)
            {
                var number = Core.Random.Range(0, positions.Count);
                var position = positions.ElementAt(number);
                if (IsValidPosition(position) == true)
                {
                    return position;
                }
                else
                {
                    positions.Remove(position);
                }
            }

            return new Vector3();
        }

        private static bool IsValidPosition(Vector3 position)
        {
            var entities = new List<BuildingBlock>();
            Vis.Entities(position, config.radiusCheck, entities);
            return entities.Count == 0;
        }

        #endregion
        
        #region Configuration | 2.0.0

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Use for player spawns")]
            public bool useForPlayerSpawns = false;
            
            [JsonProperty(PropertyName = "Buildings radius check")]
            public int radiusCheck = 25;
            
            [JsonProperty(PropertyName = "Positions to generate")]
            public int positionsToGenerate = 1500;
            
            [JsonProperty(PropertyName = "Maximal generation attempts")]
            public int maxGenerationAttempts = 3000;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                timer.Every(10f,
                    () =>
                    {
                        PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                    });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}