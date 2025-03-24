// Requires: BetterChat
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("Bulletin Degree Tags", "Yoshi", 0.2)]
    class DegreeTags : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat;

        private void OnPluginLoaded(Plugin plugin)
        {
            Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetBulletinTags));
        }

        void OnServerInitialized()
        {

            UpdateDegreeCache();
            timer.Every(300f, () =>
            {
                UpdateDegreeCache();
            });

            BetterChat?.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetBulletinTags));
        }
    
        private string GetBulletinTags(IPlayer player)
        {
            List<Degrees> tagInfo = new List<Degrees>();
            if (playerDegrees.TryGetValue(Convert.ToUInt64(player.Id), out tagInfo))
            {
                string formatedString = "";

                if (config.highestDegree)
                {
                    var highestDegree = tagInfo.Last();
                    formatedString = covalence.FormatText($"[#{GetTagColor(highestDegree)}][{highestDegree}][/#]");
                }
                else
                {
                    foreach (var degree in tagInfo)
                        formatedString += covalence.FormatText($"[#{GetTagColor(degree)}][{degree}][/#] ");
                    formatedString = formatedString.Remove(formatedString.Length - 1, 1);
                }

                return formatedString;
            }

            return null;
        }

        private string GetTagColor(Degrees degree)
        {
            switch(degree)
            {
                case Degrees.Bachelors:
                    return config.BachelorsColor;
                case Degrees.Masters:
                    return config.MastersColor;
                case Degrees.PhD:
                    return config.PhDColor;
                case Degrees.Professor:
                    return config.ProfessorColor;
                default:
                    return config.BachelorsColor;
            }
        }

        Dictionary<ulong, List<Degrees>> playerDegrees = new Dictionary<ulong, List<Degrees>>();
        void UpdateDegreeCache()
        {
            webrequest.Enqueue("http://api.bbontop.com:5001/", null, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    Puts($"Couldn't get an answer from the bulletin API!");
                    return;
                }

                var degreeData = JsonConvert.DeserializeObject<List<DegreeData>>(response);
                foreach (var cache in degreeData)
                {
                    if(!playerDegrees.ContainsKey(cache.UserID))
                    {
                        playerDegrees.Add(cache.UserID, cache.Degrees);
                        continue;
                    }

                    foreach(var degree in cache.Degrees)
                    {
                        if (!playerDegrees[cache.UserID].Contains(degree))
                            playerDegrees[cache.UserID].Add(degree);
                    }    
                }

            }, this, RequestMethod.GET);

        }

        #region data

        public class DegreeData
        {
            public ulong UserID { get; set; }
            public List<Degrees> Degrees { get; set; }
        }

        public enum Degrees
        {
            Bachelors,
            Masters,
            PhD,
            Professor
        }
        #endregion

        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Bachelors tag color")]
            public string BachelorsColor = "0061EE";

            [JsonProperty(PropertyName = "Masters tag color")]
            public string MastersColor = "00E3C0";

            [JsonProperty(PropertyName = "PhD tag color")]
            public string PhDColor = "C914BE";

            [JsonProperty(PropertyName = "Professor tag color")]
            public string ProfessorColor = "14E2EC";

            [JsonProperty(PropertyName = "Only show highest degree")]
            public bool highestDegree = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();
        #endregion
    }
}
