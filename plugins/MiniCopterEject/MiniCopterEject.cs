﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mini Copter Eject", "Ryz0r", "1.0.2")]
    [Description("Allows the pilot to eject the passenger from a MiniCopter.")]
    
    public class MiniCopterEject : RustPlugin
    {
        private const string EjectPerm = "minicoptereject.use";
        
        #region Config/Locale
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Self Eject If No Passenger")]
            public bool SelfEject = false;
            
            [JsonProperty(PropertyName = "Send Messages on Eject")]
            public bool SendOnEject = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotClient"] = "This command is meant to be run from a client, not the console.",
                ["NoPerm"] = "You don't have the permissions to use this command.",
                ["NotMounted"] = "You are not mounted, or not in a MiniCopter and may not use this functionality.",
                ["PlayerEjected"] = "You have been ejected from the MiniCopter by {0}.",
                ["PlayerEject"] = "You have ejected {0} from the MiniCopter.",
                ["NoPlayer"] = "There is no player to eject from the passenger seat.",
                ["SelfEject"] = "You have ejected yourself from the MiniCopter. Yikes!"
            }, this); 
        }
        
        private void Init()
        {
            AddCovalenceCommand("eject", nameof(EjectCommand));
            permission.RegisterPermission(EjectPerm, this);
        }
        #endregion
        #region Hooks
        private void DoEject(BaseVehicle bv, BasePlayer bp, bool selfEject = false)
        {
            BasePlayer mountedPassenger = null;
            mountedPassenger = selfEject ? bv.mountPoints[0].mountable._mounted : bv.mountPoints[1].mountable._mounted;
            
            var dismountPlayer = (BasePlayer.Find(mountedPassenger.UserIDString));

            if (dismountPlayer.IsAdmin || permission.UserHasGroup(dismountPlayer.UserIDString, "admin") ||
                dismountPlayer.net.connection.authLevel >= 1) return;
                mountedPassenger.EnsureDismounted();
                
            if (!_config.SendOnEject) return;
            

            if (selfEject)
            {
                bp.ChatMessage(lang.GetMessage("SelfEject", this, bp.UserIDString));
            }
            else
            {
                bp.ChatMessage(lang.GetMessage("PlayerEject", this, bp.UserIDString).Replace("{0}", dismountPlayer.displayName));
                dismountPlayer.ChatMessage(lang.GetMessage("PlayerEjected", this, dismountPlayer.UserIDString).Replace("{0}", bp.displayName));
            }
        }
        private static void SpawnButton(Component entity)
        {
            var button = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/button/button.prefab", entity.transform.position) as PressButton;
            if (button == null) return;

            button.SetParent(entity.GetComponent<BaseVehicle>());
            var t = button.transform;
            
            t.localPosition = new Vector3(-0.10f, -0.25f, 1f);
            t.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));
            t.localScale += new Vector3(0.1f, 0.1f, 0.1f);
            
            button.Spawn();
        }
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is MiniCopter)) return;

            SpawnButton((BaseVehicle) entity);
        }
        private object OnButtonPress(BaseNetworkable button, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, EjectPerm))
            {
                player.ChatMessage(lang.GetMessage("NoPerm", this, player.UserIDString));
                return false;
            }
            
            var parent = button.GetParentEntity();
            if (parent == null || !(parent is MiniCopter)) return null;
            
            var mc = parent as MiniCopter;
            if (mc == null) return null;

            if (!mc.mountPoints[1].mountable._mounted)
            {
                if (_config.SelfEject)
                {
                    DoEject(mc, player, true); 
                }
                else
                {
                    player.ChatMessage(lang.GetMessage("NoPlayer", this, player.UserIDString));
                }
            }
            else
            {
                DoEject(mc, player);  
            }
            return null;   
        }
        #endregion
        private void EjectCommand(IPlayer player, string command, string[] args)
        {
            var bp = player.Object as BasePlayer;
            if (bp == null)
            {
                player.Reply(lang.GetMessage("NotClient", this, player.Id));
                return;
            }

            if (!permission.UserHasPermission(player.Id, EjectPerm))
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
                return;
            }

            if (bp.isMounted && bp.GetMountedVehicle() is MiniCopter)
            {
                if (bp.GetMountedVehicle().mountPoints[1].mountable._mounted)
                {
                    var bv = bp.GetMountedVehicle();
                    DoEject(bv, bp);
                }
                else
                {
                    var bv = bp.GetMountedVehicle();
                    if (_config.SelfEject)
                    {
                        DoEject(bv, bp, true);
                    }
                    else
                    {
                        bp.ChatMessage(lang.GetMessage("NoPlayer", this, bp.UserIDString));
                    }
                }
            }
            else
            {
                bp.ChatMessage(lang.GetMessage("NotMounted", this, bp.UserIDString));
            }
        } 
    }
}