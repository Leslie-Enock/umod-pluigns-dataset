using Rust;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Crazy Copter", "Colon Blow", "1.0.2")]
    [Description("Monuments cause helicopter throttles to glitch at 100%")]

    public class CrazyCopter : CovalencePlugin
    {
        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public CrazyCopterSettings copterSettings { get; set; }

            public class CrazyCopterSettings
            {
                [JsonProperty(PropertyName = "Percent : Throttle will max out to this when around Monuments Triggers : ")] public int throttleEffect { get; set; }
                [JsonProperty(PropertyName = "Radius : Helicopter will detect Monument Triggers within this radius : ")] public float detectionRadius { get; set; }
                [JsonProperty(PropertyName = "Force : Add force against Helicopters when they are effected")] public bool forceEnabled { get; set; }
                [JsonProperty(PropertyName = "Force : Max force possible when enabled (randomizes direction and amount from zero to max)")] public float maxForceAmount { get; set; }
                [JsonProperty(PropertyName = "Minicopters are effected by glitch ? ")] public bool miniCopterEnabled { get; set; }
                [JsonProperty(PropertyName = "Scrap Helicopters are effected by glitch ? ")] public bool scrapHeliEnabled { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                copterSettings = new PluginConfig.CrazyCopterSettings
                {
                    throttleEffect = 100,
                    detectionRadius = 1f,
                    forceEnabled = true,
                    maxForceAmount = 2f,
                    miniCopterEnabled = true,
                    scrapHeliEnabled = true,
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["interference"] = "Interference from Monument in Helicopter Controls !!"
            }, this);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(BaseHelicopterVehicle baseHeli)
        {
            if (config.copterSettings.miniCopterEnabled && baseHeli.ToString().Contains("minicopter")) baseHeli.gameObject.AddComponent<ThrottleGlitchControl>();
            if (config.copterSettings.scrapHeliEnabled && baseHeli.ToString().Contains("scraptransporthelicopter")) baseHeli.gameObject.AddComponent<ThrottleGlitchControl>();
        }

        private void SendPlayerWarning(BasePlayer pilotPlayer)
        {
            var iplayer = covalence.Players.FindPlayerById(pilotPlayer.UserIDString);
            if (iplayer != null) iplayer.Message(lang.GetMessage("interference", this, iplayer.Id));
        }

        private void Unload()
        {
            DestroyAll<ThrottleGlitchControl>();
        }

        private static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }


        #endregion

        #region Throttle Glitch Control

        private class ThrottleGlitchControl : MonoBehaviour
        {
            private CrazyCopter instance;
            private BaseHelicopterVehicle baseHeli;
            private SphereCollider sphereCollider;
            private bool isEffected;
            private float throttleSpeed;
            private float counter;
            private BasePlayer pilotPlayer;

            private void Awake()
            {
                instance = new CrazyCopter();
                baseHeli = GetComponent<BaseHelicopterVehicle>();
                if (baseHeli == null) { OnDestroy(); return; }
                throttleSpeed = config.copterSettings.throttleEffect;
                sphereCollider = baseHeli.gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = config.copterSettings.detectionRadius;
                isEffected = false;
                counter = 0f;
            }

            private void OnTriggerEnter(Collider col)
            {
                if (col.name.ToLower().Contains("prevent_building"))
                {
                    isEffected = true;
                    SendWarning();
                }
            }

            private void OnTriggerExit(Collider col)
            {
                if (col.name.ToLower().Contains("prevent_building"))
                {
                    counter = 0;
                    isEffected = false;
                }
            }

            private void SendWarning()
            {
                for (int i = 0; i < baseHeli.children.Count; i++)
                {
                    if (baseHeli.children[i] is BaseMountable)
                    {
                        var isMount = (BaseMountable)baseHeli.children[i];
                        if (isMount)
                        {
                            BasePlayer pilotPlayer = isMount.GetMounted();
                            if (pilotPlayer != null) instance.SendPlayerWarning(pilotPlayer);
                        }
                    }
                }
            }

            private void FixedUpdate()
            {
                if (isEffected)
                {
                    baseHeli.currentThrottle = throttleSpeed;
                    if (config.copterSettings.forceEnabled && baseHeli.IsMounted())
                    {
                        if (counter >= 75f) { baseHeli.rigidBody.AddRelativeTorque(Random.insideUnitSphere * config.copterSettings.maxForceAmount, ForceMode.VelocityChange); SendWarning(); counter = 0f; }
                        counter++;
                    }
                }
            }

            private void OnDestroy()
            {
                if (sphereCollider != null) GameObject.Destroy(sphereCollider);
                GameObject.Destroy(this);
            }

        }

        #endregion
    }
}