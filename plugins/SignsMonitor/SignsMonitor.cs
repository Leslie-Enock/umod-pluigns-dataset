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
    [Info( "Signs Monitor", "Mr. Blue", "1.0.1" )]
    [Description( "Send a message to discord with an image of the signage content set by the user" )]
    public class SignsMonitor : RustPlugin
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
                Unsubscribe( nameof( OnSignUpdated ) );
                Unsubscribe( nameof( OnItemPainted ) );
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
                ["EmbedTitle"] = "Signage changed!",
                ["EmbedBody"] =
                    "**Player:**\n{playerName}\n{playerId}\n[Steam Profile](https://steamcommunity.com/profiles/{playerId})\n\n" +
                    "**Signage owner:**\n{ownerName}\n{ownerId}\n[Steam Profile](https://steamcommunity.com/profiles/{ownerId})\n\n" +
                    "**Signage info:**\nPosition: {signagePosition}\nType: {signageType}\n\n" +
                    "**Server:\n**{serverName}"
            }, this );
        }

        private string FormatMessage( string key, BasePlayer player, string displayName, string steamId, BaseEntity entity )
        {
            return lang.GetMessage( key, this )
                .Replace( "{playerName}", player.displayName )
                .Replace( "{playerId}", player.UserIDString )
                .Replace( "{ownerName}", displayName )
                .Replace( "{ownerId}", steamId )
                .Replace( "{signagePosition}", entity.transform.position.ToString().Replace( " ", string.Empty ) )
                .Replace( "{signageType}", entity.ShortPrefabName )
                .Replace( "{serverName}", covalence.Server.Name );
        }

        #endregion

        #region Signage Logic

        private void OnSignUpdated( ISignage signage, BasePlayer player, int textureIndex = 0 )
        {
            uint crc = signage.GetTextureCRCs()[textureIndex];
            var encodedPng = FileStorage.server.Get( crc, FileStorage.Type.png, signage.NetworkID, (uint) textureIndex );
            if( encodedPng == null ) return;

            var entity = signage as BaseEntity;
            SendDiscordEmbed( encodedPng, player, entity.OwnerID, entity );
        }

        private void OnItemPainted( PaintedItemStorageEntity entity, Item item, BasePlayer player, byte[] encodedPng )
        {
            if( entity._currentImageCrc == 0 )
            {
                return;
            }

            SendDiscordEmbed( encodedPng, player, entity.OwnerID, entity );
        }

        private void SendDiscordEmbed( byte[] image, BasePlayer player, ulong signageOwnerId, BaseEntity entity )
        {
            var signageOwnerName = ConVar.Admin.GetPlayerName( signageOwnerId );
            var signageSteamId = signageOwnerId.ToString();
            var title = FormatMessage( "EmbedTitle", player, signageOwnerName, signageSteamId, entity );
            var description = FormatMessage( "EmbedBody", player, signageOwnerName, signageSteamId, entity );

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