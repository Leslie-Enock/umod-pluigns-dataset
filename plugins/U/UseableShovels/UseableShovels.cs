using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Useable Shovels", "mr01sam", "1.0.3")]
    [Description("Allows you to dig into the ground using shovels!")]
    partial class UseableShovels : CovalencePlugin
    {
    
        private const string PERMISSION_USE = "useableshovels.use";

        void Init()
        {
            Unsubscribe(nameof(OnMeleeAttack));
            permission.RegisterPermission(PERMISSION_USE, this);
        }

        void Unload()
        {
            _monumentInfoCache = null;
        }

        void OnServerInitialized()
        {
            Subscribe(nameof(OnMeleeAttack));
        }
    }
}

namespace Oxide.Plugins
{
    partial class UseableShovels : CovalencePlugin
    {
        private Configuration config;

        private partial class Configuration
        {

            [JsonProperty(PropertyName = "Gain Stone")]
            public bool GainStone = true;

            [JsonProperty(PropertyName = "Gain Metal/Sulfur")]
            public bool GainMetalAndSulfur = true;

            [JsonProperty(PropertyName = "Gain Bait")]
            public bool GainBait = true;

            [JsonProperty(PropertyName = "Gain Plant Fiber")]
            public bool GainPlantFiber = true;

            [JsonProperty(PropertyName = "Reveal Player Stashes")]
            public bool RevealStashes = true;

            [JsonProperty(PropertyName = "Allow Digging in Monuments")]
            public bool AllowDiggingMonuments = false;

            [JsonProperty(PropertyName = "Allow Digging in Building Privileges")]
            public bool AllowDiggingBuildingPrivilege = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();
    }
}

namespace Oxide.Plugins
{
    partial class UseableShovels : CovalencePlugin
    {
        private readonly string STONE_ITEM = "stones";
        private readonly string SULPHUR_ITEM = "sulfur.ore";
        private readonly string METAL_ITEM = "metal.ore";
        private readonly string GRUB_ITEM = "grub";
        private readonly string WORM_ITEM = "worm";
        private readonly string PLANT_FIBER_ITEM = "plantfiber";

        private static MonumentInfo[] _monumentInfoCache;
        public static MonumentInfo[] MonumentInfoCached
        {
            get
            {
                if (_monumentInfoCache == null)
                {
                    _monumentInfoCache = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
                }
                return _monumentInfoCache;
            }
        }

        enum LocationType
        {
            Generic,
            Monument,
            BuildingPrivilege
        }

        LocationType GetLocationTypeFromPosition(BasePlayer basePlayer, Vector3 position)
        {
            bool inBuildingBlockedZone = basePlayer != null && basePlayer.GetBuildingPrivilege() != null;
            if (inBuildingBlockedZone)
            {
                return LocationType.BuildingPrivilege;
            }
            bool inMonument = MonumentInfoCached.Any(x => x.IsInBounds(position));
            if (inMonument)
            {
                return LocationType.Monument;
            }
            return LocationType.Generic;
        }

        ItemChance[] GetItemsFromMaterial(HitMaterial material)
        {
            switch (material)
            {
                case HitMaterial.Grass:
                    return new ItemChance[]
                    {
                        new ItemChance(GRUB_ITEM, 5, config.GainBait),
                        new ItemChance(PLANT_FIBER_ITEM, 40, config.GainPlantFiber)
                    };
                case HitMaterial.SnowGrass:
                    return new ItemChance[]
                    {
                        new ItemChance(PLANT_FIBER_ITEM, 10, config.GainPlantFiber)
                    };
                case HitMaterial.Sand:
                    return new ItemChance[]
                    {
                        new ItemChance(SULPHUR_ITEM, 10, config.GainMetalAndSulfur)
                    };
                case HitMaterial.Dirt:
                    return new ItemChance[]
                    {
                        new ItemChance(WORM_ITEM, 5, config.GainBait),
                    };
                case HitMaterial.Riverbed:
                    return new ItemChance[]
                    {
                        new ItemChance(METAL_ITEM, 10, config.GainMetalAndSulfur),
                        new ItemChance(GRUB_ITEM, 2, config.GainBait),
                    };
                case HitMaterial.Gravel:
                    return new ItemChance[]
                    {
                        new ItemChance(METAL_ITEM, 30, config.GainMetalAndSulfur),
                    };
                default:
                    return new ItemChance[0];
            }
        }

        enum HitMaterial
        {
            Undiggable,
            Grass,
            Gravel,
            Riverbed,
            SnowGrass,
            Sand,
            Dirt
        }

        HitMaterial GetHitMaterialFromID(uint material)
        {
            switch (material)
            {
                case 2306822461: return HitMaterial.Gravel;
                case 1109271974: return HitMaterial.Riverbed;
                case 3829453833: return HitMaterial.Grass;
                case 3620698611: return HitMaterial.Grass;
                case 3757806379: return HitMaterial.SnowGrass;
                case 1533752200: return HitMaterial.Sand;
                case 2551253961: return HitMaterial.Dirt;
            }
            return HitMaterial.Undiggable;
        }

        object OnMeleeAttack(BasePlayer basePlayer, HitInfo info)
        {
            object defaultReturn = null;
            if (info != null && info.Weapon != null && info.Weapon.ShortPrefabName == "paddle.entity")
            {
                var localPosition = info.HitPositionLocal;
                var worldPosition = info.HitPositionWorld;
                var locationType = GetLocationTypeFromPosition(basePlayer, worldPosition);
                var hitMaterial = GetHitMaterialFromID(info.HitMaterial);
                if (hitMaterial == HitMaterial.Undiggable)
                {
                    return defaultReturn;
                }
                if (!permission.UserHasPermission(basePlayer.UserIDString, PERMISSION_USE) || (locationType == LocationType.Monument && !config.AllowDiggingMonuments) || (locationType == LocationType.BuildingPrivilege && !config.AllowDiggingBuildingPrivilege))
                {
                    basePlayer.IPlayer.Reply(Lang("cannot dig here", basePlayer));
                    return defaultReturn;
                }
                DigHoleAndGetStuff(basePlayer, hitMaterial, localPosition, worldPosition);
            }
            return defaultReturn;
        }

        void DigHoleAndGetStuff(BasePlayer basePlayer, HitMaterial hitMaterial, Vector3 localPosition, Vector3 worldPosition)
        {
            int roll;
            int amt;
            var tool = (Paddle)basePlayer.GetActiveItem().GetHeldEntity();
            var gatherRate = (tool.damageTypes.First(x => x.type == Rust.DamageType.Blunt).amount / 40f);
            if (config.GainStone)
            {
                roll = UnityEngine.Random.Range(1, 10);
                amt = roll >= 9 ? 3 : (roll >= 4 ? 2 : 1);
                GiveItemByShortName(basePlayer, STONE_ITEM, RoundGatherAmount(amt, gatherRate));
            }
            var chances = GetItemsFromMaterial(hitMaterial);
            foreach (var itemChance in chances)
            {
                roll = UnityEngine.Random.Range(1, 100);
                amt = roll >= 100 - itemChance.Chance ? 1 : 0;
                GiveItemByShortName(basePlayer, itemChance.ItemShortName, RoundGatherAmount(amt, gatherRate));
            }
            bool didRevealStash = false;
            if (config.RevealStashes)
            {
                didRevealStash = RevealPlayerStashes(worldPosition) > 0;
            }
            if (!didRevealStash)
            {
                PlayDigEffect(basePlayer, localPosition);
            }
            Interface.CallHook("OnDig", basePlayer, localPosition, worldPosition);
        }

        int RevealPlayerStashes(Vector3 position)
        {
            var stashes = GetNearbyStashes(position, 1f);
            int revealed = 0;
            foreach (var stash in stashes)
            {
                if (stash.IsHidden())
                {
                    stash.ToggleHidden();
                    revealed++;
                }
            }
            return revealed;
        }

        List<StashContainer> GetNearbyStashes(Vector3 position, float radius)
        {
            List<StashContainer> stashes = new List<StashContainer>();
            Vis.Entities(position, radius, stashes);
            return stashes;
        }

        int RoundGatherAmount(int amt, float gatherRate)
        {
            return amt <= 0 || gatherRate <= 0 ? 0 : (int)Math.Max(1, Math.Floor(amt * gatherRate));
        }

        void PlayDigEffect(BaseEntity entity, Vector3 position)
        {
            var effect = new Effect("assets/bundled/prefabs/fx/dig_effect.prefab", position, Vector3.one);
            EffectNetwork.Send(effect);
        }

        void GiveItemByShortName(BasePlayer basePlayer, string name, int amount)
        {
            if (amount > 0)
            {
                var item = ItemManager.CreateByName(name, amount);
                basePlayer.GiveItem(item);
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class UseableShovels : CovalencePlugin
    {
        public class ItemChance
        {
            public string ItemShortName { get; set; }

            public int Chance { get; set; }

            public bool Enabled { get; set; }

            public ItemChance(string itemShortName, int chance, bool enabled)
            {
                this.ItemShortName = itemShortName;
                this.Chance = chance;
                this.Enabled = enabled;
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class UseableShovels : CovalencePlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["cannot dig here"] = "You cannot dig in this area."
            }, this);
        }

        private string Lang(string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, this, basePlayer?.UserIDString), args);
    }
}