using System.Collections.Generic;
using UnityEngine;
using Rust;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Reflection;
using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("RadHouse", "pidorassavy", "1.0.5")]
    [Description("Small plugin create RadHouse event on server")]

    class RadHouse : RustPlugin
    {
        // Other needed functions and vars
        #region SomeParameters and plugin's load
        [PluginReference] Plugin RandomSpawns;
        [PluginReference] Plugin RustMap;
		[PluginReference] Plugin LustyMap;

        private List<ZoneList> RadiationZones = new List<ZoneList>();
        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");
        private static readonly Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic))?.GetValue(null);
        private ZoneList RadHouseZone;


        public List<uint> BaseEntityList = new List<uint>();
        public List<ulong> PlayerAuth = new List<ulong>();
        private List<string> BlockedItems = new List<string>();
        private Dictionary<string, object> ItemList_Common = new Dictionary<string, object>()
        {
            ["charcoal"] = 10000,
            ["metal.fragments"] = 2500,
            ["sulfur"] = 1000,
            ["wood"] = 10000

        };
        private Dictionary<string, object> ItemList_Rare = new Dictionary<string, object>()
        {
            ["rifle.ak"] = 1,
            ["rifle.bolt"] = 1,
            ["rifle.lr300"] = 1,
            ["smg.thompson"] = 1
        };
        private Dictionary<string, object> ItemList_Top = new Dictionary<string, object>()
        {
            ["ammo.rocket.basic"] = 2,
            ["explosive.satchel"] = 4,
            ["explosive.timed"] = 1,
            ["lmg.m249"] = 1,
            ["rocket.launcher"] = 1
        };

        public bool CanLoot = false;
  
        public bool NowLooted = false;
        public Timer mytimer;
        public Timer mytimer2;
        public Timer mytimer3;
        public Timer mytimer4;
        public int timercallbackdelay = 0;

        #region CFG var's
        public bool GuiOn = true;
        public string AnchorMinCfg = "0.3445 0.16075";
        public string AnchorMaxCfg = "0.6405 0.20075";
        public string ColorCfg = "1 1 1 0.1";
        public string TextGUI = "Радиационный дом:";
        public bool RadiationTrue = false;
        public string ChatPrefix = "<color=#ffe100>Радиационный дом:</color>";

        public int TimerSpawnHouse = 3600;
        public int TimerDestroyHouse = 60;
        public int TimerLoot = 120;

        public int RadiationRadius = 10;
        public int RadiationIntensity = 25;
        #endregion




        protected override void LoadDefaultConfig()
        {
            LoadConfigValues();
        }


        private void LoadConfigValues()
        {
            
            GetConfig("[GUI]", "Включить GUI", ref GuiOn);
            GetConfig("[GUI]", "Anchor Min", ref AnchorMinCfg);
            GetConfig("[GUI]", "Anchor Max", ref AnchorMaxCfg);
            GetConfig("[GUI]", "Цвет фона", ref ColorCfg);
            GetConfig("[GUI]", "Текст в GUI окне", ref TextGUI);
            GetConfig("[Основное]", "Префикс чата", ref ChatPrefix);
            GetConfig("[Основное]", "Время спавна дома", ref TimerSpawnHouse);
            GetConfig("[Основное]", "Задержка перед лутанием ящика", ref TimerLoot);
            GetConfig("[Основное]", "Задержка перед удалением дома", ref TimerDestroyHouse);
            GetConfig("[Радиация]", "Радиус радиации", ref RadiationRadius);
            GetConfig("[Радиация]", "Отключить стандартную радиацию", ref RadiationTrue);
            GetConfig("[Радиация]", "Интенсивность радиации", ref RadiationIntensity);
            GetConfig("[Лут]", "Список обычного лута (shortname)", ref ItemList_Common);
            GetConfig("[Лут]", "Список редкого лута (shortname)", ref ItemList_Rare);
            GetConfig("[Лут]", "Список топового лута (shortname)", ref ItemList_Top);
            SaveConfig();
        }

        private void GetConfig<T>(string menu, string Key, ref T var)
        {
            if (Config[menu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[menu, Key], typeof(T));
            }

            Config[menu, Key] = var;
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            timer.Once(1, () => { CreateRadHouse(); });
            mytimer4 = timer.Repeat(TimerSpawnHouse, 0, () =>
            {
                try
                {
                    if (BaseEntityList != null)
                    {
                        DestroyRadHouse();
                    }
                    CreateRadHouse();
                }
                catch(Exception ex) { Puts(ex.ToString()); }
            });
        }

        void Loaded()
        {
            if (RadiationTrue)
            {
                OnServerRadiation();
                PrintWarning("Радиация на стандартный объектах отключена");
            }
        }
        void Unload()
        {
            if(BaseEntityList != null) DestroyRadHouse();
            if(mytimer != null) timer.Destroy(ref mytimer);
            if (mytimer2 != null) timer.Destroy(ref mytimer2);
            if (mytimer3 != null) timer.Destroy(ref mytimer3);
            if (mytimer4 != null) timer.Destroy(ref mytimer4);
        }

        #endregion

            // Function Create entity
            #region CreateAndDestroyRadHouse
        public object success;

        [ChatCommand("rh")]
        void CreateRadHouseCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (player == null) return;

            if (!player.IsAdmin)
            {
                SendReply(player, $"{ChatPrefix} Команда доступна только администраторам");
                return;
            }
            if (Args == null || Args.Length == 0)
            {
                SendReply(player, $"{ChatPrefix} Используйте /rh start или /rh cancel");
                return;
            }

            switch (Args[0])
            {
                case "start":
                    CreateRadHouse();
                    SendReply(player, $"{ChatPrefix} Вы в ручную запустили ивент");
                    return;
                case "cancel":
                    DestroyRadHouse();
                    SendReply(player, $"{ChatPrefix} Ивент остановлен");
                    return;
            }

        }
        private void OnServerRadiation()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<TriggerRadiation>();
            for (int i = 0; i < allobjects.Length; i++)
            {
                UnityEngine.Object.Destroy(allobjects[i]);
                
            }
        }
        void CreateRadHouse()
        {
            if(!plugins.Exists("RandomSpawns"))
            { return; }
            Vector3 pos;
            pos.x = 0;
            pos.y = 0;
            pos.z = 0;
            success = RandomSpawns.Call("GetSpawnPoint");

            pos = (Vector3)success;
            if(pos.y > 30f)
            {
                CreateRadHouse();
                return;
            }
            

            pos.x = pos.x + 0f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 0f;
            			
			
            BaseEntity Foundation = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", pos, new Quaternion(), true);
            BuildingBlock bbF = Foundation.GetComponent<BuildingBlock>();			
			
			var bId = BindDecay(bbF);						
			
            pos.x = pos.x - 1.5f;

            BaseEntity Wall = GameManager.server.CreateEntity("assets/prefabs/building core/wall/wall.prefab", pos, new Quaternion(), true);
            Wall.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            BuildingBlock bbW = Wall.GetComponent<BuildingBlock>();
			
			bId = BindDecay(bbW, bId);

            pos = (Vector3)success;

            pos.x = pos.x + 0f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 3f;

            BaseEntity Foundation2 = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", pos, new Quaternion(), true);
            BuildingBlock bbF2 = Foundation2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbF2, bId);
			
            pos.x = pos.x - 1.5f;

            BaseEntity Wall2 = GameManager.server.CreateEntity("assets/prefabs/building core/wall/wall.prefab", pos, new Quaternion(), true);
            Wall2.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            BuildingBlock bbW2 = Wall2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbW2, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 0f;

            BaseEntity Foundation3 = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", pos, new Quaternion(), true);
            BuildingBlock bbF3 = Foundation3.GetComponent<BuildingBlock>();

			bId = BindDecay(bbF3, bId);
			
            pos.x = pos.x + 1.5f;

            BaseEntity Wall3 = GameManager.server.CreateEntity("assets/prefabs/building core/wall/wall.prefab", pos, new Quaternion(), true);
            BuildingBlock bbW3 = Wall3.GetComponent<BuildingBlock>();

			bId = BindDecay(bbW3, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 3f;

            BaseEntity Foundation4 = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", pos, new Quaternion(), true);
            BuildingBlock bbF4 = Foundation4.GetComponent<BuildingBlock>();

			bId = BindDecay(bbF4, bId);
			
            pos.x = pos.x + 1.5f;

            BaseEntity Wall4 = GameManager.server.CreateEntity("assets/prefabs/building core/wall/wall.prefab", pos, new Quaternion(), true);
            BuildingBlock bbW4 = Wall4.GetComponent<BuildingBlock>();

			bId = BindDecay(bbW4, bId);
			
            pos = (Vector3)success;

            pos.z = pos.z - 1.5f;
            pos.y = pos.y + 1f;

            BaseEntity DoorWay = GameManager.server.CreateEntity("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", pos, new Quaternion(), true);
            DoorWay.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            BuildingBlock bbDW = DoorWay.GetComponent<BuildingBlock>();

			bId = BindDecay(bbDW, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 4.5f;

            BaseEntity DoorWay2 = GameManager.server.CreateEntity("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", pos, new Quaternion(), true);
            DoorWay2.transform.localEulerAngles = new Vector3(0f, 270f, 0f);
            BuildingBlock bbDW2 = DoorWay2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbDW2, bId);
			
            pos = (Vector3)success;

            pos.z = pos.z - 1.5f;
            pos.y = pos.y + 1f;
            pos.x = pos.x + 3f;

            BaseEntity WindowWall = GameManager.server.CreateEntity("assets/prefabs/building core/wall.window/wall.window.prefab", pos, new Quaternion(), true);
            WindowWall.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            BuildingBlock bbWW = WindowWall.GetComponent<BuildingBlock>();

			bId = BindDecay(bbWW, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 0f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 4.5f;

            BaseEntity WindowWall2 = GameManager.server.CreateEntity("assets/prefabs/building core/wall.window/wall.window.prefab", pos, new Quaternion(), true);
            WindowWall2.transform.localEulerAngles = new Vector3(0f, 270f, 0f);
            BuildingBlock bbWW2 = WindowWall2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbWW2, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 0f;
            pos.y = pos.y + 4f;
            pos.z = pos.z + 0f;

            BaseEntity Roof = GameManager.server.CreateEntity("assets/prefabs/building core/roof/roof.prefab", pos, new Quaternion(), true);
            Roof.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            BuildingBlock bbR = Roof.GetComponent<BuildingBlock>();

			bId = BindDecay(bbR, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3f;
            pos.y = pos.y + 4f;
            pos.z = pos.z + 0f;

            BaseEntity Roof2 = GameManager.server.CreateEntity("assets/prefabs/building core/roof/roof.prefab", pos, new Quaternion(), true);
            Roof2.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            BuildingBlock bbR2 = Roof2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbR2, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 0f;
            pos.y = pos.y + 4f;
            pos.z = pos.z + 3f;

            BaseEntity Roof3 = GameManager.server.CreateEntity("assets/prefabs/building core/roof/roof.prefab", pos, new Quaternion(), true);
            Roof3.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
            BuildingBlock bbR3 = Roof3.GetComponent<BuildingBlock>();

			bId = BindDecay(bbR3, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3f;
            pos.y = pos.y + 4f;
            pos.z = pos.z + 3f;

            BaseEntity Roof4 = GameManager.server.CreateEntity("assets/prefabs/building core/roof/roof.prefab", pos, new Quaternion(), true);
            Roof4.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
            BuildingBlock bbR4 = Roof4.GetComponent<BuildingBlock>();

			bId = BindDecay(bbR4, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 4.0f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 1.5f;

            BaseEntity CupBoard = GameManager.server.CreateEntity("assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab", pos, new Quaternion(), true);
            CupBoard.transform.localEulerAngles = new Vector3(0f, 270f, 0f);

			bId = BindDecay(CupBoard, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x - 1.0f;
            pos.y = pos.y + 1f;
            pos.z = pos.z + 1.5f;

            BaseEntity Box = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pos, new Quaternion(), true);
            Box.skinID = 942917320;
            Box.transform.localEulerAngles = new Vector3(0f, 90f, 0f);			
			
			bId = BindDecay(Box, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x + 3;
            pos.y = pos.y - 0.5f;
            pos.z = pos.z + 7.5f;

            BaseEntity FSteps = GameManager.server.CreateEntity("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", pos, new Quaternion(), true);
            FSteps.transform.localEulerAngles = new Vector3(0f, 90f, 0f);
            BuildingBlock bbFS = FSteps.GetComponent<BuildingBlock>();

			bId = BindDecay(bbFS, bId);
			
            pos = (Vector3)success;

            pos.x = pos.x - 0f;
            pos.y = pos.y - 0.5f;
            pos.z = pos.z - 4.5f;

            BaseEntity FSteps2 = GameManager.server.CreateEntity("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", pos, new Quaternion(), true);
            FSteps2.transform.localEulerAngles = new Vector3(0f, 270f, 0f);
            BuildingBlock bbFS2 = FSteps2.GetComponent<BuildingBlock>();

			bId = BindDecay(bbFS2, bId);
			
            Foundation.Spawn();
            Wall.Spawn();
            Foundation2.Spawn();
            Wall2.Spawn();
            Foundation3.Spawn();
            Wall3.Spawn();
            Foundation4.Spawn();
            Wall4.Spawn();
            DoorWay.Spawn();
            DoorWay2.Spawn();
            WindowWall.Spawn();
            WindowWall2.Spawn();
            Roof.Spawn();
            Roof2.Spawn();
            Roof3.Spawn();
            Roof4.Spawn();
            CupBoard.Spawn();
            Box.Spawn();
            FSteps.Spawn();
            FSteps2.Spawn();									
			
            StorageContainer Container = Box.GetComponent<StorageContainer>();									
            ItemContainer inven = Container?.inventory;						
			
            if (Container != null)
            {				
				
				if (ItemList_Common != null)
				{	
					var Common = ItemList_Common.Select(key => key.Key).ToList();				                               
					for (var i = 0; i < ItemList_Common.Count; i++)
					{
						int j = UnityEngine.Random.Range(1, 10);
						var item = ItemManager.CreateByName(Common[i], Convert.ToInt32(ItemList_Common[Common[i]]));
						if (j > 3)
						{
							item.MoveToContainer(Container.inventory, -1, false);
						}
					}
				}
				
				if (ItemList_Rare != null)
				{	
					var Rare = ItemList_Rare.Select(key => key.Key).ToList();				
					for (var i = 0; i < ItemList_Rare.Count; i++)
					{
						int j = UnityEngine.Random.Range(1, 10);
						var item = ItemManager.CreateByName(Rare[i], Convert.ToInt32(ItemList_Rare[Rare[i]]));
						if (j > 5)
						{
							item.MoveToContainer(Container.inventory, -1, false);
						}
					}
				}
				
				if (ItemList_Top != null)
				{	
					var Top = ItemList_Top.Select(key => key.Key).ToList();				
					for (var i = 0; i < ItemList_Top.Count; i++)
					{
						int j = UnityEngine.Random.Range(1, 10);
						var item = ItemManager.CreateByName(Top[i], Convert.ToInt32(ItemList_Top[Top[i]]));
						if (j > 7)
						{
							item.MoveToContainer(Container.inventory, -1, false);
						}
					}
				}
            }
			
            bbF.SetGrade((BuildingGrade.Enum)1);
            bbF.UpdateSkin();
            bbF.SetHealthToMax();
            bbF.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbW.SetGrade((BuildingGrade.Enum)1);
            bbW.UpdateSkin();
            bbW.SetHealthToMax();
            bbW.grounded = true;
            bbW.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbF2.SetGrade((BuildingGrade.Enum)1);
            bbF2.UpdateSkin();
            bbF2.SetHealthToMax();
            bbF2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbW2.SetGrade((BuildingGrade.Enum)1);
            bbW2.UpdateSkin();
            bbW2.SetHealthToMax();
            bbW2.grounded = true;
            bbW2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbF3.SetGrade((BuildingGrade.Enum)1);
            bbF3.UpdateSkin();
            bbF3.SetHealthToMax();
            bbF3.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbW3.SetGrade((BuildingGrade.Enum)1);
            bbW3.UpdateSkin();
            bbW3.SetHealthToMax();
            bbW3.grounded = true;
            bbW3.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbF4.SetGrade((BuildingGrade.Enum)1);
            bbF4.UpdateSkin();
            bbF4.SetHealthToMax();
            bbF4.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbW4.SetGrade((BuildingGrade.Enum)1);
            bbW4.UpdateSkin();
            bbW4.SetHealthToMax();
            bbW4.grounded = true;
            bbW4.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbDW.SetGrade((BuildingGrade.Enum)1);
            bbDW.UpdateSkin();
            bbDW.SetHealthToMax();
            bbDW.grounded = true;
            bbDW.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbDW2.SetGrade((BuildingGrade.Enum)1);
            bbDW2.UpdateSkin();
            bbDW2.SetHealthToMax();
            bbDW2.grounded = true;
            bbDW2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbWW.SetGrade((BuildingGrade.Enum)1);
            bbWW.UpdateSkin();
            bbWW.SetHealthToMax();
            bbWW.grounded = true;
            bbWW.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbWW2.SetGrade((BuildingGrade.Enum)1);
            bbWW2.UpdateSkin();
            bbWW2.SetHealthToMax();
            bbWW2.grounded = true;
            bbWW2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbR.SetGrade((BuildingGrade.Enum)1);
            bbR.UpdateSkin();
            bbR.SetHealthToMax();
            bbR.grounded = true;
            bbR.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbR2.SetGrade((BuildingGrade.Enum)1);
            bbR2.UpdateSkin();
            bbR2.SetHealthToMax();
            bbR2.grounded = true;
            bbR2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbR3.SetGrade((BuildingGrade.Enum)1);
            bbR3.UpdateSkin();
            bbR3.SetHealthToMax();
            bbR3.grounded = true;
            bbR3.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbR4.SetGrade((BuildingGrade.Enum)1);
            bbR4.UpdateSkin();
            bbR4.SetHealthToMax();
            bbR4.grounded = true;
            bbR4.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbFS.SetGrade((BuildingGrade.Enum)1);
            bbFS.UpdateSkin();
            bbFS.SetHealthToMax();
            bbFS.grounded = true;
            bbFS.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            bbFS2.SetGrade((BuildingGrade.Enum)1);
            bbFS2.UpdateSkin();
            bbFS2.SetHealthToMax();
            bbFS2.grounded = true;
            bbFS2.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);			
			
            BaseEntityList.Add(Foundation.net.ID);
            BaseEntityList.Add(Foundation2.net.ID);
            BaseEntityList.Add(Foundation3.net.ID);
            BaseEntityList.Add(Foundation4.net.ID);
            BaseEntityList.Add(Wall.net.ID);
            BaseEntityList.Add(Wall2.net.ID);
            BaseEntityList.Add(Wall3.net.ID);
            BaseEntityList.Add(Wall4.net.ID);
            BaseEntityList.Add(Roof.net.ID);
            BaseEntityList.Add(Roof2.net.ID);
            BaseEntityList.Add(Roof3.net.ID);
            BaseEntityList.Add(Roof4.net.ID);
            BaseEntityList.Add(DoorWay.net.ID);
            BaseEntityList.Add(DoorWay2.net.ID);
            BaseEntityList.Add(WindowWall.net.ID);
            BaseEntityList.Add(WindowWall2.net.ID);
            BaseEntityList.Add(CupBoard.net.ID);
            BaseEntityList.Add(Box.net.ID);
            BaseEntityList.Add(FSteps.net.ID);
            BaseEntityList.Add(FSteps2.net.ID);
            InitializeZone(Box.transform.position, RadiationIntensity, RadiationRadius, 666);
            Server.Broadcast($"{ChatPrefix} Радиактивный домик появился, координаты: {pos.ToString()}");
            foreach (var player in BasePlayer.activePlayerList)
            {
                CreateGui(player);
            }
            if (plugins.Exists("RustMap"))
            {
				LustyMap?.Call("AddMarker", Box.transform , Box.transform , "RadHouseMap", "rad", 0);
                RustMap?.Call("AddTemporaryMarker", "rad", false, 0.03f, 0.99f, Box.transform, "RadHouseMap");
            }
            CanLoot = false;
            NowLooted = false;
            timercallbackdelay = 0;
        }

        void DestroyRadHouse()
        {
            if(BaseEntityList != null)
            {
                foreach(uint id in BaseEntityList)
                {
                    BaseNetworkable.serverEntities.Find(id).Kill();
                }
                DestroyZone(RadHouseZone);
                if (plugins.Exists("RustMap"))
                {
					LustyMap?.Call("RemoveMarker", "RadHouseMap");
					RustMap?.Call("RemoveTemporaryMarkerByName", "RadHouseMap");
                }
                BaseEntityList.Clear();
                PlayerAuth.Clear();
            }
            foreach(var player in BasePlayer.activePlayerList)
            {
                DestroyGui(player);
            }
        }
		private uint BindDecay(BaseEntity entity, uint buildingid = 0)
		{
			DecayEntity decayEntity = entity.GetComponentInParent<DecayEntity>();      
			if(decayEntity != null)
			{
				if(buildingid == 0)  				
					buildingid = BuildingManager.server.NewBuildingID();					
						
			   decayEntity.AttachToBuilding(buildingid);
			}
			return buildingid;
		}
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (BaseEntityList != null)
                {
                    foreach (uint id in BaseEntityList)
                    {
                        BaseNetworkable entityID = BaseNetworkable.serverEntities.Find(id);
                        if (entityID.net.ID == entity.net.ID)
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex) { return null; }
            return null;
        }

        #endregion

        // Function loot box and auth in cupboard
        #region LootBox
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (BaseEntityList != null)
            {
                foreach (uint id in BaseEntityList)
                {
                    BaseNetworkable entityID = BaseNetworkable.serverEntities.Find(id);
                    if (entityID.net.ID == entity.net.ID)
                    {
                        if (!CanLoot)
                        {
                            StopLooting(player, "OnTryLootEntity");
                        }
                        else if (!PlayerAuth.Contains(player.userID))
                        {
                            StopLooting(player, "OnTryLootEntity");
                        }
                    }
                }
            }
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (BaseEntityList != null)
            {
                foreach (uint id in BaseEntityList)
                {
                    BaseNetworkable entityID = BaseNetworkable.serverEntities.Find(id);
                    if (entityID.net.ID == entity.net.ID)
                    {
                        if (CanLoot)
                        {
                            if (PlayerAuth.Contains(player.userID))
                            {
                                if (!NowLooted)
                                {
                                    NowLooted = true;
                                    Server.Broadcast($"{ChatPrefix} Игрок {player.displayName} залутал ящик в радиактивном доме. \nДом самоуничтожится через {TimerDestroyHouse} секунд");
                                    mytimer3 = timer.Once(TimerDestroyHouse, () =>
                                    {
                                        DestroyRadHouse();
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private void StopLooting(BasePlayer player, string message)
        {
            NextTick(() => player.EndLooting());
            if (PlayerAuth.Contains(player.userID))
            {
                SendReply(player, $"{ChatPrefix } Вы сможете залутать ящик, через: {mytimer.Delay - timercallbackdelay} секунд");
            }
            else { SendReply(player, $"{ChatPrefix} Вы должны быть авторизованы в шкафу для лута ящика"); }
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            var Cupboard = privilege as BuildingPrivlidge;
            var entity = privilege as BaseEntity;
            if (BaseEntityList != null)
            {
                foreach (uint id in BaseEntityList)
                {
                    BaseNetworkable entityID = BaseNetworkable.serverEntities.Find(id);
                    if (entityID.net.ID == entity.net.ID)
                    {
                        if(PlayerAuth.Contains(player.userID))
                        {
                            SendReply(player, $"{ChatPrefix} Вы уже авторизованы в шкафу");
                            return false;
                        }
                        foreach (var authPlayer in BasePlayer.activePlayerList)
                        {
                            if (PlayerAuth.Contains(authPlayer.userID))
                            {
                                SendReply(authPlayer, $"{ChatPrefix} Вас выписал из шкафа игрок {player.displayName}");
                            }
                        }
                        CanLoot = false;
                        PlayerAuth.Clear();
                        timer.Destroy(ref mytimer);
                        timer.Destroy(ref mytimer2);
                        timercallbackdelay = 0;
                        mytimer = timer.Once(TimerLoot, () =>
                        {
                            CanLoot = true;
                            foreach (var authPlayer in BasePlayer.activePlayerList)
                            {
                                if (PlayerAuth.Contains(authPlayer.userID))
                                {
                                    SendReply(authPlayer, $"{ChatPrefix} Ящик разблокирован.");
                                }
                            }
                        });
                        mytimer2 = timer.Repeat(1f, 0, () =>
                        {
                            if (timercallbackdelay >= TimerLoot)
                            {
                                timercallbackdelay = 0;
                                timer.Destroy(ref mytimer2);
                            }
                            else
                            {
                                timercallbackdelay = timercallbackdelay + 1;
                            }
                        });

                        PlayerAuth.Add(player.userID);
                        SendReply(player, $"{ChatPrefix} Через {TimerLoot} секунд вы сможете залутать ящик радиационного дома");
                        return false;
                    }
                }
            }
            return null;
        }
        #endregion

        // GUI Create and Destroy
        #region GUI
        void CreateGui(BasePlayer player)
        {
            if (GuiOn)
            {
                Vector3 pos = (Vector3)success;
                CuiElementContainer Container = new CuiElementContainer();
                CuiElement RadUI = new CuiElement
                {
                    Name = "RadUI",
                    
                    Components = {
                        new CuiImageComponent {
                            Color = ColorCfg
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.644 0.023",
                            AnchorMax = "0.833 0.063",
                        }
                    }
                };

                CuiElement RadText = new CuiElement
                {
                    Name = "RadText",
                    Parent = "HouseUI",
                    Components = {
                        new CuiTextComponent {
                            Text = $"{TextGUI} {pos.ToString()}",
                            Color = "0.76 0.76 0.76 1.00",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.1 0",
                            AnchorMax = "1 1"
                        }
                    }
                };
                CuiElement HouseUI = new CuiElement
                {
                    Name = "HouseUI",
                    Parent = "RadUI",
                    Components = {
                        new CuiRawImageComponent {
                            Url = "https://i.imgur.com/y9Uaikm.png",
                            Color = "1 1 1 1",

                        },
                        new CuiRectTransformComponent {
                        AnchorMin = "0 0",
                         AnchorMax = "1 1"
                        }
                }
                };
                CuiElement HouseIcon = new CuiElement
                {
                    Name = "HouseIcon",
                    Parent = "RadUI",
                    Components = {
                        new CuiRawImageComponent {
                            Url = "https://i.imgur.com/dXYWqc7.png",
                            Color = "0.56 0.19 0.11 1.00"
                        },
                        new CuiRectTransformComponent {
                        AnchorMin = "0.0 0.0001",
                        AnchorMax = "0.125 0.9999"
                        }
                }
                };
                
                Container.Add(RadUI);
                Container.Add(HouseUI);
                Container.Add(RadText);
               Container.Add(HouseIcon);
               
                CuiHelper.AddUi(player, Container);
            }
        }

        void DestroyGui(BasePlayer player)
        {
            if (GuiOn)
            {
                CuiHelper.DestroyUi(player, "RadUI");
                CuiHelper.DestroyUi(player, "HouseUI");
            }
        }
        #endregion

        #region Radiation Control
        private void InitializeZone(Vector3 Location, float intensity, float radius, int ZoneID)
        {
            if (!ConVar.Server.radiation)
                ConVar.Server.radiation = true;

            var newZone = new GameObject().AddComponent<RadZones>();
            newZone.Activate(Location, radius, intensity, ZoneID);

            ZoneList listEntry = new ZoneList { zone = newZone };
            RadHouseZone = listEntry;
            RadiationZones.Add(listEntry);
        }
        private void DestroyZone(ZoneList zone)
        {
            if (RadiationZones.Contains(zone))
            {
                var index = RadiationZones.FindIndex(a => a.zone == zone.zone);
                UnityEngine.Object.Destroy(RadiationZones[index].zone);
                RadiationZones.Remove(zone);
            }
        }
        public class ZoneList
        {
            public RadZones zone;
        }

        public class RadZones : MonoBehaviour
        {
            private int ID;
            private Vector3 Position;
            private float ZoneRadius;
            private float RadiationAmount;

            private List<BasePlayer> InZone;

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "NukeZone";

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }

            public void Activate(Vector3 pos, float radius, float amount, int ZoneID)
            {
                ID = ZoneID;
                Position = pos;
                ZoneRadius = radius;
                RadiationAmount = amount;

                gameObject.name = $"RadHouse{ID}";
                transform.position = Position;
                transform.rotation = new Quaternion();
                UpdateCollider();
                gameObject.SetActive(true);
                enabled = true;

                var Rads = gameObject.GetComponent<TriggerRadiation>();
                Rads = Rads ?? gameObject.AddComponent<TriggerRadiation>();
                Rads.RadiationAmountOverride = RadiationAmount;
                Rads.radiationSize = ZoneRadius;
                Rads.interestLayers = playerLayer;
                Rads.enabled = true;

                if (IsInvoking("UpdateTrigger")) CancelInvoke("UpdateTrigger");
                InvokeRepeating("UpdateTrigger", 5f, 5f);
            }

            private void OnDestroy()
            {
                CancelInvoke("UpdateTrigger");
                Destroy(gameObject);
            }

            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                {
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = ZoneRadius;
                }
            }
            private void UpdateTrigger()
            {
                InZone = new List<BasePlayer>();
                int entities = Physics.OverlapSphereNonAlloc(Position, ZoneRadius, colBuffer, playerLayer);
                for (var i = 0; i < entities; i++)
                {
                    var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    if (player != null)
                        InZone.Add(player);
                }
            }
        }
        #endregion

    }
}