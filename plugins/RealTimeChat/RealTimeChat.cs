using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{        
    [Info("Real Time Chat", "The Friendly Chap", "1.0.2")]
    [Description("Returns the server real time in chat.")]
    public class RealTimeChat : RustPlugin
    {
        #region Language
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TimeMessage"] = "The server's local time is: ",
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TimeMessage"] = "Местное время сервера: ",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TimeMessage"] = "L'heure locale du serveur est: ",
            }, this, "fr");
        }
        #endregion Language
        #region Commands
        [ChatCommand("time")]
        void RealTimeCommand(BasePlayer player)
        {
            PrintToChat(player, lang.GetMessage("TimeMessage", this, player.UserIDString) + DateTime.Now.ToString("hh:mm") + " " + DateTime.Now.ToString("tt"));
        }
        #endregion Commands
        #region Hooks
        void Init()
        {
            LoadDefaultMessages();
        }
        #endregion Hooks
    }
}