using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TargetPractice", "k1lly0u", "0.2.2")]
    class TargetPractice : RustPlugin
    {
        #region Fields
        private StatisticsData statistics;

        private Hash<ulong, Queue<TargetHitInfo>> playerUI = new Hash<ulong, Queue<TargetHitInfo>>();

        private readonly Vector3 localBullseye = new Vector3(-0.02269989f, 0.7262592f, 0.2032242f);

        private const float RADIUS_BULLSEYE = 0.03f;
        private const float RADIUS_INNER_1 = 0.11f;
        private const float RADIUS_INNER_2 = 0.19f;
        private const float RADIUS_INNER_3 = 0.29f;
        private const float RADIUS_OUTER = 0.36f;

        private const uint TARGET_BONE_ID = 365102685;
        private const uint BULLSEYE_BONE_ID = 2528224745;

        private const string POPUP_ELEMENT = "tpui.popup.{0}";
        private const string POPUP_FORMAT = "{0}pts @ {1}m";

        private const string ADMIN_PERMISSION = "targetpractice.admin";

        private enum Hit { Bullseye, Yellow, Red, Blue, Edge }

        private static string[] HitColors;

        private FieldInfo _knockdownHealth = typeof(ReactiveTarget).GetField("knockdownHealth", BindingFlags.Public | BindingFlags.NonPublic);
        private FieldInfo _lastToggleTime = typeof(ReactiveTarget).GetField("lastToggleTime", BindingFlags.Public | BindingFlags.NonPublic);
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            lang.RegisterMessages(Messages, this);

            Unsubscribe(nameof(OnEntitySpawned));

            permission.RegisterPermission(ADMIN_PERMISSION, this);

            HitColors = new string[5];
            HitColors[0] = UI.Color("95c055", 0.95f);
            HitColors[1] = UI.Color("dac15f", 0.95f);
            HitColors[2] = UI.Color("cb3427", 0.95f);
            HitColors[3] = UI.Color("30778a", 0.95f);
            HitColors[4] = UI.Color("72544b", 0.95f);
        }

        private void OnServerInitialized()
        {
            LoadData();

            if (Configuration.Target.KnockdownHealth != 100f) 
            {
                Subscribe(nameof(OnEntitySpawned));
                foreach (BaseNetworkable entity in BaseNetworkable.serverEntities)
                {
                    if (entity is ReactiveTarget)
                        OnEntitySpawned(entity as ReactiveTarget);
                }
            }
        }

        private void OnServerSave() => SaveData();

        private void OnEntitySpawned(ReactiveTarget reactiveTarget) => _knockdownHealth.SetValue(reactiveTarget, Configuration.Target.KnockdownHealth);

        private void OnEntityTakeDamage(ReactiveTarget reactiveTarget, HitInfo info)
        {
            if (info.HitBone != TARGET_BONE_ID && info.HitBone != BULLSEYE_BONE_ID)
                return;

            if (info.InitiatorPlayer == null)
                return;
            
            BasePlayer player = info.InitiatorPlayer;

            Item weapon = player.GetActiveItem();
            if (weapon == null || !Configuration.Weapons.Contains(weapon.info.shortname))
                return;

            Hit hit;
            float score = 0;
            
            if (info.HitBone.Equals(BULLSEYE_BONE_ID))
            {
                hit = Hit.Bullseye;
                score = Configuration.Scores[hit];
            }
            else
            {
                Vector3 bullseyeWorld = reactiveTarget.transform.TransformPoint(localBullseye);

                score = EvaluateScore(Vector3.Distance(bullseyeWorld, info.HitPositionWorld), out hit);
            }

            if (Configuration.Target.ResetTime != 6f)
            {
                float knockdownHealth = (float)_knockdownHealth.GetValue(reactiveTarget);

                if (knockdownHealth <= 0f)
                {
                    reactiveTarget.CancelInvoke(reactiveTarget.ResetTarget);
                    reactiveTarget.Invoke(() => ResetTarget(reactiveTarget), Configuration.Target.ResetTime);
                }
            }

            double d = Math.Round(Vector3Ex.Distance2D(player.transform.position, reactiveTarget.transform.position), 2, MidpointRounding.AwayFromZero);
            TargetHitInfo targetHitInfo = new TargetHitInfo(player.userID, hit, score, d, FormatWeaponName(weapon));

            GetUIComponent(player).Enqueue(targetHitInfo);

            if (statistics.AddScore(player, targetHitInfo) && Configuration.Notify)
                BroadcastTopScore(player.displayName, targetHitInfo);
        }

        private void Unload()
        {
            foreach (PlayerInterface ui in UnityEngine.Object.FindObjectsOfType<PlayerInterface>())
                UnityEngine.Object.Destroy(ui);

            HitColors = null;
            Configuration = null;
        }
        #endregion

        #region Functions
        private float EvaluateScore(float f, out Hit hit)
        {
            if (f < RADIUS_BULLSEYE)            
                hit = Hit.Bullseye;  
            else if (f < RADIUS_INNER_1)            
                hit = Hit.Yellow;            
            else if (f < RADIUS_INNER_2)            
                hit = Hit.Red;            
            else if (f < RADIUS_INNER_3)            
                hit = Hit.Blue;            
            else hit = Hit.Edge;

            return Configuration.Scores[hit];
        }

        private void ResetTarget(ReactiveTarget reactiveTarget)
        {
            if (!reactiveTarget.IsKnockedDown() || !reactiveTarget.CanToggle())            
                return;

            _lastToggleTime.SetValue(reactiveTarget, UnityEngine.Time.realtimeSinceStartup);
            reactiveTarget.SetFlag(BaseEntity.Flags.On, true, false, true);
            _knockdownHealth.SetValue(reactiveTarget, Configuration.Target.KnockdownHealth);
            reactiveTarget.MarkDirtyForceUpdateOutputs();
        }

        private string FormatWeaponName(Item item)
        {            
            string str = item.info.displayName.english;

            if (item.contents?.itemList?.Count > 0)            
                str += string.Format(" ({0})", item.contents?.itemList.Select(x => x.info.displayName.english).ToSentence());
            
            return str;
        }

        private void ChatMessage(BasePlayer player, string key, params object[] args)
        {
            if (args?.Length == 0)
                player.ChatMessage(Msg(key, player.UserIDString));
            else player.ChatMessage(string.Format(Msg(key, player.UserIDString), args));
        }

        private void BroadcastTopScore(string playerName, TargetHitInfo targetHitInfo)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                string score = string.Format(Msg("Notification.ServerScoreFormat", player.UserIDString), player.displayName, targetHitInfo.distance, (Hit)targetHitInfo.hit, targetHitInfo.score, targetHitInfo.weapon);

                ChatMessage(player, "Notification.NewTopScore", score);
            }
        }
        #endregion

        #region Player UI Component
        private PlayerInterface GetUIComponent(BasePlayer player) =>  player.GetComponent<PlayerInterface>() ?? player.gameObject.AddComponent<PlayerInterface>();
        
        private class PlayerInterface : MonoBehaviour
        {
            internal BasePlayer Player { get; private set; }

            internal List<TimedPopup> queue = Facepunch.Pool.GetList<TimedPopup>();

            internal List<string> elements = Facepunch.Pool.GetList<string>();

            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
            }

            private void OnDestroy()
            {
                elements.ForEach(x => CuiHelper.DestroyUi(Player, x));

                Facepunch.Pool.FreeList(ref queue);
                Facepunch.Pool.FreeList(ref elements);
            }

            internal void Enqueue(TargetHitInfo targetHitInfo)
            {
                queue.Add(new TimedPopup(UnityEngine.Time.time + Configuration.Popup.Lifetime, targetHitInfo));
                
                if (elements.Count < Configuration.Popup.Limit)
                {                    
                    string element = CreatePopup(Player, targetHitInfo, elements.Count);
                    elements.Add(element);

                    if (!InvokeHandler.IsInvoking(Player, TimedRefresh))
                        InvokeHandler.Invoke(Player, TimedRefresh, Configuration.Popup.Lifetime);
                }
            }

            private void TimedRefresh()
            {
                elements.ForEach(x => CuiHelper.DestroyUi(Player, x));
                elements.Clear();

                float time = UnityEngine.Time.time;
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    if (time >= queue[i].expiry)
                        queue.RemoveAt(i);
                }

                if (queue.Count == 0)
                {
                    Destroy(this);
                    return;
                }

                float nextRefreshTime = float.PositiveInfinity;

                for (int i = 0; i < Mathf.Min(queue.Count, Configuration.Popup.Limit); i++)
                {
                    TimedPopup timedPopup = queue[i];
                    if (timedPopup.expiry < nextRefreshTime)
                        nextRefreshTime = timedPopup.expiry - time;

                    string element = CreatePopup(Player, timedPopup.targetHitInfo, i);
                    elements.Add(element);
                }

                InvokeHandler.Invoke(Player, TimedRefresh, nextRefreshTime);
            }

            internal struct TimedPopup
            {
                internal float expiry;
                internal TargetHitInfo targetHitInfo;

                internal TimedPopup(float expiry, TargetHitInfo targetHitInfo)
                {
                    this.expiry = expiry;
                    this.targetHitInfo = targetHitInfo;
                }
            }
        }
        #endregion

        #region UI         
        public static class UI
        {
            public static CuiElementContainer Container(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName.ToString()
                    }
                };
                return container;
            }

            public static void Image(ref CuiElementContainer container, string panel, string url, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent { Url = url },
                        new CuiRectTransformComponent { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = false
                },
                panel.ToString());
            }

            public static void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel.ToString());

            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Creation      
        private static readonly UI4 IconPosition = new UI4(0f, 0f, 0.1f, 1f);
        private static readonly UI4 TextPosition = new UI4(0.15f, 0f, 1f, 1f);

        private static string CreatePopup(BasePlayer player, TargetHitInfo info, int position)
        {
            string panel = string.Format(POPUP_ELEMENT, position);

            CuiElementContainer container = UI.Container(panel, HitColors[0], CalculateAnchor(position));

            if (info.IsBullseye())
                UI.Image(ref container, panel, Configuration.Bullseye, IconPosition);
            else UI.Panel(ref container, panel, HitColors[(int)info.hit], IconPosition);

            UI.Label(ref container, panel, string.Format(POPUP_FORMAT, info.score, info.distance), 12, TextPosition, TextAnchor.MiddleLeft);

            CuiHelper.AddUi(player, container);
            return panel;
        }

        private static UI4 CalculateAnchor(int index, float xMin = 0.839f, float yMin = 0.208f, float xDim = 0.148f, float yDim = 0.027f, float spacing = 0.01f)
        {
            float y = yMin + ((yDim + spacing) * index);
            return new UI4(xMin, y, xMin + xDim, y + yDim);
        }

        [ProtoContract]
        private struct TargetHitInfo
        {
            [ProtoMember(1)]
            public string userId;

            [ProtoMember(2)]
            public int hit;

            [ProtoMember(3)]
            public float score;

            [ProtoMember(4)]
            public double distance;

            [ProtoMember(5)]
            public string weapon;

            [ProtoMember(6)]
            public double rangedScore;

            public TargetHitInfo(ulong userId, Hit hit, float score, double distance, string weapon)
            {
                this.userId = userId.ToString();
                this.hit = (int)hit;
                this.score = score;
                this.distance = distance;
                this.weapon = weapon;

                rangedScore = distance * Configuration.Scores[hit];
            }

            internal bool IsBullseye() => (Hit)hit == Hit.Bullseye;
        }
        #endregion

        #region Commands
        [ChatCommand("target")]
        private void cmdTarget(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                ChatMessage(player, "Message.Prefix");
                ChatMessage(player, "Chat.Help1");
                ChatMessage(player, "Chat.Help2");
                ChatMessage(player, "Chat.Help4");

                if (permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                    ChatMessage(player, "Chat.Help3");

                return;
            }

            switch (args[0].ToLower())
            {
                case "top":
                    {
                        List<TargetHitInfo> list = statistics._sortedScores;
                        if (list.Count > 0)
                        {
                            string scores = string.Empty;

                            for (int i = 0; i < Mathf.Min(10, list.Count); i++)
                            {
                                TargetHitInfo targetHitInfo = list[i];

                                scores += string.Format(Msg("Notification.ServerScoreFormat", player.UserIDString), covalence.Players.FindPlayerById(targetHitInfo.userId).Name ?? targetHitInfo.userId, targetHitInfo.distance, (Hit)targetHitInfo.hit, targetHitInfo.score, targetHitInfo.weapon);
                            }

                            ChatMessage(player, "Notification.TopServer", scores);
                        }
                        else ChatMessage(player, "Notification.NoScores");
                    }
                    return;

                case "pb":
                    {
                        List<TargetHitInfo> list;
                        if (statistics.GetUserTopHits(player.userID, out list))
                        {
                            string scores = string.Empty;

                            for (int i = 0; i < Mathf.Min(10, list.Count); i++)
                            {
                                TargetHitInfo targetHitInfo = list[i];

                                scores += string.Format(Msg("Notification.PBScoreFormat", player.UserIDString), targetHitInfo.distance, (Hit)targetHitInfo.hit, targetHitInfo.score, targetHitInfo.weapon);
                            }

                            ChatMessage(player, "Notification.TopPB", scores);
                        }
                        else ChatMessage(player, "Notification.NoScores");
                    }
                    return;

                case "scores":
                    {
                        ulong userId = 0UL;
                        string displayName = string.Empty;
                        if (args.Length > 1)
                        {
                            Core.Libraries.Covalence.IPlayer iPlayer = covalence.Players.FindPlayer(args[1]);
                            if (iPlayer != null)
                            {
                                Debug.Log("found player");
                                userId = ulong.Parse(iPlayer.Id);
                                displayName = iPlayer.Name;
                            }
                        }
                        else
                        {
                            userId = player.userID;
                            displayName = player.displayName;
                        }

                        if (userId == 0UL)
                        {
                            ChatMessage(player, "Notification.NoUserFound", args[1]);
                            return;
                        }

                        string scoreStr = Msg("Notification.StatsFormat", player.UserIDString);
                        if (statistics.FindUserScores(userId, ref scoreStr))
                            ChatMessage(player, "Notification.StatsForUser", displayName, scoreStr);
                        else ChatMessage(player, "Notification.NoDataFound", displayName);
                    }
                    return;

                case "wipe":
                    if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                        return;

                    statistics._players.Clear();
                    statistics._sortedScores.Clear();
                    SaveData();

                    ChatMessage(player, "Notification.WipedScores");
                    return;

                default:
                    ChatMessage(player, "Notification.InvalidSyntax");
                    break;
            }
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Score Values")]
            public Hash<Hit, float> Scores { get; set; }

            [JsonProperty(PropertyName = "Popup Options")]
            public PopupOptions Popup { get; set; }

            [JsonProperty(PropertyName = "Target Options")]
            public TargetOptions Target { get; set; }

            [JsonProperty(PropertyName = "Send server wide notification if the top server score has been beaten")]
            public bool Notify { get; set; }

            [JsonProperty(PropertyName = "Weapons allowed to be used for scoring")]
            public string[] Weapons { get; set; }

            [JsonProperty(PropertyName = "Bullseye image URL")]
            public string Bullseye { get; set; }

            public class TargetOptions
            {
                [JsonProperty(PropertyName = "Amount of damage to take to be knocked down")]
                public float KnockdownHealth { get; set; }

                [JsonProperty(PropertyName = "Amount of time it takes to reset the target")]
                public float ResetTime { get; set; }
            }

            public class PopupOptions
            {
                [JsonProperty(PropertyName = "Popup lifetime (seconds)")]
                public int Lifetime { get; set; }

                [JsonProperty(PropertyName = "Maximum active popup elements")]
                public int Limit { get; set; }
            }

            public class ScoreValues
            {
                public int Bullseye { get; set; }
                public int Yellow { get; set; }
                public int Red { get; set; }
                public int Blue { get; set; }
                public int Edge { get; set; }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Notify = true,
                Popup = new ConfigData.PopupOptions
                {
                    Lifetime = 5,
                    Limit = 15,
                },
                Scores = new Hash<Hit, float>
                {
                    [Hit.Bullseye] = 5f,
                    [Hit.Yellow] = 4f,
                    [Hit.Red] = 3f,
                    [Hit.Blue] = 2f,
                    [Hit.Edge] = 1f,
                },               
                Target = new ConfigData.TargetOptions
                {
                    KnockdownHealth = 100f,
                    ResetTime = 6f
                },
                Weapons = new string[]
                {
                    "rifle.ak",
                    "rifle.bolt",
                    "rifle.l96",
                    "rifle.lr300",
                    "rifle.m39",
                    "rifle.semiauto",
                    "lmg.m249",
                    "bow.compound",
                    "bow.hunting",
                    "crossbow",
                    "smg.2",
                    "smg.thompson",
                    "smg.mp5",
                    "pistol.semiauto",
                    "pistol.revolver",
                    "pistol.m92",
                    "pistol.pythn",
                    "pistol.nailgun",                    
                },
                Bullseye = "https://www.chaoscode.io/oxide/Images/bullseye.png",
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (Configuration.Version < new VersionNumber(0, 2, 0))
                Configuration = baseConfig;

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData() => ProtoStorage.Save<StatisticsData>(statistics, Title);

        private void LoadData()
        {
            statistics = ProtoStorage.Load<StatisticsData>(Title);

            if (statistics == null || statistics._sortedScores == null || statistics._players == null)
                statistics = new StatisticsData();
        }

        [ProtoContract]
        private class StatisticsData
        {
            [ProtoMember(1)]
            public Hash<ulong, List<TargetHitInfo>> _players = new Hash<ulong, List<TargetHitInfo>>();

            [ProtoMember(2)]
            public List<TargetHitInfo> _sortedScores = new List<TargetHitInfo>();

            [JsonIgnore]
            private CompareScore comparer;

            public StatisticsData()
            {
                comparer = new CompareScore();
            }
            
            public bool AddScore(BasePlayer player, TargetHitInfo targetHitInfo)
            {
                List<TargetHitInfo> list;
                if (!_players.TryGetValue(player.userID, out list))
                    list = _players[player.userID] = new List<TargetHitInfo>();

                int search = list.BinarySearch(targetHitInfo, (IComparer<TargetHitInfo>)comparer);
                if (search < 0)
                    list.Insert(~search, targetHitInfo);

                search = _sortedScores.BinarySearch(targetHitInfo, (IComparer<TargetHitInfo>)comparer);
                if (search < 0)
                    _sortedScores.Insert(~search, targetHitInfo);

                if (~search == 0)
                    return true;

                return false;
            }

            public bool GetUserTopHits(ulong playerId, out List<TargetHitInfo> list) => _players.TryGetValue(playerId, out list);

            public bool FindUserScores(ulong playerId, ref string format)
            {
                List<TargetHitInfo> list;
                if (!_players.TryGetValue(playerId, out list))                                   
                    return false;

                float totalScore = list.Sum(x => x.score);

                format = string.Format(format, string.Format(POPUP_FORMAT, list[0].score, list[0].distance), totalScore, list.Count, (float)list.Sum(x => x.distance) / list.Count, totalScore / list.Count);
                
                return true;
            }
            
            private class CompareScore : IComparer<TargetHitInfo>
            {
                public int Compare(TargetHitInfo a, TargetHitInfo b)
                {
                    if (TargetHitInfo.ReferenceEquals(a, b))
                        return 0;
                    else return a.rangedScore.CompareTo(b.rangedScore) * -1;                    
                }
            }
        }
        #endregion

        #region Localization
        private string Msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Message.Prefix"] = "<color=#ce422b>[ Target Practice ]</color> ",

            ["Chat.Help1"] = "<color=#ce422b>/target top</color> - Display the top 10 scores on the server",
            ["Chat.Help2"] = "<color=#ce422b>/target pb</color> - Display your personal top 10 scores",
            ["Chat.Help3"] = "<color=#ce422b>/target wipe</color> - Wipes all score data",
            ["Chat.Help4"] = "<color=#ce422b>/target scores <opt:playername></color> - Check your stats/score, or enter a players name to view their stats/score",

            ["Notification.InvalidSyntax"] = "Invalid syntax! Type <color=#ce422b>/target</color> for help",
            ["Notification.WipedScores"] = "All scores have been wiped",
            ["Notification.NoScores"] = "You do not have any scores saved",

            ["Notification.TopPB"] = "<color=#ce422b>[ Target Practice ]</color> - Personal Best\n{0}",
            ["Notification.PBScoreFormat"] = "\nDistance: <color=#ce422b>{0}</color> | Hit: <color=#ce422b>{1}</color> | Score: <color=#ce422b>{2}</color>\nWeapon: <color=#ce422b>{3}</color>\n",

            ["Notification.TopServer"] = "<color=#ce422b>[ Target Practice ]</color> - Server Best\n{0}",
            ["Notification.ServerScoreFormat"] = "\n<color=#ce422b>{0}</color> | Distance: <color=#ce422b>{1}</color> | Hit: <color=#ce422b>{2}</color>\nScore: <color=#ce422b>{3}</color> | Weapon: <color=#ce422b>{4}</color>\n",

            ["Notification.NewTopScore"] = "<color=#ce422b>[ Target Practice ]</color> - A new top score has been set!\n{0}",

            ["Notification.NoUserFound"] = "Unable to find player with partial name: <color=#ce422b>{0}</color>",
            ["Notification.NoDataFound"] = "No data saved for player: <color=#ce422b>{0}</color>",
            ["Notification.StatsForUser"] = "<color=#ce422b>[ Target Practice ]</color> - Stats for <color=#ce422b>{0}</color>\n{1}",
            ["Notification.StatsFormat"] = "\nBest Score: <color=#ce422b>{0}</color>\nTotal Score: <color=#ce422b>{1}</color>\nTotal Hits: <color=#ce422b>{2}</color>\nAvg Distance: <color=#ce422b>{3}</color>\nAvg Score: <color=#ce422b>{4}</color>\n",
        };
        #endregion
    }
}
