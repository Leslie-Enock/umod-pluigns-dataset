using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("TurretInfo", "ninco90", "1.2.0", ResourceId = 2678)]
    class TurretInfo : RustPlugin
    {
        #region Fields
        private DynamicConfigFile restorationdata;
        private StoredData storedData;
        #endregion

        #region Oxide Hooks       
        void Init(){
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);
            lang.RegisterMessages(MessagesEN, this);
            lang.RegisterMessages(MessagesES, this, "es");
            permission.RegisterPermission("turretinfo.use", this);
            permission.RegisterPermission("turretinfo.notify", this);
            permission.RegisterPermission("turretinfo.admin", this); 
        }
        #endregion

        #region StoredData
        class StoredData{
            public Dictionary<string, List<string>> Kills = new Dictionary<string, List<string>>();
            public StoredData() { }
        }

        class SpawnInfo {
            public SpawnInfo() { }
            public string UserId;
        }

        void PlayerKillsAdd(string steamid, string timeleft){
            if (!storedData.Kills.ContainsKey(timeleft))
                storedData.Kills.Add(timeleft, new List<string>());
            storedData.Kills[timeleft].Add(steamid);
            Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
        }
        #endregion

        #region Functions
        private BaseEntity FindEntity(BasePlayer player){
            var currentRot = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            Vector3 eyesAdjust = new Vector3(0f, 1.5f, 0f);
            var rayResult = Ray(player.transform.position + eyesAdjust, currentRot);
            if (rayResult is BaseEntity){
                var target = rayResult as BaseEntity;
                return target;
            }
            return null;
        }

        private object Ray(Vector3 Pos, Vector3 Aim){
            var hits = Physics.RaycastAll(Pos, Aim);
            float distance = 100f;
            object target = null;
            foreach (var hit in hits){
                if (hit.collider.GetComponentInParent<BaseEntity>() != null){
                    if (hit.distance < distance){
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BaseEntity>();
                    }
                }
            }
            return target;
        }

        private bool IsNPC(BasePlayer player){
            if (player == null) return false;
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
			
            return false;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info){
            if (entity == null) return;
            if (info == null) return;
            if (info.Initiator is AutoTurret && entity is BasePlayer){
                BasePlayer target = info.HitEntity.ToPlayer();
				if (IsNPC(target)) return;
                BasePlayer owner = BasePlayer.FindByID(info.Initiator.OwnerID);
                if (config.GS_LOG){
                    Puts(String.Format(GetMessage("killplayer"), target.displayName, owner.displayName));
                }
                if (permission.UserHasPermission(owner.UserIDString, "turretinfo.notify")){
                    if(config.GS_CHAT_GLOBAL){
                        PrintToChat(GetMessage("prefix") + String.Format(GetMessage("killplayer"), target.displayName, owner.displayName));
                    } else if(config.GS_CHAT_PRIVATE){
                        SendReply(owner, GetMessage("prefix") + String.Format(GetMessage("killplayer"), target.displayName, owner.displayName));
                    }
                }
                PlayerKillsAdd(target.userID.ToString(), info.Initiator.net.ID.ToString());
            } else {
                if (entity.ShortPrefabName == "autoturret_deployed"){
                    BasePlayer owner = BasePlayer.FindByID(entity.OwnerID);
                    if (info.Initiator as BasePlayer){
                        //Puts("Un jugador destruye la torreta");
                        BasePlayer players = BasePlayer.FindByID(info.Initiator.ToPlayer().userID);
                        if (config.GS_LOG){
                            Puts(String.Format(GetMessage("killturretplayer"), players.displayName, owner.displayName));
                        }
                        if (permission.UserHasPermission(owner.UserIDString, "turretinfo.notify")){
                            if(config.GS_CHAT_GLOBAL){
                                PrintToChat(GetMessage("prefix") + String.Format(GetMessage("killturretplayer"), players.displayName, owner.displayName));
                            } else if(config.GS_CHAT_PRIVATE){
                                SendReply(owner, GetMessage("prefix") + String.Format(GetMessage("killturretplayer"), players.displayName, owner.displayName));
                            }
                        }
                    } else {
                        if (config.GS_LOG){
                            Puts(String.Format(GetMessage("killturret"), owner.displayName));
                        }
                        if (permission.UserHasPermission(owner.UserIDString, "turretinfo.notify")){
                            if(config.GS_CHAT_GLOBAL){
                                PrintToChat(GetMessage("prefix") + String.Format(GetMessage("killturret"), owner.displayName));
                            } else if(config.GS_CHAT_PRIVATE){
                                SendReply(owner, GetMessage("prefix") + String.Format(GetMessage("killturret"), owner.displayName));
                            }
                        }
                    }
                    if (config.GS_GUI && permission.UserHasPermission(owner.UserIDString, "turretinfo.notify")){
                        CuiTurretDestroy(owner);
                        Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", owner.transform.position);
                        timer.Once(6, () =>{
                            CuiHelper.DestroyUi(owner, "BlockMsg");
                        });
                    }
                }
            }
        }

        private BaseEntity GetViewEntity(BasePlayer player){
            RaycastHit hit;
            var didHit = Physics.Raycast(player.eyes.HeadRay(), out hit, 5);
            return didHit ? hit.GetEntity() : null;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("turret")]
        void cmdAuth(BasePlayer player, string command, string[] args){
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "turretinfo.use")){
                if (args.Length == 0){
                    SendReply(player, msg("prefixbig", player.UserIDString) + String.Format(GetMessage("help"), Version.ToString()));
                } else {
                    var entity = FindEntity(player);
                    if (entity == null || (!entity.GetComponent<AutoTurret>())){
                        SendReply(player, msg("noturret", player.UserIDString));
                        return;
                    }

                    if (entity.OwnerID != player.userID){
                        if (config.GS_TEAM && entity.OwnerID > 0 && player.currentTeam > 0){
                            if (player.Team == null && !player.Team.members.Contains(entity.OwnerID)){
                                return;
                            }
                        } else if (!permission.UserHasPermission(player.UserIDString, "turretinfo.admin")){
                            SendReply(player, msg("noadmin", player.UserIDString));
                            return;
                        }
                    }

                    switch (args[0].ToLower()){
                        case "auth":
                            if (config.GS_EFFECT){
                                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", new Vector3(entity.transform.position.x, entity.transform.position.y + 0.8f, entity.transform.position.z));
                            }
                            SendReply(player, msg("prefixbig", player.UserIDString) + msg("autorizados", player.UserIDString));
                            int list = 1;
                            foreach (ProtoBuf.PlayerNameID pnid in entity.GetComponent<AutoTurret>().authorizedPlayers){
                                SendReply(player, "#" + list + " - " + pnid.username.ToString() + " (SteamID: " + pnid.userid.ToString() + ")");
                                list++;
                            }
                            return;
                        case "kills":
                            if (config.GS_EFFECT){
                                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", new Vector3(entity.transform.position.x, entity.transform.position.y + 0.8f, entity.transform.position.z));
                            }
                            SendReply(player, msg("prefixbig", player.UserIDString) + msg("kills", player.UserIDString));
                            if (storedData.Kills.ContainsKey(entity.net.ID.ToString())){
                                int i = 1;
                                foreach (var point in storedData.Kills[entity.net.ID.ToString()]){
                                    var targetPlayer = BasePlayer.Find(storedData.Kills[entity.net.ID.ToString()][i - 1]);
                                    SendReply(player, "#" + i + " - " + targetPlayer.displayName + " (SteamID: " + storedData.Kills[entity.net.ID.ToString()][i - 1] + ")");
                                    i++;
                                }
                            } else {
                                SendReply(player, msg("nokills", player.UserIDString));
                            }
                            return;
                        case "clear":
                            if (config.GS_EFFECT){
                                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", new Vector3(entity.transform.position.x, entity.transform.position.y + 0.8f, entity.transform.position.z));
                            }
                            SendReply(player, msg("prefixbig", player.UserIDString) + msg("clear", player.UserIDString));
                            storedData.Kills.Remove(entity.net.ID.ToString());
                            return;
                        case "clearall":
                            if (permission.UserHasPermission(player.UserIDString, "turretinfo.admin")){
                                if (config.GS_EFFECT){
                                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", new Vector3(entity.transform.position.x, entity.transform.position.y + 0.8f, entity.transform.position.z));
                                }
                                storedData.Kills.Clear();
                                SendReply(player, msg("prefixbig", player.UserIDString) + msg("clearall", player.UserIDString));
                            } else {
                                SendReply(player, msg("noadmin", player.UserIDString));
                            }
                            return;
                        default:
                            break;
                    }
                }
            }          
        }
        #endregion

        #region GUI
        private void CuiTurretDestroy(BasePlayer player){
            CuiHelper.DestroyUi(player, "BlockMsg");

            var elements = new CuiElementContainer();
            var BlockMsg = elements.Add(new CuiPanel {
                Image = {
                    Color = "0.80 0.10 0.10 0.60"
                },
                RectTransform = {
                    AnchorMin = "0.41 0.12",
                    AnchorMax = "0.57 0.17"
                }
            }, "Hud", "BlockMsg");
            elements.Add(new CuiElement {
                Parent = BlockMsg,
                Components = {
                        new CuiRawImageComponent {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Url = "https://image.prntscr.com/image/Euu4ok39Qo_IBrAEmbSePQ.png",
                            Color = "0 0 0 0.70"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 0",
                            AnchorMax = "0.997 0.98"
                        }
                    }
            });
            elements.Add(new CuiElement {
                Parent = BlockMsg,
                Components = {
                        new CuiRawImageComponent {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Url = "https://vignette.wikia.nocookie.net/play-rust/images/f/f9/Auto_Turret_icon.png/revision/latest?cb=20151106062203",
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.03 0.05",
                            AnchorMax = "0.25 1.5"
                        }
                    }
            });
            elements.Add(new CuiLabel {
                RectTransform = {
                        AnchorMin = "0.28 0",
                        AnchorMax = "0.84 1"
                    },
                Text = {
                        Text = "TURRET DESTROYED",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                    }
            }, BlockMsg);
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Config
        private static ConfigData config;

        private class ConfigData{
            [JsonProperty(PropertyName = "CHAT GLOBAL | Show when someone dies or someone destroys the turret in Global Chat. [true,false]")]
            public bool GS_CHAT_GLOBAL;
            [JsonProperty(PropertyName = "CHAT PRIVATE | Only show the owner when someone dies by the turret or destroys it. [true,false]")]
            public bool GS_CHAT_PRIVATE;
            [JsonProperty(PropertyName = "GUI | Display GUI Turret Destroy [true,false]")]
            public bool GS_GUI;
            [JsonProperty(PropertyName = "EFFECT | Show effect when using [true,false]")]
            public bool GS_EFFECT;
            [JsonProperty(PropertyName = "LOG | Show Show log in console [true,false]")]
            public bool GS_LOG;
            [JsonProperty(PropertyName = "TEAM SUPPORT | Allows teammates to use commands [true,false]")]
            public bool GS_TEAM;
        }

        private ConfigData GetDefaultConfig() {
            return new ConfigData {
                GS_CHAT_GLOBAL = true,
                GS_CHAT_PRIVATE = true,
                GS_GUI = true,
                GS_EFFECT = true,
                GS_LOG = true,
                GS_TEAM = true,
            };
        }

        protected override void LoadConfig(){
            base.LoadConfig();
            try {
                config = Config.ReadObject<ConfigData>();
                if (config == null){
                    LoadDefaultConfig();
                }
            } catch {
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            config = GetDefaultConfig();
        }

        protected override void SaveConfig(){
            Config.WriteObject(config);
        }
        #endregion

        #region Messaging
        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        string msg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        Dictionary<string, string> MessagesEN = new Dictionary<string, string> {
            {"prefix", "<color=#00b8ff>TurretInfo: </color>"},
            {"prefixbig", "<size=24><color=#00b8ff>TurretInfo</color></size>\n"},
            {"noturret", "You need to look at an Autoturret."},
            {"noadmin", "You can not consult something that does not belong to you."},
            {"autorizados", "<size=16>List of authorized players in this turret.</size>"},
            {"kills", "<size=16>List of players killed by this turret.</size>"},
            {"nokills", "Currently the turret has not killed any player."},
            {"clear", "<size=16>The list of players killed by this turret has been cleared.</size>"},
            {"clearall", "<size=16>The list of players killed by all the turrets has been cleared.</size>"},
            {"killplayer", "<color=#fb4c4c>{0}</color> has died due to an AutoTurret placed by <color=#3cb958>{1}</color>."},
            {"killturret", "Someone has destroyed an AutoTurret placed by <color=#3cb958>{0}</color>."},
            {"killturretplayer", "<color=#fb4c4c>{0}</color> has destroyed an AutoTurret placed by <color=#3cb958>{1}</color>."},
            {"help", "<size=16>Developed by: ninco90 | v{0}</size>\n" +
                "Look at an AutoTurret and present the following commands:\n" +
                "<color=#fb4c4c>/turret auth</color> - Check the authorized players.\n" +
                "<color=#fb4c4c>/turret kills</color> - Check the players killed.\n" +
                "<color=#fb4c4c>/turret clear</color> - Clean the list of players killed.\n" +
                "<color=#fb4c4c>/turret clearall</color> - Clean all lists (Admin only)."}
        };
        Dictionary<string, string> MessagesES = new Dictionary<string, string> {
            {"prefix", "<color=#00b8ff>TurretInfo: </color>"},
            {"prefixbig", "<size=24><color=#00b8ff>TurretInfo</color></size>\n"},
            {"noturret", "Necesita mirar una Autoturret."},
            {"noadmin", "No puedes consultar algo que no te pertenece."},
            {"autorizados", "<size=16>Lista de los jugadores autorizados en esta torreta.</size>"},
            {"kills", "<size=16>Lista de los jugadores matados por esta torreta.</size>"},
            {"nokills", "Actualmente la torreta no ha matado a ningún jugador."},
            {"clear", "<size=16>La lista de los jugadores matados por esta torreta se ha limpiado.</size>"},
            {"clearall", "<size=16>La lista de los jugadores matados por todas las torretas se ha limpiado.</size>"},
            {"killplayer", "<color=#fb4c4c>{0}</color> ha muerto por culpa de una Torreta Automática colocada por <color=#3cb958>{1}</color>."},
            {"killturret", "Alguien ha destruido una Torreta Automática colocada por <color=#3cb958>{0}</color>."},
            {"killturretplayer", "<color=#fb4c4c>{0}</color> ha destruido una Torreta Automática colocada por <color=#3cb958>{1}</color>."},
            {"help", "<size=16>Desarrollado por: ninco90 | v{0}</size>\n" +
                "Mira una AutoTurret y introduce los siguientes comandos:\n" +
                "<color=#fb4c4c>/turret auth</color> - Comprueba los jugadores autorizados.\n" +
                "<color=#fb4c4c>/turret kills</color> - Comprueba los jugadores asesinados.\n" +
                "<color=#fb4c4c>/turret clear</color> - Limpia la lista de jugadores asesinados.\n" +
                "<color=#fb4c4c>/turret clearall</color> - Limpia todas las listas (Sólo Admin)."}
        };
        #endregion
    }
}