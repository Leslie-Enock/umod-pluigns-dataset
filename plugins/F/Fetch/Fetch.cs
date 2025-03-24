using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Fetch", "CatMeat", "1.0.2")]
    [Description("Download JSON file from URI and store in a DATA subfolder")]

    class Fetch : CovalencePlugin
    {
        // Filename obtained from command parameters arg[0] (.json automatically added)
        // URI obtained from command parameters arg[1]
        // Destination directory obtained from config file or `fetch` if not defined
        // tested with dropbox link, must replace ?dl=0 with ?dl=1
        // originally intended to upload fortify files to a server running copypaste, hence the default sub-folder.

        private const string FetchUse = "fetch.use";
        private const string FetchOverwrite = "fetch.overwrite";
        private PluginConfig config;

        #region Init
        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            permission.RegisterPermission(FetchUse, this);
            permission.RegisterPermission(FetchOverwrite, this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts(Lang("FetchConfig"));
            Config.WriteObject(GetDefaultConfig(), true);
        }
        #endregion Init

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FetchConfig"] = "Creating config file.",
                ["FetchSyntax"] = "Syntax: /fetch savefilename url", 
                ["FetchExists"] = "{0} already exists! Choose another name.", 
                ["FetchInvalid"] = "Not a valid URL", 
                ["FetchErrCode"] = "Error: {0} - Could not download",
                ["FetchSaveFail"] = "Error: Failed to save {0}",
                ["FetchSaveFailLog"] = "Error: Failed to save {0} - {1} for user {2}",
                ["FetchSaved"] = "Successfully saved {0}.json" 
            }, this);
        }

        private string Lang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }
        #endregion lang

        #region Config
        private class PluginConfig
        {
            public bool AllowOverwrite;
            public string SubFolder;
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                AllowOverwrite = false,
                SubFolder = "copypaste"
            };
        }
        #endregion Config

        #region Command
        [Command("fetch")]
        private void GetRequest(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(FetchUse))
            {
                // Example: /fetch test https://www.dropbox.com/s/qh5emmd9clss069/2x2fortify.json?dl=1

                if (String.IsNullOrEmpty(config.SubFolder))
                {
                    config.SubFolder = Name.ToLower(); 
                }

                if (args.Length != 2 || String.IsNullOrEmpty(args[0]) || String.IsNullOrEmpty(args[1]))
                {
                    player.Reply(Lang("FetchSyntax", player.Id));
                    return;
                }

                string savefilename = args[0]; 
                string uri = args[1];

                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Interface.Oxide.DataDirectory}\\{config.SubFolder}\\{savefilename}"))
                {
                    if (!config.AllowOverwrite || !player.HasPermission(FetchOverwrite))
                    {
                        player.Reply(Lang("FetchExists", player.Id, savefilename));
                        return;
                    }
                }

                Uri uriResult;
                bool uriTest = Uri.TryCreate(uri, UriKind.Absolute, out uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                if (uriTest)
                {
                    try
                    {
                        webrequest.Enqueue(uri, null, (code, response) =>
                          GetCallback(code, response, uri, savefilename, player), this);
                    }
                    catch
                    {
                        player.Reply(Lang("FetchInvalid", player.Id, uri));
                        return;
                    }
                }
                else
                {
                    player.Reply(Lang("FetchInvalid", player.Id, uri));
                    return;
                }
            }
        }

        private void GetCallback(int code, string response, string uri, string savefilename, IPlayer player)
        {
            if (response == null || code != 200)
            {
                player.Reply(Lang("FetchErrCode", player.Id, code));
                return;
            }

            string SaveFilePath = String.Format($"{Interface.Oxide.DataDirectory}\\{config.SubFolder}\\{savefilename}");

            try
            {
                var json = JObject.Parse(response);
                Interface.Oxide.DataFileSystem.WriteObject(SaveFilePath, json);
            }
            catch
            {
                if (!player.IsServer)
                {
                    Puts(Lang("FetchSaveFailLog", player.Id, savefilename, SaveFilePath, player.Name));
                }

                player.Reply(Lang("FetchSaveFail", player.Id, savefilename));
                return;
            }

            player.Reply(Lang("FetchSaved", player.Id, savefilename));
        }
        #endregion Command
    }
}
