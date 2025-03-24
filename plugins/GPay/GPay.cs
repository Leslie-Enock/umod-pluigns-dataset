using System;
using System.Collections.Generic;
using System.Collections;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Text;

namespace Oxide.Plugins {

    [Info("GPay", "Soccerjunki", "2.0.2")]
    [Description("Auto donation system")]
    class GPay : CovalencePlugin {

        class GPayWebResponse
        {
            public string error { get; set; }
            public string[] commands { get; set; }
        }

        protected override void LoadDefaultConfig() {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            Config["Secret"] = "GPAY_SECRET_KEY";
            SaveConfig();
        }

        private new void LoadDefaultMessages(){
        	lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"NoDonationMessage", "No Donation was found"},
                    {"DonationFoundMessage", "Thanks for donating, your items have been added to your inventory"},
                    {"NoInventorySpace", "Please empty your inventory before claiming your donation!"},
                    {"CouldNotConnect", "Could not connect to the donation server, please try again later!"},
                    {"DonationLink", "You can Donate at https://app.gpay.io/store/SERVERNAME"},
                    {"NoSecretKeyEntered", "This command requires a secret key, example usage : /setupgpay SECRETKEYHERE"},
                    {"PluginRequiresSetup", "Please setup this plugin by typing : /setupgpay SECRETKEYHERE"},
                    {"GPAYSETUPCOMPLETE", "[GPAY] Setup successfully!"},
                    {"PleaseWait", "Please Wait"},
            }, this);
        }

        string Lang(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        [Command("donate")]
        void DonateCommand(IPlayer player, string command, string[] args) {
            player.Reply(Lang("DonationLink", player.Id));
            return;
        }

        [Command("setupgpay")]
        void setupGpay(IPlayer player, string command, string[] args){
            if(args.Length > 0){
                string secret = Config["Secret"].ToString();
                if(secret.Equals("GPAY_SECRET_KEY")){
                    Config["Secret"] = args[0];
                    SaveConfig();
                    player.Reply(Lang("GPAYSETUPCOMPLETE", player.Id));
                }else{
                    Puts("SECRET KEY ALREADY SETUP, PLEASE CHANGE THE CONFIG FILE TO SET IT AGAIN!");
                }
            }else{
                player.Reply(Lang("NoSecretKeyEntered", player.Id));
            }
        }

        [Command("claimdonation")]
        void ClaimDonatCommand(IPlayer player, string command, string[] args) {
            if(Config["Secret"].ToString().Equals("GPAY_SECRET_KEY")){
                player.Reply(Lang("PluginRequiresSetup", player.Id));
                return;
            }
            player.Reply(Lang("PleaseWait", player.Id));
            string steamid = player.Id;
            string secret = Config["Secret"].ToString();
            webrequest.EnqueueGet("http://api.gpay.io/steam/online/" + steamid + "/" + secret , (code, response) => executeOnlineCommands(code, response, player), this);
        }


        private void Init()
        {
            /**
             * !WARNING PLEASE READ CAREFULLY!
             * It's not recommended to change this as you will probably hit our rate limiting service and get your server banned from our api server
             * Once you're banned it can take upto 30 days to get unbanned
             *
             **/
            timer.Repeat(60.0f, 0, () =>
            {
                RetrieveAllPendingOfflineCommands();
            });
        }
       
        private void RetrieveAllPendingOfflineCommands() {
            string secret = Config["Secret"].ToString();
            webrequest.EnqueueGet("http://api.gpay.io/steam/offline/" + secret, (code, response) => executeOfflineCommands(code, response), this);
        }

        private void executeOfflineCommands(int code, string response)
        {
            if (response == null || code != 200)
            {
                Puts($"Error: {code} - Could not contact GPAY server ");
                return;
            }
            else
            {
                var json = JsonConvert.DeserializeObject<GPayWebResponse>(response);
                if (json.error != null)
                {
                    Puts(json.error.ToString());
                }
                else
                {
                    if (json.commands.Length > 0)
                    {
                        foreach (string s in json.commands)
                        {
                            server.Command(s.ToString());
                        }
                    }
                }
            }
        }

        void executeOnlineCommands(int code, string response, IPlayer player) {
            if (response == null || code != 200) {
                Puts($"Error: {code} - Could not contact GPAY server ");
                player.Reply(Lang("CouldNotConnect", player.Id));
                return;
            }else{
                var json = JsonConvert.DeserializeObject<GPayWebResponse>(response);
                if(json.error != null){
                    player.Reply(Lang("CouldNotConnect", player.Id));
                    Puts(json.error.ToString());
                }else{
                    if(json.commands.Length > 0){
                            foreach (string s in json.commands){
                                StringBuilder buildertwo = new StringBuilder(s.ToString());
                                buildertwo.Replace("<username>", player.Name.ToString());
                                buildertwo.Replace("<steamid>", player.Id);
                                string commandToRunb = buildertwo.ToString();
                                server.Command(commandToRunb);
                            }
                    }else{
                        Puts("No commands to run found! Have you setup your commands on the GPay cpanel?");
                        player.Reply(Lang("NoDonationMessage", player.Id));
                    }
                }
            }

        }
    }
}
