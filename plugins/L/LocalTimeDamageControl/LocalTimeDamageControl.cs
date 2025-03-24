using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Collections;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("LocalTimeDamageControl", "CARNY666", "1.0.11", ResourceId = 2720)]

    class LocalTimeDamageControl : RustPlugin
    {
        const string adminPriv = "LocalTimeDamageControl.admin";
        bool activated = false;

        void Init()
        {
            try
            { 
                PrintWarning($"{this.Title} {this.Version} Initialized @ {DateTime.Now.ToLongTimeString()}...");
                LoadConfig();
            }
            catch (Exception ex)
            {
                PrintError($"Error Init: {ex.StackTrace}");
            }

        }

        void Loaded()
        {
            try
            {
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    { "localtime", "Local time is {localtime}." },
                    { "nodamage", "You cannot cause damage during between {starttime} and {endtime}." },
                    { "activated", "You cannot cause damage during while LocalTimeDamageControl is activated." },
                    { "starts", "LocalTimeDamageControl starts at {starts}." },
                    { "remains", "LocalTimeDamageControl remains on for {remains}." },
                    { "onstatus", "LocalTimeDamageControl is ON. It will become active @ {starttime} until {endtime}" },
                    { "offstatus", "LocalTimeDamageControl is {status}. It will NOT become active." },
                    { "duration", "LocalTimeDamageControl duration is {duration} minutes." },
                    { "errorstart", "Error, please 24 hour time format: i.e 08:00 for 8 am. 20:00 for 8pm." },
                    { "errormin", "Error, please enter an integer i.e: 60 for 60 minutes." },
                    { "errorhour", "Error, please enter an integer i.e: 2 for 180 minutes." },
                    { "activate", "- Activates (ignores configuration)"},
                    { "deactivate", "- Deactivate (back to normal, uses config)"},
                    { "info", "- LocalTimeDamageControl {act} inform players when they are unable to damage."},
                    { "help1", "/lset start 8:00 am ~ Set start time for damage control." },
                    { "help2", "/lset minutes 60    ~ Set duration in minutes for damage control."},
                    { "help3", "/lset hours 12      ~ Set duration in hours for damage control."},
                    { "help4", "/lset activate      ~ Force damage control ON, ignore config."},
                    { "help5", "/lset info          ~ Toggle player message, when damage is denied."},
                    { "help6", "/lset deactivate    ~ use configured times."},
                    { "help7", "/lset off           ~ Turn off damage control."},
                    { "help8", "/lset on            ~ Turn on damage control during set times. "},
                    { "help9", "- starts at {starttime} ends at {endtime}. (Server's time)"}
            }, this, "en");


                //if ((bool)Config["LocalTimeDamageControlOn"])
                //{
                    PrintWarning("LocalTimeDamageControl starts at " + ShowTime(Config["LocalTimeDamageControlStart"]));
                    PrintWarning("LocalTimeDamageControl remains on for " + Config["LocalTimeDamageControlDuratationMinutes"] + " minutes.");                
                //}
                //else
                //{
                //    PrintWarning("LocalTimeDamageControl is off.");
                //}
                permission.RegisterPermission(adminPriv, this);
                PrintWarning(adminPriv + " privilidge is registered.");

            }
            catch (Exception ex)
            {
                PrintError($"Error Loaded: {ex.StackTrace}");
            }

        }

        protected override void LoadDefaultConfig() // Only called if the config file does not already exist
        {
            try
            {
                PrintWarning("Creating a new configuration file.");

                Config.Clear();

                Config["LocalTimeDamageControlStart"] = "08:30 AM";           // 8:30 AM
                Config["LocalTimeDamageControlDuratationMinutes"] = (8 * 60); // 8 hrs
                Config["LocalTimeDamageControlOn"] = true;
                Config["LocalTimeDamageControlInformPlayer"] = true;

                SaveConfig();
            }
            catch(Exception ex)
            {
                throw new Exception($"Error LoadDefaultConfig", ex);
            }

        }

        public bool IsDamageControlOn()
        {
            try
            {
                if (activated)
                {
                    //PrintWarning("IsDamageControlOn: activated: true;");
                    return true;
                }

                //if ((bool)Config["LocalTimeDamageControlOn"] == false)
                //{
                //    PrintWarning("IsDamageControlOn: LocalTimeDamageControlOn: false;");
                //    return false;
                //}

                DateTime startTime = DateTime.ParseExact(Config["LocalTimeDamageControlStart"].ToString(), "hh:mm tt", CultureInfo.InvariantCulture);
                DateTime endTime = DateTime.ParseExact(Config["LocalTimeDamageControlStart"].ToString(), "hh:mm tt", CultureInfo.InvariantCulture).AddMinutes(int.Parse(Config["LocalTimeDamageControlDuratationMinutes"].ToString()));

                if ((DateTime.Now.ToLocalTime() >= startTime) && (DateTime.Now.ToLocalTime() <= endTime))
                {
                    //PrintWarning("IsDamageControlOn: Within Time Window: true;");
                    return true;
                }
                else
                {
                    //PrintWarning("IsDamageControlOn: Within Time Window: false;");
                    return false;
                }
            }
            catch(Exception ex)
            {
                throw new Exception($"Error IsDamageControlOn", ex);
            }
        }

        public DateTime getStartTime()
        {
            try
            {
                return DateTime.Parse(Config["LocalTimeDamageControlStart"].ToString());
            }
            catch(Exception ex)
            {
                throw new Exception($"Error getStartTime. {Config["LocalTimeDamageControlStart"].ToString()}" , ex);
            }
        }

        public DateTime getEndTime()
        {
            try
            {
                return DateTime.Parse(Config["LocalTimeDamageControlStart"].ToString()).AddMinutes(int.Parse(Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
            }
            catch(Exception ex)
            {
                throw new Exception($"Error getEndTime. {Config["LocalTimeDamageControlStart"].ToString()}, {Config["LocalTimeDamageControlDuratationMinutes"].ToString()}", ex);
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null) return null; 
                if (info == null) return null; 
                if (info.InitiatorPlayer == null) return null;
                if (IsDamageControlOn() == false) return null;                  // check if on or off
                if (entity is BasePlayer) return null;                          // damage to players ok!!
                if (entity.OwnerID == 0) return null;
                if (entity.OwnerID == info.InitiatorPlayer.userID) return null; // owner can damage own stuff

                if (info.InitiatorPlayer != null) {

                    if ((bool)Config["LocalTimeDamageControlInformPlayer"] == true)                    
                    {
                        if (!activated)
                        {
                            if (info.InitiatorPlayer != null)
                            {
                                PrintToChat(info.InitiatorPlayer, lang.GetMessage("nodamage", this, info.InitiatorPlayer.UserIDString)
                                    .Replace("{starttime}", ShowTime(getStartTime()))
                                    .Replace("{endtime}", ShowTime(getEndTime())));
                                
                            }
                        }
                        else
                        {
                            if (info.InitiatorPlayer != null)
                                PrintToChat(info.InitiatorPlayer, lang.GetMessage("activated", this, info.InitiatorPlayer.UserIDString));
                            
                        }
                    }
                }

                info.damageTypes.ScaleAll(0.0f);                                // no damage
                return false;
            }
            catch (Exception ex)
            {
                PrintError("Error OnEntityTakeDamage: " + ex.Message);
                
            }
            return null;
        }

        [ChatCommand("lset")]
        private void lset(BasePlayer player, string command, string[] args)
        {

            PrintToChat(player, lang.GetMessage("localtime", this, player.UserIDString)
                .Replace("{localtime}", ShowTime(DateTime.Now.ToLocalTime())));

            if (!permission.UserHasPermission(player.UserIDString, adminPriv))
                return;

            if (args.Count() > 0)
            {
                #region toggle
                //if (args[0].ToLower() == "off")
                //{
                //    Config["LocalTimeDamageControlOn"] = false;
                //    PrintToChat(player, lang.GetMessage("offstatus", this, player.UserIDString)
                //        .Replace("{status}", ((bool)Config["LocalTimeDamageControlOn"] ? "ON" : "OFF")));
                //    SaveConfig();
                //    return;
                //}

                //if (args[0].ToLower() == "on")
                //{
                //    Config["LocalTimeDamageControlOn"] = true;
                //    PrintToChat(player, lang.GetMessage("onstatus", this, player.UserIDString)
                //        .Replace("{status}", ((bool)Config["LocalTimeDamageControlOn"] ? "ON" : "OFF"))
                //        .Replace("{starttime}", ShowTime(getStartTime()))
                //        .Replace("{endtime}", ShowTime(getEndTime())));

                //    SaveConfig();
                //    return;
                //}

                if (args[0].ToLower() == "activate")
                {
                    activated = true;
                    PrintToChat(player, lang.GetMessage("activate", this, player.UserIDString));
                    return;
                }
                
                if (args[0].ToLower() == "deactivate")
                {
                    activated = false;
                    PrintToChat(player, lang.GetMessage("deactivate", this, player.UserIDString));
                    return;
                }

                if (args[0].ToLower() == "info")
                {
                    if ((bool)Config["LocalTimeDamageControlInformPlayer"] == true) 
                        Config["LocalTimeDamageControlInformPlayer"] = false;

                    else
                        Config["LocalTimeDamageControlInformPlayer"] = true;

                    PrintToChat(player, lang.GetMessage("info", this, player.UserIDString).Replace("{act}",$"{(((bool)Config["LocalTimeDamageControlInformPlayer"] == true)?"WILL":"WILL NOT") }"));

                        
                    SaveConfig();
                    return;


                }
                #endregion

                if (args[0].ToLower() == "start")
                    {
                    try
                    {
                        //DateTime dateTime = DateTime.ParseExact(args[1].ToUpper(), "HH:mm", CultureInfo.InvariantCulture);

                        DateTime dateTime = DateTime.Parse(args[1].ToUpper());

                        Config["LocalTimeDamageControlStart"] = dateTime.ToString("hh:mm tt" , CultureInfo.CurrentCulture);

                        SaveConfig();

                        PrintToChat(player, lang.GetMessage("starts", this, player.UserIDString)
                            .Replace("{starts}", ShowTime(Config["LocalTimeDamageControlStart"])));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errorstart", this, player.UserIDString));
                        
                    }
                    return;
                }
                if (args[0].ToLower() == "minutes")
                {
                    try
                    {
                        Config["LocalTimeDamageControlDuratationMinutes"] = int.Parse(args[1]);
                        SaveConfig();
                        PrintToChat(lang.GetMessage("duration", this, player.UserIDString)
                            .Replace("{duration}", Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errormin", this, player.UserIDString));

                    }
                    return;
                }
                if (args[0].ToLower() == "hours")
                {
                    try
                    {
                        Config["LocalTimeDamageControlDuratationMinutes"] = int.Parse(args[1]) * 60;
                        SaveConfig();
                        PrintToChat(lang.GetMessage("duration", this, player.UserIDString)
                            .Replace("{duration}", Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errorhour", this, player.UserIDString));
                    }
                    return;
                }

            }
            else
            {
                for (int ii = 1; ii <= 9; ii++)
                    PrintToChat(player, lang.GetMessage("help" + ii, this, player.UserIDString)
                        .Replace("{starttime}", ShowTime(getStartTime()))
                        .Replace("{endtime}", ShowTime(getEndTime())));
            }


        }

        string ShowTime(object TimeIn)
        {
            return DateTime.Parse(TimeIn.ToString()).ToString("hh:mm tt");
        }

        Dictionary<string, string> get()
        {
            return new Dictionary<string, string>
            {
                { "localtime", "Local time is {localtime}." },
                { "nodamage", "You cannot cause damage during between {starttime} and {endtime}." },
                { "activated", "You cannot cause damage during while LocalTimeDamageControl is activated." },
                { "starts", "LocalTimeDamageControl starts at {starts}." },
                { "remains", "LocalTimeDamageControl remains on for {remains}." },
                { "onstatus", "LocalTimeDamageControl is ON. It will become active @ {starttime} until {endtime}" },
                { "offstatus", "LocalTimeDamageControl is {status}. It will NOT become active." },
                { "duration", "LocalTimeDamageControl duration is {duration} minutes." },
                { "errorstart", "Error, please 24 hour time format: i.e 08:00 for 8 am. 20:00 for 8pm." },
                { "errormin", "Error, please enter an integer i.e: 60 for 60 minutes." },
                { "errorhour", "Error, please enter an integer i.e: 2 for 180 minutes." },
                { "activate", "- Activates (ignores configuration)" },
                { "deactivate", "- Deactivate (back to normal, uses config)" },
                { "info", "- LocalTimeDamageControl {act} inform players when they are unable to damage." },
                { "help1", "/lset start 8:00 am ~ Set start time for damage control." },
                { "help2", "/lset minutes 60    ~ Set duration in minutes for damage control." },
                { "help3", "/lset hours 12      ~ Set duration in hours for damage control." },
                { "help4", "/lset activate      ~ Force damage control ON, ignore config." },
                { "help5", "/lset info          ~ Toggle player message, when damage is denied." },
                { "help6", "/lset deactivate    ~ use configured times." },
                { "help7", "/lset off           ~ Turn off damage control." },
                { "help8", "/lset on            ~ Turn on damage control during set times. " },
                { "help9", "- starts at {starttime} ends at {endtime}. (Server's time)" }

            };
        }

    }
}
