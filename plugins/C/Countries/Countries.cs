//#define DEBUG

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Random = System.Random;

// TODO: Implement local database support
// - https://lite.ip2location.com/database/ip-country
// - https://dev.maxmind.com/geoip/geoip2/geolite2/

namespace Oxide.Plugins
{
    [Info("Countries", "Wulf", "2.0.1")]
    [Description("Limits players connecting from certain countries")]
    class Countries : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            /*[JsonProperty("Use free IP lookup service providers (true/false)")] // TODO: Implement this
            public bool UseFreeServices = true;*/

            /*[JsonProperty("Use local IP database (true/false)")] // TODO: Implement this
            public bool UseLocalDatabase = false;*/

            /*[JsonProperty("Use paid IP lookup service provider (true/false)")] // TODO: Implement this
            public bool UsePaidService = true;*/

            /*[JsonProperty("Paid IP lookup service provider (domain name)")] // TODO: Implement this
            public string PaidProvider = "YOUR_PAID_PROVIDER";*/

            // TODO: Add option for custom provider URL and JSON response field

            [JsonProperty("API key for paid IP lookup service (if applicable)")]
            public string ApiKey = "YOUR_API_KEY";

            [JsonProperty("Ban player if country is blacklisted (true/false)")]
            public bool BanPlayer = false;

            [JsonProperty("Only allow players from server's country (true/false)")]
            public bool NativesOnly = false;

            [JsonProperty("Cache responses from IP lookup service provider (true/false)")]
            public bool CacheResponses = true;

            [JsonProperty("Number of retries on IP lookup fail (0 to disable)")]
            public int RetriesOnFail = 3;

            [JsonProperty("Log connections from players (true/false)")]
            public bool LogConnections = false;

            [JsonProperty("Use country code list as a whitelist (true/false)")]
            public bool IsWhitelist = false;

            [JsonProperty("List of two-digit countries codes to check", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CountryList = new List<string> { "CN", "JP", "HK", "KR", "RU", "VN" };

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAdmin"] = "countries",
                ["CountryAdded"] = "Country '{0}' was added to the country list",
                ["CountryListed"] = "Country '{0}' is already in the country list",
                ["CountryNotListed"] = "Country '{0}' is not in the country list",
                ["CountryRemoved"] = "Country '{0}' was removed from the country list",
                ["InvalidCountry"] = "{0} is not a valid two-digit country code",
                ["NoPlayersFound"] = "No online players found with name or ID '{0}'",
                ["PlayerCheckFailed"] = "Getting country for {0} ({1}) at {2} failed! ({3})",
                ["PlayerConnected"] = "{0} ({1}) connected from {2}",
                ["PlayerExcluded"] = "{0} ({1}) is excluded from country checking",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayerRejected"] = "This server does not allow players from {0}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["Unknown"] = "unknown",
                ["UsageAddRemove"] = "Usage: {0} add/remove <word or phrase> - add to or remove from country list",
                ["UsageCheck"] = "Usage: {0} <player name or ID> - check if player is from a listed country",
                ["UsageList"] = "Usage: {0} list - list all countries currently on the country list"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private Dictionary<string, string> apiLimits = new Dictionary<string, string>();
        private Dictionary<string, string> ipCache = new Dictionary<string, string>();
        private readonly List<Provider> providersFree = new List<Provider>();
        private readonly List<Provider> providersPaid = new List<Provider>();
        private static readonly Random random = new Random();

        private const string permAdmin = "countries.admin";
        private const string permExclude = "countries.exclude";

        private string serverCountry;

        public class Provider
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string Field { get; set; }
            public string ApiKey { get; set; }
            public int ApiLimit { get; set; }
            public string LimitPeriod { get; set; }

            public Provider(string name, string url, string field, string apiKey = "", int apiLimit = 0, string limitPeriod = "month")
            {
                Name = name;
                Url = url;
                Field = field;
                ApiKey = apiKey;
                ApiLimit = apiLimit;
                LimitPeriod = limitPeriod;
            }
        }

        private void RegisterProviders()
        {
            // TODO: Test each provider individually to confirm working

            #region Free Providers

            providersFree.Add(new Provider("dp-ip.com", "http://api.db-ip.com/v2/free/{ip}", "countryCode"));
            /*
             *   Free option, but unaware of request limit // TODO: Figure out free request limit
             */

            providersFree.Add(new Provider("freegeoip.app", "https://freegeoip.app/json/{ip}", "country_code", string.Empty, 15000, "hour"));
            /*
             *   Limit of 15,000 free requests per hour
             */

            providersFree.Add(new Provider("geolocation-db.com", "http://geolocation-db.com/json/{ip}", "country_code"));
            /*
             *   Free, but unaware of request limit // TODO: Figure out request limit
             */

            providersFree.Add(new Provider("ipgeolocationapi.com", "https://api.ipgeolocationapi.com/geolocate/{ip}", "alpha2"));
            /*
             *   Free, but unsure of request limit; caching recommended (can self-host)
             */

            //providersFree.Add(new Provider("ipinfodb.com", "http://api.ipinfodb.com/v3/ip-country/?format=json&ip={ip}&key={key}", "countryCode", config.ApiKey, 2, "second"));
            /*
             *   Limit of 2 free requests per second
             *   Get free API key: https://ipinfodb.com/register (required)
             */

            providersFree.Add(new Provider("ipwhois.io", "http://free.ipwhois.io/json/{ip}", "country_code", string.Empty, 10000, "month"));
            /*
             *   Limit of 10,000 free requests per month
             */

            #endregion Free Providers

            #region Free/Paid Providers

            //providers.Add(new Provider("extreme-ip-lookup.com", "http://extreme-ip-lookup.com/json/{ip}", "countryCode", config.ApiKey, 20, "minute")); // TODO: Figure out how API key is used
            /*
             *   Limit of 20 free requests per minute (strict); $20-25/mo for Pro package
             */

            //providers.Add(new Provider("ip-api.com", "http://ip-api.com/line/{ip}?fields=countryCode", "countryCode", config.ApiKey, 45, "minute")); // TODO: Figure out how API key is used
            /*
             *   Limit of 45 free requests per minute; unlimited paid option available for ~$15/mo: https://members.ip-api.com/
             *   Get paid API key: ??
             */

            //providers.Add(new Provider("ipapi.co", "https://ipapi.co/{ip}/json/", "country", config.ApiKey, 1000, "day")); // TODO: Figure out how API key is used
            /*
             *   Limit of 30,000 free request per month, 1k requests per day; paid options available: https://ipapi.co/#pricing
             *   Get paid API key: ??
             */

            //providers.Add(new Provider("ipdata.co", "https://api.ipdata.co/{ip}?api-key={key}", "country_code", config.ApiKey, 1500, "day"));
            /*
             *   Limit of 1,500 free requests per day; paid options available: https://ipdata.co/pricing.html
             *   Get free API key: https://ipdata.co/sign-up.html (required)
             */

            //providers.Add(new Provider("ipfinder.io", "https://api.ipfinder.io/v1/{ip}?token={key}", "country_code", config.ApiKey, 4000, "day"));
            /*
             *   Limit of 4,000 free requests per day; paid options available: https://ipfinder.io/pricing
             *   Get free API key: https://ipfinder.io/auth/signup (required)
             */

            //providers.Add(new Provider("ipgeolocation.io", "https://api.ipgeolocation.io/ipgeo?ip={ip}&apiKey={key}", "country_code2", config.ApiKey, 1000, "day"));
            /*
             *   Limit of 30,000 free requests per month, 1,000 requests per day; paid options available: https://ipgeolocation.io/pricing.html
             *   Get free API key: https://ipgeolocation.io/signup.html (required)
             */

            //providers.Add(new Provider("dp-ip.com", "http://api.db-ip.com/v2/{key}/{ip}", "countryCode", config.ApiKey));
            /*
             *   Paid option; plans start at ~$18/mo for 10k-50k per day: https://db-ip.com/api/pricing/
             *   Get paid API key: https://db-ip.com/api/pricing/basic (required)
             */

            //providers.Add(new Provider("ipstack.com", "https://api.ipstack.com/{ip}?fields=country_code&access_key={key}", "country_code", config.ApiKey, 10000, "month"));
            /*
             *   Limit of 10,000 free requests per month; paid options available: https://ipstack.com/product
             *   Get free API key: https://ipstack.com/signup/free (required)
             */

            //providers.Add(new Provider("ipapi.com", "http://api.ipapi.com/{ip}?fields=country_code&access_key={key}", "country_code", config.ApiKey, 10000, "month"));
            /*
             *   Limit of 10,000 free requests per month; paid options available: https://ipapi.com/product
             *   Get free API key: https://ipapi.com/signup/free (required)
             */

            #endregion Free/Paid Providers

            #region WIP Providers

            //providers.Add(new Provider("iphub.info", "http://v2.api.iphub.info/ip/{ip}")); // TODO: Figure out missing information
            /*
             *   Limit of ??; API key required
             *   'X-Key' header
             *   ["??"]
             */

            //providers.Add(new Provider("smartip.io", "https://api.smartip.io/{ip}?api_key={key}", "country,country-iso-code", config.ApiKey, 250000, "day")); // TODO: Handle nested fields
            /*
             *   Limit of 250,000 free requests per day; paid options available: https://smartip.io/#pricing-section
             *   Get free API key: https://smartip.io/account/register/?returnUrl=/dashboard (required)
             */

            //providers.Add(new Provider("snoopi.io", "http://api.snoopi.io/v1/?api_key={key}&user_ip_address={ip}")); // TODO: Figure out missing information
            /*
             *   Limit of ??; API key optional: ??
             *   Get API key: ??
             *   ["??"]
             */

            /*
             *   ip-geolocation.whoisxmlapi.com
             *   Limit of 1,000 free requests per month; paid options available: https://ip-geolocation.whoisxmlapi.com/api/pricing
             *   https://ip-geolocation.whoisxmlapi.com/api/v1?ipAddress={ip}&apiKey={apiKey}
             *   Get free API key: https://ip-geolocation.whoisxmlapi.com/api/signup
             *   ["location"]["country"] // TODO: Handle nested fields
             */

            /*
             *   ipinfo.io
             *   Limit of 50,000 free requests per month; paid options available: https://ipinfo.io/pricing
             *   https://ipinfo.io/{ip}?token={apiKey}
             *   Get free API key: https://ipinfo.io/signup
             *   ["country"]
             */

            /*
             *   hostip.info
             *   http://api.hostip.info/get_json.php?ip={ip}
             *   Limit of ??
             *   ["country_code"]
             */

            /*
             *   webcargo.io
             *   Limit of 500 free requests per month; paid options available: https://webcargo.io/pricing
             *   https://api.webcargo.io/ip?ip_address={ip}&key={apiKey}
             *   Get free API key: https://webcargo.io/register
             *   ["country_code"]
             */

            /*
             *   ipwhois.io
             *   Limit of ?? requests per month: https://ipwhois.io/pricing
             *   https://pro.ipwhois.io/json/{ip}?key={apiKey}
             *   ["country_code"]
             */

            /*
             *   ip-api.io // TODO: Figure out free requests limit
             *   Limit of 200 free requests per ?? without key, or 12,000 free requests per month with key; paid options available: https://ip-api.io/#pricing
             *   https://ip-api.io/json/{ip} / https://ip-api.io/json/{ip}?api_key={apiKey}
             *   ["country_code"]
             */

            /*
             *   keycdn.com // TODO: Handle nested fields
             *   Limit of 3 free requests per second
             *   https://tools.keycdn.com/geo.json?host={ip}
             *   ["data"]["geo"]["country_code"]
             */

            /*
             *   iplocate.io
             *   Limit of 1,000 free requests per day; paid options available: https://www.iplocate.io/pricing
             *   https://www.iplocate.io/api/lookup/{ip} / https://www.iplocate.io/api/lookup/{ip}?apikey={apiKey}
             *   ["country_code"]
             */

            /*
             *   ip2location.com
             *   Limt of 50 free requests per day with "demo" key, 200 free requests per day with key; paid options available: https://www.ip2location.com/buy-online#web-service
             *   https://api.ip2location.com/v2/?ip={ip}&key=demo / https://api.ip2location.com/v2/?ip={ip}&key={apiKey}
             *   ["country_code"]
             */

            /*
             *   maxmind.com
             *   https://dev.maxmind.com/geoip/geoip2/web-services/#Client_APIs
             *   https://geoip.maxmind.com/geoip/v2.1/country/{ip_address}
             *   ["country"]["iso_code"]
             */

            /*
             *   ip.city
             *   Limit of 100 free requests per day with key; paid options available: https://ip.city/users.php
             *   https://ip.city/api.php?ip=[IP]&key=[YOUR_API_KEY]
             *   ["countryCode"]
             */

            /*
             *   iptoasn.com
             *   Free, but unaware of request limit // TODO: Figure out request limit
             *   https://api.iptoasn.com/v1/as/ip/{ip}
             *   Header: 'Accept: application/json'
             *   ["as_country_code"]
             */

            #endregion WIP Providers
        }

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandAdmin));

            // Register new and migrate old permission(s)
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permExclude, this);
            MigratePermission("countryblock.bypass", permExclude);

            // Check for and remove duplicate country codes from list
            List<string> origCountryList = config.CountryList;
            config.CountryList = config.CountryList.Distinct().ToList();

            // Check for and remove any invalid country codes from list
            foreach (string country in origCountryList)
            {
                if (!IsCountryCode(country))
                {
                    config.CountryList.Remove(country);
                }
            }

            // Save updated country list, if changed
            if (!config.CountryList.SequenceEqual(origCountryList))
            {
                LogWarning("Updated country list to remove invalid/duplicate entries");
                SaveConfig();
            }

            // Load stored data
            //apiLimits = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>($"{Name}_Limits"); // TODO: Implement
            ipCache = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>($"{Name}_Cache");

            RegisterProviders();
        }

        private void OnServerInitialized()
        {
            // Look up country for server
            serverCountry = GetCachedCountry(server.Address.ToString());
            if (string.IsNullOrEmpty(serverCountry))
            {
                Action<string, int> callback = (string response, int code) =>
                {
                    // TODO: Get and output error code

                    if (!string.IsNullOrEmpty(response))
                    {
                        serverCountry = response ?? GetLang("Unknown");
                    }
                };
                IpLookup(server.Address.ToString(), callback);
            }
        }

        #endregion Initialization

        #region Commands

        private void CommandAdmin(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            // TODO: Show command usage if subcommands do not match

            // TODO: Show command usage if arg lenth is invalid

            string countryInput = string.Empty;
            switch (args[0].ToLower())
            {
                case "+":
                case "add":
                    {
                        if (args.Length != 2)
                        {
                            Message(player, "UsageAdmin", command);
                            return;
                        }

                        countryInput = args[1].ToUpper();
                        if (!IsCountryCode(countryInput))
                        {
                            Message(player, "InvalidCountry", countryInput);
                            return;
                        }

                        if (config.CountryList.Contains(countryInput))
                        {
                            Message(player, "CountryListed", countryInput);
                            break;
                        }

                        config.CountryList.Add(countryInput);
                        SaveConfig();

                        Message(player, "CountryAdded", countryInput);
                        break;
                    }

                case "-":
                case "remove":
                    {
                        if (args.Length != 2)
                        {
                            Message(player, "UsageAdmin", command);
                            return;
                        }

                        countryInput = args[1].ToUpper();
                        if (!IsCountryCode(countryInput))
                        {
                            Message(player, "InvalidCountry", countryInput);
                            return;
                        }

                        if (!config.CountryList.Contains(countryInput))
                        {
                            Message(player, "CountryNotListed", countryInput);
                            break;
                        }

                        config.CountryList.Remove(countryInput);
                        SaveConfig();

                        Message(player, "CountryRemoved", countryInput);
                        break;
                    }

                case "?":
                case "check":
                    {
                        if (args.Length < 1)
                        {
                            Message(player, "UsageCheck", command);
                            return;
                        }

                        IPlayer target = FindPlayer(args[1], player);
                        if (target == null)
                        {
                            return;
                        }

                        Action<string, int> callback = (string response, int code) =>
                        {
                            if (string.IsNullOrEmpty(response))
                            {
                                Message(player, "PlayerCheckFailed", target.Name, target.Id, target.Address, code);
                            }
                            else
                            {
                                if (!IsCountryAllowed(response))
                                {
                                    RejectPlayer(player, response);
                                }
                            }
                        };
                        IpLookup(player.Address, callback);
                        break;
                    }

                case "list":
                    Message(player, string.Join(", ", config.CountryList.Cast<string>().ToArray()));
                    break;

                default:
                    break;
            }
        }

        #endregion Commands

        #region IP Lookup

        private void IpLookup(string ip, Action<string, int> callback = null)
        {
            // TODO: Add support for local database using Geo IP plugin API

            // TODO: Check to make sure config.ApiKey is not set to "YOUR_API_KEY", is empty, or is null

            // TODO: Implement config.UseFreeServices option for only using free providers (no API key ones)

            Provider provider = providersFree[random.Next(providersFree.Count)]; // TODO: Select another provider if at limit
            string url = provider.Url.ToLower().Replace("{ip}", ip).Replace("{key}", config.ApiKey); // TODO: Handle this better

            webrequest.Enqueue(url, null, (code, response) =>
            {
#if DEBUG
                LogWarning($"DEBUG: {url}");
                LogWarning($"DEBUG: {response}");
#endif
                int retries = 0;
                string country = string.Empty;

                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    if (retries == config.RetriesOnFail)
                    {
                        callback?.Invoke(country, code); // TODO: Exclude provider that errored from next check
                        return;
                    }

                    retries++;
                    IpLookup(ip, callback);
                    return;
                }

                try
                {
                    JObject json = JObject.Parse(response);
                    // TODO: Split provider.Field into multiple fields if it contains a comma
                    if (json[provider.Field] != null && ((string)json[provider.Field]).Length == 2)
                    {
                        country = ((string)json[provider.Field]).Trim();
                    }
                }
                catch
                {
                    // Ignored
                }
                callback?.Invoke(country, code);

                // Store country and IP address in cache
                if (config.CacheResponses && !string.IsNullOrEmpty(country))
                {
                    string wildcardIp = ToWildcardIp(ip);
                    if (!IsLocalIp(ip) && !ipCache.ContainsKey(wildcardIp))
                    {
                        ipCache.Add(wildcardIp, country);
                        LogWarning($"Wildcard IP {wildcardIp} added to cache for {country}");
                        Interface.Oxide.DataFileSystem.WriteObject($"{Name}_Cache", ipCache);
                    }
                }

                // TODO: Increment and store usage toward API request limit for provider
                //Interface.Oxide.DataFileSystem.WriteObject($"{Name}_Limits", apiLimits);
#if DEBUG
                LogWarning($"DEBUG: Country response for {ip}: {(!string.IsNullOrEmpty(country) ? country : GetLang("Unknown"))}");
#endif
            }, this, RequestMethod.GET, null, 10f); // TODO: Add support for headers, and low timeout with retry
        }

        private void IsPlayerBlocked(IPlayer player)
        {
            string ip = IpAddress(player.Address);
#if DEBUG
            LogWarning($"DEBUG: Local: {IsLocalIp(ip)}, Perm: {player.HasPermission(permExclude)}");
#endif
            if (IsLocalIp(ip) || player.HasPermission(permExclude))
            {
                return;
            }

            if (config.CacheResponses)
            {
                string country = GetCachedCountry(ip);
                if (!string.IsNullOrEmpty(country) && !IsCountryAllowed(country))
                {
                    RejectPlayer(player, country);
                    return;
                }
            }

            Action<string, int> callback = (string response, int code) =>
            {
                if (string.IsNullOrEmpty(response))
                {
                    LogWarning(GetLang("PlayerCheckFailed", null, player.Name, player.Id, ip, code));
                }
                else
                {
                    if (config.LogConnections)
                    {
                        Log(GetLang("PlayerConnected", null, player.Name, player.Id, response));
                    }

                    if (!IsCountryAllowed(response))
                    {
                        RejectPlayer(player, response);
                    }
                }
            };
            IpLookup(ip, callback);
        }

        #endregion IP Lookup

        #region Player Rejection

        private object CanUserLogin(string name, string id, string ip)
        {
            if (IsLocalIp(ip) || permission.UserHasPermission(id, permExclude))
            {
                LogWarning(GetLang("PlayerExcluded", null, name, id));
                return null;
            }

            if (config.CacheResponses)
            {
                string country = GetCachedCountry(ip);
                if (!string.IsNullOrEmpty(country))
                {
                    if (config.LogConnections)
                    {
                        Log(GetLang("PlayerConnected", null, name, id, country));
                    }

                    if (!IsCountryAllowed(country))
                    {
                        return GetLang("PlayerRejected", id, country);
                    }
                }
            }

            return null;
        }

        private void OnUserConnected(IPlayer player) => IsPlayerBlocked(player);

        private void RejectPlayer(IPlayer player, string country)
        {
            if (config.BanPlayer)
            {
                player.Ban(GetLang("PlayerRejected", player.Id, country), TimeSpan.Zero); // TODO: Implement ban time and option when available
            }
            else
            {
                player.Kick(GetLang("PlayerRejected", player.Id, country));
            }
        }

        #endregion Player Rejection

        #region Helpers

        private IPlayer FindPlayer(string nameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.Connected.Where(p => p.Name == nameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string GetCachedCountry(string ip)
        {
            string wildcardIp = ToWildcardIp(ip);
            if (ipCache.ContainsKey(wildcardIp))
            {
                return ipCache[wildcardIp];
            }

            return string.Empty;
        }

        private static string IpAddress(string ip)
        {
#if DEBUG
            return "8.8.8.8"; // US
#else
            return Regex.Replace(ip, @":{1}[0-9]{1}\d*", "");
#endif
        }

        private bool IsCountryAllowed(string country)
        {
            bool countryListed = config.CountryList.Contains(country);
            if (!config.IsWhitelist && countryListed || config.IsWhitelist && !countryListed || config.NativesOnly && !serverCountry.Equals(country))
            {
                return false;
            }
        
            return true;
        }

        private bool IsCountryCode(string countryCode)
        {
            return CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(culture => new RegionInfo(culture.LCID))
                        .Any(region => region.TwoLetterISORegionName == countryCode);
        }

        private static bool IsLocalIp(string ipAddress)
        {
            string[] split = ipAddress.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            int[] ip = new[] { int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]) };
            return ip[0] == 10 || ip[0] == 127 || (ip[0] == 192 && ip[1] == 168) || (ip[0] == 172 && (ip[1] >= 16 && ip[1] <= 31));
        }

        public static string ToWildcardIp(string ip)
        {
            return ip.Substring(0, ip.LastIndexOf(".")) + ".*";
        }

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string langKey, params object[] args)
        {
            player.Reply(GetLang(langKey, player.Id, args));
        }

        private void MigratePermission(string oldPerm, string newPerm)
        {
            foreach (IPlayer player in players.All)
            {
                if (player.HasPermission(oldPerm))
                {
                    permission.GrantUserPermission(player.Id, newPerm, null);
                    permission.RevokeUserPermission(player.Id, oldPerm);
                }
            }

            foreach (string group in permission.GetGroups())
            {
                if (permission.GroupHasPermission(group, oldPerm))
                {
                    permission.GrantGroupPermission(group, newPerm, null);
                    permission.RevokeGroupPermission(group, oldPerm);
                }
            }
        }

        #endregion Helpers
    }
}