using System;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    [Info("Custom Cards", "Camoec", 1.1)]
    [Description("Allows to change the max uses of the keycards")]

    public class CustomCards : RustPlugin
    {
        private PluginConfig _config;
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Green KeyCard Max Uses")]
            public int GreenMaxUses = 4;
            [JsonProperty(PropertyName = "Blue KeyCard Max Uses")]
            public int BlueMaxUses = 4;
            [JsonProperty(PropertyName = "Red KeyCard Max Uses")]
            public int RedMaxUses = 4;
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                    throw new Exception();

                SaveConfig(); // override posible obsolet / outdated config
            }
            catch (Exception)
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (cardReader == null || card == null || player == null)
            {
                return null;
            }

            if (cardReader.accessLevel != card.accessLevel)
                return null;

            int maxUses = card.accessLevel == 1 ? _config.GreenMaxUses : card.accessLevel == 2 ? _config.BlueMaxUses : _config.RedMaxUses;

            
           
            Item cardItem = card.GetItem();
            float mHealth = cardItem.maxCondition / maxUses;

            cardReader.Invoke(cardReader.GrantCard, 0.5f);
            cardItem.LoseCondition(cardItem.condition - mHealth > 0.01 ? mHealth : cardItem.condition);
            return true;
        }
    }
}