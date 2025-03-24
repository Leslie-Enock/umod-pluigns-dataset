using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Oxide.Plugins
{

    [Info("Player Entity Remover", "Zeeuss", "0.1.6")]
    [Description("Removes certain player's entities")]
    public class PlayerEntityRemover : CovalencePlugin
    {

        #region Initialization
        const string entRemoveUse = "playerentityremover.use";
        const string entRemoveBypass = "playerentityremover.bypass";

        void Init()
        {
            permission.RegisterPermission(entRemoveUse, this);
            permission.RegisterPermission(entRemoveBypass, this);
            if (!LoadConfigVariables())
            {
                return;
            }

        }

        void Loaded()
        {
            if(configData.removeOnBan != true)
            {
                Unsubscribe("OnPlayerBanned");
            }
            else
            {
                Subscribe("OnPlayerBanned");
            }

            if(configData.removeOnDeath != true)
            {
                Unsubscribe("OnPlayerDeath");
            }
            else
            {
                Subscribe("OnPlayerDeath");
            }
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //FORMAT: {0} = playerName, {1} = number of removed entities
                ["NoPerms"] = "You don't have permission to use this command!",
                ["Syntax"] = "Syntax: /entremove playerName",
                ["NoPlayer"] = "Player not found!",
                ["RemoveMessage"] = "Removing {0}'s entities...({1})",
                ["NoEnts"] = "No entities found for this player."

            }, this);
        }
        #endregion

        #region Command
        [Command("entremove")]
        private void entityRemoveCmnd(IPlayer player, string command, string[] args)
        {

            if(!player.HasPermission(entRemoveUse))
            {
                player.Message(String.Format(lang.GetMessage("NoPerms", this, player.Id)));
                return;
            }

            if(args.Length < 1)
            {
                player.Message(String.Format(lang.GetMessage("Syntax", this, player.Id)));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if(target == null)
            {
                player.Message(String.Format(lang.GetMessage("NoPlayer", this, player.Id)));
                return;
            }

            List<BaseEntity> ents = new List<BaseEntity>();

            foreach (var ent in BaseNetworkable.serverEntities)
            {
                BaseEntity entity = ent as BaseEntity;

                if (entity == null || entity.OwnerID.ToString() != target.Id)
                {
                    continue;
                }

                ents.Add(entity);

            }
            
            if(ents.Count <= 0)
            {
                player.Message(lang.GetMessage("NoEnts", this, player.Id));
                return;
            }
            // Thanks to nivex  for teaching me new things
            ServerMgr.Instance.StartCoroutine(KillEntities(ents));

            player.Message(String.Format(lang.GetMessage("RemoveMessage", this, player.Id), target.Name, ents.Count));

        }
        #endregion

        #region Hooks
        void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            if (permission.UserHasPermission(id.ToString(), entRemoveBypass))
                return;

            List<BaseEntity> ents = new List<BaseEntity>();

            foreach (var ent in BaseNetworkable.serverEntities)
            {
                BaseEntity entity = ent as BaseEntity;

                if (entity == null || entity.OwnerID.ToString() != id.ToString())
                {
                    continue;
                }

                ents.Add(entity);

            }

            if (ents.Count <= 0)
            {
                return;
            }
            ServerMgr.Instance.StartCoroutine(KillEntities(ents));


        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (permission.UserHasPermission(player.UserIDString, entRemoveBypass))
                return;

            List<BaseEntity> ents = new List<BaseEntity>();

            foreach (var ent in BaseNetworkable.serverEntities)
            {
                BaseEntity entity = ent as BaseEntity;

                if (entity == null || entity.OwnerID.ToString() != player.UserIDString)
                {
                    continue;
                }

                ents.Add(entity);

            }

            if (ents.Count <= 0)
            {
                return;
            }
            ServerMgr.Instance.StartCoroutine(KillEntities(ents));

        }

        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {

            [JsonProperty(PropertyName = "Remove player's entities if banned")]
            public bool removeOnBan = false;

            [JsonProperty(PropertyName = "Remove player's entities on death")]
            public bool removeOnDeath = false;

        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Helpers
        IEnumerator KillEntities(List<BaseEntity> entities)
        {
            var checks = 0;
            var instruction = CoroutineEx.waitForSeconds(0.025f);
            foreach (var entity in entities)
            {
                if (++checks % 100 == 0) yield return instruction;
                if (entity == null || entity.IsDestroyed) continue;
                entity.Kill();

            }

        }
        #endregion

    }

}