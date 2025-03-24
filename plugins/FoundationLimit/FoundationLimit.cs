using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Foundation Limit", "noname", "1.4.2")]
    [Description("Limits the number of foundations allowed")]
    class FoundationLimit : CovalencePlugin
    {
        public static FoundationLimit Plugin;
        private int MaskInt = LayerMask.GetMask("Construction");
        private const string foundationlimit_admin_Perm = "foundationlimit.admin";
        private const string foundationlimit_bypass_Perm = "foundationlimit.bypass";

        private uint FoundationPrefabID;
        private uint TriangleFoundationPrefabID;

        #region Hooks

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void OnServerInitialized()
        {
            Plugin = this;
            LoadConfig();
            RegisterPermissions();

            FoundationPrefabID = StringPool.toNumber["assets/prefabs/building core/foundation/foundation.prefab"];
            TriangleFoundationPrefabID = StringPool.toNumber["assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab"];
        }

        private void Unload()
        {
            Plugin = null;
        }

        #endregion

        #region PluginIO

        #region ConfigManage

        private PluginConfig config;

        private void LoadConfig()
        {
            config = Config.ReadObject<PluginConfig>();

            if (config == null)
                config = GetDefaultConfig();
        }

        private class PluginConfig
        {
            public int SearchRange;
            public bool UseFoundationClassification;
            public int DefaultFoundationLimit;
            public int DefaultTriangleFoundationLimit;
            public int MinFoundationsBeforeMsgShowsUp;
            public List<PermissionItem> permissionItems;

            public PluginConfig()
            {
                permissionItems = new List<PermissionItem>();
            }
        }

        public class PermissionItem
        {
            public string PermissionName;
            public int FoundationLimit;
            public int TriangleFoundationLimit;

            public PermissionItem()
            {

            }

            public PermissionItem(string permissionName, int foundationLimit)
            {
                PermissionName = permissionName;
                FoundationLimit = foundationLimit;
                TriangleFoundationLimit = 0;
            }
            
            public PermissionItem(string permissionName, int squarefoundationLimit, int trianglefoundationlimit)
            {
                PermissionName = permissionName;
                FoundationLimit = squarefoundationLimit;
                TriangleFoundationLimit = trianglefoundationlimit;
            }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                SearchRange = 5,
                UseFoundationClassification = false,
                DefaultFoundationLimit = 20,
                DefaultTriangleFoundationLimit = 0,
                MinFoundationsBeforeMsgShowsUp = -1,
                permissionItems = new List<PermissionItem>()
                {
                    new PermissionItem("foundationlimit.vip", 40),
                    new PermissionItem("foundationlimit.vip2", 60),
                    new PermissionItem("foundationlimit.vip3", 80)
                }
            };
        }

        #endregion

        #region LangManage

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You do not have permission to use the '{0}' command.",
                ["No parameters"] = "Not enough parameters.",
                ["Invalid parameters"] = "Invalid parameters.",
                ["Parameter Not Int"] = "Parameter is not an integer.",

                ["FoundationLimited"] = "You can place up to {0} foundations.",
                ["SquareFoundationLimited"] = "You can place up to {0} square foundations.",
                ["TriangleFoundationLimited"] = "You can place up to {0} triangle foundations.",
                ["FoundationLimitDisplay"] = "Currently you placed {0} foundations, You can place up to {1} foundations.",
                ["SquareNTriangleFoundationLimitDisplay"] = "Currently you placed {0} square foundations and {1} triangle foundations, You can place up to {2} square foundations and {3} triangle foundations.",

                ["Limit Changed"] = "Foundation limit changed to {0}.",
                ["Limit Triangle Changed"] = "Square Foundation limit changed to {0} and Triangle Foundation limit changed to {0}.",
                ["Radius Changed"] = "Inter-Connected Foundation search scope has been changed to '{0}'.",

                ["Item Added"] = "Custom permission '{0}' has been added.",
                ["Item Removed"] = "Custom permission '{0}' has been removed.",
                ["Item Already Exist"] = "Item with same name already exists.",
                ["Item Not Exist"] = "Item does not exist.",

                ["Limit Item"] = "    <color=#C9AE00>{0}:</color> {1}",
                ["Limit TriangleOptioned Item"] = "    <color=#C9AE00>{0}:</color> Square - {1}, Triangle - {2}",
                ["NoData"] = "No Data",

                ["Class Enabled"] = "Foundation classification enabled.",
                ["Class Disabled"] = "Foundation classification disabled.",
                ["Msg Count Set"] = "MinFoundationsBeforeMsgShowsUp value is set to {0}.",

                ["Limit Status"] = "<size=20><color=#FFBB00>Foundation Limit Status</color></size>\n" +
                                   "\n" +
                                   "<color=#FFE400>SearchRange:</color> {0}\n" +
                                   "<color=#FFE400>MinFoundationsBeforeMsgShowsUp:</color> {1}\n" +
                                   "<color=#FFE400>DefaultFoundationLimit:</color> {2}\n" +
                                   "<color=#FFE400>CustomPermissionList:</color> \n{3}",

                ["Limit TriangleOptioned Status"] = "<size=20><color=#FFBB00>Foundation Limit Status</color></size>\n" +
                                   "\n" +
                                   "<color=#FFE400>SearchRange:</color> {0}\n" +
                                   "<color=#FFE400>MinFoundationsBeforeMsgShowsUp:</color> {1}\n" +
                                   "<color=#FFE400>DefaultSquareFoundationLimit:</color> {2}\n" +
                                   "<color=#FFE400>DefaultTriangleFoundationLimit:</color> {3}\n" +
                                   "<color=#FFE400>CustomPermissionList:</color> \n{4}",

                ["CommandList"] = "<size=20><color=#FFBB00>Foundation Limit</color></size>\n" +
                                  "\n" +
                                  "<color=#FFE400>/fdlimit default </color><color=#C9AE00>[limitcount]</color> - Set number of foundations to limit by default (-1 = Disable)\n" +
                                  "<color=#FFE400>/fdlimit add </color><color=#C9AE00>[permissionname] [limitcount]</color> - add custom vip permission\n" +
                                  "<color=#FFE400>/fdlimit remove </color><color=#C9AE00>[permissionname]</color> - remove custom vip permission\n" +
                                  "<color=#FFE400>/fdlimit radius </color><color=#C9AE00>[searchradius]</color> - Set scope to search for connected foundations\n" +
                                  "<color=#FFE400>/fdlimit class </color><color=#C9AE00>[on/off]</color> - Determine whether to treat triangle foundation and square foundation separately\n" +
                                  "<color=#FFE400>/fdlimit msg </color><color=#C9AE00>[integer (-1 = disable)]</color> - Minimum foundations required before the limit message shows up\n" +
                                  "<color=#FFE400>/fdlimit stat</color> - view config settings",

                ["CommandList TriangleOptioned"] = "<size=20><color=#FFBB00>Foundation Limit</color></size>\n" +
                                  "\n" +
                                  "<color=#FFE400>/fdlimit default </color><color=#C9AE00>[squarefoundationlimitcount] [trianglefoundationlimitcount]</color> - Set number of foundations to limit by default (-1 = Disable)\n" +
                                  "<color=#FFE400>/fdlimit add </color><color=#C9AE00>[permissionname] [squarefoundationlimitcount] [trianglefoundationlimitcount]</color> - add custom vip permission\n" +
                                  "<color=#FFE400>/fdlimit remove </color><color=#C9AE00>[permissionname]</color> - remove custom vip permission\n" +
                                  "<color=#FFE400>/fdlimit radius </color><color=#C9AE00>[searchradius]</color> - Set scope to search for connected foundations\n" +
                                  "<color=#FFE400>/fdlimit class </color><color=#C9AE00>[on/off]</color> - Determine whether to treat triangle foundation and square foundation separately\n" +
                                  "<color=#FFE400>/fdlimit msg </color><color=#C9AE00>[integer (-1 = disable)]</color> - Minimum foundations required before the limit message shows up\n" +
                                  "<color=#FFE400>/fdlimit stat</color> - view config settings"
            }, this);
        }
        
        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion

        #region PermissionManage

        private void RegisterPermissions()
        {
            permission.RegisterPermission(foundationlimit_admin_Perm, this);
            permission.RegisterPermission(foundationlimit_bypass_Perm, this);

            foreach (var item in config.permissionItems)
            {
                permission.RegisterPermission(item.PermissionName, this);
            }
        }

        #endregion

        #endregion

        #region InterCommand

        private void OnEntityBuilt(Planner planner, GameObject gObject)
        {
            BasePlayer player = planner?.GetOwnerPlayer();
            if (player == null)
                return;

            BaseEntity entity = gObject?.ToBaseEntity();
            if (entity == null)
                return;

            if (entity.ShortPrefabName != "foundation" && entity.ShortPrefabName != "foundation.triangle")
                return;

            if (permission.UserHasPermission(player.UserIDString, foundationlimit_bypass_Perm))
                return;

            List<BaseEntity> SearchedFoundation = FindLinkedStructures(player, entity);

            if (!config.UseFoundationClassification)
            {
                int permLimitCount = GetPermLimitCount(player);

                if (permLimitCount != -1)
                {
                    if (SearchedFoundation.Count > permLimitCount)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        player.ChatMessage(Lang("FoundationLimited", player?.UserIDString, permLimitCount.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedFoundation.Count < config.MinFoundationsBeforeMsgShowsUp)
                            return;

                        player.ChatMessage(Lang("FoundationLimitDisplay", player?.UserIDString, SearchedFoundation.Count.ToString(), permLimitCount.ToString()));
                    }
                }
            }
            else
            {
                FoundationSet permLimitCounts = GetPermClassLimitCount(player);

                int SearchedSquareFoundationCount = 0;
                int SearchedTriangleFoundationCount = 0;

                foreach (var item in SearchedFoundation)
                {
                    if (item.ShortPrefabName == "foundation")
                        SearchedSquareFoundationCount++;
                    else
                        SearchedTriangleFoundationCount++;
                }

                if (permLimitCounts.SquareFoundation != -1)
                {
                    if (SearchedSquareFoundationCount > permLimitCounts.SquareFoundation)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        player.ChatMessage(Lang("SquareFoundationLimited", player?.UserIDString, permLimitCounts.SquareFoundation.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedSquareFoundationCount < config.MinFoundationsBeforeMsgShowsUp)
                            return;

                        player.ChatMessage(Lang("SquareNTriangleFoundationLimitDisplay", player?.UserIDString, SearchedSquareFoundationCount.ToString(), permLimitCounts.SquareFoundation.ToString(), SearchedTriangleFoundationCount.ToString(), permLimitCounts.TriangleFoundation.ToString()));
                    }
                }

                if (permLimitCounts.TriangleFoundation != -1)
                {
                    if (SearchedTriangleFoundationCount > permLimitCounts.TriangleFoundation)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        player.ChatMessage(Lang("TriangleFoundationLimited", player?.UserIDString, permLimitCounts.TriangleFoundation.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedTriangleFoundationCount < config.MinFoundationsBeforeMsgShowsUp)
                            return;

                        player.ChatMessage(Lang("SquareNTriangleFoundationLimitDisplay", player?.UserIDString, SearchedSquareFoundationCount.ToString(), permLimitCounts.SquareFoundation.ToString(), SearchedTriangleFoundationCount.ToString(), permLimitCounts.TriangleFoundation.ToString()));
                    }
                }
            }
        }

        object OnStructureDemolish(BaseCombatEntity entity, BasePlayer player, bool immediate)
        {
            if (player == null || entity == null)
                return null;

            if (entity.ShortPrefabName != "foundation" && entity.ShortPrefabName != "foundation.triangle")
                return null;

            if (permission.UserHasPermission(player.UserIDString, foundationlimit_bypass_Perm))
                return null;

            List<BaseEntity> SearchedFoundation = FindLinkedStructures(player, entity);

            if (!config.UseFoundationClassification)
            {
                int permLimitCount = GetPermLimitCount(player);

                if (permLimitCount != -1)
                {
                    if (SearchedFoundation.Count > permLimitCount)
                    {
                        player.ChatMessage(Lang("FoundationLimited", player?.UserIDString, permLimitCount.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedFoundation.Count < config.MinFoundationsBeforeMsgShowsUp)
                            return null;

                        player.ChatMessage(Lang("FoundationLimitDisplay", player?.UserIDString, SearchedFoundation.Count.ToString(), permLimitCount.ToString()));
                    }
                }
            }
            else
            {
                FoundationSet permLimitCounts = GetPermClassLimitCount(player);

                int SearchedSquareFoundationCount = 0;
                int SearchedTriangleFoundationCount = 0;

                foreach (var item in SearchedFoundation)
                {
                    if (item.ShortPrefabName == "foundation")
                        SearchedSquareFoundationCount++;
                    else
                        SearchedTriangleFoundationCount++;
                }

                if (permLimitCounts.SquareFoundation != -1)
                {
                    if (SearchedSquareFoundationCount > permLimitCounts.SquareFoundation)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        player.ChatMessage(Lang("SquareFoundationLimited", player?.UserIDString, permLimitCounts.SquareFoundation.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedSquareFoundationCount < config.MinFoundationsBeforeMsgShowsUp)
                            return null;

                        player.ChatMessage(Lang("SquareNTriangleFoundationLimitDisplay", player?.UserIDString, SearchedSquareFoundationCount.ToString(), permLimitCounts.SquareFoundation.ToString(), SearchedTriangleFoundationCount.ToString(), permLimitCounts.TriangleFoundation.ToString()));
                    }
                }

                if (permLimitCounts.TriangleFoundation != -1)
                {
                    if (SearchedTriangleFoundationCount > permLimitCounts.TriangleFoundation)
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        player.ChatMessage(Lang("TriangleFoundationLimited", player?.UserIDString, permLimitCounts.TriangleFoundation.ToString()));
                    }
                    else
                    {
                        if (config.MinFoundationsBeforeMsgShowsUp != -1 && SearchedTriangleFoundationCount < config.MinFoundationsBeforeMsgShowsUp)
                            return null;

                        player.ChatMessage(Lang("SquareNTriangleFoundationLimitDisplay", player?.UserIDString, SearchedSquareFoundationCount.ToString(), permLimitCounts.SquareFoundation.ToString(), SearchedTriangleFoundationCount.ToString(), permLimitCounts.TriangleFoundation.ToString()));
                    }
                }
            }
            return null;
        }

        private List<BaseEntity> FindLinkedStructures(BasePlayer player, BaseEntity entity)
        {
            List<BaseEntity> SearchedFoundation = new List<BaseEntity>();
            List<BaseEntity> RemoveEntity = new List<BaseEntity>();
            List<BaseEntity> ExpendEntity = new List<BaseEntity>();

            List<BaseEntity> EntityList = new List<BaseEntity>();
            List<BaseEntity> SearchedTemp = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(entity.transform.position, config.SearchRange, EntityList, MaskInt);

            int permLimitCount = GetPermLimitCount(player);

            while (!(SearchedFoundation.Count > permLimitCount || EntityList.Count == 0))
            {
                foreach (var item in EntityList)
                {
                    if (item.prefabID != FoundationPrefabID && item.prefabID != TriangleFoundationPrefabID)
                    {
                        RemoveEntity.Add(item);
                        continue;
                    }
                    if (SearchedFoundation.Contains(item) == false)
                    {
                        SearchedFoundation.Add(item);
                        ExpendEntity.Add(item);
                    }
                    RemoveEntity.Add(item);
                }

                foreach (var item in ExpendEntity)
                {
                    SearchedTemp.Clear();
                    Vis.Entities<BaseEntity>(item.transform.position, config.SearchRange, SearchedTemp, MaskInt);

                    foreach (var additem in SearchedTemp)
                    {
                        if (!EntityList.Contains(additem))
                            EntityList.Add(additem);
                    }
                }
                ExpendEntity.Clear();

                foreach (var item in RemoveEntity)
                {
                    EntityList.Remove(item);
                }
                RemoveEntity.Clear();
            }

            return SearchedFoundation;
        }

        private int GetPermLimitCount(BasePlayer player)
        {
            int permLimitCount = config.DefaultFoundationLimit;
            foreach (var item in config.permissionItems)
            {
                if (permission.UserHasPermission(player.UserIDString, item.PermissionName))
                {
                    if (permLimitCount < item.FoundationLimit)
                    {
                        permLimitCount = item.FoundationLimit;
                    }
                }
            }

            return permLimitCount;
        }

        private FoundationSet GetPermClassLimitCount(BasePlayer player)
        {
            int squarePermLimitCount = config.DefaultFoundationLimit;
            int trianglePermLimitCount = config.DefaultTriangleFoundationLimit;

            foreach (var item in config.permissionItems)
            {
                if (permission.UserHasPermission(player.UserIDString, item.PermissionName))
                {
                    if (squarePermLimitCount < item.FoundationLimit)
                        squarePermLimitCount = item.FoundationLimit;

                    if (trianglePermLimitCount < item.TriangleFoundationLimit)
                        trianglePermLimitCount = item.TriangleFoundationLimit;
                }
            }

            return new FoundationSet(squarePermLimitCount, trianglePermLimitCount);
        }

        private struct FoundationSet
        {
            public int SquareFoundation;
            public int TriangleFoundation;

            public FoundationSet(int squareFoundation, int triangleFoundation)
            {
                SquareFoundation = squareFoundation;
                TriangleFoundation = triangleFoundation;
            }
        }

        #endregion

        #region Command/API

        [Command("fdlimit")]
        void SetFoundationLimitCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                if (!player.HasPermission(foundationlimit_admin_Perm))
                {
                    player.Reply(Lang("No Permission", player.Id, command));
                    return;
                }
            }

            if (0 == args.Length)
            {
                if (!config.UseFoundationClassification)
                    player.Reply(Plugin.Lang("CommandList", player.Id));
                else
                    player.Reply(Plugin.Lang("CommandList TriangleOptioned", player.Id));
                return;
            }

            int parsednum;
            int parsednum2;

            switch (args[0].ToLower())
            {
                case "default":
                    if (!config.UseFoundationClassification)
                    {
                        if (!(2 <= args.Length))
                        {
                            player.Reply(Plugin.Lang("No parameters", player.Id));
                            break;
                        }
                        if (int.TryParse(args[1], out parsednum))
                        {
                            config.DefaultFoundationLimit = parsednum;
                            Config.WriteObject(config, true);
                            player.Reply(Plugin.Lang("Limit Changed", player.Id, parsednum.ToString()));
                        }
                        else
                            player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    }
                    else
                    {
                        if (!(3 <= args.Length))
                        {
                            player.Reply(Plugin.Lang("No parameters", player.Id));
                            break;
                        }
                        if (int.TryParse(args[1], out parsednum) && int.TryParse(args[2], out parsednum2))
                        {
                            config.DefaultFoundationLimit = parsednum;
                            config.DefaultTriangleFoundationLimit = parsednum2;
                            Config.WriteObject(config, true);
                            player.Reply(Plugin.Lang("Limit Triangle Changed", player.Id, parsednum.ToString(), parsednum2.ToString()));
                        }
                        else
                            player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    }
                    break;

                case "add":
                    if (!config.UseFoundationClassification)
                    {
                        if (!(3 <= args.Length))
                        {
                            player.Reply(Plugin.Lang("No parameters", player.Id));
                            break;
                        }
                        if (int.TryParse(args[2], out parsednum))
                        {
                            bool Contains = false;
                            string permissionName = args[1];
                            if (permissionName.Length <= Name.Length + 1 || permissionName.Substring(0, Name.Length + 1) != Name.ToLower() + ".")
                                permissionName = Name.ToLower() + "." + permissionName;

                            foreach (var item in config.permissionItems)
                            {
                                if (item.PermissionName == permissionName)
                                    Contains = true;
                            }
                            if (Contains)
                            {
                                player.Reply(Plugin.Lang("Item Already Exist", player.Id, permissionName));
                                return;
                            }
                            config.permissionItems.Add(new PermissionItem(permissionName, parsednum));
                            Config.WriteObject(config, true);
                            permission.RegisterPermission(permissionName, this);
                            player.Reply(Plugin.Lang("Item Added", player.Id, permissionName));
                        }
                        else
                            player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    }
                    else
                    {
                        if (!(4 <= args.Length))
                        {
                            player.Reply(Plugin.Lang("No parameters", player.Id));
                            break;
                        }
                        if (int.TryParse(args[2], out parsednum) && int.TryParse(args[3], out parsednum2))
                        {
                            bool Contains = false;
                            string permissionName = args[1];
                            if (permissionName.Length <= Name.Length + 1 || permissionName.Substring(0, Name.Length + 1) != Name.ToLower() + ".")
                                permissionName = Name.ToLower() + "." + permissionName;

                            foreach (var item in config.permissionItems)
                            {
                                if (item.PermissionName == permissionName)
                                    Contains = true;
                            }
                            if (Contains)
                            {
                                player.Reply(Plugin.Lang("Item Already Exist", player.Id, permissionName));
                                return;
                            }
                            config.permissionItems.Add(new PermissionItem(permissionName, parsednum, parsednum2));
                            Config.WriteObject(config, true);
                            permission.RegisterPermission(permissionName, this);
                            player.Reply(Plugin.Lang("Item Added", player.Id, permissionName));
                        }
                        else
                            player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    }
                    break;

                case "remove":
                    if (!(2 <= args.Length))
                    {
                        player.Reply(Plugin.Lang("No parameters", player.Id));
                        break;
                    }
                    foreach (var item in config.permissionItems)
                    {
                        if (item.PermissionName == args[1])
                        {
                            config.permissionItems.Remove(item);
                            Config.WriteObject(config, true);
                            player.Reply(Plugin.Lang("Item Removed", player.Id, args[1]));
                            return;
                        }
                        player.Reply(Plugin.Lang("Item Not Exist", player.Id));

                    }
                    break;

                case "radius":
                    if (!(2 <= args.Length))
                    {
                        player.Reply(Plugin.Lang("No parameters", player.Id));
                        break;
                    }
                    if (int.TryParse(args[1], out parsednum))
                    {
                        config.SearchRange = parsednum;
                        Config.WriteObject(config, true);
                        player.Reply(Plugin.Lang("Radius Changed", player.Id, parsednum.ToString()));
                    }
                    else
                        player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    break;

                case "class":
                    if (!(2 <= args.Length))
                    {
                        player.Reply(Plugin.Lang("No parameters", player.Id));
                        break;
                    }
                    switch (args[1].ToLower())
                    {
                        case "on":
                            config.UseFoundationClassification = true;
                            Config.WriteObject(config, true);
                            player.Reply(Plugin.Lang("Class Enabled", player.Id));
                            break;

                        case "off":
                            config.UseFoundationClassification = false;
                            Config.WriteObject(config, true);
                            player.Reply(Plugin.Lang("Class Disabled", player.Id));
                            break;

                        default:
                            player.Reply(Plugin.Lang("Invalid parameters", player.Id));
                            break;
                    }
                    break;

                case "msg":
                    if (!(2 <= args.Length))
                    {
                        player.Reply(Plugin.Lang("No parameters", player.Id));
                        break;
                    }
                    if (int.TryParse(args[1], out parsednum))
                    {
                        config.MinFoundationsBeforeMsgShowsUp = parsednum;
                        Config.WriteObject(config, true);
                        player.Reply(Plugin.Lang("Msg Count Set", player.Id, parsednum.ToString()));
                    }
                    else
                        player.Reply(Plugin.Lang("Parameter Not Int", player.Id));
                    break;

                case "stat":
                    string msg = "";

                    if (!config.UseFoundationClassification)
                    {
                        foreach (var item in config.permissionItems)
                        {
                            msg += Plugin.Lang("Limit Item", player.Id, item.PermissionName, item.FoundationLimit) + "\n";
                        }
                        if (config.permissionItems.Count == 0)
                        {
                            msg += Plugin.Lang("NoData", player.Id);
                        }
                        else
                            msg = msg.Substring(0, msg.Length - 1);

                        player.Reply(Plugin.Lang("Limit Status", player.Id, config.SearchRange, config.MinFoundationsBeforeMsgShowsUp, config.DefaultFoundationLimit, msg));
                    }
                    else
                    {
                        foreach (var item in config.permissionItems)
                        {
                            msg += Plugin.Lang("Limit TriangleOptioned Item", player.Id, item.PermissionName, item.FoundationLimit, item.TriangleFoundationLimit) + "\n";
                        }
                        if (config.permissionItems.Count == 0)
                        {
                            msg += Plugin.Lang("NoData", player.Id);
                        }
                        else
                            msg = msg.Substring(0, msg.Length - 1);

                        player.Reply(Plugin.Lang("Limit TriangleOptioned Status", player.Id, config.SearchRange, config.MinFoundationsBeforeMsgShowsUp, config.DefaultFoundationLimit, config.DefaultTriangleFoundationLimit, msg));
                    }
                    break;

                default:
                    player.Reply(Plugin.Lang("Invalid parameters", player.Id));
                    break;
            }
        }

        #endregion
    }
}