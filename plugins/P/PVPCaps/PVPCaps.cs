using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Oxide.Core;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PVP Caps", "WhiteDragon", "1.1.5")]
    [Description("Applies a PvP damage handicap to players, based on PvP kills/deaths.")]
    internal class PVPCaps : RustPlugin
    {
        #region _fields_

        internal static PVPCaps instance;

        // configuration: change flag
        private static bool config_changed = false;

        // configuration: data
        private static Configuration config;

        // attack info
        private static Dictionary<ulong, List<AttackInfo>> attacks = new Dictionary<ulong, List<AttackInfo>>();

        // online team member counts
        private static Dictionary<ulong, ulong> teams = new Dictionary<ulong, ulong>();

        // plugin database
        private static DB db;

        // oxide permissions
        private const string PERMISSIONADMIN    = "pvpcaps.admin";    // allowed to use admin commands
        private const string PERMISSIONEXCLUDED = "pvpcaps.excluded"; // exclude player from handicaps
        private const string PERMISSIONUSE      = "pvpcaps.use";      // allows player to see handicaps

        // prefab names
        private const string MAP_LABEL = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string MAP_ICON  = "assets/prefabs/tools/map/genericradiusmarker.prefab";

        #endregion

        #region _weapons_

        protected readonly Dictionary<int, WeaponStats> weapons = new Dictionary<int, WeaponStats>
        {
            [174866732] = new WeaponStats{
                displayname = "16x Zoom Scope",
                shortname = "weapon.mod.8x.scope",
                accuracy = 0.15,
                recoil = -0.2,
                zoom = 16.0,
                aimcone = -0.3
            },
            [567235583] = new WeaponStats{
                displayname = "8x Zoom Scope",
                shortname = "weapon.mod.small.scope",
                accuracy = 0.15,
                recoil = -0.2,
                zoom = 8.0,
                aimcone = -0.3
            },
            [1545779598] = new WeaponStats{
                displayname = "Assault Rifle",
                shortname = "rifle.ak",
                damage = 50.0,
                accuracy = 4.0,
                recoil = 36.4,
                attack_rate = 450.0 / 60.0,
                range = 188.0,
                attachments = 3.0,
                aimcone = 0.2,
                capacity = 30.0,
                reload = 4.4,
                draw = 1.0
            },
            [1840822026] = new WeaponStats{
                displayname = "Beancan Grenade",
                shortname = "grenade.beancan",
                explosive_dmg = 15.0,
                lethality = 115.0,
                throw_distance = 26.0,
                fuse_length_min = 3.5,
                fuse_length_max = 4.0,
                blast_radius = 4.5,
                dud_chance = 0.15
            },
            [-1262185308] = new WeaponStats{
                displayname = "Binoculars",
                shortname = "tool.binoculars"
            },
            [1973165031] = new WeaponStats{
                displayname = "Birthday Cake",
                shortname = "cakefiveyear",
                damage = 15.0,
                attack_rate = 46.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1588298435] = new WeaponStats{
                displayname = "Bolt Action Rifle",
                shortname = "rifle.bolt",
                damage = 80.0,
                accuracy = 2.0,
                recoil = 7.0,
                attack_rate = 35.0 / 60.0,
                range = 574.0,
                attachments = 3.0,
                aimcone = 0.0,
                capacity = 4.0,
                reload = 5.0,
                draw = 1.0
            },
            [1711033574] = new WeaponStats{
                displayname = "Bone Club",
                shortname = "bone.club",
                damage = 12.0,
                attack_rate = 86.0 / 60.0,
                attack_size = 0.2,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1814288539] = new WeaponStats{
                displayname = "Bone Knife",
                shortname = "knife.bone",
                damage = 16.0,
                attack_rate = 86.0 / 60.0,
                attack_size = 0.2,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-194509282] = new WeaponStats{
                displayname = "Butcher Knife",
                shortname = "knife.butcher",
                damage = 20.0,
                attack_rate = 86.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1316706473] = new WeaponStats{
                displayname = "Camera",
                shortname = "tool.camera"
            },
            [1789825282] = new WeaponStats{
                displayname = "Candy Cane Club",
                shortname = "candycaneclub",
                damage = 18.0,
                attack_rate = 60.0 / 60.0,
                attack_size = 0.4,
                range = 1.6,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1104520648] = new WeaponStats{
                displayname = "Chainsaw",
                shortname = "chainsaw",
                damage = 12.0,
                attack_rate = 300.0 / 60.0,
                attack_size = 0.4,
                range = 1.5,
                draw = 1.0,
                melee = true
            },
            [2040726127] = new WeaponStats{
                displayname = "Combat Knife",
                shortname = "knife.combat",
                damage = 35.0,
                attack_rate = 86.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [884424049] = new WeaponStats{
                displayname = "Compound Bow",
                shortname = "bow.compound",
                damage = 100.0,
                accuracy = 0.0,
                recoil = 9.0,
                attack_rate = 60.0 / 60.0,
                range = 60.0,
                attachments = 0.0,
                aimcone = 0.2,
                capacity = 1.0,
                reload = 1.0,
                draw = 1.0
            },
            [1965232394] = new WeaponStats{
                displayname = "Crossbow",
                shortname = "crossbow",
                damage = 60.0,
                accuracy = 3.0,
                recoil = 9.0,
                attack_rate = 17.0 / 60.0,
                range = 34.0,
                attachments = 2.0,
                aimcone = 1.0,
                capacity = 1.0,
                reload = 3.6,
                draw = 1.6
            },
            [1796682209] = new WeaponStats{
                displayname = "Custom SMG",
                shortname = "smg.2",
                damage = 30.0,
                accuracy = 3.0,
                recoil = 8.0,
                attack_rate = 600.0 / 60.0,
                range = 50.0,
                attachments = 3.0,
                aimcone = 0.5,
                capacity = 24.0,
                reload = 4.0,
                draw = 1.0
            },
            [-765183617] = new WeaponStats{
                displayname = "Double Barrel Shotgun",
                shortname = "shotgun.double",
                damage = 180.0,
                accuracy = 15.0,
                recoil = 30.0,
                attack_rate = 120.0 / 60.0,
                range = 10.0,
                attachments = 2.0,
                aimcone = 0.5,
                capacity = 2.0,
                reload = 5.5,
                draw = 1.75
            },
            [-75944661] = new WeaponStats{
                displayname = "Eoka Pistol",
                shortname = "pistol.eoka",
                damage = 180.0,
                accuracy = 17.0,
                recoil = 7.0,
                attack_rate = 30.0 / 60.0,
                range = 10.0,
                attachments = 0.0,
                aimcone = 2.0,
                capacity = 1.0,
                reload = 2.0,
                draw = 0.5
            },
            [143803535] = new WeaponStats{
                displayname = "F1 Grenade",
                shortname = "grenade.f1",
                lethality = 100.0,
                throw_distance = 26.0,
                fuse_length_min = 3.0,
                fuse_length_max = 3.0,
                blast_radius = 6.0,
                thrown = true
            },
            [-1215753368] = new WeaponStats{
                displayname = "Flame Thrower",
                shortname = "flamethrower",
                damage = 8.0,
                attack_rate = 240.0 / 60.0,
                range = 1.0,
                attachments = 0.0,
                capacity = 100.0,
                reload = 3.5,
                draw = 1.8
            },
            [304481038] = new WeaponStats{
                displayname = "Flare",
                shortname = "flare",
                throw_distance = 26.0,
                fuse_length_min = 110.0,
                fuse_length_max = 120.0,
                thrown = true
            },
            [-196667575] = new WeaponStats{
                displayname = "Flashlight",
                shortname = "flashlight.held",
                damage = 15.0,
                attack_rate = 60.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true
            },
            [999690781] = new WeaponStats{
                displayname = "Geiger Counter",
                shortname = "geiger.counter"
            },
            [1569882109] = new WeaponStats{
                displayname = "Handmade Fishing Rod",
                shortname = "fishingrod.handmade"
            },
            [200773292] = new WeaponStats{
                displayname = "Hammer",
                shortname = "hammer",
                damage = 10.0,
                attack_rate = 120.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1252059217] = new WeaponStats{
                displayname = "Hatchet",
                shortname = "hatchet",
                damage = 25.0,
                attack_rate = 67.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [442289265] = new WeaponStats{
                displayname = "Holosight",
                shortname = "weapon.mod.holosight",
                accuracy = 0.7,
                zoom = 2.0,
                aimcone = -0.7,
                aimcone_hip = -0.7
            },
            [1443579727] = new WeaponStats{
                displayname = "Hunting Bow",
                shortname = "bow.hunting",
                damage = 50.0,
                accuracy = 2.0,
                recoil = 9.0,
                attack_rate = 60.0 / 60.0,
                range = 15.0,
                attachments = 0.0,
                aimcone = 1.0,
                capacity = 1.0,
                reload = 1.0,
                draw = 1.0
            },
            [1488979457] = new WeaponStats{
                displayname = "Jackhammer",
                shortname = "jackhammer",
                damage = 15.0,
                attack_rate = 400.0,
                attack_size = 0.3,
                range = 2.0,
                draw = 1.0,
                melee = true
            },
            [-778367295] = new WeaponStats{
                displayname = "L96 Rifle",
                shortname = "rifle.l96",
                damage = 80.0,
                accuracy = 2.0,
                recoil = 3.5,
                attack_rate = 23.0 / 60.0,
                range = 1125.0,
                attachments = 3.0,
                aimcone = 0.0,
                capacity = 5.0,
                reload = 3.0,
                draw = 1.0
            },
            [-1469578201] = new WeaponStats{
                displayname = "Longsword",
                shortname = "longsword",
                damage = 75.0,
                attack_rate = 30.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1469578201] = new WeaponStats{
                displayname = "LR-300 Assault Rifle",
                shortname = "longsword",
                damage = 40.0,
                accuracy = 4.0,
                recoil = 15.9,
                attack_rate = 500.0 / 60.0,
                range = 188.0,
                attachments = 3.0,
                aimcone = 0.2,
                capacity = 30.0,
                reload = 4.0,
                draw = 2.0
            },
            [-2069578888] = new WeaponStats{
                displayname = "M249",
                shortname = "lmg.m249",
                damage = 65.0,
                accuracy = 7.0,
                recoil = 7.0,
                attack_rate = 500.0 / 60.0,
                range = 317.0,
                attachments = 3.0,
                aimcone = 0.2,
                capacity = 100.0,
                reload = 7.5,
                draw = 1.8
            },
            [28201841] = new WeaponStats{
                displayname = "M39 Rifle",
                shortname = "rifle.m39",
                damage = 50.0,
                accuracy = 4.0,
                recoil = 8.5,
                attack_rate = 300.0 / 60.0,
                range = 352.0,
                attachments = 3.0,
                aimcone = 0.1,
                capacity = 20.0,
                reload = 3.25,
                draw = 1.0
            },
            [-852563019] = new WeaponStats{
                displayname = "M92 Pistol",
                shortname = "pistol.m92",
                damage = 45.0,
                accuracy = 4.0,
                recoil = 9.0,
                attack_rate = 600.0 / 60.0,
                range = 90.0,
                attachments = 3.0,
                aimcone = 1.0,
                capacity = 15.0,
                reload = 2.2,
                draw = 0.5
            },
            [-1966748496] = new WeaponStats{
                displayname = "Mace",
                shortname = "mace",
                damage = 50.0,
                attack_rate = 30.0 / 60.0,
                attack_size = 0.4,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1137865085] = new WeaponStats{
                displayname = "Machete",
                shortname = "machete",
                damage = 35.0,
                attack_rate = 60.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1318558775] = new WeaponStats{
                displayname = "MP5A4",
                shortname = "smg.mp5",
                damage = 35.0,
                accuracy = 5.0,
                recoil = 14.1,
                attack_rate = 600.0 / 60.0,
                range = 72.0,
                attachments = 3.0,
                aimcone = 0.5,
                capacity = 30.0,
                reload = 4.0,
                draw = 1.6
            },
            [-1123473824] = new WeaponStats{
                displayname = "Multiple Grenade Launcher",
                shortname = "multiplegrenadelauncher",
                damage = 125.0,
                accuracy = 4.0,
                recoil = 30.0,
                attack_rate = 150.0 / 60.0,
                range = 1.0,
                attachments = 2.0,
                aimcone = 2.25,
                capacity = 6.0,
                reload = 6.0,
                draw = 1.0
            },
            [-1405508498] = new WeaponStats{
                displayname = "Muzzle Boost",
                shortname = "weapon.mod.muzzleboost",
                attack_rate = 0.1,
                velocity = -0.1,
                damage = -0.1
            },
            [1478091698] = new WeaponStats{
                displayname = "Muzzle Brake",
                shortname = "weapon.mod.muzzlebrake",
                velocity = -0.2,
                damage = -0.2,
                accuracy = -0.38,
                recoil = -0.5,
                aimcone = 0.5,
                aimcone_hip = 2.0,
                aimcone_degrees = true
            },
            [1953903201] = new WeaponStats{
                displayname = "Nailgun",
                shortname = "pistol.nailgun",
                damage = 18.0,
                accuracy = 4.0,
                recoil = 7.0,
                attack_rate = 400.0 / 60.0,
                range = 6.0,
                attachments = 0.0,
                aimcone = 0.75,
                capacity = 16.0,
                reload = 3.4,
                draw = 0.5
            },
            [-1302129395] = new WeaponStats{
                displayname = "Pickaxe",
                shortname = "pickaxe",
                damage = 30.0,
                attack_rate = 40.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1090916276] = new WeaponStats{
                displayname = "Pitchfork",
                shortname = "pitchfork",
                damage = 40.0,
                attack_rate = 40.0 / 60.0,
                attack_size = 0.4,
                range = 2.8,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [795371088] = new WeaponStats{
                displayname = "Pump Shotgun",
                shortname = "shotgun.pump",
                damage = 210.0,
                accuracy = 14.0,
                recoil = 22.0,
                attack_rate = 55.0 / 60.0,
                range = 45.0,
                attachments = 2.0,
                aimcone = 0.0,
                capacity = 6.0,
                reload = 5.5,
                draw = 1.0
            },
            [1373971859] = new WeaponStats{
                displayname = "Python Revolver",
                shortname = "pistol.python",
                damage = 55.0,
                accuracy = 6.0,
                recoil = 18.0,
                attack_rate = 400.0 / 60.0,
                range = 72.0,
                attachments = 3.0,
                aimcone = 0.5,
                capacity = 6.0,
                reload = 3.75,
                draw = 0.5
            },
            [649912614] = new WeaponStats{
                displayname = "Revolver",
                shortname = "pistol.revolver",
                damage = 35.0,
                accuracy = 5.0,
                recoil = 7.0,
                attack_rate = 343.0 / 60.0,
                range = 36.0,
                attachments = 1.0,
                aimcone = 0.75,
                capacity = 8.0,
                reload = 3.4,
                draw = 0.5
            },
            [596469572] = new WeaponStats{
                displayname = "RF Transmitter",
                shortname = "rf.detonator"
            },
            [963906841] = new WeaponStats{
                displayname = "Rock",
                shortname = "rock",
                damage = 10.0,
                attack_rate = 46.0 / 60.0,
                attack_size = 0.2,
                range = 1.3,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [442886268] = new WeaponStats{
                displayname = "Rocket Launcher",
                shortname = "rocket.launcher",
                damage = 350.0,
                accuracy = 4.0,
                recoil = 30.0,
                attack_rate = 10.0 / 60.0,
                range = 1.0,
                attachments = 0.0,
                aimcone = 2.25,
                capacity = 1.0,
                reload = 6.0,
                draw = 1.0
            },
            [-262590403] = new WeaponStats{
                displayname = "Salvaged Axe",
                shortname = "axe.salvaged",
                damage = 40.0,
                attack_rate = 48.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1978999529] = new WeaponStats{
                displayname = "Salvaged Cleaver",
                shortname = "salvaged.cleaver",
                damage = 60.0,
                attack_rate = 30.0 / 60.0,
                attack_size = 0.4,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1506397857] = new WeaponStats{
                displayname = "Salvaged Hammer",
                shortname = "hammer.salvaged",
                damage = 30.0,
                attack_rate = 60.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1780802565] = new WeaponStats{
                displayname = "Salvaged Icepick",
                shortname = "icepick.salvaged",
                damage = 40.0,
                attack_rate = 48.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1326180354] = new WeaponStats{
                displayname = "Salvaged Sword",
                shortname = "salvaged.sword",
                damage = 50.0,
                attack_rate = 48.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-1878475007] = new WeaponStats{
                displayname = "Satchel Charge",
                shortname = "explosive.satchel",
                explosive_dmg = 75.0,
                lethality = 475.0,
                throw_distance = 20.0,
                fuse_length_min = 6.0,
                fuse_length_max = 12.0,
                blast_radius = 4.0,
                dud_chance = 0.2
            },
            [818877484] = new WeaponStats{
                displayname = "Semi-Automatic Pistol",
                shortname = "pistol.semiauto",
                damage = 40.0,
                accuracy = 4.0,
                recoil = 10.0,
                attack_rate = 400.0 / 60.0,
                range = 45.0,
                attachments = 3.0,
                aimcone = 0.75,
                capacity = 10.0,
                reload = 2.9,
                draw = 0.5
            },
            [-904863145] = new WeaponStats{
                displayname = "Semi-Automatic Rifle",
                shortname = "rifle.semiauto",
                damage = 40.0,
                accuracy = 4.0,
                recoil = 7.0,
                attack_rate = 343.0 / 60.0,
                range = 188.0,
                attachments = 3.0,
                aimcone = 0.25,
                capacity = 16.0,
                reload = 4.4,
                draw = 1.6
            },
            [-1850571427] = new WeaponStats{
                displayname = "Silencer",
                shortname = "weapon.mod.silencer",
                velocity = -0.25,
                damage = -0.25,
                accuracy = 0.33,
                recoil = -0.2,
                aimcone = -0.3,
                aimcone_hip = -0.3,
                aim_sway = -0.2
            },
            [-855748505] = new WeaponStats{
                displayname = "Simple Handmade Sight",
                shortname = "weapon.mod.simplesight",
                zoom = -0.5
            },
            [1263920163] = new WeaponStats{
                displayname = "Smoke Grenade",
                shortname = "grenade.smoke",
                throw_distance = 20.0,
                fuse_length_min = 1.0,
                fuse_length_max = 1.0
            },
            [-363689972] = new WeaponStats{
                displayname = "Snowball",
                shortname = "snowball",
                damage = 30.0,
                attack_rate = 46.0 / 60.0,
                attack_size = 0.2,
                range = 1.3,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [-41440462] = new WeaponStats{
                displayname = "Spas-12 Shotgun",
                shortname = "shotgun.spas12",
                damage = 137.0,
                accuracy = 14.0,
                recoil = 22.0,
                attack_rate = 180.0 / 60.0,
                range = 45.0,
                attachments = 3.0,
                aimcone = 0.0,
                capacity = 6.0,
                reload = 5.8,
                draw = 1.6
            },
            [-1583967946] = new WeaponStats{
                displayname = "Stone Hatchet",
                shortname = "stonehatchet",
                damage = 15.0,
                attack_rate = 67.0 / 60.0,
                attack_size = 0.3,
                range = 1.5,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [171931394] = new WeaponStats{
                displayname = "Stone Pickaxe",
                shortname = "stone.pickaxe",
                damage = 17.0,
                attack_rate = 67.0 / 60.0,
                attack_size = 0.3,
                range = 2.8,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1602646136] = new WeaponStats{
                displayname = "Stone Spear",
                shortname = "spear.stone",
                damage = 35.0,
                attack_rate = 40.0 / 60.0,
                attack_size = 0.2,
                range = 2.8,
                draw = 1.0,
                melee = true,
                thrown = true
            },
            [1397052267] = new WeaponStats{
                displayname = "Supply Signal",
                shortname = "supply.signal",
                throw_distance = 20.0,
                fuse_length_min = 4.0,
                fuse_length_max = 4.0
            },
            [1975934948] = new WeaponStats{
                displayname = "Survey Charge",
                shortname = "surveycharge",
                explosive_dmg = 8.0,
                lethality = 20.0,
                throw_distance = 8.0,
                fuse_length_min = 5.0,
                fuse_length_max = 5.0,
                blast_radius = 5.0
            },
            [1248356124] = new WeaponStats{
                displayname = "Timed Explosive Charge",
                shortname = "explosive.timed",
                explosive_dmg = 550.0,
                lethality = 550.0,
                throw_distance = 20.0,
                fuse_length_min = 10.0,
                fuse_length_max = 10.0,
                blast_radius = 4.0
            },
            [795236088] = new WeaponStats{
                displayname = "Torch",
                shortname = "torch",
                damage = 5.0,
                attack_rate = 46.0 / 60.0,
                attack_size = 0.2,
                range = 1.5,
                draw = 1.0,
                melee = true
            },
            [-1758372725] = new WeaponStats{
                displayname = "Thompson",
                shortname = "smg.thompson",
                damage = 38.0,
                accuracy = 3.0,
                recoil = 8.0,
                attack_rate = 462.0 / 60.0,
                range = 90.0,
                attachments = 3.0,
                aimcone = 0.5,
                capacity = 20.0,
                reload = 4.0,
                draw = 1.0
            },
            [1424075905] = new WeaponStats{
                displayname = "Water Bucket",
                shortname = "bucket.water"
            },
            [-1367281941] = new WeaponStats{
                displayname = "Waterpipe Shotgun",
                shortname = "shotgun.waterpipe",
                damage = 180.0,
                accuracy = 15.0,
                recoil = 22.0,
                attack_rate = 13.0 / 60.0,
                range = 10.0,
                attachments = 2.0,
                aimcone = 1.0,
                capacity = 1.0,
                reload = 4.5,
                draw = 2.4
            },
            [952603248] = new WeaponStats{
                displayname = "Weapon Flashlight",
                shortname = "weapon.mod.flashlight"
            },
            [-132516482] = new WeaponStats{
                displayname = "Weapon Laserlight",
                shortname = "weapon.mod.lasersight",
                accuracy = 0.44,
                aimcone = -0.2,
                aimcone_hip = -0.4,
                aim_sway = -0.9
            },
            [1540934679] = new WeaponStats{
                displayname = "Wooden Spear",
                shortname = "spear.wooden",
                damage = 25.0,
                attack_rate = 40.0 / 60.0,
                attack_size = 0.2,
                range = 2.6,
                draw = 1.0,
                melee = true,
                thrown = true
            }
        };
        
        #endregion

        #region _local_classes_

        internal class AddressInfo
        {
            public List<ulong> userid;
            public bool        whitelist;
        }

        internal class AttackInfo
        {
            public DateTime time;
            public ulong    userid;
            public double   penalty;
        }

        internal class DB
        {
            public Dictionary<ulong, Handicap>     handicaps;
            public Dictionary<string, AddressInfo> ip_table;

            public DB()
            {
                handicaps = new Dictionary<ulong, Handicap>();
                ip_table  = new Dictionary<string, AddressInfo>();
            }
        }

        internal class Fire : MonoBehaviour
        {
            public BaseEntity Initiator;
        }

        internal class Handicap
        {
            public string   display_name;
            public double   damage_amount;
            public DateTime decay_timer;
            public DateTime online_time;
            public ulong    warning_count;
            public DateTime warning_time;
            public ulong    cripple_count;
            public DateTime cripple_timer;
            public bool     crippled;
            public ulong    ban_count;
            public DateTime ban_timer;
            public bool     banned;

            public void processDeath(double penalty)
            {
                if(damage_amount < config.handicap_damage_max)
                {
                    if((damage_amount *= (1.0 + penalty)) > config.handicap_damage_max)
                    {
                        damage_amount = config.handicap_damage_max;
                    }

                    decay_timer = DateTime.UtcNow;
                }
            }

            public void processKill(double penalty)
            {
                if(damage_amount > config.handicap_damage_min)
                {
                    if((damage_amount /= (1.0 + penalty)) < config.handicap_damage_min)
                    {
                        damage_amount = config.handicap_damage_min;
                    }

                    decay_timer = DateTime.UtcNow;
                }
            }
        }

        internal class WeaponStats
        {
            public double accuracy;
            public double aim_sway;
            public double aimcone;
            public bool   aimcone_degrees;
            public double aimcone_hip;
            public double attachments;
            public double attack_rate;
            public double attack_size;
            public double blast_radius;
            public double capacity;
            public double damage;
            public string displayname;
            public double draw;
            public double dud_chance;
            public double explosive_dmg;
            public double fuse_length_min;
            public double fuse_length_max;
            public double lethality;
            public bool   melee;
            public double range;
            public double recoil;
            public double reload;
            public string shortname;
            public double throw_distance;
            public bool   thrown;
            public double velocity;
            public double zoom;
        }

        #endregion

        #region _configuration_

        internal class Configuration
        {
            // configuration: version
            public ulong config_version;

            // configuration: general settings
            public bool general_pvp_enabled;    // enables pvp damage on the server
            public bool general_pvp_ff_enabled; // enables pvp friendly fire damage on the server
            public bool general_pvp_team_scale; // scale damage by online team sizes

            // configuration: handicap adjustments
            public double handicap_assist_scale;    // per-assist penalty scale (0.0, 1.0]
            public ulong  handicap_ban_count;       // auto-ban ban count, (0 disabled)
            public ulong  handicap_ban_time;        // auto-ban time, in hours
            public double handicap_build_authed;    // build authed penalty scale [0.0, 1.0]
            public ulong  handicap_cripple_count;   // auto-ban cripple count (0 disabled)
            public double handicap_cripple_limit;   // auto-cripple threshold [0.0, 1.0]
            public ulong  handicap_cripple_time;    // auto-cripple time, in minutes
            public double handicap_damage_max;      // maximum damage multiplier [0.0, 1.0]
            public double handicap_damage_min;      // minimum damage multiplier (0.0, 1.0]
            public double handicap_decay_amount;    // handicap decay amount (0.0, 1.0]
            public ulong  handicap_decay_time;      // handicap decay time, in seconds
            public double handicap_killer_scale;    // per-kill penalty scale (0.0, 1.0]
            public double handicap_penalty_head;    // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_chest;   // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_arm;     // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_stomach; // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_leg;     // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_hand;    // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_foot;    // per-hit penalty [0.0, 1.0]
            public double handicap_penalty_generic; // per-hit penalty [0.0, 1.0]
            public double handicap_movement_rate;   // normal movement rate in meters/second
            public double handicap_range_normal;    // normal weapon range (0.0, 1.0]
            public ulong  handicap_warning_count;   // auto-cripple warning count (0 disabled)
            public double handicap_warning_limit;   // admin warning threshold [0.0, 1.0]

            // configuration: diagnostic outputs
            public bool output_console; // output event details to the server console
            public bool output_logfile; // output event details to the server logfile

            public static Configuration Defaults()
            {
                return new Configuration
                {
                    config_version = 10101,

                    general_pvp_enabled    = true,
                    general_pvp_ff_enabled = true,
                    general_pvp_team_scale = true,

                    handicap_assist_scale    = 0.5,
                    handicap_ban_count       = 3,
                    handicap_ban_time        = 24,
                    handicap_build_authed    = 0.5,
                    handicap_cripple_count   = 3,
                    handicap_cripple_limit   = 0.0625,
                    handicap_cripple_time    = 60,
                    handicap_damage_max      = 1.0,
                    handicap_damage_min      = 0.0001,
                    handicap_decay_amount    = 0.5,
                    handicap_decay_time      = 180,
                    handicap_killer_scale    = 0.75,
                    handicap_penalty_head    = 1.00,
                    handicap_penalty_chest   = 0.75,
                    handicap_penalty_arm     = 0.5,
                    handicap_penalty_stomach = 0.5,
                    handicap_penalty_leg     = 0.5,
                    handicap_penalty_hand    = 0.25,
                    handicap_penalty_foot    = 0.25,
                    handicap_penalty_generic = 0.5,
                    handicap_movement_rate   = 2.5,
                    handicap_range_normal    = 0.25,
                    handicap_warning_count   = 3,
                    handicap_warning_limit   = 0.125,

                    output_console = true,
                    output_logfile = true
                };
            }
        }

        private void CheckRange(ref double value, double min, double max)
        {
            if(value < min)
            {
                value = min; config_changed = true;
            }
            else if(value > max)
            {
                value = max; config_changed = true;
            }
        }

        private void CheckRange(ref ulong value, ulong min, ulong max)
        {
            if(value < min)
            {
                value = min; config_changed = true;
            }
            else if(value > max)
            {
                value = max; config_changed = true;
            }
        }

        protected override void LoadDefaultConfig() { }

        private void LoadConfiguration()
        {
            try
            {
                if(Config["Config", "Version"] is string)
                {
                    throw new Exception();
                }

                config = Config.ReadObject<Configuration>();
            }
            catch(Exception)
            {
                Puts(lang.GetMessage(c_error_bad_config, this));

                config = Configuration.Defaults();

                config_changed = true;
            }

            CheckRange(ref config.handicap_assist_scale,    double.Epsilon, 1.0);
            CheckRange(ref config.handicap_build_authed,    0.0,            1.0);
            CheckRange(ref config.handicap_ban_count,       0,              100);
            CheckRange(ref config.handicap_ban_time,        0,              720);
            CheckRange(ref config.handicap_cripple_count,   0,              100);
            CheckRange(ref config.handicap_cripple_limit,   0.0,            1.0);
            CheckRange(ref config.handicap_cripple_time,    0,              10080);
            CheckRange(ref config.handicap_damage_max,      double.Epsilon, 1.0);
            CheckRange(ref config.handicap_damage_min,      double.Epsilon, 1.0);
            CheckRange(ref config.handicap_decay_amount,    double.Epsilon, 1.0);
            CheckRange(ref config.handicap_decay_time,      15,             3600);
            CheckRange(ref config.handicap_killer_scale,    double.Epsilon, 1.0);
            CheckRange(ref config.handicap_penalty_head,    0.0,            1.0);
            CheckRange(ref config.handicap_penalty_chest,   0.0,            1.0);
            CheckRange(ref config.handicap_penalty_arm,     0.0,            1.0);
            CheckRange(ref config.handicap_penalty_stomach, 0.0,            1.0);
            CheckRange(ref config.handicap_penalty_leg,     0.0,            1.0);
            CheckRange(ref config.handicap_penalty_hand,    0.0,            1.0);
            CheckRange(ref config.handicap_penalty_foot,    0.0,            1.0);
            CheckRange(ref config.handicap_penalty_generic, 0.0,            1.0);
            CheckRange(ref config.handicap_movement_rate,   1.5,            4.5);
            CheckRange(ref config.handicap_range_normal,    double.Epsilon, 1.0);
            CheckRange(ref config.handicap_warning_count,   0,              100);
            CheckRange(ref config.handicap_warning_limit,   0.0,            1.0);

            SaveConfiguration();
        }

        private void SaveConfiguration()
        {
            if(config_changed)
            {
                Config.WriteObject(config);

                config_changed = false;
            }
        }

        #endregion

        #region _localization_

        // localization string identifiers
        private const string c_assist                    = "CAssist";
        private const string c_auto_ban_cripple_count    = "CAutoBanCrippleCount";
        private const string c_auto_ban_inherited        = "CAutoBanInherited";
        private const string c_ban_no_reason             = "CBanNoReason";
        private const string c_error_bad_config          = "CErrorBadConfig";
        private const string c_killer                    = "CKiller";
        private const string c_pvp_admin_banned_perm     = "CPvpAdminBannedPerm";
        private const string c_pvp_admin_banned_time     = "CPvpAdminBannedTime";
        private const string c_pvp_admin_crippled_perm   = "CPvpAdminCrippledPerm";
        private const string c_pvp_admin_crippled_time   = "CPvpAdminCrippledTime";
        private const string c_status                    = "CStatus";
        private const string c_victim                    = "CVictim";
        private const string m_error_admin_ipshare       = "MErrorAdminIPShare";
        private const string m_error_admin_syntax        = "MErrorAdminSyntax";
        private const string m_error_no_permission       = "MErrorNoPermission";
        private const string m_handicap_admin            = "MHandicapAdmin";
        private const string m_prefix                    = "MPrefix";
        private const string m_pvp_admin_ambiguous       = "MPvpAdminAmbiguous";
        private const string m_pvp_admin_ban             = "MPvpAdminBan";
        private const string m_pvp_admin_cripple         = "MPvpAdminCripple";
        private const string m_pvp_admin_disabled        = "MPvpAdminDisabled";
        private const string m_pvp_admin_disabled_reason = "MPvpAdminDisabledReason";
        private const string m_pvp_admin_enabled         = "MPvpAdminEnabled";
        private const string m_pvp_admin_ff_disabled     = "MPvpAdminFfDisabled";
        private const string m_pvp_admin_ff_enabled      = "MPvpAdminFfEnabled";
        private const string m_pvp_admin_ipshare_list    = "MPvpAdminIPShareList";
        private const string m_pvp_admin_ipshare_off     = "MPvpAdminIPShareOff";
        private const string m_pvp_admin_ipshare_on      = "MPvpAdminIPShareOn";
        private const string m_pvp_admin_modifier        = "MPvpAdminModifier";
        private const string m_pvp_admin_not_crippled    = "MPvpAdminNotCrippled";
        private const string m_pvp_admin_not_handicapped = "MPvpAdminNotHandicapped";
        private const string m_pvp_admin_not_found       = "MPvpAdminNotFound";
        private const string m_pvp_admin_reset           = "MPvpAdminReset";
        private const string m_pvp_admin_reset_all       = "MPvpAdminResetAll";
        private const string m_pvp_admin_team_disabled   = "MPvpAdminTeamDisabled";
        private const string m_pvp_admin_team_enabled    = "MPvpAdminTeamEnabled";
        private const string m_pvp_admin_team_list       = "MPvpAdminTeamList";
        private const string m_pvp_admin_unban           = "MPvpAdminUnban";
        private const string m_pvp_admin_unban_all       = "MPvpAdminUnbanAll";
        private const string m_pvp_admin_uncripple       = "MPvpAdminUncripple";
        private const string m_pvp_admin_uncripple_all   = "MPvpAdminUncrippleAll";
        private const string m_pvp_admin_warning         = "MPvpAdminWarning";
        private const string m_pvp_damage_decrease       = "MPvpDamageDecrease";
        private const string m_pvp_damage_increase       = "MPvpDamageIncrease";
        private const string m_pvp_damage_modifier       = "MPvpDamageModifier";
        private const string m_pvp_ff_is_disabled        = "MPvpFfIsDisabled";
        private const string m_pvp_ff_is_enabled         = "MPvpFfIsEnabled";
        private const string m_pvp_is_disabled           = "MPvpIsDisabled";
        private const string m_pvp_is_enabled            = "MPvpIsEnabled";
        private const string m_pvp_team_is_disabled      = "MPvpTeamIsDisabled";
        private const string m_pvp_team_is_enabled       = "MPvpTeamIsEnabled";
        private const string m_suffix                    = "MSuffix";

        // localization strings
        private readonly Dictionary<string, string> messages = new Dictionary<string, string>
        {
            [c_assist]                    = "assist",
            [c_auto_ban_cripple_count]    = "Too many violations (cripple limit reached)",
            [c_auto_ban_inherited]        = "Alternate account (ban inherited)",
            [c_ban_no_reason]             = "No reason given",
            [c_error_bad_config]          = "Configuration file bad or missing. Loading default configuration.",
            [c_killer]                    = "killer",
            [c_pvp_admin_banned_perm]     = " (banned permanently)",
            [c_pvp_admin_banned_time]     = " (banned for {0}h)",
            [c_pvp_admin_crippled_perm]   = " (crippled permanently)",
            [c_pvp_admin_crippled_time]   = " (crippled for {0}m)",
            [c_status]                    = "status",
            [c_victim]                    = "victim",
            [m_error_admin_ipshare]       = "<color=#ff7f7f>IP address not found for {0}.</color>",
            [m_error_admin_syntax]        = "<color=#ffff7f>Usage:\n   /pvp * [ff [off | on] | list | off [message] | on | reset | team [off | on] | unban | uncripple]\n   /pvp (steamid | displayname) [ban [hours] [reason] | cripple [minutes] | reset | unban | uncripple]\n   /pvp (steamid | displayname) ipshare [ban [hours] [reason] | list | off | on | unban]\n   /pvp (steamid | displayname) team [ban [hours] [reason] | cripple [minutes] | reset | unban | uncripple]</color>",
            [m_error_no_permission]       = "<color=#ff0000>You do not have permission to use this command.</color>",
            [m_handicap_admin]            = "{0}",
            [m_prefix]                    = "<size=12><color=#ff1f1f>[</color><color=#ff3f1f>P</color><color=#ff5f1f>v</color><color=#ff7f1f>P</color><color=#ff9f1f>C</color><color=#ff7f1f>a</color><color=#ff5f1f>p</color><color=#ff3f1f>s</color><color=#ff1f1f>]</color> ",
            [m_pvp_admin_ambiguous]       = "<color=#ff7f7f>Multiple players found:{0}</color>",
            [m_pvp_admin_ban]             = "<color=#ff7f7f>{0} has BANNED {1}.</color>",
            [m_pvp_admin_cripple]         = "<color=#ff7f7f>{0} has CRIPPLED {1}.</color>",
            [m_pvp_admin_disabled]        = "<color=#ff7f7f>{0} has DISABLED PvP damage on the server.</color>",
            [m_pvp_admin_disabled_reason] = "<color=#ff7f7f>{0} has DISABLED PvP damage on the server. (Reason: {1})</color>",
            [m_pvp_admin_enabled]         = "<color=#7fff00>{0} has ENABLED PvP damage on the server.</color>",
            [m_pvp_admin_ff_disabled]     = "<color=#ff7f7f>{0} has DISABLED friendly fire damage on the server.</color>",
            [m_pvp_admin_ff_enabled]      = "<color=#7fff00>{0} has ENABLED friendly fire damage on the server.</color>",
            [m_pvp_admin_ipshare_list]    = "<color=#ffff00>Connection history for {0}:{1}</color>",
            [m_pvp_admin_ipshare_off]     = "<color=#ffff00>IP sharing is DISABLED for {0}.</color>",
            [m_pvp_admin_ipshare_on]      = "<color=#ffff00>IP sharing is ENABLED for {0}.</color>",
            [m_pvp_admin_modifier]        = "<color=#ffff00>{0}'s PvP damage modifier is {1}%.</color><color=#ff7f7f>{2}</color>",
            [m_pvp_admin_not_crippled]    = "<color=#ffff00>No crippled players found.</color>",
            [m_pvp_admin_not_handicapped] = "<color=#ffff00>No handicapped players found.</color>",
            [m_pvp_admin_not_found]       = "<color=#ff7f7f>Player not found: {0}</color>",
            [m_pvp_admin_reset]           = "<color=#ffff00>{0} has RESET the handicap for {1}.</color>",
            [m_pvp_admin_reset_all]       = "<color=#ffff00>{0} has RESET the handicaps for all players.</color>",
            [m_pvp_admin_team_disabled]   = "<color=#ff7f7f>{0} has DISABLED team size scaling on the server.</color>",
            [m_pvp_admin_team_enabled]    = "<color=#7fff00>{0} has ENABLED team size scaling on the server.</color>",
            [m_pvp_admin_team_list]       = "<color=#ffff00>{0}'s team members:</color>",
            [m_pvp_admin_unban]           = "<color=#7fff00>{0} has UNBANNED {1}.</color>",
            [m_pvp_admin_unban_all]       = "<color=#7fff00>{0} has UNBANNED all players.</color>",
            [m_pvp_admin_uncripple]       = "<color=#7fff00>{0} has UNCRIPPLED {1}.</color>",
            [m_pvp_admin_uncripple_all]   = "<color=#7fff00>{0} has UNCRIPPLED all players.</color>",
            [m_pvp_admin_warning]         = "<color=#ffff00>WARNING: {0} received a penalty of -{1}%.</color>",
            [m_pvp_damage_decrease]       = "<color=#ff7f00>Your PvP damage modifier decreased to {0}%.</color>",
            [m_pvp_damage_increase]       = "<color=#7fff00>Your PvP damage modifier increased to {0}%.</color>",
            [m_pvp_damage_modifier]       = "<color=#ffff00>Your PvP damage modifier is {0}%.</color>",
            [m_pvp_ff_is_disabled]        = "<color=#ff7f7f>Friendly fire is disabled.</color>",
            [m_pvp_ff_is_enabled]         = "<color=#7fff00>Friendly fire is enabled.</color>",
            [m_pvp_is_disabled]           = "<color=#ff7f7f>PvP is disabled.</color>",
            [m_pvp_is_enabled]            = "<color=#7fff00>PvP is enabled.</color>",
            [m_pvp_team_is_disabled]      = "<color=#ff7f7f>Team size scaling is disabled.</color>",
            [m_pvp_team_is_enabled]       = "<color=#7fff00>Team size scaling is enabled.</color>",
            [m_suffix]                    = "</size>"
        };

        internal static void ChatMessage(BasePlayer player, string key, params object[] args)
        {
            if(player == null)
            {
                instance.Puts(string.Format(ToPlaintext(instance.lang.GetMessage(key, instance)), args));
            }
            else if(player.IsConnected)
            {
                string prefix = GetMessage(m_prefix, player.UserIDString);

                string middle = GetMessage(key, player.UserIDString, args);

                string suffix = GetMessage(m_suffix, player.UserIDString);

                StringBuilder message = new StringBuilder(prefix.Length + middle.Length + suffix.Length);
                
                message.Append(prefix).Append(middle).Append(suffix);

                player.ChatMessage(message.ToString());
            }
        }

        internal static void ChatMessageAdmin(string key, params object[] args)
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                if(instance.permission.UserHasPermission(player.UserIDString, PERMISSIONADMIN))
                {
                    ChatMessage(player, key, args);
                }
            }
        }

        internal static void ChatMessageBroadcast(string key, params object[] args)
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                ChatMessage(player, key, args);
            }
        }

        internal static string CrippleStatus(ulong userid, string admin_id = null)
        {
            if(db.handicaps[userid].banned)
            {
                if(db.handicaps[userid].ban_timer == DateTime.MinValue)
                {
                    return GetMessage(c_pvp_admin_banned_perm, admin_id);
                }
                else
                {
                    double hours = db.handicaps[userid].ban_timer.Subtract(DateTime.UtcNow).TotalMinutes / 60.0;

                    return GetMessage(c_pvp_admin_banned_time, admin_id, hours.ToString("0.0"));
                }
            }
            else if(db.handicaps[userid].crippled)
            {
                if(db.handicaps[userid].cripple_timer == DateTime.MinValue)
                {
                    return GetMessage(c_pvp_admin_crippled_perm, admin_id);
                }
                else
                {
                    double minutes = db.handicaps[userid].cripple_timer.Subtract(DateTime.UtcNow).TotalMinutes;

                    return GetMessage(c_pvp_admin_crippled_time, admin_id, minutes.ToString("0.0"));
                }
            }

            return string.Empty;
        }

        internal static string DamagePercent(ulong userid)
        {
            return (db.handicaps[userid].damage_amount * 100.0).ToString("0.00");
        }

        internal static string HandicapInfoAdmin(ulong userid, string admin_id = null)
        {
            if(admin_id == null)
            {
                string format = ToPlaintext(instance.lang.GetMessage(m_pvp_admin_modifier, instance));

                return string.Format(format, NameAndID(userid), DamagePercent(userid), CrippleStatus(userid));
            }
            else
            {
                return GetMessage(m_pvp_admin_modifier, admin_id, NameAndID(userid), DamagePercent(userid), CrippleStatus(userid, admin_id));
            }
        }

        internal static string HandicapInfoConsole(string key, ulong userid)
        {
            string label = instance.lang.GetMessage(key, instance);

            return string.Format("{0}[{1}: {2}%{3}]", label, NameAndID(userid), DamagePercent(userid), CrippleStatus(userid));
        }

        protected static string GetMessage(string key, string userid, params object[] args) => string.Format(instance.lang.GetMessage(key, instance, userid), args);

        protected static string GetMessagePlain(string key) => ToPlaintext(instance.lang.GetMessage(key, instance));

        protected override void LoadDefaultMessages() => lang.RegisterMessages(messages, this, "en");

        internal static void LogMessage(string key, params object[] args)
        {
            if(config.output_console || config.output_logfile)
            {
                string message = string.Format(GetMessagePlain(key), args);

                if(config.output_console)
                {
                    instance.Puts(message);
                }

                if(config.output_logfile)
                {
                    string time = DateTime.Now.ToString("T", DateTimeFormatInfo.InvariantInfo);

                    instance.LogToFile("log", string.Format("{0}: {1}", time, message), instance);
                }
            }
        }

        internal static string NameAndID(ulong userid)
        {
            return string.Format("{0}({1})", db.handicaps[userid].display_name, userid.ToString());
        }

        internal static string ToPlaintext(string message)
        {
            string result = string.Empty;

            if(message != null)
            {
                StringBuilder sb_result = new StringBuilder(Regex.Replace(message, "<[^>]+>", ""));

                sb_result.Replace("&lt;", "<");
                sb_result.Replace("&gt;", ">");
                sb_result.Replace("&amp;", "&");

                result = sb_result.ToString();
            }

            return result;
        }

        #endregion

        #region _commands_

        [ChatCommand("pvp")]
        private void ChatCmdPvP(BasePlayer player, string cmd, string[] args)
        {
            bool console = (player == null);

            string admin_id = console ? null : player.UserIDString;

            string admin_name = console ? "CONSOLE" : player.displayName;

            if((args == null) || args.Length == 0)
            {
                if(console)
                {
                    bool entry_found = false;

                    foreach(var entry in db.handicaps)
                    {
                        if(entry.Value.crippled || entry.Value.damage_amount != config.handicap_damage_max)
                        {
                            ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(entry.Key, admin_id));

                            entry_found = true;
                        }
                    }

                    if(!entry_found)
                    {
                        ChatMessage(player, m_pvp_admin_not_handicapped);
                    }
                }
                else
                {
                    if(permission.UserHasPermission(player.UserIDString, PERMISSIONUSE))
                    {
                        ChatMessage(player, m_pvp_damage_modifier, DamagePercent(player.userID));
                    }
                    else
                    {
                        ChatMessage(player, m_error_no_permission);
                    }
                }

                return;
            }

            if(!console && !permission.UserHasPermission(player.UserIDString, PERMISSIONADMIN))
            {
                ChatMessage(player, m_error_no_permission);

                return;
            }

            if(args[0] == "*")
            {
                if(args.Length == 1)
                {
                    ChatMessage(player, config.general_pvp_enabled ? m_pvp_is_enabled : m_pvp_is_disabled);

                    return;
                }

                if(args.Length > 3)
                {
                    goto syntax_error;
                }

                switch(args[1].ToLower())
                {
                case "ff":

                    if(args.Length == 2)
                    {
                        ChatMessage(player, config.general_pvp_ff_enabled ? m_pvp_ff_is_enabled : m_pvp_ff_is_disabled);

                        return;
                    }

                    switch(args[2].ToLower())
                    {
                    case "off":

                        config.general_pvp_ff_enabled = false; config_changed = true;

                        ChatMessageBroadcast(m_pvp_admin_ff_disabled, admin_name);

                        if(!config.output_console && console)
                        {
                            ChatMessage(player, m_pvp_admin_ff_disabled, admin_name);
                        }

                        LogMessage(m_pvp_admin_ff_disabled, admin_name);

                        return;

                    case "on":

                        config.general_pvp_ff_enabled = true; config_changed = true;

                        ChatMessageBroadcast(m_pvp_admin_ff_enabled, admin_name);

                        if(!config.output_console && console)
                        {
                            ChatMessage(player, m_pvp_admin_ff_enabled, admin_name);
                        }

                        LogMessage(m_pvp_admin_ff_enabled, admin_name);

                        return;

                    default:

                        goto syntax_error;
                    }

                case "list":

                    if(args.Length != 2)
                    {
                        goto syntax_error;
                    }

                    bool entry_found = false;

                    foreach(var entry in db.handicaps)
                    {
                        if(entry.Value.crippled)
                        {
                            ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(entry.Key, admin_id));

                            entry_found = true;
                        }
                    }

                    if(!entry_found)
                    {
                        ChatMessage(player, m_pvp_admin_not_crippled);
                    }

                    return;

                case "off":

                    if(args.Length == 2)
                    {
                        config.general_pvp_enabled = false; config_changed = true;

                        ChatMessageAdmin(m_pvp_admin_disabled, admin_name);

                        if(!config.output_console && console)
                        {
                            ChatMessage(player, m_pvp_admin_disabled, admin_name);
                        }

                        LogMessage(m_pvp_admin_disabled, admin_name);

                        return;
                    }

                    config.general_pvp_enabled = false; config_changed = true;

                    ChatMessageBroadcast(m_pvp_admin_disabled_reason, admin_name, args[2]);

                    if(!config.output_console && console)
                    {
                        ChatMessage(player, m_pvp_admin_disabled_reason, admin_name, args[2]);
                    }

                    LogMessage(m_pvp_admin_disabled_reason, admin_name, args[2]);

                    return;

                case "on":

                    if(args.Length != 2)
                    {
                        goto syntax_error;
                    }

                    config.general_pvp_enabled = true; config_changed = true;

                    ChatMessageBroadcast(m_pvp_admin_enabled, admin_name);

                    if(!config.output_console && console)
                    {
                        ChatMessage(player, m_pvp_admin_enabled, admin_name);
                    }

                    LogMessage(m_pvp_admin_enabled, admin_name);

                    return;

                case "reset":

                    if(args.Length != 2)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in db.handicaps)
                    {
                        AdminReset(admin_name, entry.Key, true);
                    }

                    ChatMessageAdmin(m_pvp_admin_reset_all, admin_name);

                    if(!config.output_console && console)
                    {
                        ChatMessage(player, m_pvp_admin_reset_all, admin_name);
                    }

                    LogMessage(m_pvp_admin_reset_all, admin_name);

                    return;

                case "team":

                    if(args.Length == 2)
                    {
                        ChatMessage(player, config.general_pvp_team_scale ? m_pvp_team_is_enabled : m_pvp_team_is_disabled);

                        return;
                    }

                    switch(args[2].ToLower())
                    {
                    case "off":

                        config.general_pvp_team_scale = false; config_changed = true;

                        ChatMessageAdmin(m_pvp_admin_team_disabled, admin_name);

                        if(!config.output_console && console)
                        {
                            ChatMessage(player, m_pvp_admin_team_disabled, admin_name);
                        }

                        LogMessage(m_pvp_admin_team_disabled, admin_name);

                        return;

                    case "on":

                        config.general_pvp_team_scale = true; config_changed = true;

                        ChatMessageAdmin(m_pvp_admin_team_enabled, admin_name);

                        if(!config.output_console && console)
                        {
                            ChatMessage(player, m_pvp_admin_team_enabled, admin_name);
                        }

                        LogMessage(m_pvp_admin_team_enabled, admin_name);

                        return;

                    default:

                        goto syntax_error;
                    }

                case "unban":

                    if(args.Length != 2)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in covalence.Players.All.ToList())
                    {
                        if(covalence.Server.IsBanned(entry.Id))
                        {
                            ulong entryid = ulong.Parse(entry.Id);

                            if(db.handicaps.ContainsKey(entryid) && db.handicaps[entryid].banned)
                            {
                                AdminUnban(admin_name, entryid, true);

                                db.handicaps[entryid].ban_count = 0;
                            }
                            else
                            {
                                ExecuteUnban(entry.Id);
                            }
                        }
                    }

                    ChatMessageAdmin(m_pvp_admin_unban_all, admin_name);

                    if(!config.output_console && console)
                    {
                        ChatMessage(player, m_pvp_admin_unban_all, admin_name);
                    }

                    LogMessage(m_pvp_admin_unban_all, admin_name);

                    return;

                case "uncripple":

                    if(args.Length != 2)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in db.handicaps)
                    {
                        AdminUncripple(admin_name, entry.Key, true);
                    }

                    ChatMessageAdmin(m_pvp_admin_uncripple_all, admin_name);

                    if(!config.output_console && console)
                    {
                        ChatMessage(player, m_pvp_admin_uncripple_all, admin_name);
                    }

                    LogMessage(m_pvp_admin_uncripple_all, admin_name);

                    return;

                default:

                    goto syntax_error;
                }
            }

            List<ulong> found = FindPlayer(args[0]);

            if(found.Count == 0)
            {
                ChatMessage(player, m_pvp_admin_not_found, args[0]);

                return;
            }
            else if(found.Count > 1)
            {
                StringBuilder entries = new StringBuilder();

                foreach(var entry in found)
                {
                    entries.Append("\n - ").Append(NameAndID(entry));
                }

                ChatMessage(player, m_pvp_admin_ambiguous, entries.ToString());

                return;
            }

            ulong userid = found[0];

            if(args.Length == 1)
            {
                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;
            }

            ulong duration = 0; string reason;

            switch(args[1].ToLower())
            {
            case "ban":

                if(!ParseDurationAndReason(ref args, 2, out duration, out reason))
                {
                    goto syntax_error;
                }

                AdminBan(admin_name, userid, duration, reason, console);

                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;

            case "cripple":

                if(args.Length > 2)
                {
                    if(args.Length > 3 || !ulong.TryParse(args[2], out duration))
                    {
                        goto syntax_error;
                    }
                }

                AdminCripple(admin_name, userid, duration, console);

                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;

            case "ipshare":

                string ip_address = FindIPAddress(userid);

                if(ip_address == null)
                {
                    ChatMessage(player, m_error_admin_ipshare, NameAndID(userid));

                    return;
                }

                if(args.Length != 3)
                {
                    goto syntax_error;
                }

                switch(args[2].ToLower())
                {
                case "ban":

                    if(!ParseDurationAndReason(ref args, 3, out duration, out reason))
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in db.ip_table[ip_address].userid)
                    {
                        AdminBan(admin_name, entry, duration, reason, console);
                    }

                    return;

                case "list":

                    StringBuilder entries = new StringBuilder();

                    foreach(var entry in db.ip_table[ip_address].userid)
                    {
                        entries.Append("\n - ").Append(NameAndID(entry));
                    }

                    ChatMessage(player, m_pvp_admin_ipshare_list, ip_address, entries.ToString());

                    break;

                case "off":

                    db.ip_table[ip_address].whitelist = false;

                    break;

                case "on":

                    db.ip_table[ip_address].whitelist = true;

                    break;

                case "unban":

                    if(args.Length != 3)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in db.ip_table[ip_address].userid)
                    {
                        AdminUnban(admin_name, entry, false, console);

                        db.handicaps[entry].ban_count = 0;
                    }

                    return;

                default:

                    goto syntax_error;
                }

                ChatMessage(player, db.ip_table[ip_address].whitelist ? m_pvp_admin_ipshare_on : m_pvp_admin_ipshare_off, NameAndID(userid));

                return;

            case "reset":

                if(args.Length != 2)
                {
                    goto syntax_error;
                }

                AdminReset(admin_name, userid, false, console);

                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;

            case "team":

                List<ulong> team = TeamList(userid);

                if(args.Length == 2)
                {
                    ChatMessage(player, m_pvp_admin_team_list, NameAndID(userid));

                    foreach(var entry in team)
                    {
                        ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(entry, admin_id));
                    }

                    return;
                }

                switch(args[2].ToLower())
                {
                case "ban":

                    if(!ParseDurationAndReason(ref args, 3, out duration, out reason))
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in team)
                    {
                        AdminBan(admin_name, entry, duration, reason, console);
                    }

                    return;

                case "cripple":

                    if(args.Length > 3)
                    {
                        if((args.Length > 4) || !ulong.TryParse(args[3], out duration))
                        {
                            goto syntax_error;
                        }
                    }

                    foreach(var entry in team)
                    {
                        AdminCripple(admin_name, entry, duration, console);
                    }

                    return;

                case "reset":

                    if(args.Length != 3)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in team)
                    {
                        AdminReset(admin_name, entry, false, console);
                    }

                    return;

                case "unban":

                    if(args.Length != 3)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in team)
                    {
                        AdminUnban(admin_name, entry, false, console);

                        db.handicaps[entry].ban_count = 0;
                    }

                    return;

                case "uncripple":

                    if(args.Length != 3)
                    {
                        goto syntax_error;
                    }

                    foreach(var entry in team)
                    {
                        AdminUncripple(admin_name, entry, false, console);
                    }

                    return;

                default:

                    goto syntax_error;
                }

            case "unban":

                if(args.Length != 2)
                {
                    goto syntax_error;
                }

                AdminUnban(admin_name, userid, false, console);

                db.handicaps[userid].ban_count = 0;

                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;

            case "uncripple":

                if(args.Length != 2)
                {
                    goto syntax_error;
                }

                AdminUncripple(admin_name, userid, false, console);

                ChatMessage(player, m_handicap_admin, HandicapInfoAdmin(userid, admin_id));

                return;

            default:

                goto syntax_error;
            }

        syntax_error:

            ChatMessage(player, m_error_admin_syntax);
        }

        [ConsoleCommand("pvp")]
        private void ConsoleCmdPvp(ConsoleSystem.Arg arg)
        {
            if(arg.IsRcon)
            {
                ChatCmdPvP(null, null, arg.Args);
            }
        }

        #endregion

        #region _helpers_

        internal void AdminBan(string admin_name, ulong userid, ulong duration, string reason, bool console = false)
        {
            if(permission.UserHasPermission(userid.ToString(), PERMISSIONADMIN))
            {
                return;
            }

            db.handicaps[userid].warning_count = 0;
            db.handicaps[userid].cripple_count = 0;
            db.handicaps[userid].cripple_timer = DateTime.MinValue;
            db.handicaps[userid].crippled      = false;
            db.handicaps[userid].banned        = true;

            if(duration > 0)
            {
                db.handicaps[userid].ban_timer = DateTime.UtcNow + TimeSpan.FromSeconds(3600.0 * duration);
            }
            else
            {
                db.handicaps[userid].ban_timer = DateTime.MinValue;
            }

            ExecuteBan(userid.ToString(), reason);

            ChatMessageAdmin(m_pvp_admin_ban, admin_name, NameAndID(userid));

            if(!config.output_console && console)
            {
                ChatMessage(null, m_pvp_admin_ban, admin_name, NameAndID(userid));
            }

            LogMessage(m_pvp_admin_ban, admin_name, NameAndID(userid));
        }

        internal void AdminBanInherited(string admin_name, ulong userid)
        {
            db.handicaps[userid].warning_count = 0;
            db.handicaps[userid].cripple_count = 0;
            db.handicaps[userid].cripple_timer = DateTime.MinValue;
            db.handicaps[userid].crippled      = false;

            ExecuteBan(userid.ToString(), GetMessagePlain(c_auto_ban_inherited));

            ChatMessageAdmin(m_pvp_admin_ban, admin_name, NameAndID(userid));

            LogMessage(m_pvp_admin_ban, admin_name, NameAndID(userid));
        }

        internal void AdminCripple(string admin_name, ulong userid, ulong duration, bool console = false)
        {
            db.handicaps[userid].crippled = true;

            if(duration > 0)
            {
                db.handicaps[userid].cripple_timer = DateTime.UtcNow + TimeSpan.FromSeconds(60.0 * duration);
            }
            else
            {
                db.handicaps[userid].cripple_timer = DateTime.MinValue;
            }

            ChatMessageAdmin(m_pvp_admin_cripple, admin_name, NameAndID(userid));

            if(!config.output_console && console)
            {
                ChatMessage(null, m_pvp_admin_cripple, admin_name, NameAndID(userid));
            }

            LogMessage(m_pvp_admin_cripple, admin_name, NameAndID(userid));
        }

        internal void AdminReset(string admin_name, ulong userid, bool silent = false, bool console = false)
        {
            if((db.handicaps[userid].damage_amount == config.handicap_damage_max) && !db.handicaps[userid].crippled)
            {
                return;
            }

            db.handicaps[userid].damage_amount = config.handicap_damage_max;
            db.handicaps[userid].decay_timer   = DateTime.UtcNow;
            db.handicaps[userid].warning_count = 0;
            db.handicaps[userid].cripple_count = 0;
            db.handicaps[userid].cripple_timer = DateTime.MinValue;
            db.handicaps[userid].crippled      = false;

            if(permission.UserHasPermission(userid.ToString(), PERMISSIONUSE))
            {
                ChatMessage(BasePlayer.FindByID(userid), m_pvp_damage_modifier, DamagePercent(userid));
            }

            if(!silent)
            {
                ChatMessageAdmin(m_pvp_admin_reset, admin_name, NameAndID(userid));

                if(!config.output_console && console)
                {
                    ChatMessage(null, m_pvp_admin_reset, admin_name, NameAndID(userid));
                }

                LogMessage(m_pvp_admin_reset, admin_name, NameAndID(userid));
            }
        }

        internal void AdminUnban(string admin_name, ulong userid, bool silent = false, bool console = false)
        {
            if(db.handicaps[userid].banned)
            {
                db.handicaps[userid].damage_amount = config.handicap_damage_max;
                db.handicaps[userid].decay_timer   = DateTime.UtcNow;
                db.handicaps[userid].warning_count = 0;
                db.handicaps[userid].cripple_count = 0;
                db.handicaps[userid].cripple_timer = DateTime.MinValue;
                db.handicaps[userid].crippled      = false;
                db.handicaps[userid].ban_timer     = DateTime.MinValue;
                db.handicaps[userid].banned        = false;
            }

            ExecuteUnban(userid.ToString());

            if(!silent)
            {
                ChatMessageAdmin(m_pvp_admin_unban, admin_name, NameAndID(userid));

                if(!config.output_console && console)
                {
                    ChatMessage(null, m_pvp_admin_unban, admin_name, NameAndID(userid));
                }

                LogMessage(m_pvp_admin_unban, admin_name, NameAndID(userid));
            }
        }

        internal void AdminUncripple(string admin_name, ulong userid, bool silent = false, bool console = false)
        {
            if(!db.handicaps[userid].crippled)
            {
                return;
            }

            db.handicaps[userid].decay_timer   = DateTime.UtcNow;
            db.handicaps[userid].cripple_timer = DateTime.MinValue;
            db.handicaps[userid].crippled      = false;

            if(!silent)
            {
                ChatMessageAdmin(m_pvp_admin_uncripple, admin_name, NameAndID(userid));

                if(!config.output_console && console)
                {
                    ChatMessage(null, m_pvp_admin_uncripple, admin_name, NameAndID(userid));
                }

                LogMessage(m_pvp_admin_uncripple, admin_name, NameAndID(userid));
            }
        }

        internal bool AutoBanCountReached(ulong userid)
        {
            return ((config.handicap_ban_count != 0) && (db.handicaps[userid].ban_count >= config.handicap_ban_count));
        }

        internal bool AutoCrippleCountReached(ulong userid)
        {
            return ((config.handicap_cripple_count != 0) && (db.handicaps[userid].cripple_count >= config.handicap_cripple_count));
        }

        internal bool AutoWarningCountReached(ulong userid)
        {
            return ((config.handicap_warning_count != 0) && (db.handicaps[userid].warning_count >= config.handicap_warning_count));
        }

        internal bool CanLoot(BasePlayer looter, ulong target)
        {
            if(target == looter.userID)
            {
                return true;
            }

            if(db.handicaps[looter.userID].crippled)
            {
                if(permission.UserHasPermission(looter.UserIDString, PERMISSIONADMIN))
                {
                    return true;
                }

                if(permission.UserHasPermission(looter.UserIDString, PERMISSIONEXCLUDED))
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        internal object CanLootHelper(BasePlayer looter, ulong target)
        {
            if(!CanLoot(looter, target))
            {
                return false;
            }

            return null;
        }

        internal void DBLoad()
        {
            if((db = Interface.Oxide.DataFileSystem.ReadObject<DB>(this.Name)) == null)
            {
                db = new DB();
            }
        }

        internal void DBStore()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, db);
        }

        internal void ExecuteBan(string user, string reason)
        {
            Server.Command("ban", user, reason);
        }

        internal void ExecuteUnban(string user)
        {
            Server.Command("unban", user);
        }

        internal string FindIPAddress(ulong userid)
        {
            foreach(var entry in db.ip_table)
            {
                foreach(var entry_id in entry.Value.userid)
                {
                    if(entry_id == userid)
                    {
                        return entry.Key;
                    }
                }
            }

            return null;
        }

        internal List<ulong> FindPlayer(string input)
        {
            string input_lower = input.ToLower();

            List<ulong> found = new List<ulong>();

            foreach(var entry in db.handicaps)
            {
                if(entry.Key.ToString() == input)
                {
                    found.Clear(); found.Add(entry.Key);

                    break;
                }
                else if(entry.Value.display_name.ToLower() == input_lower)
                {
                    found.Add(entry.Key);
                }
                else if(entry.Value.display_name.ToLower().Contains(input_lower))
                {
                    found.Add(entry.Key);
                }
            }

            return found;
        }

        internal double GetHitPenalty(BasePlayer attacker, BasePlayer victim, HitInfo info)
        {
            double penalty, range = 0.0, distance = attacker.Distance(victim);

            Item item = info?.Weapon?.GetItem();

            switch(info.boneArea)
            {
            case HitArea.Head:
                penalty = config.handicap_penalty_head; break;
            case HitArea.Chest:
                penalty = config.handicap_penalty_chest; break;
            case HitArea.Arm:
                penalty = config.handicap_penalty_arm; break;
            case HitArea.Stomach:
                penalty = config.handicap_penalty_stomach; break;
            case HitArea.Leg:
                penalty = config.handicap_penalty_leg; break;
            case HitArea.Hand:
                penalty = config.handicap_penalty_hand; break;
            case HitArea.Foot:
                penalty = config.handicap_penalty_foot; break;
            default:
                penalty = config.handicap_penalty_generic; break;
            }

            if((item != null) && weapons.ContainsKey(item.info.itemid))
            {
                WeaponStats weapon = weapons[item.info.itemid];

                if(weapon.melee)
                {
                    if((distance > (weapon.range + weapon.attack_size)) && weapon.thrown)
                    {
                        range = 10.0;
                    }
                    else
                    {
                        range = weapon.range + weapon.attack_size;
                    }
                }
                else
                {
                    double mod_zoom = 1.0;

                    if(attacker.IsAiming && (item.contents != null))
                    {
                        foreach(var weapon_mod in item.contents.itemList)
                        {
                            if(weapons.ContainsKey(weapon_mod.info.itemid))
                            {
                                WeaponStats mod = weapons[weapon_mod.info.itemid];

                                if(mod.zoom != 0.0)
                                {
                                    mod_zoom += mod.zoom * 0.0625;

                                    break;
                                }
                            }
                        }
                    }

                    range = weapon.range * mod_zoom * config.handicap_range_normal;
                }
            }

            if((range > 0.0) && (distance > range))
            {
                double distance_mod = distance / range;

                penalty *= distance_mod * distance_mod;
            }

            penalty *= GetSpeedModifier(attacker.estimatedSpeed, victim.estimatedSpeed, config.handicap_movement_rate);

            return penalty;
        }

        internal double GetSpeedModifier(double speed_a, double speed_b, double normal)
        {
            return ((speed_a < normal) ? 1.0 : (speed_a / normal)) * ((speed_b < normal) ? 1.0 : (speed_b / normal));
        }

        internal void HandicapsDecay()
        {
            TimeSpan interval = TimeSpan.FromSeconds((double)config.handicap_decay_time);

            foreach(var entry in db.handicaps)
            {
                if(entry.Value.banned)
                {
                    if(entry.Value.ban_timer != DateTime.MinValue && DateTime.UtcNow >= entry.Value.ban_timer)
                    {
                        AdminUnban("SERVER", entry.Key);
                    }

                    continue;
                }

                var player = BasePlayer.FindByID(entry.Key);

                if(entry.Value.crippled)
                {
                    if(entry.Value.cripple_timer != DateTime.MinValue && DateTime.UtcNow >= entry.Value.cripple_timer)
                    {
                        AdminUncripple("SERVER", entry.Key);
                    }
                }
                else
                {
                    if(entry.Value.damage_amount < config.handicap_damage_max)
                    {
                        var elapsed = DateTime.UtcNow.Subtract(entry.Value.decay_timer);

                        while((elapsed >= interval) && (entry.Value.damage_amount < config.handicap_damage_max))
                        {
                            entry.Value.processDeath(config.handicap_decay_amount);

                            if((player != null) && permission.UserHasPermission(player.UserIDString, PERMISSIONUSE))
                            {
                                ChatMessage(player, m_pvp_damage_increase, DamagePercent(entry.Key));
                            }

                            LogMessage(m_handicap_admin, HandicapInfoConsole(c_status, entry.Key));

                            elapsed -= interval;
                        }
                    }
                }

                if((player != null) && player.IsConnected)
                {
                    entry.Value.online_time = DateTime.UtcNow;
                }
            }
        }

        internal void OnLootHelper(BasePlayer looter, ulong target)
        {
            if(!CanLoot(looter, target))
            {
                NextTick(looter.EndLooting);
            }
        }

        internal bool ParseDurationAndReason(ref string[] args, int offset, out ulong duration, out string reason)
        {
            switch(args.Length - offset)
            {
            case 0:

                duration = 0;  reason = GetMessagePlain(c_ban_no_reason);

                return true;

            case 1:

                if(ulong.TryParse(args[offset], out duration))
                {
                    reason = GetMessagePlain(c_ban_no_reason);
                }
                else
                {
                    duration = 0; reason = args[offset];
                }

                return true;

            case 2:

                if(!ulong.TryParse(args[offset], out duration))
                {
                    goto syntax_error;
                }

                reason = args[offset + 1];

                return true;

            default:

                goto syntax_error;
            }

        syntax_error:

            duration = 0; reason = string.Empty;

            return false;
        }
        
        internal static double TeamDamageModifier(BasePlayer victim, BasePlayer attacker)
        {
            ulong attacker_count = TeamMembersOnline(attacker.currentTeam);

            ulong victim_count = TeamMembersOnline(victim.currentTeam);

            victim_count = (victim_count > 0) ? victim_count : 1;

            if(attacker_count > victim_count)
            {
                return ((double)victim_count) / ((double)attacker_count);
            }

            return 1.0;
        }

        internal static List<ulong> TeamList(ulong userid)
        {
            ulong teamid = 0;

            BasePlayer player = BasePlayer.FindByID(userid);

            if(player != null)
            {
                teamid = player.currentTeam;
            }
            else
            {
                foreach(var team in RelationshipManager.Instance.teams)
                {
                    if(team.Value.members.Contains(userid))
                    {
                        teamid = team.Key;
                    }
                }
            }

            if(teamid != 0)
            {
                return RelationshipManager.Instance.teams[teamid].members;
            }

            return new List<ulong> { userid };
        }
        
        internal static ulong TeamMembersOnline(ulong teamid)
        {
            if((teamid != 0) && teams.ContainsKey(teamid))
            {
                return teams[teamid];
            }
            
            return 0;
        }
        
        internal void TeamMonitor()
        {
            if(!config.general_pvp_team_scale)
            {
                return;
            }

            Dictionary<ulong, ulong> new_teams = new Dictionary<ulong, ulong>();

            foreach(var team in RelationshipManager.Instance.teams)
            {
                ulong count = 0;

                foreach(var userid in team.Value.members)
                {
                    OnPlayerConnected(userid, null);

                    BasePlayer player = BasePlayer.FindByID(userid);

                    if((player != null) && player.IsConnected)
                    {
                        ++count;
                    }
                }

                if(count > 0)
                {
                    new_teams.Add(team.Key, count);
                }
            }

            teams = new_teams;
        }

        internal void WorkTimer()
        {
            HandicapsDecay();

            TeamMonitor();

            timer.Once(5.0f, WorkTimer);
        }

        #endregion

        #region _hooks_

        private object CanLootEntity(BasePlayer looter, DroppedItemContainer target) => CanLootHelper(looter, target.playerSteamID);

        private object CanLootEntity(BasePlayer looter, LootableCorpse target) => CanLootHelper(looter, target.playerSteamID);

        private object CanLootPlayer(BasePlayer target, BasePlayer looter) => CanLootHelper(looter, target.userID);

        private void Init()
        {
            instance = this;

            permission.RegisterPermission(PERMISSIONADMIN, this);
            permission.RegisterPermission(PERMISSIONEXCLUDED, this);
            permission.RegisterPermission(PERMISSIONUSE, this);

            LoadConfiguration();

            DBLoad();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null)
            {
                return;
            }
                
            if(entity is BasePlayer)
            {
                BasePlayer victim = entity.ToPlayer();

                if(!victim.userID.IsSteamId())
                {
                    return;
                }

                if(!attacks.ContainsKey(victim.userID))
                {
                    attacks.Add(victim.userID, new List<AttackInfo>());
                }

                Dictionary<ulong, List<AttackInfo>> attackers = new Dictionary<ulong, List<AttackInfo>>();

                bool kill_excluded = false, kill_pvp = false;

                ulong last_attacker = 0;

                foreach(AttackInfo attackinfo in attacks[victim.userID])
                {
                    if(DateTime.UtcNow.Subtract(attackinfo.time) < TimeSpan.FromSeconds(config.handicap_decay_time))
                    {
                        if(!attackers.ContainsKey(attackinfo.userid))
                        {
                            attackers.Add(attackinfo.userid, new List<AttackInfo>());

                            if(permission.UserHasPermission(attackinfo.userid.ToString(), PERMISSIONEXCLUDED))
                            {
                                kill_excluded = true;

                                break;
                            }

                            kill_pvp = true;
                        }

                        attackers[last_attacker = attackinfo.userid].Add(attackinfo);
                    }
                }

                attacks[victim.userID].Clear();

                if(!kill_pvp || kill_excluded)
                {
                    return;
                }

                foreach(var attacklist in attackers)
                {
                    BasePlayer attacker = BasePlayer.FindByID(attacklist.Key);

                    if((attacker != null) && (attacklist.Value.Count > 0))
                    {
                        ulong hit_count = 0;
                        double hit_penalty = 0.0;

                        foreach(var attack in attacklist.Value)
                        {
                            ++hit_count; hit_penalty += attack.penalty;
                        }

                        hit_penalty /= hit_count;

                        double damage_scale = 1.0 / (1.0 + hit_penalty);

                        if(damage_scale <= config.handicap_warning_limit)
                        {
                            db.handicaps[attacker.userID].warning_time = DateTime.UtcNow;

                            string penalty = (100.0 * (1.0 - damage_scale)).ToString("0.00");

                            ChatMessageAdmin(m_pvp_admin_warning, NameAndID(attacker.userID), penalty);

                            LogMessage(m_pvp_admin_warning, NameAndID(attacker.userID), penalty);

                            if(config.handicap_warning_count != 0)
                            {
                                db.handicaps[attacker.userID].warning_count++;
                            }
                        }

                        if((damage_scale <= config.handicap_cripple_limit) || AutoWarningCountReached(attacker.userID))
                        {
                            AdminCripple("SERVER", attacker.userID, config.handicap_cripple_time);

                            db.handicaps[attacker.userID].warning_count = 0;

                            if(config.handicap_cripple_count != 0)
                            {
                                db.handicaps[attacker.userID].cripple_count++;
                            }
                        }

                        if(AutoCrippleCountReached(attacker.userID))
                        {
                            AdminBan("SERVER", attacker.userID, config.handicap_ban_time, GetMessagePlain(c_auto_ban_cripple_count));

                            if(config.handicap_ban_count != 0)
                            {
                                db.handicaps[attacker.userID].ban_count++;
                            }

                            if(AutoBanCountReached(attacker.userID))
                            {
                                db.handicaps[attacker.userID].ban_timer = DateTime.MinValue;
                            }
                        }

                        if(attacker.IsBuildingAuthed())
                        {
                            hit_penalty *= config.handicap_build_authed;
                        }

                        if(attacker.userID == last_attacker)
                        {
                            hit_penalty *= config.handicap_killer_scale;
                        }
                        else
                        {
                            hit_penalty *= config.handicap_assist_scale;
                        }

                        db.handicaps[attacker.userID].processKill(hit_penalty);

                        if(permission.UserHasPermission(attacker.UserIDString, PERMISSIONUSE))
                        {
                            ChatMessage(attacker, m_pvp_damage_decrease, DamagePercent(attacker.userID));
                        }
                    }
                }

                if(!db.handicaps[victim.userID].crippled)
                {
                    db.handicaps[victim.userID].processDeath(config.handicap_decay_amount);

                    if(permission.UserHasPermission(victim.UserIDString, PERMISSIONUSE))
                    {
                        ChatMessage(victim, m_pvp_damage_increase, DamagePercent(victim.userID));
                    }
                }

                StringBuilder message = new StringBuilder(HandicapInfoConsole(c_victim, victim.userID));

                foreach(var attacklist in attackers)
                {
                    if(attacklist.Key == last_attacker)
                    {
                        message.Append(' ').Append(HandicapInfoConsole(c_killer, attacklist.Key));
                    }
                }

                foreach(var attacklist in attackers)
                {
                    if(attacklist.Key != last_attacker)
                    {
                        message.Append(' ').Append(HandicapInfoConsole(c_assist, attacklist.Key));
                    }
                }

                LogMessage(m_handicap_admin, message.ToString());
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if(entity == null || info == null)
            {
                return null;
            }

            if(entity is BasePlayer)
            {
                BasePlayer victim = entity.ToPlayer();

                if(!victim.userID.IsSteamId() || (victim.lastDamage == DamageType.Bleeding))
                {
                    return null;
                }

                if(!db.handicaps.ContainsKey(victim.userID))
                {
                    OnPlayerConnected(victim);
                }

                BasePlayer attacker = info.InitiatorPlayer ?? info.Initiator?.gameObject?.GetComponent<Fire>()?.Initiator?.ToPlayer();

                if(attacker == null || attacker == victim)
                {
                    return null;
                }
                
                if(attacker.userID.IsSteamId())
                {
                    if(!db.handicaps.ContainsKey(attacker.userID))
                    {
                        OnPlayerConnected(attacker);
                    }

                    if(!permission.UserHasPermission(attacker.UserIDString, PERMISSIONEXCLUDED))
                    {
                        bool cancel_attack = db.handicaps[attacker.userID].crippled;

                        if(!cancel_attack && (config.general_pvp_ff_enabled == false) && (victim.currentTeam != 0))
                        {
                            cancel_attack = (victim.currentTeam == attacker.currentTeam);
                        }

                        if(config.general_pvp_enabled && !cancel_attack)
                        {
                            if(!attacks.ContainsKey(victim.userID))
                            {
                                attacks.Add(victim.userID, new List<AttackInfo>());
                            }

                            attacks[victim.userID].Add(new AttackInfo
                            {
                                time    = DateTime.UtcNow,
                                userid  = attacker.userID,
                                penalty = GetHitPenalty(attacker, victim, info)
                            });

                            double damage_amount = db.handicaps[attacker.userID].damage_amount;

                            if(config.general_pvp_team_scale)
                            {
                                damage_amount *= TeamDamageModifier(victim, attacker);
                            }

                            info.damageTypes.ScaleAll((float)damage_amount);
                        }
                        else
                        {
                            info.damageTypes  = new DamageTypeList();
                            info.DidHit       = false;
                            info.HitBone      = 0;
                            info.DoHitEffects = false;
                            info.HitEntity    = null;
                            info.HitMaterial  = 0;
                            info.PointStart   = Vector3.zero;

                            return true;
                        }
                    }
                }
            }

            return null;
        }

        private void OnFireBallDamage(FireBall fireball, BaseCombatEntity target, HitInfo info)
        {
            info.Initiator = fireball;
        }

        private void OnFireBallSpread(FireBall fireball, BaseEntity entity)
        {
            entity.gameObject.AddComponent<Fire>().Initiator = fireball.GetComponent<Fire>()?.Initiator;
        }

        private void OnFlameExplosion(FlameExplosive explosive, BaseEntity entity)
        {
            entity.gameObject.AddComponent<Fire>().Initiator = explosive.creatorEntity;
        }

        private void OnFlameThrowerBurn(FlameThrower flamethrower, BaseEntity entity)
        {
            entity.gameObject.AddComponent<Fire>().Initiator = flamethrower.GetOwnerPlayer();
        }

        private void OnLootEntity(BasePlayer looter, BaseEntity target)
        {
            if(target is LootableCorpse)
            {
                OnLootHelper(looter, (target as LootableCorpse).playerSteamID);
            }
            else if(target is DroppedItemContainer)
            {
                OnLootHelper(looter, (target as DroppedItemContainer).playerSteamID);
            }
        }

        private void OnLootPlayer(BasePlayer looter, BasePlayer target) => OnLootHelper(looter, target.userID);

        object OnMeleeAttack(BasePlayer attacker, HitInfo info)
        {
            if(!attacker.userID.IsSteamId() || !(info.HitEntity is BasePlayer))
            {
                return null;
            }

            bool cancel_attack = false;

            var victim = info.HitEntity as BasePlayer;

            if(victim.userID.IsSteamId())
            {
                if(!db.handicaps.ContainsKey(attacker.userID))
                {
                    OnPlayerConnected(attacker);
                }
                else
                {
                    cancel_attack = db.handicaps[attacker.userID].crippled;
                }
            }

            if(!cancel_attack)
            {
                double range = 0.0;

                Item item = info?.Weapon?.GetItem();

                if((item != null) && weapons.ContainsKey(item.info.itemid))
                {
                    WeaponStats weapon = weapons[item.info.itemid];

                    if(weapon.melee)
                    {
                        range = weapon.range + weapon.attack_size;
                    }
                }

                if(attacker.Distance(info.HitEntity) <= range)
                {
                    Ray ray = new Ray(attacker.eyes.position, Quaternion.Euler(attacker.eyes.HeadRay().direction) * Vector3.forward);

                    RaycastHit hit;

                    if(Physics.Raycast(ray, out hit, (float)range))
                    {
                        if(hit.collider.GetComponentInParent<BaseEntity>() == info.HitEntity)
                        {
                            return null;
                        }
                    }
                }
            }

            info.damageTypes = new DamageTypeList();
            info.DoHitEffects = false;

            return true;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            OnPlayerConnected(player.userID, player.displayName);

            if(!player.IsConnected)
            {
                return;
            }

            db.handicaps[player.userID].online_time = DateTime.UtcNow;

            int port_separator = player.Connection.ipaddress.IndexOf(':');

            if(port_separator < 0)
            {
                port_separator = player.Connection.ipaddress.Length;
            }

            string ip_address = player.Connection.ipaddress.Substring(0, port_separator);

            if(!db.ip_table.ContainsKey(ip_address))
            {
                db.ip_table.Add(ip_address, new AddressInfo
                {
                    userid    = new List<ulong> { player.userID },
                    whitelist = false
                });
            }
            else
            {
                if(!db.ip_table[ip_address].userid.Contains(player.userID))
                {
                    db.ip_table[ip_address].userid.Add(player.userID);
                }

                if(!db.ip_table[ip_address].whitelist)
                {
                    foreach(var alt_userid in db.ip_table[ip_address].userid)
                    {
                        if((player.userID != alt_userid) && db.handicaps.ContainsKey(alt_userid))
                        {
                            BasePlayer alt_player = BasePlayer.FindByID(alt_userid);

                            if(alt_player != null && alt_player.IsConnected)
                            {
                                db.ip_table[ip_address].whitelist = true;

                                break;
                            }
                        }
                    }
                }

                if(!db.ip_table[ip_address].whitelist)
                {
                    foreach(var alt_userid in db.ip_table[ip_address].userid)
                    {
                        if((player.userID != alt_userid) && db.handicaps.ContainsKey(alt_userid))
                        {
                            if(db.handicaps[alt_userid].banned)
                            {
                                db.handicaps[player.userID].banned = true;

                                if(db.handicaps[alt_userid].ban_timer == DateTime.MinValue)
                                {
                                    db.handicaps[player.userID].ban_timer = DateTime.MinValue;
                                }
                                else if(db.handicaps[player.userID].ban_timer < db.handicaps[alt_userid].ban_timer)
                                {
                                    db.handicaps[player.userID].ban_timer = db.handicaps[alt_userid].ban_timer;
                                }
                            }

                            if(db.handicaps[player.userID].ban_count < db.handicaps[alt_userid].ban_count)
                            {
                                db.handicaps[player.userID].ban_count = db.handicaps[alt_userid].ban_count;
                            }

                            if(db.handicaps[alt_userid].crippled)
                            {
                                db.handicaps[player.userID].crippled = true;

                                if(db.handicaps[alt_userid].cripple_timer == DateTime.MinValue)
                                {
                                    db.handicaps[player.userID].cripple_timer = DateTime.MinValue;
                                }
                                else if(db.handicaps[player.userID].cripple_timer < db.handicaps[alt_userid].cripple_timer)
                                {
                                    db.handicaps[player.userID].cripple_timer = db.handicaps[alt_userid].cripple_timer;
                                }
                            }

                            if(db.handicaps[player.userID].damage_amount > db.handicaps[alt_userid].damage_amount)
                            {
                                db.handicaps[player.userID].damage_amount = db.handicaps[alt_userid].damage_amount;
                            }

                            if(db.handicaps[player.userID].decay_timer < db.handicaps[alt_userid].decay_timer)
                            {
                                db.handicaps[player.userID].decay_timer = db.handicaps[alt_userid].decay_timer;
                            }
                        }
                    }
                }
            }

            if(db.handicaps[player.userID].banned)
            {
                AdminBanInherited("SERVER", player.userID);
            }

            if(permission.UserHasPermission(player.UserIDString, PERMISSIONUSE))
            {
                ChatMessage(player, m_pvp_damage_modifier, DamagePercent(player.userID));
            }
        }

        private void OnPlayerConnected(ulong userid, string name)
        {
            name = name ?? covalence.Players.FindPlayerById(userid.ToString())?.Name ?? string.Empty;

            if(!db.handicaps.ContainsKey(userid))
            {
                db.handicaps.Add(userid, new Handicap
                {
                    display_name  = name,
                    damage_amount = config.handicap_damage_max,
                    decay_timer   = DateTime.UtcNow,
                    online_time   = DateTime.MinValue,
                    warning_count = 0,
                    warning_time  = DateTime.MinValue,
                    cripple_count = 0,
                    cripple_timer = DateTime.MinValue,
                    crippled      = false,
                    ban_count     = 0,
                    ban_timer     = DateTime.MinValue,
                    banned        = false
                });
            }
            else
            {
                db.handicaps[userid].display_name = name;
            }
        }

        private void OnServerInitialized()
        {
            foreach(var player in BasePlayer.activePlayerList.ToList()) OnPlayerConnected(player);

            foreach(var player in BasePlayer.sleepingPlayerList.ToList()) OnPlayerConnected(player);

            foreach(var player in covalence.Players.All.ToList())
            {
                if(covalence.Server.IsBanned(player.Id))
                {
                    ulong userid = ulong.Parse(player.Id);

                    if(db.handicaps.ContainsKey(userid) && !db.handicaps[userid].banned)
                    {
                        db.handicaps[userid].cripple_count = 0;
                        db.handicaps[userid].cripple_timer = DateTime.MinValue;
                        db.handicaps[userid].warning_count = 0;
                        db.handicaps[userid].banned        = true;
                    }
                }
            }

            WorkTimer();
        }

        private void OnServerSave()
        {
            DBStore();

            SaveConfiguration();
        }

        private void OnUserBanned(string name, string userid_string, string ip, string reason)
        {
            ulong userid = ulong.Parse(userid_string);

            OnPlayerConnected(userid, name);

            if(!db.handicaps[userid].banned)
            {
                db.handicaps[userid].warning_count = 0;
                db.handicaps[userid].cripple_count = 0;
                db.handicaps[userid].cripple_timer = DateTime.MinValue;
                db.handicaps[userid].banned        = true;
            }
        }

        private void OnUserUnbanned(string name, string userid_string, string ip)
        {
            ulong userid = ulong.Parse(userid_string);

            OnPlayerConnected(userid, name);

            if(db.handicaps[userid].banned)
            {
                db.handicaps[userid].damage_amount = config.handicap_damage_max;
                db.handicaps[userid].decay_timer   = DateTime.UtcNow;
                db.handicaps[userid].warning_count = 0;
                db.handicaps[userid].cripple_count = 0;
                db.handicaps[userid].cripple_timer = DateTime.MinValue;
                db.handicaps[userid].crippled      = false;
                db.handicaps[userid].ban_count     = 0;
                db.handicaps[userid].ban_timer     = DateTime.MinValue;
                db.handicaps[userid].banned        = false;
            }
        }

        private void OnWeaponFired(BaseProjectile weapon, BasePlayer attacker, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if((attacker == null) || !attacker.userID.IsSteamId())
            {
                return;
            }

            if(!db.handicaps.ContainsKey(attacker.userID))
            {
                OnPlayerConnected(attacker);

                return;
            }

            if(!db.handicaps[attacker.userID].crippled)
            {
                return;
            }

            var ray = new Ray(attacker.eyes.position, Quaternion.Euler(attacker.eyes.HeadRay().direction) * Vector3.forward);

            RaycastHit hit;

            if(Physics.Raycast(ray, out hit, weapon.effectiveRange))
            {
                var destination = hit.collider.GetComponentInParent<BaseEntity>();

                List<BaseEntity> list = Pool.GetList<BaseEntity>();
                Vis.Entities(destination.transform.position, 10.0f, list);

                foreach(var entity in list)
                {
                    var victim = entity as BasePlayer;

                    if((victim != null) && victim.userID.IsSteamId())
                    {
                        foreach(var projectile in projectiles.projectiles)
                        {
                            projectile.startVel = Vector3.zero;
                        }

                        return;
                    }
                }
            }
        }

        private void Unload()
        {
            DBStore();

            SaveConfiguration();
        }

        #endregion
    }
}
