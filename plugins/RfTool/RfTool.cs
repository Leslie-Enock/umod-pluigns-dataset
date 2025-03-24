/*
    RfTool - A Rust umod plugin to manipulate/intercept in-game RF objects/signals
    Copyright (C) 2019 by Pinguin

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("RF Tool", "PinguinNordpol", "0.2.4")]
    [Description("Manipulates/intercepts in-game RF objects and signals")]
    class RfTool : CovalencePlugin
    {
        private int frequency_min = 1;
        private int frequency_max = 9999;
        private RfToolConfig config_data;
        private Timer listener_timer;

        #region Plugin Config
        /*
         * Classes & functions to load / store plugin configuration
         */
        private class ListenerData
        {
            public int frequency;
            public string msg;
            public int block_delay;
            public int cur_block_delay;

            /*
             * Constructor
             */
            public ListenerData(int _frequency, string _msg, int _block_delay)
            {
                this.frequency = _frequency;
                this.msg = _msg;
                this.block_delay = _block_delay;
                this.cur_block_delay = 0;
            }

            /*
             * Get configured frequency
             */
            public int GetFrequency()
            {
                return this.frequency;
            }

            /*
             * Get configured message
             */
            public string GetMessage()
            {
                return this.msg;
            }

            /*
             * Get block delay
             */
            public int GetBlockDelay()
            {
                return this.block_delay;
            }

            /*
             * Enable blocking
             */
            public void EnableBlocking()
            {
                if (this.block_delay != 0)
                {
                    this.cur_block_delay = this.block_delay;
                }
            }

            /*
             * Clear blocking
             */
            public void DisableBlocking()
            {
                this.cur_block_delay = 0;
            }

            /*
             * Check if listener is currently blocked and decrement block counter if it is
             */
            public bool IsBlocked()
            {
                if(this.cur_block_delay == 0)
                {
                    return false;
                }
                this.cur_block_delay -= 1;
                return true;
            }
        }
        private class RfToolConfig
        {
            public float tick_interval = 1f;
            public bool debug = false;
            public List<ListenerData> configured_listeners = new List<ListenerData>();
        }
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        private RfToolConfig GetDefaultConfig()
        {
            return new RfToolConfig();
        }
        #endregion

        #region Umod Hooks
        /*
         * Initialize plugin
         */
        void Init()
        {
            // Load plugin config
            this.config_data = Config.ReadObject<RfToolConfig>();

            // All configured listeners should be unblocked at the start
            this.ResetListenersCurBlockDelay();
        }

        /*
         * Get things rolling once server is ready
         */
        void OnServerInitialized()
        {
            // Get actual min/max frequencies in case they changed after development
            this.frequency_min = RFManager.minFreq; // Was 1
            this.frequency_max = RFManager.maxFreq; // Was 9999

            // Start listener loop timer
            this.listener_timer = timer.Every(this.config_data.tick_interval, this.CheckListeners);
        }

        /*
         * Clear up before server shuts down
         */
        void OnServerShutdown()
        {
            // Stop listener loop timer
            this.listener_timer.Destroy();
        }
        #endregion

        #region Localization
        /*
         * Load default messages
         */
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["cmd_rftool_help"] = "RfTool v{0} - A Rust umod plugin to manipulate/intercept in-game RF objects and signals\r\n" +
                                      "Copyright(C) 2019 by Pinguin and released under GPLv3\r\n" +
                                      "\r\n" +
                                      "The following commands are available:\r\n" +
                                      "  rftool.inspect : List all currently in-game exisiting receivers / broadcasters and their frequencies.\r\n" +
                                      "  rftool.enable : Enable all in-game receivers on the specified frequency.\r\n" +
                                      "  rftool.disable : Disable all in-game receivers on the specified frequency.\r\n" +
                                      "  rftool.listeners.list : List all configured virtual receivers.\r\n" +
                                      "  rftool.listeners.add : Add a virtual receiver on the specified frequency.\r\n" +
                                      "  rftool.listeners.del : Delete the virtual receiver on the specified frequency.\r\n" +
                                      "  rftool.listeners.get_interval : Get the interval in which virtual receivers operate.\r\n" +
                                      "  rftool.listeners.set_interval : Set the interval in which virtual receivers operate.\r\n" +
                                      "  rftool.debug : Toggle (enable/disable) internal debugging. Only used for testing!\r\n" +
                                      "\r\n" +
                                      "For commands that take arguments, more help is available by executing them without any arguments.\r\n" +
                                      "\r\n" +
                                      "To be able to execute any rftool commands, you need to have the umod 'rftool.use' right assigned to your user.",
                ["cmd_rftool.inspect_listeners"] = "Found {0} listener(s) on frequency {1}",
                ["cmd_rftool.inspect_broadcaster"] = "Found {0} broadcaster(s) on frequency {1}",
                ["cmd_rftool.enable_help"] = "Usage:\r\n" +
                                             "  rftool.enable <frequency>\r\n" +
                                             "\r\n" +
                                             "Description:\r\n" +
                                             "  This command enables all currently available in-game receivers listening on the specified\r\n" +
                                             "  frequency <frequency>. This is similiar to broadcasting on the given frequency in-game\r\n" +
                                             "  except that receivers set to the given frequency after this command was executed won't\r\n" +
                                             "  get enabled!\r\n" +
                                             "\r\n" +
                                             "Options:\r\n" +
                                             "  <frequency> : The frequency on which in-game receivers have to listen to get enabled ({0}-{1}).",
                ["cmd_rftool.enable_success"] = "Enabled {0} listener(s) on frequency {1}",
                ["cmd_rftool.disable_help"] = "Usage:\r\n" +
                                              "  rftool.disable <frequency>\r\n" +
                                              "\r\n" +
                                              "Description:\r\n" +
                                              "  This command disables all currently available in-game receivers listening on the specified\r\n" +
                                              "  frequency <frequency>.\r\n" +
                                              "\r\n" +
                                              "Options:\r\n" +
                                              "  <frequency> : The frequency on which in-game receivers have to listen to get disabled ({0}-{1}).",
                ["cmd_rftool.disable_success"] = "Disabled {0} listener(s) on frequency {1}",
                ["cmd_rftool.listeners.add_help"] = "Usage:\r\n" +
                                                    "  rftool.listeners.add <frequency> <log_message> [<block_delay>]\r\n" +
                                                    "\r\n" +
                                                    "Description:\r\n" +
                                                    "  This command adds a new virtual RF receiver that, when triggered in-game\r\n" +
                                                    "  on the specified frequency <frequency>, will log the specified <log_message>\r\n" +
                                                    "  message to the console. Optionally, a block delay <block_delay> may be specified\r\n" +
                                                    "  during which no more messages should be send to the console once triggered. The\r\n" +
                                                    "  actual delay is <block_delay> * 'configured interval'. See also the help message\r\n" +
                                                    "  of the command rftool.listeners.set_interval\r\n" +
                                                    "\r\n" +
                                                    "Options:\r\n" +
                                                    "  <frequency> : The frequency on which to listen for broadcasts ({0}-{1}).\r\n" +
                                                    "  <log_message> : The message that should be logged to the console.\r\n" +
                                                    "  <block_delay> : Delay to block once triggered (0=no delay).",
                ["cmd_rftool.listeners.add_error"] = "A listener on frequency {0} is already configured!",
                ["cmd_rftool.listeners.add_success"] = "Added listener on frequency {0} with a block delay of {1} and the log message '{2}'",
                ["cmd_rftool.listeners.del_help"] = "Usage:\r\n" +
                                                    "  rftool.listeners.del <frequency>\r\n" +
                                                    "\r\n" +
                                                    "Description:\r\n" +
                                                    "  This command removes a previously added virtual RF receiver operating on the\r\n" +
                                                    "  specified frequency <frequency>.\r\n" +
                                                    "\r\n" +
                                                    "Options:\r\n" +
                                                    "  <frequency> : The frequency on which the virtual receiver is listening ({0}-{1}).",
                ["cmd_rftool.listeners.del_error"] = "Currently no listeners on frequency {0} configured!",
                ["cmd_rftool.listeners.del_success"] = "Removed listener on frequency {0}",
                ["cmd_rftool.listeners.list_error"] = "Currently no listeners configured",
                ["cmd_rftool.listeners.list_success"] = "The following {0} listener(s) is/are currently configured (Frequency | Log message | Block delay):",
                ["cmd_rftool.listeners.set_interval_help"] = "Usage:\r\n" +
                                                             "  rftool.listeners.set_interval <interval>\r\n" +
                                                             "\r\n" +
                                                             "Description:\r\n" +
                                                             "  This command changes the delay in which this plugin will check for broadcasts\r\n" +
                                                             "  to <interval> seconds. A check whether one of the virtual receivers should be\r\n" +
                                                             "  triggered is only carried out once in this interval. This will also control\r\n" +
                                                             "  the final value of the block delay of a triggered receiver. As an example, if\r\n" +
                                                             "  interval is 5 and block delay is 2, a triggered receiver will be silet for the\r\n" +
                                                             "  next 5 * 2 = 10 seconds. See also the help message of the command rftool.listeners.add\r\n" +
                                                             "\r\n" +
                                                             "Options:\r\n" +
                                                             "  <interval> : Interval in seconds (>0).",
                ["cmd_rftool.listeners.set_interval_error"] = "Current listeners interval is already set to {0} second(s)",
                ["cmd_rftool.listeners.set_interval_success"] = "Updated listeners interval to {0} second(s)",
                ["cmd_rftool.listeners.get_interval_success"] = "Current listeners interval is set to {0} second(s)",
                ["cmd_rftool.debug_enabled"] = "Internal debugging has been enabled",
                ["cmd_rftool.debug_disabled"] = "Internal debugging has been disabled",
                ["errmsg_freq_invalid"] = "Invalid frequency specified!",
                ["errmsg_freq_bounds"] = "Specified frequency is out of bounds!",
                ["errmsg_interval_invalid"] = "Invalid interval value specified!",
                ["debug_checklisteners"] = "Checking listeners",
                ["debug_listenerblocked"] = "Listener on frequency {0} currently blocked",
            }, this);
        }

        #endregion

        #region Console Commands
        /*
         * Print available commands and a short description
         */
        [Command("rftool")]
        void RfToolHelp(IPlayer player, string command, string[] args)
        {
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool_help", this, player.Id), Version));
        }

        /*
         * List all registered listeners / broadcasters
         */
        [Command("rftool.inspect"), Permission("rftool.use")]
        void RfToolInspect(IPlayer player, string command, string[] args)
        {
            for (int cur_freq = frequency_min; cur_freq <= frequency_max; cur_freq++)
            {
                var listeners = RFManager.GetListenList(cur_freq);
                if(listeners.Count>0)
                {
                    this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.inspect_listeners", this, player.Id), listeners.Count, cur_freq));
                }
            }
            for (int cur_freq = frequency_min; cur_freq <= frequency_max; cur_freq++)
            {
                var broadcasters = RFManager.GetBroadcasterList(cur_freq);
                if (broadcasters.Count > 0)
                {
                    this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.inspect_broadcaster", this, player.Id), broadcasters.Count, cur_freq));
                }
            }
        }

        /*
         * Enable all RF listeners on a given frequency
         */
        [Command("rftool.enable"), Permission("rftool.use")]
        void RfToolEnable(IPlayer player, string command, string[] args)
        {
            int frequency = 0;

            // Check if player specified a frequency
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.enable_help", this, player.Id), this.frequency_min, this.frequency_max));
                return;
            }

            // Get & check frequency
            frequency = this.GetFrequency(player, args);
            if (frequency == 0) return;

            // Get all listeners for given frequency and enable them
            var listeners = RFManager.GetListenList(frequency);
            for (int i=0; i < listeners.Count; i++)
            {
                listeners[i].RFSignalUpdate(true);
            }
            
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.enable_success", this, player.Id), listeners.Count, frequency));
        }

        /*
         * Disable all RF listeners on a given frequency
         */
        [Command("rftool.disable"), Permission("rftool.use")]
        void RfToolDisable(IPlayer player, string command, string[] args)
        {
            int frequency = 0;

            // Check if player specified a frequency
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.disable_help", this, player.Id), this.frequency_min, this.frequency_max));
                return;
            }

            // Get & check frequency
            frequency = this.GetFrequency(player, args);
            if (frequency == 0) return;

            // Get all listeners for given frequency and disable them
            var listeners = RFManager.GetListenList(frequency);
            for (int i = 0; i < listeners.Count; i++)
            {
                listeners[i].RFSignalUpdate(false);
            }

            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.disable_success", this, player.Id), listeners.Count, frequency));
        }

        /*
         * Start listening on specific frequency and log broadcasts
         */
        [Command("rftool.listeners.add"), Permission("rftool.use")]
        void RfToolListenersAdd(IPlayer player, string command, string[] args)
        {
            int frequency=0;
            int block_delay = 0;

            // Check command line args
            if (args.Length == 0 || args.Length > 3)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.add_help", this, player.Id), this.frequency_min, this.frequency_max));
                return;
            }
            if (!int.TryParse(args[0], out frequency))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_invalid", this, player.Id)));
                return;
            }
            if (args.Length == 3 && !int.TryParse(args[2], out block_delay))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_invalid", this, player.Id)));
                return;
            }
            if (frequency < frequency_min || frequency > frequency_max)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_bounds", this, player.Id)));
                return;
            }

            // Make sure we are not already listening on that frequency
            if(IsListenerConfigured(frequency))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.add_error", this, player.Id), frequency));
                return;
            }

            // Add and save listener for given frequency
            this.config_data.configured_listeners.Add(new ListenerData(frequency, args[1], block_delay));
            Config.WriteObject<RfToolConfig>(this.config_data, true);
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.add_success", this, player.Id), frequency, block_delay, args[1]));
        }

        /*
         * Stop listening on specific frequency and log broadcasts
         */
        [Command("rftool.listeners.del"), Permission("rftool.use")]
        void RfToolListenersDel(IPlayer player, string command, string[] args)
        {
            int frequency = 0;

            // Check command line args
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.del_help", this, player.Id), this.frequency_min, this.frequency_max));
                return;
            }
            if (!int.TryParse(args[0], out frequency))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_invalid", this, player.Id)));
                return;
            }
            if (frequency < frequency_min || frequency > frequency_max)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_bounds", this, player.Id)));
                return;
            }

            // Make sure we are listening on that frequency
            if (!IsListenerConfigured(frequency))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.del_error", this, player.Id), frequency));
                return;
            }

            // Remove listener for given frequency
            for (int i = 0; i < this.config_data.configured_listeners.Count; i++)
            {
                if (this.config_data.configured_listeners[i].GetFrequency() == frequency)
                {
                    this.config_data.configured_listeners.RemoveAt(i);
                    Config.WriteObject<RfToolConfig>(this.config_data, true);
                    this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.del_success", this, player.Id), frequency));
                    break;
                }
            }
        }

        /*
         * List all configured listeners
         */
        [Command("rftool.listeners.list"), Permission("rftool.use")]
        void RfToolListenersList(IPlayer player, string command, string[] args)
        {
            // Make sure there are listeners configured
            if(this.config_data.configured_listeners.Count==0)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.list_error", this, player.Id)));
                return;
            }

            // Show a list of configured listeners
            string reply=string.Format(lang.GetMessage("cmd_rftool.listeners.list_success", this, player.Id), this.config_data.configured_listeners.Count);
            for (int i = 0; i < this.config_data.configured_listeners.Count; i++)
            {
                reply += "\r\n" + this.config_data.configured_listeners[i].GetFrequency().ToString() + " | '" + this.config_data.configured_listeners[i].GetMessage() + "' | " + this.config_data.configured_listeners[i].GetBlockDelay().ToString();
            }
            this.ReplyToPlayer(player, reply);
        }

        /*
         * Change interval time of listener loop
         */
        [Command("rftool.listeners.set_interval"), Permission("rftool.use")]
        void RfToolListenersSetInterval(IPlayer player, string command, string[] args)
        {
            float interval = 0f;

            // Check command line args
            if (args.Length != 1)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.set_interval_help", this, player.Id)));
                return;
            }
            if (!float.TryParse(args[0], out interval) || interval == 0f)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_interval_invalid", this, player.Id)));
                return;
            }

            // Make sure player actually specified a new value
            if(this.config_data.tick_interval == interval)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.set_interval_error", this, player.Id), this.config_data.tick_interval));
                return;
            }

            // Stop current timer loop
            this.listener_timer.Destroy();

            // Reset current block delay
            this.ResetListenersCurBlockDelay();

            // Change interval and update config file
            this.config_data.tick_interval = interval;
            Config.WriteObject<RfToolConfig>(this.config_data);

            // Start new timer with new interval
            this.listener_timer = timer.Every(this.config_data.tick_interval, this.CheckListeners);

            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.set_interval_success", this, player.Id), interval));
        }

        /*
         * Get interval time of listener loop
         */
        [Command("rftool.listeners.get_interval"), Permission("rftool.use")]
        void RfToolListenersGetInterval(IPlayer player, string command, string[] args)
        {
            this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.listeners.get_interval_success", this, player.Id), this.config_data.tick_interval));
        }

        [Command("rftool.debug"), Permission("rftool.use")]
        void RfToolDebug(IPlayer player, string command, string[] args)
        {
            // Toggle debugging
            this.config_data.debug = !this.config_data.debug;

            if(this.config_data.debug)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.debug_enabled", this, player.Id)));
            }
            else
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("cmd_rftool.debug_disabled", this, player.Id)));
            }
        }
        #endregion

        #region Listener Callback
        /*
         * Callback function getting called by the listener loop timer
         */
        private void CheckListeners()
        {
            this.LogDebug(lang.GetMessage("debug_checklisteners", this, ""));

            // Iterate over every configured listener
            for (int i = 0; i < this.config_data.configured_listeners.Count; i++)
            {
                // Make sure listener is currently not blocked
                if (!this.config_data.configured_listeners[i].IsBlocked())
                {
                    // Check if broadcasters on the listeners configured frequency are currently active
                    var broadcasters = RFManager.GetBroadcasterList(this.config_data.configured_listeners[i].GetFrequency());
                    if (broadcasters.Count > 0)
                    {
                        this.Log(this.config_data.configured_listeners[i].GetMessage());
                        // Enable blocking for next loop(s) if configured
                        this.config_data.configured_listeners[i].EnableBlocking();
                    }
                }
                else
                {
                    this.LogDebug(string.Format(lang.GetMessage("debug_listenerblocked", this, ""), this.config_data.configured_listeners[i].GetFrequency()));
                }
            }
        }
        #endregion

        #region Helper Functions
        /*
         * Helper function to parse and check a given frequency
         */
        private int GetFrequency(IPlayer player, string[] args)
        {
            // Check if player specified a correct frequency
            int frequency;
            if (!int.TryParse(args[0], out frequency))
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_invalid", this, player.Id)));
                return 0;
            }

            // Make sure frequency is valid
            if (frequency < frequency_min || frequency > frequency_max)
            {
                this.ReplyToPlayer(player, string.Format(lang.GetMessage("errmsg_freq_bounds", this, player.Id)));
                return 0;
            }

            return frequency;
        }

        /*
         * Helper function to check if a listener for a certain frequency has already been configured
         */
        private bool IsListenerConfigured(int frequency)
        {
            for(int i = 0; i < this.config_data.configured_listeners.Count; i++)
            {
                if (this.config_data.configured_listeners[i].GetFrequency() == frequency) return true;
            }
            return false;
        }

        /*
         * Helper function to reset all configured listeners current block delay
         */
        private void ResetListenersCurBlockDelay()
        {
            // All configured listeners shall be unblocked
            for (int i = 0; i < this.config_data.configured_listeners.Count; i++)
            {
                this.config_data.configured_listeners[i].DisableBlocking();
            }
        }

        /*
         * Helper functions to send messages to players / console
         */
        private void ReplyToPlayer(IPlayer player, string msg)
        {
            player.Reply(msg);
        }
        private void Log(string msg)
        {
            Puts(msg);
        }
        private void LogDebug(string msg)
        {
            if(this.config_data.debug)
            {
                this.Log("DEBUG :: " + msg);
            }
        }
        #endregion
    }
}
