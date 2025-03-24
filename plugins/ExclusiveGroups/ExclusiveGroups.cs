using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Exclusive Groups", "WhiteDragon", "0.2.8")]
    [Description("Maintains exclusive group membership within categories.")]
    internal class ExclusiveGroups : CovalencePlugin
    {
        private static ExclusiveGroups _instance;

        #region _category_

        private class Category
        {
            public class Settings : Dictionary<string, Settings.Entry>
            {
                public class Entry
                {
                    public Groups Includes;
                    public Groups Excludes;

                    public class Groups : List<string>
                    {
                        public bool Validate(string category, string type)
                        {
                            bool valid = true;

                            foreach(var group in this)
                            {
                                if(!_instance.permission.GroupExists(group) || group == "default")
                                {
                                    Log.Console(Key.CategoryGroupInvalid, new Dictionary<string, string>
                                    {
                                        { "category", category },
                                        { "group", group },
                                        { "type", type }
                                    });

                                    valid = false;
                                }
                            }

                            return valid;
                        }
                    }

                    public bool Validate(string category)
                    {
                        Configuration.Validate(ref Includes, () => new Groups());
                        Configuration.Validate(ref Excludes, () => new Groups());

                        var valid = true;

                        if(!Includes.Validate(category, nameof(Includes)))
                        {
                            valid = false;
                        }

                        if(!Excludes.Validate(category, nameof(Excludes)))
                        {
                            valid = false;
                        }

                        foreach(var group in Includes)
                        {
                            if(Excludes.Contains(group))
                            {
                                Log.Console(Key.CategoryGroupDuplicate, new Dictionary<string, string>
                                {
                                    { "category", category },
                                    { "group", group }
                                });

                                valid = false;
                            }
                        }

                        return valid;
                    }
                }

                public bool Validate()
                {
                    var included = new HashSet<string>();
                    var validate = new Queue<string>();

                    var valid = true;

                    foreach(var category in this)
                    {
                        if(category.Value == null)
                        {
                            validate.Enqueue(category.Key);

                            continue;
                        }

                        foreach(var group in category.Value.Includes)
                        {
                            if(!included.Add(group))
                            {
                                Log.Console(Key.CategoryGroupMultiple, new Dictionary<string, string>
                                {
                                    { "category", category.Key },
                                    { "group", group },
                                    { "type", nameof(Entry.Includes) }
                                });

                                valid = false;
                            }
                        }
                    }

                    while(validate.Count > 0)
                    {
                        this[validate.Dequeue()] = new Entry();

                        Configuration.SetDirty();
                    }

                    foreach(var category in this)
                    {
                        if(!category.Value.Validate(category.Key))
                        {
                            valid = false;
                        }
                    }

                    return valid;
                }
            }

            private static void Remove(IPlayer user, string category, string type, string group)
            {
                user.RemoveFromGroup(group);

                Log.Console(Key.CategoryRemove, new Dictionary<string, string>
                {
                    { "category", category },
                    { "group", group },
                    { "type", type },
                    { "userid", user.Id },
                    { "username", user.Name },
                });
            }

            public static void Update()
            {
                foreach(var user in _instance.covalence.Players.All)
                {
                    foreach(var category in config.Categories)
                    {
                        var exclude = false;

                        foreach(var group in category.Value.Includes)
                        {
                            if(user.BelongsToGroup(group))
                            {
                                if(exclude)
                                {
                                    Remove(user, category.Key, nameof(Settings.Entry.Includes), group);
                                }
                                else
                                {
                                    exclude = true;
                                }
                            }
                        }

                        if(exclude)
                        {
                            foreach(var group in category.Value.Excludes)
                            {
                                if(user.BelongsToGroup(group))
                                {
                                    Remove(user, category.Key, nameof(Settings.Entry.Excludes), group);
                                }
                            }
                        }
                    }
                }
            }
            public static void Update(string userid, string added)
            {
                var user = _instance.covalence.Players.FindPlayerById(userid);

                foreach(var category in config.Categories)
                {
                    bool exclude = category.Value.Includes.Contains(added);

                    foreach(var group in category.Value.Includes)
                    {
                        if(user.BelongsToGroup(group) && (group != added))
                        {
                            if(exclude)
                            {
                                Remove(user, category.Key, nameof(Settings.Entry.Includes), group);
                            }
                            else
                            {
                                exclude = true;
                            }
                        }
                    }

                    if(exclude)
                    {
                        foreach(var group in category.Value.Excludes)
                        {
                            if(user.BelongsToGroup(group))
                            {
                                Remove(user, category.Key, nameof(Settings.Entry.Excludes), group);
                            }
                        }
                    }
                }
            }

            public static bool Validate() => config.Categories.Validate();
        }

        #endregion _category_

        #region _configuration_

        private static Configuration config;

        private class Configuration
        {
            public Category.Settings Categories;
            public Version.Settings  Version;

            private static bool corrupt  = false;
            private static bool dirty    = false;
            private static bool upgraded = false;

            public static void Load()
            {
                dirty = false;

                try
                {
                    config = _instance.Config.ReadObject<Configuration>();

                    config.Version.Compare(0, 0, 0);
                }
                catch(NullReferenceException)
                {
                    Log.Console(Key.ConfigurationDefault);

                    dirty = true; config = new Configuration();
                }
                catch(JsonException e)
                {
                    Log.Console(Key.ConfigurationError, new Dictionary<string, string>
                    {
                        { "message", e.ToString() }
                    });

                    corrupt = true; config = new Configuration();
                }

                Validate();
            }

            public static void Save()
            {
                if(dirty && !corrupt)
                {
                    dirty = false;

                    _instance.Config.WriteObject(config);
                }
            }

            public static void SetDirty() => dirty = true;

            public static void SetUpgrade(bool upgrade = true) => upgraded = upgrade;

            public static void Unload()
            {
                Save();

                config = null;
            }

            public static bool Upgraded() => upgraded;

            public static void Validate<T>(ref T value, Func<T> initializer, Action validator = null)
            {
                if(value == null)
                {
                    dirty = true; value = initializer();
                }
                else
                {
                    validator?.Invoke();
                }
            }
            private static void Validate()
            {
                Validate(ref config.Categories, () => new Category.Settings());
                Validate(ref config.Version,    () => new Version.Settings());

                config.Version.Validate();
            }
        }

        #endregion _conifguration_

        #region _hooks_

        private void Init()
        {
            Unsubscribe(nameof(OnUserGroupAdded));

            _instance = this;

            Text.Preload();

            Configuration.Load();

            Text.Load();

            if(!Category.Validate())
            {
                throw new Exception("Configuration error.");
            }

            Configuration.Save();
        }

        protected override void LoadDefaultConfig() { }

        protected override void LoadDefaultMessages() { }

        private void Loaded()
        {
            Category.Update();

            Subscribe(nameof(OnUserGroupAdded));
        }

        private void OnUserGroupAdded(string userid, string group)
        {
            if(group != "default")
            {
                Category.Update(userid, group);
            }
        }

        private void Unload()
        {
            Text.Unload();

            Configuration.Unload();

            _instance = null;
        }

        #endregion _hooks_

        #region _log_

        private new class Log
        {
            public static void Console(Key key, Dictionary<string, string> parameters = null)
            {
                _instance.Puts(Text.Get(key, parameters));
            }
        }

        #endregion _log_

        #region _text_

        private enum Key
        {
            CategoryGroupDuplicate,
            CategoryGroupInvalid,
            CategoryGroupMultiple,
            CategoryRemove,
            ConfigurationDefault,
            ConfigurationError,
            TextKeyInvalid,
        }

        private class Text
        {
            private static readonly Dictionary<Key, string> cache = new Dictionary<Key, string>();

            private static Dictionary<string, Dictionary<string, string>> messages;

            public static string Get(Key key, Dictionary<string, string> parameters = null)
            {
                string message;

                if(cache.TryGetValue(key, out message))
                {
                    return Replace(message, parameters);
                }
                else
                {
                    var language = _instance.lang.GetServerLanguage();

                    if(string.IsNullOrEmpty(language) || !messages.ContainsKey(language))
                    {
                        language = "en";
                    }

                    Dictionary<string, string> cache;

                    if(messages.TryGetValue(language, out cache) && cache.TryGetValue(Enum.GetName(typeof(Key), key), out message))
                    {
                        return Replace(message, parameters);
                    }
                }

                return Enum.GetName(typeof(Key), key);
            }

            public static void Load()
            {
                var languages = _instance.lang.GetLanguages(_instance);

                if((languages.Length == 0) || Configuration.Upgraded())
                {
                    RegisterMessages();
                }

                var language = _instance.lang.GetServerLanguage();

                if(string.IsNullOrEmpty(language) || !languages.Contains(language))
                {
                    language = "en";
                }

                var keys = new HashSet<string>(Enum.GetNames(typeof(Key)));

                foreach(var entry in _instance.lang.GetMessages(language, _instance))
                {
                    if(string.IsNullOrEmpty(entry.Key))
                    {
                        continue;
                    }

                    if(keys.Contains(entry.Key))
                    {
                        cache.Add((Key)Enum.Parse(typeof(Key), entry.Key), entry.Value);
                    }
                    else
                    {
                        Log.Console(Key.TextKeyInvalid, new Dictionary<string, string>
                        {
                            { "key", entry.Key },
                            { "language", language }
                        });
                    }
                }
            }

            public static void Preload()
            {
                messages = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "en", new Dictionary<string, string>
                        {
                            { nameof(Key.CategoryGroupDuplicate), "Category[\"{category}\"]: Duplicate group \"{group}\"." },
                            { nameof(Key.CategoryGroupInvalid),   "Category[\"{category}\"].{type}: Invalid group \"{group}\"." },
                            { nameof(Key.CategoryGroupMultiple),  "Category[\"{category}\"].{type}: Multiple categories include group \"{group}\"." },
                            { nameof(Key.CategoryRemove),         "Category[\"{category}\"].{type}: Removed {username}[{userid}] from group \"{group}\"." },
                            { nameof(Key.ConfigurationDefault),   "Configuration: Created new configuration with default settings." },
                            { nameof(Key.ConfigurationError),     "Configuration: Using default settings. Delete the configuration file, or fix the following error, and reload; {message}" },
                            { nameof(Key.TextKeyInvalid),         "Language[\"{language}\"]: Invalid key \"{key}\"." },
                        }
                    }
                };
            }

            private static void RegisterMessages()
            {
                foreach(var langauge in messages)
                {
                    _instance.lang.RegisterMessages(langauge.Value, _instance, langauge.Key);
                }
            }

            private static string Replace(string message, Dictionary<string, string> parameters = null)
            {
                if((message != null) && (parameters != null))
                {
                    foreach(var entry in parameters)
                    {
                        message = message.Replace('{' + entry.Key + '}', entry.Value);
                    }
                }

                return message;
            }

            public static void Unload()
            {
                cache.Clear();

                foreach(var language in messages)
                {
                    language.Value.Clear();
                }

                messages.Clear();
                messages = null;
            }
        }

        #endregion _text_

        #region _version_

        private new class Version
        {
            public class Settings
            {
                public int Major;
                public int Minor;
                public int Patch;

                public Settings()
                {
                    Major = Minor = Patch = 0;
                }

                public int Compare(int major, int minor, int patch)
                {
                    return
                        (Major != major) ? (Major - major) :
                        (Minor != minor) ? (Minor - minor) :
                        (Patch != patch) ? (Patch - patch) : 0;
                }

                public void Validate()
                {
                    var current = (_instance as CovalencePlugin).Version;

                    if(Compare(current.Major, current.Minor, current.Patch) < 0)
                    {
                        Configuration.SetDirty();

                        Major = current.Major;
                        Minor = current.Minor;
                        Patch = current.Patch;

                        Configuration.SetUpgrade();
                    }
                    else
                    {
                        Configuration.SetUpgrade(false);
                    }
                }
            }
        }

        #endregion _version_
    }
}