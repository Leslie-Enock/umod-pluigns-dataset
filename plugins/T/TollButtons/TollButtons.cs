using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins {
    [Info("Toll Buttons", "KajWithAJ", "0.3.0")]
    [Description("Make players pay toll to press a button using their RP points.")]
    class TollButtons : RustPlugin {

        [PluginReference]
        private readonly Plugin Economics, ServerRewards;

        private const string PermissionUse = "tollbuttons.use";
        private const string PermissionAdmin = "tollbuttons.admin";
        private const string PermissionExclude = "tollbuttons.exclude";

        private StoredData storedData = new StoredData();

        private void Init() {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionExclude, this);
        }

        private void OnServerInitialized() {
            if (ServerRewards == null && Economics == null) {
                PrintError("ServerRewards nor Economics are loaded, at least one of those is required");
            }

            SaveConfig();
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file");
            Config["TransferTollToOwner"] = false;
            Config["MaximumPrice"] = 0;
            Config["Valuta"] = "serverrewards";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ButtonNotFound"] = "No button found",
                ["NoTollSetPermission"] = "You do not have permission to use this command.",
                ["NoButtonOwnership"] = "This button is not yours.",
                ["TollSet"] = "A toll of {0} RP was set for this button.",
                ["InsufficientFunds"] = "Insufficient fonds! You need {0} RP to press this button",
                ["TollPaid"] = "You've been charged {0} RP for pressing this button",
                ["MaximumPrice"] = "The maximum amount to configure as toll is set at {0}",
                ["InvalidNumber"] = "Provide a valid number."
            }, this);
        }

        [ChatCommand("toll")]
        private void ChatCmdCheckButton(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            var raycast = Physics.Raycast(player.eyes.HeadRay(), out hit, 2f, 2097409);
            BaseEntity button = raycast ? hit.GetEntity() : null;
            if (button == null || button as PressButton == null) {
                string message = lang.GetMessage("ButtonNotFound", this, player.UserIDString);
                player.ChatMessage(string.Format(message));
                return;
            }

            if (args.Length >=1 ) {
                if (!permission.UserHasPermission(player.UserIDString, PermissionUse)) {
                    string message = lang.GetMessage("NoTollSetPermission", this, player.UserIDString);
                    player.ChatMessage(string.Format(message));
                    return;
                }
                
                if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin) && button.OwnerID != player.userID) {
                    player.ChatMessage(string.Format(lang.GetMessage("NoButtonOwnership", this, player.UserIDString)));
                    return;
                }

                int cost = 0;
                if (!int.TryParse(args[0], out cost))
                {
                    player.ChatMessage(string.Format(lang.GetMessage("InvalidNumber", this, player.UserIDString)));
                    return;
                }

                int maximumPrice = (int) Config["MaximumPrice"];

                if (maximumPrice > 0 && cost > maximumPrice) {
                    player.ChatMessage(string.Format(lang.GetMessage("MaximumPrice", this, player.UserIDString), maximumPrice));
                    return;
                }

                if (!storedData.TollButtons.ContainsKey(button.net.ID.Value)) {
                    ButtonData buttonData = new ButtonData();
                    buttonData.cost = cost;
                    buttonData.ownerID = player.UserIDString;
                    storedData.TollButtons.Add(button.net.ID.Value, buttonData);
                } else {
                    storedData.TollButtons[button.net.ID.Value].cost = cost;
                }

                player.ChatMessage(string.Format(lang.GetMessage("TollSet", this, player.UserIDString), cost));
            } else {
                int cost = CheckButtonCost(button as PressButton);
                player.ChatMessage(string.Format(lang.GetMessage("TollSet", this, player.UserIDString), cost));
            }
        }

        private object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button.OwnerID == 0) return null;

            int cost = CheckButtonCost(button);

            if (cost > 0) {
                if (permission.UserHasPermission(player.UserIDString, PermissionExclude)) {
                    return null;
                }

                if (button.OwnerID != player.userID) {
                    string valuta = (string) Config["Valuta"];

                    if (!TryPay(player, valuta, cost)) {
                        player.ChatMessage(string.Format(lang.GetMessage("InsufficientFunds", this, player.UserIDString), cost));
                        return true;
                    } else {
                        player.ChatMessage(string.Format(lang.GetMessage("TollPaid", this, player.UserIDString), cost));

                        if ((bool) Config["TransferTollToOwner"] == true) {
                            Transfer(button.OwnerID.ToString(), valuta, cost);
                        }

                        return null;
                    }
                } else {
                    return null;
                }
            } else {
                return null;
            }
        }

        private int CheckButtonCost(PressButton button) {
            if (!storedData.TollButtons.ContainsKey(button.net.ID.Value)) {
                return 0;
            } else {
                return storedData.TollButtons[button.net.ID.Value].cost;
            }
        }

        private void Transfer(string beneficiary, string valuta, int amount) {
            switch (valuta.ToLower()) {
                case "economics":
                    Economics?.Call("Deposit", beneficiary, (double)amount);
                    break;

                case "serverrewards":
                    ServerRewards?.Call("AddPoints", beneficiary, amount);
                    break;
            }
        }

        private bool TryPay(BasePlayer player, string valuta, int price) {
            if (!CanPay(player, valuta, price)) {
                return false;
            }

            switch (valuta.ToLower()) {
                case "economics":
                    Economics?.Call("Withdraw", player.userID, (double)price);
                    break;

                case "serverrewards":
                    ServerRewards?.Call("TakePoints", player.userID, price);
                    break;
            }
            return true;
        }

        private bool CanPay(BasePlayer player, string valuta, int cost) {
            int missingAmount = CheckBalance(valuta, cost, player.userID);
            return missingAmount <= 0;
        }

        private int CheckBalance(string valuta, int price, ulong playerId) {
            switch (valuta.ToLower()) {
                case "serverrewards":
                    var points = ServerRewards?.Call("CheckPoints", playerId);
                    if (points is int) {
                        var n = price - (int)points;
                        return n <= 0 ? 0 : n;
                    }
                    return price;
                case "economics":
                    var balance = Economics?.Call("Balance", playerId);
                    if (balance is double){
                        var n = price - (double)balance;
                        return n <= 0 ? 0 : (int)Math.Ceiling(n);
                    }
                    return price;
                default:
                    PrintError($"Valuta {valuta} not recognized.");
                    return price;
            }
        }

        private class StoredData
        {
            public readonly Dictionary<ulong, ButtonData> TollButtons = new Dictionary<ulong, ButtonData>();
        }

        private class ButtonData
        {
            public int cost = 0;
            public string ownerID = "";
        }

        private void LoadData() => storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Name);

        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject(this.Name, storedData);

        private void OnServerSave() => SaveData();

        private void OnNewSave(string name)
        {
            PrintWarning("Map wipe detected - clearing TollButtons...");

            storedData.TollButtons.Clear();
            SaveData();
        }
    }
}
