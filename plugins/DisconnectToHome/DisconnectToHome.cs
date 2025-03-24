using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Disconnect To Home", "Ryz0r", "1.0.2")]
    [Description("Sends a player back to their defined home location when they disconnect.")]
    public class DisconnectToHome : RustPlugin
    {
        const string UsePerm = "disconnecttohome.use";
        
        private PluginData _data;
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        
        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Home Locations")]
            public Dictionary<string, Vector3> HomeLocations = new Dictionary<string, Vector3>();
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BuildingBlocked"] = "You are building blocked and may not set your disconnect location.",
                ["LocationSetAlready"] = "You have already set a location. If you wish to update it, please type /disconnecthere remove then set it again.",
                ["LocationNotSet"] = "You have not yet set a location! Please do /disconnecthere set.",
                ["ValidArgs"] = "/disconnecthere set - Sets your disconnect location.\n/disconnecthere remove - Removes your disconnect location.",
                ["DisconnectRemoved"] = "Your disconnect location has been removed.",
                ["DisconnectAdded"] = "Your disconnect location has been set as your current location.",
                ["HomeTeleported"] = "You have been teleported back to your set location.",
                ["NoPerm"] = "You do not have the permissions to use this command.",
                ["FoundationOrFloor"] = "You must be on a foundation or a floor."
            }, this); 
        }

        private void Loaded()
        {
            permission.RegisterPermission(UsePerm, this);
            LoadData();
        }
        
        private void OnServerSave()
        {
            SaveData();
        }

        [ChatCommand("disconnecthere")]
        private void DisconnectCommand(BasePlayer bp, string command, string[] args)
        {
            if (!permission.UserHasPermission(bp.UserIDString, UsePerm))
            {
                bp.ChatMessage(lang.GetMessage("NoPerm", this, bp.UserIDString));
                return;
            }
            
            if (bp.IsBuildingBlocked())
            {
                bp.ChatMessage(lang.GetMessage("BuildingBlocked", this, bp.UserIDString));
                return;
            }

            if (args.Length == 0 || args.Length > 1)
            {
                bp.ChatMessage(lang.GetMessage("ValidArgs", this, bp.UserIDString));
                return;
            }

            switch (args[0])
            {
                case "remove":
                    if (!_data.HomeLocations.ContainsKey(bp.UserIDString))
                    {
                        bp.ChatMessage(lang.GetMessage("LocationNotSet", this, bp.UserIDString));
                        return;
                    }

                    _data.HomeLocations.Remove(bp.UserIDString);
                    bp.ChatMessage(lang.GetMessage("DisconnectRemoved", this, bp.UserIDString));
                    SaveData();
                    break;

                case "set":
                    if (_data.HomeLocations.ContainsKey(bp.UserIDString))
                    {
                        bp.ChatMessage(lang.GetMessage("LocationSetAlready", this, bp.UserIDString));
                        return;
                    }

                    if (!CheckIfOnFoundationOrFloor(bp))
                    {
                        bp.ChatMessage(lang.GetMessage("FoundationOrFloor", this, bp.UserIDString));
                        return;
                    }

                    _data.HomeLocations.Add(bp.UserIDString, bp.transform.position);
                    bp.ChatMessage(lang.GetMessage("DisconnectAdded", this, bp.UserIDString));
                    SaveData();
                    break;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (permission.UserHasPermission(player.UserIDString, UsePerm) &&
                _data.HomeLocations.ContainsKey(player.UserIDString))
            {
                player.Teleport(_data.HomeLocations[player.UserIDString]);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, UsePerm) &&
                _data.HomeLocations.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(lang.GetMessage("HomeTeleported", this, player.UserIDString));
            }
        }

        private bool CheckIfOnFoundationOrFloor(BasePlayer player)
        {
            var position = player.transform.position;
            var foundationCheck = false;
            RaycastHit hitinfo;

            if (Physics.Raycast(position + new Vector3(0f, 0.2f, 0f), Vector3.down, out hitinfo, 3f,
                Layers.Mask.Construction) && hitinfo.GetEntity().IsValid())
            {
                var entity = hitinfo.GetEntity();
                if (entity.PrefabName.Contains("floor") || entity.PrefabName.Contains("foundation"))
                {
                    foundationCheck = true;
                }
            }

            return foundationCheck;
        }
    }
}