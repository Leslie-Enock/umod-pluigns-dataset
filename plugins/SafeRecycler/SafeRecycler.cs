using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("Safe Recycler", "Hovmodet", "1.0.1")]
    [Description("Prevents players from getting pushed away from recyclers in safezones.")]
    public class SafeRecycler : RustPlugin
    {
        const string InvisChair = "assets/prefabs/vehicle/seats/standingdriver.prefab";
        List<BasePlayer> RecyclingPlayers = new List<BasePlayer>();
        private const string UsePermission = "saferecycler.use";

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        void OnLootEntity(BasePlayer player, Recycler recycler)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "saferecycler.use"))
                return;

            if (!player.InSafeZone())
                return;

            RecyclingPlayers.Add(player);
            CuiHelper.AddUi(player, CreateUI_recyclerLock());
            CuiHelper.AddUi(player, AddButton(player));

        }
        void OnLootEntityEnd(BasePlayer player, Recycler recycler)
        {
            if(RecyclingPlayers.Contains(player))  
                RecyclingPlayers.Remove(player);

            UnlockFromRecycler(player);
            CuiHelper.DestroyUi(player, "SafeRecycler_UI");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockText"] = "Lock <color=red>☐</color>",
                ["UnlockText"] = "Unlock <color=#93f542>☑</color>"
            }, this);
        }

        private void LockToRecycler(BasePlayer player)
        {
            if (!player.IsOnGround())
                return;

            if (!RecyclingPlayers.Contains(player))
                return;

            if (!player.isMounted)
            {
                var rot = player.GetNetworkRotation().eulerAngles;
                BaseMountable chair = SpawnChair(player.transform.position, Quaternion.Euler(0, rot.y, rot.z));
                player.MountObject(chair);
                chair.mountPose = PlayerModel.MountPoses.Standing;
                chair.MountPlayer(player);
                CuiHelper.DestroyUi(player, "btn_lock");
                CuiHelper.AddUi(player, AddButton(player));
            }
            else
                UnlockFromRecycler(player);
        }

        private void UnlockFromRecycler(BasePlayer player)
        {
            BaseMountable chair = player.GetMounted();
            if (chair == null)
                return;

            DismountSafely(player, chair);

            NextTick( () =>{
                chair.Kill(BaseNetworkable.DestroyMode.None);
            });
            CuiHelper.DestroyUi(player, "btn_lock");
            CuiHelper.AddUi(player, AddButton(player));
        }

        BaseMountable SpawnChair(Vector3 pos, Quaternion rotation)
        {
            var entity = GameManager.server.CreateEntity(InvisChair, pos, rotation);
            entity.Spawn();
            return (BaseMountable)entity;
        }

        [ConsoleCommand("locktorecycler")]
        private void Locktorecycler(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return;

            LockToRecycler(player);
        }

        private void DismountSafely(BasePlayer player, BaseMountable chair)
        {
            chair._mounted.DismountObject();
            chair._mounted.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            chair._mounted.MovePosition(player.transform.position);
            chair._mounted.SendNetworkUpdateImmediate();
            chair._mounted.SendModelState(true);
            chair._mounted = null;
            player.ForceUpdateTriggers();
            if ((bool)(UnityEngine.Object)player.GetParentEntity())
            {
                BaseEntity parentEntity = player.GetParentEntity();
                player.ClientRPCPlayer<Vector3, uint>(null, player, "ForcePositionToParentOffset", parentEntity.transform.InverseTransformPoint(player.transform.position), parentEntity.net.ID);
            }
            else
                player.ClientRPCPlayer<Vector3>(null, player, "ForcePositionTo", player.transform.position);
        }

        private CuiElementContainer AddButton(BasePlayer player)
        {
            bool locked = player.isMounted;
            string txt = lang.GetMessage("LockText", this, player.UserIDString);
            if (locked)
                txt = lang.GetMessage("UnlockText", this, player.UserIDString);
            CuiElementContainer elements = new CuiElementContainer();
            elements.Add(new CuiButton
            {
                Button =
                {
                    Color = "0.39 0.3 0.39 0",
                    Command = "locktorecycler"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                },
                Text =
                {
                    Text = txt,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1",
                    FontSize = 12,
                }
            }, "SafeRecycler_UI", "btn_lock");
            return elements;
        }

        private static CuiElementContainer CreateUI_recyclerLock()
        {
            CuiElementContainer elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.9 0.519", AnchorMax = "0.9463 0.546" }
            }, "Hud.Menu", "SafeRecycler_UI");
            return elements;
        }

    }

}