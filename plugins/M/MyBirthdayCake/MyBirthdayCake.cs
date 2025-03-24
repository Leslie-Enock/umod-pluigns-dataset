using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;   //String.
using Convert = System.Convert;
using Oxide.Core;   //storeddata

namespace Oxide.Plugins
{
    [Info("My Birthday Cake", "Krungh Crow", "1.1.0")]
    [Description("Throw a Birthday Cake Bomb")]

    #region Changelogs and ToDo
    /*******************************************************************************
    * 
    * Thanks to BuzZ[PHOQUE] the original author of this plugin
    * 
    * 1.0.1 :   Started to maintain
    *       :   Updated Hook (OnActiveItemChanged)
    * 1.1.0 :   Reformatting the coding
    *       :   Fixed Compiling issues
    * 
    ********************************************************************************/
    #endregion

    public class MyBirthdayCake : RustPlugin
    {
        bool debug = false;
        private bool ConfigChanged;

        string Prefix = "[MBC] ";
        ulong ChatIcon = 76561198357983957;
        float flamingtime = 30f;
        string cakename = "Birthday Cake ";
        const string Cake_Perm = "mybirthdaycake.use";
    
        class StoredData
        {
            public Dictionary<ulong, Cake> Cakes = new Dictionary<ulong,Cake>();
            public StoredData() { }
        }
        private StoredData storedData;

        class Cake
        {
            public ulong playerownerID;
            public bool explosion;
            //public bool smokebirthday;
            public bool fire;
            public bool oilflames;
            public bool napalmflames;
            public bool flameonthrow;
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Cake_Perm, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermMsg", "You don't have permission to do that."},
                {"SpecifyMsg", "Please specify - explose - oil - napalm - fire"},
                {"GiftMsg", "Gift : Birthday Cake with {0} !"},
                {"WishMsg", "Wish a Happy Birthday !!"},
                {"NotYoursMsg", "This Cake is not yours ! It is no more a special one ..."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermMsg", "Vous n'avez pas la permission."},
                {"SpecifyMsg", "Merci de préciser - explose - oil - napalm - fire"},
                {"GiftMsg", "Cadeau : un beau gâteau {0} !"},
                {"WishMsg", "Souhaitez un Joyeux Zanniversaire!!"},
                {"NotYoursMsg", "Ce gâteau n'est pas à vous, suppression de ses capacités meurtrières ..."},

            }, this, "fr");
        }

#endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            ChatIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon" , "76561198357983957"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Plugin Prefix", "[MBC] "));
            flamingtime = Convert.ToSingle(GetConfig("Flames Settings", "Duration in seconds", "30"));
            cakename = Convert.ToString(GetConfig("Cake Settings", "Name", "Birthday Cake "));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

        #endregion

        #region Chat Commands
        [ChatCommand("cake")]
        private void Cake_Permer(BasePlayer player , string command , string[] args)
        {
            bool isauth = permission.UserHasPermission(player.UserIDString , Cake_Perm);
            if (!isauth)
            {
                Player.Message(player , lang.GetMessage("NoPermMsg" , this , player.UserIDString) , Prefix , ChatIcon);
                return;
            }
            CreateCake(player , args);
        }
        #endregion

        #region Hooks
        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name , storedData);
        }

        void OnMeleeThrown(BasePlayer player , Item item)
        {
            if (storedData.Cakes.ContainsKey(item.uid.Value) == false) return;
            if (debug) Puts($"BIRTHDAYCAKE !!! {item}");
            timer.Once(5f , () =>
            {
                DelaydCakeTrigger(item.uid.Value , item);
            });
        }

        void OnActiveItemChanged(BasePlayer player , Item oldItem , Item newItem)
        {
            if (player == null) return;
            if (newItem == null) return;
            ulong cakeId = new ulong();
            foreach (var item in storedData.Cakes)
            {
                if (newItem.uid.Value == item.Key)
                {
                    Cake cake = item.Value;
                    if (cake.playerownerID == player.userID)
                        Player.Message(player , lang.GetMessage("WishMsg" , this , player.UserIDString) , Prefix , ChatIcon);
                    else
                    {
                        Player.Message(player , lang.GetMessage("NotYoursMsg" , this , player.UserIDString) , Prefix , ChatIcon);
                        cakeId = item.Key;
                    }
                }
            }
            if (cakeId != null) storedData.Cakes.Remove(cakeId);
        }
        #endregion

        #region Methods

        void DelaydCakeTrigger(ulong cakeid, Item item)
        {
            BaseEntity entity = item.GetWorldEntity();
            if (entity == null)
            {
                if (debug) Puts("ENTITY NULL !");
                return;
            }
            Cake cake = new Cake();
            storedData.Cakes.TryGetValue(cakeid, out cake);
            if (cake == null) return;
            // type of explosion
            ////if (cake.smokebirthday) CakeRunTrigger(item, "smoke", entity);
            if (cake.explosion) CakeRunTrigger(item, "explose", entity);
            // type of flames
            if (cake.oilflames) CakeRunTrigger(item, "oil", entity);
            if (cake.napalmflames) CakeRunTrigger(item, "napalm", entity);
            if (cake.fire) CakeRunTrigger(item, "fire", entity);
            NextFrame(() =>
            {
                entity.Kill();
                storedData.Cakes.Remove(item.uid.Value);
                });
            }

        void CakeRunTrigger(Item item, string birthdaytype, BaseEntity entity)
        {
            if (debug) Puts("BoomBoom sequence");
            Vector3 daboom = new Vector3(entity.transform.position.x,entity.transform.position.y,entity.transform.position.z);
            if (debug) Puts($"Vector3 {daboom}");
            TimedExplosive boom = new TimedExplosive();
            //SmokeGrenade boomboom = new SmokeGrenade();
            if (birthdaytype == "explose")
            {
                BaseEntity GrenadeF1 = GameManager.server.CreateEntity("assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab", daboom, new Quaternion(), true);
                if (GrenadeF1 == null) if (debug) Puts("GrenadeF1 NULL BaseEntity ENTITY !!!!");
                else if (debug) Puts($"oright {GrenadeF1}");
                boom = GrenadeF1.GetComponent<TimedExplosive>();
                if (boom == null) Puts("boom NULL TimedExplosive ENTITY !!!!");
                if (debug) Puts("F1 BIRTHDAY !!!!");
                boom.Explode();
                return;
             }
            /*if (birthdaytype == "smoke")
            {
                prefab = "assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab";
                Puts("SMOKE BIRTHDAY !!!!");
                BaseEntity GrenadeSmoke = GameManager.server.CreateEntity(prefab, daboom, new Quaternion(), true);
                if (GrenadeSmoke == null) Puts("GrenadeSmoke NULL BaseEntity ENTITY !!!!");
                else Puts($"oright {GrenadeSmoke}");
                boomboom = GrenadeSmoke.GetComponent<SmokeGrenade>();
                if (boomboom != null) Puts($"SmokeGrenade {boomboom}");
                boomboom.smokeDuration = 20f;
                GrenadeSmoke.Spawn();
                //boomboom.Explode();
                return;
            }*/
            string prefab = string.Empty;
            if (birthdaytype == "fire") prefab = "assets/bundled/prefabs/fireball.prefab";
            if (birthdaytype == "oil") prefab = "assets/bundled/prefabs/oilfireballsmall.prefab";
            if (birthdaytype == "napalm") prefab = "assets/bundled/prefabs/napalm.prefab";
            BaseEntity GrenadeFlames = GameManager.server.CreateEntity(prefab, daboom, new Quaternion(), true);
            if (GrenadeFlames == null) if (debug) Puts("GrenadeFlames NULL BaseEntity ENTITY !!!!");
            else if (debug) Puts($"oright fireflames {GrenadeFlames}");
            GrenadeFlames?.Spawn();
            timer.Once(flamingtime, () => 
            {
                if (GrenadeFlames != null) GrenadeFlames.Kill();
            });
        }

        private void CreateCake(BasePlayer player , string[] args)
        {
            Cake cake = new Cake();
            cake.playerownerID = player.userID;
            string CakeType = "";
            if (args.Contains("explose"))
            {
                CakeType = "|Xplosiv";
                cake.explosion = true;
            }
            /*if (args.Contains("smoke"))
            {
                CakeType = $"{CakeType}|Smoke";
                cake.smokebirthday = true;
            }*/
            if (args.Contains("oil"))
            {
                CakeType = $"{CakeType}|Oil";
                cake.oilflames = true;
            }
            if (args.Contains("napalm"))
            {
                CakeType = $"{CakeType}|Napalm";
                cake.napalmflames = true;
            }
            if (args.Contains("fire"))
            {
                CakeType = $"{CakeType}|Fire";
                cake.fire = true;
            }
            if (CakeType == "")
            {
                Player.Message(player , lang.GetMessage("SpecifyMsg" , this , player.UserIDString) , Prefix , ChatIcon);
                return;
            }
            Item caketogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1973165031).itemid , 1 , 0);
            if (caketogive != null)
            {
                caketogive.name = cakename + CakeType;
                player.GiveItem(caketogive);
                storedData.Cakes.Add(caketogive.uid.Value , cake);
                Player.Message(player , String.Format(lang.GetMessage("GiftMsg" , this , player.UserIDString) , CakeType) , Prefix , ChatIcon);
            }
        }

        #endregion
    }
}