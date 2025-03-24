using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections.Generic;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Own Casino", "NooBlet", "1.0.6")]
    [Description("Make your own Casino Free version")]
    public class OwnCasino : CovalencePlugin
    {
        #region Vars
        private const ulong skinIDwheel = 2763733280;
        private const ulong skinIDterminal = 2763733201;
        private const string wheelprefab = "assets/prefabs/misc/casino/bigwheel/big_wheel.prefab";
        private const string terminalprefab = "assets/prefabs/misc/casino/bigwheel/bigwheelbettingterminal.prefab";
        private const string chairprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
        private static OwnCasino plugin;
        static List<string> effects = new List<string>
        {
        "assets/bundled/prefabs/fx/item_break.prefab",
        "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
        };

        #endregion Vars

        #region Hooks
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            RunChecks(go.ToBaseEntity());
        }

        private void RunChecks(BaseEntity baseEntity)
        {
            CheckDeployTermanal(baseEntity);
            CheckDeployWheel(baseEntity);
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            CheckHit(player, info?.HitEntity);

        }
        private void Loaded()
        {
            CheckchildClass();
        }
        private void CheckchildClass()
        {
            timer.Every(600f, () =>
            {
                foreach (var w in BigWheelGame.serverEntities)
                {
                    var wheel = w?.GetComponent<BigWheelGame>();
                    if (wheel == null) continue;
                    if (wheel.OwnerID != 0)
                    {
                        if (!wheel.HasComponent<WheelComponent>())
                        {
                            wheel.gameObject.AddComponent<WheelComponent>();
                            Puts("wheel checked and fixed");
                        }
                    }
                }
              
            });

        }

        #endregion Hooks

        #region Wheel Stuff
        private void CheckDeployWheel(BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (!IsWheel(entity.skinID))
            {
                return;
            }


            var transform = entity.transform;
            SpawnWheel(transform.position, transform.rotation, entity.OwnerID);
            entity.transform.position -= new Vector3(0, 3, 0);
            entity.SendNetworkUpdate();
            NextTick(() => { entity?.Kill(); });
        }

        private bool IsWheel(ulong skin)
        {
            return skin == skinIDwheel;
        }

        private void SpawnWheel(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0)
        {
            var newpos = new Vector3(position.x, position.y + 1, position.z + 0.2f);
            var wheel = GameManager.server.CreateEntity(wheelprefab, newpos, rotation);
            if (wheel == null)
            {
                return;
            }
            var transform = wheel.transform;
            var old = transform.eulerAngles;

            old.z -= 90;
            old.y -= 90;
            transform.eulerAngles = old;
            wheel.OwnerID = ownerID;
            wheel.gameObject.AddComponent<WheelComponent>();
            wheel.Spawn();
        }

        private void CheckHit(BasePlayer player, BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            if (player.userID == entity.OwnerID)
            {
                if (IsWheel(entity.skinID))
                {

                    timer.Once(0.2f, () =>
                    {
                        if (entity.IsValid() == true)
                        {
                            entity.GetComponent<WheelComponent>()?.TryPickup(player);
                        }
                    });
                }

               
            }
        }


        #endregion Wheel Stuff

        #region Terminal Stuff

        private void CheckDeployTermanal(BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (!IsTermanal(entity.skinID))
            {
                return;
            }

            var transform = entity.transform;
            SpawnTerminal(transform.position, transform.rotation, entity.OwnerID);
            entity.transform.position -= new Vector3(0, 3, 0);
            entity.SendNetworkUpdate();
            NextTick(() => { entity?.Kill(); });
        }
        private bool IsTermanal(ulong skin)
        {
            return skin == skinIDterminal;
        }

        private void SpawnTerminal(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0)
        {
            var newpos = new Vector3(position.x + 0.8f, position.y, position.z - 0.3f);
            var Terminal = GameManager.server.CreateEntity(terminalprefab, newpos, rotation);
            var chair = GameManager.server.CreateEntity(chairprefab, position, rotation);
            if (Terminal == null || chair == null)
            {
                return;
            }
            chair.OwnerID = ownerID;
            chair.Spawn();
            Terminal.SetParent(chair);
            Terminal.OwnerID = ownerID;
            Terminal.transform.localPosition = new Vector3(0.8f, 0, 0.2f);
            Terminal.transform.localRotation = Quaternion.Euler(new Vector3(0, -90, 0));
            Terminal.SetFlag(BigWheelBettingTerminal.Flags.On, true);
            Terminal.SendNetworkUpdateImmediate();
            Terminal.Spawn();
        }

        #endregion Termanal stuff

        #region Commands

        [Command("casino")]
        private void casinoCommand(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (player == null) { return; }  // no intent dor console command .. only ingame use
            if (player.IsAdmin)
            {
                player.ChatMessage(string.Format(GetLang("GiveItems", player.UserIDString)));
                GiveItems(player);
            }
            else
            {
                player.ChatMessage(string.Format(GetLang("NoUse", player.UserIDString)));
            }
        }



        #endregion Commands

        #region Item Creation
        private void GiveItems(BasePlayer player)
        {
            var items = CreateItems();

            if (items != null && player != null)
            {
                foreach (var i in items)
                {
                    player.GiveItem(i);
                }
                player.ChatMessage(string.Format(GetLang("GiveItems", player.UserIDString)));

            }
        }

        private Item[] CreateItems()
        {
            var wheel = ItemManager.CreateByName("sign.wooden.large", 1);
            var terminal = ItemManager.CreateByName("chair", 1);
            if (wheel != null || terminal != null)
            {
                wheel.name = "adminbigwheel";
                wheel.skin = skinIDwheel;
                terminal.name = "admintermanal";
                terminal.skin = skinIDterminal;
            }
            Item[] items = { wheel, terminal };
            return items;
        }
        #endregion Item Creation

        #region Classes

        private class WheelComponent : MonoBehaviour
        {
            private BigWheelGame wheel;

            private void Awake()
            {
                wheel = GetComponent<BigWheelGame>();
                InvokeRepeating("CheckGround", 5f, 5f);


            }
            private void CheckGround()
            {
                RaycastHit rhit;
                var cast = Physics.Raycast(wheel.transform.position + new Vector3(0, -0.6f, 0), Vector3.down,
                    out rhit, 21f, LayerMask.GetMask("Terrain", "Construction"));

                var entity = rhit.GetEntity();
                if (entity != null)
                {
                    if (isGroundMissing(entity))
                    {
                        GroundMissing();
                    }
                }
                else
                {
                    GroundMissing();
                }
            }
            bool isGroundMissing(BaseEntity entity)
            {
                if (entity.ShortPrefabName.Contains("foundation")) { return false; }
                if (entity.ShortPrefabName.Contains("floor")) { return false; }

                return true;
            }


            private void GroundMissing()
            {
                foreach (var effect in effects) { Effect.server.Run(effect, wheel.transform.position); }
                wheel.Kill();
            }
            public void TryPickup(BasePlayer player)
            {
                var info = default(SlotMachinePayoutSettings.PayoutInfo);
                Effect.server.Run(info.OverrideWinEffect.resourcePath, wheel.transform.position);
                wheel.Kill();
                plugin.GiveItems(player);
            }

            public void DoDestroy()
            {
                Destroy(this);
            }
        }



      

        #endregion Classes

        #region Lang API

        private string GetLang(string key, string id) => lang.GetMessage(key, this, id);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoUse"] = "You are not allowed to use this command!",
                ["GiveItems"] = "Casino Items Given!",

            }, this, "en");
        }

        #endregion Lang API

    }
}