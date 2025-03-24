using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Prop Control", "Dana", "0.1.3")]
    [Description("Become an animal")]
    public class PropControl : RustPlugin
    {
        #region References

        [PluginReference] private Plugin Vanish, BetterVanish;

        private const string permallow = "propcontrol.allow";

        private List<Props> data = new List<Props>();
        private PluginConfig _pluginConfig;

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (_pluginConfig.PropControlConfig.InvulnerableProps && entity?.gameObject?.GetComponent<NpcAI>() != null)
            {
                info?.damageTypes.Clear();
                return true;
            }
            return null;
        }

        #endregion References

        #region Classes

        public class Props
        {
            public BasePlayer player { get; set; }
            public BaseEntity entity { get; set; }
        }

        private class NPCController : MonoBehaviour
        {
            public BasePlayer player;
            public NpcAI npcAi;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
        }

        private class NpcAI : MonoBehaviour
        {
            internal Vector3 targetPos = Vector3.zero;
            internal BaseCombatEntity targetEnt { get; set; }
            internal BaseMountable mountable;
            public BasePlayer npc;
            public NPCController owner;
            public BaseEntity entity;
            public RidableHorse horse;
            public BaseNpc baseNpc;
            public Transform transformer;
            public ResourceDispenser dispenser;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                transformer = transform;

                var component = GetComponent<ResourceDispenser>();
                if (component != null)
                {
                    dispenser = component;
                }
                if (entity is RidableHorse)
                {
                    horse = entity as RidableHorse;
                    horse.maxSpeed = 200000f;
                    horse.walkSpeed = 2000f;
                    horse.trotSpeed = 4000f;
                    horse.runSpeed = horse.maxSpeed;
                }
                else if (entity is BasePlayer)
                {
                    npc = entity as BasePlayer;
                }
            }

            private void OnDestroy()
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.Kill();
                }

                if (targetEnt != null && !targetEnt.IsDestroyed)
                {
                    targetEnt.Kill();
                }

                Destroy(this);
            }

            private void Update()
            {
                if (owner == null || owner.player == null)
                {
                    return;
                }
                if (horse != null && !horse.IsDestroyed)
                {
                    horse.ServerPosition = owner.player.ServerPosition + owner.player.eyes.BodyForward() * 4f;
                    horse.ServerRotation = owner.player.eyes.bodyRotation;
                    return;
                }
                if (npc != null && !npc.IsDestroyed)
                {
                    if (baseNpc != null)
                    {
                        baseNpc.BlockEnemyTargeting(2000f);
                    }
                    npc.ServerPosition = owner.player.ServerPosition + owner.player.eyes.BodyForward() * 2f;
                    npc.ServerRotation = owner.player.ServerRotation;
                    npc.eyes.bodyRotation = owner.player.eyes.bodyRotation;
                    npc.eyes.rotation = owner.player.eyes.rotation;
                    return;
                }

                if (entity != null && !entity.IsDestroyed)
                {
                    entity.ServerPosition = owner.player.ServerPosition + owner.player.eyes.BodyForward() * 2.8f;
                    entity.ServerRotation = owner.player.eyes.bodyRotation;
                }
            }
        }

        #endregion Classes

        #region Messages/Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UnknownError"] = "Something went wrong",
                ["PlayerNotAProp"] = "You are not a prop",
                ["PlayerAlreadyAProp"] = "You are already a prop",
                ["PlayerBecameAProp"] = "You became a <color=#FFA500>{0}</color>",
                ["PlayerLeftAProp"] = "You are not a prop anymore",
                ["NoPermissions"] = "Missing permission",
                ["NoVanshPermission"] = "Missing vanish permission",
                ["NoVanishPlugin"] = "Vanish plugin required",
                ["InvalidArgs"] = "Invalid Arguments",
                ["PropsList"] = "<color=#FFA500>Available Props</color> \n\n<color=#FFA500>•</color> {0}",
            }, this);
        }

        private string Message(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Messages/Localization

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(permallow, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }
        protected override void LoadConfig()
        {
            var newConfig = new DynamicConfigFile($"{Manager.ConfigPath}/{Name}.json");
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }
            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = newConfig.ReadObject<PluginConfig>();
            if (_pluginConfig.PropControlConfig == null)
            {
                _pluginConfig.PropControlConfig = new PropControlConfig
                {
                    PropPrefabMap = new Dictionary<string, string> {
                        { "basichorse", "assets/rust.ai/agents/horse/horse.prefab" },
                        { "boar", "assets/rust.ai/agents/boar/boar.prefab" },
                        { "bear", "assets/rust.ai/agents/bear/bear.prefab" },
                        { "wolf", "assets/rust.ai/agents/wolf/wolf.prefab" },
                        { "chicken", "assets/rust.ai/agents/chicken/chicken.prefab" },
                        { "horse", "assets/rust.ai/nextai/testridablehorse.prefab" },
                        { "stag", "assets/rust.ai/agents/stag/stag.prefab" },
                        { "scientist", "assets/prefabs/npc/scientist/scientist.prefab" },
                        { "heavy", "assets/rust.ai/agents/npcplayer/humannpc/heavyscientist/heavyscientist.prefab" },
                        { "peacekeeper", "assets/prefabs/npc/scientist/scientistpeacekeeper.prefab" },
                        { "zombie", "assets/rust.ai/agents/zombie/zombie.prefab" }
                    }
                };
            }
            newConfig.WriteObject(_pluginConfig);
            PrintWarning("Config Loaded");
        }

        private void Unload()
        {
            foreach (var prop in data)
            {
                if (prop.entity == null || prop.entity.IsDestroyed) continue;
                prop.entity.Kill();
            }

            data.Clear();
        }

        #endregion Hooks

        #region Commands

        [ChatCommand("leave")]
        private void LeaveCommand(BasePlayer player)
        {
            int index = data.FindIndex(data => data.player == player);
            if (index == -1)
            {
                PrintToChat(player, Message("PlayerNotAProp", player.UserIDString));
                return;
            }
            BaseEntity entity = data[index].entity;
            data.RemoveAt(index);
            Puts(data.Count.ToString());
            if (!entity.IsDestroyed) entity.Kill();
            PrintToChat(player, Message("PlayerLeftAProp", player.UserIDString));
        }

        [ChatCommand("become")]
        private void BecomeCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "PropControl.allow"))
            {
                PrintToChat(player, Message("NoPermissions", player.UserIDString));
                return;
            }
            if (!permission.UserHasPermission(player.UserIDString, "vanish.allow") && !permission.UserHasPermission(player.UserIDString, "bettervanish.allowed"))
            {
                PrintToChat(player, Message("NoVanshPermission", player.UserIDString));
                return;
            }
            if ((Vanish == null || !Vanish.IsLoaded || !_pluginConfig.PropControlConfig.UseVanish)
                && (BetterVanish == null || !BetterVanish.IsLoaded || !_pluginConfig.PropControlConfig.UseBetterVanish))
            {
                PrintToChat(player, Message("NoVanishPlugin", player.UserIDString));
                return;
            }
            if (args.Length < 1)
            {
                PrintToChat(player, Message("InvalidArgs", player.UserIDString));
                return;
            }
            int index = data.FindIndex(data => data.player == player);
            if (index != -1) // removed linq
            {
                if (data[index].entity.IsFullySpawned())
                {
                    PrintToChat(player, Message("PlayerAlreadyAProp", player.UserIDString));
                    return;
                }
                else
                {
                    data.RemoveAt(index);
                }
            }

            if (_pluginConfig.PropControlConfig.UseBetterVanish && BetterVanish != null && BetterVanish.IsLoaded)
            {
                if (!BetterVanish.Call<bool>("IsInvisible", player))
                {
                    BetterVanish.Call("Disappear", player);
                }
            }
            else if (_pluginConfig.PropControlConfig.UseVanish && Vanish != null && Vanish.IsLoaded)
            {
                if (!Vanish.Call<bool>("IsInvisible", player))
                {
                    Vanish.Call("Disappear", player);
                }
            }

            string prefab = DeterminePrefab(args[0]);

            if (prefab == "Invalid")
            {
                PrintToChat(player, Message("InvalidArgs", player.UserIDString));
                return;
            }

            var prop = GameManager.server.CreateEntity(prefab, player.transform.position, player.transform.rotation, true);
            if (prop == null)
            {
                PrintToChat(player, Message("UnknownError", player.UserIDString));
                return;
            }

            Rust.Ai.AiManagedAgent agentComponent;
            if (prop.TryGetComponent(out agentComponent))
            {
                UnityEngine.Object.Destroy(agentComponent);
            }

            PlayerInput playerInput;
            if (prop.TryGetComponent(out playerInput))
            {
                UnityEngine.Object.Destroy(playerInput);
            }

            NPCController controller = player.gameObject.AddComponent<NPCController>();
            controller.npcAi = prop.gameObject.AddComponent<NpcAI>();
            controller.npcAi.entity = prop;

            var dispenser = prop.GetComponent<ResourceDispenser>();
            if (dispenser != null)
            {
                controller.npcAi.dispenser = dispenser;
            }

            var transformer = prop.GetComponent<Transform>();
            if (transformer != null)
            {
                controller.npcAi.transformer = transformer;
            }

            controller.npcAi.owner = controller;

            data.Add(new Props() { player = player, entity = prop });
            prop.Spawn();

            PrintToChat(player, Message("PlayerBecameAProp", player.UserIDString, args[0]));
        }

        [ChatCommand("props")]
        private void PropsCommand(BasePlayer player)
        {
            PrintToChat(player, Message("PropsList", player.UserIDString, string.Join("\n<color=#FFA500>•</color> ", _pluginConfig.PropControlConfig.PropPrefabMap.Keys)));
        }

        #endregion Commands

        #region Helpers

        private static BaseEntity FindObject(Ray ray, float distance)
        {
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
        }

        private string DeterminePrefab(string argument)
        {
            if (_pluginConfig.PropControlConfig.PropPrefabMap.ContainsKey(argument))
            {
                return _pluginConfig.PropControlConfig.PropPrefabMap[argument];
            }

            return "Invalid";
        }

        #endregion Helpers

        #region Classes

        private class PluginConfig
        {
            public PropControlConfig PropControlConfig { get; set; }
        }

        private class PropControlConfig
        {
            [JsonProperty(PropertyName = "Use - BetterVanish")]
            public bool UseBetterVanish { get; set; }

            [JsonProperty(PropertyName = "Use - Vanish")]
            public bool UseVanish { get; set; } = true;

            [JsonProperty(PropertyName = "PropPrefabMap")]
            public Dictionary<string, string> PropPrefabMap { get; set; }
            [JsonProperty(PropertyName = "Invulnerable Props - Enabled")]
            public bool InvulnerableProps { get; set; } = true;
        }

        #endregion Classes
    }
}
