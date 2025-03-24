using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Facepunch;

#region Changelogs and ToDo
/**********************************************************************
 * 
 * v1.0.1   :   Changed cfg for language message
 * v1.0.2   :   Excempt for softcore bags in bandit and outpost
 * v1.0.3   :   Added towels and beds
 * v1.0.4   :   Added check for campervans
 * 
 **********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Bed Welcomer", "Krungh Crow", "1.0.4")]
    [Description("Changes the default text on bags towels beds and campervans")]

    class BedWelcomer : RustPlugin
    {
        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BagText"] = "Welcome to our server",
            }, this);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(SleepingBag bag)
        {
            if (bag.niceName == "Unnamed Bag" || bag.niceName == "Unnamed Towel" || bag.niceName == "Bed" || bag is SleepingBagCamper)
            {
                bag.niceName = lang.GetMessage("BagText", this);
            }
            return;
        }

        #endregion
    }
}