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
    [Info( "Fireworks Monitor", "hoppel & Mr. Blue", "1.0.1" )]
    [Description( "Send a message to discord with an image of the firework pattern set by the user" )]
    public class FireworksMonitor : RustPlugin
    {
        #region Configuration

        private const string WEBHOOK_INTRO = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
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
                Unsubscribe( nameof( OnFireworkDesignChanged ) );
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
                ["EmbedTitle"] = "Firework design changed or created!",
                ["EmbedBody"] = "**Player:**\n{playerName}\n{playerId}\n[Steam Profile](https://steamcommunity.com/profiles/{playerId})\n\n**Firework owner:**\n{ownerName}\n{ownerId}\n[Steam Profile](https://steamcommunity.com/profiles/{ownerId})\n\n**Server:\n**{serverName}"
            }, this );
        }

        private string FormatMessage( string key, BasePlayer player, BasePlayer fireworkOwner )
        {
            return lang.GetMessage( key, this )
                .Replace( "{playerName}", player.displayName )
                .Replace( "{playerId}", player.UserIDString )
                .Replace( "{ownerName}", fireworkOwner.displayName )
                .Replace( "{ownerId}", fireworkOwner.UserIDString )
                .Replace( "{serverName}", covalence.Server.Name );
        }

        #endregion

        #region Firework Logic

        private static readonly Texture2D DrawingTexture = new Texture2D( 240, 240 );

        private void OnFireworkDesignChanged( PatternFirework entity, ProtoBuf.PatternFirework.Design design, BasePlayer player )
        {
            var stars = design?.stars;
            if( stars == null || stars.Count == 0 )
            {
                return;
            }

            DrawingTexture.Clear( Color.black );
            foreach( var star in stars )
            {
                var color = new Color( star.color.r, star.color.g, star.color.b );
                var scaledX = (int) ( ( star.position.x - -1 ) * 100 ) + 20;
                var scaledY = (int) ( ( star.position.y - -1 ) * 100 ) + 20;

                // Draw the points a bit bigger
                var xCondition = scaledX + 6;
                var yCondition = scaledY + 6;
                for( var x = scaledX - 3; x < xCondition; x++ )
                {
                    for( var y = scaledY - 3; y < yCondition; y++ )
                    {
                        DrawingTexture.SetPixel( x, y, color );
                    }
                }
            }

            var encodedPng = DrawingTexture.EncodeToPNG();

            SendDiscordEmbed( encodedPng, player, entity.OwnerID );
        }

        private void SendDiscordEmbed( byte[] image, BasePlayer player, ulong fireworkOwnerId )
        {
            var fireworkOwner = BasePlayer.FindByID( fireworkOwnerId );
            var title = FormatMessage( "EmbedTitle", player, fireworkOwner );
            var description = FormatMessage( "EmbedBody", player, fireworkOwner );

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = _embedColor,
                        image = new
                        {
                            url = "attachment://image.png"
                        },
                        timestamp = DateTime.Now
                    }
                }
            };

            var form = new WWWForm();
            form.AddBinaryData( "file", image, "image.png" );
            form.AddField( "payload_json", JsonConvert.SerializeObject( payload ) );

            ServerMgr.Instance.StartCoroutine( HandleUpload( _config.DiscordWebhook, form ) );
        }

        private IEnumerator HandleUpload( string url, WWWForm data )
        {
            var www = UnityWebRequest.Post( url, data );
            yield return www.SendWebRequest();

            if( www.isNetworkError || www.isHttpError )
            {
                Puts( $"Failed to post firework image to discord: {www.error}" );
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