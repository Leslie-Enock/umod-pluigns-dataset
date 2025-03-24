using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Noob Group", "Wulf", "2.0.0")]
    [Description("Adds new players to a temporary group, and permanent group on return")]
    public class NoobGroup : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        private class CustomGroup
        {
            public string Name;
            public string Title;
            public int Rank;

            public CustomGroup(string name, string title = "", int rank = 0)
            {
                Name = name;
                Title = string.IsNullOrEmpty(title) ? name.Humanize() : title;
                Rank = rank;
            }
        }

        private class Configuration
        {
            [JsonProperty("Noob group")]
            public CustomGroup NoobGroup = new CustomGroup("noob", "Newbie");

            [JsonProperty("Returning group")]
            public CustomGroup ReturningGroup = new CustomGroup("returning", "Returning");

#if HURTWORLD || RUST
            [JsonProperty("Reset on new save/map wipe")]
            public bool ResetOnWipe = false;
#endif

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
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
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandReset"] = "noobreset",
                ["GroupsReset"] = "All groups have been reset",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permReset = "noobgroup.reset";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandReset));

            permission.RegisterPermission(permReset, this);
            GroupSetup();

            // TODO: Remove old group cleanup eventually
            if (!config.ReturningGroup.Name.Equals("returningplayers", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string playerId in permission.GetUsersInGroup("returningplayers"))
                {
                    permission.AddUserGroup(CleanId(playerId), config.ReturningGroup.Name);
                    permission.RemoveUserGroup(CleanId(playerId), "returningplayers");
                }
            }

            // TODO: Remove old permission cleanup eventually
            foreach (string playerId in permission.GetPermissionUsers("noobgroup.return"))
            {
                permission.RevokeUserPermission(CleanId(playerId), "noobgroup.return");
            }

#if HURTWORLD || RUST
            if (!config.ResetOnWipe)
            {
                Unsubscribe(nameof(OnNewSave));
            }
#endif
        }

        #endregion Initialization

        private void OnUserConnected(IPlayer player)
        {
            if (!player.BelongsToGroup(config.NoobGroup.Name) && !player.BelongsToGroup(config.ReturningGroup.Name))
            {
                GroupSetup();
                permission.AddUserGroup(player.Id, config.NoobGroup.Name);
            }
            else if (player.BelongsToGroup(config.NoobGroup.Name) && !player.BelongsToGroup(config.ReturningGroup.Name))
            {
                GroupSetup();
                permission.AddUserGroup(player.Id, config.ReturningGroup.Name);
                permission.RemoveUserGroup(player.Id, config.NoobGroup.Name);
            }
        }

        #region Reset Handling

        private void GroupSetup(bool reset = false)
        {
            if (reset)
            {
                if (permission.RemoveGroup(config.NoobGroup.Name))
                {
                    Log($"Group '{config.NoobGroup} removed for reset");
                }
                if (permission.RemoveGroup(config.ReturningGroup.Name))
                {
                    Log($"Group '{config.ReturningGroup} removed for reset");
                }
            }
            if (permission.CreateGroup(config.NoobGroup.Name, config.NoobGroup.Title, config.NoobGroup.Rank))
            {
                Log($"Group '{config.NoobGroup} did not exist, created");
            }
            if (permission.CreateGroup(config.ReturningGroup.Name, config.ReturningGroup.Title, config.ReturningGroup.Rank))
            {
                Log($"Group '{config.ReturningGroup} did not exist, created");
            }
        }

        private void CommandReset(IPlayer player, string command)
        {
            if (!player.HasPermission(permReset))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            GroupSetup(true);
            Message(player, "GroupsReset");
        }

#if HURTWORLD || RUST
        private void OnNewSave() => GroupSetup(true);
#endif

        #endregion Reset Handling

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

        private string CleanId(string playerId) => Regex.Replace(playerId, "[^0-9]", "");

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
