using System;
using System.IO;
using Oxide.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Weapon Names", "klauz24", "1.2.2"), Description("Allows to change your active weapon name.")]
    internal class WeaponNames : CovalencePlugin
    {
        private Configuration _config;

        private const string _perm = "weaponnames.use";

        private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();

        private class Configuration
        {
            [JsonProperty(PropertyName = "New tag digits limit")]
            public int NewTagDigitsLimit { get; set; } = 12;

            [JsonProperty(PropertyName = "Cooldown (in minutes)")]
            public double Cooldown { get; set; } = 1.0;
        }

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
            }
            catch
            {
                string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}";
                PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}.json");
                Config.WriteObject(_config, false, $"{configPath}_invalid.json");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPerm", "You got no permission to use this command."},
                {"Syntax",  "/ct <name tag>"},
                {"Cooldown", "You must wait <color=#cacbd3>{0}</color> before using this command again."},
                {"NoComponent", "This item does not have ItemComponentNameOverride component."},
                {"DigitsLimit", "New name tag can not have more than <color=#cacbd3>{0}</color> digits."},
                {"CanNot", "This item name cannot be changed."},
                {"Changed", "Weapon name tag has been changed to: <color=#cacbd3>{0}</color>."}
            }, this);
        }

        [Command("ct")]
        private void ChatCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.Id, _perm))
            {
                if (args.Length != 0)
                {
                    if (_cooldowns.ContainsKey(player.Id))
                    {
                        var time = DateTime.Compare(_cooldowns[player.Id], DateTime.Now);
                        if (time > 0)
                        {
                            var timeSpan = _cooldowns[player.Id].Subtract(DateTime.Now);
                            player.Message(string.Format(lang.GetMessage("Cooldown", this, player.Id), string.Format("{0:00}:{1:00}:{2:00}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds)));
                            return;
                        }
                        _cooldowns.Remove(player.Id);
                    }
                    var newName = string.Join(" ", args);
                    if (newName.Length <= _config.NewTagDigitsLimit)
                    {
#if HURTWORLD
                        HandleHurtworld(player, newName);
#endif
#if RUST
						HandleRust(player, newName);
#endif
                    }
                    else
                    {
                        player.Message(string.Format(lang.GetMessage("DigitsLimit", this, player.Id), _config.NewTagDigitsLimit));
                    }
                }
                else
                {
                    player.Message(lang.GetMessage("Syntax", this, player.Id));
                }
            }
            else
            {
                player.Message(lang.GetMessage("NoPerm", this, player.Id));
            }
        }

        private void Init() => permission.RegisterPermission(_perm, this);

#if HURTWORLD
        private void HandleHurtworld(IPlayer player, string newName)
        {
            var item = (player.Object as PlayerSession)?.WorldPlayerEntity.GetComponent<EquippedHandlerBase>().GetEquippedItem();
            if (item != null)
            {
                var component = item.GetComponent<ItemComponentNameOverride>();
                if (component != null)
                {
                    component.NameKey = newName;
                    item.InvalidateBaseline();
                    player.Message(string.Format(lang.GetMessage("Changed", this, player.Id), newName));
                    _cooldowns.Add(player.Id, DateTime.Now.AddMinutes(_config.Cooldown));
                }
                else
                {
                    player.Message(lang.GetMessage("NoComponent", this, player.Id));
                }
            }
            else
            {
                player.Message(lang.GetMessage("CanNot", this, player.Id));
            }
        }
#elif RUST
        private void HandleRust(IPlayer player, string newName)
        {
            var item = (player.Object as BasePlayer)?.GetActiveItem();
            if (item != null)
            {
				item.name = newName;
				item.MarkDirty();
				player.Message(string.Format(lang.GetMessage("Changed", this, player.Id), newName));
				_cooldowns.Add(player.Id, DateTime.Now.AddMinutes(_config.Cooldown));
            }
            else
            {
                player.Message(lang.GetMessage("CanNot", this, player.Id));
            }
        }
#endif
    }
}