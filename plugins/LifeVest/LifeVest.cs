using System;
using System.Collections.Generic;
using System.Linq; 
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
            
namespace Oxide.Plugins
{    
    [Info("Life Vest", "baconjake", "0.6.2")]
    [Description("Allows players a way to revive at death location")]

    public class LifeVest : RustPlugin
    {
        private const string PERMISSION_USE = "lifevest.use";
        private const string PERMISSION_GIVE = "lifevest.give";
        private const string PERMISSION_NOCD = "lifevest.nocd";
        private Dictionary<string, float> lifeVestCooldowns = new Dictionary<string, float>();
        private BaseEntity parentEntity = null;
        private Vector3 deathPosition = Vector3.zero;

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LifeVestButtonText"] = "LifeVest",
                ["NoPermission"] = "You do not have permission to use the LifeVest.",
                ["Usage"] = "Usage: /giveLifeVest <playerName>",
                ["Cooldown"] = "Your LifeVest is on cooldown.",
                ["NoCooldown"] = "There is no cooldown on LifeVest",
                ["PlayerNotFound"] = "Player not found.",
                ["LifeVestError"] = "Error: LifeVest item not created.",
                ["LifeVestGranted"] = "LifeVest granted to {0}.",
                ["LifeVestsGranted"] = "LifeVests have been granted to all eligible players.",
                ["LifeVestReceived"] = "You've been granted a LifeVest. Use it wisely!",
                ["LifeVestNotGiven"] = "Error: LifeVest could not be given to the target player.",
                ["ServerMessage"] = "{0} has defied the odds and returned to the battlefield!"
            }, this);
    }
        
	private class ConfigData
	{
    	public string Version { get; set; }
   		public float LifeVestDurabilityLoss { get; set; }
    	public float StartingDurability { get; set; }
   		public int RevivePlayerHealth { get; set; }
    	public ulong ItemSkinId { get; set; }
    	public string ItemShortName { get; set; }
    	public string ItemDisplayName { get; set; }
    	public bool EnableDurabilityLoss { get; set; }
    	public bool EnableServerMessage { get; set; }
    	public int LifeVestCooldown { get; set; }  
	}

    private ConfigData configData;

    private bool IsWearingLifeVest(BasePlayer player)
    {
        Item chestplate = player.inventory.containerWear.FindItemByItemID(ItemManager.FindItemDefinition(configData.ItemShortName).itemid);
        return chestplate != null && chestplate.skin == configData.ItemSkinId && chestplate.condition > 0;
    }

    private void LoadConfigData()
    {
        configData = Config.ReadObject<ConfigData>();

        if (configData.Version != Version.ToString())
        {
        PrintWarning("Detected configuration mismatch. Updating to the current version...");
        LoadDefaultConfig();
        }
    }
 
    private void SaveConfigData()
    {
        Config.WriteObject(configData, true);
    }

    protected override void LoadDefaultConfig()
    {
        configData = new ConfigData
        {
        Version = Version.ToString(),
        LifeVestDurabilityLoss = 0.15f,
        StartingDurability = 0.95f,
        RevivePlayerHealth = 100,
        ItemSkinId = 2943487371,
        ItemShortName = "metal.plate.torso",
        ItemDisplayName = "LIFEVEST",
        LifeVestCooldown = 60,
        EnableDurabilityLoss = true,
        EnableServerMessage = true
        };
        SaveConfigData();
    }

    private void Init()
    {
        LoadConfigData();
        permission.RegisterPermission(PERMISSION_USE, this);
        permission.RegisterPermission(PERMISSION_GIVE, this);
        permission.RegisterPermission(PERMISSION_NOCD, this);
    }

    private Quaternion deathRotation = Quaternion.identity;
    void OnPlayerDeath(BasePlayer player, HitInfo info)
	{   
    	if (!IsWearingLifeVest(player)) return;
       
    	Item chestplate = player.inventory.containerWear.FindItemByItemID(ItemManager.FindItemDefinition(configData.ItemShortName).itemid);
    	if (configData.EnableDurabilityLoss && chestplate != null)
    	{
        chestplate.conditionNormalized -= configData.LifeVestDurabilityLoss;  // Reduce the chestplate's durability and update it
        chestplate.MarkDirty();
    	}
    	parentEntity = player.GetParentEntity();

    	if (parentEntity != null && !parentEntity.IsDestroyed)
    	{
            deathPosition = parentEntity.transform.InverseTransformPoint(player.transform.position);
            deathRotation = Quaternion.Inverse(parentEntity.transform.rotation) * player.transform.rotation;  // Store the relative rotation

            var grandparentEntity = parentEntity.GetParentEntity();  // Check for an additional parent
            if (grandparentEntity != null && !grandparentEntity.IsDestroyed)
            {
            deathPosition = grandparentEntity.transform.InverseTransformPoint(parentEntity.transform.TransformPoint(deathPosition));
            deathRotation = Quaternion.Inverse(grandparentEntity.transform.rotation) * parentEntity.transform.rotation * deathRotation;
            parentEntity = grandparentEntity;
            }
        }
   	 	else
        {
        deathPosition = player.transform.position;
        deathRotation = player.transform.rotation;
        }
        CreateLifeVestButton(player);
	}

    private void CreateLifeVestButton(BasePlayer player)  // Revive Button Customization
    {
        var elements = new CuiElementContainer();
        var lifeVestButton = elements.Add(new CuiButton
        {
            Button = { Command = "lifevest.revive", Color = "0.576 0.706 0.145 0.40" },
            RectTransform = { AnchorMin = "0.83 0.89", AnchorMax = "0.95 0.97" },
            Text = {
                    Text = lang.GetMessage("LifeVestButtonText", this, player.UserIDString),
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                    }       
        }, "Hud.Menu", "LifeVestButton");
       CuiHelper.AddUi(player, elements);
    }

    private void RemoveLifeVestButton(BasePlayer player)
    {
       CuiHelper.DestroyUi(player, "LifeVestButton");
    }

   	private void RevivePlayer(BasePlayer player, float lifeVestCondition = -1f)
	{
    	if (IsWearingLifeVest(player) && parentEntity != null && !parentEntity.IsDestroyed)
    	{
        Vector3 worldPosition = parentEntity.transform.TransformPoint(deathPosition);
        Quaternion worldRotation = parentEntity.transform.rotation * deathRotation;  // Calculate the world rotation

        player.RespawnAt(worldPosition, worldRotation);
        player.health = configData.RevivePlayerHealth;
        player.inventory.Strip();
    	}
    	else
    	{
        player.RespawnAt(player.transform.position, player.transform.rotation);
        player.health = configData.RevivePlayerHealth;
        player.inventory.Strip();

        parentEntity = null;  // Resets the parent entity and position if the player revives without LifeVest
        deathPosition = Vector3.zero;
        deathRotation = Quaternion.identity;
        }
 
    	if (lifeVestCondition > 0) // Give back the LifeVest to the player if the condition is specified
    	{
            ItemDefinition itemDef = ItemManager.FindItemDefinition(configData.ItemShortName);
            Item item = ItemManager.Create(itemDef, 1, configData.ItemSkinId);

            if (item != null)
            {
        	item.name = configData.ItemDisplayName;
        	item.condition = lifeVestCondition;
        	item.MarkDirty();

        	player.inventory.GiveItem(item);
            }
        }
        RemoveLifeVestButton(player);
    }

	void OnPlayerRespawned(BasePlayer player)
	{
    	RemoveLifeVestButton(player);  // Remove the lifevest button
        
    	parentEntity = null;  // Reset necessary variables
    	deathPosition = Vector3.zero;
    	deathRotation = Quaternion.identity;
	}

[ConsoleCommand("lifevest.revive")]
    private void LifeVestReviveCommand(ConsoleSystem.Arg args)
    {
        var player = args.Player();
        if (player == null) return;
        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
        {
        player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
        return;
        }
       	if (!permission.UserHasPermission(player.UserIDString, PERMISSION_NOCD))   // Check if the player has the cooldown bypass permission
    	{
        	float lastUseTime;
        	if (lifeVestCooldowns.TryGetValue(player.UserIDString, out lastUseTime))
        	{
            	if (Time.realtimeSinceStartup - lastUseTime < configData.LifeVestCooldown)
				{
    			player.ChatMessage(lang.GetMessage("Cooldown", this, player.UserIDString)); 
   				return;
				}
        	}
            lifeVestCooldowns[player.UserIDString] = Time.realtimeSinceStartup;  // Update the cooldown
    	}
        if (player.IsDead())  // Ensure the player is actually dead and not just downed
        {
            if (IsWearingLifeVest(player))
            {
            Item chestplate = player.inventory.containerWear.FindItemByItemID(ItemManager.FindItemDefinition(configData.ItemShortName).itemid);
            float lifeVestCondition = chestplate.condition;  // Save the LifeVest's condition
            RevivePlayer(player, lifeVestCondition);
            }
            else
            {
            RevivePlayer(player);
            }
            RemoveLifeVestButton(player); // Always remove the button after the player has been revived
    
            if (configData.EnableServerMessage) 
            {
            string announcement = string.Format(lang.GetMessage("ServerMessage", this, player.UserIDString), player.displayName);
            Server.Broadcast(announcement);
            }
        }
    }

[ChatCommand("givelifevestall")]
    private void GiveLifeVestAllCommand(BasePlayer player, string command, string[] args)
    {
        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_GIVE)) return;

            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(target.UserIDString, PERMISSION_USE))
                {
                GiveLifeVestToPlayer(target);
                }
            }

        player.ChatMessage(lang.GetMessage("LifeVestsGranted", this, player.UserIDString));
    } 

    private void GiveLifeVestToPlayer(BasePlayer target)
    {
        ItemDefinition itemDef = ItemManager.FindItemDefinition(configData.ItemShortName);
        Item item = ItemManager.Create(itemDef, 1, configData.ItemSkinId);

        if (item == null) return;

        item.name = configData.ItemDisplayName;
        item.condition = item.maxCondition * configData.StartingDurability;
        item.MarkDirty();

        target.inventory.GiveItem(item);
    }

[ChatCommand("giveLifeVest")]
    private void GiveLifeVestCommand(BasePlayer player, string command, string[] args)
    {
        if (!permission.UserHasPermission(player.UserIDString, PERMISSION_GIVE)) return;

        if (args.Length == 0)
        {
        player.ChatMessage(lang.GetMessage("Usage", this, player.UserIDString)); 
        return;
        }
        string targetName = args[0];
        BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
        player.ChatMessage(lang.GetMessage("PlayerNotFound", this, player.UserIDString)); 
        return;
        }
        ItemDefinition itemDef = ItemManager.FindItemDefinition(configData.ItemShortName); 
        Item item = ItemManager.Create(itemDef, 1, configData.ItemSkinId); 

        if (item == null)
        {
        player.ChatMessage(lang.GetMessage("LifeVestError", this, player.UserIDString)); 
        return;
        }
        else
		{
    	player.ChatMessage(string.Format(lang.GetMessage("LifeVestGranted", this, player.UserIDString), target.displayName));
    	target.ChatMessage(lang.GetMessage("LifeVestReceived", this, target.UserIDString)); 
		}

    	item.name = configData.ItemDisplayName; // Set item name 
    	item.condition = item.maxCondition * configData.StartingDurability; 
    	item.MarkDirty();

   	 	target.inventory.GiveItem(item);

        if (target.inventory.containerMain.FindItemByUID(item.uid) == null &&  // Check if the LifeVest item is present in the target's inventory
            target.inventory.containerBelt.FindItemByUID(item.uid) == null &&
            target.inventory.containerWear.FindItemByUID(item.uid) == null)
        {
        player.ChatMessage(lang.GetMessage("LifeVestNotGiven", this, player.UserIDString)); 
        return;
        }
    }

 [ChatCommand("lvc")]
        private void LifeVestCooldownCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
            player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString)); 
            return;
            }

            float lastUseTime;
            if (lifeVestCooldowns.TryGetValue(player.UserIDString, out lastUseTime))
            {
                float timePassed = Time.realtimeSinceStartup - lastUseTime;
                float timeRemaining = configData.LifeVestCooldown - timePassed;

                if (timeRemaining > 0)
                {
                player.ChatMessage($"Cooldown remaining: {timeRemaining.ToString("0.##")} seconds");
                }
                else
                {
                player.ChatMessage(lang.GetMessage("NoCooldown", this, player.UserIDString)); 
                }
            }
            else
            {
                player.ChatMessage(lang.GetMessage("NoCooldown", this, player.UserIDString)); 
            }
        }
    }
} 
