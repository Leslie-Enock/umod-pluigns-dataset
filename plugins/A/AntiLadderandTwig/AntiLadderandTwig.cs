//#define Debug
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Anti Ladder and Twig", "kaucsenta", "1.3.1")]
    [Description("Protect bases from ladder and twig frames")]
    class AntiLadderandTwig : RustPlugin
    {
        public PluginConfig config;
        [PluginReference]
        Plugin AbandonedBases;
        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
                return null;
            if (permission.UserHasPermission(player.UserIDString, "antiladderandtwig.bypass"))
                return null;
            bool raidabse_flag = true;
            bool abandonedbse_flag = false;
            if (prefab.fullName.Contains("floor.prefab") || prefab.fullName.Contains("ladder.wooden.wall") || prefab.fullName.Contains("floor.frame") || prefab.fullName.Contains("floor.triangle.frame") || prefab.fullName.Contains("floor.triangle") 
                || prefab.fullName.Contains("shopfront") || prefab.fullName.Contains("shutter") || prefab.fullName.Contains("neon") || prefab.fullName.Contains("sign"))
            {
                if(target.entity != null)
                {
                    if(target.entity is SimpleBuildingBlock || target.entity is BuildingBlock || target.entity is ShopFront || target.entity is StabilityEntity ||target.entity is NeonSign ||target.entity is Signage)
                    {
                        if (target.entity.GetBuildingPrivilege() != null)
                        {
#if Debug
                            Puts(target.player.userID.ToString());
#endif
                            foreach (var tmpplayer in target.entity.GetBuildingPrivilege().authorizedPlayers)
                            {
                                if (covalence.Players.FindPlayerById(tmpplayer.userid.ToString()) != null)
                                {
#if Debug
                                    Puts(covalence.Players.FindPlayerById(tmpplayer.userid.ToString()).Name);
#endif
                                    //if there is any valid player autorized, then it is a raided raidbase or not a raidbase
                                    raidabse_flag = false;
                                }
                                if (tmpplayer.userid == target.player.userID)
                                {
#if Debug
                                    Puts("Valid");
#endif
                                    return null;
                                }
                            }
                            if (System.Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", target.position)))
                            {
#if Debug
                                Puts("Abandoned Base entity detected");
#endif
                                //if the entity is abaddoned, then ladder can be placed like raidable bases
                                abandonedbse_flag = true;
                            }
                            
                            if (raidabse_flag)
                            {
#if Debug
                                Puts("Raidable Base");
#endif
                                return null;
                            }else if(abandonedbse_flag)
                            {
#if Debug
                                Puts("Abandoned Base");
#endif
                                return null;
                            }
                            else
                            {
#if Debug
                                Puts("Not Valid");
#endif
                                player.ChatMessage(lang.GetMessage("CantPlace", this, player.UserIDString));
                                return false;
                            }
                        }
#if Debug
                        else
                        {
                            Puts("Not Protected");
                        }
#endif
                    }
#if Debug
                    else
                    {
                        Puts("Not building or High Wall");
                    }
#endif
                }
#if Debug
                else
                {
                    Puts("Target is not valid");
                }
#endif
            }
            return null;
        }

        void SubscribeHooks(bool flag)
        {
            if (flag)
            {
                Unsubscribe(nameof(CanBuild));
            }
            else
            {
                Subscribe(nameof(CanBuild));
            }
        }
        private void Init()
        {
            LoadVariables();
            SubscribeHooks(config.disableplugin);
            permission.RegisterPermission("antiladderandtwig.bypass", this);

        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig(config);
        }
        private void LoadConfigVariables()
        {
            config = Config.ReadObject<PluginConfig>();
        }
        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Disable plugin feature true/false")]
            public bool disableplugin = false;
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            config = new PluginConfig
            {
                disableplugin = false,
            };
            SaveConfig(config);
        }
        void SaveConfig(PluginConfig config, string filename = null) => Config.WriteObject(config, true, filename);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantPlace"] = "You can't place this here."
            }, this);
        }
    }
}
