using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Ignore", "MisterPixie", "2.1.03")]
    [Description("Just an ignore API.")]
    class Ignore : CovalencePlugin
    {
        private ConfigData configData;
        private Dictionary<string, PlayerData> IgnoreData;

        class ConfigData
        {
            public int IgnoreLimit { get; set; }
        }

        class PlayerData
        {
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Ignores { get; set; } = new HashSet<string>();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                IgnoreLimit = 30
            };
            Config.WriteObject(config, true);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"List", "Ignored {0}:\n{1}"},
                {"NoIngored", "Your ignore list is empty."},
                {"NotOnIgnorelist", "{0} not found on your ignore list."},
                {"IgnoreRemoved", "{0} was removed from your ignore list."},
                {"PlayerNotFound", "Player '{0}' not found."},
                {"CantAddSelf", "You cant add yourself."},
                {"AlreadyOnList", "{0} is already ignored."},
                {"IgnoreAdded", "{0} is now ignored."},
                {"IgnorelistFull", "Your ignore list is full."},
                {"HelpText", "Use /ignore <add|+|remove|-|list> <name/steamID> to add/remove/list ignores"},
                {"Syntax", "Syntax: /ignore <add/+/remove/-> <name/steamID> or /ignore list"}
            }, this);
        }

        private void Init()
        {

            configData = Config.ReadObject<ConfigData>();
            try
            {
                IgnoreData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>(Name);
            }
            catch
            {
                IgnoreData = new Dictionary<string, PlayerData>();
            }

            AddCovalenceCommand("ignore", "cmdIgnore");
        }

        private void SaveIgnores()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, IgnoreData);
        }

        private bool AddIgnore(string playerId, string ignoreId)
        {
            var playerData = GetPlayerData(playerId);
            if (playerData.Ignores.Count >= configData.IgnoreLimit || !playerData.Ignores.Add(ignoreId)) return false;
            SaveIgnores();
            return true;
        }

        private bool RemoveIgnore(string playerId, string ignoreId)
        {
            if (!GetPlayerData(playerId).Ignores.Remove(ignoreId)) return false;
            SaveIgnores();
            return true;
        }

        private bool HasIgnored(string playerId, string ignoreId)
        {
            return GetPlayerData(playerId).Ignores.Contains(ignoreId);
        }

        private bool AreIgnored(string playerId, string ignoreId)
        {
            return GetPlayerData(playerId).Ignores.Contains(ignoreId) && GetPlayerData(ignoreId).Ignores.Contains(playerId);
        }

        private bool IsIgnored(string playerId, string ignoreId)
        {
            return GetPlayerData(ignoreId).Ignores.Contains(playerId);
        }

        private string[] GetIgnoreList(string playerId)
        {
            var playerData = GetPlayerData(playerId);
            var players = new List<string>();
            foreach (var friend in playerData.Ignores)
                players.Add(GetPlayerData(friend).Name);
            return players.ToArray();
        }

        private string[] IsIgnoredBy(string player)
        {
            PlayerData value;
            var ignores = IgnoreData.TryGetValue(player, out value) ? value.Ignores.ToArray() : new string[0];
            return ignores.ToList().ConvertAll(f => f.ToString()).ToArray();
        }

        private PlayerData GetPlayerData(string playerId)
        {
            var player = FindPlayer(playerId);
            PlayerData playerData;
            if (!IgnoreData.TryGetValue(playerId, out playerData))
                IgnoreData[playerId] = playerData = new PlayerData();
            if (player != null) playerData.Name = player.Name;
            return playerData;
        }

        private void cmdIgnore(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 0 || args.Length == 1 && !args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                player.Reply(Lang("Syntax", player.Id));
                return;
            }
            switch (args[0].ToLower())
            {
                case "list":
                    var ignoreList = GetIgnoreList(player.Id);
                    if (ignoreList.Length > 0)
                        player.Reply(Lang("List", player.Id,$"{ignoreList.Length}/{configData.IgnoreLimit}", string.Join(", ", ignoreList)));
                    else
                        player.Reply(Lang("NoIngored", player.Id));
                    return;
                case "add":
                case "+":
                    var ignorePlayer = FindPlayer(args[1]);
                    if (ignorePlayer == null)
                    {
                        player.Reply(Lang("PlayerNotFound", player.Id, args[1]));
                        return;
                    }
                    if (player == ignorePlayer)
                    {
                        player.Reply(Lang("CantAddSelf", player.Id));
                        return;
                    }
                    var playerData = GetPlayerData(player.Id);
                    if (playerData.Ignores.Count >= configData.IgnoreLimit)
                    {
                        player.Reply(Lang("IgnorelistFull", player.Id));
                        return;
                    }
                    if (playerData.Ignores.Contains(ignorePlayer.Id))
                    {
                        player.Reply(Lang("AlreadyOnList",player.Id, ignorePlayer.Name));
                        return;
                    }
                    AddIgnore(player.Id, ignorePlayer.Id);
                    player.Reply(Lang("IgnoreAdded", player.Id, ignorePlayer.Name));
                    return;
                case "remove":
                case "-":
                    var ignore = FindIgnore(args[1]);
                    if (ignore == string.Empty)
                    {
                        player.Reply(Lang("NotOnIgnorelist", player.Id, args[1]));
                        return;
                    }
                    var removed = RemoveIgnore(player.Id, ignore);
                    player.Reply(Lang(removed ? "IgnoreRemoved" : "NotOnIgnorelist", player.Id, args[1]));
                    return;
            }
        }

        private void SendHelpText(IPlayer player)
        {
            player.Reply(Lang("HelpText", player.Id));
        }

        private string FindIgnore(string ignore)
        {
            if (string.IsNullOrEmpty(ignore)) return String.Empty;
            foreach (var playerData in IgnoreData)
            {
                if (playerData.Key.ToString().Equals(ignore) || playerData.Value.Name.Contains(ignore))
                    return playerData.Key;
            }
            return string.Empty;
        }

        private IPlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in covalence.Players.Connected)
            {
                if (activePlayer.Id == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.Name.Contains(nameOrIdOrIp))
                    return activePlayer;
                if (activePlayer.Address == nameOrIdOrIp)
                    return activePlayer;
            }
            return null;
        }

    }
}
