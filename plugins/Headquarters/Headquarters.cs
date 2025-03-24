using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Headquarters", "digital0iced0", "1.2.0")]
    [Description("Allows players to have one protected headquarter base.")]
    public class Headquarters : RustPlugin
    {
        #region Declaration

        private static PluginData _data;

        protected static ConfigFile _config;

        private bool _freeForAllActive = false;

        private bool _hooksSubscribed = false;

        private Timer _utilityTimer;

        private CuiElementContainer _cachedUI;

        // Permissions
        private const string AdminPermissionName = "headquarters.admin";

        private static readonly string[] StorageTypesPenalizeModules = {
            "2module_camper",
            "1module_storage",
        };

        private static readonly string[] StorageTypes = {
            "skull_fire_pit",
            "bbq.deployed",
            "dropbox.deployed",
            "stocking_small_deployed",
            "campfire",
            "furnace.large",
            "furnace",
            "coffinstorage",
            "box.wooden.large",
            "small_stash_deployed",
            "refinery_small_deployed",
            "cupboard.tool.deployed",
            "vendingmachine.deployed",
            "woodbox_deployed",
            "locker.deployed",
        };

        #endregion

        #region Config

        protected class ConfigFile
        {
            public HeadquartersConfig HeadquartersConfig;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    HeadquartersConfig = new HeadquartersConfig(),
                };
            }
        }

        protected class HeadquartersConfig
        {
            public float Radius { get; set; } = 27.5f;

            public bool MapMarkersEnabled { get; set; } = true;

            public bool TeleportEnabled { get; set; } = false;

            public float QuitPenaltyHours { get; set; } = 3f;

            public float DistanceToTC { get; set; } = 2f;

            public bool InvulnerableTC { get; set; } = true;

            public bool ConquerModeEnabled { get; set; } = false;

            public bool FreeForAllEnabled { get; set; } = true;

            public float FreeForAllHoursAfterWipe { get; set; } = 144f;

            public string MarkerPrefab { get; set; } = "assets/prefabs/tools/map/genericradiusmarker.prefab";

            public float ProtectionPercent { get; set; } = 100f;

            public float ProtectionPercentMinimum { get; set; } = 10f;

            public float ProtectionSlotsWithoutPenalty { get; set; } = 30f;

            public float ProtectionPenaltyPercentPerSlot { get; set; } = 1.5f;

            public int ProtectionConstantSecondsAfterDamage { get; set; } = 300;

            public bool MessagePlayersHeadquarterAttacked { get; set; } = true;

            public bool MessagePlayersHeadquarterDestroyed { get; set; } = true;

            public bool UIEnabled { get; set; } = true;

            public int UIRefreshRateSeconds { get; set; } = 0;

            public Anchor UIAnchorMin { get; set; } = new Anchor(0.75f, 0.92f);

            public Anchor UIAnchorMax { get; set; } = new Anchor(0.98f, 0.98f);

            public string[] AdditionalProtectedEntities { get; set; } = new string[] {
              "window",
              "barricade",
              "turret",
              "cctvcamera",
              "dropbox",
              "mailbox",
              "lantern",
              "sign",
            };
        }

        protected class Anchor
        {
            public float X { get; set; }
            public float Y { get; set; }

            public Anchor()
            {
            }

            public Anchor(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        protected static HeadquartersConfig getConfig()
        {
            return _config.HeadquartersConfig;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            _config = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Helper Classes
        private class PluginData
        {
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Headquarter> AvailableHeadquarters = new Dictionary<string, Headquarter>() { };

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, HeadquarterMember> MemberPlayers = new Dictionary<string, HeadquarterMember>() { };

            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, HeadquarterQuitter> QuitterPlayers = new Dictionary<string, HeadquarterQuitter>() { };
        }

        private class HeadquarterQuitter
        {
            public string UserId { get; }

            public DateTime QuitStartedAt { get; set; }

            public HeadquarterQuitter(string user)
            {
                this.UserId = user;
                this.QuitStartedAt = DateTime.Now;
            }
        }

        private class HeadquarterMember
        {
            public string UserId { get; }

            // This is the id of the leader of the headquarter this player belongs to
            public string LeaderId { get; set; }

            public HeadquarterMember(string user, string leader)
            {
                this.UserId = user;
                this.LeaderId = leader;
            }
        }

        private class Headquarter
        {
            public string LeaderId { get; set; }
            public string Name { get; set; }
            public int StorageSlots { get; set; }
            public float PositionX { get; }
            public float PositionY { get; }
            public float PositionZ { get; }
            public bool IsActive { get; set; }
            public List<string> MemberIds { get; set; } = new List<string>();
            [JsonIgnore]
            public MapMarkerGenericRadius marker;
            public DateTime DismantleStartedAt { get; set; }
            public float LastKnownProtectionPercent { get; set; } = 1;
            public bool MapMarkerEnabled { get; set; }
            public DateTime LastMarkerRefresh { get; set; }
            public DateTime LastDamaged { get; set; }
            public DateTime LastUIUpdate { get; set; }
            [JsonIgnore]
            private Headquarters Instance;

            public Headquarter(string user, string name, float positionX, float positionY, float positionZ, int storageSlots = 0, bool mapMarkerEnabled = true)
            {
                this.LeaderId = user;
                this.Name = name;
                this.PositionX = positionX;
                this.PositionY = positionY;
                this.PositionZ = positionZ;
                this.IsActive = true;
                this.StorageSlots = storageSlots;
                this.MapMarkerEnabled = mapMarkerEnabled;
                this.LastDamaged = DateTime.Now.AddDays(-1);
                this.LastMarkerRefresh = DateTime.Now.AddDays(-1);
                this.LastUIUpdate = DateTime.Now.AddDays(-1);
                this.CreateMapMarker();
            }

            public void SetInstance(Headquarters instance)
            {
                this.Instance = instance;
            }

            public bool HasMember(string user)
            {
                return user == this.LeaderId || this.MemberIds.Contains(user);
            }

            public Vector3 getPosition()
            {
                return new Vector3(this.PositionX, this.PositionY, this.PositionZ);
            }

            public void MarkDamaged()
            {

                bool forceRefreshUI = false;

                if (!IsBeingRaided())
                {
                    forceRefreshUI = true;
                }

                this.LastDamaged = DateTime.Now;

                if (forceRefreshUI)
                {
                    RefreshUI(true);
                }
            }

            public void MarkUIUpdated()
            {
                this.LastUIUpdate = DateTime.Now;
            }

            public bool ShouldUpdateUI()
            {
                return DateTime.Now.Subtract(LastUIUpdate).TotalSeconds >= Headquarters.getConfig().UIRefreshRateSeconds;
            }

            public void CreateMapMarker(bool freeForAllActive = false)
            {
                if (marker != null || !MapMarkerEnabled)
                {
                    return;
                }

                marker = GameManager.server.CreateEntity(_config.HeadquartersConfig.MarkerPrefab, getPosition()) as MapMarkerGenericRadius;

                if (marker != null)
                {
                    marker.alpha = 0.6f;
                    marker.name = this.Name;

                    if (IsActive)
                    {
                        marker.color1 = (freeForAllActive) ? Color.red : Color.yellow;
                        marker.color2 = (freeForAllActive) ? Color.red : getProtectionColor();
                    }
                    else
                    {
                        marker.color1 = Color.black;
                        marker.color2 = Color.black;
                    }

                    marker.radius = 0.2f;
                    marker.Spawn();
                    marker.SendUpdate();
                }
            }

            public void RemoveMapMarker()
            {
                if (!MapMarkerEnabled)
                {
                    return;
                }

                if (marker != null)
                {
                    marker.Kill();
                    marker.SendUpdate();
                    marker.SendNetworkUpdate();
                    UnityEngine.Object.Destroy(marker);
                    marker = null;
                }
            }

            public void UpdateMapMarker()
            {
                if (!MapMarkerEnabled)
                {
                    return;
                }

                if (marker != null)
                {
                    marker.SendNetworkUpdate();
                    marker.SendUpdate();
                }
            }

            public void RefreshMapMarker(bool freeForAllActive = false, bool forceUpdate = false)
            {
                if (!MapMarkerEnabled)
                {
                    return;
                }

                if (forceUpdate || DateTime.Now.Subtract(LastMarkerRefresh).TotalSeconds > 10)
                {
                    RemoveMapMarker();
                    CreateMapMarker(freeForAllActive);
                    this.LastMarkerRefresh = DateTime.Now;
                }
            }

            private Color getProtectionColor()
            {
                if (!IsActive)
                {
                    return Color.red;
                }

                if (LastKnownProtectionPercent > .8)
                {
                    return Color.green;
                }
                else if (LastKnownProtectionPercent > .55)
                {
                    return Color.yellow;
                }
                else if (LastKnownProtectionPercent > .3)
                {
                    return new Color(1f, .65f, 0f, 1f); // Orange
                }
                else
                {
                    return Color.red;
                }
            }

            public void RecalculateProtectionScale()
            {
                if (IsBeingRaided())
                {
                    RefreshUI();
                    return;
                }

                this.LastKnownProtectionPercent = GetCurrentProtectionPercent();

                RefreshUI();
            }

            public void RefreshUI(bool forceRefresh = false)
            {
                HeadquartersConfig c = Headquarters.getConfig();

                if (Instance != null && (forceRefresh || (c.UIEnabled && ShouldUpdateUI())))
                {
                    List<string> playerIds = new List<string>();

                    playerIds.Add(LeaderId);
                    playerIds.AddRange(MemberIds);

                    playerIds.ForEach(delegate (string playerId)
                    {
                        ulong check;

                        if (ulong.TryParse(playerId, out check))
                        {
                            BasePlayer p = BasePlayer.FindByID(check);
                            if (p != null)
                            {
                                Instance.RefreshUIForPlayer(p, this);
                            }
                        }
                    });
                }
            }

            public void RemoveUI()
            {
                if (Instance == null)
                {
                    return;
                }

                List<string> playerIds = new List<string>();

                playerIds.Add(LeaderId);
                playerIds.AddRange(MemberIds);

                playerIds.ForEach(delegate (string playerId)
                {
                    ulong check;

                    if (ulong.TryParse(playerId, out check))
                    {
                        BasePlayer p = BasePlayer.FindByID(check);
                        if (p != null)
                        {
                            Instance.RemoveUIForPlayer(p);
                        }
                    }
                });
            }

            public bool IsBeingRaided()
            {
                return DateTime.Now.Subtract(LastDamaged).TotalSeconds < Headquarters.getConfig().ProtectionConstantSecondsAfterDamage;
            }

            public bool IsDismantling()
            {
                return DateTime.Now.Subtract(DismantleStartedAt).Hours < Headquarters.getConfig().QuitPenaltyHours;
            }

            public float GetCurrentProtectionPercent()
            {
                if (!IsActive)
                {
                    return 0;
                }

                HeadquartersConfig c = Headquarters.getConfig();

                return Mathf.Min(c.ProtectionPercent, Mathf.Max((c.ProtectionPercent - ((this.StorageSlots - c.ProtectionSlotsWithoutPenalty) * c.ProtectionPenaltyPercentPerSlot)), c.ProtectionPercentMinimum)) / 100;
            }
        }

        private class Rgba
        {
            public float R { get; set; }
            public float G { get; set; }
            public float B { get; set; }
            public float A { get; set; }

            public Rgba()
            {
            }

            public Rgba(float r, float g, float b, float a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public static string Format(Rgba rgba)
            {
                return $"{rgba.R / 255} {rgba.G / 255} {rgba.B / 255} {rgba.A}";
            }
        }

        private class UI
        {
            public const string MainContainer = "main_container";
            public const string ProtectionContainer = "protection_container";

            public static Rgba PrimaryColor = new Rgba(109, 141, 187, .4f);
            public static Rgba LightColor = new Rgba(255, 255, 255, .6f);
            public static Rgba TextColor = new Rgba(255, 255, 255, .5f);

            public static CuiElementContainer Container(string name, string bgColor, Anchor Min, Anchor Max,
                string parent = "Hud", float fadeOut = 0f, float fadeIn = 0f)
            {
                var newElement = new CuiElementContainer()
                {
                    new CuiElement()
                    {
                        Name = name,
                        Parent = parent,
                        FadeOut = fadeOut,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = bgColor,
                                FadeIn = fadeIn
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = $"{Min.X} {Min.Y}",
                                AnchorMax = $"{Max.X} {Max.Y}"
                            }
                        }
                    },
                };
                return newElement;
            }

            public static void Text(string name, string parent, ref CuiElementContainer container, TextAnchor anchor,
                string color, int fontSize, string text,
                Anchor Min, Anchor Max, string font = "robotocondensed-regular.ttf", float fadeOut = 0f,
                float fadeIn = 0f)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = text,
                            Align = anchor,
                            FontSize = fontSize,
                            Font = font,
                            FadeIn = fadeIn,
                            Color = color
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static void Element(string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max,
                string bgColor, float fadeOut = 0f, float fadeIn = 0f)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = fadeOut,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = bgColor,
                            Material = "",
                            FadeIn = fadeIn
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static void Image(string name, string parent, ref CuiElementContainer container, Anchor Min, Anchor Max, string img, string color)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Url = img,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Color = color,
                            Material = "Assets/Icons/IconMaterial.mat"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{Min.X} {Min.Y}",
                            AnchorMax = $"{Max.X} {Max.Y}"
                        }
                    }
                });
            }

            public static CuiElementContainer GetCachedUI(HeadquartersConfig c)
            {
                CuiElementContainer cached = Container(MainContainer, "0 0 0 0.1", c.UIAnchorMin, c.UIAnchorMax);

                Element("Icon_Element", MainContainer, ref cached, new Anchor(0f, 0f), new Anchor(0.2f, 1f), Rgba.Format(PrimaryColor));

                Element("Icon_Padded", "Icon_Element", ref cached, new Anchor(0.25f, 0.2f), new Anchor(0.9f, 0.8f), "0 0 0 0");

                Image("Icon_Image", "Icon_Padded", ref cached, new Anchor(0f, 0f), new Anchor(1f, 1f), "https://assets.umod.org/images/icons/plugin/62503f99a1307.png", Rgba.Format(LightColor));

                return cached;
            }

            public static CuiElementContainer GetProtectionUI(HeadquartersConfig c, Headquarter hq, BasePlayer player, Headquarters instance)
            {
                string titleText;
                string titleAmount;

                if (instance._freeForAllActive)
                {
                    titleText = instance.Lang("Headquarter_UI_Free_For_All", player.UserIDString);
                    titleAmount = "";
                }
                else if (!hq.IsActive)
                {
                    titleText = instance.Lang("Headquarter_UI_Dismantling", player.UserIDString);
                    titleAmount = "";
                }
                else
                {
                    float lastKnownPercent = hq.LastKnownProtectionPercent;

                    bool isBeingRaided = hq.IsBeingRaided();

                    titleText = isBeingRaided ? instance.Lang("Headquarter_UI_Raid_Locked", player.UserIDString) : instance.Lang("Headquarter_UI_Protection", player.UserIDString);
                    titleAmount = lastKnownPercent.ToString("p0");
                }

                CuiElementContainer protection = Container(ProtectionContainer, "0 0 0 0.1", new Anchor(c.UIAnchorMin.X + .001f, c.UIAnchorMin.Y), c.UIAnchorMax);

                Element("Title_Element", ProtectionContainer, ref protection, new Anchor(.2f, 0f), new Anchor(1f, 1f), Rgba.Format(PrimaryColor));
                Element("Title_Padded", "Title_Element", ref protection, new Anchor(0.05f, 0.05f), new Anchor(0.95f, 0.95f), "0 0 0 0");

                Text("Title_Protection", "Title_Padded", ref protection, TextAnchor.MiddleLeft, Rgba.Format(TextColor), 10, titleText, new Anchor(0f, .2f),
new Anchor(1f, .5f), "robotocondensed-bold.ttf");
                Text("Amount_Protection", "Title_Padded", ref protection, TextAnchor.MiddleLeft, Rgba.Format(TextColor), 10, titleAmount, new Anchor(.80f, .2f),
                    new Anchor(1f, .5f), "robotocondensed-bold.ttf");

                Text("Title_Slots", "Title_Padded", ref protection, TextAnchor.MiddleLeft, Rgba.Format(TextColor), 10, instance.Lang("Headquarter_UI_Storage_Slots", player.UserIDString), new Anchor(0f, .5f),
                    new Anchor(1f, .8f), "robotocondensed-bold.ttf");
                Text("Amount_Slots", "Title_Padded", ref protection, TextAnchor.MiddleLeft, Rgba.Format(TextColor), 10, hq.StorageSlots.ToString(), new Anchor(.80f, .5f),
                    new Anchor(1f, .8f), "robotocondensed-bold.ttf");

                return protection;
            }
        }
        #endregion


        #region Lang

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Headquarter_Init"] = "This server is running Headquarters mod.  It allows you to provide added defense to one of your bases.  For more details type /hq.help in chat",
                ["Headquarter_Protected_NoDamage"] = "This base is under the protection of a HQ.  It can't be damaged at this time.",
                ["Headquarter_Exists_Cant_Clear_List"] = "A HQ exists at this location.  You can't clear its privilege list.",
                ["Headquarter_Exists_Cant_Deauth"] = "A HQ exists at this location.  You can't deauthorize from it's Tool Cupboard.",
                ["Headquarter_Inside_Headquarter"] = "You can't create a HQ inside another HQ.",
                ["Headquarter_Not_Inside"] = "You're not inside a HQ.",
                ["Headquarter_Start_Near_TC"] = "You must stand next to your base's Tool Cupboard to start a HQ.",
                ["Headquarter_Successful_Start"] = "You have started a HQ at this base! You can invite others to join your HQ by having them authenticate at the Tool Cupboard.  To keep your HQ base secure, put a lock on your Tool Cupboard.",
                ["Headquarter_Already_Started"] = "You have already started or joined a HQ.  This disqualifies you from creating a new HQ. However, you can quit it, and after the penalty period you will be able to start another HQ.",
                ["Headquarter_Leader_Quit_Promoted"] = "Your HQ will not be dismantled.  Instead, one of its members will become its new leader.  As for your fate, you have been removed from the headquarter and deauthed from its Tool Cupboard.  You now have the mark of a quitter.  You will not be able to join or start a HQ for some time.  You can learn more by typing /hq.status.",
                ["Headquarter_Leader_Quit_Empty"] = "Your HQ is being dismantled.  Since you were its only member you will keep access to the base.  However, you will not be able to join or start a HQ for some time.  You can learn more by typing /hq.status.",
                ["Headquarter_Only_Leader"] = "Only the leader of a HQ can perform this action.",
                ["Headquarter_Found_Count"] = "Found {0} HQs.",
                ["Headquarter_Near_Found"] = "Headquarter {0} near {1}.",
                ["Headquarter_Name_Exists"] = "This name is already taken.  Please try another.",
                ["Headquarter_Already_Member"] = "You're already a member of this HQ.",
                ["Headquarter_Cleared"] = "All HQs have been removed.  Protections are disabled.",
                ["Headquarter_Here_Protection_Rating"] = "You're in {0}'s HQ! ({1}%).",
                ["Headquarter_Protection_Max_Min"] = "Maximum protection offered: {0} - Minimum: {1}",
                ["Headquarter_Protection_Slots"] = "Storage slots allowed without penalty: {0} - Penalty per additional slot utilized: {1}%",
                ["Headquarter_Protection_Raid_Lock_Seconds"] = "Protection is locked at the start of an attack.  It will remain locked for {0} seconds after the last attack.",
                ["Headquarter_Cant_Auth"] = "A HQ exists at this location and conqueror mode is disabled, therefore you can't authorize here.  If you wish to join this HQ, you must first quit your current HQ and suffer through the penalty period before you may do so.",
                ["Headquarter_Quitter"] = "You have quit your HQ and are now a deserter.  You will not be able to join or start a new HQ for some time.   You can learn more by typing /hq.status.",
                ["Headquarter_Quit_In_Progress"] = "Your reputation is still tarnished from quitting your previous headquarter.  Wait a while longer and try again.  You can learn more by typing /hq.status.",
                ["Headquarter_Conquered"] = "{0} has fallen!  It has been conquered by {1}!",
                ["Headquarter_Not_Member"] = "You are not in a headquarter.",

                ["Headquarter_Empty_Here"] = "There isn't a HQ at this position.",
                ["Headquarter_Require_Name"] = "You must provide a name for your HQ.",
                ["Headquarter_Deployable_Blocked"] = "You can't deploy this item inside someone else's HQ.",
                ["Headquarter_Dismantle_In_Progress"] = "The leader of your headquarter has previously initiated the dismantle process by performing a /hq.quit.  This process takes some time.  While its ongoing you can't start or join another HQ.",
                ["Headquarter_Being_Attacked"] = "Allies of {0}, the time to honor your alliance has come!  We're being attacked by {1}!",

                ["Headquarter_UI_Storage_Slots"] = "STORAGE SLOTS",
                ["Headquarter_UI_Protection"] = "PROTECTION",
                ["Headquarter_UI_Raid_Locked"] = "RAID LOCKED",
                ["Headquarter_UI_Free_For_All"] = "FREE FOR ALL",
                ["Headquarter_UI_Dismantling"] = "DISMANTLING",

                ["Free_For_All_Active"] = "<color=green>HQ free for all is active! HQ protections are disabled!</color>",
                ["Free_For_All_Stopped"] = "<color=red>HQ free for all is deactivated!</color>",
                ["Free_For_All_Status"] = "HQ free for all is expected {0}",
                ["Free_For_All_Only_Admin"] = "HQ free for all is deactivated.  Only an admin can enable it.",

                ["Cmd_Permission"] = "You don't have permission to use that command",
                ["Cmd_Remove_Heaquarter_Leader_Missing"] = "Please provide the user ID (numeric) of the player whose HQ you wish to remove.",
                ["Cmd_Headquarter_Removed"] = "The leader's HQ has been removed.",
                ["Cmd_Headquarter_Remove_Fail"] = "Could not find a HQ belonging to this player ID (numeric).",
                ["Cmd_Remove_Quitter_Player_Missing"] = "Remove quitter requires the player's ID (numeric).",
                ["Cmd_Remove_Quitter_Removed"] = "Quitter was removed successfully.",
                ["Cmd_Remove_Quitter_Fail"] = "Could not find a quitter with the given player ID (numeric).",
                ["Cmd_Cleared_Quitters"] = "Cleared all quitters.",

                ["Help_Welcome"] = "Welcome to Headquarters! This mod allows you to provide protection for one of your bases by designating it your headquarter (HQ).",
                ["Help_Details"] = "A few simple things to keep in mind: You can only belong to one HQ at any given time. You can switch HQ by authenticating at someone else's TC but you will lose your previous HQ.  If you place too many items inside your HQ it will reduce its protection level.  Removing items from the HQ will increase its protection again.",
                ["Help_Raid"] = "You can raid headquarters and regular bases.  However, it may not be worth it to raid a headquarter with a high protection level.  Conquer mode is {0}.",
                ["Help_Start"] = "Starts a named HQ at one of your bases' Tool Cupboard.",
                ["Help_Start_Name"] = "(name)",
                ["Help_Quit"] = "Quits your current HQ.  You will not be able to join or start a new HQ for {0} hours.",
                ["Help_FFA"] = "Provides details on how long until free for all is activated.",
                ["Help_Status"] = "Tells you your personal status.  Lets you know if there is a HQ nearby (and its protection level).  It also lets you know relevant Headquarters settings on this server.",

                ["Status_Dismantling"] = "Your previous headquarter is being dismantled.  This may have happened if it was conquered or the leader dismantled it. You will be able to start or join another HQ {0}",
                ["Status_Leader_Of_Headquarter"] = "You are the leader of {0}.",
                ["Status_Member_Of_Headquarter"] = "You are a member of {0}.",
                ["Status_Quitter"] = "Word still spreads of your dishonor in quitting a headquarter.  You will be able to start or join another HQ {0}",
                ["Status_Not_In_Headquarter"] = "You are not in a HQ!",

                ["Disband_Quit"] = "The /hq.disband command has been removed.  Please utilize /hq.quit instead for both members and leaders.",

                ["Time_In_Hours"] = "in approximately {0} hours.",
                ["Time_In_Minutes"] = "in approximately {0} minutes.",
                ["Time_Soon"] = "any moment now!",
                ["Protected"] = "protected",
                ["Unprotected"] = "unprotected",
                ["Enabled"] = "enabled",
                ["Disabled"] = "disabled",
            }, this);
        }

        #endregion

        #region Helper Methods

        string GetGrid(Vector3 pos)
        {
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(((int)letter) + x);
            return $"{letter}{z}";
        }

        private Headquarter GetPlayerHeadquarter(BasePlayer player)
        {
            if (IsLeader(player))
            {
                return _data.AvailableHeadquarters[player.UserIDString];
            }
            else if (IsMember(player))
            {
                var leaderId = _data.MemberPlayers[player.UserIDString].LeaderId;

                if (_data.AvailableHeadquarters.ContainsKey(leaderId))
                {
                    return (_data.AvailableHeadquarters[leaderId]);
                }
            }

            return null;
        }

        private BuildingPrivlidge GetHeadquarterTC(Headquarter hq)
        {
            List<BaseCombatEntity> cblist = new List<BaseCombatEntity>();
            Vis.Entities<BaseCombatEntity>(hq.getPosition(), _config.HeadquartersConfig.DistanceToTC, cblist);

            foreach (BaseCombatEntity bp in cblist.Distinct().ToList())
            {
                if (bp is BuildingPrivlidge)
                {
                    return (BuildingPrivlidge)bp;
                }
            }

            return null;
        }

        private void DismantleLeaderHQ(BasePlayer player)
        {
            var hq = _data.AvailableHeadquarters[player.UserIDString];

            DismantleHQ(hq);
        }

        private void ConqueredHQ(Headquarter hq)
        {
            DismantleHQ(hq);
        }

        private void DismantleHQ(Headquarter hq)
        {
            hq.IsActive = false;
            hq.LastKnownProtectionPercent = 0f;
            hq.DismantleStartedAt = DateTime.Now;
            hq.RefreshMapMarker(_freeForAllActive, true);
            hq.RefreshUI(true);
            RefreshMapMarkers();
        }

        private void AddQuitter(BasePlayer player)
        {
            if (!_data.QuitterPlayers.ContainsKey(player.UserIDString))
            {
                _data.QuitterPlayers.Add(player.UserIDString, new HeadquarterQuitter(player.UserIDString));
            }

            SendReply(player, Lang("Headquarter_Quitter", player.UserIDString));
        }

        private void AnnounceConquered(Headquarter conqueringHQ, Headquarter fallenHQ)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintToChat(player, Lang("Headquarter_Conquered", player.UserIDString, fallenHQ.Name, conqueringHQ.Name));
            }
        }

        private bool IsLeader(BasePlayer player)
        {
            return _data.AvailableHeadquarters.ContainsKey(player.UserIDString);
        }

        private bool IsMember(BasePlayer player)
        {
            return _data.MemberPlayers.ContainsKey(player.UserIDString);
        }

        private bool IsQuitter(BasePlayer player)
        {
            return _data.QuitterPlayers.ContainsKey(player.UserIDString);
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
                _data.AvailableHeadquarters.Values.ToList().ForEach(hq => hq.SetInstance(this));
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }
        #endregion

        #region Hooks
        private void Init()
        {
            Unsubscribe(nameof(OnItemAddedToContainer));
            Unsubscribe(nameof(OnItemRemovedFromContainer));

            permission.RegisterPermission(AdminPermissionName, this);
            SaveConfig();
        }

        private void OnServerInitialized(bool initial)
        {
            LoadData();


            _utilityTimer = timer.Every(30f, () =>
            {
                // Delay the OnItemAddedToContainer and OnItemRemovedFromContainer because they throw error if active during init
                if (!_hooksSubscribed)
                {
                    Subscribe(nameof(OnItemAddedToContainer));
                    Subscribe(nameof(OnItemRemovedFromContainer));
                    _hooksSubscribed = true;
                }

                CheckFreeForAll();
                RefreshUIForAllPlayers();
                RefreshMapMarkers();
                RemoveDismantled();
                RemoveQuitters();
            });

            RefreshStorageCounts();

            LoadMapMarkers();

            _cachedUI = UI.GetCachedUI(_config.HeadquartersConfig);

            RefreshUIForAllPlayers();
        }

        private void OnServerSave()
        {
            SaveData();
            RefreshMapMarkers();
        }

        private void Unload()
        {
            RemoveMapMarkers();
            RemoveUIForAllPlayers();
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }

            RefreshMapMarkers();

            if (_config.HeadquartersConfig.FreeForAllEnabled)
            {
                OutputFFAStatus(player);
            }

            AttemptRefreshUIForPlayer(player);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null)
            {
                return;
            }

            var entity = go.ToBaseEntity();
            var player = plan.GetOwnerPlayer();

            string prefabName = entity?.ShortPrefabName ?? "unknown";

            if (entity == null || player == null || (!StorageTypes.Contains(prefabName) && !(entity is BuildingPrivlidge)))
            {
                return;
            }

            var headquarter = GetHeadquarterAtPosition(entity.transform.position);

            if (headquarter == null)
            {
                if (entity is BuildingPrivlidge)
                {
                    SendReply(player, Lang("Headquarter_Init", player.UserIDString));
                }

                return;
            }

            if (!headquarter.HasMember(player.UserIDString))
            {
                NextTick(() =>
                {
                    SendReply(player, Lang("Headquarter_Deployable_Blocked", player.UserIDString));
                    entity.Kill();
                });
            }
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainerID, int targetSlot, int amount)
        {
            if (item == null || playerLoot == null || _freeForAllActive)
            {
                return null;
            }

            var player = item.GetOwnerPlayer();

            if (player == null)
            {
                return null;
            }

            var headquarter = GetHeadquarterAtPosition(player.transform.position);

            if (headquarter == null || !headquarter.IsActive)
            {
                return null;
            }

            var actualContainer = playerLoot.FindContainer(targetContainerID);

            if (actualContainer != null)
            {
                string prefabName = actualContainer?.entityOwner?.ShortPrefabName ?? "unknown";

                if (StorageTypes.Contains(prefabName) && !headquarter.HasMember(player.UserIDString))
                {
                    SendReply(player, Lang("Headquarter_Cant_Move_Storage", player.UserIDString));
                    return false;
                }
            }

            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null || container.entityOwner.transform == null || container.entityOwner.ShortPrefabName == null || container.entityOwner.transform.position == null)
            {
                return;
            }

            string prefabName = container.entityOwner.ShortPrefabName ?? "unknown";

            if (!StorageTypes.Contains(prefabName))
            {
                return;
            }

            var hq = GetHeadquarterAtPosition(container.entityOwner.transform.position);

            if (hq != null)
            {
                hq.StorageSlots++;
                hq.RecalculateProtectionScale();
                hq.RefreshMapMarker(_freeForAllActive);
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null)
            {
                return;
            }

            string prefabName = container?.entityOwner?.ShortPrefabName ?? "unknown";

            if (container == null || !StorageTypes.Contains(prefabName))
            {
                return;
            }

            var hq = GetHeadquarterAtPosition(container.entityOwner.transform.position);

            if (hq != null)
            {
                hq.StorageSlots--;
                hq.RecalculateProtectionScale();
                hq.RefreshMapMarker(_freeForAllActive);
            }
        }

        private object OnEntityTakeDamage(BaseVehicleModule entity, HitInfo info)
        {
            if (_freeForAllActive || entity == null || info == null)
            {
                return null;
            }

            string prefabName = entity?.ShortPrefabName ?? "unknown";

            if (StorageTypesPenalizeModules.Contains(prefabName) && info.damageTypes.Has(Rust.DamageType.Decay))
            {
                var headquarter = GetHeadquarterAtPosition(entity.transform.position);
                var vehicleModule = entity as BaseVehicleModule;

                if (headquarter != null && vehicleModule != null && entity.healthFraction < .25)
                {
                    var foundSCs = vehicleModule.children.FindAll((BaseEntity x) => x is StorageContainer && !x.ShortPrefabName.Contains("fuel"));

                    var random = new System.Random();

                    foreach (var scEntity in foundSCs)
                    {
                        var storageContainer = scEntity as StorageContainer;

                        if (storageContainer != null && !storageContainer.inventory.IsEmpty())
                        {
                            storageContainer.inventory.itemList.RemoveAt(random.Next(storageContainer.inventory.itemList.Count));
                        }
                    }

                }

                return null;
            }

            return null;
        }

        private void OnEntityDeath(BuildingPrivlidge entity, HitInfo info)
        {
            if (entity == null)
            {
                return;
            }

            var headquarter = GetHeadquarterAtPosition(entity.transform.position, _config.HeadquartersConfig.DistanceToTC);

            if (headquarter != null && headquarter.IsActive)
            {
                headquarter.IsActive = false;
                headquarter.DismantleStartedAt = DateTime.Now;
                headquarter.RefreshMapMarker(_freeForAllActive, true);

                if (info.InitiatorPlayer != null)
                {
                    var attackerHQ = GetPlayerHeadquarter(info.InitiatorPlayer);

                    if (attackerHQ != null)
                    {
                        AnnounceConquered(attackerHQ, headquarter);
                    }

                }
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (_freeForAllActive || entity == null || info == null || !info.damageTypes.IsConsideredAnAttack())
            {
                return null;
            }

            if (entity is BuildingPrivlidge)
            {
                return HandleToolCupboardDamage((BuildingPrivlidge)entity, info);
            }

            return HandleBuildingDamage(entity, info);
        }

        private object HandleToolCupboardDamage(BuildingPrivlidge entity, HitInfo info)
        {
            if (!_config.HeadquartersConfig.InvulnerableTC)
            {
                return HandleBuildingDamage(entity, info);
            }

            var headquarter = GetHeadquarterAtPosition(entity.transform.position);

            if (headquarter != null && headquarter.IsActive)
            {
                var attacker = info?.Initiator?.ToPlayer();

                if (attacker != null)
                {
                    SendReply(attacker, Lang("Headquarter_Protected_NoDamage", attacker.UserIDString));
                }

                return true;
            }

            return null;
        }

        private bool shouldHandleBuildingDamage(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock || entity is Door)
            {
                return true;
            }

            foreach (var check in _config.HeadquartersConfig.AdditionalProtectedEntities)
            {
                if (entity.name.Contains(check))
                {
                    return true;
                }
            }

            return false;
        }

        private object HandleBuildingDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!shouldHandleBuildingDamage(entity))
            {
                return null;
            }

            var attacker = info.Initiator?.ToPlayer();

            if (attacker != null)
            {
                bool isAttackerAuthed = attacker.IsBuildingAuthed(entity.transform.position, entity.transform.rotation, entity.bounds);

                if (isAttackerAuthed)
                {
                    return null;
                }
            }

            var headquarter = GetHeadquarterAtPosition(entity.transform.position);

            if (headquarter != null && headquarter.IsActive)
            {
                headquarter.RecalculateProtectionScale();
                float headquarterScale = headquarter.LastKnownProtectionPercent;
                float damageScale = Mathf.Max((1f - headquarterScale), 0f);
                info.damageTypes.ScaleAll(damageScale);
                headquarter.RefreshMapMarker(_freeForAllActive);

                if (damageScale < .01f && attacker != null)
                {
                    SendReply(info.InitiatorPlayer, Lang("Headquarter_Protected_NoDamage", attacker.UserIDString));
                }
                else if (damageScale > .01)
                {
                    MessageAllPlayersHeadquarterBeingAttacked(headquarter, attacker);
                }

                headquarter.MarkDamaged();

                return null;
            }

            return null;
        }

        private Headquarter GetHeadquarterAtPosition(Vector3 position, float radius = 0)
        {
            radius = (radius == 0) ? _config.HeadquartersConfig.Radius : radius;

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                if (Vector3.Distance(position, currentHeadquarter.Value.getPosition()) <= radius)
                {
                    return currentHeadquarter.Value;
                }
            }

            return null;
        }

        object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            var headquarter = GetHeadquarterAtPosition(player.transform.position);

            if (headquarter != null && headquarter.IsActive)
            {
                SendReply(player, Lang("Headquarter_Exists_Cant_Clear_List", player.UserIDString));
                return true;
            }

            return null;
        }

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            var headquarter = GetHeadquarterAtPosition(player.transform.position);

            if (headquarter != null && headquarter.IsActive)
            {
                SendReply(player, Lang("Headquarter_Exists_Cant_Deauth", player.UserIDString));
                return true;
            }

            return null;
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            var potentialHeadquarter = GetHeadquarterAtPosition(player.transform.position, _config.HeadquartersConfig.DistanceToTC);

            if (potentialHeadquarter != null)
            {
                if (IsQuitter(player))
                {
                    SendReply(player, Lang("Headquarter_Quit_In_Progress", player.UserIDString));
                    return true;
                }

                if (!IsLeader(player) && !IsMember(player))
                {
                    potentialHeadquarter.MemberIds.Add(player.UserIDString);
                    _data.MemberPlayers.Add(player.UserIDString, new HeadquarterMember(player.UserIDString, potentialHeadquarter.LeaderId));
                }
                else
                {
                    Headquarter nearestHeadquarter = (Headquarter)potentialHeadquarter;

                    Headquarter playerHeadquarter = GetPlayerHeadquarter(player);

                    if (nearestHeadquarter.LeaderId != playerHeadquarter.LeaderId)
                    {
                        if (!_config.HeadquartersConfig.ConquerModeEnabled)
                        {
                            SendReply(player, Lang("Headquarter_Cant_Auth", player.UserIDString));
                            return true;
                        }
                        else
                        {
                            ConqueredHQ(nearestHeadquarter);
                            AnnounceConquered(playerHeadquarter, nearestHeadquarter);
                        }
                    }
                }
            }

            return null;
        }
        #endregion

        #region Actions

        private void OutputFFAStatus(BasePlayer player)
        {
            if (_freeForAllActive)
            {
                SendReply(player, Lang("Free_For_All_Active", player.UserIDString));
            }
            else if (_config.HeadquartersConfig.FreeForAllEnabled)
            {
                var timeLeft = _config.HeadquartersConfig.FreeForAllHoursAfterWipe - DateTime.Now.Subtract(SaveRestore.SaveCreatedTime).TotalHours;
                string outLeft;

                if (timeLeft > 2)
                {
                    outLeft = Lang("Time_In_Hours", player.UserIDString, ((int)timeLeft).ToString());
                }
                else if (timeLeft < .2)
                {
                    outLeft = Lang("Time_Soon", player.UserIDString);
                }
                else
                {
                    outLeft = Lang("Time_In_Minutes", player.UserIDString, ((int)(timeLeft * 60)).ToString());
                }


                SendReply(player, Lang("Free_For_All_Status", player.UserIDString, outLeft));
            }
            else
            {
                SendReply(player, Lang("Free_For_All_Only_Admin", player.UserIDString));
            }
        }

        private void LoadMapMarkers()
        {
            if (!_config.HeadquartersConfig.MapMarkersEnabled)
            {
                return;
            }

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                currentHeadquarter.Value.CreateMapMarker();
            }
        }

        private void RefreshMapMarkers()
        {
            if (!_config.HeadquartersConfig.MapMarkersEnabled)
            {
                return;
            }

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                currentHeadquarter.Value.RefreshMapMarker(_freeForAllActive);
            }
        }

        private void RemoveMapMarkers()
        {
            if (!_config.HeadquartersConfig.MapMarkersEnabled)
            {
                return;
            }

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                currentHeadquarter.Value.RemoveMapMarker();
            }
        }

        private void RefreshStorageCounts()
        {
            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                RefreshHeadquarterStorageCount(currentHeadquarter.Value);
            }
        }

        private void RefreshHeadquarterStorageCount(Headquarter headquarter)
        {
            if (headquarter == null || !headquarter.IsActive)
            {
                return;
            }

            headquarter.StorageSlots = 0;

            List<StorageContainer> containers = new List<StorageContainer>();
            Vis.Entities<StorageContainer>(headquarter.getPosition(), _config.HeadquartersConfig.Radius, containers);

            foreach (StorageContainer sc in containers.Distinct().ToList())
            {
                string prefabName = sc?.ShortPrefabName ?? "unknown";

                if (sc != null && StorageTypes.Contains(prefabName))
                {
                    headquarter.StorageSlots += sc.inventory.itemList.Count;
                }
            }

            headquarter.RecalculateProtectionScale();
        }

        private void RefreshUIForAllPlayers()
        {
            if (_config.HeadquartersConfig.UIEnabled)
            {
                _data.AvailableHeadquarters.Values.ToList().ForEach(hq => hq.RefreshUI());
            }
        }

        private void AttemptRefreshUIForPlayer(BasePlayer p)
        {
            if (_config.HeadquartersConfig.UIEnabled)
            {
                var hq = GetPlayerHeadquarter(p);

                if (hq != null)
                {
                    RefreshUIForPlayer(p, hq);
                }
            }
        }

        private void RefreshUIForPlayer(BasePlayer p, Headquarter hq)
        {
            CuiHelper.DestroyUi(p, UI.MainContainer);
            CuiHelper.DestroyUi(p, UI.ProtectionContainer);

            if (hq != null)
            {
                CuiHelper.AddUi(p, _cachedUI);
                CuiHelper.AddUi(p, UI.GetProtectionUI(_config.HeadquartersConfig, hq, p, this));
            }
        }

        private void RemoveUIForAllPlayers()
        {
            if (_config.HeadquartersConfig.UIEnabled)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    RemoveUIForPlayer(p);
                }
            }
        }

        private void RemoveUIForPlayer(BasePlayer p)
        {
            CuiHelper.DestroyUi(p, UI.MainContainer);
            CuiHelper.DestroyUi(p, UI.ProtectionContainer);
        }

        private void CheckFreeForAll()
        {
            if (!_freeForAllActive && _config.HeadquartersConfig.FreeForAllEnabled && DateTime.Now.Subtract(SaveRestore.SaveCreatedTime).TotalHours >= _config.HeadquartersConfig.FreeForAllHoursAfterWipe)
            {
                _freeForAllActive = true;
                RefreshMapMarkers();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    PrintToChat(player, Lang("Free_For_All_Active", player.UserIDString));
                }
            }
        }


        private void RemoveDismantled()
        {
            foreach (var currentHQ in _data.AvailableHeadquarters.Values.ToList())
            {
                if (!currentHQ.IsActive)
                {
                    var isDismantleComplete = DateTime.Now.Subtract(currentHQ.DismantleStartedAt).TotalMinutes >= (_config.HeadquartersConfig.QuitPenaltyHours * 60);

                    if (isDismantleComplete)
                    {
                        currentHQ.RemoveMapMarker();
                        currentHQ.RemoveUI();
                        currentHQ.MemberIds.ForEach(memberId => _data.MemberPlayers.Remove(memberId));
                        _data.AvailableHeadquarters.Remove(currentHQ.LeaderId);
                    }
                }
            }
        }

        private void RemoveQuitters()
        {
            foreach (var quitter in _data.QuitterPlayers.Values.ToList())
            {
                var isQuitComplete = DateTime.Now.Subtract(quitter.QuitStartedAt).TotalMinutes >= (_config.HeadquartersConfig.QuitPenaltyHours * 60);

                if (isQuitComplete)
                {
                    _data.QuitterPlayers.Remove(quitter.UserId);
                }
            }
        }


        private void DeauthPlayerFromTC(BasePlayer player, BuildingPrivlidge privilege)
        {
            var found = privilege.authorizedPlayers.Find(e => e.userid == player.userID);

            if (found != null)
            {
                privilege.authorizedPlayers.Remove(found);
            }
        }

        private void MessageAllPlayersHeadquarterBeingAttacked(Headquarter hq, BasePlayer attacker)
        {
            if (attacker != null && _config.HeadquartersConfig.MessagePlayersHeadquarterAttacked && DateTime.Now.Subtract(hq.LastDamaged).TotalSeconds > _config.HeadquartersConfig.ProtectionConstantSecondsAfterDamage)
            {
                Headquarter attackerHQ = GetPlayerHeadquarter(attacker);
                String attackerString = attackerHQ == null ? attacker.displayName : attackerHQ.Name;

                foreach (var player in BasePlayer.activePlayerList)
                {
                    PrintToChat(player, Lang("Headquarter_Being_Attacked", player.UserIDString, hq.Name, attackerString));
                }
            }
        }

        private void OutputPlayerStatus(BasePlayer player)
        {
            var playerHQ = GetPlayerHeadquarter(player);

            if (playerHQ != null)
            {
                if (playerHQ.IsDismantling())
                {
                    var timeSinceDismantleStarted = DateTime.Now.Subtract(playerHQ.DismantleStartedAt).TotalMinutes;
                    var remaining = (Headquarters.getConfig().QuitPenaltyHours * 60) - timeSinceDismantleStarted;

                    SendReply(player, Lang("Status_Dismantling", player.UserIDString, Lang("Time_In_Minutes", player.UserIDString, Math.Round(Math.Max(remaining, 1)).ToString())));
                    return;
                }
                else if (IsLeader(player))
                {
                    SendReply(player, Lang("Status_Leader_Of_Headquarter", player.UserIDString, playerHQ.Name));
                }
                else
                {
                    SendReply(player, Lang("Status_Member_Of_Headquarter", player.UserIDString, playerHQ.Name));
                }
            }
            else
            {
                SendReply(player, Lang("Status_Not_In_Headquarter", player.UserIDString));
            }

            if (IsQuitter(player))
            {
                var timeSinceQuit = DateTime.Now.Subtract(_data.QuitterPlayers[player.UserIDString].QuitStartedAt).TotalMinutes;
                var remaining = (Headquarters.getConfig().QuitPenaltyHours * 60) - timeSinceQuit;

                SendReply(player, Lang("Status_Quitter", player.UserIDString, Lang("Time_In_Minutes", player.UserIDString, Math.Round(Math.Max(remaining, 1)).ToString())));
                return;
            }
        }
        #endregion

        #region Commands

        [ChatCommand("hq.help")]
        private void cmdChatHeadquarterListAll(BasePlayer player, string command)
        {
            SendReply(player, Lang("Help_Welcome", player.UserIDString));
            SendReply(player, Lang("Help_Details", player.UserIDString));
            SendReply(player, Lang("Help_Raid", player.UserIDString, _config.HeadquartersConfig.ConquerModeEnabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
            SendReply(player, "/hq.start " + Lang("Help_Start_Name", player.UserIDString) + " --- " + Lang("Help_Start", player.UserIDString));
            SendReply(player, "/hq.quit --- " + Lang("Help_Quit", player.UserIDString, _config.HeadquartersConfig.QuitPenaltyHours.ToString()));
            SendReply(player, "/hq.ffa --- " + Lang("Help_FFA", player.UserIDString));
            SendReply(player, "/hq.status --- " + Lang("Help_Status", player.UserIDString));
        }

        [ChatCommand("hq.start")]
        private void cmdChatHeadquarterStart(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args[0].Length == 0)
            {
                SendReply(player, Lang("Headquarter_Require_Name", player.UserIDString));
                return;
            }

            var hqName = args[0];

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                if (currentHeadquarter.Value.Name.ToLower() == hqName.ToLower())
                {
                    SendReply(player, Lang("Headquarter_Name_Exists", player.UserIDString));
                    return;
                }
            }

            if (IsQuitter(player))
            {
                SendReply(player, Lang("Headquarter_Quit_In_Progress", player.UserIDString));
                return;
            }

            var isNotAssociatedWithHeadquarter = (!IsLeader(player) && !IsMember(player));

            if (!isNotAssociatedWithHeadquarter && !(GetPlayerHeadquarter(player).IsActive))
            {
                SendReply(player, Lang("Headquarter_Dismantle_In_Progress", player.UserIDString));
                return;
            }

            var existingHeadquarterHere = GetHeadquarterAtPosition(player.transform.position);

            if (existingHeadquarterHere != null)
            {
                SendReply(player, Lang("Headquarter_Inside_Headquarter", player.UserIDString));
                return;
            }

            // ensure next to TC
            List<BaseCombatEntity> cblist = new List<BaseCombatEntity>();
            Vis.Entities<BaseCombatEntity>(player.transform.position, _config.HeadquartersConfig.DistanceToTC, cblist);

            bool nextToCupboard = false;
            BuildingPrivlidge tc = null;

            foreach (BaseCombatEntity bp in cblist.Distinct().ToList())
            {
                if (bp is BuildingPrivlidge)
                {
                    tc = (BuildingPrivlidge)bp;
                    nextToCupboard = true;
                }
            }

            if (!nextToCupboard)
            {
                SendReply(player, Lang("Headquarter_Start_Near_TC", player.UserIDString));
                return;
            }


            // If this user is not associated with a headquarter
            if (isNotAssociatedWithHeadquarter)
            {
                if (tc != null)
                {
                    ((BuildingPrivlidge)tc).authorizedPlayers.Clear();
                    ((BuildingPrivlidge)tc).authorizedPlayers.Add(new ProtoBuf.PlayerNameID { username = player.name, userid = player.userID });

                    var mapMarkersEnabled = _config.HeadquartersConfig.MapMarkersEnabled;
                    var hq = new Headquarter(player.UserIDString, hqName, player.transform.position.x, player.transform.position.y, player.transform.position.z, 0, mapMarkersEnabled);
                    hq.SetInstance(this);
                    _data.AvailableHeadquarters[player.UserIDString] = hq;

                    RefreshHeadquarterStorageCount(hq);
                    hq.RefreshMapMarker(_freeForAllActive);


                    SendReply(player, Lang("Headquarter_Successful_Start", player.UserIDString));
                }


                return;
            }
            else
            {

                SendReply(player, Lang("Headquarter_Already_Started", player.UserIDString));
            }
        }

        [ChatCommand("hq.disband")]
        private void cmdChatHeadquarterDisband(BasePlayer player, string command)
        {
            SendReply(player, Lang("Disband_Quit", player.UserIDString));
        }

        [ChatCommand("hq.quit")]
        private void cmdChatHeadquarterQuit(BasePlayer player, string command)
        {
            if (IsLeader(player))
            {
                Headquarter leaderPreviousHQ = _data.AvailableHeadquarters[player.UserIDString] as Headquarter;

                if (leaderPreviousHQ != null)
                {
                    if (leaderPreviousHQ.IsDismantling())
                    {
                        SendReply(player, Lang("Headquarter_Dismantle_In_Progress", player.UserIDString));
                        return;
                    }

                    // If there are any members left, promote one to new leader and build a replacement HQ
                    if (leaderPreviousHQ.MemberIds.Any())
                    {
                        string newLeader = (string)(leaderPreviousHQ.MemberIds.First());

                        _data.MemberPlayers.Remove(newLeader);

                        var newHQ = new Headquarter(newLeader, leaderPreviousHQ.Name, leaderPreviousHQ.PositionX, leaderPreviousHQ.PositionY, leaderPreviousHQ.PositionZ, leaderPreviousHQ.StorageSlots, leaderPreviousHQ.MapMarkerEnabled);

                        newHQ.SetInstance(this);

                        leaderPreviousHQ.MemberIds.ForEach(memberId => _data.MemberPlayers[memberId].LeaderId = newLeader);

                        _data.AvailableHeadquarters.Add(newLeader, newHQ);

                        var tc = GetHeadquarterTC(leaderPreviousHQ);

                        if (tc != null)
                        {
                            DeauthPlayerFromTC(player, tc);
                        }

                        leaderPreviousHQ.RemoveMapMarker();

                        _data.AvailableHeadquarters.Remove(player.UserIDString);

                        RefreshMapMarkers();

                        AddQuitter(player);

                        RemoveUIForPlayer(player);

                        SendReply(player, Lang("Headquarter_Leader_Quit_Promoted", player.UserIDString));
                    }
                    else
                    {
                        DismantleLeaderHQ(player);

                        AddQuitter(player);

                        SendReply(player, Lang("Headquarter_Leader_Quit_Empty", player.UserIDString));
                    }
                }

                return;
            }
            else if (IsMember(player))
            {
                if (_data.AvailableHeadquarters.ContainsKey(_data.MemberPlayers[player.UserIDString].LeaderId))
                {
                    Headquarter memberPreviousHQ = _data.AvailableHeadquarters[_data.MemberPlayers[player.UserIDString].LeaderId] as Headquarter;

                    if (memberPreviousHQ.IsDismantling())
                    {
                        SendReply(player, Lang("Headquarter_Dismantle_In_Progress", player.UserIDString));
                        return;
                    }

                    if (memberPreviousHQ != null)
                    {

                        var tc = GetHeadquarterTC(memberPreviousHQ);

                        if (tc != null)
                        {
                            DeauthPlayerFromTC(player, tc);
                        }

                        //remove membership there
                        memberPreviousHQ.MemberIds.Remove(player.UserIDString);


                    }
                }

                _data.MemberPlayers.Remove(player.UserIDString);

                AddQuitter(player);

                RemoveUIForPlayer(player);

                return;
            }
            else
            {
                SendReply(player, Lang("Headquarter_Not_Member", player.UserIDString));
            }
        }

        [ChatCommand("hq.list")]
        private void cmdChatHeadquarterList(BasePlayer player, string command)
        {
            SendReply(player, Lang("Headquarter_Found_Count", player.UserIDString, _data.AvailableHeadquarters.Count.ToString()));
            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                SendReply(player, Lang("Headquarter_Near_Found", player.UserIDString, currentHeadquarter.Value.Name + " " + ((currentHeadquarter.Value.IsActive && !_freeForAllActive) ? "(" + Lang("Protected", player.UserIDString) + ")" : "(" + Lang("Unprotected", player.UserIDString) + ")"), GetGrid(currentHeadquarter.Value.getPosition())));
            }
        }

        [ChatCommand("hq.teleport")]
        private void cmdChatHeadquarterTeleport(BasePlayer player, string command)
        {

            if (_config.HeadquartersConfig.TeleportEnabled)
            {
                var playerHQ = GetPlayerHeadquarter(player);

                if (playerHQ != null)
                {
                    Vector3 hqPosition = playerHQ.getPosition();
                    player.transform.position = new Vector3(hqPosition.x, hqPosition.y + 1, hqPosition.z);
                }
            }
        }

        [ChatCommand("hq.ffa")]
        private void cmdChatHeadquarterFFA(BasePlayer player, string command)
        {
            OutputFFAStatus(player);
        }

        [ChatCommand("hq.status")]
        private void cmdChatHeadquarterCheck(BasePlayer player, string command)
        {
            OutputPlayerStatus(player);

            var existingHeadquarterHere = GetHeadquarterAtPosition(player.transform.position);

            if (existingHeadquarterHere != null)
            {
                existingHeadquarterHere.RecalculateProtectionScale();
                float headquarterScale = existingHeadquarterHere.LastKnownProtectionPercent;

                SendReply(player, Lang("Headquarter_Here_Protection_Rating", player.UserIDString, existingHeadquarterHere.Name, (headquarterScale * 100).ToString()));


                existingHeadquarterHere.RefreshMapMarker(_freeForAllActive);
            }
            else
            {
                SendReply(player, Lang("Headquarter_Empty_Here", player.UserIDString));
            }

            SendReply(player, Lang("Headquarter_Protection_Max_Min", player.UserIDString, _config.HeadquartersConfig.ProtectionPercent, _config.HeadquartersConfig.ProtectionPercentMinimum));
            SendReply(player, Lang("Headquarter_Protection_Slots", player.UserIDString, _config.HeadquartersConfig.ProtectionSlotsWithoutPenalty, _config.HeadquartersConfig.ProtectionPenaltyPercentPerSlot));
            SendReply(player, Lang("Headquarter_Protection_Raid_Lock_Seconds", player.UserIDString, _config.HeadquartersConfig.ProtectionConstantSecondsAfterDamage));
        }

        [ChatCommand("grid")]
        private void cmdChatHeadquarterGrid(BasePlayer player, string command)
        {
            SendReply(player, GetGrid(player.transform.position));
        }

        [ConsoleCommand("hq.hide-markers")]
        private void cmdConsoleHideMarkers(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                currentHeadquarter.Value.RemoveMapMarker();
                currentHeadquarter.Value.MapMarkerEnabled = false;
            }
        }

        [ConsoleCommand("hq.show-markers")]
        private void cmdConsoleShowMarkers(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            foreach (KeyValuePair<string, Headquarter> currentHeadquarter in _data.AvailableHeadquarters)
            {
                currentHeadquarter.Value.MapMarkerEnabled = true;
                currentHeadquarter.Value.RecalculateProtectionScale();
                currentHeadquarter.Value.RefreshMapMarker(_freeForAllActive);
            }
        }

        [ConsoleCommand("hq.clear-all")]
        private void cmdConsoleHeadquarterClearAll(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            RemoveMapMarkers();
            RemoveUIForAllPlayers();
            _data.AvailableHeadquarters.Clear();
            _data.MemberPlayers.Clear();
            _data.QuitterPlayers.Clear();
            SaveData();
            PrintToChat(Lang("Headquarter_Cleared", arg.Player().UserIDString));
        }

        [ConsoleCommand("hq.start-ffa")]
        private void cmdConsoleStartFFA(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            _freeForAllActive = true;

            RefreshMapMarkers();
            RefreshUIForAllPlayers();

            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintToChat(player, Lang("Free_For_All_Active", player.UserIDString));
            }
        }

        [ConsoleCommand("hq.stop-ffa")]
        private void cmdConsoleStopFFA(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            _freeForAllActive = false;

            RefreshMapMarkers();
            RefreshUIForAllPlayers();

            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintToChat(player, Lang("Free_For_All_Stopped", player.UserIDString));
            }
        }

        [ConsoleCommand("hq.remove")]
        private void cmdConsoleRemoveHeadquarter(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            if (!arg.HasArgs())
            {
                arg.ReplyWith(Lang("Cmd_Remove_Heaquarter_Leader_Missing", arg.Connection.userid.ToString()));
            }

            var leaderId = arg.Args[0];

            if (_data.AvailableHeadquarters.ContainsKey(leaderId))
            {
                var headquarterToRemove = _data.AvailableHeadquarters[leaderId];
                headquarterToRemove.RemoveMapMarker();
                headquarterToRemove.RemoveUI();
                headquarterToRemove.MemberIds.ForEach(memberId => _data.MemberPlayers.Remove(memberId));
                headquarterToRemove.RemoveMapMarker();
                _data.AvailableHeadquarters.Remove(leaderId);
                _data.QuitterPlayers.Remove(leaderId);
                RefreshMapMarkers();
                arg.ReplyWith(Lang("Cmd_Headquarter_Removed", arg.Connection.userid.ToString()));
            }
            else
            {
                arg.ReplyWith(Lang("Cmd_Headquarter_Remove_Fail", arg.Connection.userid.ToString()));
            }
        }

        [ConsoleCommand("hq.remove-quitter")]
        private void cmdConsoleRemoveQuitter(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            if (!arg.HasArgs())
            {
                arg.ReplyWith(Lang("Cmd_Remove_Quitter_Player_Missing", arg.Connection.userid.ToString()));
            }

            var playerId = arg.Args[0];

            if (_data.QuitterPlayers.ContainsKey(playerId))
            {
                _data.QuitterPlayers.Remove(playerId);
                arg.ReplyWith(Lang("Cmd_Remove_Quitter_Removed", arg.Connection.userid.ToString()));
            }
            else
            {
                arg.ReplyWith(Lang("Cmd_Remove_Quitter_Fail", arg.Connection.userid.ToString()));
            }
        }

        [ConsoleCommand("hq.clear-quitters")]
        private void cmdConsoleClearQuitters(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                return;
            }

            if (!permission.UserHasPermission(arg.Connection.userid.ToString(), AdminPermissionName))
            {
                arg.ReplyWith(Lang("Cmd_Permission", arg.Connection.userid.ToString()));
                return;
            }

            _data.QuitterPlayers.Clear();

            arg.ReplyWith(Lang("Cmd_Cleared_Quitters", arg.Connection.userid.ToString()));
        }
        #endregion
    }
}