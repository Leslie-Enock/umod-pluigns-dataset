using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Facepunch;
using Rust;
using Rust.Ai.Gen2;

namespace Oxide.Plugins
{
    [Info("RadTownLoot", "Krungh Crow", "2.2.2")]
    [Description("Return of Radanimals with animal settings")]

    #region Changelogs and ToDo < 2.0.0
    /**********************************************************************
    * 
    * v1.1.2    :   Changed spawnchance from 0-1 to 0-100 (to make it more clear)
    * v1.1.3    :   Added check if initiator is player or npc
    * v1.1.3    :   Added additional checks
    * v1.1.4    :   Added nullchecks (OnEntityDeath)
    * v1.1.5    :   Fix for wolf NRE
    * v1.1.6    :   Better Null check
    * 
    **********************************************************************/
    #endregion

    #region Changelogs and ToDo > 2.0.0
    /**********************************************************************
    * 
    * 2.0.0     :   Rewrite
    *           :   Optimised Hook Calls
    *           :   Backpacks show animal type
    *           :   Changed permissions
    *           :   Added support for Alpha and Omega animal types
    *           :   Extended internal Debug System
    * 2.1.0     :   Added Boars
    *           :   Added Chickens
    *           :   Added Stags
    *           :   Added Wild Horses
    *           :   Added Animal Type Prefix (lootcontainer title) in cfg
    * 2.2.0     :   Fixed Animal names
    *           :   Fixed Animal Spawn values
    *           :   Updated language file , backup old file and delete
    *           :   Changed /rad animals command info
    *           :   Added radtownloot.admin permission
    *           :   Added /rad admin command with extended cfg info
    * 2.2.1     :   Added support for the new wolf AI (wolf2)
    * 2.2.2     :   Patched for feb 6 rust update
    * 
    **********************************************************************/
    #endregion

    class RadTownLoot : RustPlugin
    {
        [PluginReference]
        Plugin Clans, Friends;

        #region Variables

        string Admin_Perm = "radtownloot.admin";
        string Chat_Perm = "radtownloot.chat";
        string Command_Perm = "radtownloot.command";
        string Loot_Perm = "radtownloot.loot";

        ulong chaticon = 0;
        string prefix;
        string animalprefix;
        bool Debug = false;

        bool IgnoreAlpha;
        bool IgnoreOmega;

        int HealthMin;
        int HealthMax;
        int RandomHealth;
        int DamageMin;
        int DamageMax;
        int RandomDamage;
        float Speed;
        bool ShowConsole = false;
        bool ChangeValues;

        #endregion

        #region Configuration

        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts($"Config file ({this.Name}.json) issue detected. Please check syntax and fix.");
                return;
            }
            permission.RegisterPermission(Admin_Perm, this);
            permission.RegisterPermission(Chat_Perm, this);
            permission.RegisterPermission(Command_Perm, this);
            permission.RegisterPermission(Loot_Perm, this);

            Debug = configData.PlugCFG.Debug;
            IgnoreAlpha = configData.Animals.IgnoreAlpha;
            IgnoreOmega = configData.Animals.IgnoreOmega;
            prefix = configData.PlugCFG.Prefix;
            animalprefix = configData.PlugCFG.AnimalPrefix;
            chaticon = configData.PlugCFG.Chaticon;
            if (Debug) Puts($"[Debug]  Debug for [{this.Name}] is active if unintentional change cfg and reload");
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Main config")]
            public SettingsPlugin PlugCFG = new SettingsPlugin();
            [JsonProperty(PropertyName = "Animal config")]
            public SettingsAnimals Animals = new SettingsAnimals();
        }

        class SettingsPlugin
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong Chaticon = 0;
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "[<color=orange>RadTownLoot</color>] : ";
            [JsonProperty(PropertyName = "Animal Type Prefix")]
            public string AnimalPrefix = "Radtown ";
            [JsonProperty(PropertyName = "Use Random Skins")]
            public bool RandomSkins = false;
        }

        class SettingsAnimals
        {
            [JsonProperty(PropertyName = "Skip Alpha Animals")]
            public bool IgnoreAlpha = true;
            [JsonProperty(PropertyName = "Skip Omega Animals")]
            public bool IgnoreOmega = true;
            [JsonProperty(PropertyName = "Bear settings")]
            public Spawns BearSpawns = new Spawns();
            [JsonProperty(PropertyName = "Boar settings")]
            public Spawns BoarSpawns = new Spawns();
            [JsonProperty(PropertyName = "Chicken settings")]
            public Spawns ChickenSpawns = new Spawns();
            [JsonProperty(PropertyName = "Polarbear settings")]
            public Spawns PBearSpawns = new Spawns();
            [JsonProperty(PropertyName = "Stag settings")]
            public Spawns StagSpawns = new Spawns();
            [JsonProperty(PropertyName = "Wolf settings")]
            public Spawns WolfSpawns = new Spawns();
        }

        class Spawns
        {
            [JsonProperty(PropertyName = "Change stats on spawns")]
            public bool Change = false;
            [JsonProperty(PropertyName = "Show spawns in Console")]
            public bool ShowConsole = false;
            [JsonProperty(PropertyName = "Droprate 0-100")]
            public float ChanceOfCrate = 10.0f;
            [JsonProperty(PropertyName = "Minimum Health")]
            public int Healthmin = 150;
            [JsonProperty(PropertyName = "Maximum Health")]
            public int Healthmax = 250;
            [JsonProperty(PropertyName = "Minimum Strength (Att dmg)")]
            public int Damage = 20;
            [JsonProperty(PropertyName = "Maximum Strength (Att dmg")]
            public int DamageMax = 25;
            [JsonProperty(PropertyName = "Running Speed")]
            public float Speed = 6f;
            [JsonProperty(PropertyName = "Loot settings")]
            public LootSettings Loots = new LootSettings();
        }

        class LootSettings
        {
            [JsonProperty(PropertyName = "Spawn Min Amount Items")]
            public int MinAmount = 1;
            [JsonProperty(PropertyName = "Spawn Max Amount Items")]
            public int MaxAmount = 3;
            [JsonProperty(PropertyName = "Loot Table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItems> Loot { get; set; } = DefaultLoot;
        }

        private bool LoadConfigVariables()
        {
            try { configData = Config.ReadObject<ConfigData>(); }
            catch { return false; }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }
        void SaveConf() => Config.WriteObject(configData, true);

        #endregion

        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Info"] = "Animals can drop backpacks like in Legacy Rust.\n\n",
                ["AdminInfo"] = "If Change stats on spawn is false values are not changed by this plugin.\n\n",
                ["InvalidInput"] = "Please enter a valid command!",
                ["NoPermission"] = "You do not have permission to use that command!",
                ["RadTownLoot"] = "The {0} dropped something!",
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("rad")]
        private void cmdRad(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player, Command_Perm))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString), chaticon);
                if (Debug) Puts($"[Debug] {player} had no permission for using Commands");
                return;
            }
            if (args.Length == 0) Player.Message(player, msg("InvalidInput", player.UserIDString), chaticon);
            else
            {
                if (args[0].ToLower() == "admin")
                {
                    if (!HasPerm(player, Admin_Perm))
                    {
                        Player.Message(player, msg("NoPermission", player.UserIDString), chaticon);
                        if (Debug) Puts($"[Debug] {player} had no permission for using the /rad admin command.");
                        return;
                    }
                    Player.Message(player, string.Format(msg("AdminInfo", player.UserIDString))
                    //Bear Info
                    + info("<color=orange>Bear</color> : Pop <color=purple>") + Bear.Population.ToString() + info("</color> ")
                    + info("Health <color=green>") + configData.Animals.BearSpawns.Healthmin.ToString() + info("</color>/<color=red>") + configData.Animals.BearSpawns.Healthmax.ToString() + info("</color>")
                    + info($" Alive <color=green> {AnimalCount("Bear")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.BearSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.BearSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.BearSpawns.Loots.MaxAmount}</color>\n")

                    //Polarbear info
                    + info("\n<color=orange>Polarbear</color> : Pop <color=purple>") + Polarbear.Population.ToString() + info("</color> ")
                    + info($"Health <color=green> {configData.Animals.PBearSpawns.Healthmin.ToString()}</color>/<color=red>{configData.Animals.PBearSpawns.Healthmax.ToString()}</color>")
                    + info($" Alive <color=green> {AnimalCount("Polarbear")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.PBearSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.PBearSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.BearSpawns.Loots.MaxAmount}</color>\n")

                    //Wolf Info
                    + info("\n<color=orange>Wolf</color> : Pop <color=purple>") + Wolf.Population.ToString() + info("</color> ")
                    + info($"Health <color=green> {configData.Animals.WolfSpawns.Healthmin.ToString()}</color>/<color=red>{configData.Animals.WolfSpawns.Healthmax.ToString()}</color>")
                    + info($" Alive <color=green> {AnimalCount("Wolf")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.WolfSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.WolfSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.WolfSpawns.Loots.MaxAmount}</color>\n")

                    //Boar info
                    + info("\n<color=orange>Boar</color> : Pop <color=purple>") + Boar.Population.ToString() + info("</color> ")
                    + info($"Health <color=green> {configData.Animals.BoarSpawns.Healthmin.ToString()}</color>/<color=red>{configData.Animals.BoarSpawns.Healthmax.ToString()}</color>")
                    + info($" Alive <color=green> {AnimalCount("Boar")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.BoarSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.BoarSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.BoarSpawns.Loots.MaxAmount}</color>\n")

                    //Chicken info
                    + info("\n<color=orange>Chicken</color> : Pop <color=purple>") + Chicken.Population.ToString() + info("</color> ")
                    + info($"Health <color=green> {configData.Animals.ChickenSpawns.Healthmin.ToString()}</color>/<color=red>{configData.Animals.ChickenSpawns.Healthmax.ToString()}</color>")
                    + info($" Alive <color=green> {AnimalCount("Chicken")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.ChickenSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.ChickenSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.ChickenSpawns.Loots.MaxAmount}</color>\n")

                    //Stag info
                    + info("\n<color=orange>Stag</color> : Pop <color=purple>") + Stag.Population.ToString() + info("</color> ")
                    + info($"Health <color=green> {configData.Animals.StagSpawns.Healthmin.ToString()}</color>/<color=red>{configData.Animals.StagSpawns.Healthmax.ToString()}</color>")
                    + info($" Alive <color=green> {AnimalCount("Stag")}</color> ")
                    + info($"\nChange stats on spawns : <color=green>{configData.Animals.StagSpawns.Change}</color>\n")
                    + info($"Droprate 0-100 : <color=green>{configData.Animals.StagSpawns.ChanceOfCrate}</color>%\n")
                    + info($"Max items : <color=green>{configData.Animals.StagSpawns.Loots.MaxAmount}</color>")
                    , chaticon);
                    return;
                }
                if (args[0].ToLower() == "animals")
                {
                    Player.Message(player, string.Format(msg("Info", player.UserIDString))
                    //Bear Info
                    + info("<color=orange>Bears</color>") + info($" : <color=green> {AnimalCount("Bear")}</color> ")
                    //Polarbear info
                    + info("\n<color=orange>Polarbears</color>") + info($" : <color=green> {AnimalCount("Polarbear")}</color> ")
                    //Wolf Info
                    + info("\n<color=orange>Wolves</color>") + info($" : <color=green> {AnimalCount("Wolf")}</color> ")
                    //Boar info
                    + info("\n<color=orange>Boars</color>") + info($" : <color=green> {AnimalCount("Boar")}</color> ")
                    //Chicken info
                    + info("\n<color=orange>Chickens</color>") + info($" : <color=green> {AnimalCount("Chicken")}</color> ")
                    //Stag info
                    + info("\n<color=orange>Stags</color>") + info($" : <color=green> {AnimalCount("Stag")}</color> ")
                    , chaticon);
                    return;
                }

                Player.Message(player, msg("InvalidInput", player.UserIDString), chaticon);
            }
        }
        #endregion

        #region Message helpers

        private string msg(string key, string id = null) => prefix + lang.GetMessage(key, this, id);

        private string info(string key, string id = null) => lang.GetMessage(key, this, id);

        void TIP(BasePlayer player, string message, float dur)
        {
            if (player == null) return;
            string msg = info(message);//takes the message from languagefile
            player.SendConsoleCommand("gametip.showgametip", msg);
            timer.Once(dur, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        #endregion

        #region Oxide Hooks

        void OnEntityDeath(BaseNPC2 animal , HitInfo info)
        {
            if (animal == null || info == null) return;
            if ((animal.name.Contains("Alpha") && IgnoreAlpha) || (animal.name.Contains("Omega") && IgnoreOmega))
            {
                if (Debug) Puts($"Skipping triggers for {animal.name}");
                return;
            }
            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null) return;
            var wolfrate = configData.Animals.WolfSpawns.ChanceOfCrate;

            if (animal is Wolf2 && (HasPerm(attacker , Loot_Perm)))
            {
                if (wolfrate <= 0f || UnityEngine.Random.value > wolfrate / 100)
                {
                    if (Debug) Puts("wolfrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f , 0.5f , 0f) , animal.transform.rotation , "Wolf");
                {
                    if (HasPerm(attacker , Chat_Perm)) Player.Message(attacker , string.Format(msg("RadTownLoot" , attacker.UserIDString) , "wolf") , chaticon);
                    LogToFile("RadTownKills" , $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped" , this);
                }
            }
        }

        void OnEntityDeath(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null) return;
            if ((animal.name.Contains("Alpha") && IgnoreAlpha) || (animal.name.Contains("Omega") && IgnoreOmega))
            {
                if (Debug) Puts($"Skipping triggers for {animal.name}");
                return;
            }

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null) return;
            var wolfrate = configData.Animals.WolfSpawns.ChanceOfCrate;
            var bearrate = configData.Animals.BearSpawns.ChanceOfCrate;
            var boarrate = configData.Animals.BoarSpawns.ChanceOfCrate;
            var chickenrate = configData.Animals.ChickenSpawns.ChanceOfCrate;
            var stagrate = configData.Animals.StagSpawns.ChanceOfCrate;
            var pbearrate = configData.Animals.PBearSpawns.ChanceOfCrate;


            if (animal is Wolf && (HasPerm(attacker, Loot_Perm)))
            {
                if (wolfrate <= 0f || UnityEngine.Random.value > wolfrate / 100)
                {
                    if (Debug) Puts("wolfrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Wolf");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), animal.name), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped", this);
                }
            }
            if (animal is Bear && (HasPerm(attacker, Loot_Perm)))
            {
                if (bearrate <= 0f || UnityEngine.Random.value > bearrate / 100)
                {
                    if (Debug) Puts("bearrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Bear");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), animal.name), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped", this);
                }
            }
            if (animal is Boar && (HasPerm(attacker, Loot_Perm)))
            {
                if (boarrate <= 0f || UnityEngine.Random.value > boarrate / 100)
                {
                    if (Debug) Puts("boarrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Boar");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), "Boar"), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a Boar and loot was dropped", this);
                }
            }
            if (animal is Chicken && (HasPerm(attacker, Loot_Perm)))
            {
                if (chickenrate <= 0f || UnityEngine.Random.value > chickenrate / 100)
                {
                    if (Debug) Puts("chickenrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Chicken");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), animal.name), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped", this);
                }
            }
            if (animal is Stag && (HasPerm(attacker, Loot_Perm)))
            {
                if (stagrate <= 0f || UnityEngine.Random.value > stagrate / 100)
                {
                    if (Debug) Puts("stagrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Stag");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), animal.name), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped", this);
                }
            }
            if (animal is Polarbear && (HasPerm(attacker, Loot_Perm)))
            {
                if (bearrate <= 0f || UnityEngine.Random.value > pbearrate / 100)
                {
                    if (Debug) Puts("polarbearrate was not the random value skipping loot");
                    return;
                }

                SpawnRadLoot(animal.transform.position + new Vector3(0f, 0.5f, 0f), animal.transform.rotation, "Polarbear");
                {
                    if (HasPerm(attacker, Chat_Perm)) Player.Message(attacker, string.Format(msg("RadTownLoot", attacker.UserIDString), animal.name), chaticon);
                    LogToFile("RadTownKills", $"{DateTime.Now:h:mm:ss tt}] {attacker} killed a {animal.name} and loot was dropped", this);
                }
            }
            return;
        }

        void OnEntitySpawned(BaseNPC2 animal)
        {
            if (animal == null) return;
            ShowConsole = false;
            ChangeValues = false;
            if ((animal.name.Contains("Alpha") && IgnoreAlpha) || (animal.name.Contains("Omega") && IgnoreOmega))
            {
                if (Debug) Puts($"Skipping spawn values for {animal.name}");
                return;
            }

            if (animal is Wolf2)
            {
                HealthMin = configData.Animals.WolfSpawns.Healthmin;
                HealthMax = configData.Animals.WolfSpawns.Healthmax;
                ShowConsole = configData.Animals.WolfSpawns.ShowConsole;
                ChangeValues = configData.Animals.WolfSpawns.Change;
            }

            if (!ChangeValues) return;

            RandomHealth = UnityEngine.Random.Range(HealthMin , HealthMax);
            RandomDamage = UnityEngine.Random.Range(DamageMin , DamageMax);

            animal.InitializeHealth(RandomHealth , RandomHealth);
            if (ShowConsole) Puts($"A {animal.name} spawned with {RandomHealth} HP and {RandomDamage} Strength");
        }

        void OnEntitySpawned(BaseAnimalNPC animal)
        {
            if (animal == null) return;
            ShowConsole = false;
            ChangeValues = false;
            if ((animal.name.Contains("Alpha") && IgnoreAlpha) || (animal.name.Contains("Omega") && IgnoreOmega))
            {
                if (Debug) Puts($"Skipping spawn values for {animal.name}");
                return;
            }

            if (animal is Bear)
            {
                HealthMin = configData.Animals.BearSpawns.Healthmin;
                HealthMax = configData.Animals.BearSpawns.Healthmax;
                DamageMin = configData.Animals.BearSpawns.Damage;
                DamageMax = configData.Animals.BearSpawns.DamageMax;
                Speed = configData.Animals.BearSpawns.Speed;
                ShowConsole = configData.Animals.BearSpawns.ShowConsole;
                ChangeValues = configData.Animals.BearSpawns.Change;
            }
            if (animal is Boar)
            {
                HealthMin = configData.Animals.BoarSpawns.Healthmin;
                HealthMax = configData.Animals.BoarSpawns.Healthmax;
                DamageMin = configData.Animals.BoarSpawns.Damage;
                DamageMax = configData.Animals.BoarSpawns.DamageMax;
                Speed = configData.Animals.BoarSpawns.Speed;
                ShowConsole = configData.Animals.BoarSpawns.ShowConsole;
                ChangeValues = configData.Animals.BoarSpawns.Change;
            }
            if (animal is Polarbear)
            {
                HealthMin = configData.Animals.PBearSpawns.Healthmin;
                HealthMax = configData.Animals.PBearSpawns.Healthmax;
                DamageMin = configData.Animals.PBearSpawns.Damage;
                DamageMax = configData.Animals.PBearSpawns.DamageMax;
                Speed = configData.Animals.PBearSpawns.Speed;
                ShowConsole = configData.Animals.PBearSpawns.ShowConsole;
                ChangeValues = configData.Animals.PBearSpawns.Change;
            }
            if (animal is Wolf)
            {
                HealthMin = configData.Animals.WolfSpawns.Healthmin;
                HealthMax = configData.Animals.WolfSpawns.Healthmax;
                DamageMin = configData.Animals.WolfSpawns.Damage;
                DamageMax = configData.Animals.WolfSpawns.DamageMax;
                Speed = configData.Animals.WolfSpawns.Speed;
                ShowConsole = configData.Animals.WolfSpawns.ShowConsole;
                ChangeValues = configData.Animals.WolfSpawns.Change;
            }
            if (animal is Boar)
            {
                HealthMin = configData.Animals.BoarSpawns.Healthmin;
                HealthMax = configData.Animals.BoarSpawns.Healthmax;
                DamageMin = configData.Animals.BoarSpawns.Damage;
                DamageMax = configData.Animals.BoarSpawns.DamageMax;
                Speed = configData.Animals.BoarSpawns.Speed;
                ShowConsole = configData.Animals.BoarSpawns.ShowConsole;
                ChangeValues = configData.Animals.BoarSpawns.Change;
            }
            if (animal is Chicken)
            {
                HealthMin = configData.Animals.ChickenSpawns.Healthmin;
                HealthMax = configData.Animals.ChickenSpawns.Healthmax;
                DamageMin = configData.Animals.ChickenSpawns.Damage;
                DamageMax = configData.Animals.ChickenSpawns.DamageMax;
                Speed = configData.Animals.ChickenSpawns.Speed;
                ShowConsole = configData.Animals.ChickenSpawns.ShowConsole;
                ChangeValues = configData.Animals.ChickenSpawns.Change;
            }
            if (animal is Stag)
            {
                HealthMin = configData.Animals.StagSpawns.Healthmin;
                HealthMax = configData.Animals.StagSpawns.Healthmax;
                DamageMin = configData.Animals.StagSpawns.Damage;
                DamageMax = configData.Animals.StagSpawns.DamageMax;
                Speed = configData.Animals.StagSpawns.Speed;
                ShowConsole = configData.Animals.StagSpawns.ShowConsole;
                ChangeValues = configData.Animals.StagSpawns.Change;
            }

            if (!ChangeValues) return;

            RandomHealth = UnityEngine.Random.Range(HealthMin,HealthMax);
            RandomDamage = UnityEngine.Random.Range(DamageMin, DamageMax);

            animal.InitializeHealth(RandomHealth, RandomHealth);
            animal.AttackDamage = RandomDamage;
            animal.Stats.Speed = Speed;
            animal.Stats.TurnSpeed = Speed;
            if (ShowConsole) Puts($"A {animal.name} spawned with {RandomHealth} HP and {RandomDamage} Strength");
        }

        #endregion

        #region Helpers

        object AnimalCount(string _animal)
        {
            if (_animal == "Wolf") return BaseNetworkable.serverEntities.OfType<Wolf>().Count().ToString();
            if (_animal == "Bear") return BaseNetworkable.serverEntities.OfType<Bear>().Count().ToString();
            if (_animal == "Polarbear") return BaseNetworkable.serverEntities.OfType<Polarbear>().Count().ToString();
            if (_animal == "Boar") return BaseNetworkable.serverEntities.OfType<Boar>().Count().ToString();
            if (_animal == "Chicken") return BaseNetworkable.serverEntities.OfType<Chicken>().Count().ToString();
            if (_animal == "Stag") return BaseNetworkable.serverEntities.OfType<Stag>().Count().ToString();
            else return "N/a";
        }

        bool HasPerm(BasePlayer player, string perm) { return (permission.UserHasPermission(player.UserIDString, perm));}

        #endregion

        #region Loot

        private Dictionary<string, List<ulong>> Skins { get; set; } = new Dictionary<string, List<ulong>>();

        private static List<LootItems> DefaultLoot
        {
            get
            {
                return new List<LootItems>
                {
                    new LootItems { shortname = "ammo.pistol", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.pistol.fire", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.pistol.hv", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.rifle", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.rifle.explosive", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.rifle.hv", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.rifle.incendiary", amount = 5, skin = 0, amountMin = 5 },
                    new LootItems { shortname = "ammo.rocket.basic.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "ammo.rocket.fire.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "ammo.rocket.hv.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "ammo.shotgun", amount = 12, skin = 0, amountMin = 8 },
                    new LootItems { shortname = "explosive.timed", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "explosives", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "pistol.m92", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "rifle.ak.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "rifle.bolt.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "shotgun.spas12", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "smg.2.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "smg.thompson.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.8x.scope.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.flashlight.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.holosight.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.lasersight.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.silencer.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "weapon.mod.small.scope.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "grenade.f1.bp", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "pickaxe", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "hatchet", amount = 1, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "can.beans", amount = 3, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "can.tuna", amount = 3, skin = 0, amountMin = 1 },
                    new LootItems { shortname = "black.raspberries", amount = 5, skin = 0, amountMin = 3 },
                };
            }
        }

        public class LootItems
        {
            public string shortname { get; set; }
            public int amount { get; set; }
            public ulong skin { get; set; }
            public int amountMin { get; set; }
        }

        private void SpawnRadLoot(Vector3 pos, Quaternion rot, string AnimalType)
        {
            var backpack = GameManager.server.CreateEntity(StringPool.Get(1519640547), pos, rot, true) as DroppedItemContainer;

            if (backpack == null) return;

            backpack.inventory = new ItemContainer();
            backpack.inventory.ServerInitialize(null, 36);
            backpack.inventory.GiveUID();
            backpack.inventory.entityOwner = backpack;
            backpack.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            backpack.playerName = animalprefix + AnimalType;
            backpack.Spawn();
            if (AnimalType == "Wolf")
            {
                if(Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.WolfSpawns.Loots.Loot.ToList(), AnimalType);
            }
            if (AnimalType == "Bear")
            {
                if (Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.BearSpawns.Loots.Loot.ToList(), AnimalType);
            }
            if (AnimalType == "Boar")
            {
                if (Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.BoarSpawns.Loots.Loot.ToList(), AnimalType);
            }
            if (AnimalType == "Chicken")
            {
                if (Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.ChickenSpawns.Loots.Loot.ToList(), AnimalType);
            }
            if (AnimalType == "Stag")
            {
                if (Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.StagSpawns.Loots.Loot.ToList(), AnimalType);
            }
            if (AnimalType == "Polarbear")
            {
                if (Debug) Puts($"AnimalType is {AnimalType}");
                SpawnLoot(backpack.inventory, configData.Animals.PBearSpawns.Loots.Loot.ToList(), AnimalType);
            }
        }

        private void SpawnLoot(ItemContainer container, List<LootItems> loot, string AnimalType)
        {
            int total = 0;
            if (AnimalType == "Wolf") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.WolfSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.WolfSpawns.Loots.MaxAmount));
            if (AnimalType == "Bear") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.BearSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.BearSpawns.Loots.MaxAmount));
            if (AnimalType == "Boar") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.BoarSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.BoarSpawns.Loots.MaxAmount));
            if (AnimalType == "Chicken") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.ChickenSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.ChickenSpawns.Loots.MaxAmount));
            if (AnimalType == "Stag") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.StagSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.StagSpawns.Loots.MaxAmount));
            if (AnimalType == "Polarbear") total = UnityEngine.Random.Range(Math.Min(loot.Count, configData.Animals.PBearSpawns.Loots.MinAmount), Math.Min(loot.Count, configData.Animals.PBearSpawns.Loots.MaxAmount));
            if (Debug) Puts($"{AnimalType} {total} items");
            if (total == 0 || loot.Count == 0) return;

            container.capacity = total;
            ItemDefinition def;
            List<ulong> skins;
            LootItems lootItem;

            for (int j = 0; j < total; j++)
            {
                if (loot.Count == 0) break;

                lootItem = loot.GetRandom();

                loot.Remove(lootItem);

                if (lootItem.amount <= 0) continue;

                string shortname = lootItem.shortname;
                bool isBlueprint = shortname.EndsWith(".bp");

                if (isBlueprint) shortname = shortname.Replace(".bp", string.Empty);

                def = ItemManager.FindItemDefinition(shortname);

                if (def == null)
                {
                    Puts("Invalid shortname: {0}", lootItem.shortname);
                    continue;
                }

                ulong skin = lootItem.skin;

                if (configData.PlugCFG.RandomSkins && skin == 0 || configData.PlugCFG.RandomSkins && skin == 0)
                {
                    skins = GetItemSkins(def);

                    if (skins.Count > 0) skin = skins.GetRandom();
                }

                int amount = lootItem.amount;

                if (amount <= 0) continue;

                if (lootItem.amountMin > 0 && lootItem.amountMin < lootItem.amount)
                {
                    amount = UnityEngine.Random.Range(lootItem.amountMin, lootItem.amount);
                }

                Item item;

                if (isBlueprint)
                {
                    item = ItemManager.CreateByItemID(-996920608, 1, 0);

                    if (item == null) continue;

                    item.blueprintTarget = def.itemid;
                    item.amount = amount;
                }
                else item = ItemManager.Create(def, amount, skin);

                if (!item.MoveToContainer(container, -1, false)) item.Remove();
            }
        }

        private List<ulong> GetItemSkins(ItemDefinition def)
        {
            List<ulong> skins;
            if (!Skins.TryGetValue(def.shortname, out skins))
            {
                Skins[def.shortname] = skins = ExtractItemSkins(def, skins);
            }

            return skins;
        }

        private List<ulong> ExtractItemSkins(ItemDefinition def, List<ulong> skins)
        {
            skins = new List<ulong>();

            foreach (var skin in def.skins)
            {
                skins.Add(Convert.ToUInt64(skin.id));
            }
            foreach (var asi in Rust.Workshop.Approved.All.Values)
            {
                if (!string.IsNullOrEmpty(asi.Skinnable.ItemName) && asi.Skinnable.ItemName == def.shortname)
                {
                    skins.Add(Convert.ToUInt64(asi.WorkshopdId));
                }
            }

            return skins;
        }

        #endregion
    }
}