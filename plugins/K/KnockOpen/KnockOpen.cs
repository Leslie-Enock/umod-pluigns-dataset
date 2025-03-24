using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Knock Open", "OfficerJAKE", "2.5.8")]
    [Description("Opens security doors when knocked on by players and holding the correct card")]
    internal class KnockOpen : RustPlugin
    {

        public static string eff = string.Empty;
        public static Vector3 effTarget = Vector3.zero;
        public static string cardColor = string.Empty;
        public static string doorColor = string.Empty;
        public static string logMsg = string.Empty;

        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings = new Settings();

            [JsonProperty(PropertyName = "Tuners")]
            public Tuners tuners = new Tuners();

            public class Settings
            {
                [JsonProperty(PropertyName = "Post To Chat")]
                public bool PostToChat = true;

                [JsonProperty(PropertyName = "Damage Keycards")]
                public bool DamageKeycards = true;

                [JsonProperty(PropertyName = "Use Effects")]
                public bool UseEffects = true;

                [JsonProperty(PropertyName = "Close Doors")]
                public bool CloseDoors = true;

                [JsonProperty(PropertyName = "Hurt On Shock")]
                public bool HurtOnShock = false;
            }

            public class Tuners
            {
                [JsonProperty(PropertyName = "Close Door Delay")]
                public float CloseDoorDelay = 15.0F;

                [JsonProperty(PropertyName = "Damage To Deal")]
                public float DamageToDeal = 1.0F;

                [JsonProperty(PropertyName = "Hurt Amount")]
                public float HurtAmount = 5.0F;
				
                [JsonProperty(PropertyName = "Effect On Success")]
                public string EffectSuccess = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";

                [JsonProperty(PropertyName = "Effect On Failure")]
                public string EffectFailure = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
            }
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
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SUCCESS"] = "You opened the door by knocking and waving your card",
                ["FAILURE"] = "You need to be holding a keycard in your hands when you knock",
                ["WRONG_CARD"] = "It appears you have a {0} card, but need a {1} one",
                ["ITEM_BROKE"] = "Your {0} card just broke, so we threw it away",
            }, this);
        }
		
        void OnDoorKnocked(Door door, BasePlayer player)
        {
			
            if (!door.isSecurityDoor || player == null) return;
			
            switch (door.PrefabName)
            {
                case "assets/bundled/prefabs/static/door.hinged.security.green.prefab":
                    {
                        doorColor = "green";
                        break;
                    }
                case "assets/bundled/prefabs/static/door.hinged.security.blue.prefab":
                    {
                        doorColor = "blue";
                        break;
                    }
                case "assets/bundled/prefabs/static/door.hinged.security.red.prefab":
                    {
                        doorColor = "red";
                        break;
                    }
                default: return;
            }

            HeldEntity heldEnt = player.GetHeldEntity() as HeldEntity;
            if (heldEnt == null)
            {
                Logging(player, Lang("FAILURE"));
                return;
            }

            Item card = (Item)player.GetActiveItem();
            if (card == null) return;

            switch (heldEnt.GetOwnerItemDefinition().shortname)
            {
                case "keycard_green":
                    {
                        cardColor = "green";
                        break;
                    }
                case "keycard_blue":
                    {
                        cardColor = "blue";
                        break;
                    }
                case "keycard_red":
                    {
                        cardColor = "red";
                        break;
                    }
                default:
                    {
                        Logging(player, Lang("FAILURE"));
                        return;
                    }
            }
			
			//player holding card in hand - toggle door
            ToggleDoor(player, door, eff, doorColor, cardColor, card);
        }

        private void ToggleDoor(BasePlayer player, Door door, string eff, string doorColor, string cardColor, Item card)
        {

            if (doorColor.ToLower() != cardColor.ToLower())
            {

                DoEffect(player, door, card, "SHOCK");
                if (config.settings.HurtOnShock)
                {
                    DoEffect(player, door, card, "FAKE_HURT");
                    player.Hurt(config.tuners.HurtAmount);
                }
				
                DoEffect(player, door, card, "FAILURE");
                Logging(player, Lang("WRONG_CARD", null, cardColor, doorColor));
                return;
            }

            Timer openDoor = timer.In(2.0F, () =>
            {
                door.SetOpen(true);
                card.MarkDirty();
                DoEffect(player, door, card, "SUCCESS");
                Logging(player, Lang("SUCCESS"));
            });

            if (config.settings.DamageKeycards)
            {
                DamageCards(player, door, eff, doorColor, cardColor, card);
            }

            if (config.settings.CloseDoors)
            {
				
                Timer closeDoor = timer.Once(config.tuners.CloseDoorDelay, () => door.SetOpen(false));
            }

        }

        private void DamageCards(BasePlayer player, Door door, string eff, string doorColor, string cardColor, Item card)
        {

            float DamageToDo = config.tuners.DamageToDeal;

            card.condition = (card.condition - DamageToDo);
            card.MarkDirty();

            if (card.isBroken)
            {
				//card broke, lets hurl it away
                Vector3 myVector = new Vector3(0.0f, 1.0f, 0.0f);
                card.DropAndTossUpwards(myVector, 3.0F);

                DoEffect(player, door, card, "ITEM_BROKE");
                Logging(player, Lang("ITEM_BROKE", null, cardColor));

            }
        }

        private void DoEffect(BasePlayer player, Door door, Item card, string eff)
        {
            switch (eff)
            {
                case "SUCCESS":
                    {
                        eff = config.tuners.EffectSuccess;
                        effTarget = door.transform.position;
                        if (eff == string.Empty || eff == "")
                        {
                            eff = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
                        }
                        break;
                    }
                case "FAILURE":
                    {
                        eff = config.tuners.EffectFailure;
                        effTarget = door.transform.position;
                        if (eff == string.Empty || eff == "")
                        {
                            eff = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
                        }
                        break;
                    }
                case "SHOCK":
                    {
                        eff = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";
                        effTarget = door.transform.position;
                        break;
                    }
                case "FAKE_HURT":
                    {
                        eff = "assets/bundled/prefabs/fx/takedamage_generic.prefab";
                        effTarget = player.transform.position;
                        break;
                    }
                case "ITEM_BROKE":
                    {
                        eff = "assets/bundled/prefabs/fx/item_break.prefab";
                        effTarget = player.transform.position;
                        break;
                    }

                default: return;
            }

            if (config.settings.UseEffects)
            {
                Effect.server.Run(eff, effTarget);
            }

        }

        private void Logging(BasePlayer player, string logMsg)
        {

            if (config.settings.PostToChat)
            {
                player.ChatMessage(logMsg);
            }

        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

    }
}
