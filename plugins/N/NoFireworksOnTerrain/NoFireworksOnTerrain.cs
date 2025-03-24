using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("No Fireworks On Terrain", "0x89A", "1.0.4")]
    [Description("Restricts placement of fireworks")]

    public class NoFireworksOnTerrain : RustPlugin
    {
        const string bypassPermission = "nofireworksonterrain.bypass";

        private HashSet<uint> fireworkPrefabIds = new HashSet<uint> { 1538862213, 3537935076, 1303486792, 2125925416, 2059113456, 571344195,  //Big Boomers
                                                                       2628631722, 2847715782, 1410649145, 793494534,  //Roman Candles
                                                                       1311124308, 2771932546, 4042905807 };  //Volcanoes

        #region -Init-

        void Init() => permission.RegisterPermission(bypassPermission, this);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotOnFoundationFloor"] = "Fireworks must be placed on a foundation or floor"
            }
            , this);
        }

        #endregion

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            BasePlayer player = planner.GetOwnerPlayer();

            if (player != null && fireworkPrefabIds.Contains(prefab.prefabID) && !permission.UserHasPermission(player.UserIDString, bypassPermission) && target.entity == null)
            {
                PrintToChat(player, lang.GetMessage("NotOnFoundationFloor", this, player.UserIDString));

                return true;
            }

            return null;
        }
    }
}
