using System;
using System.Linq;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Oxide.Core.Configuration;
using UnityEngine;
using Oxide.Core;

using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Card Reader Doors", "Ts3Hosting", "2.1.1")]
    [Description("Create access doors with card readers")]
    public class CardReaderDoors : RustPlugin
    {
        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;
        private bool Changed;
        private static string theadmin = "cardreaderdoors.admin";
        public Dictionary<ulong, int> activate = new Dictionary<ulong, int>();
		
        public ulong CardskinID;
        public int close;
		public float damagetotal;
		public bool damage;
		public bool damageNo;
		
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["nope"] = "You do not have the perms to do that!",
                ["nopeperm"] = "You do not have the permission to access this room!",
                ["dooradd"] = "You just added this door to the list.",
                ["lockadd"] = "You just added lock for door name {0}.",
                ["nodoor"] = "There is no saved door file {0}.",
                ["fail"] = "Usage /setlock <DoorSavedName>",
                ["faillock"] = "Usage /lockdoor <DoorSavedName> <permission>",
				["UsageActive"] = "Usage /lockdoor active <AccessLevel Number 1 = green, 2 = blue 3 = red>",
                ["dooraddfail"] = "Somthing went wrong is this door name already in the list or you are not close enuf..",
                ["nodoorfail"] = "This is not a door..",
                ["noreader"] = "This is not a cardreader..",
                ["active"] = "You must activate the mode /doorlock active to enable/disable",
                ["activeon"] = "You now active to place cardreaders",
                ["activeoff"] = "You are no longer in active mode.",
				["WrongCard"] = "Wrong Access Key Card Color",
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            LoadVariables();
        }
        void LoadVariables()
        {
            //  CardskinID = Convert.ToUInt64(GetConfig("Keycard", "CardSkinID", 1566879106));
            close = Convert.ToInt32(GetConfig("Door Settings", "Time in seconds to close the door", 5));
			damageNo = Convert.ToBoolean(GetConfig("Card Settings", "Disable KeyCard Damage", false));
			damage = Convert.ToBoolean(GetConfig("Card Settings", "Add More Damage On Card use", false));
			damagetotal = Convert.ToSingle(GetConfig("Card Settings", "Damage Amount", 0.10));

            Puts("Config Loaded");
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/Rooms");
            LoadData();
            RegisterPermissions();
            LoadVariables();
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        private void RegisterPermissions()
        {
            permission.RegisterPermission(theadmin, this);
			if (pcdData.pEntity == null) return;
            foreach (var perm in pcdData.pEntity.Values)
            {
                if (!string.IsNullOrEmpty(perm.permission) && !permission.PermissionExists(perm.permission))
                    permission.RegisterPermission(perm.permission, this);
            }
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>(Name + "/Rooms");
            }
            catch
            {
                Puts("Couldn't load Rooms data, creating new Rooms file");
                pcdData = new PlayerEntity();
            }

        }
        class Access
        {
            public string displayName;
            public ulong steamID;
        }
        class Reader
        {
            //  public ulong NetID;
        }
        class PlayerEntity
        {
            public Dictionary<string, PCDInfo> pEntity = new Dictionary<string, PCDInfo>();


            public PlayerEntity() { }
        }
        class PCDInfo
        {
            public uint SpawnEntity;
            public ulong doorID;
            public string roomName;
            public string permission;
            public Dictionary<ulong, Reader> reader = new Dictionary<ulong, Reader>();
            public List<Access> access = new List<Access>();
        }
        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

		private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            CardReader reader = info?.HitEntity as CardReader;
            if (reader != null)
            {
                if (!permission.UserHasPermission(player.UserIDString, theadmin) || !activate.ContainsKey(player.userID.Get()))
                    return;
				reader.Kill();
			}
		}
		
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan?.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, theadmin) || !activate.ContainsKey(player.userID.Get()))
            {
                return;
            }
            var entity = go?.ToBaseEntity() ?? null;
            if (entity == null) return;
            if (entity is BaseEntity)
            {
                if (entity.ShortPrefabName == "laserdetector")
                {
					CardReader changed = null;
                    var reader = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/cardreader.prefab");
                    if (reader == null) return;
                    reader.transform.localPosition = entity.transform.localPosition + new Vector3(0f, -1.35f, 0f);
                    reader.transform.localRotation = entity.transform.localRotation;
                    reader.gameObject.SetActive(true);
					if (reader is CardReader)
						changed = reader as CardReader; 
					if (changed != null)
					changed.accessLevel = activate[player.userID.Get()];
                    reader.Spawn();
                    reader.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
                    SpawnRefresh(reader);
                    reader.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    reader.SendNetworkUpdateImmediate();
					NextTick(() => { entity.Kill(); });
                }
            }
        }

        private string checkdoor(ulong netID)
        {
            foreach (var door in pcdData.pEntity.Values.ToList())
            {
                if (door.reader.ContainsKey(netID))
                {
                    return door.roomName;
                }
            }
            return null;
        }

        object OnCardSwipe(CardReader reader1, Keycard card1, BasePlayer player)
        {			
            var check = checkdoor(reader1.net.ID.Value);
            if (check == null) return null;
			if (!pcdData.pEntity.ContainsKey(check)) return null;
			float access = card1.accessLevel;
			if (damage || damageNo)
			{
				Item item = card1.GetItem();
				NextTick(() =>
				{
					if (item != null && damageNo)
					{
						item.condition = item.condition + 1;
					}
					else if (item != null && damage)
						item.condition = item.condition - damagetotal;
				});
			}
				
			if (card1.accessLevel != reader1.accessLevel)
			{
				SendReply(player, lang.GetMessage("WrongCard", this, player.UserIDString));
				return null;
			}
			var perm = pcdData.pEntity[check].permission;
            if (perm == null) return null;
            if (!permission.UserHasPermission(player.UserIDString, perm))
            {
                SendReply(player, lang.GetMessage("nopeperm", this, player.UserIDString));
                return null;
            }
			
            var doorID = pcdData.pEntity[check].doorID;
            Door door = BaseNetworkable.serverEntities.Find(new NetworkableId(doorID)) as Door;
            if (door == null) return null;

            if (!door.IsOpen() && (door.GetComponent<Door>()))
            {
                door.SetFlag(BaseEntity.Flags.Open, true);				
                timer.Once(close, () => { door?.SetFlag(BaseEntity.Flags.Open, false); if (reader1 != null) { reader1.CancelInvoke(new Action(reader1.GrantCard)); reader1.CancelInvoke(new Action(reader1.CancelAccess)); reader1.CancelAccess(); } });
            }
            return null;
        }
		
        void SpawnRefresh(BaseNetworkable entity1)
        {
            UnityEngine.Object.Destroy(entity1.GetComponent<Collider>());
        }

        [ChatCommand("lockdoor")]
        private void lockdoor(BasePlayer player, string command, string[] args)
        {
			bool checkN = false;
            if (!permission.UserHasPermission(player.UserIDString, theadmin))
            {
                SendReply(player, lang.GetMessage("nope", this, player.UserIDString));
                return;
            }
			if (args.Length == 0)
			{
				SendReply(player, lang.GetMessage("faillock", this, player.UserIDString));
				return;
			}				
            if (args[0].ToLower() == "active")
            {
								
                if (activate.ContainsKey(player.userID.Get()))
                {
					activate.Remove(player.userID.Get());
                    SendReply(player, lang.GetMessage("activeoff", this, player.UserIDString));
                    return;
                }
                else
                {
					if (args.Length != 2)
					{
						SendReply(player, lang.GetMessage("UsageActive", this, player.UserIDString));
						return;
					}
					if (args[1] == "1") checkN = true;
					if (args[1] == "2") checkN = true;
					if (args[1] == "3") checkN = true;

					if (!checkN)
					{
						SendReply(player, lang.GetMessage("UsageActive", this, player.UserIDString));
						return;
					}
					var ids = default(int);
					if (!int.TryParse(args[1], out ids))
					{
						SendReply(player, lang.GetMessage("faillock", this));
						return;
					}
                    activate.Add(player.userID.Get(), ids);
                    SendReply(player, lang.GetMessage("activeon", this, player.UserIDString));
                    return;
                }
            }
            if (args.Length != 2)
            {
                SendReply(player, lang.GetMessage("faillock", this, player.UserIDString));
                return;
            }
            var rayResult = raydoor(player);
            if (rayResult == null)
            {
                SendReply(player, lang.GetMessage("nodoorfail", this, player.UserIDString));
                return;
            }
            if (rayResult is Door)
            {
                var entity = rayResult as BaseEntity;
                var DoorId = entity.net.ID.Value;

                if (!pcdData.pEntity.ContainsKey(args[0].ToLower()))
                {
                    var perm = "cardreaderdoors." + args[1].ToLower();

                    pcdData.pEntity.Add(args[0].ToLower(), new PCDInfo());
                    pcdData.pEntity[args[0].ToLower()].roomName = args[0].ToLower();
                    pcdData.pEntity[args[0].ToLower()].doorID = entity.net.ID.Value;
                    pcdData.pEntity[args[0].ToLower()].permission = perm;
                    SaveData();
                    if (!string.IsNullOrEmpty(perm) && !permission.PermissionExists(perm))
                        permission.RegisterPermission(perm, this);
                    entity.SetFlag(BaseEntity.Flags.Locked, true, false, true);
                    SendReply(player, lang.GetMessage("dooradd", this, player.UserIDString));
                    return;
                }
                SendReply(player, lang.GetMessage("dooraddfail", this, player.UserIDString));
                return;
            }
            SendReply(player, lang.GetMessage("nodoorfail", this, player.UserIDString));
        }

        [ChatCommand("setlock")]
        private void setlock(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, theadmin))
            {
                SendReply(player, lang.GetMessage("nope", this, player.UserIDString));
                return;
            }
			
            if (args.Length != 1)
            {
                SendReply(player, lang.GetMessage("fail", this, player.UserIDString));
                return;
            }
            var rayResult = raydoor(player);
            if (rayResult == null)
            {
                SendReply(player, lang.GetMessage("noreader", this, player.UserIDString));
                return;
            }
            if (!pcdData.pEntity.ContainsKey(args[0].ToLower()))
            {
                SendReply(player, lang.GetMessage("nodoor", this, player.UserIDString), args[0]);
				return;
            }
            if (rayResult is CardReader)
            {
                var entity = rayResult as CardReader;
                pcdData.pEntity[args[0].ToLower()].reader.Add(entity.net.ID.Value, new Reader());
                SaveData();
                SendReply(player, lang.GetMessage("lockadd", this, player.UserIDString), args[0]);
                return;
            }
            SendReply(player, lang.GetMessage("noreader", this, player.UserIDString));
        }
        private BaseEntity raydoor(BasePlayer player)
        {
            RaycastHit doorhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out doorhit, 1f)) {return null;}
            return doorhit.GetEntity();
        }

    }
}

