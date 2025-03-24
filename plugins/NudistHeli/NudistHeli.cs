using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Nudist Heli", "Panduck", "0.1.1")]
    [Description("Configurable helicopter engagement behaviour.")]
    public class NudistHeli : RustPlugin
    {

        #region Fields

        private NudistHeliSettings _settings;
        
        private Dictionary<BasePlayer, double> _hostilePlayers;
        private static double CurrentTime => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        #endregion

        #region Config
        
        private static NudistHeliSettings GetDefaultConfig()
        {
            return new NudistHeliSettings()
            {
                MaxClothingCount = 3,
                HostileTime = 6f,
                OnlyEngageOnWeaponHeld = false,
                RestrictedWeapons = new HashSet<string>()
                {
                    "rifle.ak",
                    "rifle.bolt",
                    "smg.2",
                    "shotgun.double",
                    "rifle.l96",
                    "rifle.lr300",
                    "lmg.m249",
                    "rifle.m39",
                    "pistol.m92",
                    "smg.mp5",
                    "shotgun.pump",
                    "pistol.python",
                    "pistol.revolver",
                    "rocket.launcher",
                    "pistol.semiauto",
                    "rifle.semiauto",
                    "shotgun.spas12",
                    "smg.thompson"
                }
            };
        }
        
        public class NudistHeliSettings
        {
            public int MaxClothingCount { get; set; }
            public float HostileTime { get; set; }
            public HashSet<string> RestrictedWeapons { get; set; }
            public bool OnlyEngageOnWeaponHeld { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            _settings = Config.ReadObject<NudistHeliSettings>();
            _hostilePlayers = new Dictionary<BasePlayer, double>();
        }
        
        private void OnEntityKill(BaseNetworkable entity)
        {
            if(entity is BaseHelicopter)
            {
                ClearHostiles();
            }
        }
        
        private bool CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            return player.IsAlive() && !player.IsNpc && IsHostile(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            RemoveHostile(player);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            player.cachedThreatLevel = 0f;
            RemoveHostile(player);
        }

        private object OnThreatLevelUpdate(BasePlayer player)
        {
            if (_settings.OnlyEngageOnWeaponHeld)
            {
                foreach (Item item in player.inventory.containerBelt.itemList)
                {
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (!_settings.RestrictedWeapons.Contains(item.info.shortname)) continue;
                    if (heldEntity == null || !(heldEntity is BaseProjectile) || heldEntity is BowWeapon) continue;
                    player.cachedThreatLevel += 2f;
                    SetHostile(player, _settings.HostileTime);
                    break;
                }
            }
            else
            {
                if (player.inventory.containerWear.itemList.Count >= _settings.MaxClothingCount)
                {
                    player.cachedThreatLevel += 2f;
                    SetHostile(player, _settings.HostileTime);
                }

                foreach (Item item in player.inventory.containerBelt.itemList)
                {
                    BaseEntity heldEntity = item.GetHeldEntity();
                    if (!_settings.RestrictedWeapons.Contains(item.info.shortname)) continue;
                    if (heldEntity == null || !(heldEntity is BaseProjectile) || heldEntity is BowWeapon) continue;
                    player.cachedThreatLevel += 2f;
                    SetHostile(player, _settings.HostileTime);
                    break;
                }
            }

            return true;
        }

        #endregion

        #region Helpers

        private bool IsHostile(BasePlayer player)
        {
            if (!_hostilePlayers.ContainsKey(player))
            {
                return false;
            }

            return _hostilePlayers[player] - CurrentTime > 0;
        }

        private void SetHostile(BasePlayer player, float duration)
        {
            if (!_hostilePlayers.ContainsKey(player))
            {
                _hostilePlayers.Add(player, 0);
            }

            _hostilePlayers[player] = CurrentTime + duration;
        }

        private void RemoveHostile(BasePlayer player)
        {
            if (_hostilePlayers.ContainsKey(player))
            {
                _hostilePlayers.Remove(player);
            }
        }
        
        private void ClearHostiles()
        {
            _hostilePlayers.Clear();
        }

        #endregion

    }
}