using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{ 
    [Info("PrivilegeDeploy", "k1lly0u", "0.1.7")]
    [Description("Choose which deployable items require building privilege to deploy")]
    class PrivilegeDeploy : RustPlugin
    {
        private readonly Hash<string, ItemDefinition> prefabToItem = new Hash<string, ItemDefinition>();
        private readonly Hash<string, List<ItemAmount>> constructionToIngredients = new Hash<string, List<ItemAmount>>();

        #region Hooks
        private void Loaded() => Unsubscribe(nameof(OnEntitySpawned));

        private void OnServerInitialized()
        {
            LoadVariables();
            InitValidList();

            Subscribe(nameof(OnEntitySpawned));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["blocked"] = "You can not build this outside of a building privileged area!"
            }, this);
        }

        private void OnEntitySpawned(BaseEntity baseEntity)
        {
            if (baseEntity is ResourceEntity || baseEntity is BaseNpc || baseEntity is LootContainer)
                return;

            if (configData.Deployables.Contains(baseEntity.ShortPrefabName) || configData.Deployables.Contains(baseEntity.PrefabName))
            {
                if (!baseEntity.OwnerID.IsSteamId())
                    return;

                BasePlayer player = BasePlayer.FindByID(baseEntity.OwnerID);
                if (player == null || player.IsAdmin || player.IsBuildingAuthed())
                    return;

                List<ItemAmount> items = Facepunch.Pool.GetList<ItemAmount>();

                if (baseEntity is BuildingBlock && constructionToIngredients.ContainsKey(baseEntity.PrefabName))                
                    items.AddRange(constructionToIngredients[baseEntity.PrefabName]);                   
                
                else if (prefabToItem.ContainsKey(baseEntity.PrefabName))
                    items.Add(new ItemAmount(prefabToItem[baseEntity.PrefabName], 1));
                         
                NextTick(() =>
                {
                    if (baseEntity == null || baseEntity.IsDestroyed)
                    {
                        Facepunch.Pool.FreeList(ref items);
                        return;
                    }
                                        
                    (baseEntity as StorageContainer)?.inventory?.Clear();

                    if (baseEntity is BaseTrap || !(baseEntity is BaseCombatEntity))
                        baseEntity.Kill();
                    else (baseEntity as BaseCombatEntity).DieInstantly();

                    if (player != null && player.IsConnected)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            ItemAmount itemAmount = items[i];

                            Item item = ItemManager.Create(itemAmount.itemDef, (int)itemAmount.amount);

                            ItemModDeployable deployable = item.info.GetComponent<ItemModDeployable>();
                            if (deployable != null)
                            {
                                BaseOven oven = deployable.entityPrefab.Get()?.GetComponent<BaseOven>();
                                if (oven != null)
                                    oven.startupContents = System.Array.Empty<ItemAmount>();
                            }

                            player.inventory.GiveItem(item, player.inventory.containerBelt);
                            player.Command("note.inv", new object[] { item.info.itemid, 1, item.name, 0 });
                        }

                        player.ChatMessage(lang.GetMessage("blocked", this, player.UserIDString));
                    }

                    Facepunch.Pool.FreeList(ref items);
                });                
            }
        }
        #endregion

        #region Prefab to Item links
        private void InitValidList()
        {
            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                ItemModDeployable deployable = itemDefinition?.GetComponent<ItemModDeployable>();
                if (deployable == null)
                    continue;
                
                if (!prefabToItem.ContainsKey(deployable.entityPrefab.resourcePath))                
                    prefabToItem.Add(deployable.entityPrefab.resourcePath, itemDefinition);                
            }

            foreach (Construction construction in GetAllPrefabs<Construction>())
            {
                if (construction.deployable == null && !string.IsNullOrEmpty(construction.fullName))
                {
                    if (!constructionToIngredients.ContainsKey(construction.fullName))                    
                        constructionToIngredients.Add(construction.fullName, construction.defaultGrade.costToBuild);
                }
            }
        }

        private T[] GetAllPrefabs<T>()
        {
            Dictionary<uint, PrefabAttribute.AttributeCollection> prefabs = PrefabAttribute.server.prefabs;
            if (prefabs == null)
                return new T[0];

            List<T> results = new List<T>();
            foreach (PrefabAttribute.AttributeCollection prefab in prefabs.Values)
            {
                T[] arrayCache = prefab.Find<T>();
                if (arrayCache == null || !arrayCache.Any())
                    continue;

                results.AddRange(arrayCache);
            }

            return results.ToArray();
        }
        #endregion

        #region Config
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "deployables")]
            public List<string> Deployables { get; set; }
        }

        private void LoadVariables()
        {           
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                Deployables = new List<string>
                {
                    "barricade.concrete",
                    "barricade.metal",
                    "barricade.sandbags",
                    "barricade.stone",
                    "barricade.wood",
                    "barricade.woodwire",
                    "campfire",
                    "gates.external.high.stone",
                    "gates.external.high.wood",
                    "wall.external.high",
                    "wall.external.high.stone",
                    "landmine",
                    "beartrap"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}

