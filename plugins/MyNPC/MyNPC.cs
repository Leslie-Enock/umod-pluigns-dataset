using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Rust;

namespace Oxide.Plugins
{
    [Info("My NPC", "Lincoln", "1.1.0")]
    [Description("Spawn your own player NPCs and access their inventories!")]

    class MyNPC : RustPlugin
    {
        #region Variables
        private readonly Hash<string, float> cooldowns = new Hash<string, float>();
        float cooldownTime;
        private const string npcEntity = "assets/rust.ai/agents/npcplayer/npcplayertest.prefab";
        Dictionary<string, ulong> npcList = new Dictionary<string, ulong>();
        private static NPCPlayer baseNPC;
        private const string permUse = "mynpc.use";
        private const string permBypassCooldown = "mynpc.bypasscooldown";
        private const string permMultipleNPC = "mynpc.multiplenpc";
        #endregion

        #region Functions
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permBypassCooldown, this);
            permission.RegisterPermission(permMultipleNPC, this);
            config = Config.ReadObject<PluginConfig>();
            foreach (var npc in BaseNetworkable.serverEntities.OfType<NPCPlayer>().ToArray())
            {
                if (npc == null) continue;
                if (npc.OwnerID == 0) continue;
                var player = BasePlayer.FindByID(npc.OwnerID) ?? null;
                if (player == null) continue;
                player.userID = npc.OwnerID;
                npcList.Add(npc.displayName, npc.OwnerID);
            }
        }

        private void OnPlayerConnected()
        {
            foreach (var npc in BaseNetworkable.serverEntities.OfType<NPCPlayer>().ToArray())
            {
                if (npc == null) continue;
                if (npc.OwnerID == 0) continue;
                var player = BasePlayer.FindByID(npc.OwnerID) ?? null;
                if (player == null) continue;
                npc.EndSleeping();
            }
        }

        private bool hasPermission(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse)) return true;
            return false;
        }
        private void Unload()
        {
            baseNPC = null;
        }

        private void DestroyNPC(BasePlayer player, NPCPlayer npc)
        {
            if (player == null || npc == null || npc.OwnerID != player.userID) return;

            npcList.Remove(player.userID.ToString());
            ChatMessage(player, "Killed");
            npc.Kill();
        }
        object GetActiveNPC(BasePlayer player) //Get all NPC on the map and check ownership
        {
            foreach (var npc in BaseNetworkable.serverEntities.OfType<NPCPlayer>().ToArray())
            {
                if (npc.OwnerID == player.userID)
                {
                    Puts(npc.displayName + " is owned by " + player.userID);
                    return baseNPC = npc;
                }
            }
            return null;
        }
        private void SpawnNPC(BasePlayer player)
        {
            var position = player.transform.position;
            BasePlayer npc = GameManager.server.CreateEntity(npcEntity, position).ToPlayer();
            npc.OwnerID = player.userID;
            npc.Spawn();
            npc.Heal(100f);
            npc.OverrideViewAngles(player.eyes.rotation.eulerAngles);
            ChatMessage(player, "Spawned");
            npcList.Add(npc.displayName, player.userID);

        }
        void RecallNPC(BasePlayer player, NPCPlayer npc)
        {
            npc.transform.position = player.transform.position;
            npc.OverrideViewAngles(player.eyes.rotation.eulerAngles);
            npc.SendNetworkUpdateImmediate();
            npc.UpdateNetworkGroup();

            ChatMessage(player, "NPCRecall");
            return;
        }
        void GoToNPC(BasePlayer player, NPCPlayer npc)
        {
            player.transform.position = npc.transform.position;
            npc.SendNetworkUpdateImmediate();
            npc.UpdateNetworkGroup();

            ChatMessage(player, "GoTo");
            return;
        }
        void HealNPC(BasePlayer player, NPCPlayer npc)
        {
            npc.Heal(100f);
            npc.SendNetworkUpdateImmediate();
            npc.UpdateNetworkGroup();

            ChatMessage(player, "Healed");
            return;
        }
        void NPCStatus(BasePlayer player, NPCPlayer npc)
        {
            Vector3 location = npc.transform.position;
            int xPos = (int)location.x;
            int yPos = (int)location.y;
            int zPos = (int)location.z;
            var health = (int)npc.Health();
            ChatMessage(player, "Check", health, xPos, yPos, zPos);
            return;
        }

        bool OnCoolDown(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permBypassCooldown)) return false;

            if (!cooldowns.ContainsKey(player.UserIDString))
            {
                cooldowns.Add(player.UserIDString, 0f);
            }

            if (cooldownTime > 0 && cooldowns[player.UserIDString] + cooldownTime > Interface.Oxide.Now)
            {
                ChatMessage(player, "Cooldown", config.Cooldown);
                return true;
            }
            cooldowns[player.UserIDString] = Interface.Oxide.Now;
            return false;
        }
        private void ViewInventory(BasePlayer player, BasePlayer targetplayer)
        {

            player.EndLooting();

            LootableCorpse corpse = GameManager.server.CreateEntity(StringPool.Get(2604534927), Vector3.zero) as LootableCorpse;
            corpse.CancelInvoke("RemoveCorpse");
            corpse.syncPosition = false;
            corpse.limitNetworking = true;
            corpse.playerName = targetplayer.displayName;
            corpse.playerSteamID = 0;
            corpse.enableSaving = false;
            corpse.Spawn();
            corpse.SetFlag(BaseEntity.Flags.Locked, true);
            Buoyancy bouyancy;
            if (corpse.TryGetComponent<Buoyancy>(out bouyancy))
            {
                UnityEngine.Object.Destroy(bouyancy);
            }
            Rigidbody ridgidbody;
            if (corpse.TryGetComponent<Rigidbody>(out ridgidbody))
            {
                UnityEngine.Object.Destroy(ridgidbody);
            }
            corpse.SendAsSnapshot(player.Connection);

            timer.Once(0.3f, () =>
            {
                StartLooting(player, targetplayer, corpse);
            });
        }
        private void StartLooting(BasePlayer player, BasePlayer targetplayer, LootableCorpse corpse)
        {
            player.inventory.loot.AddContainer(targetplayer.inventory.containerMain);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerWear);
            player.inventory.loot.AddContainer(targetplayer.inventory.containerBelt);
            player.inventory.loot.entitySource = corpse;
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>(null, player, "RPC_OpenLootPanel", "player_corpse");
        }

        #endregion

        #region Config
        private class PluginConfig
        {
            public int Cooldown;
        }

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                Cooldown = 10
            };
        }
        #endregion

        #region Chat Commands
        //command for debugging NPCs
        [ChatCommand("npc.list")]
        private void ListCommand(BasePlayer player, string command, string[] args)
        {
            string entry = "";

            if (!player.IsAdmin) return;
            if (npcList.Count() == 0)
            {
                ChatMessage(player, "NoNPCOnMap");
                return;
            }
            foreach (var npc in npcList)
            {
                entry = "npcID " + npc.Key + " SteamID " + npc.Value;
            }

            PrintToConsole(entry);
            ChatMessage(player, "Console");
        }
        [ChatCommand("npc")]
        private void NPCCommand(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }
            if (player.IsAdmin)
            {
                ChatMessage(player, "AdminHelp");
                return;
            };

            ChatMessage(player, "Help");
        }
        [ChatCommand("npc.help")]
        private void HelpCommand(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }
            if (player.IsAdmin)
            {
                ChatMessage(player, "AdminHelp");
                return;
            };
            ChatMessage(player, "Help");
        }
        [ChatCommand("npc.add")]
        private void NPCAddCommand(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player) == true)
            {
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, permMultipleNPC))
            {
                SpawnNPC(player);
                return;
            }
            GetActiveNPC(player);
            if (npcList.ContainsValue(player.userID) && baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                SpawnNPC(player);
                return;
            }
            if (npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "Exists");
                return;
            }
            SpawnNPC(player);
        }
        [ChatCommand("npc.kill")]
        private void KillNPCCommand(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }
            GetActiveNPC(player);
            if (baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                ChatMessage(player, "NoNPC");
                return;
            }
            if (!npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "NoNPC");
                return;
            }
            DestroyNPC(player, baseNPC);
        }
        [ChatCommand("npc.heal")]
        private void HealNPCCommand(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }
            GetActiveNPC(player);

            if (baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                ChatMessage(player, "NoNPC");
                return;
            }
            if (!npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "NoNPC");
                return;
            }
            HealNPC(player, baseNPC);
        }
        [ChatCommand("npc.status")]
        private void CheckNPCCommand(BasePlayer player, string command, string[] args)
        {
            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }
            GetActiveNPC(player);

            if (baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                ChatMessage(player, "NoNPC");
                return;
            }

            if (!npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "NoNPC");
                return;
            }
            NPCStatus(player, baseNPC);
        }

        [ChatCommand("npc.inv")]
        private void ViewInvCmd(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (hasPermission(player) == false)
            {
                ChatMessage(player, "Permission");
                return;
            }

            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {

                RaycastHit hitinfo;

                if (!Physics.Raycast(player.eyes.HeadRay(), out hitinfo, 3f, (int)Layers.Server.Players))
                {
                    ChatMessage(player, "NotFound");
                    return;
                }

                BasePlayer targetplayerhit = hitinfo.GetEntity().ToPlayer();
                if (targetplayerhit == null)
                {
                    ChatMessage(player, "NotFound");
                    return;
                }

                if (targetplayerhit.ShortPrefabName == "npcplayertest" && targetplayerhit.OwnerID == player.userID)
                {
                    ViewInventory(player, targetplayerhit);
                }
                return;
            }
            GetActiveNPC(player);
            NPCPlayer target = baseNPC;

            if (target == null)
            {
                return;
            }
            BasePlayer targetplayer = target as BasePlayer;
            if (targetplayer == null)
            {
                return;
            }
            ViewInventory(player, targetplayer);
        }
        [ChatCommand("npc.recall")]
        private void RecallNPCCommand(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player) == true)
            {
                return;
            }

            cooldownTime = config.Cooldown;

            if (!npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "NoNPC");
                return;
            }
            if (OnCoolDown(player))
            {
                return;
            }
            if (ValidationChecks(player) == true)
            {
                return;
            }

            GetActiveNPC(player);

            if (baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                ChatMessage(player, "NoNPC");
                return;
            }

            RecallNPC(player, baseNPC);

            return;
        }
        [ChatCommand("npc.goto")]
        private void TeleportNPCCommand(BasePlayer player, string command, string[] args)
        {
            if (ValidationChecks(player) == true)
            {
                return;
            }

            cooldownTime = config.Cooldown;

            if (!npcList.ContainsValue(player.userID))
            {
                ChatMessage(player, "NoNPC");
                return;
            }
            if (OnCoolDown(player))
            {
                return;
            }
            if (ValidationChecks(player) == true)
            {
                return;
            }

            GetActiveNPC(player);

            if (baseNPC == null)
            {
                npcList.Remove(player.userID.ToString());
                ChatMessage(player, "NoNPC");
                return;
            }

            GoToNPC(player, baseNPC);

            return;
        }
        #endregion

        #region Hooks
        void OnEntityKill(BaseEntity entity)
        {
            if (entity.ShortPrefabName != "npcplayertest") return;

            NPCPlayer npc = entity as NPCPlayer;
            if (npc.OwnerID == 0) return;
            var player = BasePlayer.FindByID(npc.OwnerID) ?? null;
            npcList.Remove(player.userID.ToString());

        }
        void OnEntityTakeDamage(NPCPlayer npc, HitInfo info)
        {
            if (npc == null || info.InitiatorPlayer == null || npc.OwnerID == 0) return;
            var player = BasePlayer.FindByID(npc.OwnerID);
            var attacker = info.InitiatorPlayer.UserIDString;

            if (npc.ShortPrefabName == "npcplayertest")
            {
                var currentNPCHealth = (int)npc._health;

                if (npc.OwnerID == 0) return;

                if (npc.OwnerID.ToString() != attacker)
                {
                    //Puts("You do not own this NPC. SteamID " + npc.OwnerID + " owns this NPC. Your ID is " + player.OwnerID);
                    info.damageTypes.ScaleAll(0f);
                    ChatMessage(info.InitiatorPlayer, "CantDamage");
                    return;
                }
                //Puts("This is your NPC " + attacker);
                return;
            }

        }
        #endregion

        #region Helpers
        private bool PositionIsInWater(Vector3 position)
        {
            var colliders = Facepunch.Pool.GetList<Collider>();
            Vis.Colliders(position, 0.5f, colliders);
            var flag = colliders.Any(x => x.gameObject?.layer == (int)Rust.Layer.Water);
            Facepunch.Pool.FreeList(ref colliders);
            return flag;
        }
        bool ValidationChecks(BasePlayer player)
        {
            if (player == null || player.IsDead()) return true;

            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                ChatMessage(player, "Permission");
                return true;
            }
            if (player.IsHeadUnderwater() || player.IsSwimming() || PositionIsInWater(player.transform.position))
            {
                ChatMessage(player, "WaterBad");
                return true;
            }
            if (player.IsBuildingBlocked())
            {
                ChatMessage(player, "BuildingBlocked");
                return true;
            }
            if (player.isMounted)
            {
                ChatMessage(player, "Mounted");
                return true;
            }
            if (player.IsFlying || !player.IsOnGround())
            {
                ChatMessage(player, "Flying");
                return true;
            }
            if (player.IsWounded())
            {
                ChatMessage(player, "Wounded");
                return true;
            }

            return false;
        }
        #endregion

        #region Localization

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
        player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "<size=18><color=#ffc34d>MyNPC</color></size>\n<color=#9999ff>/npc.add</color> to spawn your NPC.\n<color=#9999ff>/npc.recall</color> to recall your NPC.\n<color=#9999ff>/npc.goto</color> teleport to your NPC.\n<color=#9999ff>/npc.kill</color> to kill your NPC.\n<color=#9999ff>/npc.heal</color> to heal your NPC.\n<color=#9999ff>/npc.inv</color> to access your NPC's inventory.\n<color=#9999ff>/npc.status</color> to check the status of your NPC.",
                ["AdminHelp"] = "<size=18><color=#ffc34d>MyNPC</color></size>\n<color=#9999ff>/npc.add</color> to spawn your NPC.\n<color=#9999ff>/npc.recall</color> to recall your NPC.\n<color=#9999ff>/npc.goto</color> teleport to your NPC.\n<color=#9999ff>/npc.kill</color> to kill your NPC.\n<color=#9999ff>/npc.heal</color> to heal your NPC.\n<color=#9999ff>/npc.inv</color> to access your NPC's inventory.\n<color=#9999ff>/npc.status</color> to check the status of your NPC.\n\n<color=#ff6666>(Admin only)</color>\n<color=#9999ff>/npc.list</color> get the count and ownership of NPCs on the map.",
                ["Spawned"] = "<color=#ffc34d>MyNPC</color>: You have spawned an NPC.\nType <color=#9999ff>/npc</color> for more npc commands!",
                ["Healed"] = "<color=#ffc34d>MyNPC</color>: You NPC has been healed to <color=#b0fa66>100%</color>",
                ["Check"] = "<color=#ffc34d>MyNPC</color>: You NPC is <color=#b0fa66>alive</color> with <color=#b0fa66>{0}</color> health at location X:<color=#ffc34d>{1}</color>  Y:<color=#ffc34d>{2}</color> Z:<color=#ffc34d>{3}</color>",
                ["NPCRecall"] = "<color=#ffc34d>MyNPC</color>: You have recalled your NPC.",
                ["GoTo"] = "<color=#ffc34d>MyNPC</color>: You have teleported to your NPC.",
                ["Console"] = "<color=#ffc34d>MyNPC</color>: Check your <color=#ffc34d>console</color> for the results.",
                ["NoNPCOnMap"] = "<color=#ffc34d>MyNPC</color>: There are no player spawned NPCs on the map.",
                ["Killed"] = "<color=#ffc34d>MyNPC</color>: Your NPC has been killed.",
                ["NoNPC"] = "<color=#ffc34d>MyNPC</color>: You don't have an NPC out.",
                ["NotFound"] = "<color=#ffc34d>MyNPC</color>: No owned NPCs found.",
                ["Exists"] = "<color=#ffc34d>MyNPC</color>: You already have an NPC out.",
                ["CantDamage"] = "<color=#ffc34d>MyNPC</color>: You cannot damage an NPC that you don't own.",
                ["Permission"] = "<color=#ffc34d>MyNPC</color>: You don't have an NPC out.",
                ["BuildingBlocked"] = "<color=#ffc34d>MyNPC</color>: Can't spawn/recall/goto an NPC while building blocked.",
                ["Mounted"] = "<color=#ffc34d>MyNPC</color>: Can't spawn/recall/goto an NPC while mounted.",
                ["WaterBad"] = "<color=#ffc34d>MyNPC</color>: NPCs can't swim.",
                ["Flying"] = "<color=#ffc34d>MyNPC</color>: Can't spawn/recall/goto while jumping, flying, or falling",
                ["Cooldown"] = "<color=#ffc34d>MyNPC</color>: You are on a {0} second cooldown.",

            }, this, "en");
        }
        #endregion
    }
}
