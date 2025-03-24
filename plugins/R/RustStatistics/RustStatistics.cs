using System;
using CompanionServer.Handlers;
using Oxide.Core.Libraries;
using ConVar;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Game.Rust.Libraries;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Rust Statistics", "Pwenill", "1.0.0")]
    [Description("Get player statistics for sent to our website, and warn the server of potential threat")]
    class RustStatistics : RustPlugin
    {
        #region Variables
        private PluginConfig config;
        private Timer timerSendStats;

        private Dictionary<string, PlayerStats> playersData = new Dictionary<string, PlayerStats>();
        private ServerStats serversStats = new ServerStats();

        // API KEY VERSION ACCES TO API
        public string rs_token = "3x77pEqNj648XhDdMr8p9AG9p33WH4k7M6yNSUsx4Ccb8Y";
        public string rs_url = "https://plugins.ruststatistics.com";
        #endregion

        #region Class
        private class ResponseRequest
        {
            public string data;
            public string type;
        }
        private class ServerStats
        {
            public ServerOptional info = new ServerOptional();
            public Dictionary<DateTime, int> players = new Dictionary<DateTime, int>();
            public Dictionary<string, string> admins = new Dictionary<string, string>();
        }
        private class ServerOptional
        {
            public string hostname;
            public string description;
            public int queryport;
            public int port;

            public ServerOptional()
            {
                hostname = ConVar.Server.hostname;
                description = ConVar.Server.description;
                queryport = ConVar.Server.queryport;
                port = ConVar.Server.port;
            }
        }
        private class PlayerStats
        {
            public string SteamID;
            public string Username;
            public Dictionary<string, Dictionary<string, int>> Npc_hit;
            public Dictionary<string, Dictionary<string, int>> Hits;
            public Dictionary<DateTime, StatsKD> KD;
            public Dictionary<DateTime, int> Messages;
            public Dictionary<DateTime, int> Connections;
            public List<Constructions> ContructionsDestroyed;

            public PlayerStats(string steamID, string username)
            {
                SteamID = steamID;
                Username = username;
                KD = new Dictionary<DateTime, StatsKD>();
                Npc_hit = new Dictionary<string, Dictionary<string, int>>();
                Hits = new Dictionary<string, Dictionary<string, int>>();
                Messages = new Dictionary<DateTime, int>();
                Connections = new Dictionary<DateTime, int>();
                ContructionsDestroyed = new List<Constructions>();
            }
        }
        private class Constructions
        {
            public string Name;
            public string Grade;
            public Dictionary<string, int> Weapons;

            public Constructions(string name, string grade, Dictionary<string, int> weapons)
            {
                Name = name;
                Grade = grade;
                Weapons = weapons;
            }
        }
        private class StatsKD
        {
            public int Kills;
            public int Deaths;

            public StatsKD(int kills = 0, int deaths = 0)
            {
                Kills = kills;
                Deaths = deaths;
            }
        }
        #endregion

        #region Config
        private class PluginConfig
        {
            public string ClientID;
            public string ClientSecret;
            public string Token;
            public bool Linked;
            public float Interval;
            public bool DebugConsole;
            public BansLevelsConfig BansLevel;
        }
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Token = "false",
                ClientID = "",
                ClientSecret = "",
                Linked = false,
                DebugConsole = false,
                BansLevel = new BansLevelsConfig(),
            };
        }
        private class BansLevelsConfig
        {
            public int Critical;
            public int Moderate;
            public int Minor;

            public BansLevelsConfig()
            {
                Critical = 3;
                Moderate = 5;
                Minor = -1;
            }
        }
        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Hooks

        #region Server
        void OnServerSave()
        {
            if (timerSendStats != null)
                if (timerSendStats.Destroyed)
                    TimerStatistics();
        }
        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
        }
        void Loaded()
        {
            if (config.Token != "false")
            {
                if (config.ClientID == "" || config.ClientSecret == "")
                {
                    PrintWarning(string.Format(lang.GetMessage("RequiredLinked", this), $"https://ruststatistics/add?token={config.Token}"));
                    return;
                }
                else
                {
                    if (config.Linked)
                    {
                        PrintWarning(lang.GetMessage("PluginLinked", this));
                        TimerStatistics();
                        return;
                    }
                }
            }

            Dictionary<string, string> headers = new Dictionary<string, string>();
            Dictionary<string, string> query = new Dictionary<string, string>();

            // HEADERS
            headers.Add("rs_token", rs_token);
            headers.Add("client_id", config.ClientID);
            headers.Add("client_secret", config.ClientSecret);

            Dictionary<string, string> playersBans = new Dictionary<string, string>();
            foreach (var item in Admin.Bans())
                playersBans.Add(item.steamid.ToString(), item.notes);

            string hostname = "Rust Server";
            if (ConVar.Admin.ServerInfo().Hostname != null)
                hostname = ConVar.Admin.ServerInfo().Hostname;

            // BODY
            query.Add("token", config.Token);
            query.Add("hostname", hostname);
            query.Add("port", ConVar.Server.port.ToString());
            query.Add("queryport", ConVar.Server.queryport.ToString());
            query.Add("description", ConVar.Server.description);
            query.Add("banned", JsonConvert.SerializeObject(playersBans));

            webrequest.Enqueue($"{rs_url}/server/create", QueryString(query), (code, response) =>
            {
                if (code == 0)
                    return;

                ResponseRequest obj = JsonConvert.DeserializeObject<ResponseRequest>(response);
                if (obj.data == null)
                    return;

                if (code != 200)
                {
                    if (obj.type != null && obj.type == "linked")
                    {
                        config.Linked = true;
                        SaveConfig();
                    }

                    DebugConsole(obj.data, "error");
                    return;
                }

                if (obj.type != null)
                {
                    if (obj.type == "token")
                    {
                        config.Token = obj.data;
                        SaveConfig();

                        PrintWarning(string.Format(lang.GetMessage("SuccessToken", this), $"https://ruststatistics.com/add?token={config.Token}"));
                    }
                    if (obj.type == "linked")
                    {
                        config.Linked = true;
                        SaveConfig();

                        PrintWarning(lang.GetMessage("PluginLinked", this));
                        TimerStatistics();
                    }
                }
            }, this, RequestMethod.POST, headers);
        }
        #endregion

        #region Statistics
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var building = entity as BuildingBlock;
            if (building == null)
                return;

            BasePlayer player = info.InitiatorPlayer;
            if (player == null)
                return;

            PlayerStats playerStats = addOrGetPlayer(player.UserIDString, player.displayName);
            if (playerStats == null)
                return;

            if (info.WeaponPrefab == null)
                return;

            if (!(info.WeaponPrefab is TimedExplosive))
                return;

            Dictionary<string, int> weapons;
            string weaponName = info.WeaponPrefab.ShortPrefabName;
            string grade = building.grade.ToString();

            var construction = playerStats.ContructionsDestroyed.Where(x => x.Name == entity.ShortPrefabName && x.Grade == grade).FirstOrDefault();
            if (construction != null)
            {
                int count;
                if (construction.Weapons.TryGetValue(weaponName, out count))
                {
                    count++;
                    return;
                }
                construction.Weapons.Add(weaponName, 1);
                return;
            }

            Dictionary<string, int> weapons2 = new Dictionary<string, int>();
            weapons2.Add(weaponName, 1);
            playerStats.ContructionsDestroyed.Add(new Constructions(entity.ShortPrefabName, grade, weapons2));
        }
        void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            getAuthToken(token =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Authorization", token);
                headers.Add("rs_token", rs_token);

                Dictionary<string, string> query = new Dictionary<string, string>();
                query.Add("steamid", id.ToString());
                query.Add("ip", address);
                query.Add("reason", reason);
                query.Add("port", ConVar.Server.port.ToString());

                webrequest.Enqueue($"{rs_url}/asb/ban", QueryString(query), (code, response) =>
                {
                    if (code == 0)
                        return;

                    ResponseRequest data = JsonConvert.DeserializeObject<ResponseRequest>(response);
                    if (data.data == null)
                        return;

                    if (code != 200)
                    {
                        DebugConsole(data.data, "error");
                        return;
                    }
                }, this, RequestMethod.POST, headers);
            });
        }
        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!player.IsConnected)
                return null;

            if (player.IsNpc)
                return null;

            BasePlayer killed = info.InitiatorPlayer;
            if (killed == null)
                return null;

            if (killed.UserIDString == player.UserIDString)
                return null;

            if (killed.IsNpc)
                return null;

            // Get player data is death
            PlayerStats playerDeath = addOrGetPlayer(player.UserIDString, player.displayName);
            if (playerDeath != null)
            {
                if (playerDeath.KD.ContainsKey(DateTime.Today))
                    playerDeath.KD[DateTime.Today].Deaths++;
                else
                    playerDeath.KD.Add(DateTime.Today, new StatsKD(deaths: 1));
            }

            PlayerStats playerKilled = addOrGetPlayer(killed.UserIDString, killed.displayName);
            if (playerKilled != null)
            {
                if (playerKilled.KD.ContainsKey(DateTime.Today))
                    playerKilled.KD[DateTime.Today].Kills++;
                else
                    playerKilled.KD.Add(DateTime.Today, new StatsKD(kills: 1));
            }
            return null;
        }
        object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null)
                return null;

            if (info.HitEntity == null)
                return null;

            if (info.HitEntity is NPCPlayer || info.HitEntity is BaseAnimalNPC)
            {
                IncrementNPCHits(attacker, info);
                return null;
            }

            BasePlayer victim = info.HitEntity.ToPlayer();
            if (victim == null)
                return null;

            IncrementHits(attacker, info);
            return null;
        }
        object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            PlayerStats pstats = addOrGetPlayer(player.UserIDString, player.displayName);
            if (pstats != null)
                IncrementCountTime(pstats.Messages);
            return null;
        }
        void OnPlayerConnected(BasePlayer player)
        {
            string id = player.UserIDString;
            string ipAddress = player.net.connection.ipaddress;
            string name = player.displayName;
            ExecuteAdminSystemBans(id, ipAddress, name);

            if (serversStats.players.ContainsKey(DateTime.Today))
                serversStats.players[DateTime.Today]++;
            else
                serversStats.players.Add(DateTime.Today, 1);

            PlayerStats pstats = addOrGetPlayer(id, name);
            if (pstats != null)
                IncrementCountTime(pstats.Connections);
        }
        #endregion

        #endregion

        #region Web
        void TimerStatistics()
        {
            if (timerSendStats != null)
                timerSendStats.Destroy();

            timerSendStats = timer.Once(config.Interval, () => SendStatistics());
        }
        void SendStatistics()
        {
            serversStats.admins = new Dictionary<string, string>();
            foreach (var player in BasePlayer.allPlayerList)
                if (player.IsAdmin)
                    if (!serversStats.admins.ContainsKey(player.UserIDString))
                        serversStats.admins.Add(player.UserIDString, player.displayName);

            getAuthToken(token =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("Authorization", token);
                headers.Add("rs_token", rs_token);

                Dictionary<string, string> query = new Dictionary<string, string>();
                query.Add("players", JsonConvert.SerializeObject(playersData.Values));
                query.Add("server", JsonConvert.SerializeObject(serversStats));

                DebugConsole("Waiting for send statistics");
                webrequest.Enqueue($"{rs_url}/statistics/send", QueryString(query), (code, response) =>
                {
                    if (code == 0)
                        return;

                    ResponseRequest data = JsonConvert.DeserializeObject<ResponseRequest>(response);
                    if (data.data == null)
                        return;

                    if (code != 200)
                    {
                        if (data.type != null)
                        {
                            if (data.type == "date")
                            {
                                float newTimer = float.Parse(data.data);

                                config.Interval = newTimer;
                                SaveConfig();

                                DebugConsole("The Time is incorect !");
                            }
                        }
                        else
                        {
                            DebugConsole(data.data, "error");
                        }
                    }

                    if (data.data == "true")
                    {
                        DebugConsole("Statistics was send succesfully");

                        config.Interval = 1800;
                        SaveConfig();

                        playersData = new Dictionary<string, PlayerStats>();
                        serversStats = new ServerStats();
                    }

                    TimerStatistics();
                }, this, RequestMethod.POST, headers);
            });
        }
        void getAuthToken(Action<string> callback)
        {
            if (config.ClientID == "" || config.ClientSecret == "")
                return;

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("rs_token", rs_token);
            headers.Add("client_id", config.ClientID);
            headers.Add("client_secret", config.ClientSecret);

            webrequest.Enqueue($"{rs_url}/token", null, (code, response) =>
            {
                if (code == 0)
                    return;

                ResponseRequest data = JsonConvert.DeserializeObject<ResponseRequest>(response);
                if (data.data == null)
                    return;

                if (code != 200)
                {
                    DebugConsole(data.data, "error");
                    return;
                }

                callback(data.data);
            }, this, RequestMethod.POST, headers);
        }
        #endregion

        #region Functions
        void ExecuteAdminSystemBans(string id, string ipAddress, string name)
        {
            getAuthToken(token =>
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                Dictionary<string, string> query = new Dictionary<string, string>();

                headers.Add("Authorization", token);
                headers.Add("rs_token", rs_token);

                query.Add("steamid", id);
                query.Add("ip", ipAddress);
                query.Add("port", ConVar.Server.port.ToString());

                webrequest.Enqueue($"{rs_url}/asb/", QueryString(query), (code, response) =>
                {
                    if (code == 0)
                        return;

                    ResponseRequest data = JsonConvert.DeserializeObject<ResponseRequest>(response);
                    if (data.data == null)
                        return;

                    if (code != 200)
                    {
                        DebugConsole(data.data, "error");
                        return;
                    }

                    banPlayerWithLevel(data.data, id);

                    DebugConsole(data.data);
                    PrintToChat(string.Format(lang.GetMessage("ReportedMessage", this), name, data.data.Replace(',', '\n')));
                }, this, RequestMethod.POST, headers);
            });
        }
        object banPlayerWithLevel(string data, string steamid)
        {
            Dictionary<string, string> list = new Dictionary<string, string>();

            foreach (var item in data.Split(','))
            {
                string[] spliting = item.Split(' ');
                string level = spliting[1];
                string count = spliting[0];

                list.Add(level, count);
            }

            foreach (var level in list)
            {
                string reason = string.Format(lang.GetMessage("AutoBanLevel", this), level.Value, level.Key) + "B8Q9AZ";
                int count;
                if (int.TryParse(level.Value, out count))
                {
                    switch (level.Key)
                    {
                        case "critical":
                            if (config.BansLevel.Critical <= count)
                            {
                                Player.Ban(steamid, reason);
                                return reason;
                            }
                            break;
                        case "moderate":
                            if (config.BansLevel.Moderate <= count)
                            {
                                Player.Ban(steamid, reason);
                                return reason;
                            }
                            break;
                        case "minor":
                            if (config.BansLevel.Minor <= count)
                            {
                                Player.Ban(steamid, reason);
                                return reason;
                            }
                            break;
                    }
                }
            }
            return null;
        }
        void DebugConsole(string message, string type = "warning")
        {
            if (!config.DebugConsole)
                return;

            if (type == "warning")
                PrintWarning(message);

            if (type == "error")
                PrintError(message);
        }
        object IncrementNPCHits(BasePlayer player, HitInfo info)
        {
            Dictionary<string, int> keyValuePairs = new Dictionary<string, int>();
            Dictionary<string, int> statsWeapons;

            PlayerStats pstats = addOrGetPlayer(player.UserIDString, player.displayName);
            if (pstats == null)
                return null;

            if (info.HitEntity == null)
                return null;

            // Check is dead
            if (info.HitEntity.ShortPrefabName.Contains("corpse"))
                return null;

            if (pstats.Npc_hit.TryGetValue(info.HitEntity.ShortPrefabName, out statsWeapons))
            {
                if (statsWeapons.ContainsKey(info.Weapon.GetItem().info.shortname))
                    return statsWeapons[info.Weapon.GetItem().info.shortname]++;
                statsWeapons.Add(info.Weapon.GetItem().info.shortname, 1);

                return null;
            }

            keyValuePairs.Add(info.Weapon.GetItem().info.shortname, 1);
            pstats.Npc_hit.Add(info.HitEntity.ShortPrefabName, keyValuePairs);

            return null;
        }
        object IncrementHits(BasePlayer player, HitInfo info)
        {
            Dictionary<string, int> keyValuePairs = new Dictionary<string, int>();
            Dictionary<string, int> statsWeapons;

            PlayerStats stats = addOrGetPlayer(player.UserIDString, player.displayName);
            if (stats == null)
                return null;

            if (stats.Hits.TryGetValue(info.boneName, out statsWeapons))
            {
                if (statsWeapons.ContainsKey(info.Weapon.GetItem().info.shortname))
                    return statsWeapons[info.Weapon.GetItem().info.shortname]++;
                statsWeapons.Add(info.Weapon.GetItem().info.shortname, 1);

                return null;
            }

            keyValuePairs.Add(info.Weapon.GetItem().info.shortname, 1);
            stats.Hits.Add(info.boneName, keyValuePairs);
            return null;
        }
        object IncrementCountTime(Dictionary<DateTime, int> dictionary)
        {
            if (dictionary.ContainsKey(DateTime.Today))
                return dictionary[DateTime.Today]++;
            dictionary.Add(DateTime.Today, 1);
            return null;
        }
        PlayerStats addOrGetPlayer(string steamid, string username)
        {
            PlayerStats playerStats;
            if (!playersData.TryGetValue(steamid, out playerStats))
            {
                playerStats = new PlayerStats(steamid, username);
                playersData.Add(steamid, playerStats);
            }

            return playerStats;
        }
        public static string QueryString(Dictionary<string, string> dict)
        {
            var list = new List<string>();
            foreach (var item in dict)
                list.Add(item.Key + "=" + item.Value);
            return string.Join("&", list);
        }

        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PluginLinked"] = "The plugin is well connected to ruststatistics.com",
                ["RequiredLinked"] = "You must connect your server to our website via this url {0}",
                ["SuccessToken"] = "A new token was saved, please link it via the url {0}",
                ["AutoBanLevel"] = "You were automatically banned because you have {0} \"{1}\" warnings",
                ["ReportedMessage"] = "\"{0}\" users has been reported by AdminSystemBans\n{1}",
            }, this);
        }
        #endregion
    }
}
