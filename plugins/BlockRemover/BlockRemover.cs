using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Block Remover", "austinv900", "0.4.55")]
    [Description("Allows admins to count and remove building blocks outside of cupboard range")]
    class BlockRemover : RustPlugin
    {
        private ConfigData configData;
        private const string PermCount = "blockremover.count";
        private const string PermRemove = "blockremover.remove";

        class ConfigData
        {
            public bool WarnPlayers { get; set; }
            public VersionNumber Version { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new ConfigData
            {
                WarnPlayers = true,
                Version = Version
            }, true);
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            ["Block.Remove.Grade.Start"] = "Admin is removing all {block.Grade} blocks outside of Building Privilege...",
            ["Block.Remove.Grade.End"] = "Admin has removed {block.Count} {block.Grade} blocks from the map",
            ["Block.Remove.All.Start"] = "Admin is removing all blocks outside of Building Privilege...",
            ["Block.Remove.All.End"] = "Admin has removed {block.Count} blocks from the map"
        }, this);

        void OnServerInitialized()
        {
            configData = Config.ReadObject<ConfigData>();
            if (configData.Version != Version)
            {
                configData.Version = Version;
                Config.WriteObject(configData, true);
            }
            permission.RegisterPermission(PermCount, this);
            permission.RegisterPermission(PermRemove, this);
        }

        [ConsoleCommand("block.countall")]
        void cmdCountBlockAll(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg, PermCount)) return;
            var replyBuilder = new StringBuilder();
            var stabilityEntities = FindAllCupboardlessStabilityEntities(replyBuilder);
            replyBuilder.AppendLine($"There are {stabilityEntities.Count} blocks outside of Building Privilege");

            if (stabilityEntities.Count > 0)
            {
                var types = stabilityEntities.GroupBy(p => p.GetType());
                var vals = string.Join(", ", types.Select(kv => $"{kv.Key.FullName}({kv.Count()})"));
                replyBuilder.Append(vals);
            }

            SendReply(arg, replyBuilder.ToString().TrimEnd(' ', ','));
            Facepunch.Pool.FreeList(ref stabilityEntities);
        }

        [ConsoleCommand("block.count")]
        void cmdCountBlock(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg, PermCount)) return;
            BuildingGrade.Enum grade;
            if (!ParseGrade(arg, out grade)) return;

            var replyBuilder = new StringBuilder();
            var blocks = FindAllCupboardlessBlocks(grade, replyBuilder);
            replyBuilder.AppendLine($"There are {blocks.Count} {grade} blocks outside of Building Privilege");
            SendReply(arg, replyBuilder.ToString());
            Facepunch.Pool.FreeList(ref blocks);
        }

        [ConsoleCommand("block.remove")]
        void cmdRemoveBlock(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg, PermRemove)) return;
            BuildingGrade.Enum grade;
            if (!ParseGrade(arg, out grade)) return;
            

            var replyBuilder = new StringBuilder();
            var blocks = FindAllCupboardlessBlocks(grade, replyBuilder);

            if (blocks.Count > 0)
            {
                var keys = new Dictionary<string, string>();
                keys.Add("block.Count", blocks.Count.ToString());
                keys.Add("block.Grade", grade.ToString());
                keys.Add("task.StartTime", DateTime.UtcNow.ToShortTimeString());
                var started_at = Time.realtimeSinceStartup;
                WarnOnlineMembers("Block.Remove.Grade.Start", keys);
                ServerMgr.Instance.StartCoroutine(ProcessRemoval(blocks, l =>
                {
                    keys.Add("task.EndTime", DateTime.UtcNow.ToShortTimeString());
                    keys.Add("task.ElapseSeconds", (Time.realtimeSinceStartup - started_at).ToString("0.000"));
                    replyBuilder.AppendLine($"Destroyed {blocks.Count} {grade} blocks in {keys["task.ElapseSeconds"]} seconds");
                    WarnOnlineMembers("Block.Remove.Grade.End", keys);
                    SendReply(arg, replyBuilder.ToString());
                    Facepunch.Pool.FreeList(ref l);
                }));
            }
            else
            {
                SendReply(arg, replyBuilder.ToString());
                Facepunch.Pool.FreeList(ref blocks);
            }
        }

        [ConsoleCommand("block.removeall")]
        void cmdRemoveBlockAll(ConsoleSystem.Arg arg)
        {
            if (!CheckAccess(arg, PermRemove)) return;
            
            var replyBuilder = new StringBuilder();
            var stabilityEntities = FindAllCupboardlessStabilityEntities(replyBuilder);

            if (stabilityEntities.Count > 0)
            {
                var keys = new Dictionary<string, string>();
                keys.Add("block.Count", stabilityEntities.Count.ToString());
                keys.Add("task.StartTime", DateTime.UtcNow.ToShortTimeString());
                var started_at = Time.realtimeSinceStartup;
                WarnOnlineMembers("Block.Remove.All.Start", keys);

                ServerMgr.Instance.StartCoroutine(ProcessRemoval(stabilityEntities, l =>
                {
                    keys.Add("task.EndTime", DateTime.UtcNow.ToShortTimeString());
                    keys.Add("task.ElapseSeconds", (Time.realtimeSinceStartup - started_at).ToString("0.000"));
                    var type = stabilityEntities.GroupBy(e => e.GetType());
                    var vals = string.Join(", ", type.Select(kv => $"{kv.Key.FullName}({kv.Count()})"));
                    keys.Add("block.RemoveList", vals);
                    replyBuilder.AppendLine($"Destroyed {stabilityEntities.Count} blocks in {keys["task.ElapseSeconds"]} seconds");
                    replyBuilder.Append(vals);
                    WarnOnlineMembers("Block.Remove.All.End", keys);
                    SendReply(arg, replyBuilder.ToString().TrimEnd(' ', ','));
                    Facepunch.Pool.FreeList(ref stabilityEntities);
                }));
            }
            else
            {
                SendReply(arg, replyBuilder.ToString().TrimEnd(' ', ','));
                Facepunch.Pool.FreeList(ref stabilityEntities);
            }
        }

        List<BuildingBlock> FindAllCupboardlessBlocks(BuildingGrade.Enum grade, StringBuilder reply)
        {
            var blocks = FindAllBuildingBlocks(grade, reply);
            FilterAllCupboardless(blocks, reply);
            return blocks;
        }

        List<StabilityEntity> FindAllCupboardlessStabilityEntities(StringBuilder reply)
        {
            var stabilityEntities = FindAllStabilityEntities(reply);
            FilterAllCupboardless(stabilityEntities, reply);
            return stabilityEntities;
        }

        void FilterAllCupboardless<T>(List<T> blocks, StringBuilder reply) where T : StabilityEntity
        {
            var started_at = Time.realtimeSinceStartup;

            foreach (var ent in blocks.ToArray())
            {
                if (ent is BuildingPrivlidge)
                {
                    blocks.Remove(ent);
                    continue;
                }
                
                var priv = ent.GetBuildingPrivilege();

                if (priv != null)
                {
                    blocks.Remove(ent);
                    continue;
                }

                if (TerrainMeta.Path.Monuments.Any(m => m.Bounds.Contains(ent.transform.position)))
                {
                    blocks.Remove(ent);
                    continue;
                }

                if ((ent is Door || ent is Lift) && ent.OwnerID == 0)
                {
                    blocks.Remove(ent);
                    continue;
                }

                if (ent.transform.position == Vector3.zero)
                {
                    continue;
                }

                var bounds = new OBB(ent.transform.position, ent.transform.rotation, ent.bounds);
                priv = ent.GetBuildingPrivilege(bounds);

                if (priv != null)
                {
                    blocks.Remove(ent);
                    continue;
                }
            }

            reply?.AppendLine($"Finding {blocks.Count} cupboardless blocks took {Time.realtimeSinceStartup - started_at:0.000} seconds");
        }

        List<BuildingBlock> FindAllBuildingBlocks(BuildingGrade.Enum grade, StringBuilder reply)
        {
            var started_at = Time.realtimeSinceStartup;
            var blocks = Facepunch.Pool.GetList<BuildingBlock>();
            blocks.AddRange(BaseNetworkable.serverEntities.OfType<BuildingBlock>().Where(block => block.grade == grade));
            reply?.AppendLine($"Finding {blocks.Count} {grade} blocks took {Time.realtimeSinceStartup - started_at:0.000} seconds");
            return blocks;
        }

        List<StabilityEntity> FindAllStabilityEntities(StringBuilder reply)
        {
            var started_at = Time.realtimeSinceStartup;
            var stabilityEntities = Facepunch.Pool.GetList<StabilityEntity>();
            stabilityEntities.AddRange(BaseNetworkable.serverEntities.OfType<StabilityEntity>());
            reply?.AppendLine($"Finding {stabilityEntities.Count} blocks took {Time.realtimeSinceStartup - started_at:0.000} seconds");
            return stabilityEntities;
        }

        bool CheckAccess(ConsoleSystem.Arg arg, string perm)
        {
            if (arg != null && arg.Connection == null || arg.Player() != null && (arg.Player().IsAdmin || permission.UserHasPermission(arg.Player().UserIDString, perm)))
                return true;
            SendReply(arg, "You need to be admin to use that command");
            return false;
        }

        bool ParseGrade(ConsoleSystem.Arg arg, out BuildingGrade.Enum grade)
        {
            grade = BuildingGrade.Enum.Twigs;
            if (arg.HasArgs())
            {
                try
                {
                    grade = (BuildingGrade.Enum)Enum.Parse(typeof(BuildingGrade.Enum), arg.GetString(0), true);
                }
                catch (Exception)
                {
                    SendReply(arg, $"Unknown grade '{arg.GetString(0)}'");
                    return false;
                }
            }
            return true;
        }

        void WarnOnlineMembers(string langKey, Dictionary<string, string> args)
        {
            if (!configData.WarnPlayers)
                return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                var msg = GetMessage(langKey, player.userID);

                if (args != null)
                {
                    foreach (var replacement in args)
                    {
                        msg = msg.Replace("{" + replacement.Key + "}", replacement.Value, StringComparison.InvariantCultureIgnoreCase);
                    }
                }

                player.ChatMessage(msg);
            }
        }

        string GetMessage(string key, ulong id) => lang.GetMessage(key, this, id == 0 ? null : id.ToString());

        private IEnumerator ProcessRemoval<T>(List<T> blocks, Action<List<T>> onFinish) where T : StabilityEntity
        {
            var current = 0;

            while (true)
            {
                var set = blocks.Skip(current);

                if (set.Count() == 0)
                {
                    break;
                }

                for (var i = 0; i < 10; i++)
                {
                    var c = set.ElementAtOrDefault(i);

                    c?.Kill();
                    current++;
                }

                yield return null;
            }

            onFinish(blocks);
        }
    }
}
