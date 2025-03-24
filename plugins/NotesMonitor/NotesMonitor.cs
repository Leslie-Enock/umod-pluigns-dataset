// ReSharper disable CheckNamespace

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info( "Notes Monitor", "Mr. Blue", "1.0.3" )]
    [Description( "Send a message to discord with the content of a note set by the user" )]
    public class NotesMonitor : RustPlugin
    {
        #region Configuration

        private const string WEBHOOK_INTRO =
            "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private static int? _embedColor;

        private class PluginConfiguration
        {
            [JsonProperty( "Discord Webhook" )] public string DiscordWebhook;
            [JsonProperty( "Embed Color" )] public string EmbedColor;
        }

        private PluginConfiguration _config;

        private void Init()
        {
            _config = Config.ReadObject<PluginConfiguration>();
            _embedColor = FromHex( _config.EmbedColor );

            if( _config.DiscordWebhook == WEBHOOK_INTRO )
            {
                PrintWarning( $"Please set the discord webhook in the configuration file! ({WEBHOOK_INTRO})" );
                Unsubscribe( nameof( OnServerCommand ) );
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject( GetDefaultConfig(), true );
        }

        private PluginConfiguration GetDefaultConfig()
        {
            return new PluginConfiguration
            {
                DiscordWebhook = WEBHOOK_INTRO,
                EmbedColor = "#54a8fc"
            };
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["EmbedTitle"] = "Note changed!",
                ["EmbedBody"] =
                    "**Player:**\n{playerName}\n{playerId}\n[Steam Profile](https://steamcommunity.com/profiles/{playerId})\n\n" +
                    "**Note content:**\nOld:```\n{oldText}```\nNew:```\n{newText}```\n" +
                    "**Server:\n**{serverName}"
            }, this );
        }

        private string FormatMessage( string key, BasePlayer player, string prevText, string newText )
        {
            return lang.GetMessage( key, this )
                .Replace( "{playerName}", player.displayName )
                .Replace( "{playerId}", player.UserIDString )
                .Replace( "{oldText}", prevText )
                .Replace( "{newText}", newText )
                .Replace( "{serverName}", covalence.Server.Name );
        }

        #endregion

        #region Signage Logic

        private const string NOTE_UPDATE_COMMAND = "note.update";

        private void OnServerCommand( ConsoleSystem.Arg arg )
        {
            if( arg.cmd.FullName != NOTE_UPDATE_COMMAND )
            {
                return;
            }

            if( !arg.HasArgs( 2 ) )
            {
                return;
            }

            var player = arg.Player();
            if( player == null )
            {
                return;
            }

            var id = arg.GetULong( 0 );
            var item = player.inventory.FindItemByUID( new ItemId( id ) );
            if( item == null )
            {
                return;
            }

            var str = arg.GetString( 1 );
            SendDiscordEmbed( player, item?.text ?? string.Empty, str.Truncate( 1024 ) );
        }

        private void SendDiscordEmbed( BasePlayer player, string prevText, string newText )
        {
            prevText = prevText.Replace( "`", "'" );
            newText = newText.Replace( "`", "'" );

            prevText = string.IsNullOrWhiteSpace( prevText ) ? " " : prevText;
            newText = string.IsNullOrWhiteSpace( newText ) ? " " : newText;

            var title = FormatMessage( "EmbedTitle", player, prevText, newText );
            var description = FormatMessage( "EmbedBody", player, prevText, newText );

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = _embedColor,
                        timestamp = DateTime.Now,
                    }
                }
            };

            var form = new WWWForm();
            form.AddField( "payload_json", JsonConvert.SerializeObject( payload ) );

            ServerMgr.Instance.StartCoroutine( HandleUpload( _config.DiscordWebhook, form ) );
        }

        private IEnumerator HandleUpload( string url, WWWForm data )
        {
            var www = UnityWebRequest.Post( url, data );
            yield return www.SendWebRequest();

            if( www.isNetworkError || www.isHttpError )
            {
                Puts( $"Failed to post sign image to discord: {www.error}" );
            }
        }

        #endregion

        #region Helpers

        private static int? FromHex( string value )
        {
            var match = Regex.Match( value, "#?([0-9a-f]{6})" );
            if( !match.Success )
            {
                return null;
            }

            return int.Parse( match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber );
        }

        #endregion
    }
}