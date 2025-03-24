//#define DEBUG

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Plagued NPCs", "Wulf", "3.1.1")]
    [Description("Customize NPC player health, attire, skins and weapons")]
    public class PlaguedNPCs : CovalencePlugin
    {
        #region Configuration

        private Configuration _config;

        public class PlaguedNPC
        {
            [JsonProperty("Enable NPC")]
            public bool Enable = true;

            [JsonProperty("Health")]
            public int Health = 100;

            [JsonProperty("Max Health")]
            public int MaxHealth = 100;

            [JsonProperty("Speed")]
            public float Speed = 7f;

            [JsonProperty("No Loot")]
            public bool NoLoot = false;

            [JsonProperty("Glowing Eyes")]
            public bool GlowingEyes = true;

            [JsonProperty("Sound Effect")]
            public bool SoundEffect = true;

            [JsonProperty("Attire", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<string>> Attire = new Dictionary<string, List<string>>()
            {
                {
                    "Head", new List<string>()
                    {
                        "mask.balaclava", "burlap.headwrap", "none"
                    }
                },
                {
                    "Torso", new List<string>()
                    {
                        "burlap.shirt", "tshirt", "tshirt.long", "none"
                    }
                },
                {
                    "Hands", new List<string>()
                    {
                        "burlap.gloves", "none"
                    }
                },
                {
                    "Legs", new List<string>()
                    {
                        "burlap.trousers", "pants.shorts"
                    }
                },
                {
                    "Feet", new List<string>()
                    {
                        "shoes.boots", "none"
                    }
                }
            };

            [JsonProperty("Weapons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Weapons = new List<string>()
            {
                "hatchet", "knife.butcher", "knife.combat", "machete", "pistol.nailgun", "salvaged.cleaver"
            };

            [JsonProperty("Skins", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<ulong>> Skins = new Dictionary<string, List<ulong>>()
            {
                ["mask.balaclava"] = new List<ulong>() { 1174080411, 811558576 },
                ["burlap.headwrap"] = new List<ulong>() { 1076584212, 811534810 },
                ["burlap.shirt"] = new List<ulong>() { 1177719024, 582568540 },
                ["tshirt"] = new List<ulong>() { 811616832, 960936268 },
                ["tshirt.long"] = new List<ulong>() { 1161735516, 810504871 },
                ["burlap.gloves"] = new List<ulong>() { 917605230, 1464134946 },
                ["burlap.trousers"] = new List<ulong>() { 1177788927, 823281717 },
                ["pants.shorts"] = new List<ulong>() { 885479497, 841150520 },
                ["shoes.boots"] = new List<ulong>() { 1428936568, 1291665415 }
            };

            [JsonProperty("Use Kits")]
            public bool UseKits = false;

            [JsonProperty("Kits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Kits = new List<string>()
            {
                "plagued-kit1", "plagued-kit2"
            };
        }

        private class Configuration
        {
            [JsonProperty("Scarecrows")]
            public PlaguedNPC Scarecrows = new PlaguedNPC();

            [JsonProperty("Scientists")]
            public PlaguedNPC Scientists = new PlaguedNPC();

            [JsonProperty("Tunnel Dwellers")]
            public PlaguedNPC TunnelDwellers = new PlaguedNPC();

            [JsonProperty("Underwater Dwellers")]
            public PlaguedNPC UnderwaterDwellers = new PlaguedNPC();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #region Handling

        [PluginReference]
        private Plugin Kits;

        private const int plaguedFlag = 0x1000000;

        private PlaguedNPC GetPlaguedNpc(NPCPlayer npc)
        {
            PlaguedNPC plaguedNpc = null;

            switch (npc.GetType().Name)
            {
                case "ScarecrowNPC":
                    if (_config.Scarecrows.Enable)
                    {
                        plaguedNpc = _config.Scarecrows;
                    }
                    break;

                case "ScientistNPC":
                    if (_config.Scientists.Enable) // TODO: Expand options for subtypes
                    {
                        plaguedNpc = _config.Scientists;
                    }
                    break;

                case "TunnelDweller":
                    if (_config.TunnelDwellers.Enable)
                    {
                        plaguedNpc = _config.TunnelDwellers;
                    }
                    break;

                case "UnderwaterDweller":
                    if (_config.UnderwaterDwellers.Enable)
                    {
                        plaguedNpc = _config.UnderwaterDwellers;
                    }
                    break;
            }

            return plaguedNpc;
        }

        private void OnEntitySpawned(NPCPlayer npc)
        {
#if DEBUG
            LogWarning($"{npc.GetType().Name} spawned");
#endif
            PlaguedNPC plaguedNpc = GetPlaguedNpc(npc);
            if (plaguedNpc != null)
            {
                if (plaguedNpc.UseKits && Kits != null && Kits.IsLoaded && plaguedNpc.Kits.Count > 0)
                {
                    GiveKit(npc, plaguedNpc.Kits[Core.Random.Range(0, plaguedNpc.Kits.Count - 1)]);
                }
                else
                {
                    ClotheNpc(npc, plaguedNpc);
                }

#if DEBUG
                LogWarning($"Original NPC max health: {npc._maxHealth}, new NPC max health: {plaguedNpc.MaxHealth}");
                LogWarning($"Original NPC health: {npc.health}, new NPC health: {plaguedNpc.Health}");
#endif
                npc.InitializeHealth(plaguedNpc.Health, plaguedNpc.MaxHealth);

                NextTick(() =>
                {
                    NPCPlayer npcSpecific = npc;
                    if (npc is ScarecrowNPC)
                    {
                        ScarecrowNPC scarecrowNpc = npc as ScarecrowNPC;
                        if (scarecrowNpc?.Brain?.Navigator != null)
                        {
#if DEBUG
                            LogWarning($"Original NPC speed: {scarecrowNpc.Brain.Navigator.Speed}, new NPC speed: {plaguedNpc.Speed}");
#endif
                            scarecrowNpc.Brain.Navigator.Speed = plaguedNpc.Speed;
                        }
                    }
                    else
                    {
                        global::HumanNPC humanNpc = npc as global::HumanNPC;
                        if (humanNpc?.Brain?.Navigator != null)
                        {
#if DEBUG
                            LogWarning($"Original NPC speed: {humanNpc.Brain.Navigator.Speed}, new NPC speed: {plaguedNpc.Speed}");
#endif
                            humanNpc.Brain.Navigator.Speed = plaguedNpc.Speed;
                        }
                    }
                });

                npc.SetPlayerFlag((BasePlayer.PlayerFlags)plaguedFlag, true);
#if DEBUG
                LogWarning($"NPC has plagued flag? {IsPlagued(npc)}");
#endif
            }
        }

        private void OnNpcTarget(NPCPlayer npc)
        {
            if (!IsPlagued(npc))
            {
                return;
            }

            PlaguedNPC plaguedNpc = GetPlaguedNpc(npc);
            if (plaguedNpc != null && plaguedNpc.SoundEffect)
            {
                Effect.server.Run($"assets/bundled/prefabs/fx/player/beartrap_scream.prefab", npc, 0u, Vector3.zero, npc.eyes.transform.forward.normalized);
            }
        }

        private void OnCorpsePopulate(NPCPlayer npc, LootableCorpse corpse)
        {
            if (!IsPlagued(npc))
            {
                return;
            }

            PlaguedNPC plaguedNpc = GetPlaguedNpc(npc);
            if (plaguedNpc != null && plaguedNpc.NoLoot)
            {
                EmptyCorpse(npc, corpse);
            }
        }

        private void OnPlayerCorpseSpawned(NPCPlayer npc, LootableCorpse corpse)
        {
            if (!IsPlagued(npc))
            {
                return;
            }

            PlaguedNPC plaguedNpc = GetPlaguedNpc(npc);
            if (plaguedNpc != null && plaguedNpc.NoLoot)
            {
                EmptyCorpse(npc, corpse);
            }
        }

        #endregion Handling

        #region Helpers

        private void ClotheNpc(NPCPlayer npc, PlaguedNPC plaguedNpc)
        {
            ItemContainer containerWear = npc.inventory.containerWear;
            containerWear.Clear();

            if (plaguedNpc.GlowingEyes)
            {
                Item glowingEyes = ItemManager.CreateByName("gloweyes");
                if (glowingEyes != null)
                {
                    glowingEyes.MoveToContainer(containerWear);
                }
            }

            foreach (KeyValuePair<string, List<string>> clothing in plaguedNpc.Attire)
            {
                Item item = GetItem(clothing.Value, plaguedNpc);
                if (item != null)
                {
#if DEBUG
                    LogWarning($"Gave {npc.name} ({npc.net.ID}) {item.info.displayName.english}");
#endif
                    item.MoveToContainer(containerWear);
                }
            }

            Item weapon = GetItem(plaguedNpc.Weapons, plaguedNpc);
            if (weapon != null)
            {
#if DEBUG
                LogWarning($"Gave {npc.name} ({npc.net.ID}) {weapon.info.displayName.english}");
#endif
                BaseProjectile projectile = weapon.GetHeldEntity() as BaseProjectile;
                if (projectile != null)
                {
                    projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
                }

                ItemContainer containerBelt = npc.inventory.containerBelt;
                containerBelt.Clear();

                weapon.MoveToContainer(containerBelt);
            }
        }

        private void EmptyCorpse(NPCPlayer npc, LootableCorpse corpse)
        {
            NextTick(() =>
            {
                for (int i = 0; i < corpse.containers.Length; i++)
                {
                    while (corpse.containers[i].itemList?.Count > 0)
                    {
                        Item item = corpse.containers[i].itemList[0];
                        item.RemoveFromContainer();
                        item.Remove(0f);
                    }
                }
            });
        }

        private Item GetItem(List<string> items, PlaguedNPC plaguedNpc)
        {
            if (items.Count < 1)
            {
                return null;
            }

            int itemIndex = Core.Random.Range(0, items.Count - 1);

            if (items[itemIndex] == "none")
            {
                return null;
            }

            List<ulong> skins;
            string chosenItem = items[itemIndex];
            bool skinsDefined = plaguedNpc.Skins.TryGetValue(chosenItem, out skins);

            Item selectedItem;
            if (skinsDefined && skins.Count > 0)
            {
                selectedItem = ItemManager.CreateByName(chosenItem, 1, skins[Core.Random.Range(0, skins.Count - 1)]);
            }
            else
            {
                selectedItem = ItemManager.CreateByName(chosenItem, 1);
            }
            return selectedItem;
        }

        private void GiveKit(NPCPlayer npc, string kitName)
        {
            npc.inventory.Strip();

            object kitResult = Kits.Call<object>("GiveKit", npc, kitName);
            if (kitResult == null || !(kitResult is bool))
            {
                LogError($"Failed to give kit {kitName} to {npc.name} ({npc.net.ID}): {kitResult}");
            }
        }

        private bool IsPlagued(NPCPlayer npc) => npc.HasPlayerFlag((BasePlayer.PlayerFlags)plaguedFlag);

        #endregion Helpers
    }
}
