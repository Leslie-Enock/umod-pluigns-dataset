using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Troll Cheaters", "x0x58x", "0.2.0")]
    [Description("Troll Cheaters with Op Guns and other troll commands")]
    class TrollCheaters : RustPlugin
    {
        #region Init
        ConfigData configData;
        private static string rocketGUI = @"
        [
          {
            ""name"": ""87cb-9615-bede"",
            ""parent"": ""Hud"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.RawImage"",
                ""color"": ""1 1 1 1"",
                ""url"": ""http://www.rigormortis.be/wp-content/uploads/rust-icon-512.png""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0.965"",
                ""anchormax"": ""0.02 1""
              }
            ]
          }
        ]
        ";
        private static string blueScreen = @"
        [
          {
            ""name"": ""6b26-3034-e391"",
            ""parent"": ""Overlay"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.RawImage"",
                ""color"": ""1 1 1 1"",
                ""url"": ""https://i0.wp.com/www.novabach.com/wp-content/uploads/2021/03/Blue-Screen-of-death.jpg?fit=1280%2C720&ssl=1""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          }
        ]
        ";
        void Init()
        {
            permission.RegisterPermission("trollcheaters.admin", this);
            permission.RegisterPermission("trollcheaters.rspeed", this);
            permission.RegisterPermission("trollcheaters.dropban", this);
            permission.RegisterPermission("trollcheaters.rdban", this);
            permission.RegisterPermission("trollcheaters.bscreen", this);
            LoadPlayerData();
        }

        #endregion

        #region Config      
        //Assistance Ujiou#6646
        protected override void LoadDefaultConfig() => configData = LoadBaseConfig();
        protected override void SaveConfig() => Config.WriteObject(configData, true);
        private string Lang(string langKey, BasePlayer player, params object[] args) => string.Format(lang.GetMessage(langKey, this, player.UserIDString), args);


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();

                if (configData == null)
                    throw new JsonException();
                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }
        private ConfigData LoadBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
                    status = false,
                    speed = 1000,
                },
                Version = Version
            };
        }
        class ConfigData
        {

            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Troll Cheaters Status")]
                public bool status { get; set; }
                [JsonProperty(PropertyName = "Rocket Speed")]
                public int speed { get; set; }
                [JsonProperty(PropertyName = "Player")]
                public object player { get; set; }
            }
            [JsonProperty(PropertyName = "Version: ")]
            public Core.VersionNumber Version { get; set; }
        }
        //Assistance Ujiou#6646

        private Dictionary<ulong, TCInfo> tcInfo = new Dictionary<ulong, TCInfo>();

        private void SavePlayerData()
        {
            if (tcInfo != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/TCInfo", tcInfo);
        }

        private void LoadPlayerData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/TCInfo"))
            {
                tcInfo = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, TCInfo>>($"{Name}/TCInfo");
            }
            else
            {
                tcInfo = new Dictionary<ulong, TCInfo>();
                SavePlayerData();
            }
        }

        public class TCInfo
        {
            [JsonProperty(PropertyName = "Name: ")]
            public string Name { get; set; }

        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Perms"] = "No perms",
                ["status"] = "Troll Cheaters Status: {0}",
                ["validNum"] = "Please enter a valid number",
                ["validPrefab"] = "Dang something changed or wrong prefab??",
                ["dExist"] = "Whatever you're targeting doesn't exist.",
                ["cheater"] = "This cheater is already on the list >:(",
                ["saveCheater"] = "Saving user data to the file...",
                ["notCheater"] = "This is not a cheater.",
                ["removeCheater"] = "Deleting user data from the file...",
                ["rocketSpeed"] = "Rocket Speed set to {0}",
                ["noUser"] = "No user found please look at the user with your crosshair",
            }, this);
        }
        #endregion

        #region Commands

        #region ChatCommand trocket
        [ChatCommand("trocket")]
        void trocket(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.admin"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }
            else
            {
                configData.settings.status = !configData.settings.status;
                string status = Lang("status", player, configData.settings.status);

                SendReply(player, status);
                if (configData.settings.status)
                {
                    CuiHelper.AddUi(player, rocketGUI);
                }
                else
                {
                    CuiHelper.DestroyUi(player, "87cb-9615-bede");
                }
            }
        }

        #endregion

        #region ChatCommand rspeed
        [ChatCommand("rspeed")]
        void rspeed(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.rspeed"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }

            if (args.Length > 0)
            {
                int a;
                bool res;
                res = int.TryParse(args[0], out a);
                if (res && a > 0)
                {
                    configData.settings.speed = a;
                    SaveConfig();
                    SendReply(player, Lang("rocketSpeed", player, configData.settings.speed));
                }
                else
                {
                    string valid = lang.GetMessage("validNum", this, player.UserIDString);
                    SendReply(player, valid);
                    return;


                }
            }
            else
            {
                string valid = lang.GetMessage("validNum", this, player.UserIDString);
                SendReply(player, valid);
                return;
            }

        }
        #endregion

        #region ChatCommand dropban
        [ChatCommand("dropban")]
        void dropban(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.dropban"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }
            else
            {

                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, float.MaxValue, LayerMask.GetMask("Player (Server)")))
                {
                    string noUser = lang.GetMessage("noUser", this, player.UserIDString);
                    player.ChatMessage(noUser);
                    return;
                }
                else
                {
                    var entity = hit.GetEntity() as BasePlayer;
                    if (entity == null)
                    {
                        string dExist = lang.GetMessage("dExist", this, player.UserIDString);
                        player.ChatMessage(dExist);
                        return;

                    }
                    else
                    {
                        string cheater = lang.GetMessage("cheater", this, player.UserIDString);
                        string saveCheater = lang.GetMessage("saveCheater", this, player.UserIDString);
                        if (!tcInfo.ContainsKey(entity.userID))
                        {
                            tcInfo.Add(entity.userID, new TCInfo() { Name = entity.displayName });
                            SavePlayerData();
                            player.ChatMessage(saveCheater);
                            ripCheater(entity);
                        }
                        else
                        {
                            player.ChatMessage(cheater);
                            return;
                        }
                    }
                }
            }
        }
        #endregion

        #region ChatCommand rdban
        [ChatCommand("rdban")]
        void rdban(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.rdban"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }
            else
            {

                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, float.MaxValue, LayerMask.GetMask("Player (Server)")))
                {
                    string noUser = lang.GetMessage("noUser", this, player.UserIDString);
                    player.ChatMessage(noUser);
                    return;
                }
                else
                {
                    var entity = hit.GetEntity() as BasePlayer;
                    if (entity == null)
                    {
                        string dExist = lang.GetMessage("dExist", this, player.UserIDString);
                        player.ChatMessage(dExist);
                        return;
                    }
                    else
                    {
                        string notCheater = lang.GetMessage("notCheater", this, player.UserIDString);
                        string removeCheater = lang.GetMessage("removeCheater", this, player.UserIDString);
                        if (!tcInfo.ContainsKey(entity.userID))
                        {
                            player.ChatMessage(notCheater);
                            return;
                        }
                        else
                        {
                            tcInfo.Remove(entity.userID);
                            SavePlayerData();
                            player.ChatMessage(removeCheater);
                        }
                    }
                }
            }
        }
        #endregion

        #region Blue Screen
        [ChatCommand("bscreen")]
        void bscreen(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.bscreen"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, float.MaxValue, LayerMask.GetMask("Player (Server)")))
            {
                string noUser = lang.GetMessage("noUser", this, player.UserIDString);
                player.ChatMessage(noUser);
                return;
            }
            var entity = hit.GetEntity() as BasePlayer;
            if (entity == null)
            {
                string dExist = lang.GetMessage("dExist", this, player.UserIDString);
                player.ChatMessage(dExist);
                return;

            }
            else
            {
                CuiHelper.AddUi(entity, blueScreen);
                timer.Repeat(0.2f, 100, () =>
                {
                    RunEffect(entity.transform.position, "assets/bundled/prefabs/fx/headshot.prefab", entity);
                });
            }            
        }

        [ChatCommand("rbscreen")]
        void rbscreen(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "trollcheaters.bscreen"))
            {
                string perms = lang.GetMessage("Perms", this, player.UserIDString);
                SendReply(player, perms);
                return;
            }
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, float.MaxValue, LayerMask.GetMask("Player (Server)")))
            {
                string noUser = lang.GetMessage("noUser", this, player.UserIDString);
                player.ChatMessage(noUser);
                return;
            }
            var entity = hit.GetEntity() as BasePlayer;
            if (entity == null)
            {
                string dExist = lang.GetMessage("dExist", this, player.UserIDString);
                player.ChatMessage(dExist);
                return;

            }
            else
            {
                CuiHelper.DestroyUi(entity, "6b26-3034-e391");
            }
        }
        #endregion

        #endregion

        #region Hooks
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "trollcheaters.admin") && configData.settings.status)
            {
                var prop = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab",
                    player.eyes.position + player.eyes.BodyForward().normalized,
                    player.eyes.rotation
                    );

                ServerProjectile sProjectile = prop.GetComponent<ServerProjectile>();
                if (sProjectile == null)
                {
                    string validPrefab = lang.GetMessage("validPrefab", this, player.UserIDString);
                    PrintWarning(validPrefab);
                    return;
                }
                else
                {
                    sProjectile.InitializeVelocity(player.eyes.HeadForward().normalized * configData.settings.speed);
                    sProjectile.gravityModifier = 0;
                    prop.Spawn();
                }

            }
        }
        object OnItemPickup(BasePlayer player)
        {
            if (tcInfo.ContainsKey(player.userID))
            {
                ripCheater(player);
            }
            return null;
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (configData.settings.status)
            {
                CuiHelper.AddUi(player, rocketGUI);
            }
            else
            {
                CuiHelper.DestroyUi(player, "87cb-9615-bede");
            }
        }
        object OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
        {
            if (tcInfo.ContainsKey(player.userID))
            {
                ripCheater(player);
            }
            return null;
        }
        object OnActiveItemChange(BasePlayer player, Item oldItem, uint newItemId)
        {
            if (tcInfo.ContainsKey(player.userID))
            {
                ripCheater(player);
            }
            return null;
        }
        void Loaded(BasePlayer player)
        {
            if (configData.settings.status)
            {
                CuiHelper.AddUi(player, rocketGUI);
            }
            else
            {
                CuiHelper.DestroyUi(player, "87cb-9615-bede");
            }
        }
        void ripCheater(BasePlayer player)
        {
            DropUtil.DropItems(player.inventory.containerMain, player.transform.position);
            DropUtil.DropItems(player.inventory.containerBelt, player.transform.position);
            DropUtil.DropItems(player.inventory.containerWear, player.transform.position);
        }
        void RunEffect(Vector3 position, string prefab, BasePlayer player = null)
        {
            var effect = new Effect();
            effect.Init(Effect.Type.Generic, position, Vector3.zero);
            effect.pooledString = prefab;

            if (player != null)
            {
                EffectNetwork.Send(effect, player.net.connection);
            }
            else
            {
                EffectNetwork.Send(effect);
            }
        }
        #endregion
    }
}