using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;
using System;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using Oxide.Core.Libraries;


namespace Oxide.Plugins
{
    [Info("Puzzle Points", "Rustonauts", "1.6.3")]
    [Description("Rewards players with scrap, economics, or RP for swiping puzzle cards and completing missions. Broadcasting this is defaulted true.")]
    class PuzzlePoints : RustPlugin
    {

        #region Fields

        [PluginReference]
        private Plugin Economics, ServerRewards, MonumentLock, MonumentNames, SuperCard;

        private Dictionary<int, string> CardTypes = new Dictionary<int, string>();
        private List<Dictionary<string, object>> _monuments;


        #endregion


        #region Init
        
        private void Init()
        {
            //Puts("-- init()");
        }

        private void OnServerInitialized()
        {     
            //Puts("-- server init()");
            LoadData();

            CardTypes.Add(1, "green");
            CardTypes.Add(2, "blue");
            CardTypes.Add(3, "red");
            CardTypes.Add(0, "super");
        }

        #endregion


        #region Configuration

        public Configuration _config;
        
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            
            try
            {
                _config = Config.ReadObject<Configuration>();        
            
                if(_config == null) {
                    Puts("-- no config, creating new");
                    LoadDefaultConfig();
                }                


                if (_config.Version < Version) {                                                             
                    UpdateConfigValues();
                }
					
                SaveConfig();

                if (_config == null) throw new Exception();
                Puts("Config loaded");                
            }
            catch(Exception e)
            {              
                Puts($"-- Error: " + e.GetBaseException().ToString());                 
            }
        }

    
        protected override void LoadDefaultConfig()
        {
            Configuration c = new Configuration();

            LoadDefaultRewards(c);

            SaveConfig();
        }


        protected void LoadDefaultRewards(Configuration config)
        {
            //economics
            config.rewards.Add(new SwipeReward(1, 50, "", "economics"));
            config.rewards.Add(new SwipeReward(2, 150, "", "economics"));
            config.rewards.Add(new SwipeReward(3, 300, "", "economics"));

            //rp
            config.rewards.Add(new SwipeReward(1, 1, "", "rp"));
            config.rewards.Add(new SwipeReward(2, 2, "", "rp"));
            config.rewards.Add(new SwipeReward(3, 3, "", "rp"));

            //dog tags
            config.rewards.Add(new SwipeReward(1, 1, "dogtags", null, 1223900335));
            config.rewards.Add(new SwipeReward(2, 1, "dogtags", null, 1036321299));
            config.rewards.Add(new SwipeReward(3, 1, "dogtags", null, -602717596));

            //scrap
            config.rewards.Add(new SwipeReward(1, 25,  "", "scrap", -932201673));
            config.rewards.Add(new SwipeReward(2, 100, "", "scrap", -932201673));
            config.rewards.Add(new SwipeReward(3, 300, "", "scrap", -932201673)); 

            //lowgradefuel
            config.rewards.Add(new SwipeReward(1, 25, "hq", "metal.refined", 0, false));
            config.rewards.Add(new SwipeReward(2, 50, "hq", "metal.refined", 0, false));
            config.rewards.Add(new SwipeReward(3, 100, "hq", "metal.refined", 0, false));
        }



        private void UpdateConfigValues()
        {            
            if (_config.Version == null || _config.Version == default(VersionNumber) || _config.Version < new VersionNumber(1,5,9))
            {
                Puts("-- ancient config found:  updating..");
                Configuration c = new Configuration();
                LoadDefaultRewards(c); 

                // config v 1.5.7
                if(_config.Version == null) UpdateConfigValues157(c);
                
                if(_config.Version < new VersionNumber(1,5,9)) UpdateConfigValues157(c);

                _config = c;
                _config.Version = new VersionNumber(1, 5, 9);
                SaveConfig();
            }
        }


        private void UpdateConfigValues157(Configuration c)
        {
            c.consoleMessages = _config.consoleMessages;
            c.broadcastSwipe = _config.broadcastSwipe;
            c.cooldown = _config.cooldown;             

            foreach(SwipeReward reward in c.rewards) {
                
                if(reward.reward_item_shortname == "economics") {
                    reward.is_active = _config.useEconomics ?? false;
                    if(reward.access_level == 1) reward.amount = _config.greenCardMoney ?? 0;
                    if(reward.access_level == 2) reward.amount = _config.blueCardMoney ?? 0;
                    if(reward.access_level == 3) reward.amount = _config.redCardMoney ?? 0;                    
                } 

                if(reward.reward_item_shortname == "rp") {
                    reward.is_active = _config.useServerRewards ?? false;
                    if(reward.access_level == 1) reward.amount = _config.greenCardRP ?? 0;
                    if(reward.access_level == 2) reward.amount = _config.blueCardRP ?? 0;
                    if(reward.access_level == 3) reward.amount = _config.redCardRP ?? 0;  
                }

                if(reward.reward_item_shortname == "scrap") {
                    reward.is_active = _config.useScrap ?? false;
                    if(reward.access_level == 1) reward.amount = _config.greenCardScrap ?? 0;
                    if(reward.access_level == 2) reward.amount = _config.blueCardScrap ?? 0;
                    if(reward.access_level == 3) reward.amount = _config.redCardScrap ?? 0;  
                }                
                
            }

            _config = c;
        }


        private void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }


        public class SwipeReward
        {
            public int access_level = 1;
            public string? reward_item_shortname;
            public int reward_item_id;
            public double amount;
            public bool is_active = true;
            public string name;


            public SwipeReward(int cardAccess, double _amount, string _name="", string shortname=null, int id=0, bool _is_active=true)
            {
                access_level = cardAccess;                
                reward_item_shortname = shortname;                                
                reward_item_id = id;
                amount = _amount;
                is_active = _is_active;
                name = _name;
                if(_name.Length < 2) name = shortname;
            }

            public SwipeReward()
            {

            }


        }


        public class Configuration
        {   [JsonIgnore]            
            public bool? useEconomics;
            [JsonIgnore]
            public bool? useServerRewards;
            [JsonIgnore]
            public bool? useScrap;
            [JsonIgnore]
            public int? redCardScrap = 300;
            [JsonIgnore]
            public int? blueCardScrap = 100;
            [JsonIgnore]
            public int? greenCardScrap = 25;
            [JsonIgnore]
            public int? redCardRP = 3;     
            [JsonIgnore]       
            public int? blueCardRP = 2;
            [JsonIgnore]
            public int? greenCardRP = 1;
            [JsonIgnore]
            public double? redCardMoney = 300.00;            
            [JsonIgnore]
            public double? blueCardMoney = 150.00;            
            [JsonIgnore]
            public double? greenCardMoney = 50.00;

            [JsonProperty(PropertyName = "Show Console Messages")]
            public bool consoleMessages = true;

            [JsonProperty(PropertyName = "Show Global Chat Monument Messages (eg. Player swipped card at Launch)")]
            public bool broadcastSwipe = true;

            [JsonIgnore]
            public bool? debugMode = false;
            
            [JsonProperty(PropertyName = "Cooldown: Amount of time (secs) a player must wait before getting rewarded to avoid swipe spam")]
            public int cooldown = 600;

            [JsonProperty(PropertyName = "Swipe Rewards")]
            public List<SwipeReward> rewards = new List<SwipeReward>();

            public VersionNumber Version;


            public Configuration()
            {
                Version = new VersionNumber(1, 5, 7);
            }
        }

   
        #endregion Configuration


        #region DataFile

        private StoredData storedData;

        public class MSession
        {
            [JsonProperty("Player Id")]
            public string player_id;

            [JsonProperty("Monument ShortName")]
            public string monument_shortname;

            [JsonProperty("Card Access Level")]
            public int access_level;

            [JsonProperty("Session started_at")]
            private string started_at;            


            public MSession(string _player_id, int _access_level, string _monument_shortname)
            {
                player_id = _player_id;
                access_level = _access_level;
                started_at = DateTime.Now.ToString();
                monument_shortname = _monument_shortname;
            }

            
            public string GetPlayerId() {return player_id;}
            public BasePlayer GetPlayer(string _player_id){return BasePlayer.FindAwakeOrSleeping(_player_id);}


            public bool InCooldown(int _cooldown)
            {                
                DateTime dt1 = DateTime.Now;
                DateTime dt2 = DateTime.Parse(started_at);
                TimeSpan lock_time = dt1-dt2;
                
                if(Math.Truncate(lock_time.TotalSeconds) < _cooldown) return true;

                return false;
            }
            

        }


        private class StoredData
        { 
            public List<MSession> m_sessions  { get; set; } = new List<MSession>();


            public StoredData()
            {   

            }

            public StoredData(List<MSession> _sessions)
            {
                m_sessions = _sessions;
            }
        }


        private void LoadData()
        {
            try
            {
                Puts("loading datafile..{0}", Name.ToString());
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                Puts("Datafile loaded");
            }
            catch
            {
                storedData = null;
            }
            if (storedData == null)
            {
                Puts("-- no datafile, creating new..");
                ClearData();
            }
        }


        private void SaveData(bool show_console=false)
        { 
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

            if(show_console) Puts("-- data saved!");
        }


        private void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }

        #endregion


        #region Oxide Hooks

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {                   
            if(IsInCooldown(player, card)) return null;            

            var gridPosition = GetGridPosition(player.ServerPosition);                        
            string _shortName = "";
            Item item = card.GetItem();

            if(UseMonumentNames()) _shortName = GetClosestMonument(player);               
            
            if((!cardReader.IsOn() && item.conditionNormalized > 0f) )
            { 
                //make sure the card reader and card are the same access levels
                if (cardReader.accessLevel == card.accessLevel || card.accessLevel == 0)
                {                         
                    //if we're not using MonumentLock, or if we are and cardswipe passes, value is null
                    if (Interface.Call("OnPPSwipe", player, CardTypes[card.accessLevel], cardReader, _shortName, gridPosition) != null)
                    {                        
                        return 1;
                    }
                    
                    Reward(player, card);           
                }
            }

            return null;
        }

        #endregion Oxide Hooks


        #region Core

        private bool IsInCooldown(BasePlayer player, Keycard card)
        {
            MSession _session = GetSession(player, card);
            if(_session != null && _session.InCooldown(_config.cooldown)) return true;
            if(_session != null) storedData.m_sessions.Remove(_session);
            SaveData();
            
            return false;
        }


        private void Reward(BasePlayer player, Keycard card)
        {          
            foreach(SwipeReward reward in _config.rewards) {
                if(card.accessLevel == reward.access_level && reward.is_active) {

                    //economics specific reward
                    if(reward.reward_item_shortname == "economics") {

                        //are we using economics and has it been turned on
                        if (UseEconomics()) 
                        {
                            //api call to economics
                            bool isDeposit = (bool) Economics.Call("Deposit", player.UserIDString, (double)reward.amount);
                            
                            if(_config.consoleMessages && isDeposit)
                            {
                                SendReply(player, GetLang("EconomicsAwarded", player.UserIDString, (double)reward.amount, CardTypes[card.accessLevel]));
                            }
                        }
                    }

                    //rp specific reward
                    else if(reward.reward_item_shortname == "rp") {                        
                        if (UseRewards())
                        {
                            //api call to server rewards
                            bool isAdd = (bool) ServerRewards.Call("AddPoints", player.UserIDString, (int)reward.amount); 

                            if(_config.consoleMessages && isAdd)
                            {
                                SendReply(player, GetLang("PointsAwarded", player.UserIDString, (int)reward.amount, CardTypes[card.accessLevel]));
                            }                          
                        }
                    }

                    //use basic item giving from shortname
                    else {
                        Item item;

                        if(reward.reward_item_id != 0) 
                            item = ItemManager.CreateByItemID(reward.reward_item_id, (int)reward.amount);                            
                        
                        else {
                            if(reward.reward_item_shortname == null) return;
                            item = ItemManager.CreateByName(reward.reward_item_shortname, (int)reward.amount);
                        }


                        if(!player.inventory.GiveItem(item))
                                item.Drop(player.eyes.position, player.eyes.BodyForward() *2);


                        if(_config.consoleMessages)
                        {
                            SendReply(player, GetLang("ItemAwarded", player.UserIDString, item.info.shortname, (int)reward.amount, CardTypes[card.accessLevel]));
                        }
                    }
                }
            }
            
            

            //all good, so lets add a new session
            storedData.m_sessions.Add(new MSession(player.UserIDString, card.accessLevel, GetClosestMonument(player)));
            SaveData();
            
            BroadcastSwipe(player, CardTypes[card.accessLevel]);
        }



        private void BroadcastSwipe(BasePlayer player, string cardType)
        {
            var gridPosition = GetGridPosition(player.ServerPosition);

            var _shortName = GetClosestMonument(player);
            

            if (_config.broadcastSwipe && MonumentLock == null)
            {
                BroadcastToChat("CardSwipedAtName", player.displayName, cardType, GetDisplayName(_shortName), gridPosition);                    
            }

            if(_config.consoleMessages)
            {
                Puts(GetLang("CardSwipedAtName", null, player.displayName, cardType, GetDisplayName(_shortName), gridPosition));
            }
        }



        private string GetClosestMonument(BasePlayer _player)
        {
            foreach(MonumentInfo info in TerrainMeta.Path.Monuments) {
                if(GetGridPosition(_player.transform.position) == GetGridPosition(info.transform.position)) return GetShortName(info.name);
            }
            //var monument = MonumentNames.Call("API_ClosestMonument", _player.transform.position) as string;
            
            return null;
        }



        #endregion Core


        #region Helpers


        private string GetShortName(string prefab_name) => prefab_name.ToString().Split("/").Last().Split(".prefab").First();


        private MSession GetSession(BasePlayer player, Keycard card)
        {
            string shortname = "";

            foreach(MonumentInfo info in TerrainMeta.Path.Monuments) {
                if(GetGridPosition(player.transform.position) == GetGridPosition(info.transform.position))  shortname = GetShortName(info.name.ToString());                                                                                                           
            }


            foreach(MSession _session in storedData.m_sessions) {
                if((_session.player_id == player.UserIDString) && (_session.access_level == card.accessLevel) && _session.monument_shortname == shortname) return _session;
            }

            return null;
        }



        private void BroadcastToChat(string langkey, params object[] args)
        {
            for (int i=0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(GetLang(langkey, player.UserIDString, args));
            }
        }



        private string GetDisplayName(string _shortName)
        {
            if(UseMonumentNames()) return MonumentNames.Call("GetDisplayName", _shortName) as string;
            return _shortName;
        }


        private bool UseMonumentNames()
        {           
            if(MonumentNames == null) return false;
            if(!MonumentNames.IsLoaded) return false;

            return true;
        }


        private bool UseEconomics()
        {
            if(Economics == null) return false;
            if(!Economics.IsLoaded) return false;

            return true;
        }

        private bool UseRewards()
        {
            if(ServerRewards == null) return false;
            if(!ServerRewards.IsLoaded) return false;

            return true;
        }


        //copied from DiscordLogger
        private string GetGridPosition(Vector3 position) => MapHelper.PositionToString(position); // PhoneController.PositionToGridCoord(position);


        //copied from Give
        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }


        #endregion Helpers
    

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EconomicsAwarded"] = "${0} awarded for {1} card swipe!",
                ["PointsAwarded"] = "{0} Reward Points awarded for {1} card swipe!",
                ["ItemAwarded"] = "{0} rewarded ({1}) for {2} card swipe!",
                ["CardSwipedAtName"] = "{0} swiped a {1} card at {2} ({3})!",
                ["CardSwipedAt"] = "{0} swiped a {1} card at {3}!",
                ["GreenCardLabel"] = "green",
                ["BlueCardLabel"] = "blue",
                ["RedCardLabel"] = "red",
            }, this);
        }

        #endregion Localization
    }
}