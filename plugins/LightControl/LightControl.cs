using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Rust.Modular;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Network;
using Oxide.Core.Configuration;
using Rust;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("Light Control", "Hovmodet", "1.0.4")]
    [Description("Toggle lights separately")]
    public class LightControl : RustPlugin
    {
        private const string UsePermission = "LightControl.use";

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LightHowto"] = "To toggle the light on your {0}, use the \"headtoggle\" command.",
                ["LightNoPermission"] = "You dont have permissions to use this command.",
            }, this);
        }

        public bool hasPerm(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), UsePermission))
                return false;

            return true;
        }

        [ConsoleCommand("headtoggle")]
        private void lightheadcmd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;
 
            if(!hasPerm(player))
            {
                SendReply(player, "<color=red>" + string.Format(lang.GetMessage("LightNoPermission", this, player.UserIDString)) + "</color>");
                return;
            }

            foreach (Item obj in player.inventory.containerWear.itemList)
            {
                ItemModWearable component = obj.info.GetComponent<ItemModWearable>();
                if ((bool)component && component.emissive)
                {
                    obj.SetFlag(global::Item.Flag.IsOn, !obj.HasFlag(global::Item.Flag.IsOn));
                    obj.MarkDirty();
                }
            }
        }

        private void lighthand(BasePlayer player)
        {
            if (player == null)
                return;

            if (!hasPerm(player))
            {
                SendReply(player, "<color=red>" + string.Format(lang.GetMessage("LightNoPermission", this, player.UserIDString)) + "</color>");
                return;
            }

            Item activeItem = player.GetActiveItem();
            if (activeItem != null)
            {
                BaseEntity heldEntity = activeItem.GetHeldEntity();
                if (heldEntity != null)
                {
                    HeldEntity component = heldEntity.GetComponent<HeldEntity>();
                    if ((bool)(UnityEngine.Object)component)
                        component.SendMessage("SetLightsOn", !component.LightsOn(), SendMessageOptions.DontRequireReceiver);
                }
            }

            if (!player.isMounted)
                return;
            player.GetMounted().LightToggle(player);
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.Name.ToLower() == "lighttoggle")
            {
                lighthand(arg.Player());
                return false;
            }
            return null;
        }

        object CanWearItem(PlayerInventory inventory, Item item, int targetSlot)
        {
            BasePlayer player = inventory.containerWear.playerOwner;

            if (player == null)
                return null;

            if (!hasPerm(player))
            {
                return null;
            }

            ItemModWearable component = item.info.GetComponent<ItemModWearable>();

            if (component == null)
                return null;

            if (component.emissive)
            {
                SendReply(player, "<color=yellow>" + string.Format(lang.GetMessage("LightHowto", this, player.UserIDString), item.info.displayName.english) + "</color>");
            }

            return null;
        }

    }
}