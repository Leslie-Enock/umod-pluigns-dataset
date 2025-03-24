using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;
using ProtoBuf;
using Rust;
using System.Linq;
using System.Numerics;
using System.Resources;
using Network;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Free Laptop", "Sam Greaves", "2.1.1")]
    [Description("A free plugin to add functionality to laptops")]
    class FreeLaptop : RustPlugin
    {
        private readonly int _layerMask = LayerMask.GetMask("Construction", "Deployed", "Default");
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoLaptop"] = "You dont have a laptop in your hands! noob!",
                ["NoInteraction"] = "There is nothing to interact with!",
                ["NoCodeLock"] = "There is no code lock to open!",
                ["AuthorizedPlayers"] = "Authorized players:\n",
                ["NoAuthorizedPlayers"] = "No authorized players",
                ["LaptopDistanceSettings"] = "Laptop can be used within a distance of ",
                ["CanOpenDoorsSettings"] = "Players can open code locked doors with laptops.",
                ["CanSeeTcAuthSettings"] = "Players can see who is authorized on tc with laptops.",
                ["CanSeeDoorAuth"] = "Players can see who is authorized on doors with laptops.",
                ["CanUnlockTc"] = "Players can see the tc code with a laptop",
                ["CanSeeTurretAuthSettings"] = "Players can see who is authorized on turrets with laptops.",
                ["CanUnlockTurretSettings"] = "Players can unlock turrets with laptops.",

            }, this);
        }

        [ChatCommand("laptop")]
        void ChatLaptop(BasePlayer player, string command, string[] args)
        {
            // first check for laptop
            Item item = LaptopInHands(player);
            if (item != null)
            {
                // then get entity player is looking at 
                BaseEntity entity = GetEntityLookingAt(player);

                // then decide which behaviour stream to go down
                if (args != null && args.Length > 0)
                {
                    if (args[0] == "settings")
                        ShowSettings(player);
                    if (args[0] == "auth")
                        Interact(player, entity, true);
                }
                else
                {
                    if (Interact(player, entity, false))
                        item.UseItem();
                }
            }
            else
            {
                player.ChatMessage(Lang("NoLaptop", player.UserIDString));
            }
        }

        /// <summary>
        /// Method to check if active item is a laptop.
        /// </summary>
        /// <param name="player">The player who's active item we check.</param>
        /// <returns>Item if its a laptop, null if not.</returns>
        private Item LaptopInHands(BasePlayer player)
        {
            Item activeItem = (player.GetActiveItem() as Item);

            return activeItem?.info.shortname == "targeting.computer" ? activeItem : null;
        }

        /// <summary>
        /// Get the entity the player is looking at within the config range.
        /// </summary>
        /// <param name="player">The player who is looking at entity.</param>
        /// <returns>The entity being looked at in range or null.</returns>
        private BaseEntity GetEntityLookingAt(BasePlayer player)
        {
            RaycastHit hit;
            BaseEntity entity = null;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, config.LaptopDistance, _layerMask))
            {
                return hit.GetEntity();
            }

            return null;
        }

        /// <summary>
        /// Shows the settings from the config object in a nice way for players.
        /// </summary>
        /// <param name="player">Player to show settings to.</param>
        private void ShowSettings(BasePlayer player)
        {
            player.ChatMessage(Lang("LaptopDistanceSettings", player.UserIDString) + config.LaptopDistance);
            if (config.CanOpenDoors)
                player.ChatMessage(Lang("CanOpenDoorsSettings", player.UserIDString));
            if (config.CanSeeDoorAuth)
                player.ChatMessage(Lang("CanSeeDoorAuth", player.UserIDString));
            if (config.CanSeeTcAuth)
                player.ChatMessage(Lang("CanSeeTcAuthSettings", player.UserIDString));
            if (config.CanUnlockTc)
                player.ChatMessage(Lang("CanUnlockTc", player.UserIDString));
            if (config.CanSeeTurretAuth)
                player.ChatMessage(Lang("CanSeeTurretAuthSettings", player.UserIDString));
            if (config.CanUnlockTurret)
                player.ChatMessage(Lang("CanUnlockTurretSettings", player.UserIDString));
        }

        /// <summary>
        /// Attempt to interact with the given entity.
        /// </summary>
        /// <param name="player">Player who is interacting.</param>
        /// <param name="entity">Entity to interact with.</param>
        /// <param name="showAuth">If the interaction is to show auth list.</param>
        /// <returns>True if interaction was successful.</returns>
        private bool Interact(BasePlayer player, BaseEntity entity, bool showAuth)
        {
            if (entity)
            {
                if (entity is Door)
                {
                    if (showAuth && config.CanSeeDoorAuth)
                        return DisplayDoorAuthorizedPlayers(player, entity as Door);
                    else if (config.CanOpenDoors)
                        return DoorUnlocking(entity as Door, player);
                }
                else if (entity is BuildingPrivlidge)
                {
                    if (showAuth && config.CanSeeTcAuth)
                        return DisplayTcAuthorizedPlayers(player, entity as BuildingPrivlidge);
                    else if (config.CanUnlockTc)
                        return TcUnlocking(entity as BuildingPrivlidge, player);
                }
                else if (entity is AutoTurret)
                {
                    if (showAuth && config.CanSeeTurretAuth)
                        return DisplayTurretAuthorizedPlayers(player, entity as AutoTurret);
                    else if (config.CanUnlockTurret)
                        return TurretUnlocking(entity as AutoTurret, player);
                }
            }

            player.ChatMessage(Lang("NoInteraction", player.UserIDString));
            return false;
        }

        #region Interactions

        /// <summary>
        /// Unlocks the given door.
        /// </summary>
        /// <param name="door">Door to unlock.</param>
        /// <returns>True if the door unlock is successful.</returns>
        private bool DoorUnlocking(Door door, BasePlayer player)
        {
            if (door != null)
            {
                var lockSlot = door.GetSlot(BaseEntity.Slot.Lock);

                if (lockSlot is CodeLock)
                {
                    door.SetOpen(!door.IsOpen());
                    return true;
                }
                else
                {
                    player.ChatMessage(Lang("NoCodeLock", player.UserIDString));
                }
            }

            return false;
        }

        /// <summary>
        /// Unlocks the given tc.
        /// </summary>
        /// <param name="tc">Tc to unlock.</param>
        /// <returns>True if the tc unlock is successful.</returns>
        private bool TcUnlocking(BuildingPrivlidge tc, BasePlayer player)
        {
            if (tc != null)
            {
                var lockSlot = tc.GetSlot(BaseEntity.Slot.Lock);

                if (lockSlot is CodeLock)
                {
                    var codeLock = (CodeLock) lockSlot;
                    player.ChatMessage(codeLock.code);
                    return true;
                }
                else
                {
                    player.ChatMessage(Lang("NoCodeLock", player.UserIDString));
                }
            }

            return false;
        }

        /// <summary>
        /// Unlocks the given turret.
        /// </summary>
        /// <param name="turret">Turret to unlock.</param>
        /// <returns>True if the turret unlock is successful.</returns>
        private bool TurretUnlocking(AutoTurret turret, BasePlayer player)
        {
            if (turret != null)
            {
                turret.SetIsOnline(false);
                return true;
            }

            return false;
        }

        #endregion

        #region Get Authorized players

        /// <summary>
        /// Display a list of authorized players for the given tc.
        /// </summary>
        /// <param name="player">Player making the request.</param>
        /// <param name="tc">Tc to get list from.</param>
        /// <returns>True.</returns>
        private bool DisplayTcAuthorizedPlayers(BasePlayer player, BuildingPrivlidge tc)
        {
            var lockSlot = tc.GetSlot(BaseEntity.Slot.Lock);

            if (lockSlot is CodeLock)
            {
                string msg = Lang("AuthorizedPlayers", player.UserIDString);
                int count = 0;

                foreach (var user in tc.authorizedPlayers)
                {
                    count++;
                    msg += $"{count}. {GetName(user.userid.ToString())}\n";
                }

                player.ChatMessage(count > 0 ? msg : Lang("NoAuthorizedPlayers", player.UserIDString));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Display a list of authorized players for the given door.
        /// </summary>
        /// <param name="player">Player making the request.</param>
        /// <param name="door">Door to get list from.</param>
        /// <returns>True.</returns>
        private bool DisplayDoorAuthorizedPlayers(BasePlayer player, Door door)
        {
            var lockSlot = door.GetSlot(BaseEntity.Slot.Lock);

            if (lockSlot is CodeLock)
            {
                var codeLock = (CodeLock) lockSlot;
                string msg = Lang("AuthorizedPlayers", player.UserIDString, door.ShortPrefabName,
                                 GetName(door.OwnerID.ToString()));

                int authed = 0;

                foreach (var user in codeLock.whitelistPlayers)
                {
                    authed++;
                    msg += $"{authed}. {GetName(user.ToString())}\n";
                }

                player.ChatMessage(authed == 0 ? Lang("NoAuthorizedPlayers", player.UserIDString) : msg);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Display a list of authorized players for the given turret.
        /// </summary>
        /// <param name="player">Player making the request.</param>
        /// <param name="turret">Turret to get list from.</param>
        /// <returns></returns>
        private bool DisplayTurretAuthorizedPlayers(BasePlayer player, AutoTurret turret)
        {
            string msg = Lang("AuthorizedPlayers", player.UserIDString);
            int count = 0;

            foreach (var user in turret.authorizedPlayers)
            {
                count++;
                msg += $"{count}. {GetName(user.userid.ToString())}\n";
            }

            player.ChatMessage(count > 0 ? msg : Lang("NoAuthorizedPlayers", player.UserIDString));

            return true;
        }

        #endregion

        private string GetName(string id) => id == "0" ? "[SERVERSPAWN]" : covalence.Players.FindPlayer(id)?.Name;

        private string Lang(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        private class PluginConfig
        {
            public float LaptopDistance;
            public bool CanOpenDoors;
            public bool CanSeeTcAuth;
            public bool CanSeeDoorAuth;
            public bool CanUnlockTc;
            public bool CanSeeTurretAuth;
            public bool CanUnlockTurret;
        }

        private PluginConfig config;

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                CanOpenDoors = true,
                LaptopDistance = 1f,
                CanSeeTcAuth = true,
                CanSeeDoorAuth = true,
                CanUnlockTc = true,
                CanSeeTurretAuth = true,
                CanUnlockTurret = true,
            };
        }
    }
}