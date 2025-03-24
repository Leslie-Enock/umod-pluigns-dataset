using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("No Tech Tree", "Sche1sseHund", 1.2)]
    [Description("Allows server owner to disable the use of the tech tree")]

    class NoTechTree: RustPlugin
    {    
    	
        private const string PERMISSION_ALLOWBLOCKBYPASS = "notechtree.bypass";		
        
        private void Init()
		{
			permission.RegisterPermission(PERMISSION_ALLOWBLOCKBYPASS, this);
			
		}       
		private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {      	
            
        	if(permission.UserHasPermission(player.UserIDString, PERMISSION_ALLOWBLOCKBYPASS))
        		return null;             	                
				
			PrintToChat(player, lang.GetMessage("NoTechTree", this, player.UserIDString));                 
            return false;
        }
        
        
      
		protected override void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoTechTree"] = "The Tech Tree has been disabled.",				
			}, this, "en");
            
            // Espanol
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoTechTree"] = "El ÃƒÆ’Ã‚Â¡rbol tecnolÃƒÆ’Ã‚Â³gico se ha desactivado",				
			}, this, "es");
            
            // Deutsch
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoTechTree"] = "Der Forschungsbaum wurde deaktiviert.",				
			}, this, "de");
		}
        
    }
    
}
        