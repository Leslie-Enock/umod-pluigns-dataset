using System;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Crafting Store", "CraftingStore", "0.1.5")]
    [Description("Checks the CraftingStore donation platform for new payments and executes the commands that have been set.")]

    class CraftingStore : CovalencePlugin
    {
        private string baseUrl = "https://api.craftingstore.net/v4/";
        private string baseUrlAlternative = "https://api-fallback.craftingstore.net/v4/";
		private bool useAlternativeBaseUrl = false;

        private string apiToken = "";

        void OnServerInitialized()
        {
            // Set config
            this.apiToken = Config["token"].ToString();
            int fetchFrequencyMinutes = Int32.Parse(Config["frequencyMinutes"].ToString());

            if (this.apiToken == "Enter your API token") 
			{
                PrintError("Your API token is not yet set, please set the API token in the config and reload the CraftingStore plugin.");
                return;
            }

            if (fetchFrequencyMinutes < 4) 
			{
                // Set to 5 minutes when the frequency is to 4 or lower.
                PrintError("The fetch frequency was set below the minimum (5 minutes). Please change this value in the config, CraftingStore will still work and fetch the commands every 5 minutes.");
                fetchFrequencyMinutes = 5;
            }
			
            // Request commands on load
            RequestCommands();

            // Create timer that will execute the commands
            timer.Repeat(fetchFrequencyMinutes * 60, 0, () => 
			{
                RequestCommands();
            });
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating CraftingStore Config");
	    
            Config["token"] = "Enter your API token";
            Config["frequencyMinutes"] = 5;
        }

        private ApiResponse ParseResponse(string response)
        {
            ApiResponse commands = JsonConvert.DeserializeObject<ApiResponse>(response);
            return commands;
        }

        private void GetRequest(string uri, string action)
        {
            // Set the authentication header
            Dictionary<string, string> headers = new Dictionary<string, string> { { "token", this.apiToken } };

            webrequest.Enqueue(getBaseUrl() + uri, null, (code, response) =>
                GetCallback(code, response, action), this, Core.Libraries.RequestMethod.GET, headers);
        }

        private void GetCallback(int code, string response, string action)
        {
            if (response == null || code != 200) 
			{
                if (this.useAlternativeBaseUrl == false) {
                    // Some installations cannot work with our TLS/SSL certificate, use our alternative endpoint.
                    this.useAlternativeBaseUrl = true;
                    RequestCommands();
                    return;
                }
                PrintError("Invalid response returned, please contact us if this error persists.");
				PrintError(response);
                return;
            }

            // Create model from the JSON response.
            ApiResponse parsedResponse = ParseResponse(response);

            // Validate that the request got a success response back, if not, return the message.
            if (!parsedResponse.success) 
			{
                PrintError("Did not receive success status: " + parsedResponse.message);
                return;
            }

            if (action == "queue") 
			{
                this.ProcessQueuedCommands(parsedResponse);
				return;
            }
        }

        private void PostRequest(string uri, string action, string payload)
        {
            // Set the authentication header
            Dictionary<string, string> headers = new Dictionary<string, string> { { "token", this.apiToken } };

            webrequest.Enqueue(getBaseUrl() + uri, payload, (code, response) =>
                PostCallback(code, response, action), this, Core.Libraries.RequestMethod.POST, headers);
        }

        private void PostCallback(int code, string response, string action)
        {
            if (response == null || code != 200) 
			{
                PrintError("Got error: Invalid response returned, please contact us if this error persists.");
                return;
            }

            // Create model from the JSON response.
            ApiResponse parsedResponse = ParseResponse(response);

            // Validate that the request got a success response back, if not, return the message.
            if (!parsedResponse.success) 
			{
                PrintError("Did not receive success status: " + parsedResponse.message);
                return;
            }
        }

        private void ProcessQueuedCommands(ApiResponse parsedResponse)
        {
            QueueResponse[] donations = parsedResponse.result;

            List<int> ids = new List<int>();

            foreach (QueueResponse donation in donations)
            {
                // Add donation to executed list.
                ids.Add(donation.id);

                // Execute commands
                Puts("Executing Command: " + donation.command);
				server.Command(donation.command);
            }

            if (ids.Count > 0) 
			{
                // Mark as complete if there are commands processed
                string serializedIds = JsonConvert.SerializeObject(ids);

                string payload = "removeIds=" + serializedIds;

                PostRequest("queue/markComplete", "markComplete", payload);
            }
        }

        private string getBaseUrl()
        {
            return this.useAlternativeBaseUrl ? this.baseUrlAlternative : this.baseUrl;
        }

        private void RequestCommands()
        {
            GetRequest("queue", "queue");
        }
        
        public class QueueResponse 
		{
            public int id;
            public string command;
            public string packageName;
        }

        public class ApiResponse 
		{
            public int id;
            public bool success;
            public string error;
            public string message;
            public QueueResponse[] result;
        }
    }
}
