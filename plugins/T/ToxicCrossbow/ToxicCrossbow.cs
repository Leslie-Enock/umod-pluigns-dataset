using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Toxic Crossbow", "DezLife", "0.0.8")]
    [Description("A crossbow that has the ability to fire one charge of a radiation arrow!")]
    class ToxicCrossbow : CovalencePlugin
    {
        #region Var
        ItemDefinition itemAmmo = null;
        #endregion

        #region Config
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("SkinID for the item")]
            public ulong SkinID;
            [JsonProperty("DisplayName for the item")]
            public string DisplayName;
            [JsonProperty("The amount of radiation will be given every 3 seconds")]
            public float Radiations;
            [JsonProperty("Radius of radiation")]
            public float RadiationsRadius;
            [JsonProperty("Exposure time in seconds")]
            public int RadiationsTime;
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                SkinID = 1956876693,
                DisplayName = "Toxic crossbow",
                Radiations = 10,
                RadiationsRadius = 30,
                RadiationsTime = 30
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion  

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerNot"] = "Player not found!",
                ["TheGiveSucc"] = "The player was successfully issued {0}!",
                ["YouGiveSucc"] = "You have successfully received {0}",
                ["NoPerm"] = "You are not authorized to use this command.",
                ["Invalidsyntax"] = "Use toxiccrossbow.give <steamid or name>",
            }, this);
        }
        #endregion

        #region Hook
        private void OnServerInitialized()
        {
            itemAmmo = ItemManager.FindItemDefinition("arrow.fire");
        }
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null)
                return;

            if (info.Weapon?.skinID == config.SkinID && info.damageProperties?.name == "Damage.Arrow")
            {
                Item item = info.Weapon.GetItem();
                item.skin = 0;
                item.name = "Crossbow";

                var pos = info.HitPositionOnRay();
                timer.Repeat(1, config.RadiationsTime, () =>
                {
                    List<BasePlayer> players = Facepunch.Pool.GetList<BasePlayer>();                  
                    Vis.Entities(pos, config.RadiationsRadius, players);
                    for (int i = 0; i < players.Count; i++)
                        players[i].metabolism.radiation_poison.value += config.Radiations;
                    Facepunch.Pool.FreeList(ref players);
                });
            }
        }
        #endregion

        #region Metods
        private void CreateItem(BasePlayer target)
        {
            Item item = ItemManager.CreateByName("crossbow", 1, config.SkinID);
            item.name = config.DisplayName;
            item.condition = 0.1f;
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null && itemAmmo != null)
            {
                weapon.primaryMagazine.ammoType = itemAmmo;
                weapon.primaryMagazine.contents = 1;
            }
            target.GiveItem(item);
        }

        #endregion

        #region Commands

        [Command("toxiccrossbow.give")]
        private void ConsoleCommandBlade(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(lang.GetMessage("Invalidsyntax", this, player.Id));
                return;
            }
            var targets = players.FindPlayer(args[0])?.Object as BasePlayer;
            if (targets == null)
            {
                player.Reply(lang.GetMessage("PlayerNot", this, player.Id));
                return;
            }
            CreateItem(targets);
            player.Reply(string.Format(lang.GetMessage("TheGiveSucc", this, player.Id), config.DisplayName));
            targets.ChatMessage(string.Format(lang.GetMessage("YouGiveSucc", this, targets.UserIDString), config.DisplayName));
        }
        #endregion
    }
}