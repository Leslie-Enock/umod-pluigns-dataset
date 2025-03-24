using System.Collections.Generic;
using System.ComponentModel;
using Facepunch;
using Network;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mute Explosions", "1AK1/MJSU", "1.0.3")]
    [Description("Mutes explosion sounds for everyone but the initiator")]
    internal class MuteExplosions : CovalencePlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig;
        
        private const string UsePermission = "muteexplosions.use"; //Players with this permission will have the sound effect changed
        private const string BypassPermission = "muteexplosions.bypass"; //Players with this permission can hear the sound effect normally

        private readonly Hash<uint, string> _explosionEffect = new Hash<uint, string>();
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            permission.RegisterPermission(BypassPermission, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }
        #endregion

        #region Oxide Hooks
        private void OnEntitySpawned(TimedExplosive explosive)
        {
            BasePlayer player = explosive.creatorEntity as BasePlayer;
            if (player != null && !HasPermission(explosive.creatorEntity as BasePlayer, UsePermission))
            {
                return;
            }
            
            _explosionEffect[explosive.net.ID] = explosive.explosionEffect.resourcePath;
            explosive.explosionEffect.guid = null;
        }

        private void OnEntityKill(TimedExplosive explosive)
        {
            string effectName = _explosionEffect[explosive.net.ID];
            if (string.IsNullOrEmpty(effectName))
            {
                return;
            }

            _explosionEffect.Remove(explosive.net.ID);

            List<Connection> connections = Pool.GetList<Connection>();

            if (_pluginConfig.SendToOwner)
            {
                BasePlayer creator = explosive.creatorEntity as BasePlayer;
                if (creator != null && creator.IsConnected)
                {
                    connections.Add(creator.Connection);
                }
            }

            Vector3 explosivePosition = explosive.PivotPoint();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (HasPermission(player, BypassPermission) || Vector3.Distance(player.transform.position, explosivePosition) < _pluginConfig.SendToRange)
                {
                    connections.Add(player.Connection);
                }
            }
            
            Effect effect = new Effect(effectName, explosivePosition, explosive.explosionUsesForward ? explosive.transform.forward : Vector3.up);
            SendEffect(effect, connections);
            Pool.FreeList(ref connections);
        }
        #endregion
        
        #region Effect Handling
        private void SendEffect(Effect effect, List<Connection> connections)
        {
            effect.pooledstringid = StringPool.Get(effect.pooledString);
            if (effect.pooledstringid == 0U)
            {
                Debug.LogWarning("EffectNetwork.Send - unpooled effect name: " + effect.pooledString);
            }
            else
            {
                Net.sv.write.Start();
                Net.sv.write.PacketID(Message.Type.Effect);
                effect.WriteToStream(Net.sv.write);
                Net.sv.write.Send(new SendInfo(connections));
            }
        }
        #endregion

        #region Helpers
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes

        public class PluginConfig
        {
            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Send to explosive owner")]
            public bool SendToOwner { get; set; }
            
            [DefaultValue(0)]
            [JsonProperty(PropertyName = "Send to in range (Meters)")]
            public float SendToRange { get; set; }
        }

        #endregion

    }

}