using System;
using System.Collections.Generic;
using System.Linq;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Forever Alone", "misticos", "1.0.1")]
    [Description("Limit the number of players in a team on the server")]
    class ForeverAlone : RustPlugin
    {
        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");

		static float AroundRadius;
        static float CheckInterval;
        static float timeBeforeShock;
        static float DamagePerTime;
		
        static int MaxAllowedPlayers;

        void OnServerInitialized()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CheckComponent(player);
            }

            MaxAllowedPlayers = Config.Get<int>("MaxAllowedPlayers");

            Puts("Max allowed players in team: " + MaxAllowedPlayers);
        }

        protected override void LoadDefaultConfig()
        {
            Config["MaxAllowedPlayers"] = MaxAllowedPlayers = GetConfig("MaxAllowedPlayers", 2);
			Config["AroundRadius"] = AroundRadius = GetConfig("AroundRadius", 10f);
			Config["CheckInterval"] = CheckInterval = GetConfig("CheckInterval", 5f);
			Config["timeBeforeShock"] = timeBeforeShock = GetConfig("timeBeforeShock", 60f);
			Config["DamagePerTime"] = DamagePerTime = GetConfig("DamagePerTime", 50f);
        }
		
		T GetConfig<T>(string name, T defaultValue)
            => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));

        void Unload()
        {
            foreach (AroundTimer arTimer in Resources.FindObjectsOfTypeAll<AroundTimer>())
            {
                UnityEngine.Object.Destroy(arTimer);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            CheckComponent(player);
        }

        void CheckComponent(BasePlayer player)
        {
            AroundTimer arTimer = player.GetComponent<AroundTimer>();

            if (!arTimer)
            {
                player.gameObject.AddComponent<AroundTimer>();
            }
        }

        class AroundTimer : MonoBehaviour
        {
            float ElaspedSeconds;
            BasePlayer player;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckAround", CheckInterval, CheckInterval);
            }

            void CheckAround()
            {
                if (!player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                if (player.IsDead() || player.IsSleeping()) return;

                int entities = Physics.OverlapSphereNonAlloc(player.transform.position, AroundRadius, Vis.colBuffer, playerLayer);

                int playersAround = 0;

                for (var i = 0; i < entities; i++)
                {
                    var player = Vis.colBuffer[i].GetComponentInParent<BasePlayer>();

                    if (player != null && (player == this.player || !player.IsDead() && !player.IsSleeping() && IsVisible(player, this.player.eyes.position, player.eyes.position)))
                    {
                        playersAround++;           
                    }
                }

                if(playersAround > MaxAllowedPlayers)
                {
                    ElaspedSeconds += CheckInterval;

                    if (ElaspedSeconds >= timeBeforeShock)
                    {
                        player.ChatMessage($"You exceed the limit of the joint game.\nNo more than {MaxAllowedPlayers} people per team.");
                        DoShock(player);
                    }
                }
                else
                {
                    ElaspedSeconds = Mathf.Max(0, ElaspedSeconds - CheckInterval); 
                }
            }
        }

        int GetLimit => MaxAllowedPlayers;

        static void DoShock(BasePlayer player)
        {
            player.Hurt(DamagePerTime, DamageType.ElectricShock, player, false);
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", player, 0, Vector3.zero, Vector3.forward, null, false);
        }

        static bool IsVisible(BasePlayer player, Vector3 source, Vector3 dest) => player.IsVisible(source, dest);
    }
}