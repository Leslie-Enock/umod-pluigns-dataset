/*
 ########### README ####################################################
                                                                             
  !!! DON'T EDIT THIS FILE !!!
                                                                     
 ########### CHANGES ###################################################

 1.0.0
    - Plugin release

 #######################################################################
*/

using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Libraries;
using VLB;

namespace Oxide.Plugins
{
    [Info("Player Effects", "paulsimik", "1.0.0")]
    [Description("Adds Effect to the Player")]
    class PlayerEffects : RustPlugin
    {
        private bool DEBUG = false;

        #region [Fields]

        private const string permUse = "playereffects.use";
        private const string MISSING_EFFECT = "assets/bundled/prefabs/fx/missing.prefab";
        private const string BARRICADE_EFFECT = "assets/bundled/prefabs/fx/door/barricade_spawn.prefab";
        private const string FIRE2_EFFECT = "assets/bundled/prefabs/fx/fire/fire_v2.prefab";

        #endregion

        #region [Oxide Hooks]

        private void Init() => permission.RegisterPermission(permUse, this);

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyPlayerComponent(player);
        }

        private void OnPlayerDisconnected(BasePlayer player) => DestroyPlayerComponent(player);

        #endregion

        #region [Hooks]   

        private PlayerEffect GetPlayer(BasePlayer player)
        {
            if (player == null)
                return null;

            var playerEffect = player.GetComponent<PlayerEffect>();
            if (playerEffect == null)
                return null;

            return playerEffect;
        }

        private void DestroyPlayerComponent(BasePlayer player)
        {
            PlayerEffect playerEffect = GetPlayer(player);
            if (playerEffect == null)
                return;

            UnityEngine.Object.Destroy(playerEffect);
        }

        private void AddEffect(PlayerEffect playerEffect, string typeEffect)
        {
            switch (typeEffect)
            {
                case "0":
                case "disable":
                case "disabled":
                    {
                        playerEffect.DestroyTimer();
                        SendReply(playerEffect.player, GetLang("Disable", playerEffect.player.UserIDString));                 
                        return;
                    }
                case "1":
                case "particles":
                    {
                        playerEffect.effect = MISSING_EFFECT;
                        playerEffect.effectPosition = Vector3.zero;
                        playerEffect.time = 1f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect1", playerEffect.player.UserIDString));
                        return;
                    }
                case "2":
                case "smoke":
                    {
                        playerEffect.effect = BARRICADE_EFFECT;
                        playerEffect.effectPosition = Vector3.zero;
                        playerEffect.time = 0.2f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect2", playerEffect.player.UserIDString));
                        return;
                    }
                case "3":
                case "fire":
                    {
                        playerEffect.effect = FIRE2_EFFECT;
                        playerEffect.effectPosition = new Vector3(0, 0, -1);
                        playerEffect.time = 8f;
                        playerEffect.DestroyTimer();
                        playerEffect.RunTimer();
                        SendReply(playerEffect.player, GetLang("Effect3", playerEffect.player.UserIDString));
                        return;
                    }
                case "help":
                    {
                        SendReply(playerEffect.player, GetLang("Help", playerEffect.player.UserIDString));
                        return;
                    }
                default:
                    SendReply(playerEffect.player, GetLang("Invalid", playerEffect.player.UserIDString));
                    return;
            }
        }

        #endregion

        #region [Chat Commands]

        [ChatCommand("pe")]
        private void cmdPlayerEffect(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return;
            }

            PlayerEffect playerEffect = player.gameObject.GetOrAddComponent<PlayerEffect>();
            if (playerEffect == null)
                return;

            var type = args.Length > 0 ? args[0] : null;
            AddEffect(playerEffect, type);
        }

        #endregion

        #region [Classes]

        public class PlayerEffect : MonoBehaviour
        {
            public BasePlayer player;
            public string effect;
            public Vector3 effectPosition;
            public float time;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                effect = string.Empty;
                effectPosition = Vector3.zero;
                time = 1f;
            }

            public void RunTimer() => InvokeRepeating("RunEffect", 0.2f, time);

            public void DestroyTimer() => CancelInvoke("RunEffect");

            private void RunEffect()
            {
                if (string.IsNullOrEmpty(effect) || player == null)
                    return;

                Effect.server.Run(effect, player, 0, effectPosition, new Vector3(1, 0, 0), null, true);
            }

            private void OnDestroy()
            {
                DestroyTimer();
                Destroy(this); 
            }
        }

        #endregion

        #region [Localization]

        private string GetLang(string key, string playerID) => lang.GetMessage(key, this, playerID);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPerm", "You don't have permissions" },
                { "Invalid", "Invalid syntax!\nType /pe help" },
                { "Disable", "Effect has been disabled" },
                { "Effect1", "Effect <color=#a8a6a6>'particles'</color> has been activated" },
                { "Effect2", "Effect <color=#a8a6a6>'smoke'</color> has been activated" },
                { "Effect3", "Effect <color=#a8a6a6>'fire'</color> has been activated" },
                { "Help", "<size=16><color=#3498db>Player Effects</color></size>" +
                "\n<color=#a8a6a6>/pe 0 or disable</color> - disable effect" +
                "\n<color=#a8a6a6>/pe 1 or particles</color> - particles effect" +
                "\n<color=#a8a6a6>/pe 2 or smoke</color> - smoke effect" +
                "\n<color=#a8a6a6>/pe 3 or fire</color> - fire effect" }

            }, this);
        }

        #endregion

        #region [Helpers]

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permUse);
        }

        private void SendMessage(BasePlayer player, string msg) => Player.Message(player, msg);

        private void PrintDebug(object message)
        {
            if (!DEBUG)
                return;

            Debug.Log($"{this.Name} Debug: {message}");
        }

        #endregion
    }
}