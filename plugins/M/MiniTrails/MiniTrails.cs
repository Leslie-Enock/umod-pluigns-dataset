/*
  ██████  ▄▄▄       ██▓      ██████  ▄▄▄
▒██    ▒ ▒████▄    ▓██▒    ▒██    ▒ ▒████▄
░ ▓██▄   ▒██  ▀█▄  ▒██░    ░ ▓██▄   ▒██  ▀█▄
  ▒   ██▒░██▄▄▄▄██ ▒██░      ▒   ██▒░██▄▄▄▄██
▒██████▒▒ ▓█   ▓██▒░██████▒▒██████▒▒ ▓█   ▓██▒
▒ ▒▓▒ ▒ ░ ▒▒   ▓▒█░░ ▒░▓  ░▒ ▒▓▒ ▒ ░ ▒▒   ▓▒█░
░ ░▒  ░ ░  ▒   ▒▒ ░░ ░ ▒  ░░ ░▒  ░ ░  ▒   ▒▒ ░
░  ░  ░    ░   ▒     ░ ░   ░  ░  ░    ░   ▒   
 Contact Salsa#7717 on Discord for programming/business inquiries
*/

using System.Collections.Generic;
using System.Collections;
using Oxide.Game.Rust.Libraries;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mini Trails", "Salsa", "2.1.4")]
    [Description("Draws trails behind minicopters")]
    class MiniTrails : RustPlugin
    {
        #region Fields

        private static Data data;
        private Dictionary<ulong, MiniT> MountedMinis = new Dictionary<ulong, MiniT>();

        private const string SnowballGunEnt = "assets/prefabs/misc/xmas/snowballgun/snowballgun.entity.prefab";
        private const string AnchorEnt      = "assets/prefabs/visualization/sphere.prefab"; // Looking for invisible alternative
        private const string FlameEnt       = "assets/prefabs/weapons/flamethrower/flamethrower.entity.prefab";
        private const string WooshSound     = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
        private const string SupplyEnt      = "assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab";
        private const string FireworkEnt    = "assets/prefabs/deployable/fireworks/volcanofirework-red.prefab";

        private const string PermUse = "minitrails.use";

        #endregion

        #region Configuration

        private enum trailType
        {
            fire,
            snow, // not working properly
            pink,
            firework
        }

        private class Preferences
        {
            public bool active = false;

            public int count = 1;

            public trailType type = trailType.fire;

            public Preferences() { }
        }

        private class Data
        {
            public Dictionary<ulong, Preferences> preferences = new Dictionary<ulong, Preferences>();

            public Data() { }
        }

        #endregion

        #region Hooks

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            var mini = mountable.GetComponentInParent<BaseVehicle>() ?? null;
            if (mini == null || 
                !(mini is Minicopter) ||
                mini.mountPoints[0].mountable != mountable ||
                !permission.UserHasPermission(player.UserIDString, PermUse)) return;

            mini.OwnerID = player.userID;
            if (MountedMinis.ContainsKey(player.userID))
                MountedMinis.Remove(player.userID);

            MountedMinis.Add(player.userID, new MiniT(mini, player));
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            var mini = mountable.GetComponentInParent<Minicopter>() ?? null;
            if (mini == null || !MountedMinis.ContainsKey(player.userID)) return;

            try
            {
                MountedMinis[player.userID].clearTrail();
                MountedMinis.Remove(player.userID);
            }
            catch { }

        }

        private void OnEntityKill(Minicopter mini)
        {
            if (!MountedMinis.ContainsKey(mini.OwnerID) || !data.preferences.ContainsKey(mini.OwnerID)) return;
            if (!data.preferences[mini.OwnerID].active) return;
            MountedMinis[mini.OwnerID].clearTrail();
            MountedMinis.Remove(mini.OwnerID);
        }

        private void OnServerSave() => Interface.Oxide.DataFileSystem.WriteObject(Name, data);

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, data);
            data = null;
        }

        private void Init()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
            if (data == null)
            {
                data = new Data();
                Interface.Oxide.DataFileSystem.WriteObject(Name, data);
            }

            permission.RegisterPermission(PermUse, this);
        }

        #endregion

        private class MiniT // A mini with a trail
        {
            public BaseVehicle mini;
            public BasePlayer driver;
            public List<BaseEntity> anchors = new List<BaseEntity>();
            public List<BaseEntity> trails = new List<BaseEntity>();

            private void spawnFlame(string prefab, Vector3 localPos, Quaternion localRotation)
            {
                BaseEntity trail = GameManager.server.CreateEntity(prefab, mini.transform.position, new Quaternion());
                if (trail == null) return;

                if (data.preferences[driver.userID].type == trailType.snow)
                    trails.Add(trail);

                BaseEntity anchor = GameManager.server.CreateEntity(AnchorEnt, mini.transform.position, new Quaternion(0, 0, 0, 0), true);
                if (anchor == null) return;

                anchor.SetParent(mini);
                anchor.Spawn();
                anchor.transform.localPosition = localPos;
                anchor.transform.localRotation = localRotation;

                trail.SetParent(anchor);
                trail.Spawn();

                trail.SendNetworkUpdateImmediate(true);

                anchors.Add(anchor);
            }

            private void spawnSupplySignal(string prefab, Vector3 localPos, Quaternion localRotation)
            {
                BaseEntity trail = GameManager.server.CreateEntity(prefab, mini.transform.position);
                if (trail == null) return;

                trail.SetParent(mini);
                trail.transform.localPosition = localPos;
                trail.transform.localRotation = localRotation;
                trail.GetComponent<Rigidbody>().useGravity = false;
                trail.GetComponent<Rigidbody>().isKinematic = true;

                trail.Spawn();

                {
                    SupplySignal ss = trail as SupplySignal;

                    ss.CancelInvoke(ss.Explode);
                    ss.Invoke(() =>
                    {
                        ss.SetFlag(BaseEntity.Flags.On, true);
                        ss.SendNetworkUpdateImmediate();
                    }, 0);
                }

                trails.Add(trail);
            }

            private void spawnFirework(string prefab, Vector3 localPos, Quaternion localRotation)
            {

                BaseFirework fw = GameManager.server.CreateEntity(prefab) as BaseFirework;
                if (fw == null) return;

                fw.SetParent(mini);
                fw.transform.localPosition = localPos;
                fw.transform.localRotation = localRotation;

                fw.fuseLength = 0;
                fw.activityLength = 5000;
                fw.Ignite(fw.transform.position);

                fw.Spawn();
                trails.Add(fw);
            }

            public void spawnTrail()
            {
                switch (data.preferences[driver.userID].type)
                {
                    case trailType.fire:
                        switch (data.preferences[driver.userID].count)
                        {
                            case 1:
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -75, 0));
                                break;
                            case 2:
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -105, 0));
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -45, 0));
                                break;
                            case 3:
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -105, 0));
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -75, 0));
                                spawnFlame(FlameEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -45, 0));
                                break;
                            default:
                                break;
                        }
                        break;

                    case trailType.snow:
                        switch (data.preferences[driver.userID].count)
                        {
                            case 1:
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 1.5f, -1.0f), Quaternion.Euler(0f, -75, 0));
                                break;
                            case 2:
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -105, 0));
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0, -45, 0));
                                break;
                            case 3:
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0f, -105, 0));
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0f, -75, 0));
                                spawnFlame(SnowballGunEnt, new Vector3(0.0f, 0.8f, -1.0f), Quaternion.Euler(0f, -45, 0));
                                break;
                            default:
                                break;
                        }
                        break;

                    case trailType.pink:
                        spawnSupplySignal(SupplyEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, 0, 0));
                        break;

                    case trailType.firework:
                        switch (data.preferences[driver.userID].count)
                        {
                            case 1:
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, 0, 0));
                                break;
                            case 2:
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, -30, 0));
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, 30, 0));
                                break;
                            case 3:
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, -30, 0));
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, 0, 0));
                                spawnFirework(FireworkEnt, new Vector3(0, 0.8f, -2), Quaternion.Euler(-90, 30, 0));
                                break;
                            default:
                                break;
                        }
                        break;

                }

                Effect.server.Run(WooshSound, mini.transform.position + new Vector3(0.0f, 0.9f, -2.0f));
            }

            public void clearTrail()
            {
                switch (data.preferences[driver.userID].type)
                {
                    case trailType.fire:
                        foreach (BaseEntity s in anchors)
                            if (s != null) s.Kill();
                        anchors.Clear();
                        break;

                    case trailType.snow:
                        foreach (BaseEntity s in anchors)
                            if (s != null) s.Kill();
                        anchors.Clear();
                        //looper.Destroy();
                        break;

                    case trailType.firework:
                        foreach (BaseEntity t in trails)
                            if (t != null) t.Kill();
                        trails.Clear();
                        break;

                    case trailType.pink:
                        trails[0].Kill();
                        trails.Clear();
                        break;

                }
            }

            public MiniT(BaseVehicle _mini, BasePlayer _player)
            {
                mini = _mini;
                driver = _player;
                if (data.preferences.ContainsKey(_player.userID))
                    if (data.preferences[_player.userID].active)
                        spawnTrail();
            }
        }

        [ChatCommand("trail")]
        private void cmdTrail(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                SendChatMessage(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }

            if (!data.preferences.ContainsKey(player.userID))
                data.preferences.Add(player.userID, new Preferences());

            if (args.Length == 0)
            {
                if (data.preferences[player.userID].active)
                {
                    data.preferences[player.userID].active = false;
                    if (MountedMinis.ContainsKey(player.userID))
                        MountedMinis[player.userID].clearTrail();

                    SendChatMessage(player, lang.GetMessage("TrailOff", this, player.UserIDString));
                }
                else
                {
                    data.preferences[player.userID].active = true;
                    if (MountedMinis.ContainsKey(player.userID))
                        MountedMinis[player.userID].spawnTrail();

                    SendChatMessage(player, lang.GetMessage("TrailOn", this, player.UserIDString));
                }

                return;
            }
            else if (args.Length == 1)
            {
                int newCount;
                try
                {
                    newCount = int.Parse(args[0]);
                    if (newCount > 0 && newCount < 4)
                    {
                        if (MountedMinis.ContainsKey(player.userID) && data.preferences[player.userID].active)
                            MountedMinis[player.userID].clearTrail();
                        data.preferences[player.userID].count = newCount;
                        if (MountedMinis.ContainsKey(player.userID) && data.preferences[player.userID].active)
                            MountedMinis[player.userID].spawnTrail();
                        SendChatMessage(player, lang.GetMessage("SetCount", this, player.UserIDString) + $" {args[0]}");
                    }
                    else
                    {
                        SendChatMessage(player, lang.GetMessage("InvalidTrailCount", this, player.UserIDString));
                        return;
                    }
                }

                catch (FormatException)
                {
                    trailType newType = data.preferences[player.userID].type;

                    switch (args[0].ToLower())
                    {
                        case "fire":
                            newType = trailType.fire;
                            SendChatMessage(player, lang.GetMessage("SetFire", this, player.UserIDString));
                            break;

                        case "snow":
                            newType = trailType.snow;
                            SendChatMessage(player, lang.GetMessage("SetSnow", this, player.UserIDString));
                            break;

                        case "pink":
                            newType = trailType.pink;
                            SendChatMessage(player, lang.GetMessage("SetPink", this, player.UserIDString));
                            break;

                        case "firework":
                            newType = trailType.firework;
                            SendChatMessage(player, lang.GetMessage("SetFirework", this, player.UserIDString));
                            break;

                        default:
                            SendChatMessage(player, lang.GetMessage("UnknownArg", this, player.UserIDString) + $" \"{args[0]}\"");
                            return;
                    }

                    if (MountedMinis.ContainsKey(player.userID) && data.preferences[player.userID].active)
                        MountedMinis[player.userID].clearTrail();
                    data.preferences[player.userID].type = newType;
                    if (MountedMinis.ContainsKey(player.userID) && data.preferences[player.userID].active)
                        MountedMinis[player.userID].spawnTrail();
                }
            }
            else SendChatMessage(player, lang.GetMessage("Syntax", this, player.UserIDString));
        }

        /*
        [ChatCommand("test")]
        private void cmdTest(BasePlayer player, string command, string[] args)
        {
        }
        */

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "Mini Trails",
                ["Syntax"] =
                    "Invalid arguments\n" +
                    "<color=#00FF00>Usage:</color>\n" +
                    "/trail\t\t\t\t\t|   toggle trail\n" +
                    "/trail [1-3]\t\t\t\t|   set number of trails\n" +
                    "/trail [fire | snow | pink]\t|   set trail type",
                ["SetFire"] = "Trail set to fire",
                ["SetSnow"] = "Trail set to snow",
                ["SetPink"] = "Trail set to pink smoke",
                ["SetFirework"] = "Trail set to firework",
                ["UnknownArg"] = "Unknown argument",
                ["InvalidTrailCount"] = "Invalid number of trails. Must be between 1 and 3",
                ["SetCount"] = "Trail count set to",
                ["TrailOn"] = "Trail toggled on!",
                ["TrailOff"] = "Trail toggled off",
                ["NoPerm"] = "You do not have permission to use this command",
                ["MessageHeader"] = "<color=#c21111>Rustic Rejects</color>: "
            }, this);
        }

        void SendChatMessage(BasePlayer player, string msg) =>
            SendReply(player, lang.GetMessage("MessageHeader", this, player.UserIDString) + msg);
    }
}