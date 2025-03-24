using Oxide.Core;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Hazmat Diving", "Krungh Crow", "2.0.2")]
    [Description("This will protect you from drowning and cold damage while swimming.")]

/*======================================================================================================================= 
*
*   
*   16th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   2.0.0   20181116    Rewrite of plugin by new maintainer _ hazmat clothes parts no more existing. Switched to Suit. added messages on wear
*   2.0.1   20190123    permission hazmatdiving.use
*
*   add scientist suit ?
*
*
*********************************************
*   Original author :   DaBludger on versions <2.0.0
*   Maintainer(s)   :   BuzZ since 20181116 from v2.0.0
*   Maintainer(s)   :   Krungh Crow since 20201009 from v2.0.2
*********************************************   
*
*=======================================================================================================================*/

    public class HazmatDiving : RustPlugin
    {
        private bool Changed;
        bool debug = false;
        bool loaded;

        private bool applydamageArmour = false;
        //private bool configloaded = false;
        private float armourDamageAmount = 0f;
        private float dmgdrowning1 = 30f;
        private float dmgcold1 = 30f;
        private float dmgdrowning2 = 50f;
        private float dmgcold2 = 50f;
        private float dmgdrowning3 = 40f;
        private float dmgcold3 = 40f;
        private float dmgdrowning4 = 35f;
        private float dmgcold4 = 35f;
        private float dmgdrowning5 = 60f;
        private float dmgcold5 = 60f;

        string Prefix = "[HazmatDiving] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#ebdf42";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#8bd9ff";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561199090290915;  

        const string HazmatUse = "hazmatdiving.use"; 

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(HazmatUse, this);
        }

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        void Loaded()
        {
            loaded = true;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private void LoadVariables()
        {
            if (debug){Puts("Loading Config File:");}

            dmgcold1 = Convert.ToSingle(GetConfig("Hazmat Suit Variables", "Cold reduction in %", "30"));
            if (debug){Puts($"Cold damage = X - {dmgcold1}%");}
            dmgdrowning1 = Convert.ToSingle(GetConfig("Hazmat Suit Variables", "Drowning reduction in %", "30"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning1}%");}

            dmgcold2 = Convert.ToSingle(GetConfig("Heavy Scientist Suit Variables", "Cold reduction in %", "50"));
            if (debug){Puts($"Cold damage = X - {dmgcold2}%");}
            dmgdrowning2 = Convert.ToSingle(GetConfig("Heavy Scientist Suit Variables", "Drowning reduction in %", "50"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning2}%");}

            dmgcold3 = Convert.ToSingle(GetConfig("Scientist Suit (Green) Variables", "Cold reduction in %", "40"));
            if (debug){Puts($"Cold damage = X - {dmgcold3}%");}
            dmgdrowning3 = Convert.ToSingle(GetConfig("Scientist Suit (Green) Variables", "Drowning reduction in %", "40"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning3}%");}

            dmgcold4 = Convert.ToSingle(GetConfig("Scientist Suit (Blue) Variables", "Cold reduction in %", "35"));
            if (debug){Puts($"Cold damage = X - {dmgcold4}%");}
            dmgdrowning4 = Convert.ToSingle(GetConfig("Scientist Suit (Blue) Variables", "Drowning reduction in %", "35"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning4}%");}

            dmgcold5 = Convert.ToSingle(GetConfig("Space Suit Variables", "Cold reduction in %", "60"));
            if (debug){Puts($"Cold damage = X - {dmgcold5}%");}
            dmgdrowning5 = Convert.ToSingle(GetConfig("Space Suit Variables", "Drowning reduction in %", "60"));
            if (debug){Puts($"Drown damage = X - {dmgdrowning5}%");}

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[HazmatDiving] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#ebdf42"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#8bd9ff"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561199090290915"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /

            ////////////////// FROM AUTHOR
            //applydamageArmour = Convert.ToBoolean(GetConfig("Attire", "TakesDamage", "false"));
            //Puts("Amour takes damage: "+ applydamageArmour);
            //armourDamageAmount = Convert.ToSingle(GetConfig("Attire", "DamageAmount", "0.0"));
            //Puts("How much damage does the armour take: "+ armourDamageAmount);

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }


#region MESSAGES

        protected override void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"coldMsg", "Cold damages will be reduce by"},
                {"drowningMsg", "Drowning damages will be reduce by"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"coldMsg", "Les dommages de froid seront réduits de"},
                {"drowningMsg", "Les dommages de noyade seront réduits de"},

            }, this, "fr");
        }

#endregion

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null) return;
            BasePlayer onlinecheck = entity.ToPlayer();
            if (onlinecheck == null) return;
            if (onlinecheck.IsConnected == false)
            {
                if (debug){Puts($"-> IGNORED DAMAGE. From not steam online player.");}
                return;
            }
            bool diver = permission.UserHasPermission(onlinecheck.UserIDString, HazmatUse);
            if (!diver) return;
            if (hitinfo.hasDamage)
            {
                float damagedone;
                bool armourDamaged = false;
                if (hitinfo.damageTypes?.Get(Rust.DamageType.Drowned) > 0)
                {
                    damagedone = getDamageDeduction(onlinecheck, Rust.DamageType.Drowned);
                    float newdamage = getScaledDamage(hitinfo.damageTypes.Get(Rust.DamageType.Drowned), damagedone);
                    hitinfo.damageTypes.Set(Rust.DamageType.Drowned, newdamage);
                    armourDamaged = true;
                    if (debug){Puts($"-> DROWNED damage");}
                }
                if (hitinfo.damageTypes?.Get(Rust.DamageType.Cold) > 0 && onlinecheck.IsSwimming())
                {
                    damagedone = getDamageDeduction(onlinecheck, Rust.DamageType.Cold);
                    float newdamage = getScaledDamage(hitinfo.damageTypes.Get(Rust.DamageType.Cold), damagedone);
                    hitinfo.damageTypes.Set(Rust.DamageType.Cold, newdamage);
                    armourDamaged = true;
                    if (debug){Puts($"-> COLD damage on SWIMMING");}
                }
                //////////////////////////////
                // IF CONFIG damageArmour is true ... damage the armour !
                /////////////////////////////
                /////// FROM ORIGINAL AUTHOR

                /*if (armourDamaged && applydamageArmour)
                {
                    foreach (Item item in onlinecheck.inventory.containerWear.itemList) // foreach is not a good point
                    {
                        if (item.info.name.ToLower().Contains("hazmat"))
                        {
                            item.condition = item.condition - armourDamageAmount;
                        }
                    }
                }*/
            }
        }

        private float getScaledDamage(float current, float deduction)
        {
            float newdamage = current - (current * deduction);
            if (newdamage < 0)
            {
                newdamage = 0;
            }
            return newdamage;
        }

        private float getDamageDeduction(BasePlayer player, Rust.DamageType damageType)
        {
            float dd = 0.0f;
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (!item.isBroken)
                {
                    if (item.info.shortname == "hazmatsuit")
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning1/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold1/100);
                        }
                    }

                    else if (item.info.shortname == "scientistsuit_heavy")
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning2/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold2/100);
                        }
                    }

                    else if (item.info.shortname == "hazmatsuit_scientist_peacekeeper")
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning3/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold3/100);
                        }
                    }

                    else if (item.info.shortname == "hazmatsuit_scientist")
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning4/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold4/100);
                        }
                    }

                    else if (item.info.shortname == "hazmatsuit.spacesuit")
                    {
                        if (damageType == Rust.DamageType.Drowned)
                        {
                            dd += (dmgdrowning5/100);
                        }
                        if (damageType == Rust.DamageType.Cold)
                        {
                            dd += (dmgcold5/100);
                        }
                    }
                }
            }
            return dd;
        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false) return;
            if (item == null) return;
            if (inventory == null) return;
            BasePlayer HDUser = inventory.GetComponent<BasePlayer>();
            if (HDUser == null) return;
            if (HDUser.IsConnected == false) return;
            bool diver = permission.UserHasPermission(HDUser.UserIDString, HazmatUse);
            if (!diver) return;

            if (item.info.shortname == "hazmatsuit")
            {
                Player.Message(HDUser, $"Hazmat Suit :\n<color={ChatColor}>{lang.GetMessage("coldMsg", this, HDUser.UserIDString)} {dmgcold1}%\n"
                + $"{lang.GetMessage("drowningMsg", this, HDUser.UserIDString)} {dmgdrowning1}%</color>",$"<color={PrefixColor}> {Prefix} </color>"
                , SteamIDIcon); 
            }   

            if (item.info.shortname == "scientistsuit_heavy")
            {
                Player.Message(HDUser, $"Heavy Scientist Suit :\n<color={ChatColor}>{lang.GetMessage("coldMsg", this, HDUser.UserIDString)} {dmgcold2}%\n"
                + $"{lang.GetMessage("drowningMsg", this, HDUser.UserIDString)} {dmgdrowning2}%</color>",$"<color={PrefixColor}> {Prefix} </color>"
                , SteamIDIcon); 
            }

            if (item.info.shortname == "hazmatsuit_scientist_peacekeeper")
            {
                Player.Message(HDUser, $"Green Scientist Suit :\n<color={ChatColor}>{lang.GetMessage("coldMsg", this, HDUser.UserIDString)} {dmgcold3}%\n"
                + $"{lang.GetMessage("drowningMsg", this, HDUser.UserIDString)} {dmgdrowning3}%</color>",$"<color={PrefixColor}> {Prefix} </color>"
                , SteamIDIcon); 
            }

            if (item.info.shortname == "hazmatsuit_scientist")
            {
                Player.Message(HDUser, $"Blue Scientist Suit :\n<color={ChatColor}>{lang.GetMessage("coldMsg", this, HDUser.UserIDString)} {dmgcold4}%\n"
                + $"{lang.GetMessage("drowningMsg", this, HDUser.UserIDString)} {dmgdrowning4}%</color>",$"<color={PrefixColor}> {Prefix} </color>"
                , SteamIDIcon); 
            }   

            if (item.info.shortname == "hazmatsuit.spacesuit")
            {
                Player.Message(HDUser, $"Space Suit :\n<color={ChatColor}>{lang.GetMessage("coldMsg", this, HDUser.UserIDString)} {dmgcold5}%\n"
                + $"{lang.GetMessage("drowningMsg", this, HDUser.UserIDString)} {dmgdrowning5}%</color>",$"<color={PrefixColor}> {Prefix} </color>"
                , SteamIDIcon); 
            }  
        }
    }
}