using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Finder", "MON@H", "3.1.1")]
    [Description("Find Players, SleepingBags, Doors, Sleepers, and teleport to them")]
    public class Finder : CovalencePlugin
    {
        #region Initialization
        [PluginReference] private Plugin PlayerDatabase;

        private Dictionary<ulong, PlayerFinder> cachedFinder = new Dictionary<ulong, PlayerFinder>();

        private const string PERMISSION_FIND = "finder.find";
        private const string PERMISSION_TP = "finder.tp";

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_FIND, this);
            permission.RegisterPermission(PERMISSION_TP, this);
            foreach (var command in configData.chatS.commands)
                AddCovalenceCommand(command, nameof(CmdFind));
        }

        private void OnServerInitialized()
        {
            UpdateConfig();
        }

        private void UpdateConfig()
        {
            if (configData.chatS.commands.Length == 0)
                configData.chatS.commands = new[] { "find" };
            SaveConfig();
        }

        #endregion Initialization

        #region Configuration

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings globalS = new GlobalSettings();

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatSettings chatS = new ChatSettings();

            public class GlobalSettings
            {
                [JsonProperty(PropertyName = "Use permissions")]
                public bool usePermission = true;

                [JsonProperty(PropertyName = "Allow admins to use without permission")]
                public bool adminsAllowed = true;
            }

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Chat command")]
                public string[] commands = new[] { "find" };
                [JsonProperty(PropertyName = "Chat prefix")]
                public string prefix = "<color=#00FFFF>[Finder]</color>: ";

                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong steamIDIcon = 0;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion Configuration

        #region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You do not have permission to use this command",
                ["MultiplePlayers"] = "Multiple players found",
                ["NoResults"] = "You didn't find anything yet.",
                ["OutOfRange"] = "This ID is out of range.",
                ["NoPlayers"] = "No matching players found.",
                ["NoPosition"] = "This player doesn't have a position.",
                ["NoBuildings"] = "This player hasn't built anything yet.",
                ["NoBags"] = "This player doesn't have any sleeping bags.",
                ["NoCupboardPrivileges"] = "This player doesn't have any cupboard privileges.",
                ["SelectTargetPlayer"] = "You need to select a target player.",
                ["SelectTargetFindID"] = "You need to select a target findid.",
                ["CantTP"] = "You are using the console, you can't tp!",
                ["ItemUsage"] = "Usage: <color=#FFFF00>/{0} item <ITEMNAME> <MINAMOUNT> optional:<STEAMID></color>.",
                ["InvalidItemName"] = "You didn't use a valid item name.",
                ["SyntaxError"] = "Syntax error occured!\n" +
                "type <color=#FFFF00>/{0} <h | help></color> to view help",
                ["FindSyntax"] = "Command usages:\n" +
                "<color=#FFFF00>/{0} <p | player> <PLAYERNAME | STEAMID></color> - Find player with ID / all players with partial names.\n" +
                "<color=#FFFF00>/{0} <c | cupboard> <PLAYERNAME | STEAMID></color> - Find all cupboards owned by a specific player.</color>\n" +
                "<color=#FFFF00>/{0} bag <PLAYERNAME | STEAMID></color> - Find all sleeping bags owned by a specific player.\n" +
                "<color=#FFFF00>/{0} <b | building> <PLAYERNAME | STEAMID></color> - Find all buildings owned by a specific player.\n" +
                "<color=#FFFF00>/{0} <i | item> <ITEMNAME> <MINAMOUNT></color> - Find all items owned by a specific player.\n" +
                "<color=#FFFF00>/{0} tp FINDID</color> - Teleport to any of your results.",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "У вас нет разрешения на использование этой команды",
                ["MultiplePlayers"] = "Найдено несколько игроков",
                ["NoResults"] = "Вы ещё ничего не нашли.",
                ["OutOfRange"] = "Этот ID вне допустимого диапазона.",
                ["NoPlayers"] = "Подходящих игроков не найдено.",
                ["NoPosition"] = "У этого игрока нет позиции.",
                ["NoBuildings"] = "Этот игрок еще ничего не построил.",
                ["NoBags"] = "У этого игрока нет спальных мешков.",
                ["NoCupboardPrivileges"] = "Этот игрок не авторизирован ни в одном шкафу.",
                ["SelectTargetPlayer"] = "Вам нужно выбрать целевого игрока.",
                ["SelectTargetFindID"] = "Вам нужно выбрать целевой findID.",
                ["CantTP"] = "Вы используете консоль, вы не можете tp!",
                ["ItemUsage"] = "Использование: <color=#FFFF00>/{0} item <ITEMNAME> <MINAMOUNT> optional:<STEAMID></color>.",
                ["InvalidItemName"] = "Вы использовали неверное название предмета.",
                ["SyntaxError"] = "Синтаксическая ошибка!\n" +
                "напишите <color=#FFFF00>/{0} <h | help></color> чтобы отобразить подсказки",
                ["FindSyntax"] = "Использование команд:\n" +
                "<color=#FFFF00>/{0} <p | player> <PLAYERNAME | STEAMID></color> - Найти игрока по ID / всех игроков с частичным совпадением имён\n" +
                "<color=#FFFF00>/{0} <c | cupboard> <PLAYERNAME | STEAMID></color> - Найти все шкафы, принадлежащие определенному игроку\n" +
                "<color=#FFFF00>/{0} bag <PLAYERNAME | STEAMID></color> - Найти все спальные мешки, принадлежащие определенному игроку\n" +
                "<color=#FFFF00>/{0} <b | building> <PLAYERNAME | STEAMID></color> - Найти все здания, принадлежащие определенному игроку\n" +
                "<color=#FFFF00>/{0} <i | item> <ITEMNAME> <MINAMOUNT></color> - Найти все предметы, принадлежащие определенному игроку\n" +
                "<color=#FFFF00>/{0} tp FINDID</color> - Телепортация к любому из ваших результатов",
            }, this, "ru");
        }

        #endregion Localization

        #region Commands

        private void CmdFind(IPlayer iplayer, string command, string[] args)
        {
            var player = (iplayer.Object as BasePlayer);
            string returnstring = string.Empty;

            if(!hasPermission(iplayer, "find"))
            {
                Print(iplayer, Lang("NotAllowed", player.UserIDString));
                return;
            }

            if (args == null || args.Length == 0)
            {
                Print(iplayer, Lang("SyntaxError", player.UserIDString, configData.chatS.commands[0]));
                return;
            }

            var puserid = player == null ? 0L : player.userID;
            switch(args[0].ToLower())
            {
                case "h":
                case "help":
                    Print(iplayer, Lang("FindSyntax", player.UserIDString, configData.chatS.commands[0]));
                    return;
                case "b":
                case "bag":
                case "building":
                case "c":
                case "cupboard":
                case "p":
                case "player":
                    if(args.Length == 1)
                    {
                        Print(iplayer, Lang("SelectTargetPlayer", player.UserIDString));
                        return;
                    }
                    var f = FindPlayerID(args[1], player);
                    if(!(f is ulong))
                    {
                        Print(iplayer, f.ToString());
                        return;
                    }
                    ulong targetID = (ulong)f;
                    var d = GetPlayerInfo(targetID);
                    returnstring = d.ToString() + ":\n";
                    switch (args[0].ToLower())
                    {
                        case "p":                        
                        case "player":
                            var p = FindPosition(targetID);
                            if(p == null)
                            {
                                returnstring += Lang("NoPosition", player.UserIDString);
                            }
                            else 
                                d.AddFind("Position", (Vector3)p, string.Empty);
                            break;                            
                        case "bag":
                            var bs = SleepingBag.FindForPlayer(targetID, true).ToList();
                            if (bs.Count == 0)
                            {
                                returnstring += Lang("NoBags", player.UserIDString);
                            }
                            foreach(var b in bs)
                            {
                                d.AddFind(b.ShortPrefabName, b.transform.position, b.niceName);
                            }
                            break;
                        case "c":
                        case "cupboard":
                            var cs = Resources.FindObjectsOfTypeAll<BuildingPrivlidge>().Where(x => x.authorizedPlayers.Any((ProtoBuf.PlayerNameID z) => z.userid == targetID)).ToList();
                            if(cs.Count== 0)
                            {
                                returnstring += Lang("NoCupboardPrivileges", player.UserIDString);
                            }
                            foreach(var c in cs)
                            {
                                d.AddFind("Tool Cupboard", c.transform.position, string.Empty);
                            }
                            break;
                        case "b":
                        case "building":
                            var bb = Resources.FindObjectsOfTypeAll<BuildingBlock>().Where(x => x.OwnerID == targetID).ToList();
                            if (bb.Count == 0)
                            {
                                returnstring += Lang("NoBuildings", player.UserIDString);
                            }
                            var dic = new Dictionary<uint, Dictionary<string, object>>();
                            foreach(var b in bb)
                            {
                                if(!dic.ContainsKey(b.buildingID))
                                {
                                Puts("b.transform.position = " + b.transform.position.ToString());
                                    dic.Add(b.buildingID, new Dictionary<string, object>
                                    {
                                        {"pos", b.transform.position },
                                        {"num", 0 }
                                    });
                                }
                                dic[b.buildingID]["num"] = (int)dic[b.buildingID]["num"] + 1;
                            }
                            foreach (var c in dic)
                            {
                                d.AddFind("Building", (Vector3)c.Value["pos"], c.Value["num"].ToString());
                            }
                            break;
                        default:
                            break;
                    }
                    for (int i = 0; i < d.Data.Count; i++)
                    {
                        returnstring += i.ToString() + " - " + d.Data[i].ToString() + "\n";
                    }
                    if (cachedFinder.ContainsKey(puserid))
                    {
                        cachedFinder[puserid].Data.Clear();
                        cachedFinder[puserid] = null;
                        cachedFinder.Remove(puserid);
                    }
                    cachedFinder.Add(puserid, d);
                    break;
                case "i":
                case "item":
                    if(args.Length < 3)
                    {
                        Print(iplayer, Lang("ItemUsage", player.UserIDString, configData.chatS.commands[0]));
                        return;
                    }
                    var pu = GetPlayerInfo(puserid);
                    var itemname = args[1].ToLower();
                    ulong ownerid = 0L;
                    if (args.Length > 3)
                        ulong.TryParse(args[3], out ownerid);
                    var itemamount = 0;
                    if(!(int.TryParse(args[2], out itemamount)))
                    {
                        Print(iplayer, Lang("ItemUsage", player.UserIDString, configData.chatS.commands[0]));
                        return;
                    }
                    ItemDefinition item = null;
                    for(int i = 0; i < ItemManager.itemList.Count; i++)
                    {
                        if(ItemManager.itemList[i].displayName.english.ToLower() == itemname.ToLower())
                        {
                            item = ItemManager.itemList[i];
                            break;
                        }
                    }
                    if(item == null)
                    {
                        Print(iplayer, Lang("InvalidItemName", player.UserIDString));
                        return;
                    }
                    foreach (StorageContainer sc in Resources.FindObjectsOfTypeAll<StorageContainer>())
                    {
                        ItemContainer inventory = sc.inventory;
                        if (inventory == null) continue;
                        List<Item> list = inventory.itemList.FindAll((Item x) => x.info.itemid == item.itemid);
                        int amount = 0;
                        if (amount < itemamount) continue;
                        pu.AddFind("Box", sc.transform.position, amount.ToString());
                    }
                    foreach (BasePlayer bp in Resources.FindObjectsOfTypeAll<BasePlayer>())
                    {
                        PlayerInventory inventory = player.inventory;
                        if (inventory == null) continue;
                        int amount = inventory.GetAmount(item.itemid);
                        if (amount < itemamount) continue;
                        Dictionary<string, object> scdata = new Dictionary<string, object>();
                        pu.AddFind(string.Format("{0} {1}", player.userID.ToString(), player.displayName), bp.transform.position, amount.ToString());
                    }
                    for (int i = 0; i < pu.Data.Count; i++)
                    {
                        returnstring += i.ToString() + " - " + pu.Data[i].ToString() + "\n";
                    }
                    if (cachedFinder.ContainsKey(puserid))
                    {
                        cachedFinder[puserid].Data.Clear();
                        cachedFinder[puserid] = null;
                        cachedFinder.Remove(puserid);
                    }
                    cachedFinder.Add(puserid, pu);
                    break;
                case "tp":
                    if(player == null)
                    {
                        Print(iplayer, Lang("CantTP", player.UserIDString));
                        return;
                    }
                    if(!hasPermission(iplayer, "TP"))
                    {
                        Print(iplayer, Lang("NotAllowed", player.UserIDString));
                        return;
                    }
                    if (!cachedFinder.ContainsKey(puserid))
                    {
                        Print(iplayer, Lang("NoResults", player.UserIDString));
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Print(iplayer, Lang("SelectTargetFindID", player.UserIDString));
                        return;
                    }
                    var fp = cachedFinder[puserid];
                    var id = 0;
                    int.TryParse(args[1], out id);
                    if(id >= fp.Data.Count)
                    {
                        Print(iplayer, Lang("OutOfRange", player.UserIDString));
                        return;
                    }

                    var data = cachedFinder[puserid].Data[id];
                    player.MovePosition(data.Pos);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", data.Pos);
                    returnstring += data.ToString();
                    break;
                default:
                    returnstring += Lang("FindSyntax", player.UserIDString, configData.chatS.commands[0]);
                    break;
            }
            Print(iplayer, returnstring);
            return;
        }

        #endregion Commands

        #region Helpers

        bool hasPermission(IPlayer player, string perm)
        {
            if (!configData.globalS.usePermission)
                return true;
            
            if (perm.ToLower() == "find" && permission.UserHasPermission(player.Id, PERMISSION_FIND))
                return true;
            
            if (perm.ToLower() == "tp" && permission.UserHasPermission(player.Id, PERMISSION_TP))
                return true;
            
            if (configData.globalS.adminsAllowed && player.IsAdmin)
                return true;

            return false;
        }

        class FindData
        {
            string Name;
            public Vector3 Pos;
            string TypeName;

            public FindData(string TypeName, Vector3 Pos, string Name)
            {
                this.Name = Name;
                this.TypeName = TypeName;
                this.Pos = Pos;
            }

            public override string ToString()
            {
                return string.Format("{0} - {1}{2}", TypeName, Pos.ToString(), Name == string.Empty ? string.Empty : (" - " + Name));
            }            
        }

        class PlayerFinder
        {
            string Name;
            string Id;
            bool Online;

            public List<FindData> Data = new List<FindData>();

            public PlayerFinder(string Name, string Id, bool Online)
            {
                this.Name = Name;
                this.Id = Id;
                this.Online = Online;
            }
            public void AddFind(string TypeName, Vector3 Pos, string Name)
            {
                Data.Add(new FindData(TypeName, Pos, Name));
            }

            public override string ToString()
            {
                return string.Format("{0} {1} - {2}", Id, Name, Online ? "Connected" : "Offline");
            }
        }

        PlayerFinder GetPlayerInfo(ulong userID)
        {
            var steamid = userID.ToString();
            var player = covalence.Players.FindPlayer(steamid);
            if(player != null)
            {
                return new PlayerFinder(player.Name, player.Id, player.IsConnected);
            }

            if(PlayerDatabase != null)
            {
                var name = (string)PlayerDatabase?.Call("GetPlayerData", steamid, "name");
                if(name != null)
                {
                    return new PlayerFinder(name, steamid, false);
                }
            }

            return new PlayerFinder("Unknown", steamid, false);
        }

        private object FindPosition(ulong userID)
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.userID == userID)
                {
                    return p.transform.position;
                }
            }
            foreach (var p in BasePlayer.sleepingPlayerList)
            {
                if (p.userID == userID)
                {
                    return p.transform.position;
                }
            }
            return null;
        }

        private object FindPlayerID(string arg, BasePlayer source = null)
        {
            ulong userID = 0L;
            if (arg.Length == 17 && ulong.TryParse(arg, out userID))
                return userID;

            var players = covalence.Players.FindPlayers(arg).ToList();
            if(players.Count > 1)
            {
                var returnstring = Lang("MultiplePlayers", source.UserIDString) + ":\n";
                foreach(var p in players)
                {
                    returnstring += string.Format("{0} - {1}\n", p.Id, p.Name);
                }
                return returnstring;
            }
            if(players.Count == 1)
            {
                return ulong.Parse(players[0].Id);
            }

            if (PlayerDatabase != null)
            {
                string success = PlayerDatabase.Call("FindPlayer", arg) as string;
                if (success.Length == 17 && ulong.TryParse(success, out userID))
                {
                    return userID;
                }
                else
                    return success;
            }

            return Lang("NoPlayers", source.UserIDString);
        }

        private void Print(IPlayer player, string message)
        {
            var text = string.IsNullOrEmpty(configData.chatS.prefix) ? string.Empty : $"{configData.chatS.prefix}{message}";
#if RUST
            (player.Object as BasePlayer).SendConsoleCommand ("chat.add", 2, configData.chatS.steamIDIcon, text);
            return;
#endif
            player.Message(text);
        }

        #endregion Helpers
    }
}
