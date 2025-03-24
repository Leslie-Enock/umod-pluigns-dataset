// Requires: ZoneManager



using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("No Safe Zone Sleepers", "NooBlet", "1.9")]
    [Description("Automaticly Creates a Zone to remove sleeping players from Outpost and Bandit Camp")]
    public class NoSafeZoneSleepers : CovalencePlugin
    {
        List<string> createdZones = new List<string>();
        int number = 0;

        #region Cached Variables

        private bool UseCustomZone = true;       
        private bool KillSleepers = false;
        private int TimeToKill = 60;
        private int OupostZoneRadius = 150;
        private int BanditCampZoneRadius = 150;
        private int OtherZones = 20;
        private bool UseMessages = false;         
        private string EnterMessage = "You are now Entering {0} a No Sleep Zone";
        private string ExitMessage = "You are now leaving {0} a No Sleep Zone";
        private string MessageColor = "#95a5a6";
        private List<object> CustomZones = new List<object> {
                "12345",
                "54321"
               
        };
        

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file");
        }
        private void LoadConfiguration()
        {
            CheckCfg<bool>("1 Use Custom SafeZone settings", ref UseCustomZone);
            CheckCfg<bool>("2 Kill Sleepers on disconnect", ref KillSleepers);
            CheckCfg<int>("3 Time to delay Kill", ref TimeToKill);
            CheckCfg<int>("4 Oupost Zone Radius", ref OupostZoneRadius);
            CheckCfg<int>("5 BanditCamp Zone Radius", ref BanditCampZoneRadius);
            CheckCfg<int>("6 Ranch and Fishing Village Zone Radius", ref OtherZones);
            CheckCfg<bool>("7 Use Enter and Leave Messages?", ref UseMessages);
            CheckCfg<string>("8 Enter Message", ref EnterMessage);
            CheckCfg<string>("9 Leave Message", ref ExitMessage);
            CheckCfg<string>("A Message Color", ref MessageColor);
            CheckCfg<List<object>>("Custom SafeZone Zoneid's", ref CustomZones);
            
            
            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }



        #endregion config


        #region Hooks

        [PluginReference]
        private Plugin ZoneManager;


        private void OnServerInitialized()
        {
            LoadConfiguration();
            if (!UseCustomZone)
            {
                AddZones();
            }
                
        }

        void Unload()
        {
            ClearZones();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveSleeper(player);
        }

        #endregion Hooks

        
        #region Methods

        bool isInPriv(BasePlayer player)
        {
            if(player == null) { return false; }
            var priv = player.GetBuildingPrivilege();
            if(priv == null) 
            { 
                return false; 
            } 
            else
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false) ;
                return true;
            }
        }

        string ZoneName(string message,string zonename)
        {
            string name = "";
            if (message.Contains("{0}")) { name = message.Replace("{0}", zonename); } else { name = message; }
            return name;
        }
        private bool CheckCustomZone(BasePlayer player)
        {
            foreach (var zid in CustomZones)
            {
                if (ZoneManager.Call<bool>("IsPlayerInZone", zid.ToString(), player))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetPlayerZone(BasePlayer player)
        {
            string zone = "";
            foreach (var zid in CustomZones)
            {
                if (ZoneManager.Call<bool>("IsPlayerInZone", zid.ToString(), player))
                {
                    zone = zid.ToString();
                    return zone;
                }
            }
            return zone;
        }

        private void RemoveSleeper(BasePlayer sleeper)
        {
            if (!isInPriv(sleeper))
            {
                if (UseCustomZone)
                {

                    if (CheckCustomZone(sleeper))
                    {

                        if (KillSleepers)
                        {
                            float time = float.Parse(TimeToKill.ToString()) * 60;
                            timer.Once(time, () =>
                            {
                                if (!sleeper.IsConnected)
                                {
                                    Addflag(GetPlayerZone(sleeper), "killsleepers");
                                }
                            });
                        }
                        else
                        {
                            Addflag(GetPlayerZone(sleeper), "ejectsleepers");
                            sleeper.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false);
                        }

                    }
                    return;
                }
                else
                {
                    foreach (string zone in createdZones)
                    {
                        if (ZoneManager.Call<bool>("isPlayerInZone", zone, sleeper))
                        {


                            if (KillSleepers)
                            {
                                float time = float.Parse(TimeToKill.ToString()) * 60;
                                timer.Once(time, () =>
                                {
                                    if (!sleeper.IsConnected)
                                    {
                                        sleeper.Kill();
                                    }
                                });
                            }
                            else
                            {
                                Addflag(zone, "ejectsleepers");
                                sleeper.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false);
                            }

                        }
                    }
                }
            }
        }
       

        private void Addflag(string zone,string flag)
        {
            ZoneManager?.Call("AddFlag", zone, flag);
           

            timer.Once(10f, () =>
            {
               ZoneManager?.Call("RemoveFlag", zone, flag);
            });

        }

        private void ClearZones()
        {
            if (createdZones != null)
            {
                foreach (string zone in createdZones)
                {
                    ZoneManager?.Call("EraseZone", zone);
                    Puts($"{zone} has been Removed");
                }
            }
           
        }

        private void AddZones()
        {

            //  assets/bundled/prefabs/autospawn/monument/medium/stables_a.prefab
            // assets/bundled/prefabs/autospawn/monument/harbor/fishing_village_a.prefab
            // assets/bundled/prefabs/autospawn/monument/harbor/fishing_village_b.prefab
            //assets/bundled/prefabs/autospawn/monument/harbor/fishing_village_c.prefab
            

            foreach (var current in TerrainMeta.Path.Monuments)
            {
                if (current.name == "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab" || current.name.Contains("assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab")||current.name.Contains("fishing_village") || current.name.Contains("stables"))
                {
                    string[] messages = new string[4];
                    string name = "";
                    if (UseMessages)
                    {
                         messages = new string[8];
                    }
                    else
                    {
                         messages = new string[4];
                    }

                    if (current.displayPhrase.english.StartsWith("Bandit"))
                    {
                        name = "BanditCamp";
                    }
                    if (current.displayPhrase.english.StartsWith("Outpost"))
                    {
                        name = "OutPost";
                    }
                    if (current.displayPhrase.english.StartsWith("Ranch"))
                    {
                        name = "Ranch";
                    }
                    if (current.displayPhrase.english.StartsWith("Fishing Village"))
                    {
                        name = "Fishing Village";
                    }
                    if (current.displayPhrase.english.StartsWith("Large Fishing Village"))
                    {
                        name = "Large Fishing Village";
                    }

                    string zoneId = $"{name}.{number}";
                    string friendlyname = name;
                    string ID = zoneId;

                    
                    messages[0] = "name";
                    messages[1] = friendlyname;
                    //messages[2] = "ejectsleepers";
                    //messages[3] = "true";
                    messages[2] = "radius";
                    if (name == "OutPost")
                    {
                        messages[3] = OupostZoneRadius.ToString();
                    }
                    else
                    {
                        messages[3] = BanditCampZoneRadius.ToString();
                    }
                    if (name == "Ranch" || name == "Fishing Village" || name == "Large Fishing Village")
                    {
                        messages[3] = OtherZones.ToString();
                    }

                    if (UseMessages)
                    {
                        string entermsg = $"<color=red>[NSZS]</color> :<color={MessageColor}> {ZoneName(EnterMessage, current.displayPhrase.english)} </color> ";
                        string leavemsg = $"<color=red>[NSZS]</color> :<color={MessageColor}> {ZoneName(ExitMessage,current.displayPhrase.english)} </color> ";

                        messages[4] = "enter_message";
                        messages[5] = entermsg;
                        messages[6] = "leave_message";
                        messages[7] = leavemsg;
                    }

                    ZoneManager?.CallHook("CreateOrUpdateZone", ID, messages, current.transform.position);
                    number++;
                    createdZones.Add(zoneId);
                    Puts($"{ID} has been created");
                }
            }
        }



        #endregion Methods


        //#region Debug Testing
        //private BasePlayer findPlayer(string name)
        //{
        //    BasePlayer target = BasePlayer.FindAwakeOrSleeping(name);

        //    return target;
        //}

        //[Command("sleep")]
        //private void sleepCommand(IPlayer iplayer, string command, string[] args)
        //{
        //    BasePlayer player = iplayer.Object as BasePlayer;
        //    player.StartSleeping();
        //}

        //#endregion Debug Testing
    }
}