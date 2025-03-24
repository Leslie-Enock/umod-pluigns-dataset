using Rust;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

#region Changelogs and ToDo
/**********************************************************************
* 2.0.0 :   Rewrite
* 2.0.1 :   Removed a double using directive
*           Removed usage of ImageLibrary (obsolete)
*           Added more checks on Animal/Npc deaths.
*           Added ZombieHorde checks.
*           Hud is now completely transparent
* 2.0.2 :   Fixed exploit on meatconsumption outside playerinventory
*           Harvesting heli/bradley gibs hqm will count as hqm gather
*           Fixed Colors running the same after rank 3

**********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Cannibal", "Krungh Crow", "2.0.2")]
    [Description("Cannibal Game Mode")]
    class Cannibal : CovalencePlugin
    {
        [PluginReference] private Plugin SimpleKillFeed;

        #region Variables
        ulong SteamIDIcon = 76561199232739560;
        string cannibalicon = "assets/icons/skull.png";
        const string Admin_Perm = "cannibal.admin";
        const string CPermanent_Perm = "cannibal.permanent";
        const string FXEatingSound = "assets/bundled/prefabs/fx/gestures/eat_generic.prefab";
        #endregion

        #region Configuration

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Prefix")]
            public string Prefix = "[Cannibal] ";
            [JsonProperty(PropertyName = "UI Settings")]
            public UISettings Settings = new UISettings();
            [JsonProperty(PropertyName = "FX Settings")]
            public FXSettings FX = new FXSettings();
            [JsonProperty(PropertyName = "Cannibal Settings")]
            public CSettings CannibalS = new CSettings();
        }

        class UISettings
        {
            [JsonProperty(PropertyName = "Use Bloody background")]
            public bool UseBloody = true;
            [JsonProperty(PropertyName = "Hud Anchor Min")]
            public string Anchormin = "0 0.95";
            [JsonProperty(PropertyName = "Hud Anchor Max")]
            public string Anchormax = "0.10 1";
        }

        class CSettings
        {
            [JsonProperty(PropertyName = "Save frequency (seconds)")]
            public float TimerAdd = 5;
            [JsonProperty(PropertyName = "Minimum Cannibal time (seconds)")]
            public float MinTime = 300;
            [JsonProperty(PropertyName = "Cannibal Healing")]
            public CHSettings Heal = new CHSettings();
            [JsonProperty(PropertyName = "Cannibal Eating")]
            public CESettings Eat = new CESettings();
            [JsonProperty(PropertyName = "Cannibal Gather")]
            public CGSettings Gather = new CGSettings();
            [JsonProperty(PropertyName = "Cannibal Damage Dealt")]
            public CDSettings Dmg = new CDSettings();
        }

        class FXSettings
        {
            [JsonProperty(PropertyName = "FX during Melee hits")]
            public string MeleeHit = "assets/bundled/prefabs/fx/player/gutshot_scream.prefab";
            [JsonProperty(PropertyName = "FX Turning Cannibal")]
            public string Turned = "assets/bundled/prefabs/fx/player/howl.prefab";
            [JsonProperty(PropertyName = "FX Buff end notification")]
            public string BuffEnd = "assets/bundled/prefabs/fx/invite_notice.prefab";
        }

        class CHSettings
        {
            [JsonProperty(PropertyName = "Can heal self (bandages)")]
            public bool CanHealBandage = true;
            [JsonProperty(PropertyName = "Heal Bonus self (bandages)")]
            public int BonusBandage = 5;
            [JsonProperty(PropertyName = "Can heal others (bandage)")]
            public bool CanHealOthersBandage = true;
            [JsonProperty(PropertyName = "Can heal self (syringe)")]
            public bool CanHealSyringe= true;
            [JsonProperty(PropertyName = "Heal Bonus self (syringe)")]
            public int BonusSyringe = 10;
            [JsonProperty(PropertyName = "Can heal others (syringe)")]
            public bool CanHealOthersSyringe = true;
            [JsonProperty(PropertyName = "Can consume largemedkit")]
            public bool CanConsumeLmedkit = true;
            [JsonProperty(PropertyName = "Can consume antiradpill")]
            public bool CanConsumeRadpills = true;
        }

        class CESettings
        {
            [JsonProperty(PropertyName = "Buff on Human Meat")]
            public bool BuffHumMeat = true;
            [JsonProperty(PropertyName = "Buff Comfort duration (seconds)")]
            public int BuffDuration = 60;
            [JsonProperty(PropertyName = "Buff Comfort (0-100)")]
            public float BuffComfort = 50f;
            [JsonProperty(PropertyName = "Extra nutrition (incl heal)")]
            public int BuffHunger = 20;
            [JsonProperty(PropertyName = "Extra hydration")]
            public int BuffThirst = 30;
            [JsonProperty(PropertyName = "Radiation penalty")]
            public int BuffRadiation = 10;
            [JsonProperty(PropertyName = "Food items (shortname , true/false)")]
            public Dictionary<string , bool> FoodItems = new Dictionary<string , bool>
            { };
        }

        class CGSettings
        {
            [JsonProperty(PropertyName = "Wood Gather rate")]
            public float GWood = 1.0f;
            [JsonProperty(PropertyName = "Stone Gather rate")]
            public float GStone = 1.0f;
            [JsonProperty(PropertyName = "Sulfur Gather rate")]
            public float GSulfur = 1.0f;
            [JsonProperty(PropertyName = "Metal Gather rate")]
            public float GMetal = 1.0f;
            [JsonProperty(PropertyName = "HQM Gather rate")]
            public float GHQM = 1.0f;
        }

        class CDSettings
        {
            [JsonProperty(PropertyName = "Cannibal vs Cannibal Scale Boost")]
            public bool CanScaleBoost = true;
            [JsonProperty(PropertyName = "Melee Weapons (shortname , damagescale)")]
            public Dictionary<string , float> MeleeWeapons = new Dictionary<string , float>
            {};
            [JsonProperty(PropertyName = "Ranged Weapons (shortname , damagescale)")]
            public Dictionary<string , float> RangedWeapons = new Dictionary<string , float>
            {};
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData , true);

        #endregion

        #region Datafile
        private DynamicConfigFile data;
        DataFile dataFile;

        private CannibalLifeTimeDataFile cannibalLifeTimeDataFile;
        private class CannibalLifeTimeDataFile
        {
            public Dictionary<string , CannibalLifeTimeData> CannibalLifeTime { get; set; } = new Dictionary<string , CannibalLifeTimeData>();
        }

        private class CannibalLifeTimeData
        {
            [JsonProperty(PropertyName = "Name")]
            public string PlayerName { get; set; }
            [JsonProperty(PropertyName = "Total Time as Cannibal")]
            public double CannibalLifeTime { get; set; }
            [JsonProperty(PropertyName = "Total Human Meat Consumed")]
            public int HumanMeatConsumed { get; set; }
            [JsonProperty(PropertyName = "Total Humans Killed")]
            public int HumansKilled { get; set; }
            [JsonProperty(PropertyName = "Total Cannibals Killed")]
            public int CannibalsKilled { get; set; }
            [JsonProperty(PropertyName = "Total Npc Killed")]
            public int NpcKilled { get; set; }
            [JsonProperty(PropertyName = "Total Animals Killed")]
            public int AnimalsKilled { get; set; }
            [JsonProperty(PropertyName = "Total Wood Collected")]
            public int WoodCollected = 0;
            [JsonProperty(PropertyName = "Total Stone Collected")]
            public int StoneCollected = 0;
            [JsonProperty(PropertyName = "Total Sulfur Ore Collected")]
            public int SulfurCollected = 0;
            [JsonProperty(PropertyName = "Total Metal Ore Collected")]
            public int MetalCollected = 0;
            [JsonProperty(PropertyName = "Total Hqm Ore Collected")]
            public int HQMCollected = 0;
        }

        private void LoadCannibalLifeTimeData()
        {
            cannibalLifeTimeDataFile = Interface.Oxide.DataFileSystem.ReadObject<CannibalLifeTimeDataFile>("Cannibal/CannibalsLife") ?? new CannibalLifeTimeDataFile();
        }

        private void SaveCannibalLifeTimeData()
        {
            if (cannibalLifeTimeDataFile == null)
            {
                cannibalLifeTimeDataFile = new CannibalLifeTimeDataFile();
            }

            Interface.Oxide.DataFileSystem.WriteObject("Cannibal/CannibalsLife" , cannibalLifeTimeDataFile);
        }

        private void SaveData()
        {
            if (dataFile == null) return;
            Interface.Oxide.DataFileSystem.WriteObject("Cannibal/Cannibals" , dataFile);
        }

        private void LoadData()
        {
            dataFile = Interface.Oxide.DataFileSystem.ReadObject<DataFile>("Cannibal/Cannibals");

            if (dataFile == null)
            {
                dataFile = new DataFile();
                SaveData();
            }
        }

        private class DataFile
        {
            public Dictionary<string , PlayerData> Players = new Dictionary<string , PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Name")]
            public string DisplayName = string.Empty;
            [JsonProperty(PropertyName = "Current Time as Cannibal")]
            public double TotalTimeAsCannibal = 0.0;
            [JsonProperty(PropertyName = "Current Human Meat Consumed")]
            public int HumanMeatConsumed = 0;
            [JsonProperty(PropertyName = "Current Humans Killed")]
            public int HumansKilled = 0;
            [JsonProperty(PropertyName = "Current Cannibals Killed")]
            public int CannibalsKilled = 0;
            [JsonProperty(PropertyName = "Current Npc Killed")]
            public int NpcKilled = 0;
            [JsonProperty(PropertyName = "Current Animals Killed")]
            public int AnimalsKilled = 0;
            [JsonProperty(PropertyName = "Wood Collected")]
            public int WoodCollected = 0;
            [JsonProperty(PropertyName = "Stone Collected")]
            public int StoneCollected = 0;
            [JsonProperty(PropertyName = "Sulfur Ore Collected")]
            public int SulfurCollected = 0;
            [JsonProperty(PropertyName = "Metal Ore Collected")]
            public int MetalCollected = 0;
            [JsonProperty(PropertyName = "Hqm Ore Collected")]
            public int HQMCollected = 0;
        }

        private void AddPlayerData(IPlayer player)
        {
            if (dataFile == null)
            {
                LoadData();
            }

            string playerId = player.Id;

            if (!dataFile.Players.ContainsKey(playerId))
            {
                dataFile.Players.Add(playerId , new PlayerData
                {
                    DisplayName = player.Name
                });
                SaveData();
                StartCannibalTimer(player);
                if (cannibalLifeTimeDataFile == null)
                {
                    LoadCannibalLifeTimeData();
                }
            }
        }

        private void RemovePlayer(IPlayer player)
        {
            if (dataFile == null)
            {
                LoadData();
            }

            if (cannibalLifeTimeDataFile == null)
            {
                LoadCannibalLifeTimeData();
            }

            string playerId = player.Id;

            if (dataFile.Players.ContainsKey(playerId))
            {
                double cannibalLifeTime = dataFile.Players[playerId].TotalTimeAsCannibal;
                string playerName = player.Name;
                int humanMeatConsumed = dataFile.Players[playerId].HumanMeatConsumed;
                int humansKilled = dataFile.Players[playerId].HumansKilled;
                int cannibalsKilled = dataFile.Players[playerId].CannibalsKilled;
                int animalsKilled = dataFile.Players[playerId].AnimalsKilled;
                int npcKilled = dataFile.Players[playerId].NpcKilled;
                int woodCollected = dataFile.Players[playerId].WoodCollected;
                int stoneCollected = dataFile.Players[playerId].StoneCollected;
                int sulfurCollected = dataFile.Players[playerId].SulfurCollected;
                int metalCollected = dataFile.Players[playerId].MetalCollected;
                int hqmCollected = dataFile.Players[playerId].HQMCollected;

                if (cannibalLifeTimeDataFile.CannibalLifeTime.ContainsKey(playerId))
                {
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].CannibalLifeTime += cannibalLifeTime;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].HumanMeatConsumed += humanMeatConsumed;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].HumansKilled += humansKilled;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].CannibalsKilled += cannibalsKilled;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].AnimalsKilled += animalsKilled;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].NpcKilled += npcKilled;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].WoodCollected += woodCollected;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].StoneCollected += stoneCollected;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].SulfurCollected += sulfurCollected;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].MetalCollected += metalCollected;
                    cannibalLifeTimeDataFile.CannibalLifeTime[playerId].HQMCollected += hqmCollected;
                }
                else
                {
                    CannibalLifeTimeData cannibalLifeTimeData = new CannibalLifeTimeData
                    {
                        PlayerName = playerName ,
                        CannibalLifeTime = cannibalLifeTime,
                        HumanMeatConsumed = humanMeatConsumed,
                        HumansKilled = humansKilled,
                        CannibalsKilled = cannibalsKilled,
                        AnimalsKilled = animalsKilled,
                        NpcKilled = npcKilled,
                        WoodCollected = woodCollected ,
                        StoneCollected = stoneCollected ,
                        SulfurCollected = sulfurCollected ,
                        MetalCollected = metalCollected ,
                        HQMCollected = hqmCollected
                    };
                    cannibalLifeTimeDataFile.CannibalLifeTime.Add(playerId , cannibalLifeTimeData);
                }
                dataFile.Players.Remove(playerId);
                SaveData();
                SaveCannibalLifeTimeData();
                Puts($"{playerName} has been removed from the list of cannibals.");
            }
            else
            {
                //Puts($"{player.Name} was not found in the list of cannibals.");
            }
        }

        #endregion

        #region LanguageAPI en/fr
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["N/a"] = "N/a" ,
                ["All"] = "All" ,
                ["SKFTurned"] = "Turned into a Cannibal" ,
                ["TotalCannibals"] = "There are a total of {0} cannibals" ,
                ["TotalCannibalsOnline"] = "There are a {0}/{1} cannibals online" ,
                ["Permission"] = "Permission" ,
                ["Admin"] = "Admin" ,
                ["InvalidInput"] = "Please enter a valid command!" ,
                ["NoPermission"] = "You dont have permission for this command!" ,
                ["AteHumanMeat"] = "You ate human meat and turned into a cannibal!" ,
                ["BuffEnd"] = "Your buff ended eat more meat to have one again." ,
                ["GetCannibalTime"] = "You have been Cannibal for {0} days, {1} hours, {2} minutes, and {3} seconds.",
                ["StartCannibalCmd"] = "You became a cannibal by choice.",
                ["NotCannibalCmd"] = "You are not a Cannibal" ,
                ["AllreadyCannibalCmd"] = "You are allready a Cannibal" ,
                ["PermanentCannibalCmd"] = "You are a Cannibal permanantly" ,
                ["EndCannibalCmd"] = "You have been Cannibal long enough and returned to normal life." ,
                ["EndCannibalCmdNotification"] = "{player} Is no longer a cannibal after {0} days, {1} hrs, {2} minutes, and {3} seconds." ,
                ["FailEndCannibalCmd"] = "You have not been Cannibal long enough to return to normal life.",
                ["BonusHeal"] = "You recieved {0} additional health!" ,
                ["CannotHealSelfBandage"] = "Cannibals cannot be healed with bandages.",
                ["CannotHealSelfMedkit"] = "Cannibals cannot use Large Medical Kits." ,
                ["CannotHealSelfPills"] = "Cannibals cannot use Anti radiation Pills." ,
                ["CannotHealSelfSyringe"] = "Cannibals cannot be healed with syringes.",
                ["ExtraDmg"] = "You dealt {0} extra damage." ,
                ["ExtraDmgPenalty"] = "You dealt {0} less damage." ,
                ["OnConsumeHumanMeat"] = "Meaaaaat ... !!!!",
                ["OnConsumeWater"] = "Cannibals cannot drink water in a normal way.",
                ["InfoTitle"] = "Cannibal Information" ,
                ["InfoTitleMain"] = "Main Page" ,
                ["InfoTitleStat"] = "Statistics" ,
                ["InfoClose"] = "Exit" ,
                ["InfoMainLeftPanelTitle"] = "Server Information" ,
                ["InfoMainLeftPanel"] = "This panel Is for your servers description on the Cannibal system.\n" +
                "This can be a very long language file entry.\n" +
                "\n\nPlace your text here for your players to read" ,
                ["InfoWelcome"] = "Welcome to our server" ,
                ["InfoMain"] = "Main" ,
                ["InfoDamage"] = "Damage" ,
                ["InfoStat"] = "Statistics" ,
                ["InfoStatDescription"] = "Description" ,
                ["InfoStatCurrent"] = "Current" ,
                ["InfoStatTotal"] = "Total" ,
                ["InfoStatTime"] = "Time" ,
                ["InfoStatMeats"] = "Human Meat" ,
                ["InfoStatKillHuman"] = "Humans Killed" ,
                ["InfoStatKillCannibal"] = "Cannibals Killed" ,
                ["InfoStatKillNpc"] = "NPC Killed" ,
                ["InfoStatKillAnimal"] = "Animals Killed" ,
                ["InfoStatWood"] = "Wood Collected" ,
                ["InfoStatStone"] = "Stone Collected" ,
                ["InfoStatSulfur"] = "Sulfur Ore Collected" ,
                ["InfoStatMetal"] = "Metal Ore Collected" ,
                ["InfoStatHQM"] = "HQM Ore Collected" ,
                ["InfoStatSubtext"] = "This panel shows your Cannibal related statistics like how long you have been one.\nAnd future placeholder for more related stuff like kills etc etc.",
                ["InfoWeaponTitle"] = "Weapon Damage Scaling" ,
                ["InfoWeaponTitleMelee"] = "Melee Weapons" ,
                ["InfoWeaponTitleRanged"] = "Ranged Weapons" ,
                ["InfoCmdInfoTitle"] = "Chat Commands" ,
                ["InfoCmdInfo"] = "This Information UI" ,
                ["InfoCmdStart"] = "Manually start Cannibal mode" ,
                ["InfoCmdEnd"] = "Manually end Cannibal mode" ,
                ["InfoCmdTime"] = "Shows how long you are a Cannibal" ,
                ["InfoCmdTotal"] = "Shows total Cannibals logd" ,
                ["InfoCmdOnline"] = "Shows how many Cannibals are on" ,
                ["InfoGatheringTitle"] = "Gathering" ,
                ["InfoGatheringDescription"] = "Gathering Boost or penalty" ,
                ["InfoRankings"] = "Rankings" ,
                ["InfoRankingsTitle"] = "Top 10 Ranks" ,
                ["Top Eaters"] = "Top Eaters" ,
                ["Top Slayers"] = "Top Slayers" ,
                ["Top Traitors"] = "Top Traitors" ,
                ["Top PVE"] = "Top PVE" ,
                ["Top Butchers"] = "Top Butchers" ,
                ["Wood"] = "Wood" ,
                ["Stones"] = "Stones" ,
                ["Sulfur"] = "Sulfur" ,
                ["Metal"] = "Metal" ,
                ["HQM"] = "HQM" ,
            } , this);

            lang.RegisterMessages(new Dictionary<string , string>
            {
                ["N/a"] = "N/D" ,
                ["All"] = "Tous" ,
                ["SKFTurned"] = "Transformé en cannibale",
                ["TotalCannibals"] = "Il y a un total de {0} cannibales" ,
                ["TotalCannibalsOnline"] = "Il y a {0}/{1} cannibales en ligne" ,
                ["Permission"] = "Permission" ,
                ["Admin"] = "Admin" ,
                ["InvalidInput"] = "Veuillez entrer une commande valide !" ,
                ["NoPermission"] = "Vous n'avez pas la permission d'utiliser cette commande !" ,
                ["AteHumanMeat"] = "Vous avez mangé de la viande humaine et êtes devenu cannibale !" ,
                ["BuffEnd"] = "Votre buff a pris fin. Mangez plus de viande pour en obtenir un nouveau." ,
                ["GetCannibalTime"] = "Vous êtes cannibale depuis {0} jours, {1} heures, {2} minutes et {3} secondes." ,
                ["StartCannibalCmd"] = "Vous êtes devenu cannibale par choix." ,
                ["NotCannibalCmd"] = "Vous n'êtes pas un cannibale." ,
                ["AllreadyCannibalCmd"] = "Vous êtes déjà un cannibale." ,
                ["PermanentCannibalCmd"] = "Vous êtes cannibale de manière permanente." ,
                ["EndCannibalCmd"] = "Vous avez été cannibale assez longtemps et êtes revenu à une vie normale." ,
                ["EndCannibalCmdNotification"] = "{player} n'est plus un cannibale après {0} jours, {1} heures, {2} minutes et {3} secondes." ,
                ["FailEndCannibalCmd"] = "Vous n'avez pas été cannibale assez longtemps pour revenir à une vie normale." ,
                ["BonusHeal"] = "Vous avez reçu {0} points de vie supplémentaires !" ,
                ["CannotHealSelfBandage"] = "Les cannibales ne peuvent pas être soignés avec des bandages." ,
                ["CannotHealSelfMedkit"] = "Les cannibales ne peuvent pas utiliser de grands kits médicaux." ,
                ["CannotHealSelfPills"] = "Les cannibales ne peuvent pas utiliser de pilules contre les radiations." ,
                ["CannotHealSelfSyringe"] = "Les cannibales ne peuvent pas être soignés avec des seringues." ,
                ["ExtraDmg"] = "Vous avez infligé {0} dégâts supplémentaires." ,
                ["ExtraDmgPenalty"] = "Vous avez infligé {0} dégâts de moins." ,
                ["OnConsumeHumanMeat"] = "Viande... !!!!" ,
                ["OnConsumeWater"] = "Les cannibales ne peuvent pas boire de l'eau normalement." ,
                ["InfoTitle"] = "Information Cannibale" ,
                ["InfoTitleMain"] = "Page principale" ,
                ["InfoTitleStat"] = "Statistiques" ,
                ["InfoClose"] = "Fermer" ,
                ["InfoWelcome"] = "Bienvenue sur notre serveur" ,
                ["InfoMain"] = "Principal" ,
                ["InfoMainLeftPanelTitle"] = "Informations sur le serveur" ,
                ["InfoMainLeftPanel"] = "Ce panneau est destiné à la description de votre serveur sur le système Cannibal.\n" +
                "Ceci peut être une entrée de fichier de langue très longue.\n" +
                "\n\nPlacez votre texte ici pour que vos joueurs le lisent" ,
                ["InfoDamage"] = "Dommages" ,
                ["InfoStat"] = "Statistiques" ,
                ["InfoStatDescription"] = "Description" ,
                ["InfoStatCurrent"] = "Actuel" ,
                ["InfoStatTotal"] = "Total" ,
                ["InfoStatTime"] = "Temps" ,
                ["InfoStatMeats"] = "Viande humaine" ,
                ["InfoStatKillHuman"] = "Humains tués" ,
                ["InfoStatKillCannibal"] = "Cannibales tués" ,
                ["InfoStatKillNpc"] = "PNJ tués" ,
                ["InfoStatKillAnimal"] = "Animaux tués" ,
                ["InfoStatWood"] = "Bois collecté" ,
                ["InfoStatStone"] = "Pierre collectée" ,
                ["InfoStatSulfur"] = "Minerai de soufre collecté" ,
                ["InfoStatMetal"] = "Minerai de métal collecté" ,
                ["InfoStatHQM"] = "Minerai de HQM collecté" ,
                ["InfoStatSubtext"] = "Ce panneau affiche vos statistiques liées au cannibalisme, telles que la durée pendant laquelle vous avez été cannibale. Et futur espace réservé pour plus de statistiques liées, comme les victoires, etc." ,
                ["InfoWeaponTitle"] = "Échelle de dégâts des armes" ,
                ["InfoWeaponTitleMelee"] = "Armes de mêlée" ,
                ["InfoWeaponTitleRanged"] = "Armes à distance" ,
                ["InfoCmdInfoTitle"] = "Commandes de Chat" ,
                ["InfoCmdInfo"] = "Cette interface d'information" ,
                ["InfoCmdStart"] = "Démarrer manuellement le mode cannibale" ,
                ["InfoCmdEnd"] = "Arrêter manuellement le mode cannibale" ,
                ["InfoCmdTime"] = "Affiche depuis combien de temps vous êtes un cannibale" ,
                ["InfoCmdTotal"] = "Affiche le total des Cannibales enregistrés" ,
                ["InfoCmdOnline"] = "Affiche le nombre de Cannibales en ligne" ,
                ["InfoGatheringTitle"] = "Collecte" ,
                ["InfoGatheringDescription"] = "Bonus ou pénalité de collecte" ,
                ["InfoRankings"] = "Classements" ,
                ["InfoRankingsTitle"] = "Top 10 Classements" ,
                ["Top Eaters"] = "Top Mangeurs" ,
                ["Top Slayers"] = "Top Tueurs" ,
                ["Top Traitors"] = "Top Traîtres" ,
                ["Top PVE"] = "Top JcE" ,
                ["Top Butchers"] = "Top Bouchers" ,
                ["Wood"] = "Bois" ,
                ["Stones"] = "Pierres" ,
                ["Sulfur"] = "Soufre" ,
                ["Metal"] = "Métal" ,
                ["HQM"] = "HQM" ,
            } , this , "fr");

        }
        #endregion

        #region Commands
        [Command("Cannibal")]
        private void CannibalCmd(IPlayer player , string command , string[] args)
        {
            string prefix = configData.Prefix;

            if (args.Length < 1)
            {
                if (player.IsServer)
                {

                    Puts(lang.GetMessage("InvalidInput" , this , null));
                    return;
                }

                SendMessage(player , "InvalidInput");
                return;
            }
            switch (args[0])
            {
                case "time":
                    {
                        if (player.IsServer)
                        {
                            Puts(lang.GetMessage("InvalidInput" , this , null));
                            return;
                        }
                        if (IsCannibal(player))
                        {
                            double GetTime = GetCannibalTime(player);
                            TimeSpan formattedTime = FormatTime(GetTime);
                            string message = lang.GetMessage("GetCannibalTime" , this , player.Id);
                            message = message.Replace("{0}" , formattedTime.Days.ToString());
                            message = message.Replace("{1}" , formattedTime.Hours.ToString());
                            message = message.Replace("{2}" , formattedTime.Minutes.ToString());
                            message = message.Replace("{3}" , formattedTime.Seconds.ToString());
                            SendMessage(player , message);
                            return;
                        }
                        else
                        {
                            SendMessage(player , "NotCannibalCmd");
                        }
                        break;
                    }
                case "start":
                    {
                        if (player.IsServer)
                        {
                            Puts(lang.GetMessage("InvalidInput" , this , null));
                            return;
                        }
                        if (!IsCannibal(player))
                        {
                            if (!HasPerm(player , CPermanent_Perm))
                            {
                                SetupPlayer(player);
                                SendMessage(player , "StartCannibalCmd");
                                SimpleKillFeed?.Call("SendKillfeedmessage" , $"{player.Name} {Translate(player, "SKFTurned")}");
                            }
                            else
                            {
                                SendMessage(player , "NoPermission");
                            }
                        }
                        else
                        {
                            SendMessage(player , "AllreadyCannibalCmd");
                        }
                        break;
                    }
                case "end":
                    {
                        if (player.IsServer)
                        {
                            Puts(lang.GetMessage("InvalidInput" , this , null));
                            return;
                        }
                        if (!HasPerm(player , CPermanent_Perm))
                        {
                            if (IsCannibal(player))
                            {
                                if (GetCannibalTime(player) > configData.CannibalS.MinTime || HasPerm(player , Admin_Perm))
                                {

                                    double GetTime = GetCannibalTime(player);
                                    TimeSpan formattedTime = FormatTime(GetTime);
                                    string message = lang.GetMessage("EndCannibalCmdNotification" , this , player.Id);
                                    message = message.Replace("{player}" , player.Name.ToString());
                                    message = message.Replace("{0}" , formattedTime.Days.ToString());
                                    message = message.Replace("{1}" , formattedTime.Hours.ToString());
                                    message = message.Replace("{2}" , formattedTime.Minutes.ToString());
                                    message = message.Replace("{3}" , formattedTime.Seconds.ToString());
                                    SimpleKillFeed?.Call("SendKillfeedmessage" , message);
                                    NextTick(() =>
                                    {
                                        RemovePlayer(player);
                                        SendMessage(player , "EndCannibalCmd");
                                        // Stop and destroy the timer
                                        StopCannibalTimer(player);
                                        Cannibalhud(player.Object as BasePlayer);
                                    });

                                    break;
                                }
                                else
                                {
                                    SendMessage(player , "FailEndCannibalCmd");
                                }
                            }
                            if (!IsCannibal(player))
                            {
                                SendMessage(player , "NotCannibalCmd");
                            }
                        }
                        else
                        {
                            SendMessage(player , "PermanentCannibalCmd");
                        }
                        break;
                    }
                case "total":
                    {
                        int cannibalCount = CountCannibals();
                        if (player.IsServer)
                        {
                            Puts($"Total number of cannibals: {cannibalCount}"); // Log the count
                            return;
                        }

                        if (HasPerm(player , Admin_Perm))
                        {
                            string message = lang.GetMessage("TotalCannibals" , this , player.Id);
                            message = message.Replace("{0}" , $"{cannibalCount}");
                            SendMessage(player , message);
                        }
                        else
                        {
                            SendMessage(player , "NoPermission");
                        }
                        break;
                    }
                case "info":
                    {
                        CannibalinfoPanel(player);
                        break;
                    }
                case "online":
                    {
                        int totalCannibalCount = CountCannibals();
                        int onlineCannibalCount = CountOnlineCannibals();

                        if (player.IsServer)
                        {

                            Puts($"Total number of cannibals online: {onlineCannibalCount}");
                            return;
                        }

                        if (HasPerm(player , Admin_Perm))
                        {
                            string message = lang.GetMessage("TotalCannibalsOnline" , this , player.Id);
                            message = message.Replace("{0}" , $"{onlineCannibalCount}");
                            message = message.Replace("{1}" , $"{totalCannibalCount}");
                            SendMessage(player , message);
                        }
                        else
                        {
                            SendMessage(player , "NoPermission");
                        }
                        break;
                    }
                default:
                    if (player.IsServer)
                    {

                        Puts(lang.GetMessage("InvalidInput" , this , null));
                        return;
                    }
                    player.Reply(prefix + lang.GetMessage("InvalidInput" , this , player.Id));
                    break;
            }
        }

        #endregion

        #region Hooks

        private void OnDispenserBonus(ResourceDispenser dispenser , BasePlayer player , Item item)
        {
            if (player == null) return;
            OnDispenserGather(dispenser , player , item);
        }

        private void OnDispenserGather(ResourceDispenser dispenser , BaseEntity entity , Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null || !player.IsValid() || !IsCannibal(player)) return;

            var ent = dispenser.GetComponent<BaseEntity>();
            if (ent == null) return;

            string itemShortname = item.info.shortname;
            int itemAmount = item.amount;
            float multiplier = GetMultiplierForItem(itemShortname);

            int modifiedAmount = (int)(itemAmount * multiplier);
            int ExtraAmount = modifiedAmount - itemAmount;
            //Puts($"OnDispenserGather: Dispenser {ent} : Player {player} gathered {itemAmount}x {itemShortname}.\nModified Amount: {modifiedAmount} Logd amount : {ExtraAmount}");

            Item modifiedItem = ItemManager.CreateByItemID(item.info.itemid , modifiedAmount);

            switch (modifiedItem.info.shortname)
            {
                case "wood":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].WoodCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                case "stones":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].StoneCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                case "metal.ore":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].MetalCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                case "sulfur.ore":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].SulfurCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                case "hq.metal.ore":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].HQMCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                case "metal.refined":
                    player.GiveItem(modifiedItem);
                    dataFile.Players[player.UserIDString].HQMCollected += ExtraAmount;
                    item.UseItem(itemAmount);
                    break;
                default:
                    player.GiveItem(modifiedItem);
                    break;
            }
        }

        private void OnEntityDeath(BaseAnimalNPC animal , HitInfo info)
        {
            if (animal == null || info.InitiatorPlayer == null) return;

            var attacker = info.InitiatorPlayer;

            // Check if the attacker is a valid player and a cannibal
            if (info.InitiatorPlayer.userID.IsSteamId() && IsCannibal(attacker))
            {
                string attackerId = attacker.UserIDString;

                // Check if the attacker's data is in the dictionary
                if (dataFile.Players.TryGetValue(attackerId , out var playerData))
                {
                    playerData.AnimalsKilled += 1;
                }
                else
                {
                    Puts($"Player data not found for {attackerId} in OnEntityDeath method.");
                    if (!IsCannibal(attacker)) return;
                }
            }
        }

        private void OnEntityDeath(BasePlayer victim , HitInfo info)
        {
            if (victim == null || info == null || info.InitiatorPlayer == null) return;

            var attacker = info.InitiatorPlayer;

            if (IsZombieHorde(attacker)) return;

            if (IsCannibal(attacker) && !victim.IsNpc && victim.UserIDString.Length == 17 && !IsZombieHorde(victim))
            {
                //Puts("Human attacker is cannibal victim should not be npc");
                dataFile.Players[attacker.UserIDString].HumansKilled += 1;
                return;
            }
            if (IsCannibal(attacker) && victim.IsNpc && victim.UserIDString.Length < 17 && IsZombieHorde(victim))
            {
                //Puts("Human attacker is cannibal victim is npc");
                dataFile.Players[attacker.UserIDString].NpcKilled += 1;
                return;
            }
            if (IsCannibal(attacker) && IsCannibal(victim) && !IsZombieHorde(victim))
            {
                //Puts("Human attacker is cannibal victim is cannibal");
                dataFile.Players[attacker.UserIDString].CannibalsKilled += 1;
                return;
            }
            else
            {
                if (!IsCannibal(attacker)) return;
                //Puts("Human attacker is cannibal victim is a unknown npc type but counted as npc");
                dataFile.Players[attacker.UserIDString].NpcKilled += 1;
                return;
            }
            return;
        }

        private object OnPlayerDrink(BasePlayer player , LiquidContainer container)
        {
            string containerString = container.ToString();
            Match match = Regex.Match(containerString , @"\[(\d+)\]");
            if (match.Success)
            {
                string networkId = match.Groups[1].Value;
                string containerStringWithoutId = Regex.Replace(containerString , @"\[\d+\]" , "");
                string containerShortPrefabName = containerStringWithoutId.Replace('_' , '.');
                if (IsCannibal(player))
                {
                    if (configData.CannibalS.Eat.FoodItems.TryGetValue(containerShortPrefabName , out bool isAllowed))
                    {
                        if (isAllowed)
                        {
                            //Puts($"OnPlayerDrink isAllowed is true: {isAllowed}");
                            return null;
                        }
                        else
                        {
                            SendMessage(player , "OnConsumeWater");
                            //Puts($"OnPlayerDrink isAllowed is false: {isAllowed}");
                            return true;
                        }
                    }
                }
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity , HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            BasePlayer victim = entity as BasePlayer;
            if (entity == null || info == null || attacker == null) return null;

            if (attacker != null && entity != null && IsCannibal(attacker))
            {
                if (entity is BasePlayer || entity is BaseAnimalNPC)
                {
                    if (IsCannibal(attacker) && IsCannibal(victim) && !configData.CannibalS.Dmg.CanScaleBoost) return null;
                    string itemShortName = info?.Weapon?.GetItem()?.info?.shortname;
                    if (itemShortName == null)
                    {
                        BaseEntity heldEntity = attacker.GetHeldEntity();
                        if (heldEntity != null)
                        {

                            string shortName = heldEntity.ShortPrefabName;
                            if (shortName.Contains("rocket_launcher") || shortName.Contains("grenade"))
                            {
                                //Puts($"{shortName}"); //prefab
                                float damageMultiplier = 1.1f;//grab from cfg instead of this test value
                                float originalDamage = info.damageTypes.Total();
                                float modifiedDamage = originalDamage * damageMultiplier;
                                info.damageTypes.ScaleAll(damageMultiplier);
                                if (damageMultiplier > 1.0f)
                                {
                                    SendMessage(attacker , $"Original Damage: {originalDamage}, Modified Damage: {modifiedDamage}");
                                    string message = lang.GetMessage("ExtraDmg" , this , attacker.UserIDString);
                                    message = message.Replace("{0}" , Mathf.Round(modifiedDamage - originalDamage).ToString("F1"));
                                    SendMessage(attacker , message);
                                }
                            }
                            return null;
                        }
                    return null;
                    }

                    if (IsMeleeWeapon(itemShortName , info.Weapon.GetEntity()))
                    {
                        float damageMultiplier = configData.CannibalS.Dmg.MeleeWeapons[itemShortName];
                        float originalDamage = info.damageTypes.Total();
                        float modifiedDamage = originalDamage * damageMultiplier;
                        info.damageTypes.ScaleAll(damageMultiplier);
                        Effect.server.Run(configData.FX.MeleeHit , entity.transform.position + new Vector3(0 , 1 , 0));
                        if (damageMultiplier > 1.0f)
                        {
                            string message = lang.GetMessage("ExtraDmg" , this , attacker.UserIDString);
                            message = message.Replace("{0}" , Mathf.Round(modifiedDamage - originalDamage).ToString("F1"));
                            SendMessage(attacker , message);
                        }
                        else if (damageMultiplier < 1.0f)
                        {
                            string message = lang.GetMessage("ExtraDmgPenalty" , this , attacker.UserIDString);
                            message = message.Replace("{0}" , Mathf.Round(originalDamage - (originalDamage * damageMultiplier)).ToString("F1"));
                            SendMessage(attacker , message);
                        }
                    }
                    else if (IsRangedWeapon(itemShortName , info.Weapon.GetEntity()))
                    {
                        Puts(info?.Initiator.ToString());
                        float damageMultiplier = configData.CannibalS.Dmg.RangedWeapons[itemShortName];
                        float originalDamage = info.damageTypes.Total();
                        float modifiedDamage = originalDamage * damageMultiplier;
                        info.damageTypes.ScaleAll(damageMultiplier);
                        if (damageMultiplier > 1.0f)
                        {
                            string message = lang.GetMessage("ExtraDmg" , this , attacker.UserIDString);
                            message = message.Replace("{0}" , Mathf.Round(modifiedDamage - originalDamage).ToString("F1"));
                            SendMessage(attacker , message);
                        }
                        else if (damageMultiplier < 1.0f)
                        {
                            string message = lang.GetMessage("ExtraDmgPenalty" , this , attacker.UserIDString);
                            message = message.Replace("{0}" , Mathf.Round(originalDamage - (originalDamage * damageMultiplier)).ToString("F1"));
                            SendMessage(attacker , message);
                        }
                    }
                }
                return null;
            }
            return null;
        }

        private object OnHealingItemUse(MedicalTool tool , BasePlayer player)
        {
            if (!player.userID.IsSteamId()) return null;

            IPlayer iPlayer = covalence.Players.FindPlayerById(player.UserIDString);

            if (!IsCannibal(iPlayer)) return null;

            BasePlayer toolOwner = tool.GetOwnerPlayer();
        
            if (toolOwner != null)
            {
                IPlayer toolOwnerIPlayer = covalence.Players.FindPlayerById(toolOwner.UserIDString);

                int itemId = tool.GetItem().info.itemid; // Get the item ID from the item

                switch (itemId)
                {
                    case 1079279582: // syringe.medical
                        if (toolOwner == player && configData.CannibalS.Heal.CanHealSyringe)
                        {
                            int BonusSyringe = (configData.CannibalS.Heal.BonusSyringe);
                            if (BonusSyringe != 0) player.Heal(BonusSyringe);
                            if(player._health < player._maxHealth)
                            {
                                string message = lang.GetMessage("BonusHeal" , this , iPlayer.Id);
                                message = message.Replace("{0}" , BonusSyringe.ToString());
                                SendMessage(iPlayer , message);
                            }
                            return null;
                        }
                        else if (toolOwner != player && configData.CannibalS.Heal.CanHealOthersSyringe) return null;
                        else
                        {
                            SendMessage(iPlayer , "CannotHealSelfSyringe");
                            //Puts($"{iPlayer.Name} is using a syringe but not allowed to heal himself and/or others with syringes.");
                            return true;
                        }
                        break;
                    case -2072273936: // bandage
                        if (toolOwner == player && configData.CannibalS.Heal.CanHealBandage)
                        {
                            int BonusBandage = (configData.CannibalS.Heal.BonusBandage);
                            if (BonusBandage != 0) player.Heal(BonusBandage);
                            if (player._health < player._maxHealth)
                            {
                                string message = lang.GetMessage("BonusHeal" , this , iPlayer.Id);
                                message = message.Replace("{0}" , BonusBandage.ToString());
                                SendMessage(iPlayer , message);
                            }
                            return null;
                        }
                        else if (toolOwner != player && configData.CannibalS.Heal.CanHealOthersBandage) return null;
                        else
                        {
                            SendMessage(iPlayer , "CannotHealSelfBandage");
                            //Puts($"{iPlayer.Name} is using a bandage but not allowed to heal himself and/or others with bandages.");
                            return true;
                        }
                        break;
                    default:
                        Puts($"{iPlayer.Name} is using an unknown medical tool with item ID {itemId}.");
                        break;
                }
            }
            return null;
        }

        // Define a dictionary to keep track of the last time a player consumed raw human meat
        Dictionary<string , float> lastConsumedTimes = new Dictionary<string , float>();

        private object OnItemAction(Item item , string action , BasePlayer player)
        {
            if (item == null || item.GetRootContainer() == null) return null;
            //Puts($"Action was {action} with item {item.ToString()}");
            var itemcontainer = item.GetRootContainer();
            if (action == "drop") return null;

            IPlayer iPlayer = covalence.Players.FindPlayerById(player.UserIDString);
            float oldHealth = player.health; // Get the player's old health
            string foodShortName = item.info.shortname; // Get the shortname of the food item
            string playerId = iPlayer.Id;
            if (IsCannibal(iPlayer) && action == "consumecontents")
            {
                //Puts($"Consumable was : {item} id : {item.info.itemid} inside consumecontents");
                switch (item.info.shortname)
                {
                    case "waterjug": // waterjug
                        if (configData.CannibalS.Eat.FoodItems.TryGetValue(foodShortName , out bool isAllowed))
                        {
                            if (isAllowed)
                            {
                                return null;
                            }
                            else
                            {
                                SendMessage(iPlayer , $"OnConsumeWater");
                                return true;
                            }
                        }
                        break;
                    default:
                        {
                            //Puts($"Action was {action} inside cannibal consumecontents 2 (default) item : {item}");
                            return null;
                        }
                        break;
                        return null;
                }
            }

            if (IsCannibal(iPlayer) && action == "consume")
            {
                //Puts($"Action was {action} inside cannibal consume 1");
                if (configData.CannibalS.Eat.FoodItems.TryGetValue(foodShortName , out bool isAllowed))
                {
                    if (isAllowed)
                    {
                        float newHealth = player.health; // Get the player's new health after giving boosts

                        float healthChange = newHealth - oldHealth; // Calculate the health change
                        string healthChanged = Mathf.Round(healthChange).ToString("F1");

                        //Puts($"{iPlayer.Name} is eating {foodShortName}. Health added: +{healthChanged}");
                        return null;
                    }
                    else
                    {
                        SendMessage(iPlayer , $"Cannot consume {foodShortName}.");//<-- needs message
                        //Puts($"{iPlayer.Name} is not allowed to consume {foodShortName}.");
                        return true;
                    }
                }
                else
                {
                    //Puts($"Item {foodShortName} not found in the foodlist switching to secondary check.");
                    switch (item.info.itemid)
                    {
                        case 254522515: // largemedkit
                            if (configData.CannibalS.Heal.CanConsumeLmedkit) { return null; }
                            else
                            {
                                SendMessage(iPlayer , "CannotHealSelfMedkit");
                                //Puts($"{iPlayer.Name} is using a largemedkit but not allowed to heal himself");
                                return true;
                            }
                            break;
                        case -1432674913: // antiradpills
                            if (configData.CannibalS.Heal.CanConsumeRadpills) { return null; }
                            else
                            {
                                SendMessage(iPlayer , "CannotHealSelfPills");
                                //Puts($"{iPlayer.Name} is using Anti Radiation pills but not allowed to use them");
                                return true;
                            }
                        case 1536610005: // cooked human meat
                            if (!configData.CannibalS.Eat.BuffHumMeat)
                            {
                                return null;
                            }
                            else
                            {
                                if (lastConsumedTimes.TryGetValue(playerId , out float lastConsumedTime) && Time.realtimeSinceStartup - lastConsumedTime < 1.5f)
                                {
                                    return true;
                                }
                                lastConsumedTimes[playerId] = Time.realtimeSinceStartup;
                                ShowCannibalTimer(player);
                                dataFile.Players[player.UserIDString].HumanMeatConsumed += 1;
                                SendMessage(player , "OnConsumeHumanMeat");
                                GiveBoosts(player ,
                                configData.CannibalS.Eat.BuffComfort / 100 ,
                                configData.CannibalS.Eat.BuffRadiation ,
                                configData.CannibalS.Eat.BuffThirst ,
                                configData.CannibalS.Eat.BuffHunger ,
                                configData.CannibalS.Eat.BuffDuration);
                                ShowCannibalTimer(player);
                                return null;
                            }
                            break;
                        case -682687162: // burned human meat
                            if (!configData.CannibalS.Eat.BuffHumMeat)
                            {
                                return null;
                            }
                            else
                            {
                                if (lastConsumedTimes.TryGetValue(playerId , out float lastConsumedTime) && Time.realtimeSinceStartup - lastConsumedTime < 1.5f)
                                {
                                    return true;
                                }
                                lastConsumedTimes[playerId] = Time.realtimeSinceStartup;
                                ShowCannibalTimer(player);
                                dataFile.Players[player.UserIDString].HumanMeatConsumed += 1;
                                SendMessage(player , "OnConsumeHumanMeat");
                                GiveBoosts(player ,
                                configData.CannibalS.Eat.BuffComfort / 100 ,
                                configData.CannibalS.Eat.BuffRadiation ,
                                configData.CannibalS.Eat.BuffThirst ,
                                configData.CannibalS.Eat.BuffHunger ,
                                configData.CannibalS.Eat.BuffDuration);
                                ShowCannibalTimer(player);
                                return null;
                            }
                            break;
                        case -1709878924: // raw human meat
                            if (!configData.CannibalS.Eat.BuffHumMeat)
                            {
                                return null;
                            }
                            else
                            {
                                if (lastConsumedTimes.TryGetValue(playerId , out float lastConsumedTime) && Time.realtimeSinceStartup - lastConsumedTime < 1.5f)
                                {
                                    return true;
                                }
                                lastConsumedTimes[playerId] = Time.realtimeSinceStartup;
                                ShowCannibalTimer(player);
                                dataFile.Players[player.UserIDString].HumanMeatConsumed += 1;
                                SendMessage(player , "OnConsumeHumanMeat");
                                GiveBoosts(player ,
                                configData.CannibalS.Eat.BuffComfort / 100 ,
                                configData.CannibalS.Eat.BuffRadiation ,
                                configData.CannibalS.Eat.BuffThirst ,
                                configData.CannibalS.Eat.BuffHunger ,
                                configData.CannibalS.Eat.BuffDuration);
                                SendFX(player , FXEatingSound);
                                item.UseItem();
                                ShowCannibalTimer(player);
                                return true;
                            }
                            break;
                        default:
                            {
                                //Puts($"Action was {action} inside cannibal consume 2 (default) item : {item}");
                                return null;
                            }
                            break;
                            return null;
                    }

                }

            }
            switch (item.info.itemid)
            {
                case 1536610005: // cooked human meat
                    SetupPlayer(iPlayer);
                    Cannibalhud(player);
                    SendMessage(iPlayer , "AteHumanMeat");
                    SimpleKillFeed?.Call("SendKillfeedmessage" , $"{player.displayName} {Translate(iPlayer , "SKFTurned")}");
                    Effect.server.Run(configData.FX.Turned , player.transform.position);
                    return null;
                    break;
                default:
                    {
                        return null;
                    }
                    break;
            }
            return null;
        }

        private void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
            CheckAndAddBaseWeapons();
            CheckAndAddFoodItems();
            permission.RegisterPermission(Admin_Perm , this);
            permission.RegisterPermission(CPermanent_Perm , this);

            NextTick(() =>
            {
                foreach (var player in players.Connected)
                {
                    Cannibalhud(player.Object as BasePlayer);
                    if (IsCannibal(player))
                    {
                        StartCannibalTimer(player);
                    }
                }
            });

        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyCannibalHuds(player);
                if (textRefreshTimer != null && !textRefreshTimer.Destroyed)
                {
                    textRefreshTimer.Destroy();
                    CuiHelper.DestroyUi(player , "CannibalUITimerPanel_" + player.userID);
                }
                CuiHelper.DestroyUi(player , "Melee WeaponsPanel");
                CuiHelper.DestroyUi(player , "Ranged WeaponsPanel");
                CuiHelper.DestroyUi(player , "MeleeWeaponsPanelCloseButton");
                CuiHelper.DestroyUi(player , "MainPanel");
            }
        }

        private void OnPlayerDisconnected(BasePlayer player , string reason)
        {
            IPlayer iPlayer = covalence.Players.FindPlayerById(player.UserIDString);
            StopCannibalTimer(iPlayer);
            foreach (BasePlayer plyr in BasePlayer.activePlayerList)
                Cannibalhud(plyr);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            IPlayer iPlayer = covalence.Players.FindPlayerById(player.UserIDString);
            foreach (BasePlayer plyr in BasePlayer.activePlayerList) Cannibalhud(plyr);
            StartCannibalTimer(iPlayer);
        }

        private void OnPlayerDeath(BasePlayer player , HitInfo info) => DestroyCannibalHuds(player);

        #endregion

        #region Cannibal Core

        // Define a dictionary to store the total remaining boost duration for each player
        Dictionary<ulong , int> totalBoostDurations = new Dictionary<ulong , int>();
        // Define a dictionary to store the boost timers for each player
        Dictionary<ulong , Timer> boostTimers = new Dictionary<ulong , Timer>();

        private void GiveBoosts(BasePlayer player , float comfort , int radiation , int thirst , int hunger , int duration)
        {
            Timer existingTimer = null; // Declare existingTimer outside the if statements

            // Check if the player is still a cannibal
            if (!IsCannibal(player))
            {
                // If not a cannibal, stop and destroy the active timer (if any)
                if (boostTimers.TryGetValue(player.userID , out existingTimer))
                {
                    existingTimer.Destroy();
                    boostTimers.Remove(player.userID);
                    player.metabolism.comfort.min = 0;
                }
                return; // Don't apply boosts if not a cannibal
            }
            //Puts("should boost");
            player.metabolism.radiation_poison.Add(radiation);
            player.metabolism.hydration.Add(30 + thirst);
            player.metabolism.calories.Add(hunger);
            player.health += hunger;
            player.metabolism.comfort.min = comfort;
            int remainingDuration;
            // Check if the player already has a boost duration, if yes, add to it
            if (totalBoostDurations.TryGetValue(player.userID , out remainingDuration))
            {
                remainingDuration += duration;
            }
            else
            {
                remainingDuration = duration;
            }
            if (boostTimers.TryGetValue(player.userID , out existingTimer))
            {
                existingTimer.Destroy();
                boostTimers.Remove(player.userID); // Remove the old timer from the dictionary
            }

            existingTimer = timer.Repeat(1, remainingDuration , () =>
            {
                // Update the total boost duration each second
                remainingDuration--;
                totalBoostDurations[player.userID] = remainingDuration;
                // Check if the player is no longer a cannibal or the duration is exhausted
                if (remainingDuration <= 0 || !IsCannibal(player))
                {
                    // Stop and destroy the timer
                    existingTimer.Destroy();
                    boostTimers.Remove(player.userID);
                    player.metabolism.comfort.min = 0;
                    // Clear the player's total boost duration when the timer expires
                    totalBoostDurations[player.userID] = 0;
                    //Puts($"Boost timer ended for player {player.displayName}.");
                    Effect.server.Run(configData.FX.BuffEnd , player.transform.position);
                    SendMessage(player , "Your buff ended");
                }
            });
            boostTimers[player.userID] = existingTimer;// Store the boost timer in the dictionary
            //Puts($"Remaining time for player {player.displayName}: {remainingDuration} seconds");

        }

        private void SetupPlayer(IPlayer player)
        {
            AddPlayerData(player);
            StartCannibalTimer(player);
        }

        private Dictionary<string , Timer> playerTimers = new Dictionary<string , Timer>();

        private void StartCannibalTimer(IPlayer player)
        {
            string playerId = player.Id;
            if (!playerTimers.ContainsKey(playerId))
            {
                Timer cannibalTimer = timer.Repeat(configData.CannibalS.TimerAdd , 0 , () => IncrementCannibalTime(player));
                playerTimers.Add(playerId , cannibalTimer);
            }
        }

        private void IncrementCannibalTime(IPlayer player)
        {
            string playerId = player.Id;
            if (dataFile.Players.ContainsKey(playerId))
            {
                double oldTime = dataFile.Players[playerId].TotalTimeAsCannibal;
                dataFile.Players[playerId].TotalTimeAsCannibal += configData.CannibalS.TimerAdd;
                double newTime = dataFile.Players[playerId].TotalTimeAsCannibal;
                SaveData();
                //Puts($"Player: {player.Name}, Old Time: {oldTime}, New Time: {newTime}");
            }
        }

        private void StopCannibalTimer(IPlayer player)
        {
            string playerId = player.Id;
            if (playerTimers.TryGetValue(playerId , out var cannibalTimer))
            {
                playerTimers.Remove(playerId);
                cannibalTimer.Destroy();
            }
        }

        #endregion

        #region Item Cfg Updater
        private bool IsNotMeleeItem(string shortName)
        {
            string[] IsNotMeleeIKey = new string[]
            {
                "weapon",
                "weapon.mod",
                "pistol",
                "smg",
                "rifle",
                "lmg",
                "homingmissile",
                "hmlmg",
                "multiplegrenadelauncher",
                "bow",
                "crossbow",
                "shotgun",
                "gun",
                "grenade",
                "pistol",
                "rocket",
                "snowball",
                "flamethrower"
            };
            return IsNotMeleeIKey.Any(keyword => shortName.Contains(keyword));
        }

        private void CheckAndAddBaseWeapons()
        {
            IEnumerable<ItemDefinition> weaponDefinitions = ItemManager.itemList.Where(item => item.category == ItemCategory.Weapon);

            foreach (ItemDefinition itemDef in weaponDefinitions)
            {
                string shortName = itemDef.shortname;
                {
                    if (!IsNotMeleeItem(shortName) && !configData.CannibalS.Dmg.MeleeWeapons.ContainsKey(shortName))
                    {
                        if (!configData.CannibalS.Dmg.MeleeWeapons.ContainsKey(shortName))
                        {
                            Puts(shortName + " added to melee");
                            configData.CannibalS.Dmg.MeleeWeapons.Add(shortName , 1.0f);
                        }
                    }
                    else if (!shortName.Contains("weapon.mod") && !shortName.StartsWith("grenade.") && shortName != "snowball" && !configData.CannibalS.Dmg.RangedWeapons.ContainsKey(shortName) && !configData.CannibalS.Dmg.MeleeWeapons.ContainsKey(shortName))
                    {
                        Puts(shortName + " added to projectile");
                        configData.CannibalS.Dmg.RangedWeapons.Add(shortName , 1.0f);
                    }
                    if(shortName.Contains("weapon.mod") && !configData.CannibalS.Dmg.RangedWeapons.ContainsKey(shortName) && !configData.CannibalS.Dmg.MeleeWeapons.ContainsKey(shortName))
                    {
                        //Puts(shortName + " : Should not be added to melee or projectile");//add to debugg later
                    }
                }
            }
            SaveConf();
        }

        private bool ExcludedFood(string shortName)
        {
            string[] excludedFoodItemKeywords = new string[]
            {
                "seed.",
                "smallwaterbottle",
                "botabag",
                "seed.hemp",
                "humanmeat.cooked",
                "humanmeat.burned",
                "humanmeat.raw",
                "humanmeat.spoiled",
                "fish.anchovy",
                "fish.catfish",
                "fish.herring",
                "fish.orangeroughy",
                "fish.salmon",
                "fish.sardine",
                "fish.smallshark",
                "fish.troutsmall",
                "fish.yellowperch",
            };
            return !excludedFoodItemKeywords.Any(keyword => shortName.Contains(keyword));
        }

        private void CheckAndAddFoodItems()
        {
            IEnumerable<ItemDefinition> foodDefinitions = ItemManager.itemList.Where(item => item.category == ItemCategory.Food);
            foreach (ItemDefinition itemDef in foodDefinitions)
            {
                string shortName = itemDef.shortname;
                if (ExcludedFood(shortName) && !shortName.Contains("clone.") && !configData.CannibalS.Eat.FoodItems.ContainsKey(shortName))
                {
                    Puts($"{shortName} added to food items");
                    configData.CannibalS.Eat.FoodItems.Add(shortName , false);
                }
            }
            if (!configData.CannibalS.Eat.FoodItems.ContainsKey("water.catcher.small"))
            {
                configData.CannibalS.Eat.FoodItems.Add("water.catcher.small" , true);
                Puts($"water.catcher.small added to food items");
            }
            if (!configData.CannibalS.Eat.FoodItems.ContainsKey("water.catcher.large"))
            {
                configData.CannibalS.Eat.FoodItems.Add("water.catcher.large" , true);
                Puts($"water.catcher.large added to food items");
            } 
            /*
            if (!configData.CannibalS.Eat.FoodItems.ContainsKey("water.barrel"))
            {
                configData.CannibalS.Eat.FoodItems.Add("water.barrel" , true);
                Puts($"water.barrel added to food items");
            }
            if (!configData.CannibalS.Eat.FoodItems.ContainsKey("vehicle.2mod.fuel.tank"))
            {
                configData.CannibalS.Eat.FoodItems.Add("vehicle.2mod.fuel.tank" , true);
                Puts($"vehicle.2mod.fuel.tank added to food items");
            }
            if (!configData.CannibalS.Eat.FoodItems.ContainsKey("botabag"))
            {
                configData.CannibalS.Eat.FoodItems.Add("botabag" , false);
                Puts($"botabag added to food items");
            }
            */
            SaveConf();
        }

        #endregion

        #region API

        public T GetCannibalData<T>(IPlayer player , string dataType)
        {
            if (player == null)
            {
                Puts("Error: Player object is null.");
                return default(T);
            }

            if (dataFile == null)
            {
                LoadData();
            }

            string playerId = player.Id;

            if (dataFile.Players.ContainsKey(playerId))
            {
                PlayerData playerData = dataFile.Players[playerId];

                switch (dataType.ToLower())
                {
                    case "totaltimeascannibal":
                        return (T)Convert.ChangeType(playerData.TotalTimeAsCannibal , typeof(T));
                    case "humanmeatconsumed":
                        return (T)Convert.ChangeType(playerData.HumanMeatConsumed , typeof(T));
                    case "humanskilled":
                        return (T)Convert.ChangeType(playerData.HumansKilled , typeof(T));
                    case "cannibalskilled":
                        return (T)Convert.ChangeType(playerData.CannibalsKilled , typeof(T));
                    case "animalskilled":
                        return (T)Convert.ChangeType(playerData.AnimalsKilled , typeof(T));
                    case "npckilled":
                        return (T)Convert.ChangeType(playerData.NpcKilled , typeof(T));
                    case "woodcollected":
                        return (T)Convert.ChangeType(playerData.WoodCollected , typeof(T));
                    case "stonecollected":
                        return (T)Convert.ChangeType(playerData.StoneCollected , typeof(T));
                    case "sulfurcollected":
                        return (T)Convert.ChangeType(playerData.SulfurCollected , typeof(T));
                    case "metalcollected":
                        return (T)Convert.ChangeType(playerData.MetalCollected , typeof(T));
                    case "hqmcollected":
                        return (T)Convert.ChangeType(playerData.HQMCollected , typeof(T));
                    default:
                        Puts($"Invalid data type: {dataType}");
                        break;
                }
            }
            else
            {
                Puts($"{player.Name}'s data not found.");
            }
            return default(T);
        }

        public T GetCannibalLifeTimeData<T>(IPlayer player , string dataType)
        {
            if (cannibalLifeTimeDataFile == null) LoadCannibalLifeTimeData();

            string playerId = player.Id;

            if (cannibalLifeTimeDataFile.CannibalLifeTime.ContainsKey(playerId))
            {
                CannibalLifeTimeData lifetimeData = cannibalLifeTimeDataFile.CannibalLifeTime[playerId];

                switch (dataType.ToLower())
                {
                    case "canniballifetime":
                        return (T)Convert.ChangeType(lifetimeData.CannibalLifeTime , typeof(T));
                    case "humanmeatconsumed":
                        return (T)Convert.ChangeType(lifetimeData.HumanMeatConsumed , typeof(T));
                    case "humanskilled":
                        return (T)Convert.ChangeType(lifetimeData.HumansKilled , typeof(T));
                    case "cannibalskilled":
                        return (T)Convert.ChangeType(lifetimeData.CannibalsKilled , typeof(T));
                    case "animalskilled":
                        return (T)Convert.ChangeType(lifetimeData.AnimalsKilled , typeof(T));
                    case "npckilled":
                        return (T)Convert.ChangeType(lifetimeData.NpcKilled , typeof(T));
                    case "woodcollected":
                        return (T)Convert.ChangeType(lifetimeData.WoodCollected , typeof(T));
                    case "stonecollected":
                        return (T)Convert.ChangeType(lifetimeData.StoneCollected , typeof(T));
                    case "sulfurcollected":
                        return (T)Convert.ChangeType(lifetimeData.SulfurCollected , typeof(T));
                    case "metalcollected":
                        return (T)Convert.ChangeType(lifetimeData.MetalCollected , typeof(T));
                    case "hqmcollected":
                        return (T)Convert.ChangeType(lifetimeData.HQMCollected , typeof(T));
                    default:
                        Puts($"Invalid data type: {dataType}");
                        break;
                }
            }
            else
            {
                Puts($"{player.Name}'s lifetime data not found.");
            }
            return default(T);
        }

        public bool IsCannibal(BasePlayer player)
        {
            if (player == null)return false;
            IPlayer iplayer = covalence.Players.FindPlayerById(player.UserIDString);
            if (iplayer == null) return false;
            return IsCannibal(iplayer);
        }

        public bool IsCannibal(IPlayer player)
        {
            if (dataFile == null) LoadData();
            string playerId = player.Id;
            bool isCannibal = dataFile.Players.ContainsKey(playerId);
            return isCannibal;
        }

        public double GetTotalCannibalTime(IPlayer player)
        {
            if (player == null)
            {
                return 0.0;
            }

            double currentTime = GetCannibalTime(player);
            double storedTime = GetCannibalLifeTime(player);
            return currentTime + storedTime;
        }

        public double GetCannibalLifeTime(IPlayer player)
        {
            if (cannibalLifeTimeDataFile == null)
            {
                LoadCannibalLifeTimeData();
            }

            if (cannibalLifeTimeDataFile != null && cannibalLifeTimeDataFile.CannibalLifeTime.TryGetValue(player.Id , out CannibalLifeTimeData cannibalLifeTimeData))
            {
                return cannibalLifeTimeData.CannibalLifeTime;
            }
            return 0.0;
        }

        public double GetCannibalTime(BasePlayer player)
        {
            IPlayer iplayer = covalence.Players.FindPlayerById(player.UserIDString);
            return GetCannibalTime(iplayer);
        }

        public double GetCannibalTime(IPlayer player)
        {
            string playerId = player.Id;

            if (dataFile.Players.ContainsKey(playerId))
            {
                return dataFile.Players[playerId].TotalTimeAsCannibal;
            }
            return 0.0;
        }

        public int CountCannibals()
        {
            if (dataFile == null) LoadData();
            return dataFile.Players.Count;
        }

        public int CountOnlineCannibals()
        {
            if (dataFile == null) LoadData();
            int onlineCannibalCount = 0;
            foreach (IPlayer player in players.Connected)
            {
                if (IsCannibal(player))
                {
                    onlineCannibalCount++;
                }
            }
            return onlineCannibalCount;
        }

        public void AddPlayerAsCannibal(BasePlayer player)
        {
            IPlayer iplayer = covalence.Players.FindPlayerById(player.UserIDString);
            AddPlayerAsCannibal(iplayer);
        }

        public void AddPlayerAsCannibal(IPlayer player)
        {
            AddPlayerData(player);
        }

        public void RemovePlayerFromCannibals(IPlayer player)
        {
            RemovePlayer(player);
        }

        public void RemovePlayerFromCannibals(BasePlayer player)
        {
            IPlayer iplayer = covalence.Players.FindPlayerById(player.UserIDString);
            RemovePlayer(iplayer);
        }
        #endregion

        #region CUI HUD

        void DestroyCannibalHuds(BasePlayer player)
        {
            CuiHelper.DestroyUi(player , "CannibalUI");
        }

        [Command("CannibalHudClose")]
        private void CannibalHudClose(IPlayer player , string command , string[] args)
        {
            if (player == null) return;
            BasePlayer basePlayer = player.Object as BasePlayer;
            DestroyCannibalHuds(basePlayer);
            textRefreshTimer.Destroy();
            CuiHelper.DestroyUi(basePlayer , "CannibalUITimerPanel_" + basePlayer.userID);
        }

        void Cannibalhud(BasePlayer player)
        {
            DestroyCannibalHuds(player);
            // Define colors
            string CannibalMeat = "1 1 1 0.8";
            if (IsCannibal(player)) CannibalMeat = "1 0 0 0.8";//red
            var elements = new CuiElementContainer();

            // Main panel for the CannibalUI
            var mainPanel = elements.Add(new CuiPanel
            {
                Image = { Color = $"0 0 0 0" } ,
                RectTransform = { AnchorMin = configData.Settings.Anchormin , AnchorMax = configData.Settings.Anchormax } ,
                CursorEnabled = false
            } , "Under" , "CannibalUI");

            // Counter panel inside CannibalUI
            var counterPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" } ,
                RectTransform = { AnchorMin = "0 0" , AnchorMax = "1 1" } ,
                CursorEnabled = false
            } , mainPanel , "CounterPanel");
            // Add the icons
            AddIconElement(elements , "CounterPanel" , cannibalicon , "0 0" , "35 35" , CannibalMeat);
            AddIconElement(elements , "CounterPanel" , "assets/icons/stopwatch.png" , "35 4" , "60 31" , "1 1 1 0.8");

            CuiHelper.AddUi(player , elements);
            ShowCannibalTimer(player);
        }

        void AddTextElement(CuiElementContainer elements , string parent , string text , string offset)
        {
            elements.Add(new CuiElement
            {
                Parent = parent ,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offset,
                        OffsetMax = $"{int.Parse(offset.Split(' ')[0]) + 85} {int.Parse(offset.Split(' ')[1]) + 15}"
                    }
                }
            });
        }

        void AddIconElement(CuiElementContainer elements , string parent , string sprite , string offsetMin , string offsetMax , string color)
        {
            if (sprite.Contains("assets"))
            {
                elements.Add(new CuiElement
                {
                    Parent = parent ,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = sprite,
                            Material = "assets/icons/iconmaterial.mat",
                            Color = color,
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = offsetMin,
                            OffsetMax = offsetMax
                        }
                    }
                });
            }
            else
            {
                elements.Add(new CuiElement
                {
                    Parent = parent ,
                    Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = sprite,
                        Material = "assets/icons/iconmaterial.mat",
                        Color = color,
                        FadeIn = 0.0f
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
                });
            }
        }

        void AddImageElement(CuiElementContainer elements , string parent , string png , string offsetMin , string offsetMax , string color)
        {
            elements.Add(new CuiElement
            {
                Parent = parent ,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = png,
                        Material = "assets/icons/iconmaterial.mat",
                        Color = color,
                        FadeIn = 0.0f
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    }
                }
            });
        }

        const float TimerInterval = 1f;//1 ?
        Timer textRefreshTimer;
        bool isTimerActive = false;

        void ShowCannibalTimer(BasePlayer player)
        {
            if (isTimerActive)
            {
                UpdateCannibalTimerText(player);
                if (!IsCannibal(player))
                {
                    StopCannibalTimer(player);
                }
            }
            else
            {
                textRefreshTimer = timer.Repeat(TimerInterval , 0 , () =>
                {
                    UpdateCannibalTimerText(player);
                    if (GetRemainingTime(player.userID) == "No Buff" || !IsCannibal(player))
                    {
                        StopCannibalTimer(player);
                    }
                });
                isTimerActive = true;
            }
        }

        void StopCannibalTimer(BasePlayer player)
        {
            if (textRefreshTimer != null && !textRefreshTimer.Destroyed)
            {
                textRefreshTimer.Destroy();
            }
            DestroyCannibalTimerPanel(player);
            textRefreshTimer = null;
            isTimerActive = false;
        }

        void UpdateCannibalTimerText(BasePlayer player)
        {
            string color = "1 1 1 1";

            int remainingTime = totalBoostDurations.TryGetValue(player.userID , out var time) ? time : 0;
            if (remainingTime <= 0)
            { 
                return;
            }

            if (remainingTime <= 30 && remainingTime >= 10)
            {
                color = HexToCuiColor("#00FF00");// Green
            }

            if (remainingTime <= 10 && remainingTime >= 1)
            {
                color = HexToCuiColor("#FF0000");// Red
            }

            string remainingTimeString = GetRemainingTime(player.userID);
            CuiHelper.DestroyUi(player , "CannibalUITimerPanel_" + player.userID);
            CuiHelper.AddUi(player , new List<CuiElement>
            {
                new CuiElement
                {
                    Parent = "CounterPanel",
                    Name = "CannibalUITimerPanel_" + player.userID,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0" ,
                            AnchorMax = "1 1",
                            OffsetMin = "65 0",
                            OffsetMax = "240 0"
                        },
                        new CuiTextComponent
                        {
                            Text = remainingTimeString,
                            FontSize = 16,
                            Color = color,
                            Align = TextAnchor.MiddleLeft
                        }
                    }
                }
            });
        }

        void DestroyCannibalTimerPanel(BasePlayer player)
        {
            var panelName = "CannibalUITimerPanel_" + player.userID;
            CuiHelper.DestroyUi(player , panelName);
        }

        string GetRemainingTime(ulong userID)
        {
            if (textRefreshTimer == null || textRefreshTimer.Destroyed)
            {
                return "No Buff";
            }
            int remainingTime = totalBoostDurations.TryGetValue(userID , out var time) ? time : 0;
            return remainingTime > 0 ? $"{FormatTime(remainingTime)}" : "No Buff";
        }

        #endregion

        #region CUI Info Panel
        int currentPage = 1; // Set the default page

        private void CannibalinfoPanel(IPlayer iPlayer)
        {
            if (iPlayer.Object is BasePlayer basePlayer)
            {
                var container = new CuiElementContainer();
                var TitleColor = HexToCuiColor("#0077b5");

                CuiHelper.DestroyUi(basePlayer , "MainPanel");
                // Create the main panel
                var mainPanel = container.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0.3" } ,
                    RectTransform = { AnchorMin = "0.1 0.1" , AnchorMax = "0.9 0.9" } ,
                    CursorEnabled = true
                } , "Overlay" , "MainPanel");
                if (configData.Settings.UseBloody)
                {
                    container.Add(new CuiElement
                    {
                        Parent = "MainPanel" ,
                        Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/content/ui/overlay_bleeding.png",
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 0 0 0.65",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                    });

                }

                container.Add(new CuiPanel { Image = { Color = "1 0 0 0.8"} , RectTransform = { AnchorMin = $"0.005 0.873" , AnchorMax = $"0.995 0.876" } } , mainPanel);

                switch (currentPage)
                {
                    case 1:
                        CreatePage1(ref container , mainPanel , iPlayer);
                        break;
                    case 2:
                        CreatePage2(ref container , mainPanel , iPlayer);
                        break;
                    case 3:
                        CreatePage3(ref container , mainPanel , iPlayer);
                        break;
                    case 4:
                        CreatePage4(ref container , mainPanel , iPlayer);
                        break;
                }

                //Create the Title
                AddTextWithOutline(container , Translate(iPlayer , "InfoTitle") , 42 , "0 0.80" , "1 0.99" , TextAnchor.UpperCenter , mainPanel);

                // Create the close button
                AddTextWithOutline(container , $"<color=red>{Translate(iPlayer , "InfoClose")}</color>" , 16 , "0.025 0.85" , "0.07 0.90" , TextAnchor.MiddleCenter , mainPanel);
                container.Add(new CuiButton
                {
                    Button = { Command = "ui.close MainPanel" , Color = "1 0 0 0" , FadeIn = 0.0f } ,
                    RectTransform = { AnchorMin = "0.025 0.85" , AnchorMax = "0.07 0.90" } ,
                    Text = { Text = "" , FontSize = 16 , Align = TextAnchor.MiddleCenter }
                } , mainPanel);
                AddTextWithOutline(container , $"<color=white>{Translate(iPlayer , "InfoMain")}</color>" , 16 , "0.08 0.85" , "0.15 0.90" , TextAnchor.MiddleCenter , mainPanel);
                container.Add(new CuiButton
                {
                    Button = { Command = "CPage1" , Color = "1 0 0 0" , FadeIn = 0.0f } ,
                    RectTransform = { AnchorMin = "0.08 0.85" , AnchorMax = "0.15 0.90" } ,
                    Text = { Text = "" , FontSize = 16 , Align = TextAnchor.MiddleCenter }
                } , mainPanel);
                AddTextWithOutline(container , $"<color=white>{Translate(iPlayer , "InfoDamage")}</color>" , 16 , "0.16 0.85" , "0.24 0.90" , TextAnchor.MiddleCenter , mainPanel);
                container.Add(new CuiButton
                {
                    Button = { Command = "CPage2" , Color = "1 0 0 0" , FadeIn = 0.0f } ,
                    RectTransform = { AnchorMin = "0.16 0.85" , AnchorMax = "0.24 0.90" } ,
                    Text = { Text = "" , FontSize = 16 , Align = TextAnchor.MiddleCenter }
                } , mainPanel);
                AddTextWithOutline(container , $"<color=white>{Translate(iPlayer , "InfoStat")}</color>" , 16 , "0.25 0.85" , "0.33 0.90" , TextAnchor.MiddleCenter , mainPanel);
                container.Add(new CuiButton
                {
                    Button = { Command = "CPage3" , Color = "1 0 0 0" , FadeIn = 0.0f } ,
                    RectTransform = { AnchorMin = "0.25 0.85" , AnchorMax = "0.33 0.90" } ,
                    Text = { Text = "" , FontSize = 16 , Align = TextAnchor.MiddleCenter }
                } , mainPanel);

                AddTextWithOutline(container , $"<color=white>{Translate(iPlayer , "InfoRankings")}</color>" , 16 , "0.34 0.85" , "0.42 0.90" , TextAnchor.MiddleCenter , mainPanel);
                container.Add(new CuiButton
                {
                    Button = { Command = "CPage4" , Color = "1 0 0 0.2" , FadeIn = 0.0f } ,
                    RectTransform = { AnchorMin = "0.34 0.85" , AnchorMax = "0.42 0.90" } ,
                    Text = { Text = "" , FontSize = 16 , Align = TextAnchor.MiddleCenter }
                } , mainPanel);

                AddTextWithOutline(container , $"<color=white>{this.Title} v{this.Version}</color>" , 12 , "0.01 0.01" , "0.99 0.05" , TextAnchor.LowerRight , mainPanel);

                CuiHelper.AddUi(basePlayer , container);
            }
        }

        void CreatePage1(ref CuiElementContainer container , string mainPanel, IPlayer player)
        {
            // Page Title
            AddTextWithOutline(container , $"<color=orange>{Translate(player , "InfoTitleMain")}</color>" , 24 , "0 0.81" , "1 0.86" , TextAnchor.MiddleCenter , mainPanel);
            // Page intro
            AddTextWithOutline(container , $"{Translate(player , "InfoWelcome")} <color=orange>{player.Name}</color>." , 16 , "0 0.77" , "1 0.81" , TextAnchor.MiddleCenter , mainPanel);
            // Left panel
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.05 0.05" , AnchorMax = $"0.45 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "Main1");
            container.Add(new CuiElement
            {
                Parent = "Main1" ,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = cannibalicon,
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 1 1 0.2",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -200",
                            OffsetMax = "200 200"
                        }
                    }
            });
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoMainLeftPanelTitle")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "Main1");
            var height = 0.93;
            var heightlow = 0.89;
            AddTextWithOutline(container , Translate(player , "InfoMainLeftPanel") , 14 , "0.025 0.025" , $"0.975 {heightlow - 0.01}" , TextAnchor.UpperLeft , "Main1");

            // Right panel
            var panel2 = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.55 0.05" , AnchorMax = $"0.95 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "Main2a");
            container.Add(new CuiElement
            {
                Parent = "Main2a" ,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = cannibalicon,
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 1 1 0.2",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -200",
                            OffsetMax = "200 200"
                        }
                    }
            });
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel2);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoCmdInfoTitle")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "Main2a");
            height = 0.93;
            heightlow = 0.89;
            AddChatCommandsText(container , Translate(player , "/cannibal info") , Translate(player , "InfoCmdInfo") , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "/cannibal start") , $"<color=green>{Translate(player , "InfoCmdStart")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "/cannibal end") , $"<color=green>{Translate(player , "InfoCmdEnd")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "/cannibal time") , Translate(player , "InfoCmdTime") , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "/cannibal total") , $"<color=orange>{Translate(player , "InfoCmdTotal")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "/cannibal online") , $"<color=orange>{Translate(player , "InfoCmdOnline")}</color>" , ref height , ref heightlow);
            AddTextWithOutline(container , Translate(player , $"{Translate(player , "All")} <color=green>{Translate(player , "Permission")}</color> <color=orange>{Translate(player , "Admin")}</color>") , 14 , $"0.025 {heightlow}" , $"0.975 {height}" , TextAnchor.UpperLeft , "Main2a");
            height -= 0.05;
            heightlow -= 0.05;

            //InfoGatheringDescription
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 {heightlow - 0.01}" , AnchorMax = $"1 {height}" } } , panel2);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoGatheringTitle")}</color>" , 16 , $"0 {heightlow - 0.01}" , $"1 {height}" , TextAnchor.MiddleCenter , "Main2a");
            height -= 0.08;
            heightlow -= 0.08;
            AddChatCommandsText(container , Translate(player , "Wood") , $"{configData.CannibalS.Gather.GWood * 100 - 100}% <color=white>{Translate(player , $"InfoGatheringDescription")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "Stones") , $"{configData.CannibalS.Gather.GStone * 100 - 100}% <color=white>{Translate(player , $"InfoGatheringDescription")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "Sulfur") , $"{configData.CannibalS.Gather.GSulfur * 100 - 100}% <color=white>{Translate(player , $"InfoGatheringDescription")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "Metal") , $"{configData.CannibalS.Gather.GMetal * 100 - 100}% <color=white>{Translate(player , $"InfoGatheringDescription")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "HQM") , $"{configData.CannibalS.Gather.GHQM * 100 - 100}% <color=white>{Translate(player , $"InfoGatheringDescription")}</color>" , ref height , ref heightlow);
            AddChatCommandsText(container , Translate(player , "InfoStatMeats") , $"<color=white>{Translate(player , "N/a")}</color>" , ref height , ref heightlow);
        }

        void CreatePage2(ref CuiElementContainer container , string mainPanel ,IPlayer player)
        {
            AddTextWithOutline(container , $"<color=orange>{Translate(player , "InfoWeaponTitle")}</color>" , 24 , "0 0.81" , "1 0.86" , TextAnchor.MiddleCenter , mainPanel);
            CreateWeaponPanel(container , "InfoWeaponTitleMelee" , configData.CannibalS.Dmg.MeleeWeapons , 0 , mainPanel , player);
            CreateWeaponPanel(container , "InfoWeaponTitleRanged" , configData.CannibalS.Dmg.RangedWeapons , 1 , mainPanel, player);
        }

        void CreatePage3(ref CuiElementContainer container , string mainPanel , IPlayer player)
        {
            // Time Checks
            double GetTime = GetCannibalData<double>(player , "totalTimeAsCannibal");
            var formattedTime = FormatTimeFilter(GetTime);
            string CannibalTime = $"{formattedTime}";
            double GetTotalTime = GetCannibalLifeTimeData<double>(player , "cannibalLifeTime");
            double BothTimes = GetTotalTime + GetTime;
            var formattedTotalTime = FormatTimeFilter(BothTimes);
            if (GetTime == 0) CannibalTime = Translate(player , "N/a");
            if (GetTotalTime == 0) formattedTotalTime = Translate(player , "N/a");
            // Human kill Checks
            int HumanKills = GetCannibalData<int>(player , "humanskilled");
            int HumanKillsLife = GetCannibalLifeTimeData<int>(player , "humanskilled");
            int TotalHumanKills = HumanKills + HumanKillsLife;
            // Npc kill Checks
            int NpcKills = GetCannibalData<int>(player , "npckilled");
            int NpcKillsLife = GetCannibalLifeTimeData<int>(player , "npckilled");
            int TotalNpcKills = NpcKills + NpcKillsLife;
            // Cannibal kill Checks
            int CannibalKills = GetCannibalData<int>(player , "cannibalskilled");
            int CannibalKillsLife = GetCannibalLifeTimeData<int>(player , "cannibalsKilled");
            int TotalCannibalKills = CannibalKills + CannibalKillsLife;
            // Animal kill Checks
            int AnimalKills = GetCannibalData<int>(player , "animalskilled");
            int AnimallKillsLife = GetCannibalLifeTimeData<int>(player , "animalskilled");
            int TotalAnimalKills = AnimalKills + AnimallKillsLife;
            // Consumed meat Checks
            int meatConsumed = GetCannibalData<int>(player , "humanmeatconsumed");
            int meatConsumedlife = GetCannibalLifeTimeData<int>(player , "humanmeatconsumed");
            int TotalMeatConsumed = meatConsumed + meatConsumedlife;
            // Wood Collected checks
            int woodcollected = GetCannibalData<int>(player , "woodcollected");
            int woodcollectedlife = GetCannibalLifeTimeData<int>(player , "woodcollected");
            int TotalWoodCollected = woodcollected + woodcollectedlife;
            // Stone Collected checks
            int stonecollected = GetCannibalData<int>(player , "stonecollected");
            int stonecollectedlife = GetCannibalLifeTimeData<int>(player , "stonecollected");
            int TotalStoneCollected = stonecollected + stonecollectedlife;
            // Sulfur Collected checks
            int sulfurcollected = GetCannibalData<int>(player , "sulfurcollected");
            int sulfurcollectedlife = GetCannibalLifeTimeData<int>(player , "sulfurcollected");
            int TotalSulfurCollected = sulfurcollected + sulfurcollectedlife;
            // Metal Collected checks
            int metalcollected = GetCannibalData<int>(player , "metalcollected");
            int metalcollectedlife = GetCannibalLifeTimeData<int>(player , "metalcollected");
            int TotalMetalCollected = metalcollected + metalcollectedlife;
            // HQM Collected checks
            int hqmcollected = GetCannibalData<int>(player , "hqmcollected");
            int hqmcollectedlife = GetCannibalLifeTimeData<int>(player , "hqmcollected");
            int TotalHQMCollected = hqmcollected + hqmcollectedlife;
            string CountWColor = "white";
            string CountStColor = "white";
            string CountSuColor = "white";
            string CountMColor = "white";
            if (woodcollected < 0) CountWColor = "red";
            if (stonecollected < 0) CountStColor = "red";
            if (sulfurcollected < 0) CountSuColor = "red";
            if (metalcollected < 0) CountMColor = "red";
            // Page Title
            AddTextWithOutline(container , $"<color=orange>{Translate(player , "InfoTitleStat")}</color>" , 24 , "0 0.81" , "1 0.86" , TextAnchor.MiddleCenter , mainPanel);

            // Stats panel
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.05 0.05" , AnchorMax = $"0.45 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "Stats");
            container.Add(new CuiElement
            {
                Parent = "Stats" ,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/icons/stopwatch.png",
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 1 1 0.2",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -200",
                            OffsetMax = "200 200"
                        }
                    }
            });
            // Stats Content
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , "Stats");
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoStatDescription")}</color>" , 16 , "0.025 0.945" , "0.40 0.995" , TextAnchor.MiddleLeft , "Stats");
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoStatCurrent")}</color>" , 16 , "0.41 0.945" , "0.69 0.995" , TextAnchor.MiddleCenter , "Stats");
            AddTextWithOutline(container , $"<color=white>{Translate(player , "InfoStatTotal")}</color>" , 16 , "0.71 0.945" , "0.995 0.995" , TextAnchor.MiddleCenter , "Stats");

            var height = 0.93;
            var heightlow = 0.89;

            AddStatPanelWithText(container , Translate(player , "InfoStatTime") , CannibalTime , formattedTotalTime , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatMeats") , $"{meatConsumed}" , $"{TotalMeatConsumed}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatKillHuman") , $"{HumanKills}" , $"{TotalHumanKills}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatKillCannibal") , $"{CannibalKills}" , $"{TotalCannibalKills}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatKillAnimal") , $"{AnimalKills}" , $"{TotalAnimalKills}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatKillNpc") , $"{NpcKills}" , $"{TotalNpcKills}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatWood") , $"<color={CountWColor}>{woodcollected}</color>" , $"{TotalWoodCollected}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatStone") , $"<color={CountStColor}>{stonecollected}</color>" , $"{TotalStoneCollected}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatSulfur") , $"<color={CountSuColor}>{sulfurcollected}</color>" , $"{TotalSulfurCollected}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatMetal") , $"<color={CountMColor}>{metalcollected}</color>" , $"{TotalMetalCollected}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "InfoStatHQM") , $"{hqmcollected}" , $"{TotalHQMCollected}" , ref height , ref heightlow);
            AddStatPanelWithText(container , Translate(player , "PlaceHolder") , Translate(player , "N/a") , Translate(player , "N/a") , ref height , ref heightlow);

            AddTextWithOutline(container , Translate(player , "InfoStatSubtext") , 14 , "0.025 0.025" , $"0.975 {heightlow - 0.01}" , TextAnchor.LowerLeft , "Stats");
        }

        void CreatePage4(ref CuiElementContainer container , string mainPanel , IPlayer player)
        {
            if (player == null)
            {
                // Handle the case where player is null (log an error, return, etc.)
                Puts("Player is null in CreatePage4 method.");
                return;
            }

            AddTextWithOutline(container , $"<color=orange>{Translate(player , "InfoRankingsTitle")}</color>" , 24 , "0 0.81" , "1 0.86" , TextAnchor.MiddleCenter , mainPanel);
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.005 0.05" , AnchorMax = $"0.20 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "RankMeat");
            var panel2 = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.205 0.05" , AnchorMax = $"0.40 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "RankHKill");
            var panel3 = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.405 0.05" , AnchorMax = $"0.60 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "RankCKill");
            var panel4 = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.605 0.05" , AnchorMax = $"0.80 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "RankNPCKill");
            var panel5 = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"0.805 0.05" , AnchorMax = $"0.995 0.76" } ,
                CursorEnabled = true
            } , mainPanel , "RankAnimalKill");

            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "Top Eaters")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "RankMeat");

            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel2);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "Top Slayers")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "RankHKill");

            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel3);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "Top Traitors")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "RankCKill");

            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel4);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "Top PVE")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "RankNPCKill");

            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel5);
            AddTextWithOutline(container , $"<color=white>{Translate(player , "Top Butchers")}</color>" , 16 , "0 0.945" , "1 1" , TextAnchor.MiddleCenter , "RankAnimalKill");

            AddRanking(container , panel , "humanmeatconsumed" , "assets/icons/meat.png");
            AddRanking(container , panel2 , "humanskilled", "assets/icons/weapon.png");
            AddRanking(container , panel3 , "cannibalskilled", "assets/icons/demolish.png");
            AddRanking(container , panel4 , "npckilled" , "assets/content/ui/hypnotized.png");
            AddRanking(container , panel5 , "animalskilled" , "assets/icons/food_raw.png");

        }
        private void AddRanking(CuiElementContainer container , string ranking , string Type ,string image)
        {
            container.Add(new CuiElement
            {
                Parent = ranking ,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = image,
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 1 1 0.2",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-100 -100",
                            OffsetMax = "100 100"
                        }
                    }
            });
            // Create a dictionary to store players and their values
            Dictionary<string , int> playerValues = new Dictionary<string , int>();

            // Populate the dictionary with player values
            foreach (var playerEntry in dataFile.Players)
            {
                var playerId = playerEntry.Key;
                var playerData = playerEntry.Value;

                // Determine the value based on the specified type
                int valueAmount = 0;
                switch (Type)
                {
                    case "humanmeatconsumed":
                        valueAmount = playerData.HumanMeatConsumed;
                        break;
                    case "humanskilled":
                        valueAmount = playerData.HumansKilled;
                        break;
                    case "cannibalskilled":
                        valueAmount = playerData.CannibalsKilled;
                        break;
                    case "npckilled":
                        valueAmount = playerData.NpcKilled;
                        break;
                    case "animalskilled":
                        valueAmount = playerData.AnimalsKilled;
                        break;
                        // Add more cases for other types as needed
                }

                // Add player and value to the dictionary
                playerValues.Add(playerId , valueAmount);
            }

            // Order the dictionary by values in descending order
            var topPlayers = playerValues.OrderByDescending(pair => pair.Value);

            int rank = 1;
            var height = 0.93;
            var heightlow = 0.89;
            string titleColor = "white";

            foreach (var topPlayer in topPlayers.Take(10))
            {
                var playerId = topPlayer.Key;
                if (dataFile.Players.TryGetValue(playerId , out var playerData))
                {
                    if (rank == 1) titleColor = "orange";
                    if (rank == 2) titleColor = "magenta";
                    if (rank == 3) titleColor = "lightblue";
                    if (rank >= 4) titleColor = "white";
                    var playerName = playerData.DisplayName ?? "Unknown";

                    // Display player rank, name, and value for the specified type
                    AddTextWithOutline(container , $"{rank} <color={titleColor}>{playerName}</color> - ({topPlayer.Value})" , 14 , $"0 {heightlow}" , $"1 {height}" , TextAnchor.MiddleCenter , ranking);
                }
                else
                {
                    Puts($"Player data not found for {playerId} in CreatePage4 method.");
                }

                rank++;
                height -= 0.05;
                heightlow -= 0.05;
            }

            // Add blank entries for the remaining lines (up to 5)
            for (int i = rank; i <= 5; i++)
            {
                AddTextWithOutline(container , $"<color=grey>{i}. Open position. </color>" , 14 , $"0 {heightlow}" , $"1 {height}" , TextAnchor.MiddleCenter , ranking);
                height -= 0.05;
                heightlow -= 0.05;
            }
        }

        private void AddChatCommandsText(CuiElementContainer container , string labelText , string valueText , ref double height , ref double heightLow)
        {
            container.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.7" } , RectTransform = { AnchorMin = $"0.01 {heightLow}" , AnchorMax = $"0.29 {height}" } , CursorEnabled = true } , "Main2a");
            AddTextWithOutline(container , $"{labelText}" , 14 , $"0.025 {heightLow}" , $"0.305 {height}" , TextAnchor.MiddleLeft , "Main2a");

            container.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.7" } , RectTransform = { AnchorMin = $"0.30 {heightLow}" , AnchorMax = $"0.990 {height}" } , CursorEnabled = true } , "Main2a");
            AddTextWithOutline(container , $"<color=white>{valueText}</color>" , 12 , $"0.31 {heightLow}" , $"0.995 {height}" , TextAnchor.MiddleLeft , "Main2a");

            height -= 0.05;
            heightLow -= 0.05;
        }

        private void AddStatPanelWithText(CuiElementContainer container , string labelText , string valueText , string valueText2 , ref double height , ref double heightLow)
        {
            container.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.7" } , RectTransform = { AnchorMin = $"0.01 {heightLow}" , AnchorMax = $"0.39 {height}" } , CursorEnabled = true } , "Stats");
            AddTextWithOutline(container , $"{labelText}" , 14 , $"0.025 {heightLow}" , $"0.405 {height}" , TextAnchor.MiddleLeft , "Stats");

            container.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.7" } , RectTransform = { AnchorMin = $"0.40 {heightLow}" , AnchorMax = $"0.690 {height}" } , CursorEnabled = true } , "Stats");
            AddTextWithOutline(container , $"<color=white>{valueText}</color>" , 14 , $"0.41 {heightLow}" , $"0.695 {height}" , TextAnchor.MiddleCenter , "Stats");

            container.Add(new CuiPanel { Image = { Color = "0.1 0.1 0.1 0.7" } , RectTransform = { AnchorMin = $"0.70 {heightLow}" , AnchorMax = $"0.990 {height}" } , CursorEnabled = true } , "Stats");
            AddTextWithOutline(container , $"<color=white>{valueText2}</color>" , 14 , $"0.71 {heightLow}" , $"0.990 {height}" , TextAnchor.MiddleCenter , "Stats");

            height -= 0.05;
            heightLow -= 0.05;
        }

        float spacing = 0.01f; // Adjust the spacing value

        private void CreateWeaponPanel(CuiElementContainer container , string title , Dictionary<string , float> weapons , int column , string parentPanel ,IPlayer player)
        {

            // Colors
            string ScaleColor = "white";

            // Calculate position based on the column
            float positionX = column * 0.5f + 0.05f;

            // Create the panel
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.45" } ,
                RectTransform = { AnchorMin = $"{positionX} 0.05" , AnchorMax = $"{positionX + 0.4f} 0.76" } ,
                CursorEnabled = true
            } , parentPanel , $"{title}Panel");
            container.Add(new CuiElement
            {
                Parent = $"{title}Panel" ,
                Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/icons/weapon.png",
                            Material = "assets/icons/iconmaterial.mat",
                            Color = "1 1 1 0.2",
                            FadeIn = 0.0f
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-200 -200",
                            OffsetMax = "200 200"
                        }
                    }
            });
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0.8" } , RectTransform = { AnchorMin = $"0 0.945" , AnchorMax = $"1 1" } } , panel);
            // Add title text using your current text conversion method
            AddTextWithOutline(container , Translate(player , $"{title}") , 16 , "0 0.80" , "1 0.99" , TextAnchor.UpperCenter , panel);

            // Add weapon entries
            float offsetY = 0.93f; // Starting height for the first line
            float lineHeight = 0.038f; // Adjust this value for proper spacing
            int entryCount = 0;
            int maxEntriesPerColumn = 19; // Set the maximum entries per column
            float offsetX = 0.05f; // Starting offset for the first column

            foreach (var weapon in weapons)
            {
                if (weapon.Value > 1) ScaleColor = "green";
                if (weapon.Value < 1) ScaleColor = "red";
                if (weapon.Value == 1) ScaleColor = "white";

                // Create a panel with the same anchors as the text
                var textPanel = container.Add(new CuiPanel
                {
                    Image = { Color = "0.1 0.1 0.1 0.7" } ,
                    RectTransform = { AnchorMin = $"{offsetX} {offsetY - lineHeight * (entryCount + 1)}" , AnchorMax = $"{offsetX + 0.4f} {offsetY - lineHeight * entryCount}" } ,
                    CursorEnabled = true
                } , panel);
                int result = (int)Math.Floor(weapon.Value * 100 - 100);
                string roundedpercentage = $"{result}%";
                if (result == 0) roundedpercentage = "";
                // Add weapon entry text with outline
                AddTextWithOutline(container , $"  {weapon.Key}  <color={ScaleColor}>{roundedpercentage}</color>" , 14 , $"{offsetX} {offsetY - lineHeight * (entryCount + 1)}" , $"{offsetX + 0.4f} {offsetY - lineHeight * entryCount}" , TextAnchor.MiddleLeft , panel);

                offsetY -= spacing; // Adjust the spacing value
                entryCount++;

                // Check if the maximum entries per column is reached
                if (entryCount >= maxEntriesPerColumn)
                {
                    // Reset the counter and move to the next column
                    entryCount = 0;
                    positionX += 0.5f; // Adjust this value for the spacing between columns
                    offsetX += 0.5f; // Adjust this value for the spacing between columns
                    offsetY = 0.93f; // Reset starting height for the first line
                }
            }
        }

        private void AddTextWithOutline(CuiElementContainer container , string text , int fontSize , string anchorMin , string anchorMax , TextAnchor align , string parentPanel)
        {
            container.Add(new CuiElement
            {
                Parent = parentPanel ,
                Components =
                {
                    new CuiTextComponent { Text = text, FontSize = fontSize, Align = align, Color = "1 1 1 1", FadeIn = 0.0f },
                    new CuiOutlineComponent { Color = "0.1 0.1 0.1 1", Distance = "0.5 0.5" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1.5 1.5" },
                    new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }
            });
        }

        [Command("CPage1")]
        private void CPage1(IPlayer player , string command , string[] args)
        {
            currentPage = 1;
            CannibalinfoPanel(player);
        }
        [Command("CPage2")]
        private void CPage2(IPlayer player , string command , string[] args)
        {
            currentPage = 2;
            CannibalinfoPanel(player);
        }
        [Command("CPage3")]
        private void CPage3(IPlayer player , string command , string[] args)
        {
            currentPage = 3;
            CannibalinfoPanel(player);
        }
        [Command("CPage4")]
        private void CPage4(IPlayer player , string command , string[] args)
        {
            currentPage = 4;
            CannibalinfoPanel(player);
        }
        [Command("ui.close")]
        private void CloseUICommand(IPlayer iPlayer , string command , string[] args)
        {
            // Check the title of the panel to close
            string panelTitle = args.Length > 0 ? args[0] : string.Empty;

            if (iPlayer.Object is BasePlayer basePlayer)
            {
                // Destroy the specified panel
                CuiHelper.DestroyUi(basePlayer , panelTitle);
                currentPage = 1;
            }
        }

        #endregion

        #region Helpers

        private static bool IsZombieHorde(BasePlayer player) => player.GetType().Name.Equals("ZombieNPC");

        private float GetMultiplierForItem(string itemShortname)
        {
            switch (itemShortname)
            {
                case "wood":
                    return configData.CannibalS.Gather.GWood;
                case "stones":
                    return configData.CannibalS.Gather.GStone;
                case "metal.ore":
                    return configData.CannibalS.Gather.GMetal;
                case "sulfur.ore":
                    return configData.CannibalS.Gather.GSulfur;
                case "hq.metal.ore":
                    return configData.CannibalS.Gather.GHQM;
                case "metal.refined":
                    return configData.CannibalS.Gather.GHQM;
                default:
                    return 1f; // Default multiplier
            }
        }

        private void SendFX(BasePlayer player , string FXstring)
        {
            Effect.server.Run(FXstring , player.ServerPosition + new Vector3(0 , 1 , 0));
        }

        private string Translate(IPlayer player , string key)
        {
            string Translation = lang.GetMessage(key , this , player.Id);
            return Translation;
        }

        bool IsMeleeWeapon(string itemShortName , BaseEntity weaponEntity)
        {
            if (itemShortName == null || weaponEntity == null) return false;
            bool isMelee = weaponEntity is BaseMelee;
            bool isListedMelee = configData.CannibalS.Dmg.MeleeWeapons.ContainsKey(itemShortName);
            return isMelee && isListedMelee;
        }

        bool IsRangedWeapon(string itemShortName , BaseEntity weaponEntity)
        {
            if (itemShortName == null || weaponEntity == null) return false;
            bool isRanged = weaponEntity is BaseProjectile;
            bool isListedRanged = configData.CannibalS.Dmg.RangedWeapons.ContainsKey(itemShortName);
            return isRanged && isListedRanged;
        }

        public string HexToCuiColor(string hex)/// needs some work on the alpha part
        {
            hex = hex.TrimStart('#');
            int r = int.Parse(hex.Substring(0 , 2) , System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2 , 2) , System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4 , 2) , System.Globalization.NumberStyles.HexNumber);

            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;

            return $"{rf} {gf} {bf} 1";
        }

        bool HasPermission(IPlayer player , string perm)
        {
            if (player is BasePlayer rustPlayer)
            {
                return permission.UserHasPermission(rustPlayer.UserIDString , perm);
            }
            else
            {
                return permission.UserHasPermission(player.Id , perm);
            }
        }

        bool HasPerm(IPlayer player , string perm) => HasPermission(player , perm);

        void SendMessage(object playerObject , string messageKey)
        {
            string prefix = configData.Prefix;
            string text;

            if (playerObject is BasePlayer)
            {
                BasePlayer basePlayer = playerObject as BasePlayer;
                text = $"{prefix} {lang.GetMessage(messageKey , this , basePlayer.UserIDString)}";
                basePlayer.SendConsoleCommand("chat.add" , 2 , SteamIDIcon , text);
            }
            else if (playerObject is IPlayer)
            {
                IPlayer iPlayer = playerObject as IPlayer;
                text = $"{prefix} {lang.GetMessage(messageKey , this , iPlayer.Id)}";
                (iPlayer.Object as BasePlayer).SendConsoleCommand("chat.add" , 2 , SteamIDIcon , text);
            }
        }

        // Function to format time into days, hours, minutes, and seconds
        private TimeSpan FormatTime(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        private string FormatTimeFilter(double seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            string formattedTime = $"{(timeSpan.TotalDays >= 1 ? (int)timeSpan.TotalDays + "d" : "")}" +
                                   $"{(timeSpan.Hours > 0 ? (int)timeSpan.Hours + "h" : "")}" +
                                   $"{(timeSpan.Minutes > 0 ? timeSpan.Minutes + "m" : "")}" +
                                   $"{(timeSpan.Seconds > 0 ? timeSpan.Seconds + "s" : "")}";
            return formattedTime;
        }

        #endregion
    }
}