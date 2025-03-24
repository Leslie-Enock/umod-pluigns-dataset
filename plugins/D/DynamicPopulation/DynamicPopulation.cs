using Connection = Network.Connection;
using Oxide.Core.Libraries.Covalence;
using Exception = System.Exception;
using System.Collections.Generic;
using Newtonsoft.Json;
using ConVar;

namespace Oxide.Plugins {
    [Info("Dynamic Population", "Aspectdev", "2.1.6")]
    [Description("Automatically manage server MaxPlayers with customization options.")]
    class DynamicPopulation : CovalencePlugin {
        #region Fields

		public class PluginToggles {
			public bool Enable_Plugin = false;
			public bool Enable_Queueing = true;
			public bool OverageIsQueue = true;
			public bool FPS_Limiting = true;
			public bool Enable_Logs = true;
		};

		public class BaseServerVariables {
			public int Server_MinPlayers = 175;
			public int Server_MaxPlayers = 250;
			public int Average_FPS_Limit = 15;
		};

		public class DecreasePopOptions {
			public int Decrease_Pop_Threshold = 10;
			public int Pop_Decrease_Amount = 5;
		};

		public class QueuedEnabledOptions {
			public int Queue_Increase_Threshold = 10;
			public int Pop_Increase_Amount = 5;
		};

		public class QueueingDisabledOptions {
			public int Increase_Pop_Threshold = 5;
			public int Pop_Increase_Amount = 5;
		};

        private static Configuration _config;
        private class Configuration {
			public PluginToggles PluginToggles = new PluginToggles();
			public BaseServerVariables BaseServerVariables = new BaseServerVariables();
			public DecreasePopOptions DecreasePopOptions = new DecreasePopOptions();
			public QueuedEnabledOptions QueuedEnabledOptions = new QueuedEnabledOptions();
			public QueueingDisabledOptions QueueingDisabledOptions = new QueueingDisabledOptions();
        }

        #endregion

        #region Init

        private void Init() {
			if ( !_config.PluginToggles.Enable_Queueing && _config.QueueingDisabledOptions.Increase_Pop_Threshold > _config.DecreasePopOptions.Decrease_Pop_Threshold) {
				PrintError("Increase_Pop_Threshold cannot be greater than Decrease_Pop_Threshold, loading default config.");
				LoadDefaultConfig();
			};

			if (!_config.PluginToggles.Enable_Plugin) {
				PrintError("Config Variable enable_plugin is set to false. Please set your server's environmental variables and then reload the plugin.");
				Unsubscribe(nameof(OnPlayerConnected));
				Unsubscribe(nameof(CanClientLogin));
				Unsubscribe(nameof(OnPlayerDisconnected));
			};
        }

        #endregion

        #region Hooks

        void OnPlayerConnected(BasePlayer player) {
            UpdatePop();
        }

        void CanClientLogin(Connection connection) {
            UpdatePop();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason) {
            UpdatePop();
        }

        #endregion

        #region Config

        protected override void LoadConfig() {
			base.LoadConfig();
			_config = Config.ReadObject<Configuration>();
			if (_config == null) throw new Exception();
			SaveConfig();
        }

		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration();

		#endregion

		# region Functions

        private void UpdatePop()
        {
			int PlaceHolder_Current_Players = covalence.Server.Players + ConVar.Admin.ServerInfo().Joining;
			int Base_Current_Player_Amount = (_config.PluginToggles.Enable_Queueing) ? PlaceHolder_Current_Players : PlaceHolder_Current_Players + ConVar.Admin.ServerInfo().Queued;
			int Base_Max_Player_Capacity = covalence.Server.MaxPlayers;
			int Base_Queued_Players = (_config.PluginToggles.OverageIsQueue) ? (Base_Current_Player_Amount - Base_Max_Player_Capacity) + ConVar.Admin.ServerInfo().Queued : ConVar.Admin.ServerInfo().Queued;

			float AverageServerFPS = Performance.current.frameRateAverage;

			int Manipulated_Max_Player_Capacity = Base_Max_Player_Capacity;
			int Manipulated_Queued_Players = Base_Queued_Players;

			if (Manipulated_Max_Player_Capacity < _config.BaseServerVariables.Server_MinPlayers || Manipulated_Max_Player_Capacity > _config.BaseServerVariables.Server_MaxPlayers) {
				Manipulated_Max_Player_Capacity = (Manipulated_Max_Player_Capacity < _config.BaseServerVariables.Server_MinPlayers) ? _config.BaseServerVariables.Server_MinPlayers : _config.BaseServerVariables.Server_MaxPlayers;
			};

			if (_config.PluginToggles.FPS_Limiting ? AverageServerFPS > _config.BaseServerVariables.Average_FPS_Limit : true) {
				if (_config.PluginToggles.Enable_Queueing) {
					if (Manipulated_Queued_Players >= _config.QueuedEnabledOptions.Queue_Increase_Threshold && Manipulated_Max_Player_Capacity + _config.QueuedEnabledOptions.Pop_Increase_Amount <= _config.BaseServerVariables.Server_MaxPlayers) {
						for (int i = 0; i < 50; i++) {
							bool IncreaseCheck = (Manipulated_Queued_Players >= _config.QueuedEnabledOptions.Queue_Increase_Threshold && Manipulated_Max_Player_Capacity + _config.QueuedEnabledOptions.Pop_Increase_Amount <= _config.BaseServerVariables.Server_MaxPlayers);
							if (!IncreaseCheck) break;
							Manipulated_Max_Player_Capacity += _config.QueuedEnabledOptions.Pop_Increase_Amount;
							Manipulated_Queued_Players -= _config.QueuedEnabledOptions.Pop_Increase_Amount;
						};
					};
				} else {
					if (Manipulated_Max_Player_Capacity - Base_Current_Player_Amount <= _config.QueueingDisabledOptions.Increase_Pop_Threshold && Manipulated_Max_Player_Capacity + _config.QueueingDisabledOptions.Pop_Increase_Amount <= _config.BaseServerVariables.Server_MaxPlayers) {
						for (int i = 0; i < 50; i++) {
							bool IncreaseCheck = (Manipulated_Max_Player_Capacity - Base_Current_Player_Amount <= _config.QueueingDisabledOptions.Increase_Pop_Threshold && Manipulated_Max_Player_Capacity + _config.QueueingDisabledOptions.Pop_Increase_Amount <= _config.BaseServerVariables.Server_MaxPlayers);
							if (!IncreaseCheck) break;
							Manipulated_Max_Player_Capacity += _config.QueueingDisabledOptions.Pop_Increase_Amount;
						};
					};
				};
			};

			if (Manipulated_Max_Player_Capacity - Base_Current_Player_Amount >= _config.DecreasePopOptions.Decrease_Pop_Threshold && Base_Max_Player_Capacity - _config.DecreasePopOptions.Pop_Decrease_Amount >= _config.BaseServerVariables.Server_MinPlayers) {
				for (int i = 0; i < 50; i++) {
					bool DecreaseCheck = (Manipulated_Max_Player_Capacity - Base_Current_Player_Amount > _config.DecreasePopOptions.Decrease_Pop_Threshold && Manipulated_Max_Player_Capacity - _config.DecreasePopOptions.Pop_Decrease_Amount >= _config.BaseServerVariables.Server_MinPlayers);
					if (!DecreaseCheck) break;
					Manipulated_Max_Player_Capacity -= _config.DecreasePopOptions.Pop_Decrease_Amount;
				};
			};

			if (Manipulated_Max_Player_Capacity != Base_Max_Player_Capacity) SendUpdateRequest(Base_Max_Player_Capacity, Manipulated_Max_Player_Capacity);
        }

		private void SendUpdateRequest(int oldMaxPop, int newMaxPop) {
			if (_config.PluginToggles.Enable_Logs) Puts(string.Format("{0} Max Pop from {1} to {2}", (oldMaxPop > newMaxPop) ? "Decreasing" : "Increasing", oldMaxPop, newMaxPop));
			covalence.Server.Command(string.Format("server.maxplayers {0}", newMaxPop));
		}

		#endregion
    }
}