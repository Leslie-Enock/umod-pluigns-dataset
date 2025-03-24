using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Plugins
{
    [Info("No Mini", "Sche1sseHund", 0.9)]
    [Description("Prevents users from using/buying mini and scrap transport helicopters")]
    class NoMini : RustPlugin
	{
		private ConfigFile CfgFile; 
        
        private const string PERMISSION_ALLOWMINI = "nomini.allowmini";	
        private const string PERMISSION_ALLOWATTACK = "nomini.allowattack";	
        private const string PERMISSION_ALLOWSCRAPPY = "nomini.allowscrappy";	
        private const string PERMISSION_ALLOWAIRWOLFVEND = "nomini.allowairwolf";	
        
        
        private const string PREFAB_MINICOPTER = "assets/content/vehicles/minicopter/minicopter.entity.prefab";        
        private const string PREFAB_SCRAPTRANSPORT = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";               
        private const string PREFAB_ATTACKCOPTER = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab";
		private const string PREFAB_BANDITNPC1="assets/prefabs/npc/bandit/shopkeepers/bandit_conversationalist.prefab";
        private const string PREFAB_BANDITNPC1_SHORT="bandit_conversationalist";
        
        
        void Init()
        {        	
			permission.RegisterPermission(PERMISSION_ALLOWMINI, this); 
            permission.RegisterPermission(PERMISSION_ALLOWSCRAPPY, this); 
            permission.RegisterPermission(PERMISSION_ALLOWATTACK, this);             
            permission.RegisterPermission(PERMISSION_ALLOWAIRWOLFVEND, this);   
           
        }        
        
        object OnEngineStart(BaseVehicle vehicle, BasePlayer driver)
		{	
        	
        	
            if((vehicle.name == PREFAB_MINICOPTER) && !permission.UserHasPermission(driver.UserIDString,PERMISSION_ALLOWMINI))
            {
            	PrintToChat(driver, lang.GetMessage("NoMini", this, driver.UserIDString));   
                return false;
            }   
                
            if((vehicle.name == PREFAB_SCRAPTRANSPORT) && !permission.UserHasPermission(driver.UserIDString,PERMISSION_ALLOWSCRAPPY))
            {                	
             	PrintToChat(driver, lang.GetMessage("NoScrappy", this, driver.UserIDString));   
				return false;
			}
                
            if((vehicle.name == PREFAB_ATTACKCOPTER) && !permission.UserHasPermission(driver.UserIDString,PERMISSION_ALLOWATTACK))
            {                	
             	PrintToChat(driver, lang.GetMessage("NoAttack", this, driver.UserIDString));   
				return false;
			}
            
    		return null;
		}
        
        
        void OnEntitySpawned(BaseVehicle entity)
		{
			
        	if((entity.name == PREFAB_MINICOPTER && CfgFile.KillMiniOnSpawn) || (entity.name == PREFAB_SCRAPTRANSPORT && CfgFile.KillScrappyOnSpawn)  || (entity.name == PREFAB_ATTACKCOPTER && CfgFile.KillAttackOnSpawn))
            {   
                    NextTick(() => { KillMini((BaseVehicle)  entity); });                    
            }
                
			
		}
        void KillMini(BaseVehicle entity)
        {           				
           
           if(CfgFile.ExplodeOnKill)            
				entity.DieInstantly();				
            else
            	entity.Kill();                     
                
		}
        
        object OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
		{
        	//Puts(npcTalking.name);
            if((npcTalking.name == PREFAB_BANDITNPC1 || npcTalking.name == PREFAB_BANDITNPC1_SHORT) && CfgFile.DisableBanditVendor  && !permission.UserHasPermission(player.UserIDString,PERMISSION_ALLOWAIRWOLFVEND))
            {
            	PrintToChat(player, lang.GetMessage("NoVendor", this, player.UserIDString));   
        		return false; 
            }
             
    		return null;
		}
        #region Config
        
        public class ConfigFile
        {         	
        	public bool KillMiniOnSpawn { get; set; }
            public bool KillAttackOnSpawn { get; set; }
            public bool KillScrappyOnSpawn { get; set; }
            public bool ExplodeOnKill  { get; set; }
            public bool DisableBanditVendor  { get; set; }
        	
            public ConfigFile()
			{            	
                
            		KillMiniOnSpawn = true;
                    KillAttackOnSpawn = true;
                    KillScrappyOnSpawn = true;
                    ExplodeOnKill = true;
                    DisableBanditVendor =true;
	        }
        	
    	}
        
        protected override void LoadDefaultConfig()
        {
			PrintWarning("Creating a new default configuration file.");
        	CfgFile = new ConfigFile();
		}

        protected override void LoadConfig()
        {
			base.LoadConfig();
			try
	        {
        		CfgFile = Config.ReadObject<ConfigFile>();
				if(CfgFile == null) CreateNewConfig();
            }
			catch { CreateNewConfig(); }
        }

        protected override void SaveConfig() => Config.WriteObject(CfgFile);

        private void CreateNewConfig()
        {
	    	PrintWarning($"Configuration file is not valid. Creating a new one.");
			CfgFile = new ConfigFile();
	        SaveConfig();
		}
        
        #endregion
        
        #region Lang
        
        protected override void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoMini"] = "You are not allowed to use minicopters on this server.",				
                ["NoScrappy"] = "You are not allowed to use scrap transport helicopters on this server.",	
                ["NoAttack"] = "You are not allowed to use personal attack helicopters on this server.",	
                ["NoVendor"] = "You are not allowed to talk to this vendor.",
			}, this, "en");
            
            // Espanol
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoMini"] = "No estÃƒÆ’Ã‚Â¡ permitido usar minicÃƒÆ’Ã‚Â³pteros en este servidor",				
                ["NoScrappy"] = "No estÃƒÆ’Ã‚Â¡ permitido usar helicÃƒÆ’Ã‚Â³pteros chatarra en este servidor",	
                ["NoAttack"] = "No está permitido utilizar helicópteros de ataque personales en este servidor",	
                ["NoVendor"] = "No tienes permitido hablar con esta tendera.",
			}, this, "es");
            
            // Deutsch
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoMini"] = "Auf diesem Server darfst du keine Minikopter benutzen",				
                ["NoScrappy"] = "Sie kÃƒÆ’Ã‚Â¶nnen auf diesem Server keine Schrott Helikopter verwenden",	
                ["NoAttack"] = "Der Einsatz persönlicher Kampfhubschrauber ist auf diesem Server nicht gestattet.",	
                ["NoVendor"] = "Sie dÃ¼rfen nicht mit diesem Ladenbesitzer sprechen.",
			}, this, "de");
		}
        
        #endregion
        
        
	}
}
