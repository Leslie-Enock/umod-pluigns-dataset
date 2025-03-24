using Oxide.Core.Plugins;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Text;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
	[Info("Computers Plus", "mr01sam", "1.0.6")]
	[Description("Adds new functionality to the computer stations.")]
	partial class ComputersPlus : CovalencePlugin
	{
		/*
		 * CHANGELOG
		 * - Fixed bug where players would not get emails if they were offline.
		 * - Added message screen for players without permissions.
		 * - Fixed broken images.
		 */
		private static ComputersPlus PLUGIN;

		[PluginReference]
		private Plugin ImageLibrary;

		/* Global Data */
		private static Dictionary<string, string> Language = new Dictionary<string, string>();
		private static Dictionary<string, ComputersPlusApp> RegisteredApps = new Dictionary<string, ComputersPlusApp>();
		private static Dictionary<string, string> AppsInUse = new Dictionary<string, string>();

		#region Properties
		internal EZUI.Component AppComponent { get; private set; } = null;
		private bool Success { get; set; } = true;
        #endregion

        #region Permissions
		private const string PermissionAdmin = "computersplus.admin";
		/* Additional permissions are registered for each App in the format computersplus.<app-name> */
		#endregion

		#region Initialization
		void CreateApps()
		{
			if (RegisteredApps == null)
            {
				RegisteredApps = new Dictionary<string, ComputersPlusApp>();
            }
            RegisterApp(EMAIL_APP);
        }

		void Init()
		{
			PLUGIN = this;
			UnsubscribeAll();
		}

		void Unload()
		{
			CleanupUI();
			UnloadApps();
			NullifyStaticVariables();
		}

		void OnServerSave()
		{
			SaveApps();
		}

		void OnServerInitialized(bool initial)
		{
			try
			{
				ImageLibrary.Call("isLoaded", null);
			}
			catch (Exception)
			{
				PrintWarning($"The required dependency ImageLibary is not installed, {Name} will not work properly without it.");
				Success = false;
				return;
            }
            EZUI.GenerateToken();
			LoadImages();
			InitApps();
			SubscribeAll();
		}

		void UnsubscribeAll()
        {
			Unsubscribe(nameof(OnEntityMounted));
			Unsubscribe(nameof(OnEntityDismounted));
			Unsubscribe(nameof(CanDismountEntity));
			Unsubscribe(nameof(OnBookmarkControl));
			Unsubscribe(nameof(OnBookmarkControlEnd));
		}

		void SubscribeAll()
		{
			Subscribe(nameof(OnEntityMounted));
			Subscribe(nameof(OnEntityDismounted));
			Subscribe(nameof(CanDismountEntity));
			Subscribe(nameof(OnBookmarkControl));
			Subscribe(nameof(OnBookmarkControlEnd));
		}

		void NullifyStaticVariables()
        {
			PLUGIN = null;
			RegisteredApps = null;
			AppsInUse = null;
			Language = null;
        }

		void CleanupUI()
        {
			for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
			{
				EZUI.DestroyAll(BasePlayer.activePlayerList[i]);
			}
		}

		void InitApps()
		{
			foreach(ComputersPlusApp app in RegisteredApps.Values)
            {
				ImageLibrary.Call<bool>("AddImage", app.IconUrl, $"{app.Id}Icon", 0UL);
				Call(app.InitMethod);
            }
		}

		void SaveApps()
		{
			foreach (ComputersPlusApp app in RegisteredApps.Values)
			{
				Call(app.SaveMethod);
			}
		}

		void UnloadApps()
		{
            foreach (ComputersPlusApp app in RegisteredApps.Values)
            {
                Call(app.UnloadMethod);
            }
        }
		#endregion

		#region Oxide Hooks
		void OnEntityMounted(ComputerStation entity, BasePlayer basePlayer)
		{
			ShowHomeUi(basePlayer);
		}

		void OnEntityDismounted(ComputerStation entity, BasePlayer basePlayer)
		{
			CloseUi(basePlayer);
			NextTick(() => 
			{
				CloseUi(basePlayer); // incase it doesn't close initally
			});
		}

		object CanDismountEntity(BasePlayer player, ComputerStation computerStation)
		{
            if (IsUsingApp(player))
            {
                return false;
            }
            return null;
		}

		object OnBookmarkControl(ComputerStation computerStation, BasePlayer basePlayer, string bookmarkName, IRemoteControllable remoteControllable)
		{
			CloseUi(basePlayer);
			return null;
		}

		object OnBookmarkControlEnd(ComputerStation computerStation, BasePlayer basePlayer, BaseEntity controlledEntity)
		{
			ShowHomeUi(basePlayer);
			return null;
		}
		#endregion

		#region Helper Methods
		void RegisterApp(ComputersPlusApp newApp)
        {
			RegisteredApps.Add(newApp.Id, newApp);
			if (!permission.PermissionExists(newApp.Permission))
            {
				permission.RegisterPermission(newApp.Permission, this);
			}
			Call(newApp.CreateMethod);
		}

		void LoadImages()
		{
			ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/43ytxMa.png", $"App.Close", 0UL);
			ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/oM0NEIP.png", $"PC.Denied", 0UL);
		}

		bool IsUsingComputer(BasePlayer basePlayer) => basePlayer.GetMounted() != null ? (basePlayer.GetMounted() is ComputerStation) : false;

		bool IsUsingApp(BasePlayer player, string appId = null)
		{
			if (appId != null)
				return AppsInUse.ContainsKey(player.UserIDString) && AppsInUse[player.UserIDString] == appId;
			return AppsInUse.ContainsKey(player.UserIDString);
		}

		bool UserHasAnyPermission(BasePlayer basePlayer)
        {
			return RegisteredApps.Values.Any(app => permission.UserHasPermission(basePlayer.UserIDString, app.Permission));
		}
		#endregion

		#region UI
		private readonly string BASE_UI_ID = "PC";
		private readonly string HOME_UI_ID = "HOME";
		private readonly string APP_UI_ID = "APP";

		void CloseUi(BasePlayer basePlayer)
        {
			AppsInUse.Remove(basePlayer.UserIDString);
			var ui = EZUI.Find(basePlayer.UserIDString, BASE_UI_ID);
			ui?.Destroy(basePlayer);
        }

		void CloseHomeUi(BasePlayer basePlayer)
        {
			var ui = EZUI.Find(basePlayer.UserIDString, $"{BASE_UI_ID}.Content.{HOME_UI_ID}");
			ui?.Destroy(basePlayer);
		}

		void CloseAppUi(BasePlayer basePlayer)
        {
			var ui = EZUI.Find(basePlayer.UserIDString, $"{BASE_UI_ID}.Content.{HOME_UI_ID}.Content.{APP_UI_ID}");
			ui?.Destroy(basePlayer);
			AppsInUse.Remove(basePlayer.UserIDString);
		}

		EZUI.Component CreateAppGrid(EZUI.Component home, BasePlayer basePlayer)
        {
			var grid = new EZUI.LayoutComponent("Apps")
			{
                Transparent = true,
                RowCount = 8,
                ColCount = 15,
				PixelPadding = new EZUI.Dir<int>(5)
            }.Create(home);
            foreach (ComputersPlusApp app in RegisteredApps.Values)
            {
				if (app != null && permission.UserHasPermission(basePlayer.UserIDString, app.Permission))
                {
					var box = new EZUI.BoxComponent(app.Id)
					{
						Transparent = true
					}.Create(grid);
					var icon = new EZUI.ImageComponent("Icon")
					{
						ImageKey = $"{app.Id}Icon",
						Size = 0.6f,
						AnchorTo = EZUI.Anchor.TopCenter
					}.Create(box);
					var text = new EZUI.TextComponent("Title")
					{
						Text = app.Name,
						TextAlign = TextAnchor.UpperCenter,
						FontSize = 10,
						PixelHeight = 25,
						FontColor = EZUI.StyleSheet.DEFAULT.FontColorLight1,
						AnchorTo = EZUI.Anchor.BottomCenter
					}.Create(box);
					var button = new EZUI.ButtonComponent("Click")
					{
						Transparent = true,
						Command = $"app.launch {app.Id}",
						ClickSfx = SFX_LAUNCH
					}.Create(box);
				}
            }
            return grid;
        }

		void ShowHomeUi(BasePlayer basePlayer)
		{
			if (Success)
            {
				bool hasAccess = UserHasAnyPermission(basePlayer);
				var ui = new EZUI.BoxComponent(BASE_UI_ID)
				{
					Height = 0.75f,
					Centered = true,
					Width = 0.75f,
				}.Create();
				var home = new EZUI.BoxComponent(HOME_UI_ID)
				{
					OutlinePixelWeight = 5,
					BackgroundColor = hasAccess ? EZUI.StyleSheet.DEFAULT.HomeBackgroundColor : EZUI.Color.BLACK,
					Centered = true
				}.Create(ui);
				if (hasAccess)
                {
					var version = new EZUI.TextComponent("Version")
					{
						AnchorTo = EZUI.Anchor.BottomRight,
						AutoSizeHeight = true,
						AutoSizeWidth = true,
						FontColor = EZUI.Color.WHITE,
						FontSize = 10,
						Up = 5,
						Left = 5,
						TextAlign = TextAnchor.LowerRight,
						Text = $"COBALT OS v{Version.Major}.{Version.Minor}.{Version.Patch}"
					}.Create(home);
					var grid = CreateAppGrid(home, basePlayer);
				}
				else
                {
					var denied = new EZUI.ImageComponent("Deny")
					{
						ImageKey = "PC.Denied",
						ImageColor = EZUI.Color.RED,
						Size = 0.2f,
						AnchorTo = EZUI.Anchor.MiddleCenter,
						Up = 50
					}.Create(home);
					var text = new EZUI.TextComponent("Text")
					{
						Text = Lang("app.access.denied", basePlayer),
						FontColor = EZUI.Color.RED,
						TextAlign = TextAnchor.UpperCenter,
						FontSize = 28,
						TopAlign = denied.BottomSide.Pixels,
						Down = 20,
						PixelHeight = 100
					}.Create(home);
					var reason = new EZUI.TextComponent("Reason")
					{
						Text = Lang("app.access.reason", basePlayer),
						FontColor = EZUI.Color.WHITE,
						TextAlign = TextAnchor.LowerCenter,
						AnchorTo = EZUI.Anchor.BottomCenter,
						FontSize = 12,
						PixelHeight = 100
					}.Create(home);
				}

				ui.Render(basePlayer);
			}
		}

		void ShowAppUi(BasePlayer basePlayer, ComputersPlusApp app)
		{
			var home = EZUI.Find(basePlayer, $"{BASE_UI_ID}.{HOME_UI_ID}");
			var window = new EZUI.WindowComponent(APP_UI_ID)
			{
				HeaderText = app.Name,
				Scale = 0.85f,
				Centered = true,
				OnCloseCommand = "app.close"
			}.Create(home);
			this.AppComponent = window;
			window.Render(basePlayer);
			if (!AppsInUse.ContainsKey(basePlayer.UserIDString))
            {
				AppsInUse.Add(basePlayer.UserIDString, app.Id);
			}
		}
		#endregion

		#region Commands
		[Command("app.launch")]
		private void cmd_app_launch(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;
			if (basePlayer != null && IsUsingComputer(basePlayer))
			{
				string appId = args[0];
				ComputersPlusApp app = null;
				if (RegisteredApps.TryGetValue(appId, out app) && permission.UserHasPermission(player.Id, app.Permission)) {
					ShowAppUi(basePlayer, app);
					Call(app.LaunchMethod, basePlayer.UserIDString);
				}
				else
                {
					player.Reply(Lang("app.launch.error", basePlayer));
                }
			}
		}

		[Command("app.close")]
		private void cmd_app_close(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;
			if (basePlayer != null)
            {
				CloseAppUi(basePlayer);
			}
		}

		[Command("pc.leave")]
		private void cmd_pc_leave(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;
			if (basePlayer != null)
            {
				EZUI.DestroyAll(basePlayer);
				BaseMountable mountable = basePlayer.GetMounted();
				if (mountable != null && mountable is ComputerStation)
				{
					mountable.DismountPlayer(basePlayer);
				}
			}
		}
		#endregion

	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus
    {
        class ComputersPlusApp {
            private string _id;
            private string _name;
            private string _iconUrl;
            private string _permission;

            public ComputersPlusApp(string id, string name, string iconUrl, string permission)
            {
                this._id = id;
                this._name = name;
                this._iconUrl = iconUrl;
                this._permission = permission;
            }
   
            public string Id
            {
                get { return _id; }
            }

            public string Name
            {
                get { return _name; }
            }

            public string IconUrl
            {
                get { return _iconUrl; }
            }

            public string Permission
            {
                get { return _permission; }
            }

            public string LaunchMethod
            {
                get { return $"Launch{Id.TitleCase()}App"; }
            }

            public string SaveMethod
            {
                get { return $"Save{Id.TitleCase()}App"; }
            }

            public string CreateMethod
            {
                get { return $"Create{Id.TitleCase()}App"; }
            }

            public string InitMethod
            {
                get { return $"Init{Id.TitleCase()}App"; }
            }

            public string UnloadMethod
            {
                get { return $"Unload{Id.TitleCase()}App"; }
            }

            public override string ToString()
            {
                return Id.ToString();
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
		private Configuration config;

		private partial class Configuration 
		{

		}

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Default configuration values will be used. It is recommended to backup your current configuration file and remove it to generate a fresh one.");
				LoadDefaultConfig();
			}
			base.SaveConfig();
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig() => config = new Configuration();

	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus
    {
        class DoubleDictionary<TkeyA, TkeyB, Tvalue>
		{
			private Dictionary<TkeyA, Dictionary<TkeyB, Tvalue>> a2b;
			private Dictionary<TkeyB, Dictionary<TkeyA, Tvalue>> b2a;

			public DoubleDictionary()
			{
				a2b = new Dictionary<TkeyA, Dictionary<TkeyB, Tvalue>>();
				b2a = new Dictionary<TkeyB, Dictionary<TkeyA, Tvalue>>();
			}

			public void Set(TkeyA keyA, TkeyB keyB, Tvalue value)
			{
				if (!a2b.ContainsKey(keyA))
					a2b.Add(keyA, new Dictionary<TkeyB, Tvalue>());
				if (!b2a.ContainsKey(keyB))
					b2a.Add(keyB, new Dictionary<TkeyA, Tvalue>());
				if (!a2b[keyA].ContainsKey(keyB))
					a2b[keyA].Add(keyB, value);
				else
					a2b[keyA][keyB] = value;
				if (!b2a[keyB].ContainsKey(keyA))
					b2a[keyB].Add(keyA, value);
				else
					b2a[keyB][keyA] = value;
			}

			public Tvalue Get(TkeyA keyA, TkeyB keyB)
			{
				if (a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB))
					return a2b[keyA][keyB];
				return default(Tvalue);
			}

			public Dictionary<TkeyB, Tvalue> GetA(TkeyA keyA)
			{
				if (a2b.ContainsKey(keyA))
					return a2b[keyA];
				return new Dictionary<TkeyB, Tvalue>();
			}

			public Dictionary<TkeyA, Tvalue> GetB(TkeyB keyB)
			{
				if (b2a.ContainsKey(keyB))
					return b2a[keyB];
				return new Dictionary<TkeyA, Tvalue>();
			}

			public bool ContainsKey(TkeyA keyA, TkeyB keyB)
			{
				return a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB);
			}

			public bool ContainsKey(TkeyA keyA)
			{
				return a2b.ContainsKey(keyA);
			}

			public void Delete(TkeyA keyA, TkeyB keyB)
			{
				if (a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB))
					a2b[keyA].Remove(keyB);
				if (b2a.ContainsKey(keyB) && b2a[keyB].ContainsKey(keyA))
					b2a[keyB].Remove(keyA);
			}

			public void DeleteA(TkeyA keyA)
			{
				foreach (TkeyB keyB in b2a.Keys)
					if (b2a[keyB].ContainsKey(keyA))
						b2a[keyB].Remove(keyA);
				if (a2b.ContainsKey(keyA))
					a2b.Remove(keyA);
			}

			public void DeleteB(TkeyB keyB)
			{
				foreach (TkeyA keyA in a2b.Keys)
					if (a2b[keyA].ContainsKey(keyB))
						a2b[keyA].Remove(keyB);
				if (b2a.ContainsKey(keyB))
					b2a.Remove(keyB);
			}

			public int Count(TkeyA keyA)
			{
				return a2b[keyA].Count;
			}
		}
	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
		protected override void LoadDefaultMessages()
		{
            Language = new Dictionary<string, string>();
            CreateApps();
            Language = Language.Concat(new Dictionary<string, string>
            {
                ["app.error.launch"] = "That app is not registered or this player does not have permission to use it.",
                ["app.access.denied"] = "Access Denied",
                ["app.access.reason"] = "You do not have any app permissions for this plugin."
            }).ToDictionary(x => x.Key, y => y.Value);
            lang.RegisterMessages(Language, this);
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private string Lang(string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, this, basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static readonly string SFX_LAUNCH = "assets/prefabs/tools/binoculars/sound/fovchange.prefab";
        public static readonly string SFX_CLICK = "assets/prefabs/tools/detonator/effects/unpress.prefab";
        public static readonly string SFX_SUBMIT = "assets/prefabs/tools/flashlight/effects/turn_on.prefab";
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus
    {

        private T LoadDataFile<T>(string fileName)
        {
            try
            {
                return Interface.Oxide.DataFileSystem.ReadObject<T>($"{Name}/{fileName}");
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        private void SaveDataFile<T>(string fileName, T data)
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{fileName}", data);
        }

        private void PlaySfx(BasePlayer player, string sound) => EffectNetwork.Send(new Effect(sound, player, 0, Vector3.zero, Vector3.forward), player.net.connection);
    }
}

namespace Oxide.Plugins
{
	partial class ComputersPlus : CovalencePlugin
	{
		private readonly ComputersPlusApp EMAIL_APP = new ComputersPlusApp("email", "Email", "https://freeiconshop.com/wp-content/uploads/edd/mail-var-outline-filled.png", "computersplus.email");

		#region App Hooks
		void CreateEmailApp(string userIdString)
		{
			RegisterEmailLanguage();
		}

		void InitEmailApp()
		{
			EmailManager.MaxInboxSize = config.Email.MaxInboxSize;
			
			LoadEmails();
			AddEmailAppImages();
			RegisterEmailFunctions();
		}

		void LaunchEmailApp(string userIdString)
		{
			BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(userIdString));
			RenderEmailApp(basePlayer);
		}

		void SaveEmailApp()
		{
			SaveEmails();
		}

		void UnloadEmailApp()
		{
			SaveEmails();
		}
        #endregion

        #region Config
        private partial class Configuration
		{
			[JsonProperty(PropertyName = "Email Config")]
			public EmailConfiguration Email = new EmailConfiguration();
		}

		private class EmailConfiguration
		{
			[JsonProperty(PropertyName = "Max Inbox Size")]
			public int MaxInboxSize { get; set; } = 24;
		}
        #endregion

        #region Helper Methods
        private void AddEmailAppImages()
		{
			ImageLibrary.Call<bool>("AddImage", "https://freeiconshop.com/wp-content/uploads/edd/mail-var-outline-filled.png", $"{EMAIL_APP.Id}Unread", 0UL);
			ImageLibrary.Call<bool>("AddImage", "https://imgur.com/GYiA73s.png", $"{EMAIL_APP.Id}Read", 0UL);
			ImageLibrary.Call<bool>("AddImage", "https://imgur.com/dUlC5Yf.png", $"{EMAIL_APP.Id}Compose", 0UL);
			ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/HMcxb5Q.png", $"{EMAIL_APP.Id}Delete", 0UL);
			ImageLibrary.Call<bool>("AddImage", "https://uxwing.com/wp-content/themes/uxwing/download/communication-chat-call/send-icon.png", $"{EMAIL_APP.Id}Send", 0UL);
		}

		private void RegisterEmailLanguage()
		{
			Language = Language.Concat(new Dictionary<string, string>
			{
				["email.compose"] = "Compose",
				["email.subject"] = "Subject",
				["email.recipients"] = "Recipients",
				["email.send"] = "Send",
				["email.message"] = "Message",
				["email.to"] = "To",
				["email.from"] = "From",
				["email.received"] = "Received",
				["email.unread"] = "Unread",
				["email.inbox"] = "Inbox",
				["email.days"] = "days ago",
				["email.hours"] = "hrs ago",
				["email.minutes"] = "min ago",
				["email.now"] = "just now",
				["email.cleared"] = "Cleared inbox for {0}",
				["email.success"] = "Successfully sent to {0} recipients",
				["email.partial"] = "Successfully sent to {0} recipients. Failed to find {1} recipients.",
				["email.fail"] = "Failed to find any recipients. No messages sent.",
				["email.usage.clear"] = "Usage: email.clear <username>"
			}).ToDictionary(x => x.Key, x => x.Value);
        }

        private void RegisterEmailFunctions()
		{
			EZUI.RegisterFunction(nameof(RenderEmailView));
			EZUI.RegisterFunction(nameof(RenderEmailCompose));
			EZUI.RegisterFunction(nameof(SendEmail));
			EZUI.RegisterFunction(nameof(DeleteEmail));
		}

		private void LoadEmails()
		{
			var existing = LoadDataFile<Dictionary<string, List<EmailMessage>>>("Emails");
			if (existing != null)
			{
				EmailManager.SetAllEmails(existing);
			}
		}

		private void SaveEmails()
		{
			var all = EmailManager.GetAllEmails();
			SaveDataFile("Emails", all);
		}

		private void DeleteEmail(string userIdString, string emailGuid)
        {
			BasePlayer basePlayer = BasePlayer.FindAwakeOrSleeping(userIdString);
			if (basePlayer != null)
            {
				EmailManager.DeleteEmail(userIdString, Guid.Parse(emailGuid));
				var inbox = UpdateInbox(basePlayer);
				inbox.Paginator?.UpdatePageAndRender(basePlayer);
			}
		}

		private void SendEmail(string userIdString)
		{
			BasePlayer basePlayer = BasePlayer.FindAwakeOrSleeping(userIdString);
			var recipients = EZUI.Get(basePlayer, EMAIL_APP, "Recipients")?.As<EZUI.InputComponent>()?.Data;
			var subject = EZUI.Get(basePlayer, EMAIL_APP, "Subject")?.As<EZUI.InputComponent>()?.Data;
			var text = EZUI.Get(basePlayer, EMAIL_APP, "Message")?.As<EZUI.InputComponent>()?.Data;
			var split = recipients.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
			var recipientIds = split.Select(y => players.FindPlayer(y)?.Id).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();
			if (recipients != null && subject != null && text != null)
			{
				int count = 0;
				if (split.Count > 0)
                {
					foreach (var id in recipientIds)
					{
						var email = new EmailMessage(userIdString, id, subject, text);
						EmailManager.SendEmail(email);
						count++;
					}
				}
				else
                {
					UpdateInfoText(basePlayer, Lang("email.fail", basePlayer));
				}
				string status;
				if (count == split.Count)
				{
					status = Lang("email.success", basePlayer, count);
				}
				else if (count > 0)
                {
					int fails = split.Count - count;
					status = Lang("email.partial", basePlayer, count, fails);
				}
				else
                {
					status = Lang("email.fail", basePlayer, count);
				}
				var window = EZUI.Get(basePlayer, EMAIL_APP, "Compose");
				if (window != null)
				{
					window.Destroy(basePlayer);
					timer.In(0.5f, () =>
					{
						UpdateInfoText(basePlayer, status);
						timer.In(4f, () =>
						{
							UpdateInfoText(basePlayer, string.Empty);
						});
					});
				}
				UpdateInbox(basePlayer).Render(basePlayer);
			}
		}
        #endregion

        #region UI
        private void RenderEmailCompose(string userIdString)
		{
			var basePlayer = BasePlayer.FindByID(ulong.Parse(userIdString));
			if (basePlayer != null)
			{
				var height = 45;
				var width = 0.5f;
				var window = new EZUI.WindowComponent("Compose")
				{
					HeaderText = Lang("email.compose", basePlayer),
					Scale = 0.5f
				}.Tag(EMAIL_APP).Create();
				var subject = new EZUI.InputComponent("Subject")
				{
					Text = $"{Lang("email.subject", basePlayer)}:",
					CharsLimit = 55,
					AnchorTo = EZUI.Anchor.TopLeft,
					PixelMargin = new EZUI.Dir<int>(0, 0, 10, 0),
					PixelHeight = height,
					Width = width
				}.Tag(EMAIL_APP).Create(window);
				var recipients = new EZUI.InputComponent("Recipients")
				{
					Text = $"{Lang("email.recipients", basePlayer)}:",
					CharsLimit = 1024,
					TopAlign = subject.BottomSide.Pixels,
					PixelMargin = new EZUI.Dir<int>(0, 0, 10, 0),
					PixelHeight = height,
					Width = width
				}.Tag(EMAIL_APP).Create(window);
				var send = new EZUI.ButtonComponent("Send")
				{
                    Text = Lang("email.send", basePlayer),
                    AnchorTo = EZUI.Anchor.BottomRight,
					TextAlign = TextAnchor.MiddleCenter,
					BackgroundColor = EZUI.StyleSheet.DEFAULT.BackgroundColorDark1,
					OutlineColor = EZUI.StyleSheet.DEFAULT.BackgroundColorLight1,
					OutlinePixelWeight = 1,
					PixelWidth = 75,
					PixelHeight = 25,
					ClickSfx = SFX_SUBMIT,
					Command = $"ezui.call {EZUI.Token} SendEmail {basePlayer.UserIDString}",
					PixelMargin = new EZUI.Dir<int>(5, 0, 0, 0)
				}.Create(window);
				var message = new EZUI.InputComponent("Message")
				{
					Text = $"{Lang("email.message", basePlayer)}:",
					CharsLimit = 2048,
					TextAlign = TextAnchor.UpperLeft,
					TopAlign = recipients.BottomSide.Pixels,
					BottomAlign = send.TopSide.Pixels
				}.Tag(EMAIL_APP).Create(window);

				window.Render(basePlayer);
			}
		}
		private void RenderEmailView(string userIdString, string emailGuid)
        {
			var basePlayer = BasePlayer.FindByID(ulong.Parse(userIdString));
			if (basePlayer != null)
            {
                var email = EmailManager.GetEmail(userIdString, Guid.Parse(emailGuid));
				email.MarkAsRead();
				UpdateInbox(basePlayer).Render(basePlayer);
				var window = new EZUI.WindowComponent("View")
                {
                    HeaderText = email.Subject,
                    Scale = 0.5f,
                }.Create();
				var height = 20;
				var width = 100;
				var to = new EZUI.LayoutComponent("To")
				{
					AnchorTo = EZUI.Anchor.TopLeft,
					PixelHeight = height,
					PixelWidth = width,
					ColCount = 2,
					Entries = new List<EZUI.Component>()
                    {
						new EZUI.TextComponent("Label") { Text = $"{Lang("email.to", basePlayer)}:" },
						new EZUI.TextComponent("Value") { Text = $"{email.RecipientDisplayName}" },
					}
				}.Create(window);
                var from = new EZUI.LayoutComponent("From")
                {
                    TopAlign = to.BottomSide.Pixels,
                    PixelHeight = height,
                    PixelWidth = width,
                    ColCount = 2,
                    Entries = new List<EZUI.Component>()
                    {
                        new EZUI.TextComponent("Label") { Text = $"{Lang("email.from", basePlayer)}:" },
                        new EZUI.TextComponent("Value") { Text = $"{email.ComposerDisplayName}" },
                    }
                }.Create(window);
                var received = new EZUI.LayoutComponent("Received")
                {
                    TopAlign = from.BottomSide.Pixels,
                    PixelHeight = height,
                    PixelWidth = width,
                    ColCount = 2,
                    Entries = new List<EZUI.Component>()
                    {
                        new EZUI.TextComponent("Label") { Text = $"{Lang("email.received", basePlayer)}:" },
                        new EZUI.TextComponent("Value") { Text = $"{email.ElapsedTimeString(basePlayer)}" },
                    }
                }.Create(window);
                var subject = new EZUI.LayoutComponent("Subject")
                {
                    TopAlign = received.BottomSide.Pixels,
                    PixelHeight = height,
                    PixelWidth = width,
                    ColCount = 2,
                    PixelMargin = new EZUI.Dir<int>(0, 0, 5, 0),
                    Entries = new List<EZUI.Component>()
                    {
                        new EZUI.TextComponent("Label") { Text = $"{Lang("email.subject", basePlayer)}:" },
                        new EZUI.TextComponent("Value") { Text = $"{email.Subject}" },
                    }
                }.Create(window);
                var text = new EZUI.TextComponent("Message")
                {
                    TopAlign = subject.BottomSide.Pixels,
                    TextAlign = TextAnchor.UpperLeft,
                    PixelPadding = new EZUI.Dir<int>(5),
                    Text = email.Text,
                    BackgroundColor = EZUI.Color.WHITE
                }.Create(window);
                window.Render(basePlayer);
            }
		}
        private EZUI.Component CreateEmailEntryComponent(BasePlayer basePlayer, EmailMessage email, EZUI.Component list)
		{
			var box = new EZUI.BoxComponent("Item")
			{
				BackgroundColor = email.Unread ? EZUI.StyleSheet.DEFAULT.BackgroundColorHighlight1 : EZUI.StyleSheet.DEFAULT.BackgroundColorLight2,
				PixelPadding = new EZUI.Dir<int>(5),
			}.Create(list);
			var img = new EZUI.ImageComponent("Icon")
			{
				Transparent = true,
				Height = 0.8f,
				PixelWidth = 18,
				//PixelHeight = 36,
				ImageKey = $"{EMAIL_APP.Id}Unread",
				ImageHidden = !email.Unread,
				CenterY = true,
				AnchorTo = EZUI.Anchor.Left
			}.Create(box);
			var from = new EZUI.TextComponent("From")
			{
				Text = email.ComposerDisplayName,
				PixelWidth = 100,
				Right = 100
			}.Create(box);
			var subject = new EZUI.TextComponent("Subject")
			{
				Text = email.Subject,
				PixelWidth = 250,
				Right = 250
			}.Create(box);
			var received = new EZUI.TextComponent("Received")
			{
				Text = email.ElapsedTimeString(basePlayer),
				PixelWidth = 100,
				Right = 550
			}.Create(box);
            var btn = new EZUI.ButtonComponent("Clickable")
            {
                Transparent = true,
                Command = $"ezui.call {EZUI.Token} RenderEmailView {basePlayer.UserIDString} {email.Guid}",
				Width = 0.97f
            }.Create(box);
            var delete = new EZUI.ButtonComponent("Delete")
			{
				Transparent = true,
				ImageKey = $"{EMAIL_APP.Id}Delete",
				Command = $"ezui.call {EZUI.Token} DeleteEmail {basePlayer.UserIDString} {email.Guid}",
				Size = 0.5f,

				CenterY = true,
				AnchorTo = EZUI.Anchor.MiddleRight,
				Left = 5
			}.Create(box);
			return box;
		}

		EZUI.LayoutComponent UpdateInbox(BasePlayer basePlayer, EZUI.Component inbox = null)
        {
			if (inbox == null)
            {
				inbox = EZUI.Get(basePlayer, EMAIL_APP, "Inbox");
            }
			var layout = inbox.As<EZUI.LayoutComponent>();
			layout.ClearEntries();
			var emails = EmailManager.GetPlayerInbox(basePlayer.UserIDString);
			foreach (var email in emails)
			{
				var entry = CreateEmailEntryComponent(basePlayer, email, layout);
			}
			UpdateStatusText(basePlayer);
			return layout;
		}

		private string StatusText(BasePlayer basePlayer)
        {
			return $"{Lang("email.inbox", basePlayer)} ({EmailManager.GetPlayerInboxCount(basePlayer.UserIDString)}/{EmailManager.MaxInboxSize})";
		}

		void UpdateStatusText(BasePlayer basePlayer)
        {
			var status = EZUI.Get(basePlayer, EMAIL_APP, "Status")?.As<EZUI.TextComponent>();
			if (status != null)
            {
				status.Text = StatusText(basePlayer);
				status.Create().Render(basePlayer);
			}
		}

		void UpdateInfoText(BasePlayer basePlayer, string newText)
		{
			var info = EZUI.Get(basePlayer, EMAIL_APP, "Info")?.As<EZUI.TextComponent>();
			if (info != null)
			{
				info.Text = newText;
				info.Create().Render(basePlayer);
			}
		}

		void RenderEmailApp(BasePlayer basePlayer)
		{
			var main = new EZUI.BoxComponent("Container")
			{
				Transparent = true
			}.Tag(EMAIL_APP.Id).Create(AppComponent);
			var button = new EZUI.ButtonComponent("Compose")
			{
				Text = $"{Lang("email.compose", basePlayer)}",
				PixelHeight = 15,
				PixelWidth = 70,
				TextAlign = TextAnchor.MiddleCenter,
				AnchorTo = EZUI.Anchor.TopLeft,
				BackgroundColor = EZUI.StyleSheet.DEFAULT.BackgroundColorDark1,
				Command = $"ezui.call {EZUI.Token} RenderEmailCompose {basePlayer.UserIDString}",
				PixelMargin = new EZUI.Dir<int>(0, 0, 5, 0),
				OutlinePixelWeight = 1
			}.Create(main);
			var header = new EZUI.BoxComponent("Header")
			{
				PixelHeight = 30,
				TopAlign = button.BottomSide.Pixels,
				PixelPadding = new EZUI.Dir<int>(5),
				PixelMargin = new EZUI.Dir<int>(0, 0, 5, 0)
			}.Create(main);
            var h1 = new EZUI.TextComponent("H1")
            {
                Text = $"{Lang("email.unread", basePlayer)}",
				AutoSizeWidth = true,
				Right = 0
            }.Create(header);
            var h2 = new EZUI.TextComponent("H2")
            {
                Text = $"{Lang("email.from", basePlayer)}",
				AutoSizeWidth = true,
				Right = 100
			}.Create(header);
            var h3 = new EZUI.TextComponent("H3")
            {
                Text = $"{Lang("email.subject", basePlayer)}",
				AutoSizeWidth = true,
				Right = 250
			}.Create(header);
            var h4 = new EZUI.TextComponent("H4")
            {
                Text = $"{Lang("email.received", basePlayer)}",
				AutoSizeWidth = true,
				Right = 550
            }.Create(header);
			var pag = new EZUI.PaginatorComponent("Paginator")
			{
				AnchorTo = EZUI.Anchor.BottomLeft,
				PixelHeight = 25,
				OutlinePixelWeight = 1,
				PixelMargin = new EZUI.Dir<int>(5, 0, 0, 0)
			}.Create(main).As<EZUI.PaginatorComponent>();
			var body = new EZUI.BoxComponent("Body")
			{
				TopAlign = header.BottomSide.Pixels,
				BottomAlign = pag.TopSide.Pixels,
				OutlinePixelWeight = 1,
				BackgroundColor = EZUI.Color.WHITE
			}.Create(main);
			var status = new EZUI.TextComponent("Status")
			{
				AnchorTo = EZUI.Anchor.BottomLeft,
				PixelPadding = new EZUI.Dir<int>(5),
				FontSize = 10,
				Text = StatusText(basePlayer),
				Width = 0.5f,
				PixelHeight = 25
			}.Tag(EMAIL_APP).Create(body);
			var info = new EZUI.TextComponent("Info")
			{
				AnchorTo = EZUI.Anchor.BottomRight,
				PixelPadding = new EZUI.Dir<int>(5),
				FontSize = 10,
				Text = string.Empty,
				TextAlign = TextAnchor.MiddleRight,
				PixelHeight = 25,
				Width = 0.5f,
			}.Tag(EMAIL_APP).Create(body);
			var inbox = new EZUI.LayoutComponent("Inbox")
			{
				RowCount = 8,
				ColCount = 1,
				EntryPixelPadding = new EZUI.Dir<int>(5),
				BottomAlign = status.TopSide.Pixels
			}.Tag(EMAIL_APP.Id).As<EZUI.LayoutComponent>().SetPaginator(pag).Create(body).As<EZUI.LayoutComponent>();
			inbox = UpdateInbox(basePlayer, inbox);
			main.Render(basePlayer);
			pag.UpdatePageAndRender(basePlayer, 1);
			
		}
		#endregion

		#region Commands
		[Command("email.clear"), Permission(PermissionAdmin)]  // email.clear <username> 
		private void cmd_email_clear(IPlayer player, string command, string[] args)
		{
			if (args.Length == 1)
            {
				var username = args[0];
				var target = players.FindPlayer(username);
				if (target != null)
                {
					EmailManager.ClearPlayerInbox(target.Id);
					player.Reply(Lang("email.cleared", player.Id, target.Name));
					return;
				}
            }
			player.Reply(Lang("email.usage.clear", player.Id));
		}
		#endregion
	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus
    {
        private static class EmailManager
        {
            private static Dictionary<string, List<EmailMessage>> EmailMessages = new Dictionary<string, List<EmailMessage>>();
            public static int MaxInboxSize { get; set; } = 24;
            public static int SendEmail(EmailMessage email)
            {
                if (EmailMessages.ContainsKey(email.RecipientStringId))
                {
                    if (EmailMessages[email.RecipientStringId].Count < MaxInboxSize)
                    {
                        EmailMessages[email.RecipientStringId].Add(email);
                        return 1;
                    }
                }
                else
                {
                    EmailMessages.Add(email.RecipientStringId, new List<EmailMessage>() { email });
                    return 1;
                }
                return 0;
            }
            public static void SetAllEmails(Dictionary<string, List<EmailMessage>> emailMessages)
            {
                EmailMessages = emailMessages;
            }
            public static Dictionary<string, List<EmailMessage>> GetAllEmails()
            {
                return EmailMessages;
            }
            public static EmailMessage[] GetPlayerInbox(string userIdString)
            {
                if (EmailMessages.ContainsKey(userIdString))
                {
                    return EmailMessages[userIdString].Reverse<EmailMessage>().ToArray();
                }
                else
                {
                    return new EmailMessage[] { };
                }
            }
            public static int GetPlayerInboxCount(string userIdString)
            {
                if (EmailMessages.ContainsKey(userIdString))
                {
                    return EmailMessages[userIdString].Count;
                }
                else
                {
                    return 0;
                }
            }
            public static void ClearPlayerInbox(string userIdString)
            {
                if (EmailMessages.ContainsKey(userIdString))
                {
                    EmailMessages.Remove(userIdString);
                }
            }
            public static void DeleteEmail(string userIdString, Guid emailGuid)
            {
                if (EmailMessages.ContainsKey(userIdString))
                {
                    EmailMessages[userIdString].RemoveAll(x => x.Guid == emailGuid);
                }
            }
            public static EmailMessage GetEmail(string userIdString, Guid emailGuid)
            {
                if (EmailMessages.ContainsKey(userIdString))
                {
                    return EmailMessages[userIdString].Where(x => x.Guid == emailGuid).FirstOrDefault();
                }
                else
                {
                    return null;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
	partial class ComputersPlus : CovalencePlugin
	{
		[Serializable]
		private class EmailMessage
		{
			public EmailMessage(string composerStringId, string recipientStringId, string subject, string text)
            {
				Guid = Guid.NewGuid();
				Unread = true;
				Subject = subject;
				ComposerStringId = composerStringId;
				RecipientStringId = recipientStringId;
				Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
				Text = text;
            }
			public Guid Guid { get; set; }
			public bool Unread { get; set; }
			public string Subject { get; set; }
			public string ComposerStringId { get; set; }
			public string RecipientStringId { get; set; }
			public long Timestamp { get; set; }
			public string Text { get; set; }

			[JsonIgnore]
			public string ComposerDisplayName
            {
				
				get
                {
					return BasePlayer.FindAwakeOrSleeping(ComposerStringId)?.displayName;
				}
            }
			[JsonIgnore]
			public string RecipientDisplayName
			{
				get
				{
					return BasePlayer.FindAwakeOrSleeping(RecipientStringId)?.displayName;
				}
			}
			public void MarkAsRead()
			{
				this.Unread = false;
			}
			public string ElapsedTimeString(BasePlayer basePlayer)
			{
				var elapsed = TimeSpan.FromSeconds(DateTimeOffset.Now.ToUnixTimeSeconds() - Timestamp);
				if (elapsed.TotalDays >= 1)
				{
					return $"{(int)Math.Floor(elapsed.TotalDays)} {PLUGIN.Lang("email.days", basePlayer)}";
				}
				else if (elapsed.TotalHours >= 1)
				{
					return $"{(int)Math.Floor(elapsed.TotalHours)} {PLUGIN.Lang("email.hours", basePlayer)}";
				}
				else if (elapsed.TotalMinutes >= 1)
				{
					return $"{(int)Math.Floor(elapsed.TotalMinutes)} {PLUGIN.Lang("email.minutes", basePlayer)}";
				}
				else
				{
					return $"{PLUGIN.Lang("email.now", basePlayer)}";
				}
			}
		}
	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            public class BasicComponent : Component
            {
                public BasicComponent(string id) : base(id)
                {
                }

                protected override Component CreateHelper()
                {
                    return this;
                }

                public override Component Inherit(Component from, bool recursive = false)
                {
                    this.BackgroundColor = from.BackgroundColor;
                    this.PixelHeight = from.PixelHeight;
                    this.PixelWidth = from.PixelWidth;
                    this.PixelX = from.PixelX;
                    this.PixelY = from.PixelY;
                    this.PixelPadding = from.PixelPadding;
                    this.PixelMargin = from.PixelMargin;

                    return recursive ? base.Inherit(from) : this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            private interface IBoxComponent
            {
                Color OutlineColor { get; set; }
                int OutlinePixelWeight { get; set; }
            }

            public class BoxComponent : Component, IBoxComponent
            {
                public BoxComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorLight1;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.OutlineColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;

                protected override Component CreateHelper()
                {
                    if (OutlinePixelWeight > 0)
                    {
                        var outline = new BasicComponent("Outline")
                        {
                            BackgroundColor = BackgroundColor == Color.TRANSPARENT ? Color.TRANSPARENT : OutlineColor,
                            PixelPadding = new Dir<int>(OutlinePixelWeight)
                        }.Create(this);
                        var content = new BasicComponent("Content")
                        {
                            BackgroundColor = BackgroundColor
                        }.Create(outline);
                        this.Content = content;
                    }
                    else
                    {
                        this.Content = this;
                    }
                    return this;
                }

                public override Component Inherit(Component from, bool recursive=false)
                {
                    var casted = (IBoxComponent)from;
                    this.OutlineColor = this.OutlineColor ?? casted.OutlineColor;
                    this.OutlinePixelWeight = casted.OutlinePixelWeight;
                    return recursive ? base.Inherit(from) : this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        [Command("button.click")] // button.click <token> <sfx> <command..>
        private void cmd_button_click(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])) { return; }
            var basePlayer= player.Object as BasePlayer;
            if (basePlayer != null && args.Length >= 2)
            {
                var sfx = args[1];
                PlaySfx(basePlayer, sfx);
                if (args.Length > 2)
                {
                    var cmd = args[2];
                    args = args.Skip(3).ToArray();
                    player.Command(cmd, args);
                }
            }
        }

        public static partial class EZUI
        {
            private interface IButtonComponent
            {
                string Command { get; set; }
            }

            public class ButtonComponent : Component, IButtonComponent, IBoxComponent, ITextComponent, IImageComponent
            {
                public ButtonComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorLight3;
                public string Command { get; set; } = "";
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.OutlineColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;
                public string Text { get; set; } = "";
                public string FontType { get; set; } = DEFAULT_FONT_TYPE;
                public int FontSize { get; set; } = DEFAULT_FONT_SIZE;
                public Color FontColor { get; set; } = StyleSheet.DEFAULT.FontColorLight1;
                public TextAnchor TextAlign { get; set; } = TextAnchor.MiddleLeft;
                public string ImageKey { get; set; }
                public string ClickSfx { get; set; } = SFX_CLICK;
                public bool ImageHidden { get; set; } = false;
                public bool AutoSizeHeight { get; set; } = false;
                public bool AutoSizeWidth { get; set; } = false;
                public Color ImageColor { get; set; } = null;

                public override Component Init(Component parent)
                {
                    if (AutoSizeHeight)
                    {
                        this.PixelHeight = FontSize + 1;
                    }
                    if (AutoSizeWidth)
                    {
                        this.PixelWidth = (int)Math.Ceiling((FontSize + 1) * 0.50f) * Text?.Length ?? 0;
                    }
                    return base.Init(parent);
                }

                protected override Component CreateHelper()
                {
                    var box = new BoxComponent("Box")
                    {
                        BackgroundColor = BackgroundColor
                    }
                    .Inherit(this)
                    .Create(this);
                    var parent = box;
                    if (ImageKey != null)
                    {
                        var image = new ImageComponent("Image"){}
                        .Inherit(this)
                        .Create(parent);
                        parent = image;
                    }
                    if (Text != null)
                    {
                        var text = new TextComponent("Text") { }
                        .Inherit(this)
                        .Create(parent);
                        parent = text;
                    }
                    
                    var button = new BasicComponent("Button") { Empty = true }.Create(parent);
                    button.Elements.Add(new CuiElement
                    {
                        Name = button.GlobalId,
                        Parent = button.ParentGlobalId,
                        Components = {
                            new CuiButtonComponent
                            {
                                Command = $"button.click {Token} {ClickSfx} {Command}",
                                Color = Color.TRANSPARENT.ToString()
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                    return this;
                }

                public override Component Inherit(Component from, bool recursive = false)
                {
                    var casted = (IButtonComponent)from;
                    this.Command = casted.Command;
                    return recursive ? base.Inherit(from) : this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        #region Commands
        [Command("component.render")] // component.render <token> <globalId>
        private void cmd_component_render(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])){return;}
            var basePlayer= player.Object as BasePlayer;
            if (basePlayer != null && args.Length > 0)
            {
                string globalId = args[0];
                var component = EZUI.Find(basePlayer.UserIDString, globalId);
                if (component != null)
                {
                    component.Render(basePlayer);
                }
            }
        }
        [Command("component.destroy")] // component.destroy <token> <globalId>
        private void cmd_component_destroy(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])) { return; }
            var basePlayer= player.Object as BasePlayer;
            if (basePlayer != null && args.Length > 1)
            {
                string globalId = args[1];
                var component = EZUI.Find(basePlayer.UserIDString, globalId);
                if (component != null)
                {
                    component.Destroy(basePlayer);
                }
            }
        }
        #endregion
        public partial class EZUI
        {
            public abstract class Component
            {
                #region Constructors
                public Component(string id)
                {
                    this.LocalId = id;
                    this.Children = new HashSet<Component>();
                }
                #endregion

                #region Private Variables
                private Component _parent = null;
                private int? _pixelX = null;
                private int? _pixelY = null;
                private int? _pixelWidth = null;
                private int? _pixelHeight = null;
                private float _x = 0f;
                private float _y = 0f;
                private float _width = 1f;
                private float _height = 1f;
                private float? _size;

                private int _newPixelX;
                private int _newPixelY;
                private int _newPixelWidth;
                private int _newPixelHeight;
                private float _newX;
                private float _newY;
                private float _newWidth;
                private float _newHeight;
                #endregion

                #region Identity Properties
                public HashSet<string> Tags { get; } = new HashSet<string>();
                public string LocalId { get; set; }
                public string GlobalId { get { return Parent == null ? LocalId : $"{Parent.GlobalId}.{LocalId}"; } }
                public Component Parent { get { return _parent; } }
                public Component Content { get; set; }
                public HashSet<Component> Children { get; set; }
                #endregion

                #region Parent Properties
                public string ParentGlobalId
                {
                    get
                    {
                        return Parent == null ? "Overlay" : Parent.GlobalId;
                    }
                }
                public int ParentPixelY
                {
                    get { return Parent == null ? 0 : Parent.PixelY; }
                }
                public int ParentPixelX
                {
                    get { return Parent == null ? 0 : Parent.PixelX; }
                }
                public Dir<int> ParentPixelPadding
                {
                    get
                    {
                        return Parent == null ? new Dir<int>(0) : Parent.PixelPadding;
                    }
                }
                public int ParentPixelWidth
                {
                    get { return Parent == null ? MAX_X : Parent.PixelWidth; }
                }
                public int ParentPixelHeight
                {
                    get { return Parent == null ? MAX_Y : Parent.PixelHeight; }
                }
                #endregion

                #region Anchor and Offset Properties
                public string AnchorMin
                {
                    get
                    {
                        return $"0 0";
                    }
                }
                public string AnchorMax
                {
                    get
                    {
                        return $"0 0";
                    }
                }
                public string OffsetMin
                {
                    get { return $"{PixelX - ParentPixelX} {PixelY - ParentPixelY}"; }
                }
                public string OffsetMax
                {
                    get { return $"{PixelX - ParentPixelX + PixelWidth} {PixelY - ParentPixelY + PixelHeight}"; }
                }
                #endregion

                #region Pixel Properties
                public int PixelX { get { return _newPixelX; } set { _pixelX = value; } }
                public int PixelY { get { return _newPixelY; } set { _pixelY = value; } }
                public int PixelHeight { get { return _newPixelHeight; } set { _pixelHeight = value; } }
                public int PixelWidth { get { return _newPixelWidth; } set { _pixelWidth = value; } }
                public int PixelSize { get { return _newPixelHeight; } set { _pixelWidth = value; _pixelHeight = value; } }
                public virtual Dir<int> PixelPadding { get; set; } = new Dir<int>(0);
                public Dir<int> PixelMargin { get; set; } = new Dir<int>(0);
                public Dir<int> PixelCrop { get; set; } = new Dir<int>(0);
                #endregion

                #region Relative Properties
                public float X { get { return _newX; } set { _x = value; } }
                public float Y { get { return _newY; } set { _y = value; } }
                public float Height { get { return _newHeight; } set { _height = value; } }
                public float Width { get { return _newWidth; } set { _width = value; } }
                public float Size { get { return _newHeight; } set { _size = value; _height = value; } }
                public float Scale { protected get { return _height; } set { _height = value; _width = value; } }
                public Dir<float> Padding { get; set; }
                public Dir<float> Margin { get; set; }
                #endregion

                #region Element Properties
                public bool Empty { get; set; } = false;
                public List<CuiElement> Elements { get; set; } = new List<CuiElement>();
                public bool Transparent { get { return BackgroundColor.Alpha == 0; } set { BackgroundColor = Color.TRANSPARENT; } }
                public virtual Color BackgroundColor { get; set; } = Color.TRANSPARENT;
                public float Opacity { get { return BackgroundColor.Alpha; } set { BackgroundColor.Alpha = value; } }
                public bool Created { get; protected set; } = false;
                public bool Rendered { get; protected set; } = false;
                #endregion

                #region Positional Properties
                public virtual bool CenterX { get; set; } = false;
                public bool CenterY { get; set; } = false;
                public bool Centered { get { return CenterX && CenterY; } set { CenterX = true; CenterY = true; } }
                public Anchor? AnchorTo { private get; set; }
                public int? TopAlign { private get; set; }
                public int? BottomAlign { private get; set; }
                public int? RightAlign { private get; set; }
                public int? LeftAlign { private get; set; }
                public int Left { private get; set; } = 0;
                public int Right { private get; set; } = 0;
                public int Up { private get; set; } = 0;
                public int Down { private get; set; } = 0;
                #endregion

                #region Value Properties
                public Value TopSide
                {
                    get
                    {
                        return new Value(PixelY + PixelHeight + PixelMargin.Top, MAX_Y);
                    }
                }
                public Value BottomSide
                {
                    get
                    {
                        return new Value(PixelY - PixelMargin.Bottom, MAX_Y);
                    }
                }
                public Value LeftSide
                {
                    get
                    {
                        return new Value(PixelX - PixelMargin.Left, MAX_X);
                    }
                }
                public Value RightSide
                {
                    get
                    {
                        return new Value(PixelX + PixelWidth + PixelMargin.Right, MAX_X);
                    }
                }
                #endregion

                #region Info Properties
                public Dictionary<string, object> Info
                {
                    get
                    {
                        var info = new Info()
                        {
                            LocalId = LocalId,
                            GlobalId = GlobalId,
                            ParentId = Parent == null ? "Overlay" : Parent.GlobalId,
                            Children = $"{Children.Count}",
                            Position = $"({PixelX},{PixelY}) ({PixelX + PixelWidth},{PixelY + PixelHeight})",
                            Dimensions = $"({PixelWidth}x{PixelHeight})",
                            Proportions = $"({Math.Round(Width, 3)}x{Math.Round(Height, 3)})",
                            Padding = $"{PixelPadding}",
                            Margin = $"{PixelMargin}",
                            Color = $"{(BackgroundColor != null ? BackgroundColor.ToString() : "Null")}"
                        };
                        return JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(info));
                    }
                }

                public override string ToString()
                {
                    StringBuilder sb = new StringBuilder();
                    int col1 = 20;
                    int col2 = 40;
                    int num = col1 + col2 + 9;
                    sb.AppendLine();
                    sb.AppendLine(new string('#', num));
                    sb.AppendLine($"## COMPONENT - {GlobalId.ToUpper().PadRight(col1 + col2 - 9)} ##");
                    sb.AppendLine(new string('#', num));
                    foreach (var item in Info)
                    {
                        sb.AppendLine($"## {item.Key.PadLeft(col1)} | {item.Value.ToString().PadRight(col2)} ##");
                    }
                    sb.AppendLine(new string('#', num));
                    sb.AppendLine();
                    return sb.ToString();
                }
                #endregion

                #region Methods
                public virtual Component Init(Component parent)
                {
                    this.Content = null;
                    this.Children = new HashSet<Component>();
                    this._parent = parent?.Content == null ? parent : parent.Content;

                    // Calculate boundaries
                    var bounds = new BoundingBox(ParentPixelY + ParentPixelPadding.Bottom, ParentPixelX + ParentPixelPadding.Left, ParentPixelY + ParentPixelHeight - ParentPixelPadding.Top, ParentPixelX + ParentPixelWidth - ParentPixelPadding.Right); // includes padding

                    // Init bounds
                    bounds.Bottom = _pixelY ?? ParentPixelY;
                    bounds.Left = _pixelX ?? ParentPixelX;
                    bounds.Height = _pixelHeight ?? ParentPixelHeight; // takes padding into account
                    bounds.Width = _pixelWidth ?? ParentPixelWidth;

                    // Apply relative transformations
                    bounds.Bottom += (int)Math.Round(bounds.Height * _y);
                    bounds.Left += (int)Math.Round(bounds.Width * _x);
                    bounds.Height = (int)Math.Round(bounds.Height * _height);
                    bounds.Width = _size != null ? bounds.Height : (int)Math.Round(bounds.Width * _width);

                    // Apply positional modifications
                    if (AnchorTo != null)
                    {
                        switch (AnchorTo)
                        {
                            case Anchor.Left:
                                LeftAlign = bounds.MinLeft;
                                break;
                            case Anchor.Right:
                                RightAlign = bounds.MaxRight;
                                break;
                            case Anchor.TopCenter:
                                TopAlign = bounds.MaxTop;
                                CenterX = true;
                                break;
                            case Anchor.TopLeft:
                                TopAlign = bounds.MaxTop;
                                LeftAlign = bounds.MinLeft;
                                break;
                            case Anchor.TopRight:
                                TopAlign = bounds.MaxTop;
                                RightAlign = bounds.MaxRight;
                                break;
                            case Anchor.MiddleRight:
                                CenterY = true;
                                RightAlign = bounds.MaxRight;
                                break;
                            case Anchor.MiddleLeft:
                                CenterY = true;
                                LeftAlign = bounds.MinLeft;
                                break;
                            case Anchor.MiddleCenter:
                                Centered = true;
                                break;
                            case Anchor.BottomLeft:
                                BottomAlign = bounds.MinBottom;
                                LeftAlign = bounds.MinLeft;
                                break;
                            case Anchor.BottomRight:
                                BottomAlign = bounds.MinBottom;
                                RightAlign = bounds.MaxRight;
                                break;
                        }
                    }
                    if (CenterX)
                    {
                        var gapX = bounds.GapX / 2;
                        bounds.Left = bounds.MinLeft + gapX;
                        bounds.Right = bounds.MaxRight - gapX;
                    }
                    if (CenterY)
                    {
                        var gapY = bounds.GapY / 2;
                        bounds.Bottom = bounds.MinBottom + gapY;
                        bounds.Top = bounds.MaxTop - gapY;
                    }
                    // Right and Left Align
                    if (RightAlign != null && LeftAlign != null)
                    {
                        bounds.Right = (int)RightAlign;
                        bounds.Left = (int)LeftAlign;
                    }
                    else if (RightAlign != null)
                    {
                        var width = bounds.Width;
                        bounds.Right = (int)RightAlign;
                        bounds.Left = bounds.Right - width;
                    }
                    else if (LeftAlign != null)
                    {
                        var width = bounds.Width;
                        bounds.Left = (int)LeftAlign;
                        bounds.Right = bounds.Left + width;
                    }
                    // Top and Bottom Align
                    if (TopAlign != null && BottomAlign != null)
                    {
                        bounds.Top = (int)TopAlign;
                        bounds.Bottom = (int)BottomAlign;
                    }
                    else if (TopAlign != null)
                    {
                        var height = bounds.Height;
                        bounds.Top = (int)TopAlign;
                        bounds.Bottom = bounds.Top - height;
                    }
                    else if (BottomAlign != null)
                    {
                        var height = bounds.Height;
                        bounds.Bottom = (int)BottomAlign;
                        bounds.Top = bounds.Bottom + height;
                    }

                    // Pixel cropping
                    bounds.Left += PixelCrop.Left;
                    bounds.Right -= PixelCrop.Right;
                    bounds.Bottom += PixelCrop.Bottom;
                    bounds.Top -= PixelCrop.Top;

                    // Pixel nudging
                    bounds.Left -= Left;
                    bounds.Right -= Left;
                    bounds.Left += Right;
                    bounds.Right += Right;
                    bounds.Bottom -= Down;
                    bounds.Top -= Down;
                    bounds.Bottom += Up;
                    bounds.Top += Up;

                    // Set property values
                    this._newPixelX = bounds.Left;
                    this._newPixelY = bounds.Bottom;
                    this._newPixelWidth = bounds.Width;
                    this._newPixelHeight = bounds.Height;
                    this._newX = (float)bounds.Left / (ParentPixelX + ParentPixelWidth);
                    this._newY = (float)bounds.Bottom / (ParentPixelY + ParentPixelHeight);
                    this._newWidth = (float)bounds.Width / ParentPixelWidth;
                    this._newHeight = (float)bounds.Height / ParentPixelHeight;
                    this.Parent?.AddChild(this);
                    return this;
                }

                public Component Tag(string tag)
                {
                    this.Tags.Add(tag);
                    return this;
                }
                public Component Tag(object tag)
                {
                    return Tag(tag.ToString());
                }


                public Component Create()
                {
                    return Create(this.Parent);
                }

                public Component Create(Component parent)
                {
                    this.Created = false;
                    Init(parent);
                    this.Elements = CreateBaseElements();
                    var component = CreateHelper();
                    component.Created = true;
                    return component;
                }

                public Component CreateRecursively()
                {
                    return this.CreateRecursively(this.Parent);
                }

                public Component CreateRecursively(Component parent)
                {
                    var children = this.Children;
                    var comp = this.Create(parent);
                    foreach (var child in children)
                    {
                        child?.CreateRecursively(comp);
                    }
                    return comp;
                }

                protected abstract Component CreateHelper();

                public virtual Component Inherit(Component from, bool recursive=false)
                {
                    this.BackgroundColor = this.BackgroundColor ?? from.BackgroundColor;
                    return this;
                }

                protected virtual List<CuiElement> CreateBaseElements()
                {
                    var elements = new List<CuiElement>();
                    if (!Empty)
                    {
                        elements.Add(new CuiElement
                        {
                            Name = GlobalId,
                            Parent = ParentGlobalId,
                            Components ={
                            new CuiImageComponent {
                                Color = BackgroundColor.ToString()
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = this.AnchorMin,
                                AnchorMax = this.AnchorMax,
                                OffsetMin = this.OffsetMin,
                                OffsetMax = this.OffsetMax
                            }
                        }
                        });
                    }
                    return elements;
                }

                public virtual Component Include(Component[] components)
                {
                    if (Content == null)
                    {
                        this.Children.UnionWith(components);
                    }
                    else
                    {
                        this.Content.Children.UnionWith(components);
                    }
                    return this;
                }

                public virtual void AddChild(Component child)
                {
                    if (Content == null)
                    {
                        this.Children.Add(child);
                    }
                    else
                    {
                        this.Content.Children.Add(child);
                    }
                }

                public void Render(BasePlayer basePlayer)
                {
                    UnloadNested(basePlayer.UserIDString, this);
                    var elements = RenderHelper(basePlayer, new List<CuiElement>());
                    CuiHelper.DestroyUi(basePlayer, GlobalId);
                    CuiHelper.AddUi(basePlayer, elements);
                    Rendered = true;
                }

                private List<CuiElement> RenderHelper(BasePlayer basePlayer, List<CuiElement> elements)
                {
                    var component = OnRender(basePlayer);
                    elements.AddRange(component.Elements);
                    EZUI.Load(basePlayer.UserIDString, component);
                    foreach(var tag in component.Tags)
                    {
                        EZUI.Tag(basePlayer, component, tag);
                    }
                    foreach (var child in component.Children)
                    {
                        elements = child.RenderHelper(basePlayer, elements);
                    }
                    return elements;
                }
                public List<Component> GetNestedChildren()
                {
                    return GetNestedChildrenHelper(new List<Component>());
                }

                private List<Component> GetNestedChildrenHelper(List<Component> children)
                {
                    children.AddRange(this.Children);
                    foreach (var child in this.Children)
                    {
                        children = child.GetNestedChildrenHelper(children);
                    }
                    return children;
                }


                public void Destroy(BasePlayer basePlayer)
                {
                    var component = OnDestroy(basePlayer);
                    CuiHelper.DestroyUi(basePlayer, GlobalId);
                    EZUI.UnloadNested(basePlayer.UserIDString, component);
                }

                public TComponent As<TComponent>() where TComponent : Component
                {
                    return (TComponent)this;
                }

                #endregion

                #region Protected Hooks
                protected virtual Component OnRender(BasePlayer basePlayer)
                {
                    return this;
                }

                protected virtual Component OnDestroy(BasePlayer basePlayer)
                {
                    return this;
                }
                #endregion
                #region HashSet overrides
                public override int GetHashCode()
                {
                    return this.GlobalId.GetHashCode();
                }
                public override bool Equals(object obj) 
                {
                    if ((obj == null) || !this.GetType().Equals(obj.GetType()))
                    {
                        return false;
                    }
                    else
                    {
                        Component c = (Component)obj;
                        return GlobalId == c.GlobalId;
                    }
                }
                #endregion
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        [Command("ezui.call")] // ezui.call <token> <function> <args..>
        private void cmd_ezui_call(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])) { return; }
            if (args.Length >= 3)
            {
                string function = args[1];
                if (EZUI.FunctionIsRegisterd(function))
                {
                    args = args.Skip(2).ToArray();
                    Call(function, args);
                }
            }
        }

        public partial class EZUI
        {

            #region Static
            private readonly static DoubleDictionary<string, string, Component> loaded = new DoubleDictionary<string, string, Component>();
            private readonly static DoubleDictionary<string, string, Component> tagged = new DoubleDictionary<string, string, Component>();
            private readonly static int MAX_X = 1280;
            private readonly static int MAX_Y = 720;
            private readonly static string DEFAULT_FONT_TYPE = "RobotoCondensed-Regular.ttf";
            private readonly static int DEFAULT_FONT_SIZE = 12;
            private static string _token = "";
            private readonly static List<string> RegisteredFunctions = new List<string>();
            internal static string Token
            {
                get
                {
                    return _token;
                }
            }

            public static void RegisterFunction(string function)
            {
                RegisteredFunctions.Add(function);
            }

            public static bool FunctionIsRegisterd(string function)
            {
                return RegisteredFunctions.Contains(function);
            }

            public static void GenerateToken()
            {
                _token = Guid.NewGuid().ToString();
            }

            public static bool ValidateToken(IPlayer player, string command, string token)
            {
                if (token == Token)
                {
                    return true;
                }
                PLUGIN.PrintWarning($"Player '{player.Name}' attempted to use command '{command}' with an invalid token");
                return false;
            }

            public static void Tag(BasePlayer basePlayer, Component component, string tag)
            {
                tagged.Set(basePlayer.UserIDString, $"{tag}.{component.LocalId}", component);
            }

            public static Component Get(BasePlayer basePlayer, object tag, string localId)
            {
                return tagged.Get(basePlayer.UserIDString, $"{tag}.{localId}");
            }

            private static void Load(string userIdString, Component ui)
            {
                loaded.Set(userIdString, ui.GlobalId, ui);
            }

            public static void Unload(string userIdString, Component ui)
            {
                Unload(userIdString, ui.GlobalId);
            }

            public static void Unload(string userIdString, string globalId)
            {
                loaded.Delete(userIdString, globalId);
            }

            public static void UnloadNested(string userIdString, Component ui)
            {
                var filtered = loaded.GetA(userIdString).Where(x => x.Key.StartsWith(ui.GlobalId)).Select(x => x.Key).ToList();
                for (int i = 0; i < filtered.Count; i++)
                {
                    Unload(userIdString, filtered[i]);;
                }
            }

            public static void DestroyAll(BasePlayer basePlayer)
            {
                var all = loaded.GetA(basePlayer.UserIDString).Values.ToList();
                for (int i = 0; i < all.Count; i++)
                {
                    var baseUI = all[i];
                    baseUI?.Destroy(basePlayer);
                }
            }

            public static List<Component> GetAll(BasePlayer basePlayer)
            {
                return loaded.GetA(basePlayer.UserIDString).Values.ToList();
            }

            public static string PrintLoaded(BasePlayer basePlayer)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                var all = loaded.GetA(basePlayer.UserIDString);
                int i = 0;
                foreach (var entry in all)
                {
                    var baseUI = entry.Value;
                    string index = $"[{i}]";
                    sb.AppendLine($"{index.PadLeft(5)} {baseUI.GlobalId}");
                    i++;
                }
                sb.AppendLine();
                return sb.ToString();
            }

            public static Component Find(string userIdString, string globalId)
            {
                return loaded.Get(userIdString, globalId);
            }

            public static Component Find(BasePlayer basePlayer, string globalId)
            {
                return Find(basePlayer.UserIDString, globalId);
            }

            public static Component Query(string userIdString, string localId)
            {
                return loaded.GetA(userIdString).Where(x => x.Key.Contains(localId)).FirstOrDefault().Value;
            }

            public static Component Query(BasePlayer basePlayer, string localId)
            {
                return Query(basePlayer.UserIDString, localId);
            }
            #endregion

            #region Classes
            public enum Anchor
            {
                TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight, Top, Right, Bottom, Left
            }

            public class Info
            {
                public string LocalId { get; set; }
                public string GlobalId { get; set; }
                public string ParentId { get; set; }
                public string Position { get; set; }
                public string Children { get; set; }
                public string Dimensions { get; set; }
                public string Proportions { get; set; }
                public string Padding { get; set; }
                public string Margin { get; set; }
                public string Color { get; set; }
            }

            public class Color
            {
                public static Color WHITE = new Color(1f, 1f, 1f);
                public static Color RED = new Color(1f, 0f, 0f);
                public static Color GREEN = new Color(0f, 1f, 0f);
                public static Color BLUE = new Color(0f, 0f, 1f);
                public static Color BLACK = new Color(0f, 0f, 0f);
                public static Color TRANSPARENT = new Color(0f, 0f, 0f, 0f);

                public Color() { }

                public Color(string colorString, char delim = ' ')
                {
                    try
                    {
                        string[] split = colorString.Split(delim);
                        Red = float.Parse(split[0]);
                        Green = float.Parse(split[1]);
                        Blue = float.Parse(split[2]);
                        Alpha = float.Parse(split[3]);
                    }
                    catch (Exception)
                    {
                        PLUGIN.PrintError($"Invalid color string '{colorString}'");
                    }
                }

                public Color(int r, int g, int b)
                {
                    Red = r / 255f;
                    Green = g / 255f;
                    Blue = b / 255f;
                }

                public Color(float r, float g, float b)
                {
                    Red = r;
                    Green = g;
                    Blue = b;
                }

                public Color(float r, float g, float b, float a)
                {
                    Red = r;
                    Green = g;
                    Blue = b;
                    Alpha = a;
                }

                public float Red
                {
                    get; set;
                } = 1f;

                public float Green
                {
                    get; set;
                } = 1f;

                public float Blue
                {
                    get; set;
                } = 1f;

                public float Alpha
                {
                    get; set;
                } = 1f;

                public override string ToString()
                {
                    return $"{Red} {Green} {Blue} {Alpha}";
                }
            }

            public class Dir<T>
            {
                public readonly static Dir<int> ZERO = new Dir<int>(0, 0, 0, 0);

                public Dir() { }

                public Dir(T all) : this(all, all, all, all) { }

                public Dir(T top, T right, T bottom, T left)
                {
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                    Left = left;
                }

                public Dir<float> ConvertToFloat()
                {
                    return new Dir<float>()
                    {
                        Top = CastToFloat(Top) / MAX_Y,
                        Right = CastToFloat(Right) / MAX_X,
                        Bottom = CastToFloat(Bottom) / MAX_Y,
                        Left = CastToFloat(Left) / MAX_X
                    };
                }

                public Dir<int> ConvertToInt()
                {
                    return new Dir<int>()
                    {
                        Top = (int)Math.Round(CastToFloat(Top) * MAX_Y),
                        Right = (int)Math.Round(CastToFloat(Right) * MAX_X),
                        Bottom = (int)Math.Round(CastToFloat(Bottom) * MAX_Y),
                        Left = (int)Math.Round(CastToFloat(Left) * MAX_X)
                    };
                }

                private float CastToFloat(T value)
                {
                    return float.Parse(((object)value).ToString());
                }

                private int CastToInt(T value)
                {
                    return (int)Math.Round(CastToFloat(value));
                }

                public T Top { get; set; }
                public T Right { get; set; }
                public T Bottom { get; set; }
                public T Left { get; set; }

                public override string ToString()
                {
                    return $"{Top} {Right} {Bottom} {Left}";
                }
            }

            public class Value
            {
                public Value() { }
                public Value(int pixels, int max) {
                    Pixels = pixels;
                    Percent = pixels / max;
                }
                public float Percent { get; }
                public int Pixels { get; }
            }

            public class BoundingBox
            {
                private int _left;
                private int _right;
                private int _bottom;
                private int _top;
                public BoundingBox(int minBottom, int minLeft, int maxTop, int maxRight) 
                {
                    this.MinBottom = minBottom;
                    this.MinLeft = minLeft;
                    this.MaxTop = maxTop;
                    this.MaxRight = maxRight;
                    _bottom = minBottom;
                    _left = minLeft;
                    _top = maxTop;
                    _right = maxRight;
                }
                public int GapX
                {
                    get
                    {
                        return (_left - MinLeft) + (MaxRight - _right);
                    }
                }
                public int GapY
                {
                    get
                    {
                        return (_bottom - MinBottom) + (MaxTop - _top);
                    }
                }
                public int MaxTop { get; set; }
                public int MaxRight { get; set; }
                public int MinBottom { get; set; }
                public int MinLeft { get; set; }
                public int Bottom
                {
                    get
                    {
                        return _bottom;
                    }
                    set
                    {
                        _bottom = Math.Max(MinBottom, value);
                    }
                }
                public int Left
                {
                    get
                    {
                        return _left;
                    }
                    set
                    {
                        _left = Math.Max(MinLeft, value);
                    }
                }
                public int Right
                {
                    get
                    {
                        return _right;
                    }
                    set
                    {
                        _right = Math.Min(MaxRight, value);
                    }
                }
                public int Top
                {
                    get
                    {
                        return _top;
                    }
                    set
                    {
                        _top = Math.Min(MaxTop, value);
                    }
                }
                public int Height
                {
                    get { return Math.Max(0, Top - Bottom); }
                    set { Top = Bottom + value; }
                }
                public int Width
                {
                    get { return Math.Max(0, Right - Left); }
                    set { Right = Left + value; }
                }

                public override string ToString()
                {
                    return $"{Top} {Right} {Bottom} {Left}";
                }
            }

            #endregion
        }
    }
}

namespace Oxide.Plugins
{
	partial class ComputersPlus : CovalencePlugin
	{
		public static partial class EZUI
        {
            private interface IImageComponent
            {
                string ImageKey { get; set; }
                bool ImageHidden { get; set; }
                Color ImageColor { get; set; }
            }

            public class ImageComponent : Component, IBoxComponent, IImageComponent
            {
                public ImageComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = Color.TRANSPARENT;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;
                public string ImageKey { get; set; } = "";
                public bool ImageHidden { get; set; } = false;
                public Color ImageColor { get; set; } = null;

                protected override Component CreateHelper()
                {
                    var box = new BoxComponent("Box")
                    {
                        BackgroundColor = BackgroundColor
                    }
                    .Inherit(this)
                    .Create(this);
                    var image = new BasicComponent("Image") { Empty = true }.Create(box);
                    if (!ImageHidden)
                    {

                        var element = new CuiElement
                        {
                            Name = image.GlobalId,
                            Parent = image.ParentGlobalId,
                            Components ={
                                new CuiRectTransformComponent {
                                    AnchorMin = $"0 0",
                                    AnchorMax = $"1 1"
                                }
                            }
                        };
                        if (ImageColor == null)
                        {
                            element.Components.Add(new CuiRawImageComponent
                            {
                                Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", ImageKey)
                            });
                        }
                        else
                        {
                            element.Components.Add(new CuiImageComponent
                            {
                                Png = PLUGIN.ImageLibrary?.Call<string>("GetImage", ImageKey),
                                Color = ImageColor.ToString()
                            });
                        }
                        image.Elements.Add(element);
                    }
                    return this;
                }

                public override Component Inherit(Component from, bool recursive = false)
                {
                    var casted = (IImageComponent)from;
                    this.ImageKey = casted.ImageKey;
                    this.ImageHidden = casted.ImageHidden;
                    return recursive ? base.Inherit(from) : this;
                }
            }
        }
	}
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        #region Commands
        [Command("input.data")] // input.data <token> <globalId> <commandLength> <command> <data..>
        private void cmd_input_data(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])) { return; }
            var basePlayer= player.Object as BasePlayer;
            if (basePlayer != null && args.Length >= 4)
            {
                string globalId = args[1];
                var component = EZUI.Find(basePlayer.UserIDString, globalId);
                if (component != null)
                {
                    int cmdLength = 0;
                    var cmdArgs = new string[] { };
                    var value = "";
                    if (int.TryParse(args[3], out cmdLength) && cmdLength > 0)
                    {
                        args = args.Skip(3).ToArray();
                        cmdArgs = args.Take(cmdLength).ToArray();
                        args = args.Skip(cmdLength).ToArray();
                    }
                    else
                    {
                        args = args.Skip(3).ToArray();
                    }
                    if (args.Length > 0)
                    {
                        value = string.Join(" ", args);
                        ((EZUI.InputComponent)component).Data = value;
                    }
                    if (cmdArgs.Length > 0)
                    {
                        var cmd = cmdArgs[0];
                        var newCmdArgs = cmdArgs.Skip(1).ToList();
                        newCmdArgs.AddRange(value.Split(' '));
                        player.Command(cmd, newCmdArgs.ToArray());
                    }
                }
            }
        }
        #endregion

        public static partial class EZUI
        {
            private interface IInputComponent
            {
                string Command { get; set; }
                int CharsLimit { get; set; }
            }

            public class InputComponent : Component, IInputComponent, IBoxComponent, ITextComponent
            {
                public InputComponent(string id) : base(id)
                {
                }

                public override Dir<int> PixelPadding { get; set; } = new Dir<int>(0);
                public override Color BackgroundColor { get; set; } = Color.WHITE;
                public string Command { get; set; } = "";
                public int CharsLimit { get; set; } = 6;
                public string Text { get; set; } = null;
                public string FontType { get; set; } = StyleSheet.DEFAULT.FontType1;
                public int FontSize { get; set; } = 12;
                public Color FontColor { get; set; } = StyleSheet.DEFAULT.FontColorDark1;
                public TextAnchor TextAlign { get; set; } = TextAnchor.MiddleLeft;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.OutlineColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;
                public bool AutoSizeHeight { get; set; } = false;
                public bool AutoSizeWidth { get; set; } = false;
                public string Data { get; set; } = null;

                private int CommandLength
                {
                    get
                    {
                        return string.IsNullOrEmpty(Command) ? 0 : Command.Split(' ').Length;
                    }
                }

                protected override Component CreateHelper()
                {
                    var box = new BoxComponent("Input")
                    {
                        BackgroundColor = this.Parent?.BackgroundColor
                    }
                    .Inherit(this)
                    .Create(this);
                    var parent = box;
                    if (Text != null)
                    {
                        var label = new TextComponent("Label")
                        {
                            BackgroundColor = Color.TRANSPARENT,
                            AnchorTo = Anchor.TopLeft,
                            Text = Text,
                            AutoSizeHeight = true,
                            PixelMargin = new Dir<int>(0, 0, 5, 0)
                        }.Create(box);
                        var body = new BoxComponent("Body")
                        {
                            TopAlign = label.BottomSide.Pixels,
                            BackgroundColor = BackgroundColor,
                            PixelPadding = new Dir<int>(5)
                        }.Create(box);
                        parent = body;
                    }
                    var input = new BasicComponent("Typer") { Empty = true }.Create(parent);
                    input.Elements.Add(new CuiElement
                    {
                        Name = input.GlobalId,
                        Parent = parent.GlobalId,
                        Components = {
                            new CuiInputFieldComponent
                            {
                                CharsLimit = CharsLimit,
                                Command = $"input.data {Token} {GlobalId} {CommandLength} {Command}",
                                Font = FontType,
                                FontSize = FontSize,
                                Color = FontColor.ToString(),
                                Align = TextAlign
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = input.AnchorMin,
                                AnchorMax = input.AnchorMax,
                                OffsetMin = input.OffsetMin,
                                OffsetMax = input.OffsetMax
                            }
                        }
                    });
                    return this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            public enum LayoutFlow
            {
                Horizontal,
                Vertical,
                HorizontalReverse,
                VerticalReverse
            }

            private interface ILayoutComponent
            {
                List<Component> Entries { get; set; }
                LayoutFlow LayoutFlow { get; set; }
                int RowCount { get; set; }
                int ColCount { get; set; }
                int Page { get; }
                Dir<int> EntryPixelPadding { get; set; }
            }

            public class LayoutComponent : Component, IBoxComponent, ILayoutComponent
            {
                public LayoutComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = Color.TRANSPARENT;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorDark1;
                public int OutlinePixelWeight { get; set; } = 1;
                public List<Component> Entries { get; set; } = new List<Component>();
                public LayoutFlow LayoutFlow { get; set; } = LayoutFlow.Horizontal;
                public int RowCount { get; set; } = 1;
                public int ColCount { get; set; } = 1;
                public Dir<int> EntryPixelPadding { get; set; } = new Dir<int>(0);
                public int Page { get; internal set; } = 1;
                private Component ListContent { get; set; } = null;
                public int MaxPage { get { return Math.Max(1, (int)Math.Ceiling((float)Entries.Count / (RowCount * ColCount))); } }
                public PaginatorComponent Paginator { get; internal set; }


                protected override Component CreateHelper()
                {
                    var box = CreateListContent();
                    this.ListContent = box;
                    return this;
                }

                private Component CreateListContent()
                {
                    var box = new BoxComponent("Layout")
                    {
                        BackgroundColor = BackgroundColor
                    }.Inherit(this).Create(this);
                    return box;
                }

                private void CreateEntries()
                {
                    var box = this.ListContent;
                    int pixelWidth = box.Content.PixelWidth / ColCount;
                    int pixelHeight = box.Content.PixelHeight / RowCount;
                    int startX = box.Content.PixelX + EntryPixelPadding.Right;
                    int startY = box.Content.PixelY + box.Content.PixelHeight - pixelHeight - EntryPixelPadding.Bottom;
                    int pixelX = startX;
                    int pixelY = startY;
                    int i = (Page - 1) * RowCount * ColCount;
                    int startI = i;
                    int count = RowCount * ColCount;
                    int row = 0;
                    int col = 0;
                    foreach (var entry in Entries.Skip(startI).Take(count))
                    {
                        var flex = new BasicComponent($"{i}")
                        {
                            PixelX = pixelX,
                            PixelY = pixelY,
                            PixelWidth = pixelWidth,
                            PixelHeight = pixelHeight,
                            PixelPadding = new Dir<int>(0, EntryPixelPadding.Right, EntryPixelPadding.Bottom, 0),
                            BackgroundColor = Color.TRANSPARENT
                        }.Create(box);
                        CreateEntry(flex, entry);

                        if (col < ColCount - 1)
                        {
                            col++;
                            pixelX += pixelWidth;
                        }
                        else
                        {
                            row++;
                            col = 0;
                            pixelX = startX;
                            pixelY -= flex.PixelHeight;
                        };
                        if (row >= RowCount)
                        {
                            break;
                        }
                        i++;
                    }
                }

                public void CreateEntry(Component parent, Component entry, int i = 0)
                {
                    var children = entry.Children;
                    var e = entry.Create(parent);
                    foreach (var child in children)
                    {
                        CreateEntry(e, child, i++);
                    }
                }
                protected override Component OnRender(BasePlayer basePlayer)
                {
                    var c = CreateRecursively().As<EZUI.LayoutComponent>();
                    c.CreateEntries();
                    if (this.Paginator != null)
                    {
                        Paginator.MaxPage = MaxPage;
                        Paginator.Create().Render(basePlayer);
                    }
                    return this;
                }

                public void ClearEntries()
                {
                    this.Entries.Clear();
                }

                public override void AddChild(Component child)
                {
                    if (Created && child.LocalId != "Layout")
                    {
                        Entries.Add(child);
                        if (this.Paginator != null)
                        {
                            this.Paginator.MaxPage = MaxPage;
                        }
                    }
                    else
                    {
                        base.AddChild(child);
                    }
                }

                public LayoutComponent SetPaginator(PaginatorComponent paginator)
                {
                    this.Paginator = paginator;
                    if (Paginator.Layout == null)
                    {
                        Paginator.SetLayout(this);
                    }
                    return this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        [Command("page.set")] // page.set <token> <paginatorId> <layoutId> <page> 
        private void cmd_page_set(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0 || !EZUI.ValidateToken(player, command, args[0])) { return; }
            args = args.Skip(1).ToArray();
            BasePlayer basePlayer = player.Object as BasePlayer;
            int page;
            if (args.Length >= 3 && int.TryParse(args[2], out page) && page > 0)
            {
                string pagId = args[0];
                string layoutId = args[1];
                var pag = (EZUI.PaginatorComponent)EZUI.Find(basePlayer.UserIDString, pagId.ToString());
                if (pag.Page != page)
                {
                    var layout = (EZUI.LayoutComponent)EZUI.Find(basePlayer.UserIDString, layoutId.ToString());
                    if (pag != null && layout != null && page <= layout.MaxPage)
                    {
                        pag.UpdatePageAndRender(basePlayer, page);
                    }
                }
            }
        }

        public static partial class EZUI
        {
            private interface IPaginatorComponent
            {
                LayoutComponent Layout { get; }
                int Page { get; }
                int MaxPage { get; }
            }

            public class PaginatorComponent : Component, IPaginatorComponent, IBoxComponent, ITextComponent
            {
                public PaginatorComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorLight2;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;
                public List<Component> Entries { get; set; }
                public string Text { get; set; }
                public string FontType { get; set; } = DEFAULT_FONT_TYPE;
                public int FontSize { get; set; } = 12;
                public Color FontColor { get; set; } = StyleSheet.DEFAULT.FontColorDark1;
                public TextAnchor TextAlign { get; set; } = TextAnchor.MiddleCenter;
                public LayoutComponent Layout { get; internal set; }
                public int Page { get; internal set; } = 1;
                public int MaxPage { get; set; } = 1;
                public bool AutoSizeHeight { get; set; } = false;
                public bool AutoSizeWidth { get; set; } = true;

                protected override Component CreateHelper()
                {
                    var box = new BoxComponent("Box")
                    {
                        BackgroundColor = BackgroundColor
                    }
                    .Inherit(this)
                    .Create(this);
                    var left = new ButtonComponent("Left")
                    {
                        Command = $"page.set {Token} {GlobalId} {Layout?.GlobalId} {(Page > 1 ? Page - 1 : 1)}",
                        PixelWidth = 60,
                        Text = "<<",
                        BackgroundColor = StyleSheet.DEFAULT.BackgroundColorLight3,
                        TextAlign = TextAlign,
                        FontType = FontType,
                        FontSize = 14,
                        FontColor = FontColor,
                        AnchorTo = Anchor.MiddleLeft
                    }.Create(box);
                    var right = new ButtonComponent("Right")
                    {
                        Command = $"page.set {Token} {GlobalId} {Layout?.GlobalId} {(Page + 1)}",
                        PixelWidth = 60,
                        BackgroundColor = StyleSheet.DEFAULT.BackgroundColorLight3,
                        FontType = FontType,
                        FontSize = 14,
                        FontColor = FontColor,
                        Text = ">>",
                        TextAlign = TextAlign,
                        AnchorTo = Anchor.MiddleRight
                    }.Create(box);
                    var text = new TextComponent("Text")
                    {
                        Text = $"{Page}/{MaxPage}",
                        Centered = true,
                        TextAlign = TextAlign
                    }.Inherit(this).Create(box);

                    return this;
                }

                public PaginatorComponent SetLayout(LayoutComponent layout)
                {
                    this.Layout = layout;
                    if (Layout.Paginator == null)
                    {
                        Layout.SetPaginator(this);
                    }
                    return this;
                }

                public PaginatorComponent UpdatePageAndRender(BasePlayer basePlayer, int page = 0)
                {
                    if (page <= 0)
                    {
                        page = Page;
                    }
                    this.MaxPage = Layout.MaxPage;
                    this.Page = Math.Min(page, MaxPage);
                    this.Layout.Page = Page;
                    Layout.Create().Render(basePlayer);
                    this.Create().Render(basePlayer);
                    return this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            public class StyleSheet
            {
                #region Defaults
                public static readonly StyleSheet DEFAULT = new StyleSheet();
                #endregion
                #region Home Colors
                public Color HomeBackgroundColor { get; set; } = new Color(0.125f, 0.549f, 0.443f);
                #endregion
                #region Background Colors
                public Color BackgroundColorLight1 { get; set; } = new Color(200, 200, 200);
                public Color BackgroundColorLight2 { get; set; } = new Color(185, 185, 185);
                public Color BackgroundColorLight3 { get; set; } = new Color(170, 170, 170);
                public Color BackgroundColorLight4 { get; set; } = new Color(155, 155, 155);
                public Color BackgroundColorDark1 { get; set; } = new Color(42, 0, 255);
                public Color BackgroundColorDark2 { get; set; } = new Color(42, 0, 255);
                public Color BackgroundColorDark3 { get; set; } = new Color(42, 0, 255);
                public Color BackgroundColorDark4 { get; set; } = new Color(42, 0, 255);
                public Color BackgroundColorHighlight1 { get; set; } = new Color(255, 240, 150);
                #endregion
                #region Outline Colors
                public Color OutlineColorLight1 { get; set; } = new Color(255, 255, 255);
                public Color OutlineColorDark1 { get; set; } = new Color(0, 0, 0);
                #endregion
                #region Font Colors
                public Color FontColorLight1 { get; set; } = new Color(255, 255, 255);
                public Color FontColorLight2 { get; set; } = new Color(220, 220, 220);
                public Color FontColorDark1 { get; set; } = new Color(0, 0, 0);
                public Color FontColorDark2 { get; set; } = new Color(40, 40, 40);
                #endregion
                #region Font Types
                public string FontType1 { get; set; } = "RobotoCondensed-Regular.ttf";
                public string FontType2 { get; set; } = "RobotoCondensed-Bold.ttf";
                #endregion
                #region Font Size
                public int FontSize1 { get; set; } = 14;
                public int FontSize2 { get; set; } = 12;
                public int FontSize3 { get; set; } = 10;
                #endregion
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            private interface ITextComponent
            {
                string Text { get; set; }
                string FontType { get; set; }
                int FontSize { get; set; }
                Color FontColor { get; set; }
                TextAnchor TextAlign { get; set; }
                bool AutoSizeHeight { get; set; }
                bool AutoSizeWidth { get; set; }
            }

            public class TextComponent : Component, ITextComponent, IBoxComponent
            {
                public TextComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = Color.TRANSPARENT;
                public string Text { get; set; } = "";
                public string FontType { get; set; } = DEFAULT_FONT_TYPE;
                public int FontSize { get; set; } = DEFAULT_FONT_SIZE;
                public Color FontColor { get; set; } = StyleSheet.DEFAULT.FontColorDark1;
                public TextAnchor TextAlign { get; set; } = TextAnchor.MiddleLeft;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.OutlineColorDark1;
                public int OutlinePixelWeight { get; set; } = 0;
                public bool AutoSizeHeight { get; set; } = false;
                public bool AutoSizeWidth { get; set; } = false;

                protected override Component CreateHelper()
                {
                    var box = new BoxComponent("Box")
                    {
                        BackgroundColor = BackgroundColor
                    }.Inherit(this)
                    .Create(this);
                    var text = new BasicComponent("Text") { Empty = true }.Create(box);
                    text.Elements.Add(new CuiElement
                    {
                        Name = text.GlobalId,
                        Parent = text.ParentGlobalId,
                        Components = {
                        new CuiTextComponent {
                            Text = Text,
                            Font = FontType,
                            FontSize = FontSize,
                            Color = FontColor.ToString(),
                            Align = TextAlign
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                    });
                    return this;
                }

                public override Component Init(Component parent)
                {
                    if (AutoSizeHeight)
                    {
                        this.PixelHeight = FontSize + 1;
                    }
                    if (AutoSizeWidth)
                    {
                        this.PixelWidth = (int) Math.Ceiling((FontSize+1) * 0.48f) * Text?.Length ?? 0;
                    }
                    return base.Init(parent);
                }

                public override Component Inherit(Component from, bool recursive = false)
                {
                    var casted = (ITextComponent)from;
                    this.Text = casted.Text ?? Text;
                    this.FontType = casted.FontType;
                    this.FontSize = casted.FontSize;
                    this.FontColor = casted.FontColor;
                    this.TextAlign = casted.TextAlign;
                    this.AutoSizeHeight = casted.AutoSizeHeight;
                    this.AutoSizeWidth = casted.AutoSizeWidth;
                    return recursive ? base.Inherit(from) : this;
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
		[Command("t.show")]
		private void cmd_ui_test_show(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;
            //var main = new EZUI.BasicComponent("test") { Height = 0.5f, Width = 0.5f }.Create(null);
            //var main = new EZUI.BoxComponent("test") { Height = 0.8f, Width = 0.8f, Centered = true }.Create(null);
            //var box1 = new EZUI.BoxComponent("Box1") { Height = 0.2f, Width = 0.2f, BackgroundColor = EZUI.Color.RED, Centered = true}.Create(main);
            //var box2 = new EZUI.BoxComponent("Box2") { Height = 0.1f, Width = 0.1f, BackgroundColor = EZUI.Color.GREEN, TopAlign = box1.BottomSide.Pixels, LeftAlign = box1.LeftSide.Pixels }.Create(main);

            //var main = new EZUI.ButtonComponent("test")
            //{
            //    PixelHeight = 100,
            //    PixelWidth = 100,
            //    Text = "Hello",
            //    Command = "t.close",
            //    BackgroundColor = EZUI.Color.WHITE,
            //    TextAlign = UnityEngine.TextAnchor.MiddleCenter
            //}.Create(null);

            //var main = new EZUI.ImageComponent("test")
            //{
            //    PixelHeight = 100,
            //    PixelWidth = 100,
            //    ImageKey = "note"
            //}.Create(null);

            //var main = new EZUI.TextComponent("test")
            //{
            //    PixelHeight = 100,
            //    PixelWidth = 500,
            //    Text = "Hellooooo"
            //}.Create(null);
            //var main = new EZUI.WindowComponent("test")
            //{
            //    Scale = 0.5f,
            //    Centered = true
            //}.Create();
            //var pag = new EZUI.PaginatorComponent("Pag")
            //{
            //    PixelHeight = 40
            //}.Create(main).As<EZUI.PaginatorComponent>();
            //var list = new EZUI.LayoutComponent("List")
            //{
            //    Centered = true,
            //    ColCount = 4,
            //    RowCount = 1,
            //    BottomAlign = pag.TopSide.Pixels
            //}.Init(main).As<EZUI.LayoutComponent>().SetPaginator(pag).Create();
            //for (int i = 0; i < 15; i++)
            //{
            //    //var img = new EZUI.ImageComponent($"Img") { Size = 0.25f, ImageKey = "note", Centered = true }.Init(list);
            //    var txt = new EZUI.TextComponent("Txt")
            //    {
            //        Text = "Entry" + i.ToString(),
            //        TextAlign = UnityEngine.TextAnchor.MiddleCenter,
            //        Centered = true
            //    }.Init(list);
            //}
            //list.Create(main);
            //main.Render(basePlayer);
            //var main = new EZUI.BoxComponent("test")
            //{
            //    Height = 0.5f,
            //    Width = 0.5f,
            //    PixelPadding = new EZUI.Dir<int>(20)
            //}.Create(null);
            //var pag = new EZUI.PaginatorComponent("pag")
            //{
            //    PixelHeight = 40,
            //    BackgroundColor = EZUI.Color.RED
            //}.Create(main).As<EZUI.PaginatorComponent>();
            //var list = new EZUI.LayoutComponent("list")
            //{
            //    BackgroundColor = EZUI.Color.BLUE,
            //    Centered = true,
            //    ColCount = 4,
            //    RowCount = 1,
            //    BottomAlign = pag.TopSide.Pixels
            //}.Create(main).As<EZUI.LayoutComponent>().SetPaginator(pag);
            //for (int i = 0; i < 6; i++)
            //{
            //    //var img = new EZUI.ImageComponent($"Img") { Size = 0.25f, ImageKey = "note", Centered = true }.Init(list);
            //    var box = new EZUI.BoxComponent("MyBox")
            //    {
            //        BackgroundColor = EZUI.Color.GREEN,
            //        Size = 0.4f
            //    }.Create(list);
            //    var sub = new EZUI.BoxComponent("Sub")
            //    {
            //        BackgroundColor = EZUI.Color.BLACK,
            //        Size = 0.6f
            //    }.Create(box);
            //}
            //var main = new EZUI.WindowComponent("test")
            //{
            //    Scale = 0.5f,
            //    Centered = true
            //}.Create();
            //var toolbar = new EZUI.ToolbarComponent("TB")
            //{
            //    PixelHeight = 20,
            //    Buttons = new List<EZUI.ToolbarButton>()
            //        {
            //            new EZUI.ToolbarButton() { /*ImageKey = "note"*/ Text = "Compose" },
            //            new EZUI.ToolbarButton() { /*ImageKey = "note"*/ Text = "Delete" }
            //        },
            //    AnchorTo = EZUI.Anchor.TopLeft
            //}.Create(main);
            //var text = new EZUI.TextComponent("txt")
            //{
            //    Text = "Hello my name is Sam",
            //    FontSize = 8,
            //    AutoSizeWidth = true,
            //    BackgroundColor = EZUI.Color.RED
            //}.Create(main);
            //main.Render(basePlayer);
        }

        [Command("t.email")]
        private void cmd_ui_test_email(IPlayer player, string command, string[] args)
        {

            var inbox = EmailManager.GetPlayerInbox(player.Id);
            PLUGIN.Puts($"Inbox size {inbox.Length}");
        }

        [Command("t.close")]
		private void cmd_ui_test_close(IPlayer player, string command, string[] args)
		{

			BasePlayer basePlayer = player.Object as BasePlayer;


            var test = EZUI.Find(basePlayer.UserIDString, "test");
            test.Destroy(basePlayer);
            //test.Destroy(basePlayer);
            CuiHelper.DestroyUi(basePlayer, "test");
            CuiHelper.DestroyUi(basePlayer, "test.Box1");
            CuiHelper.DestroyUi(basePlayer, "Box1");
            CuiHelper.DestroyUi(basePlayer, "Box1.Content");
            CuiHelper.DestroyUi(basePlayer, "cpBase");
            CuiHelper.DestroyUi(basePlayer, "cpHome");
            CuiHelper.DestroyUi(basePlayer, "cpApp");
            
        }

        [Command("t.close2")]
        private void cmd_ui_test_close2(IPlayer player, string command, string[] args)
        {

            BasePlayer basePlayer = player.Object as BasePlayer;
            CuiHelper.DestroyUi(basePlayer, "PC");
            CuiHelper.DestroyUi(basePlayer, "test");
            CuiHelper.DestroyUi(basePlayer, "Box1");
            CuiHelper.DestroyUi(basePlayer, "Box1.Content");
            CuiHelper.DestroyUi(basePlayer, "cpBase");
            CuiHelper.DestroyUi(basePlayer, "cpHome");
            CuiHelper.DestroyUi(basePlayer, "test");
            CuiHelper.DestroyUi(basePlayer, "test.Content");
            CuiHelper.DestroyUi(basePlayer, "test.Content.Entry0");
            CuiHelper.DestroyUi(basePlayer, "test.Content.Entry0.Content");
            CuiHelper.DestroyUi(basePlayer, "test.Content.Entry0.Content.Box");
            CuiHelper.DestroyUi(basePlayer, "test.Content.Entry0.Content.Box.Content");
        }

        [Command("t.print")]
		private void cmd_ui_test_print(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;
			Puts($"{new string('\n', 30)}{EZUI.PrintLoaded(basePlayer)}");
			if (args.Length > 0)
            {
				var gid = args[0];
                int i;
                if (int.TryParse(gid, out i))
                {
                    var all = EZUI.GetAll(basePlayer);
                    if (i < all.Count)
                    {
                        var ui = all[i];
                        Puts(ui.ToString());
                    }
                }
                else
                {
                    var ui = EZUI.Find(basePlayer.UserIDString, gid);
                    if (ui != null)
                    {
                        Puts(ui.ToString());
                    }
                }
            }
		}

        [Command("sfx")]
        private void cmd_sfx(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            var sfx = string.Join(" ", args);
            PlaySfx(basePlayer, sfx);
        }

        [Command("pc.destroy")]
        private void cmd_pc_destroy(IPlayer player, string command, string[] args)
        {
            var allPcs = GameObject.FindObjectsOfType<ComputerStation>();
            foreach (ComputerStation stat in allPcs)
            {
                stat.Kill();
            }
        }

        #region Tests
        private void TestBox(BasePlayer basePlayer)
        {
            //var box1 = new EZUI.BoxFactory(null, "test")
            //{
            //    Size = 0.6f,
            //    BackgroundColor = EZUI.Color.RED,
            //    Centered = true
            //}.Create();
            //var box2 = new EZUI.BoxFactory(box1, "Box2")
            //{
            //    Height = 0.3f,
            //    Width = 0.5f,
            //    BackgroundColor = EZUI.Color.BLUE,
            //    AnchorTo = EZUI.Anchor.TopRight,
            //    PixelMargin = new EZUI.Dir<int>(0, 0, 5, 0)
            //}.Create();
            //var box3 = new EZUI.BoxFactory(box1, "Box3")
            //{
            //    PixelHeight = 200,
            //    PixelWidth = 5,
            //    BackgroundColor = EZUI.Color.GREEN,
            //    TopAlign = box2.BottomSide.Pixels,
            //    LeftAlign = box2.LeftSide.Pixels
            //}.Create();
            //box1.Render(basePlayer);
        }

        private void TestWindow(BasePlayer basePlayer)
        {
            //var main = new EZUI.WindowBoxFactory(null, "test")
            //{
            //    PixelPadding = new EZUI.Dir<int>(5),
            //    Height = 0.8f,
            //    Width = 0.8f,
            //    Centered = true
            //}.Create();
            //var box = new EZUI.BoxFactory(main, "Box")
            //{
            //    PixelX = 50,
            //    PixelY = 50,
            //    PixelWidth = 100,
            //    PixelHeight = 100,
            //    BackgroundColor = EZUI.Color.RED
            //}.Create();
            //var box2 = new EZUI.BoxFactory(main, "Box2")
            //{
            //    X = 0,
            //    Y = 0,
            //    Width = 0.1f,
            //    Height = 0.5f,
            //    BackgroundColor = EZUI.Color.BLUE
            //}.Create();
            //main.Render(basePlayer);
        }
        #endregion
    }
}

namespace Oxide.Plugins
{
    partial class ComputersPlus : CovalencePlugin
    {
        public static partial class EZUI
        {
            private interface IWindowComponent
            {
                Color HeaderBackgroundColor { get; set; }
                int HeaderPixelHeight { get; set; }
                string HeaderText { get; set; }
            }

            public class WindowComponent : Component, IWindowComponent, IBoxComponent
            {
                public WindowComponent(string id) : base(id)
                {
                }

                public override Color BackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorLight1;
                public Color HeaderBackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorDark1;
                public string HeaderText { get; set; } = "Header";
                public int HeaderPixelHeight { get; set; } = 24;
                public Color OutlineColor { get; set; } = StyleSheet.DEFAULT.OutlineColorDark1;
                public int OutlinePixelWeight { get; set; } = 4;
                private float WindowScale { get; set; } = 1.0f;
                private Color WindowBackgroundColor { get; set; } = StyleSheet.DEFAULT.BackgroundColorLight1;
                public string OnCloseCommand { get; set; } = "";

                protected override List<CuiElement> CreateBaseElements()
                {
                    WindowBackgroundColor = this.BackgroundColor;
                    WindowScale = this.Scale;
                    this.Scale = 1f;
                    this.BackgroundColor = Color.TRANSPARENT;
                    this.Init(this.Parent);
                    return base.CreateBaseElements();
                }

                private Component CreateHeader(Component box)
                {
                    var header = new BoxComponent("Header")
                    {
                        BackgroundColor = HeaderBackgroundColor,
                        PixelHeight = HeaderPixelHeight,
                        AnchorTo = Anchor.TopLeft,
                        PixelMargin = new Dir<int>(0, 0, OutlinePixelWeight, 0),
                        PixelPadding = new Dir<int>(5)
                    }
                    .Create(box);
                    var text = new TextComponent("Text")
                    {
                        Text = HeaderText,
                        AnchorTo = Anchor.TopLeft,
                        Width = 0.5f,
                        TextAlign = UnityEngine.TextAnchor.MiddleLeft,
                        FontColor = Color.WHITE
                    }.Create(header);
                    var close = new ButtonComponent("Close")
                    {
                        Transparent = true,
                        ImageKey = "App.Close",
                        AnchorTo = Anchor.Right,
                        Size = 1f,
                        Left = 2,
                        CenterY = true,
                        Command = $"component.destroy {Token} {GlobalId}"
                    }.Create(header);
                    return header;
                }

                protected override Component CreateHelper()
                {
                    var shadow = new BoxComponent("Shadow")
                    {
                        BackgroundColor = new Color(0, 0, 0, 0.9f)
                    }.Create(this);
                    var box = new BoxComponent("Window")
                    {
                        BackgroundColor = OutlineColor,
                        Scale = WindowScale,
                        Centered = Centered
                    }
                    .Inherit(this)
                    .Create(shadow);
                    var header = CreateHeader(box);
                    var content = new BoxComponent("Body")
                    {
                        BackgroundColor = WindowBackgroundColor,
                        PixelPadding = new Dir<int>(5),
                        TopAlign = header.BottomSide.Pixels
                    }
                    .Create(box);
                    this.Content = content;
                    return this;
                }

                protected override Component OnDestroy(BasePlayer basePlayer)
                {
                    var split = OnCloseCommand.Split(' ');
                    var command = split[0];
                    var args = split.Skip(1);
                    basePlayer.Command(command, args);
                    return base.OnDestroy(basePlayer);
                }
            }
        }
    }
}
