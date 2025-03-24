// #define DEBUG

namespace Oxide.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    [Info("Commit Feed", "Jacob", "1.0.0")]
    [Description("Displays new Rust commits to chat.")]
    internal class CommitFeed : RustPlugin
    {
        private const string Url = "https://rust.facepunch.com/rss/commits";

        private Commit? _latestCommit;

        private IEnumerable<Commit> ParseCommits(string xml)
        {
            // HTML Regex: <div class=""change-entry"" id=""(?'id'\d+)"">\s*<div class=""time""><a href="".*"">(?'time'\d+:\d+)<\/a><\/div>\s*<div class=""change"">(?'change'[\w\s\S]*?)(?:<span class=""branch"" title=""branch name"">(?'branch'\/[\w\s\S]*?)<\/span>)?<\/div>\s*<\/div>

            // TODO: parse the newly added author information
            var matches = Regex.Matches(xml,
                @"<item><guid isPermaLink="".*?"">.*?<\/guid><link>.*?<\/link>.*?<title>(?'branch'.*?)\/(?'id'\d+)<\/title><description>(?'description'[\w\s\S]*?)<\/description>");

            foreach (var groups in matches
                .Cast<Match>()
                .Select(x => x.Groups))
            {
                var description = groups["description"].Value;
                if (description == "Private")
                {
                    continue;
                }

                yield return new Commit
                {
                    Branch = groups["branch"].Value,
                    Id = Convert.ToInt32(groups["id"].Value),
                    Description = description
                };
            }
        }

        private void UpdateLatestCommit(IEnumerable<Commit> commits)
        {
            _latestCommit = commits
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

#if DEBUG
            Console.WriteLine("Latest commit: " + JsonConvert.SerializeObject(_latestCommit));
#endif
        }

        protected override void LoadDefaultMessages() =>
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommitAnnouncement"] = "<size=16><color=#CD412B>Commit Feed</color></size>\n<size=13>{0}</size>",
                ["Error"] = $"Error fetching commits from \"{Url}.\""
            }, this);

        private void OnServerInitialized()
        {
            webrequest.Enqueue(Url, string.Empty, (code, xml) =>
            {
                if (code != 200)
                {
                    PrintWarning(lang.GetMessage("Error", this));
                    return;
                }

                UpdateLatestCommit(ParseCommits(xml));
            }, this);

            timer.Every(60, () =>
            {
                webrequest.Enqueue(Url, string.Empty, (code, xml) =>
                {
                    if (code != 200)
                    {
                        PrintWarning(lang.GetMessage("Error", this));
                        return;
                    }

                    var newCommits = ParseCommits(xml)
                        .Where(x => x.Id > _latestCommit?.Id)
                        .ToArray();
                    if (!newCommits.Any())
                    {
                        return;
                    }

                    UpdateLatestCommit(newCommits);

                    foreach (var commit in newCommits)
                    {
#if DEBUG
                        Console.WriteLine("New commit: " + JsonConvert.SerializeObject(commit));
#endif

                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            PrintToChat(player,
                                lang.GetMessage("CommitAnnouncement", this, player.UserIDString),
                                    $"{commit.Description}\n#{commit.Id}, {commit.Branch}");
                        }
                    }
                }, this);
            });
        }

        private struct Commit
        {
            public string Branch { get; set; }
            public int Id { get; set; }
            public string Description { get; set; }
        }
    }
}
