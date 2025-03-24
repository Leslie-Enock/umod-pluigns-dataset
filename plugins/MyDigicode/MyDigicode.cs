using System.Collections.Generic;   //list.config
using System;   //Convert
using Oxide.Core;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("My Digicode", "BuzZ", "1.0.1")]
    [Description("Set a unique code for all codelocks, with autolock and remote options")]

/*======================================================================================================================= 
*   
*   30th december 2018
*
*   0.0.1   20181230    creation
*   1.0.0              code and new version#umod
*   1.0.1   20190907    +bool msg info on destroy
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*=======================================================================================================================*/

    public class MyDigicode : RustPlugin
    {
        bool debug = false;
        const string Digicoder = "mydigicode.user"; 
        const string AutoRemote = "mydigicode.auto"; 
        const string LockRemote = "mydigicode.lock"; 
        private bool ConfigChanged;
        bool loaded;

        string Prefix = "[DIGICODE] ";                       // CHAT PLUGIN PREFIX
        string PrefixColor = "#2eae00";                 // CHAT PLUGIN PREFIX COLOR
        ulong SteamIDIcon = 76561198413544653;          // SteamID FOR PLUGIN ICON
        bool destroy_info = false;

    class StoredData
    {
        public Dictionary<ulong, Digicode> playerdigicode = new Dictionary<ulong, Digicode>();

        public StoredData()
        {
        }
    }
        private StoredData storedData;

        public class Digicode
        {
            public string digicode;
            public bool autolock;
            public List<uint> uniques = new List<uint>();
        }
#region LOAD/UNLOAD
//////////////////////
// GET ALL DIGICODES ON LOAD - and analyse one by one
//////////////////////
        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(Digicoder, this);
            permission.RegisterPermission(AutoRemote, this);
            permission.RegisterPermission(LockRemote, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); 
            TheWorldIsLocked();
        }

        void OnServerInitialized()
        {
            loaded = true;
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }
#endregion
#region LANG MESSAGES

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"NoPermMsg", "Do not have permission for this"},
            {"DigicodeMsg", "Your digicode is <color=orange>{0}</color>\ncode applied on {1} locks\nauto lock is <color=cyan>{2}</color>"},
            {"AutoModeMsg", "Auto lock mode for new codelock\nis now : <color=cyan>{0}</color>"},
            {"LockMsg", "Locking [{0}] codelock(s)"},
            {"UnlockMsg", "Unlocking [{0}] codelock(s)"},
            {"NoDataMsg", "No digicode data"},
            {"KilledMsg", "One of your codelock has disappeared"},
            {"ChangedMsg", "Your digicode has changed to : <color=orange>{0}</color>"},
            {"NewSpawnMsg", "Digicode <color=orange>{0}</color> applied to new CodeLock."},


        }, this, "en");

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"NoPermMsg", "Vous n'avez pas la permission pour cela."},
            {"DigicodeMsg", "Votre digicode est : <color=orange>{0}</color>\ncode appliqué à {1} serrures\nauto lock est <color=cyan>{2}</color>"},
            {"AutoModeMsg", "Mode Auto lock pour les nouvelles serrures\nest maintenant : <color=cyan>{0}</color>"},
            {"LockMsg", "Verrouillage de [{0}] serrure(s)"},
            {"UnlockMsg", "Déverrouillage de [{0}] serrure(s)"},
            {"NoDataMsg", "Pas de données enregistrées."},
            {"KilledMsg", "Une de vos serrures a été détruite."},
            {"ChangedMsg", "Votre digicode est maintenant : <color=orange>{0}</color>"},
            {"NewSpawnMsg", "Digicode <color=orange>{0}</color> enregistré sur une nouvelle serrure."},

        }, this, "fr");
    }

#endregion
#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[DIGICODE] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#2eae00"));                // CHAT PLUGIN PREFIX COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198413544653"));
            destroy_info = Convert.ToBoolean(GetConfig("Message Settings", "Inform player when a lock is destroyed", false));

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
//////////////////////////////////////////////////////////////////////////////////////////////////////
#region SCAN ALL WOLRD codelocks
/////////////////////////
// WHOLE CODELOCK WORLD
/////////////////////////////
        void TheWorldIsLocked()
        {
            ResetAllCounters();
            foreach (var worldlocks in UnityEngine.Object.FindObjectsOfType<CodeLock>())
            {
                if (debug) Puts("FOUND ONE codelock");
                BaseEntity parent = worldlocks.GetParentEntity() as BaseEntity; //to use in future
                if (parent == null)
                {
                    if (debug) Puts("parent NULL !");
                    return;
                }
                if (parent.OwnerID == null)
                {
                    if (debug) Puts("parent owner is NULL");
                    return;
                }
                bool masterlocker = permission.UserHasPermission(parent.OwnerID.ToString(), Digicoder);
                if (!masterlocker) return;
                OneDigicodeAnalyze(worldlocks, parent, null);
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        void ResetAllCounters()
        {
            Dictionary<ulong, Digicode > tempplayerdigicode = new Dictionary<ulong, Digicode>();
            foreach (var item in storedData.playerdigicode)
            {
                Digicode now = new Digicode();
                now = item.Value;
                List<uint> IDz = new List<uint>();
                now.uniques = IDz;
                tempplayerdigicode.Add(item.Key, now);
            }
            storedData.playerdigicode = tempplayerdigicode;
        }
#endregion
#region SCAN ONE PLAYER codelocks population
//////////////////////
// ONE PLAYER CODELOCK WORLD
///////
        void MyWorldIsLocked(ulong playerID, string reason)
        {
            ResetMyCounter(playerID);
            foreach (var worldlocks in UnityEngine.Object.FindObjectsOfType<CodeLock>())
            {
                if (debug) Puts("FOUND ONE codelock");
                BaseEntity parent = worldlocks.GetParentEntity() as BaseEntity; //to use in future
                if (parent == null)
                {
                    if (debug) Puts("parent NULL !");
                    return;
                }
                if (parent.IsDestroyed) continue;
                if (parent.OwnerID == null)
                {
                    if (debug) Puts("parent owner is NULL");
                    return;
                }
                if (parent.OwnerID != playerID) return;
                OneDigicodeAnalyze(worldlocks, parent, reason);
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        void ResetMyCounter(ulong playerID)
        {
            Digicode now = new Digicode();
            foreach (var item in storedData.playerdigicode)
            {
                if (item.Key == playerID)
                {
                    now = item.Value;
                    now.uniques = new List<uint>();
                }
            }
            storedData.playerdigicode.Remove(playerID);
            storedData.playerdigicode.Add(playerID, now);
        }
#endregion
#region RANDOMIzator
///////////////////
// CODE RANDOMIZATOR
//////////////////////
        private string CodeRandomizor()
        {
            int random = Core.Random.Range(0, 9999);
            //string randomstring = Convert.ToString(random);
            string randomstring = random.ToString("0000");
            if (debug) Puts($"randomstring {randomstring}");
            return(randomstring);
        }
#endregion
#region ENTITY SPAWN HOOK
////////////////////////////////////////////
// NEW DIGICODE = new analyse
///////////////////
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!loaded) return; 
    	    CodeLock codelockcomponent = entity.GetComponent<CodeLock>();
            if (codelockcomponent != null)
            {
                if (debug) Puts($"CODELOCK SPAWNED IN WORLD !");
                bool masterlocker = permission.UserHasPermission(codelockcomponent.OwnerID.ToString(), Digicoder);
                if (masterlocker)
                {
                    BaseEntity parent = entity.GetParentEntity() as BaseEntity; //to use in future
                    if (parent == null)
                    {
                        if (debug) Puts("parent NULL !");
                        return;
                    }
                    if (parent.OwnerID == null)
                    {
                        if (debug) Puts("parent owner is NULL");
                        return;
                    }
                    OneDigicodeAnalyze(codelockcomponent, parent, "newspawn");
                }
            }
        }
#endregion
#region ANALYZE ONE codelock
//////////////////
// CODE ANALYZATOR
//////////////////
        void OneDigicodeAnalyze(CodeLock dacodelock, BaseEntity parent, string reason)
        {
            if (debug) Puts("OneDigicodeAnalyze");
            List<uint> IDz = new List<uint>();

            //BaseLock lockbase = dacodelock as BaseLock;   //future
            //BaseEntity  lockentity = dacodelock as BaseEntity;

            Digicode now = new Digicode();
            if (storedData.playerdigicode.ContainsKey(parent.OwnerID))
            {
                storedData.playerdigicode.TryGetValue(parent.OwnerID, out now);
                if (debug) Puts($"PLAYER ALREADY IN DATABASE !");
            }
            else
            {
                string randomized = CodeRandomizor();
                now.digicode = randomized;
                if (debug) Puts($"PLAYER ADDED in database - code {now.digicode} !");
            }
            if (now == null) return;
            if (debug) Puts($"APPLYING ONE CODE {now.digicode} !");
            IDz = now.uniques;
            IDz.Add(dacodelock.net.ID);
            dacodelock.code = now.digicode;
            now.uniques = IDz;
            storedData.playerdigicode.Remove(parent.OwnerID);
            storedData.playerdigicode.Add(parent.OwnerID, now);
            switch (reason)
            {
                case "newspawn" : 
                {
                    if (now.autolock) dacodelock.SetFlag(BaseEntity.Flags.Locked, true);
                    foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                    {
                        if (player.userID == parent.OwnerID)
                        {
                            Player.Message(player,String.Format(lang.GetMessage("NewSpawnMsg", this, player.UserIDString), now.digicode),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                        }
                    }
                    break;
                }
                case "lock" : 
                {
                    dacodelock.SetFlag(BaseEntity.Flags.Locked, true);
                    break;
                }      
                case "unlock" : 
                {
                    dacodelock.SetFlag(BaseEntity.Flags.Locked, false);
                    break;
                }    
            }
        }
#endregion
#region CHAT COMMANDS
//////////////////
// CHAT COMMAND : info
//////////////////////
        [ChatCommand("digi")]
        void MyDigicodeChatCommand(BasePlayer player, string command, string[] args)
        {
            bool masterlocker = permission.UserHasPermission(player.UserIDString, Digicoder);
            if (!masterlocker)
            {
                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            Digicode now = new Digicode();
            List<uint> IDz = new List<uint>();
            if (storedData.playerdigicode.ContainsKey(player.userID))
            {
                storedData.playerdigicode.TryGetValue(player.userID, out now);
                if (debug) Puts($"PLAYER ALREADY IN DATABASE !");
                IDz = now.uniques;
                if (args.Length == 0)
                {
                    Player.Message(player,String.Format(lang.GetMessage("DigicodeMsg", this, player.UserIDString), now.digicode, IDz.Count, now.autolock),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                }
                if (args.Length == 1)
                {
                    switch (args[0].ToLower())
                    {
                        case "code" : 
                        {
                            if (debug) Puts($"PLAYER CHAT COMMAND code change !");
                            now.digicode = CodeRandomizor();
                            storedData.playerdigicode.Remove(player.userID);
                            storedData.playerdigicode.Add(player.userID, now);
                            MyWorldIsLocked(player.userID, null);
                            Player.Message(player,String.Format(lang.GetMessage("ChangedMsg", this, player.UserIDString), now.digicode),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                            break;
                        }
                        case "auto" : 
                        {
                            if (debug) Puts($"PLAYER CHAT COMMAND mode autolock !");
                            bool autolocker = permission.UserHasPermission(player.UserIDString, AutoRemote);
                            if (!autolocker)
                            {
                                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                                return;
                            }
                            now.autolock = !now.autolock;
                            storedData.playerdigicode.Remove(player.userID);
                            storedData.playerdigicode.Add(player.userID, now);
                            Player.Message(player,String.Format(lang.GetMessage("AutoModeMsg", this, player.UserIDString), now.autolock),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                            break;
                        }
                        case "lock" : 
                        {
                            if (debug) Puts($"PLAYER CHAT COMMAND lock all !");
                            bool remotelocker = permission.UserHasPermission(player.UserIDString, LockRemote);
                            if (!remotelocker)
                            {
                                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                                return;
                            }
                            Player.Message(player,String.Format(lang.GetMessage("LockMsg", this, player.UserIDString), now.uniques.Count),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                            MyWorldIsLocked(player.userID, "lock");
                            break;
                        }
                        case "unlock" : 
                        {
                            if (debug) Puts($"PLAYER CHAT COMMAND unlock all !");
                            bool remotelocker = permission.UserHasPermission(player.UserIDString, LockRemote);
                            if (!remotelocker)
                            {
                                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                                return;
                            }
                            Player.Message(player,String.Format(lang.GetMessage("UnlockMsg", this, player.UserIDString), now.uniques.Count),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                            MyWorldIsLocked(player.userID, "unlock");
                            break;
                        }
                    }
                }
            }
            else
            {
                Player.Message(player, lang.GetMessage("NoDataMsg", this, player.UserIDString),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
            }
        }

#endregion
#region ENTITY KILL HOOK
//////////////
// KILL CODELOCK
///////////////////
        void OnEntityKill(BaseNetworkable Entity)
        {
            if (!loaded) return;
			if (Entity == null) return;
    	    CodeLock codelockcomponent = Entity.GetComponent<CodeLock>();
            if (codelockcomponent != null)
            {
                BaseEntity parent = codelockcomponent.GetParentEntity() as BaseEntity; //to use in future
                if (parent == null)
                {
                    if (debug) Puts("parent NULL !");
                    return;
                }
                if (parent.OwnerID == null)
                {
                    if (debug) Puts("parent owner is NULL");
                    return;
                }
                if (debug) Puts($"KILL one codelock from {parent.OwnerID}");
                foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                {
                    if (player.userID == parent.OwnerID && destroy_info)
                    {
                        Player.Message(player, lang.GetMessage("KilledMsg", this, player.UserIDString),$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    }
                }
                MyWorldIsLocked(parent.OwnerID, null);
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }
#endregion
    }
}