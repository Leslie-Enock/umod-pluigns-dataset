using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Custom Ammo", "Ryz0r", "1.0.1")]
    [Description("Allows players to use a custom ammunition in their gun they are holding. Enabling custom ammo will give infinite ammo.")]
    internal class CustomAmmo : RustPlugin
    { 
        private const string NoReloadPerm = "customammo.noreload";
        private const string NoDamagePerm = "customammo.nodamage";
        private const string CommandPerm = "customammo.command";

        private static List<string> _toggledList = new List<string>();
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _toggledList);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BadFormat"] = "Incorrect Format - Try: /switch ammo.rifle",
                ["ArgOver"] = "You provided {0} arguments, but 1 was expected.",
                ["InvalidItem"] = "The item you are wielding is not a valid weapon.",
                ["BadAmmo"] = "Oh god no! This item is prohibited from being used. It will do bad things.",
                ["InvalidAmmo"] = "That is not a valid ammo type - Try: ammo.rifle, ammo.pistol, etc..",
                ["NoPerm"] = "You do not have the permissions required to use this command.",
                ["Disabled"] = "You have disabled the switch command.",
                ["NotOn"] = "You have not yet initialized the /switch command."
                
            }, this);
        }
        
        private void OnNewSave(string filename)
        {
            if(Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                Interface.Oxide.DataFileSystem.GetFile(Name).Clear();
                Interface.Oxide.DataFileSystem.GetFile(Name).Save();

                Puts($"Wiped '{Name}.json'");
            }
        }
        
        private void Init()
        {
            permission.RegisterPermission(NoReloadPerm, this);
            permission.RegisterPermission(NoDamagePerm, this);
            permission.RegisterPermission(CommandPerm, this);
        }

        [ChatCommand("switch")]
        private void SwitchAmmoCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, CommandPerm))
            {
                if (args.Length == 0)
                {
                    if (_toggledList.Contains(player.UserIDString))
                    {
                        _toggledList.Remove(player.UserIDString);
                        SaveData();
                        
                        SendReply(player, lang.GetMessage("Disabled", this, player.UserIDString));
                    }
                    else
                    {
                        SendReply(player, lang.GetMessage("NotOn", this, player.UserIDString));
                    }
                }
                else
                {
                    if (args.Length > 1)
                    {
                        SendReply(player,
                            string.Format(lang.GetMessage("ArgOver", this, player.UserIDString), args.Length));
                        return;
                    }

                    if (args[0].Contains("rocket") || args[0].Contains("grenade") || !args[0].StartsWith("ammo."))
                    {
                        SendReply(player, lang.GetMessage("BadAmmo", this, player.UserIDString));
                        return;
                    }

                    var weapon = player.GetHeldEntity() as BaseProjectile;

                    if (!weapon.IsValid())
                    {
                        SendReply(player, lang.GetMessage("InvalidItem", this, player.UserIDString));
                        return;
                    }

                    if (!ItemManager.FindItemDefinition(args[0]))
                    {
                        SendReply(player, lang.GetMessage("InvalidAmmo", this, player.UserIDString));
                        return;
                    }

                    if (weapon == null) return;
                    _toggledList.Add(player.UserIDString);
                    SaveData();
                    
                    weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(args[0]);
                    weapon.SendNetworkUpdateImmediate();
                }
            }
            else
            {
                SendReply(player, lang.GetMessage("NoPerm", this, player.UserIDString));
            }
        }

        private void OnWeaponFired(BaseProjectile weapon, BasePlayer player)
        {
            if (_toggledList.Contains(player.UserIDString))
            {
                if (permission.UserHasPermission(player.UserIDString, NoDamagePerm))
                {
                    weapon.GetItem().condition = weapon.GetItem().info.condition.max;
                    weapon.SendNetworkUpdateImmediate();
                }
            
                if (permission.UserHasPermission(player.UserIDString, NoReloadPerm))
                {
                    if (weapon.primaryMagazine.contents > 0) return;
                    weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                    weapon.SendNetworkUpdateImmediate();
                }
            }
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (_toggledList.Contains(player.UserIDString))
            {
                projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
                projectile.SendNetworkUpdateImmediate();
                return true;
            }
            
            return null;
        }

    }
}