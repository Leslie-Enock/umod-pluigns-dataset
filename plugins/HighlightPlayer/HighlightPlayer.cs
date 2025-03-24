using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Highlight Player", "Freakyy", "1.2.1")]
    [Description("Marks players on the map after using a command. So other players can find the target for pvp action.")]
    class HighlightPlayer : RustPlugin
    {
        #region variables

        public List<MapMarkerGenericRadius> Markers = new List<MapMarkerGenericRadius>();
        public List<BasePlayer> Players_To_Highlight = new List<BasePlayer>();

        //permissions stuff
        private string permission_can_highlight_other_players = "HighlightPlayer.CanHighlightOtherPlayers";
        private string permission_cant_be_highlighted_by_other_players = "HighlightPlayer.CantBeHighlightedByOtherPlayers";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotHighligted"] = "You are not highlighted.",
                ["NotHighligtedAnymore"] = "You are not highlighted anymore.",
                ["SuccessfullyHighlighted"] = "From now on for the next " + (Configuration.How_Long_Is_Player_Highlighted / 60) + " minutes, are you a highlighted target on the map. If you want to change this use the '/uhme' command.",
                ["NoPermissionToHighlightOtherPlayers"] = "You are not allowed to highlight other players.",
                ["SomethingWentWrongHighlightOtherPlayer"] = "Something went wrong please try again with '/h <playername>'.",
                ["HighlightedByOtherPlayer"] = "You just got highlighted by: ",
                ["OtherPlayerAlreadyHighlighted"] = " is already highlighted.",
                ["SuccessfullyHighlightedOtherPlayer"] = " is now highlighted.",
                ["UseOtherCommandToHighlightYourself"] = "Use /hme to highlight yourself.",
                ["CantHighlightThisPlayer"] = "You are not allowed to highlight this player.",
            }, this);
        }
        #endregion

        #region hooks
        void OnServerInitialized()
        {
            timer.Repeat(Configuration.Update_delay, 0, () =>
            {
                update_player_position_on_map();
            });
        }

        void Init() {
            permission.RegisterPermission(permission_can_highlight_other_players, this);
            permission.RegisterPermission(permission_cant_be_highlighted_by_other_players, this);
        }
        void OnPlayerConnected(BasePlayer player)
        {
            update_player_position_on_map();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            clearplayermarker(player);
        }

        void Unload()
        {
            clearallmarkers();

            Players_To_Highlight.Clear();
        }
        #endregion

        #region commands

        [ChatCommand("hme")] // highlight me
        void highlight(BasePlayer player)
        {
            highlight_player(player);
            SendReply(player, lang.GetMessage("SuccessfullyHighlighted", this, player.UserIDString));
        }

        [ChatCommand("uhme")] // unhighlight me
        void unhighlight(BasePlayer player)
        {
            unhighlight_player(player);
        }

        [ChatCommand("h")]
        private void highlightOtherPlayer(BasePlayer player, string command, string[] args)
        {
            if(!has_user_permission(player.IPlayer, permission_can_highlight_other_players))
            {
                SendReply(player, lang.GetMessage("NoPermissionToHighlightOtherPlayers", this, player.UserIDString));
                return;
            }

            if (args.Length != 1 || args[0] == null)     
            {
                SendReply(player, lang.GetMessage("SomethingWentWrongHighlightOtherPlayer", this, player.UserIDString));
                return;
            }

            if (args[0] is string)
            {
                IPlayer player_to_highlight = covalence.Players.FindPlayer(args[0]);
                BasePlayer player_to_highlight_base = player_to_highlight.Object as BasePlayer;

                if(player_to_highlight_base.userID == player.userID && !player.IsAdmin)
                {
                    SendReply(player, lang.GetMessage("UseOtherCommandToHighlightYourself", this, player.UserIDString));
                    return;
                }

                if (has_user_permission(player_to_highlight, permission_cant_be_highlighted_by_other_players))
                {
                    SendReply(player, lang.GetMessage("CantHighlightThisPlayer", this, player.UserIDString));
                    return;
                }

                if (Players_To_Highlight.Contains(player_to_highlight_base))
                {
                    SendReply(player, player_to_highlight_base.displayName + " " + lang.GetMessage("OtherPlayerAlreadyHighlighted", this, player.UserIDString));
                    return;
                }
                else
                {
                    highlight_player(player_to_highlight_base);
                    SendReply(player, player_to_highlight_base.displayName + " " + lang.GetMessage("SuccessfullyHighlightedOtherPlayer", this, player.UserIDString));
                    SendReply(player_to_highlight_base, lang.GetMessage("HighlightedByOtherPlayer", this, player.UserIDString) + " " + player.displayName);
                }
            }
            else
            {
                SendReply(player, lang.GetMessage("SomethingWentWrongHighlightOtherPlayer", this, player.UserIDString));
                return;
            }
        }
        #endregion

        #region functions
        private void unhighlight_player(BasePlayer player)
        {
            if (Players_To_Highlight.Contains(player))
            {
                clearplayermarker(player);
            }
            else
            {
                SendReply(player, lang.GetMessage("NotHighligted", this, player.UserIDString));
            }
        }
        private void highlight_player(BasePlayer player)
        {
            Players_To_Highlight.Add(player);
            update_player_position_on_map();
            timer.Once(Configuration.How_Long_Is_Player_Highlighted, () =>
            {
                clearplayermarker(player);
            });
        }
        private bool has_user_permission(IPlayer player, string permission_name)
        {
            return player.IsAdmin || player.HasPermission(permission_name);
        }
        void clearplayermarker(BasePlayer player)
        {
            if (Players_To_Highlight.Contains(player))
            {
                Players_To_Highlight.Remove(player);
            }

            foreach (var marker in Markers)
            {
                if (marker == null)
                {
                    continue;
                }

                if (marker.transform.position == player.transform.position)
                {
                    if(!marker.IsDestroyed) marker.Kill();
                    marker.SendUpdate();
                }
            }
            update_player_position_on_map();
            SendReply(player, lang.GetMessage("NotHighligtedAnymore", this, player.UserIDString));
        }
        void clearallmarkers()
        {
            foreach (var marker in Markers)
            {
                if (marker != null)
                {
                    if (!marker.IsDestroyed) marker.Kill();
                    marker.SendUpdate();
                }
            }
            Markers.Clear();
        }
        void update_player_position_on_map()
        {
            clearallmarkers();
            foreach (BasePlayer player in Players_To_Highlight)
            {
                if (player == null)
                    continue;

                MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", player.transform.position) as MapMarkerGenericRadius;

                if (mapMarker != null)
                {
                    mapMarker.alpha = 0.4f;
                    mapMarker.color1 = Color.black;
                    mapMarker.color2 = Color.red;
                    mapMarker.name = player.displayName;
                    mapMarker.radius = Configuration.Marker_Radius;
                    Markers.Add(mapMarker);
                    mapMarker.Spawn();
                    mapMarker.SendUpdate();
                }
            }
        }
        #endregion

        #region config
        private struct Configuration
        {
            public static float Update_delay = 10f; //seconds
            public static float Marker_Radius = 0.5f;
            public static float How_Long_Is_Player_Highlighted = 30f; //seconds
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.Update_delay, "Update Delay (seconds)", "10f");
            GetConfig(ref Configuration.Marker_Radius, "Marker Radius", "0.5f");
            GetConfig(ref Configuration.How_Long_Is_Player_Highlighted, "How Long Is Player Highlighted (seconds)", "30f");

            SaveConfig();
        }

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
            {
                return;
            }

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");
        #endregion
    }
}
