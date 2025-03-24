using Rust;
using System;
using GameTips;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Chute", "Colon Blow", "3.0.1")]
    class Chute : CovalencePlugin
    {
        // Added config options to block chute up if raid or combat blocked by NoEscape Plugin
        // Added config options for different cooldowns for chute and chuteup commands
        // Added config options for different VIP Coolodwns for chute and chuteup commands

        #region Loadup

        [PluginReference]
        Plugin NoEscape;

        static Chute _instance;
        private const string permAllowed = "chute.allowed";
        private const string permAllowedUp = "chute.up.allowed";
        private const string permNoCooldown = "chute.nocooldown";
        private const string permVIPCooldown = "chute.vipcooldown";


        private static List<ulong> coolDownList = new List<ulong>();

        static int layerMask = 1 << (int)Rust.Layer.Water | 1 << (int)Rust.Layer.World | 1 << (int)Rust.Layer.Construction | 1 << (int)Rust.Layer.Debris | 1 << (int)Rust.Layer.Default | 1 << (int)Rust.Layer.Terrain | 1 << (int)Rust.Layer.Tree | 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Deployed;

        private void Loaded()
        {
            _instance = this;
            LoadConfig();
            LoadMessages();
            permission.RegisterPermission(permAllowed, this);
            permission.RegisterPermission(permAllowedUp, this);
            permission.RegisterPermission(permNoCooldown, this);
            permission.RegisterPermission(permVIPCooldown, this);

        }

        #endregion

        #region Localization

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["noperms"] = "You don't have permission to use this command.",
                ["commandnotfound"] = "Command was not found, please try again.",
                ["undercooldown"] = "Chute Cooldown Active, please wait : ",
                ["cooldownremoved"] = "Chute Cooldown removed !! ",
                ["onground"] = "You are too close to the ground to do that !!! ",
                ["alreadymounted"] = "You are already mounted !!",
                ["raidblocked"] = "Sorry, you are Raid Blocked, you cannot do that.",
                ["combatblocked"] = "Sorry, you are Combat Blocked, you cannot do that.",
                ["readytodismount"] = "<color=red>SAFTEY RELEASED !!!</color> PRESS <color=black>[JUMP KEY]</color> again to cut parachute away.. warning you will Freefall !!!",
                ["usagemessage"] = "Use <color=black>[FWD, BACK, LEFT and RIGHT]</color> Keys to Speed UP, Slow Down and Turn\nPRESS <color=black>[JUMP KEY]</color> to cut away parachute and Free Fall !!"
            }, this);
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public ChuteSettings chuteSettings { get; set; }

            public class ChuteSettings
            {
                [JsonProperty(PropertyName = "Parachute - Speed - Max Forward speed allowed : ")] public float MaxParachuteFWDSpeed { get; set; }
                [JsonProperty(PropertyName = "Parachute - Lift - Max up lift force allowed : ")] public float MaxParachteLift { get; set; }
                [JsonProperty(PropertyName = "Drop Height - Altitude at which Chuteup moves player to : ")] public float ChuteDropHeight { get; set; }
                [JsonProperty(PropertyName = "Cooldown - Enable Cooldown on using any Chute command ? ")] public bool UseCooldown { get; set; }
                [JsonProperty(PropertyName = "Cooldown - Chute Command Cooldown Time (seconds) : ")] public float ChuteCoolDown { get; set; }
                [JsonProperty(PropertyName = "Cooldown - Chute Up Command Cooldown Time (seconds) : ")] public float ChuteUpCoolDown { get; set; }
                [JsonProperty(PropertyName = "Cooldown - VIP Chute Command Cooldown Time (Seconds) : ")] public float VIPChuteCoolDown { get; set; }
                [JsonProperty(PropertyName = "Cooldown - VIP Chute Up Command Cooldown Time (Seconds) : ")] public float VIPChuteUpCoolDown { get; set; }
                [JsonProperty(PropertyName = "Global - Map size offset - Moves the spawn locations farther inland so Chute and Player dont spawn at edge of map : ")] public float GlobalMapOffset { get; set; }
                [JsonProperty(PropertyName = "NoEspace - Raid Blocked  - Prevent Chute Up command if players are Raid Blocked with No Escape Plugin ? ")] public bool BlockOnRaid { get; set; }
                [JsonProperty(PropertyName = "NoEspace - Combat Blocked  - Prevent Chute Up command if players are Combat Blocked with No Escape Plugin ? ")] public bool BlockOnCombat { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                chuteSettings = new PluginConfig.ChuteSettings
                {
                    MaxParachuteFWDSpeed = 15f,
                    MaxParachteLift = 15f,
                    ChuteDropHeight = 500f,
                    UseCooldown = true,
                    ChuteCoolDown = 600f,
                    ChuteUpCoolDown = 1000f,
                    VIPChuteCoolDown = 300f,
                    VIPChuteUpCoolDown = 500f,
                    GlobalMapOffset = 500f,
                    BlockOnRaid = false,
                    BlockOnCombat = false,
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

        #region Commands

        [Command("Chute")]
        private void cmdChute(IPlayer iPlayer, string command, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            if (player && permission.UserHasPermission(player.UserIDString, permAllowed))
            {
                if (player.isMounted) { iPlayer.Message(lang.GetMessage("alreadymounted", this, iPlayer.Id)); return; }

                if (PlayerOnGround(player)) return;

                ServerMgr.Instance.StartCoroutine(ChuteProcessing(player, false));
                return;
            }
            else player.IPlayer.Message(lang.GetMessage("noperms", this, player.IPlayer.Id));
        }

        [Command("Chuteup")]
        private void cmdChuteUp(IPlayer iPlayer, string command, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            if (player && permission.UserHasPermission(player.UserIDString, permAllowedUp))
            {
                if (player.isMounted) { iPlayer.Message(lang.GetMessage("alreadymounted", this, iPlayer.Id)); return; }

                if (IsRaidBlocked(player) || IsCombatBlocked(player)) return;

                ServerMgr.Instance.StartCoroutine(ChuteProcessing(player, true));
                return;
            }
            else player.IPlayer.Message(lang.GetMessage("noperms", this, player.IPlayer.Id));
        }



        #endregion

        #region Rust Hooks

        private bool PlayerOnGround(BasePlayer player)
        {
            if (Physics.Raycast(new Ray(player.transform.position, Vector3.down), 3f, layerMask))
            {
                player.IPlayer.Message(lang.GetMessage("onground", this, player.IPlayer.Id));
                return true;
            }
            return false;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null) return null;
            var isParachuting = entity.GetComponentInParent<ParachuteEntity>();
            if (isParachuting && !isParachuting.wantsDismount) return false;
            return null;
        }

        #endregion

        #region Methods

        private bool IsRaidBlocked(BasePlayer player)
        {
            if (NoEscape == null) return false;
            if (!config.chuteSettings.BlockOnRaid) return false;
            bool success = Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
            if (success) player.IPlayer.Message(lang.GetMessage("raidblocked", this, player.IPlayer.Id));
            return success;
        }

        private bool IsCombatBlocked(BasePlayer player)
        {
            if (NoEscape == null) return false;
            if (!config.chuteSettings.BlockOnCombat) return false;
            bool success = Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player));
            if (success) player.IPlayer.Message(lang.GetMessage("combatblocked", this, player.IPlayer.Id));
            return success;
        }

        private Vector3 FindRandomLocation()
        {
            Vector3 spawnpoint = new Vector3();

            float spawnline = ((ConVar.Server.worldsize) / 2) - config.chuteSettings.GlobalMapOffset;
            float yrandom = UnityEngine.Random.Range(config.chuteSettings.ChuteDropHeight * 0.75f, config.chuteSettings.ChuteDropHeight * 1.25f);
            spawnpoint = new Vector3(UnityEngine.Random.Range(-spawnline, spawnline), yrandom, UnityEngine.Random.Range(-spawnline, spawnline));
            return spawnpoint;
        }

        private IEnumerator ChuteProcessing(BasePlayer player, bool isRandom)
        {

            if (!permission.UserHasPermission(player.UserIDString, permNoCooldown))
            {
                if (config.chuteSettings.UseCooldown)
                {
                    if (coolDownList.Contains(player.userID))
                    {
                        var timeLeft = config.chuteSettings.ChuteCoolDown;
                        if (isRandom) timeLeft = config.chuteSettings.ChuteUpCoolDown;
                        if (permission.UserHasPermission(player.UserIDString, permVIPCooldown))
                        {
                            timeLeft = config.chuteSettings.VIPChuteCoolDown;
                            if (isRandom) timeLeft = config.chuteSettings.VIPChuteUpCoolDown;
                        }

                        var hasTimer1 = player.GetComponent<CooldownTimer>();
                        if (hasTimer1)
                        {
                            timeLeft = timeLeft - (hasTimer1.cooldownTimer / 15);
                        }

                        player.IPlayer.Message(lang.GetMessage("undercooldown", this, player.IPlayer.Id) + $" {timeLeft.ToString("F0")} Seconds Left.");
                        yield break;
                    }

                    coolDownList.Add(player.userID);
                    var hasTimer = player.GetComponent<CooldownTimer>();
                    if (hasTimer) { hasTimer.OnDestroy(); };

                    var addTimer = player.gameObject.AddComponent<CooldownTimer>();
                    addTimer.EnableTimer(isRandom);
                }
            }

            if (!isRandom) { OpenParachute(player); yield break; }

            RespawnAtRandom(player);
            yield return new WaitForEndOfFrame();
        }

        private void RespawnAtRandom(BasePlayer player)
        {
            if (player == null) return;
            Vector3 respawnpos = new Vector3(0f, config.chuteSettings.ChuteDropHeight, 0f);
            respawnpos = FindRandomLocation();

            MovePlayerToPosition(player, respawnpos, Quaternion.identity);
            OpenParachute(player);
        }

        private void MovePlayerToPosition(BasePlayer player, Vector3 position, Quaternion rotation)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Unused2, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Unused1, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.transform.position = (position);
            player.transform.rotation = (rotation);
            player.StopWounded();
            player.StopSpectating();
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            player.ClearEntityQueue(null);
            player.SendFullSnapshot();
        }

        private static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        private void SendInfoMessage(BasePlayer player, string message, float time)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private void Unload()
        {
            coolDownList.Clear();
            DestroyAll<ParachuteEntity>();
            DestroyAll<CooldownTimer>();
        }

        #endregion

        #region API

        private void ExternalAddPlayerChute(BasePlayer player, bool isRandom = false)
        {
            ServerMgr.Instance.StartCoroutine(ChuteProcessing(player, isRandom));
        }

        #endregion

        #region Add Parachute

        public void OpenParachute(BasePlayer player)
        {
            if (player == null) return;
            var getParent = player.GetParentEntity();
            if (getParent != null)
            {
                float fwdVel = 1f;
                var hasRigid = getParent.GetComponentInParent<Rigidbody>();
                if (hasRigid) fwdVel = hasRigid.velocity.magnitude;
                AttachParachuteEntity(player, fwdVel);
            }
            else AttachParachuteEntity(player);
        }

        private void AttachParachuteEntity(BasePlayer player, float fwdVel = 1f)
        {
            Vector3 position = player.transform.position;
            var rotation = Quaternion.Euler(new Vector3(0f, player.GetNetworkRotation().eulerAngles.y, 0f));

            DroppedItem chutePack = new DroppedItem();
            chutePack = ItemManager.CreateByItemID(476066818, 1, 0).Drop(position, Vector3.zero, rotation).GetComponent<DroppedItem>();
            chutePack.allowPickup = false;
            chutePack.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), chutePack, "IdleDestroy"));

            var addParachutePack = chutePack.gameObject.AddComponent<ParachuteEntity>();
            addParachutePack.fwdForce = fwdVel;
            addParachutePack.SetPlayer(player);
            addParachutePack.SetInput(player.serverInput);
        }

        #endregion

        #region Parachute Entity

        public class ParachuteEntity : MonoBehaviour
        {
            Chute _instance = new Chute();
            private DroppedItem worldItem;
            private Rigidbody myRigidbody;
            private BaseEntity chair;
            private BaseMountable chairMount;
            private BaseEntity parachute;
            private BasePlayer player;

            private InputState input;

            public float fwdForce = 5f;
            public float upForce = -10f;

            private float counter = 0f;
            private bool enabled = false;
            public bool wantsDismount = false;
            private bool forceDismount = false;

            private void Awake()
            {
                worldItem = GetComponent<DroppedItem>();
                if (worldItem == null) { OnDestroy(); return; }
                myRigidbody = worldItem.GetComponent<Rigidbody>();
                if (myRigidbody == null) { OnDestroy(); return; }

                parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(), new Quaternion(), false);
                parachute.enableSaving = true;
                parachute.SetParent(worldItem, 0, false, false);
                parachute?.Spawn();
                parachute.transform.localEulerAngles += new Vector3(0, 0, 0);
                parachute.transform.localPosition += new Vector3(0f, 1.3f, -0.1f);

                string chairprefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
                chair = GameManager.server.CreateEntity(chairprefab, new Vector3(), new Quaternion(), false);
                chair.enableSaving = true;
                chair.GetComponent<BaseMountable>().isMobile = true;
                chair.Spawn();

                chair.transform.localEulerAngles += new Vector3(0, 0, 0);
                chair.transform.localPosition += new Vector3(0f, -1f, 0f);
                chair.SetParent(parachute, 0, false, false);
                chair.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                chair.UpdateNetworkGroup();

                chairMount = chair.GetComponent<BaseMountable>();
                if (chairMount == null) { OnDestroy(); return; }

                enabled = false;
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (!enabled) return;
                if ((1 << (collision.gameObject.layer & 31) & 1084293393) > 0)
                {
                    this.OnDestroy();
                }
            }

            public void SetPlayer(BasePlayer player)
            {
                this.player = player;
                chair.GetComponent<BaseMountable>().MountPlayer(player);
                enabled = true;
                SendInfoMessage(player, _instance.lang.GetMessage("usagemessage", _instance, player.IPlayer.Id), 12f);
            }

            private void SendInfoMessage(BasePlayer player, string message, float time)
            {
                player?.SendConsoleCommand("gametip.showgametip", message);
                _instance.timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }

            public void SetInput(InputState input)
            {
                this.input = input;
            }

            private void FixedUpdate()
            {
                if (!enabled) return;
                if (chair == null || player == null || forceDismount || !chairMount._mounted) { OnDestroy(); return; }

                var currentPlayerPos = player.transform.position;
                var currentPos = myRigidbody.transform.position;
                var getRotAngles = myRigidbody.transform.rotation.eulerAngles;

                #region Collision Checks

                if (player != null && player.IsHeadUnderwater()) { this.OnDestroy(); return; }

                #endregion

                #region Check Player Input

                if (input.WasJustPressed(BUTTON.JUMP))
                {
                    if (wantsDismount) { forceDismount = true; OnDestroy(); return; };
                    SendInfoMessage(player, _instance.lang.GetMessage("readytodismount", _instance, player.IPlayer.Id), 8f);
                    wantsDismount = true;
                }

                // Check player input and adjust rotation accordingly
                if (input.IsDown(BUTTON.FORWARD) || input.IsDown(BUTTON.BACKWARD))
                {
                    float direction = input.IsDown(BUTTON.FORWARD) ? 1f : -1f;
                    float newRotation = getRotAngles.x + direction;
                    bool isValidRotation = newRotation > 320f || newRotation < 40f;
                    float adjustment = isValidRotation ? newRotation : worldItem.transform.rotation.eulerAngles.x;
                    myRigidbody.transform.rotation = Quaternion.Euler(adjustment, myRigidbody.transform.rotation.eulerAngles.y, myRigidbody.transform.rotation.eulerAngles.z);
                }

                if (input.IsDown(BUTTON.RIGHT) || input.IsDown(BUTTON.LEFT))
                {
                    float direction = input.IsDown(BUTTON.RIGHT) ? 1f : -1f;
                    myRigidbody.AddTorque(Vector3.up * direction, ForceMode.Acceleration);
                }

                #endregion

                #region Rotation Angle Checks

                float deltaForce = (1f + fwdForce / 10f) * Time.deltaTime;

                // If facing down, speed up and less lift, else if back slow down and more lift if fast enough
                if (getRotAngles.x == 0f)
                {
                    //...
                }
                // if parachute is angled down in front, increase fwdForce and reduce upForce
                else if (getRotAngles.x > 0f && getRotAngles.x < 180f)
                {
                    fwdForce = Mathf.MoveTowards(fwdForce, config.chuteSettings.MaxParachuteFWDSpeed, deltaForce);
                    upForce = Mathf.MoveTowards(upForce, -20f, deltaForce);
                }
                else if (getRotAngles.x == 180f)
                {
                    //...
                }
                // if parachute is angled back
                else if (getRotAngles.x > 180f && getRotAngles.x <= 379f)
                {
                    // If leaning back and going slow, slow fwd speed and reduce lift
                    if (fwdForce > 7f)
                    {
                        fwdForce = Mathf.MoveTowards(fwdForce, -1f, 2f * Time.deltaTime);
                        upForce = Mathf.MoveTowards(upForce, 10f, 10f * Time.deltaTime);
                    }
                    else
                    {
                        fwdForce = Mathf.MoveTowards(fwdForce, -1f, 3f * Time.deltaTime);
                        upForce = Mathf.MoveTowards(upForce, -10f, 4f * Time.deltaTime);
                    }
                }

                #endregion

                #region Apply Forces

                // Apply forward force
                myRigidbody.AddForce(this.transform.forward * fwdForce, ForceMode.Acceleration);

                // Apply damping force if there is any velocity
                if (myRigidbody.velocity.magnitude != 0f)
                {
                    myRigidbody.AddForce(-myRigidbody.velocity.normalized * 5f, ForceMode.Acceleration);
                }

                // Apply upward impulse if downward velocity exceeds upward force
                if (myRigidbody.velocity.y < upForce)
                {
                    myRigidbody.AddForce(Vector3.up * (upForce - myRigidbody.velocity.y), ForceMode.Impulse);
                }

                #endregion

                #region Rotation Resistance

                //Rotation Reistance Force
                if (myRigidbody.angularVelocity.y != 0f)
                {
                    myRigidbody.AddTorque(new Vector3(0f, -myRigidbody.angularVelocity.y, 0f) * 1f, ForceMode.Acceleration);
                    myRigidbody.transform.rotation = Quaternion.Euler(myRigidbody.transform.rotation.eulerAngles.x, myRigidbody.transform.rotation.eulerAngles.y, -myRigidbody.angularVelocity.y * 50f);

                }

                #endregion

                worldItem.transform.hasChanged = true;
                worldItem.SendNetworkUpdateImmediate();
                worldItem.UpdateNetworkGroup();

                player.transform.hasChanged = true;
                player.SendNetworkUpdateImmediate(false);
                player.UpdateNetworkGroup();
            }

            public void Release()
            {
                enabled = false;
                if (chair != null && chair.GetComponent<BaseMountable>().IsMounted())
                    chair.GetComponent<BaseMountable>().DismountPlayer(player, false);
                if (player != null && player.isMounted)
                    player.DismountObject();

                if (!chair.IsDestroyed) chair.Kill();
                if (!parachute.IsDestroyed) parachute.Kill();
                if (!worldItem.IsDestroyed) worldItem.Kill();
                UnityEngine.GameObject.Destroy(this.gameObject);
            }

            public void OnDestroy()
            {
                player = null;
                Release();
                GameObject.Destroy(this);
            }
        }

        #endregion

        #region Chute Cooldown Timer

        public class CooldownTimer : MonoBehaviour
        {
            private BasePlayer player;
            public float cooldownTimer;
            public float waitTime;
            private bool enabled;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                if (player == null) { OnDestroy(); return; }

                cooldownTimer = 0f;
                waitTime = config.chuteSettings.ChuteCoolDown;
                enabled = false;
            }

            public void EnableTimer(bool isChuteUp)
            {
                if (!_instance.permission.UserHasPermission(player.UserIDString, permVIPCooldown))
                {
                    waitTime = config.chuteSettings.ChuteCoolDown;
                    if (isChuteUp) waitTime = config.chuteSettings.ChuteUpCoolDown;
                }
                else
                {
                    waitTime = config.chuteSettings.VIPChuteCoolDown;
                    if (isChuteUp) waitTime = config.chuteSettings.VIPChuteUpCoolDown;
                }
                enabled = true;
            }

            private void SendInfoMessage(BasePlayer player, string message, float time)
            {
                player?.SendConsoleCommand("gametip.showgametip", message);
                _instance.timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }

            private void FixedUpdate()
            {
                if (!enabled) return;
                if (cooldownTimer >= waitTime * 15f)
                {
                    coolDownList.Remove(player.userID);
                    SendInfoMessage(player, _instance.lang.GetMessage("cooldownremoved", _instance, player.IPlayer.Id), 4f);
                    OnDestroy();
                }
                cooldownTimer++;
            }

            public void OnDestroy()
            {
                GameObject.Destroy(this);
            }
        }

        #endregion
    }
}