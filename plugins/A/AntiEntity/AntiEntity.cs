using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Anti Entity", "birthdates", "1.0.1")]
    [Description("Deny certain entities from spawning")]
    public class AntiEntity : RustPlugin
    {
        #region Hooks
        private void Init()
        {
            LoadConfig();
        }

        void OnServerInitialized()
        {
            CleanupNonPrefabs();
            CleanupExisting();
        }

        void CleanupNonPrefabs()
        {
            for (var z = 0; z < _config.noSpawn.Count; z++)
            {
                var e = _config.noSpawn[z];
                if (!IsPrefab(e))
                {
                    PrintError(e + " is not a valid prefab! Removing...");
                    _config.noSpawn.Remove(e);
                }
            }
            SaveConfig();
        }

        bool IsPrefab(string pref)
        {
            if (!StringPool.toNumber.ContainsKey(pref))
            {
                return false;
            }
            return true;
        }

        void CleanupExisting()
        {
            var entities = BaseNetworkable.serverEntities.ToList().Where(x => _config.noSpawn.Contains(x.PrefabName)).ToList();
            for (var z = 0; z < entities.Count; z++)
            {
                var x = entities[z];
                if (!x.IsDestroyed) x.Kill();
            }
        }

        void OnEntitySpawned(BaseNetworkable x)
        {
            if (!(x is BaseEntity)) return;
            if (!_config.noSpawn.Contains(x.PrefabName)) return;
            if (!x.IsDestroyed)
            {
                //Timer for anything wanting this as a parent
                timer.In(0.05f, () => x.Kill());
            }
        }


        [ConsoleCommand("antientity")]
        void ConsoleCMD(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            var Id = arg.Connection?.userid.ToString();
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith(lang.GetMessage("InvalidArgs", this, Id));
                return;
            }
            var args = arg.Args;
            var pref = args[1];
            switch (args[0].ToLower())
            {
                case "add":
                    if (!IsPrefab(pref))
                    {
                        arg.ReplyWith(lang.GetMessage("NotAPrefab", this, Id));
                        return;
                    }
                    _config.noSpawn.Add(pref);
                    SaveConfig();
                    arg.ReplyWith(string.Format(lang.GetMessage("SuccessAdd", this, Id), pref));
                    break;
                case "remove":
                    if (!_config.noSpawn.Contains(pref))
                    {
                        arg.ReplyWith(lang.GetMessage("DoesntExist", this, Id));
                        break;
                    }
                    _config.noSpawn.Remove(pref);
                    SaveConfig();
                    arg.ReplyWith(string.Format(lang.GetMessage("SuccessRemove", this, Id), pref));
                    break;
                default:
                    arg.ReplyWith(lang.GetMessage("InvalidArgs", this, Id));
                    break;
            }
        }
        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"InvalidArgs", "antientity remove|add <prefab>"},
                {"SuccessRemove", "You have successfully removed the prefab {0}"},
                {"SuccessAdd", "You have successfully added the prefab {0}"},
                {"DoesntExist", "That prefab is not in the list!"},
                {"NotAPrefab", "That is not a valid prefab."}
            }, this);
        }

        public class ConfigFile
        {
            [JsonProperty("Entities that are denied from spawning (cant spawn, long prefab names accepted)")]
            public List<string> noSpawn;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    noSpawn = new List<string>
                    {
                        "assets/content/vehicles/boats/rowboat/rowboat.prefab"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
            CleanupExisting();
        }
        #endregion 
    }
}
//Generated with birthdates' Plugin Maker
