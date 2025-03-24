using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("PirateInsults", "Baron Von Finchus", "1.0.5")]
    [Description("Send insults to yourself or other players.")]

    class PirateInsults : CovalencePlugin
    {
        void Init()
        {
            SetupLanguage();
            permission.RegisterPermission("pirateinsults.use", this);
        }

        void SetupLanguage()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use this command.",
                ["InvalidSyntax"] = "Invalid syntax. Syntax: /insult [me|playername].",
                ["NoPlayersFound"] = "No players were found with that name.",
                ["MultiplePlayersFound"] = "Multiple players were found with that name.",
                ["WebRequestFailed"] = "WebRequest to {0} failed!",
                ["YouInsultedYourself"] = "[PirateInsult:] Ye burned yer own jolly roger by mumbling t' yourself:\n<color=yellow>{0}</color>",
                ["YouInsultedPlayer"] = "[PirateInsult:] Ye shouted <color=yellow>{1}</color> at <color=yellow>{0}</color>, the scurvy dog will rue the day they crossed ye!",
                ["PlayerInsultedYou"] = "[PirateInsult:] Avast! <color=yellow>{0}</color> fired a shot accross yer bow by shouting:\n<color=yellow>{1}</color>"
            }, this, "en");
        }

        [Command("pirateinsult")]
        void cmdInsult(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("pirateinsults.use"))
            {
                player.Reply(lang.GetMessage("NoPermission", this, player.Id));
                return;
            }

            if (args == null || args.Length == 0)
            {
                player.Reply(lang.GetMessage("InvalidSyntax", this, player.Id));
                return;
            }

            IPlayer targetPlayer;

            if (args[0] == "me")
                targetPlayer = player;
            else
            {
                IEnumerable<IPlayer> targetList = players.FindPlayers(args[0]);

                if (targetList.Count() < 1)
                {
                    player.Reply(lang.GetMessage("NoPlayersFound", this, player.Id));
                    return;
                }

                if (targetList.Count() > 1)
                {
                    player.Reply(lang.GetMessage("MultiplePlayersFound", this, player.Id));
                    return;
                }

                targetPlayer = players.FindPlayer(args[0]);
            }

            string insultURL = "https://pirate.monkeyness.com/api/insult";
            webrequest.Enqueue(insultURL, null, (code, response) =>
            {

                if (code != 200 || response == null)
                {
                    player.Reply(string.Format(lang.GetMessage("WebRequestFailed", this, player.Id), insultURL));
                    return;
                }

                //JObject joResponse = JObject.Parse(response);                   
                //var returnedinsult = joResponse["insult"];
                if (targetPlayer == player)
                {
                    player.Reply(string.Format(lang.GetMessage("YouInsultedYourself", this, player.Id), response));
                    return;
                }

                player.Reply(string.Format(lang.GetMessage("YouInsultedPlayer", this, player.Id), targetPlayer.Name, response));
                targetPlayer.Reply(string.Format(lang.GetMessage("PlayerInsultedYou", this, targetPlayer.Id), player.Name, response));


            }, this);

        }
    }
}