using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Rocket Guns", "Sapnu Puas #3696", "1.2.4")]
    [Description("Allow 556 guns to fire rockets based on ammo type")]
    public class RocketGuns : RustPlugin
    {
        List<ulong> ActiveGUNS = new List<ulong>();
        private const string PermUse = "rocketguns.use";
        #region[Rocket prefabs]
        public string Rocket = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        public string Hv = "assets/prefabs/ammo/rocket/rocket_hv.prefab";
        public string Fire = "assets/prefabs/ammo/rocket/rocket_fire.prefab";
        // public string Mrls = "assets/content/vehicles/mlrs/rocket_mlrs.prefab";
        #endregion[Rocket prefabs]

        #region GUI
        void DestroyCUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "GunUI");
        }
        public void CreateAmmoIcon(BasePlayer player, int ammo)
        {
            DestroyCUI(player);
            CuiElementContainer elements = new CuiElementContainer();
            string panel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0.5 0.5 0.0" },
                RectTransform = { AnchorMin = config.ImageAnchorMin, AnchorMax = config.ImageAnchorMax }
            }, "Hud.Menu", "GunUI");
            elements.Add(new CuiElement
            {
                Parent = panel,
                Components =
                {
                    new CuiImageComponent { ItemId = ammo },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            CuiHelper.AddUi(player, elements);
        }
        #endregion GUI

        #region[Commands]
        private void ToggleRocketCMD(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
                return;

            BasePlayer basePlayer = player.Object as BasePlayer;
            if(basePlayer != null)
            {
                if (!permission.UserHasPermission(basePlayer.userID.ToString(), PermUse))
                {
                    SendReply(basePlayer, lang.GetMessage("NoPerm", this, basePlayer.userID.ToString()));
                    return;
                }
                if (ActiveGUNS.Contains(basePlayer.userID))
                {
                    SendReply(basePlayer, lang.GetMessage("Off", this, basePlayer.userID.ToString()));
                    ActiveGUNS.Remove(basePlayer.userID);
                    DestroyCUI(basePlayer);
                    return;
                }
                else
                {
                    SendReply(basePlayer, lang.GetMessage("On", this, basePlayer.userID.ToString()));
                    ActiveGUNS.Add(basePlayer.userID);
                    UpdateIcon(basePlayer);
                    return;
                }
            }


           

        }
        #endregion[Commands]

        #region[Hooks]

        #region[load/unload]
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyCUI(player);
            }
            ActiveGUNS = null;
        }
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
            AddCovalenceCommand(config.Command, nameof(ToggleRocketCMD));
        }
        #endregion[load/unload]

        #region[gui Hooks]
        void OnPlayerDisconnected(BasePlayer player) => DestroyCUI(player);
        void OnPlayerDeath(BasePlayer player, HitInfo info) => DestroyCUI(player);
        object OnWeaponReload(BaseProjectile weapon, BasePlayer player)
        {
            if (player != null && player.userID.IsSteamId())
            {
                if (ActiveGUNS.Contains(player.userID))
                {
                   

                   
                    timer.Once(weapon.reloadTime + 0.2f, () =>
                    {
                        
                        UpdateIcon(player);
                    });

                   
                }
            }
            return null;
        }
        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
           DestroyCUI(player);
            var heldEntity = player.GetActiveItem();
            if (heldEntity != null)
            {
                var weapon = heldEntity.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (ActiveGUNS.Contains(player.userID))
                    {
                        NextFrame(() =>
                        {
                            UpdateIcon(player);
                        });

                    }
                }
            }
        }
        void OnAmmoUnload(BaseProjectile weapon, Item item, BasePlayer player)
        {

            if (ActiveGUNS.Contains(player.userID))
            {
                NextFrame(() =>
                {
                    UpdateIcon(player);
                });

            }
            return;
        }
        #endregion[gui hooks]

        void OnWeaponFired(BaseProjectile weapon, BasePlayer player, ItemModProjectile ammo, ProtoBuf.ProjectileShoot projectiles)
        {
            if (player == null) return;
            if (ActiveGUNS.Contains(player.userID) && ammo.ammoType == Rust.AmmoTypes.RIFLE_556MM)
            {
                string rocket = string.Empty;

                switch (ammo.name)
                {
                    case "ammo_rifle.item":
                        rocket = RocketToFire(config.Normal);
                        break;

                    case "ammo_rifle_hv.item":
                        rocket = RocketToFire(config.Hv);
                        break;

                    case "ammo_rifle_fire.item":
                        rocket = RocketToFire(config.Fire);
                        break;

                    case "ammo_rifle_explosive.item":
                        rocket = RocketToFire(config.Explo);
                        break;

                    default:
                        break;
                }

                if (string.IsNullOrEmpty(rocket))
                {
                    return;
                }
                FireRockets(player, rocket);
            }
            else return;
        }

        #endregion[Hooks]

        #region[Methods]
        public string RocketToFire(int id)
        {
            switch (id)
            {
                case 1:
                    return Rocket;

                case 2:
                    return Hv;

                case 3:
                    return Fire;

                default:
                    return string.Empty;
            }

        }
        public int RocketIcon(int id)
        {
            switch (id)
            {
                case 1:
                    return -742865266;

                case 2:
                    return -1841918730;

                case 3:
                    return 1638322904;

                default:
                    return 0;
            }
        }
        public void UpdateIcon(BasePlayer player)
        {
            DestroyCUI(player);
            var heldEntity = player.GetActiveItem();
            if (heldEntity != null)
            {
                var weapon = heldEntity.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    var ammodef = weapon.primaryMagazine.ammoType;
                    ItemDefinition itemDefinition = ItemManager.FindItemDefinition(ammodef.itemid);
                    ItemModProjectile ammo = itemDefinition.GetComponent<ItemModProjectile>();
                    if (ammo.ammoType == Rust.AmmoTypes.RIFLE_556MM)
                    {
                        int rocketicon = 0;
                        var ammount = weapon.primaryMagazine.contents;

                        if (ammount < 1) return;
                        

                        var ammotype = weapon.primaryMagazine.ammoType;

                        switch (ammo.name)
                        {
                            case "ammo_rifle.item":
                                rocketicon = RocketIcon(config.Normal);
                                break;

                            case "ammo_rifle_hv.item":
                                rocketicon = RocketIcon(config.Hv);
                                break;

                            case "ammo_rifle_fire.item":
                                rocketicon = RocketIcon(config.Fire);
                                break;

                            case "ammo_rifle_explosive.item":
                                rocketicon = RocketIcon(config.Explo);
                                break;

                            default:
                                break;
                        }

                        if (rocketicon == 0)
                        {
                            rocketicon = ammotype.itemid;
                        }

                        CreateAmmoIcon(player, rocketicon);
                    }
                }
            }

        }
        public void FireRockets(BasePlayer player, string rocketPrefab)
        {
            if (player == null) return;
            var rocket = GameManager.server.CreateEntity(rocketPrefab, player.eyes.position, new Quaternion());
            if (rocket != null)
            {
                if(rocketPrefab != null)
                {
                    float speed = 0;

                    switch (rocketPrefab)
                    {
                        case "assets/prefabs/ammo/rocket/rocket_basic.prefab":
                            speed = config.NormalSpeed;
                            break;

                        case "assets/prefabs/ammo/rocket/rocket_hv.prefab":
                            speed = config.HvSpeed;
                            break;

                        case "assets/prefabs/ammo/rocket/rocket_fire.prefab":
                            speed = config.FireSpeed;
                            break;

                        default:
                            break;
                    }
                    rocket.creatorEntity = player;
                    rocket.SendMessage("InitializeVelocity", player.eyes.HeadForward() * speed);
                    rocket.OwnerID = player.userID;
                    rocket.Spawn();
                    rocket.ClientRPC(null, "RPCFire");


                    Interface.CallHook("OnRocketLaunched", player,rocket);


                }
                
            }
        }

        
        #endregion[Methods]

        #region Config
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Toggle command")]
            public string Command = "togglegun";

            [JsonProperty("1 = normal rocket, 2 = hv rocket , 3 = incendiary rocket , 0 = none")]
            public int notused = 0;

            [JsonProperty("rocket type to fire when using normall 5.56")]
            public int Normal = 1;

            [JsonProperty("rocket type to fire when using hv 5.56")]
            public int Hv = 2;

            [JsonProperty("rocket type to fire when using Incendiary 5.56")]
            public int Fire = 3;

            [JsonProperty("rocket type to fire when using explosive 5.56")]
            public int Explo = 0;


            //[JsonProperty("Allow Incendiary rockets (may cause server lag if too many fired)")]
           // public bool UseFire = false;

            [JsonProperty("Hv rocket speed ")]
            public float HvSpeed = 200;

            [JsonProperty("Normal rocket speed")]
            public float NormalSpeed = 100;

            [JsonProperty("Incendiary rocket speed")]
            public float FireSpeed = 100;

            [JsonProperty("Image AnchorMin")]
            public string ImageAnchorMin = "0.645 0.023";

            [JsonProperty("Image AnchorMax")]
            public string ImageAnchorMax = "0.688 0.095";

        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region[Localization]
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "You dont have permission to use this command",
                ["Off"] = "Raid gun disengaged",
                ["On"] = "Raid gun engaged"
            }, this);
        }
        #endregion[Localization]
    }
}
