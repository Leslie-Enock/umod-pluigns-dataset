using System;                   //config
using System.Collections.Generic;   //config

namespace Oxide.Plugins
{
	[Info("Hazmat Skin Changer", "MasterSplinter", "1.0.3")]
	[Description("Craft/Skin any Hazmat instead of the stock Hazmat for players with permission.")]

/*======================================================================================================================= 
*
*   Thx to BuzZ the original creator of this plugin
*
*=======================================================================================================================*/


	public class HazmatSkinChanger : RustPlugin
	{

        string Prefix = "Hazmat Skin Changer: ";       // CHAT PLUGIN PREFIX
        string PrefixHelp = "Hazmat Skin Changer";       // CHAT PLUGIN PREFIX HELP
        string PrefixColor = "#555555";                 // CHAT PLUGIN PREFIX COLOR
        ulong SteamIDIcon = 76561199133165664;          //  STEAMID created for this plugin 76561199133165664
        private bool ConfigChanged;
        string stocksuit = "Hazmat Suit";
        string suitblue = "Revised Blue Scientist Suit";
        string suitgreen = "Revised Green Peacekeeper Suit";
        string suitheavy = "Revised Heavy Scientist Suit";
        string suitarctic = "Revised Arctic Scientist Suit";
        string arcticsuit = "Revised Arctic Suit";
        string suitspace = "Revised Space Suit";
        string suitnomad = "Revised Nomad Suit";


        bool loaded = false;
        bool debug = true;

        const string HTSS_craftS = "hazmatskinchanger.craft_bluesuit";//to be able to craft blue scientistsuit
        const string HTSS_skinS = "hazmatskinchanger.skin_bluesuit";//to be able to skin blue scientistsuit
        const string HTSS_wearS = "hazmatskinchanger.wear_bluesuit";//to be able to use blue scientist suit

        const string HTSS_craftGS = "hazmatskinchanger.craft_greensuit";//to be able to craft green scientist suit      
        const string HTSS_skinGS = "hazmatskinchanger.skin_greensuit";//to be able to skin green scientist suit
        const string HTSS_wearGS = "hazmatskinchanger.wear_greensuit";//to be able to use green scientist suit

        const string HTSS_craftH = "hazmatskinchanger.craft_heavy";//to be able to craft heavy scientist suit
        const string HTSS_skinH = "hazmatskinchanger.skin_heavy";//to be able to skin heavy scientist suit
        const string HTSS_wearH = "hazmatskinchanger.wear_heavy";//to be able to use heavy scientist suit

        const string HTSS_craftASS = "hazmatskinchanger.craft_arctic_scientist";//to be able to craft arctic scientist suit
        const string HTSS_skinASS = "hazmatskinchanger.skin_arctic_scientist";//to be able to skin arctic scientist suit
        const string HTSS_wearASS = "hazmatskinchanger.wear_arctic_scientist";//to be able to use artic scientist suit

        const string HTSS_craftAS = "hazmatskinchanger.craft_arcticsuit";//to be able to craft arctic suit
        const string HTSS_skinAS = "hazmatskinchanger.skin_arcticsuit";//to be able to skin arctic suit
        const string HTSS_wearAS = "hazmatskinchanger.wear_arcticsuit";//to be able to use artic suit

        const string HTSS_craftSS = "hazmatskinchanger.craft_spacesuit";//to be able to craft the space suit (does not change players skinned hazmat suits)
        const string HTSS_skinSS = "hazmatskinchanger.skin_spacesuit";//to be able to skin the space suit (does not change players skinned hazmat suits)
        const string HTSS_wearSS = "hazmatskinchanger.wear_spacesuit";//to be able to use the space suit (does not change players skinned hazmat suits)

        const string HTSS_craftNS = "hazmatskinchanger.craft_nomadsuit";//to be able to craft the nomad suit (does not change players skinned hazmat suits)
        const string HTSS_skinNS = "hazmatskinchanger.skin_nomadsuit";//to be able to skin the nomad suit (does not change players skinned hazmat suits)                         
        const string HTSS_wearNS = "hazmatskinchanger.wear_nomadsuit";//to be able to use the nomad suit (does not change players skinned hazmat suits)                         


        protected override void LoadDefaultConfig()

        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "Hazmat Skin Changer: "));                  // CHAT PLUGIN PREFIX
            PrefixHelp = Convert.ToString(GetConfig("Chat Settings", "PrefixHelp", "Hazmat Skin Changer"));                  // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#47ff6f"));                       // CHAT PLUGIN PREFIX COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Icon", "SteamIDIcon", 76561199133165664));                   // SteamID FOR PLUGIN ICON
            suitblue = Convert.ToString(GetConfig("Suit Name", "Blue scientist suit", "Revised Blue Scientist Suit"));
            suitgreen = Convert.ToString(GetConfig("Suit Name", "Green scientist suit", "Revised Green Peacekeeper Suit"));
            suitheavy = Convert.ToString(GetConfig("Suit Name", "Heavy scientist suit", "Revised Heavy Scientist Suit"));
            suitarctic = Convert.ToString(GetConfig("Suit Name", "Arctic scientist suit", "Revised Arctic Scientist Suit"));
            arcticsuit = Convert.ToString(GetConfig("Suit Name", "Arctic suit", "Revised Arctic Suit"));
            suitspace = Convert.ToString(GetConfig("Suit Name", "Space suit", "Revised Space Suit"));
            suitnomad = Convert.ToString(GetConfig("Suit Name", "Nomad suit", "Revised Nomad Suit"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        #region MESSAGES / LANG

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"TransMsg", "[#e0e0e0]Your crafted Hazmat Suit has transformed to a[/#]"},
                {"BackTransMsg", "[#e0e0e0]It has returned to a classic Hazmat Suit[/#]"},
                {"NoPermMsg", "[#e0e0e0]You are not allowed to wear a[/#]"},
                {"need_hold_hazmat", "[#e0e0e0]You are not holding a Hazmat![/#]"},
                {"hazmat_skinned", "[#e0e0e0]Your Hazmat has successfully skinned to a[/#]"},
                {"noperm_hazmat_skin", "[#e0e0e0]You are not allowed to skin your Hazmat to a[/#]"},
                {"skinitem_help", "\n[#eeeeee]<size=14>Available Commands:</size>[/#]\n[#ffe479]/skinhazmat[/#] [#e0e0e0]- Display Help[/#]\n[#ffe479]/skinhazmat <Available Suits>[/#] [#e0e0e0]- Skin Hazmat to selected suit[/#]\n \n[#ffd479]<size=14>Available Suits:</size>[/#]\n[#ffe479]\"blue\"[/#] [#e0e0e0]- Blue Scientist Suit[/#]\n[#ffe479]\"green\"[/#] [#e0e0e0]- Green Peacekeeper Suit[/#]\n[#ffe479]\"heavy\"[/#] [#e0e0e0]- Heavy Scientist Suit[/#]\n[#ffe479]\"arcticscientist\"[/#] [#e0e0e0]- Arctic Scientist Suit[/#]\n[#ffe479]\"arctic\"[/#] [#e0e0e0]- Arctic Suit[/#]\n[#ffe479]\"space\"[/#] [#e0e0e0]- Space Suit[/#]\n[#ffe479]\"nomad\"[/#] [#e0e0e0]- Nomad Suit[/#]\n \n[#73c2fa]<size=14>Example:</size>[/#]\n[#ffe479]/skinhazmat space[/#] [#e0e0e0]- This will skin to a Space Suit[/#]"},

                #region Templates/Color Codes

                //{"Template_help", "\n[#eeeeee]<size=14>Title of Section:</size>[/#]\n[#ffe479]/commandhere 1[/#] [#e0e0e0]- Description of chatcommand[/#]\n[#ffe479]/commandhere 2[/#] [#e0e0e0]- Description of chatcommand[/#]\n \n[#ffd479]<size=14>Title of Section 2:</size>[/#]\n[#ffe479]\"commandhere\"[/#] [#e0e0e0]- Description of command[/#]\n[#ffe479]\"commandhere\"[/#] [#e0e0e0]- Description of command[/#]\n \n[#73c2fa]<size=14>Title of Section 3:</size>[/#]\n[#ffe479]\"anything\" [/#] [#e0e0e0]- Description of anything[/#]\n"},
                //{"Template_info", "[#e0e0e0]What you like to say to the player[/#]"},

                //Player.Message(player, $"{lang.GetMessage("Template_help", this, player.UserIDString)}", $"<color={PrefixColor}>{PrefixHelp/Prefix}</color>", SteamIDIcon);
                //Player.Message(player, $"{lang.GetMessage("Template_info", this, player.UserIDString)} <color=#ffe479>{player.UserIDString}.</color> {lang.GetMessage("Template_info", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix/PrefixHelp}</color>", SteamIDIcon);

                //#e0e0e0 Grey
                //#eeeeee Dark White 
                //#ffd479 Orange
                //#ffe479 Yellow
                //#73c2fa Blue
                //#f9f178 Red
                //#a0ffb5 Light Green                             

                #endregion

            }, this, "en");
        }

        #endregion

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

		private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(HTSS_craftS, this);
            permission.RegisterPermission(HTSS_skinS, this);
            permission.RegisterPermission(HTSS_wearS, this);

            permission.RegisterPermission(HTSS_craftGS, this);
            permission.RegisterPermission(HTSS_skinGS, this);
            permission.RegisterPermission(HTSS_wearGS, this);

            permission.RegisterPermission(HTSS_craftH, this);
            permission.RegisterPermission(HTSS_skinH, this);
            permission.RegisterPermission(HTSS_wearH, this);

            permission.RegisterPermission(HTSS_craftASS, this);
            permission.RegisterPermission(HTSS_skinASS, this);
            permission.RegisterPermission(HTSS_wearASS, this);

            permission.RegisterPermission(HTSS_craftAS, this);
            permission.RegisterPermission(HTSS_skinAS, this);
            permission.RegisterPermission(HTSS_wearAS, this);

            permission.RegisterPermission(HTSS_craftSS, this);
            permission.RegisterPermission(HTSS_skinSS, this);
            permission.RegisterPermission(HTSS_wearSS, this);

            permission.RegisterPermission(HTSS_craftNS, this);
            permission.RegisterPermission(HTSS_skinNS, this);
            permission.RegisterPermission(HTSS_wearNS, this);

            loaded = true;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (loaded == false) return;
            if (item == null) return;
            BasePlayer owner = task.owner as BasePlayer;
            int color = 1266491000;//stock suit
            string suitName = stocksuit;

            if (item.info.shortname == "hazmatsuit")
            {
                ulong unull = 0;
                if (permission.UserHasPermission(owner.UserIDString, HTSS_craftS) == true)
                {
                    item.UseItem();
                    color = -253079493;//blue scientist suit
                    suitName = suitblue;
                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftGS) == true)
                {
                    item.UseItem();
                    color = -1958316066;//green scientist suit
                    suitName = suitgreen;
                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftH) == true)
                {
                    item.UseItem();
                    color = -1772746857;//heavy scientist suit
                    suitName = suitheavy;

                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftASS) == true)
                {
                    item.UseItem();
                    color = 1107575710;//arctic scientist suit
                    suitName = suitarctic;

                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftAS) == true)
                {
                    item.UseItem();
                    color = -470439097;//arctic suit
                    suitName = arcticsuit;

                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftSS) == true)
                {
                    item.UseItem();
                    color = -560304835;//space suit
                    suitName = suitspace;
                }
                else if (permission.UserHasPermission(owner.UserIDString, HTSS_craftNS) == true)
                {
                    item.UseItem();
                    color = 491263800;//Nomad suit
                    suitName = suitnomad;
                }
                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                itemtogive.name = suitName;
                if (itemtogive == null) return;
                if (owner == null) return;
                if (suitName == stocksuit) return;
                owner.GiveItem(itemtogive);
                Player.Message(owner, $"{lang.GetMessage("TransMsg", this, owner.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
            }

        }

        void CanWearItem(PlayerInventory inventory, Item item, int targetPos)
        {
            if (loaded == false) { return; }
            if (item == null) { return; }
            if (inventory == null) { return; }
            BasePlayer wannawear = inventory.GetComponent<BasePlayer>();

            if (wannawear.IsConnected)
            {
                if (debug) Puts($"item.name = {item.name} || item.info.shortname = {item.info.shortname} || itemID = {ItemManager.FindItemDefinition(item.info.shortname)?.itemid}");
                if (item.name == null) return;
                if (item.name.Contains($"{suitblue}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitblue;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                    }
                }
                if (item.name.Contains($"{suitgreen}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearGS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitgreen;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitheavy}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearH) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitheavy;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitarctic}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearASS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitarctic;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{arcticsuit}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearAS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = arcticsuit;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitspace}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearSS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitspace;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitnomad}") == true)
                {
                    if (permission.UserHasPermission(wannawear.UserIDString, HTSS_wearNS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitnomad;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (wannawear == null) { return; }
                        wannawear.GiveItem(itemtogive);
                        Player.Message(wannawear, $"{lang.GetMessage("NoPermMsg", this, wannawear.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, wannawear.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
            }
        }

        void OnItemPickup(Item item, BasePlayer player, int targetPos)
        {
            if (loaded == false) { return; }
            if (item == null) { return; }

            if (player.IsConnected)
            {
                if (debug) Puts($"item.name = {item.name} || item.info.shortname = {item.info.shortname} || itemID = {ItemManager.FindItemDefinition(item.info.shortname)?.itemid}");
                if (item.name == null) return;
                if (item.name.Contains($"{suitblue}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitblue;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitgreen}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearGS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitgreen;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitheavy}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearH) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitheavy;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitarctic}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearASS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitarctic;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{arcticsuit}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearAS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = arcticsuit;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitspace}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearSS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitspace;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
                if (item.name.Contains($"{suitnomad}") == true)
                {
                    if (permission.UserHasPermission(player.UserIDString, HTSS_wearNS) == false) //if permission to use/wear the crafted suits
                    {
                        string suitName = suitnomad;

                        item.Remove();
                        ulong unull = 0;
                        Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1266491000).itemid, 1, unull);//gives regular hazmat suit if wear permission is false on attempt to wear the Revised suit
                        if (itemtogive == null) { return; }
                        if (player == null) { return; }
                        player.GiveItem(itemtogive);
                        Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)} <color=#ffe479>{suitName}.</color> {lang.GetMessage("BackTransMsg", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                    }
                }
            }
        }

        #region Commands

        [ChatCommand("skinhazmat")]
        private void skinhazmatCmd(BasePlayer player, string command, string[] args, Item item)
        {
            if (player == null) return;

            if (args.Length < 1)
            {
                Player.Message(player, $"{lang.GetMessage("skinitem_help", this, player.UserIDString)}", $"<color={PrefixColor}>{PrefixHelp}</color>", SteamIDIcon);

                return;
            }

            switch (args[0].ToLower())
            {
                case "blue":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinS) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = -253079493;//blue scientist suit
                                string suitName = suitblue;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);

                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinS) == false)
                        {
                            string suitName = suitblue;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "green":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinGS) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = -1958316066;//green scientist suit
                                string suitName = suitgreen;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinGS) == false)
                        {
                            string suitName = suitgreen;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "heavy":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinH) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = -1772746857;//heavy scientist suit
                                string suitName = suitheavy;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinH) == false)
                        {
                            string suitName = suitheavy;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "arcticscientist":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinASS) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = 1107575710;//arctic scientist suit
                                string suitName = suitarctic;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinASS) == false)
                        {
                            string suitName = suitarctic;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "arctic":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinAS) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = -470439097;//arctic scientist suit
                                string suitName = arcticsuit;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinAS) == false)
                        {
                            string suitName = arcticsuit;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "space":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinSS) == true) //if permision to skin
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = -560304835;//space suit
                                string suitName = suitspace;

                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinSS) == false)
                        {
                            string suitName = suitspace;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }

                case "nomad":
                    {
                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinNS) == true)//if permision to craft
                        {
                            if (player.GetActiveItem() == null)
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }

                            if (!player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                Player.Message(player, $"{lang.GetMessage("need_hold_hazmat", this, player.UserIDString)}", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                                return;
                            }

                            if (player.GetActiveItem().info.shortname.Contains("hazmatsuit"))
                            {
                                if (loaded == false) return;

                                int color = 491263800;//Nomad suit
                                string suitName = suitnomad;


                                player.GetActiveItem().UseItem();
                                ulong unull = 0;

                                Item itemtogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(color).itemid, 1, unull);
                                itemtogive.name = suitName;
                                if (itemtogive == null) { return; }
                                if (player == null) { return; }
                                player.GiveItem(itemtogive);
                                Player.Message(player, $"{lang.GetMessage("hazmat_skinned", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                            }
                        }

                        if (permission.UserHasPermission(player.UserIDString, HTSS_skinNS) == false)
                        {
                            string suitName = suitnomad;

                            Player.Message(player, $"{lang.GetMessage("noperm_hazmat_skin", this, player.UserIDString)} <color=#ffe479>{suitName}.</color>", $"<color={PrefixColor}>{Prefix}</color>", SteamIDIcon);
                        }

                        return;
                    }
            }
        }

        #endregion
    }
}