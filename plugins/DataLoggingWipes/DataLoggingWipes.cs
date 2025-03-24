// Requires: DataLogging

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core.Extensions;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Logging;
using Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("Data Logging: Wipes", "Rustoholics", "0.2.0")]
    [Description("Log every wipe")]

    public class DataLoggingWipes : DataLogging
    {
        #region Object
        public class Wipe
        {
            public DateTime Date = DateTime.Now;
            public int Seed;
            public string SaveFile = "";
            public string MapFile = "";
            public int Version;
            public string OxideVersion;
        }
        
        #endregion
        
        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoRecords"] = "There are no records of any wipes",
                ["LastWipe"] = "There last wipe happened on {0}",
            }, this);
        }
        
        #endregion
        
        #region Setup
        
        DataList<Wipe> _data;

        private void OnServerInitialized()
        {
            _data = new DataList<Wipe>("global","wipe");
            var lastwipe = _data.GetDataLast();
            var currentSeed = ConVar.Server.seed;
            var savefile = World.SaveFileName;
            Match match = Regex.Match(savefile, @"\.([\d]*)\.sav");
            int version = 0;
                
            if (match.Success)
            {
                if (!int.TryParse(match.Groups[1].Value, out version))
                    version = 0;
            }

            if (lastwipe == null || lastwipe.Seed != currentSeed || lastwipe.Version != version || lastwipe.SaveFile != savefile)
            {
                _data.AddData(new Wipe
                {
                    Seed = currentSeed,
                    SaveFile = savefile,
                    MapFile = World.MapFileName,
                    Version = version,
                    OxideVersion = new RustExtension(new ExtensionManager(new CompoundLogger())).Version.ToString()
                });
                _data.Save();
            }

        }

        #endregion
        
        #region Data Analysis
        
        #endregion
        
        #region Commands

        [Command("datalogging.lastwipe")]
        private void LastWipeCommand(IPlayer iplayer, string command, string[] args)
        {
            var lastwipe = _data.GetDataLast();
            if (lastwipe == null)
            {
                iplayer.Reply(Lang("NoRecords", iplayer.Id));
                return;
            }
            iplayer.Reply(Lang("LastWipe",iplayer.Id, lastwipe.Date));
        }

        
        #endregion

        #region API

        private DateTime API_LastWipe()
        {
            var lastwipe = _data.GetDataLast();
            if (lastwipe == null) return default(DateTime);

            return lastwipe.Date;
        }

        #endregion
    }
}