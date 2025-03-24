// Reference: System.Drawing
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Graphics = System.Drawing.Graphics;
using System.Text;
using Facepunch;
using Oxide.Core.Libraries.Covalence;
using Encoder = System.Drawing.Imaging.Encoder;

namespace Oxide.Plugins
{
    [Info("Auto Sign Moderation", "Whispers88", "1.1.1")]
    [Description("Uses the Omni AI/Open AI to auto moderate image content")]
    public class AutoSignModeration : CovalencePlugin
    {
        private Dictionary<ulong, float> _signCooldown = new Dictionary<ulong, float>();
        private Queue<SignData> _queuedImages = new Queue<SignData>();
        private Dictionary<ImageKey, SignData> _signsQueuedPool = new Dictionary<ImageKey, SignData>();

        private string permWhitelist = "autosignmoderation.whitelist";

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Image Size 25 - 100%")]
            public float imageSizeReduction = 50;

            [JsonProperty("Image Quality 25 - 100%")]
            public float imageQualityReduction = 75;

            [JsonProperty("Sign Update Cooldown (seconds)")]
            public float signCooldown = 5;

            [JsonProperty("Player Moderated Cooldown (seconds)")]
            public float signModerationCooldown = 300;

            [JsonProperty("Hide signs while being checked")]
            public bool hideSign = true;

            [JsonProperty("Use Temp Loading Image")]
            public bool useTempImage = false;

            [JsonProperty("Temp Loading Image URL:")]
            public string tempModerationImageURL = "https://i.postimg.cc/4NNrqT2x/pngegg-2.png";

            [JsonProperty("Logging Mode Only")]
            public bool loggingMode = false;

            [JsonProperty("Send Player Chat Warnings")]
            public bool chatWarnings = false;

            [JsonProperty("Batch Mode - Disables hiding of signs")]
            public BatchSettings batchSettings = new BatchSettings();

            [JsonProperty("Discord Settings")]
            public DiscordSettings discordSettings = new DiscordSettings();

            [JsonProperty("Moderation API (Free) - Limited Options")]
            public ModerationAPI moderationAPI = new ModerationAPI();

            [JsonProperty("Advance Moderation API (Paid)")]
            public GPTModel gptModel = new GPTModel();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class BatchSettings
        {
            [JsonProperty("Check images in batches (Advance Mode Only)")]
            public bool imagePooling = true;

            [JsonProperty("Batch Image Check Rate (Minutes)")]
            public float imagePoolingRate = 15;

            [JsonProperty("Minimum images to batch check")]
            public float minImagesPooled = 3;

            [JsonProperty("Max checks to bypass minimum images 0 = no bypass")]
            public float maxChecksImagesPooled = 4;
        }

        public class DiscordSettings
        {
            [JsonProperty("Log to Discord")]
            public bool discordLogging = false;

            [JsonProperty("Log moderated Images to Discord (WARNING THIS MAY SEND NSFW CONTENT TO YOUR DISCORD)")]
            public bool discordImageLogging = false;

            [JsonProperty("Discord Webhook")]
            public string DiscordWebhook = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty("Discord Username")]
            public string DiscordUsername = "Sign Moderator";

            [JsonProperty("Server Name")]
            public string ServerName = "";

            [JsonProperty("Avatar URL")]
            public string AvatarUrl = "https://i.ibb.co/sQ10728/Loading-Pls-Wait2.png";
        }

        public class ModerationAPI
        {
            [JsonProperty("Enable")]
            public bool enabled = true;

            [JsonProperty("Open AI Token")]
            public string apiToken = "https://openai.com/index/openai-api/";

            [JsonProperty("Cooldown between API Checks (seconds)")]
            public float apiCooldown = 1;

            [JsonProperty("Block images of harassment")]
            public bool harassment = true;

            [JsonProperty("Block images of harassment/threatening")]
            public bool harassmentThreatening = true;

            [JsonProperty("Block images of sexual")]
            public bool sexual = true;

            [JsonProperty("Block images of hate")]
            public bool hate = true;

            [JsonProperty("Block images of hate/threatening")]
            public bool hateThreatening = true;

            [JsonProperty("Block images of illicit")]
            public bool illicit = true;

            [JsonProperty("Block images of illicit/violent")]
            public bool illicitViolent = true;

            [JsonProperty("Block images of self-harm/intent")]
            public bool selfHarmIntent = true;

            [JsonProperty("Block images of self-harm/instructions")]
            public bool selfHarmInstructions = true;

            [JsonProperty("Block images of self-harm")]
            public bool selfHarm = true;

            [JsonProperty("Block images of sexual/minors")]
            public bool sexualMinors = true;

            [JsonProperty("Block images of violence")]
            public bool violence = true;

            [JsonProperty("Block images of violence/graphic")]
            public bool violenceGraphic = true;
        }

        public class GPTModel
        {
            [JsonProperty("Enable GPT Model (WARNING THIS IS PAID PLEASE READ DOCS)")]
            public bool enabled = false;

            [JsonProperty("Open AI Token")]
            public string apiToken = "https://openai.com/index/openai-api/";

            [JsonProperty("Cooldown between API Checks (seconds)")]
            public float apiCooldown = 1;

            [JsonProperty("Model (Don't change this if you dont know what it is)")]
            public string model = "gpt-4o-mini";

            [JsonProperty("Content to moderate")]
            public string prompt = "Pornography, Hate Speech, Child Exploitation, Racist images signs text or symbols, Words like nigger, symbols which resemble swastikas";
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
                ["WarningMessage"] = "You cannot post explicit content on signs",
                ["CooldownMessage"] = "You need to wait {0} before updating the sign."
            }, this);
        }

        #endregion Localization

        #region Classes
        public class SignData
        {
            public ulong playerId;
            public Signage sign;
            public int textureIndex;
            public uint crc;
            public FileStorage.Type type;
        }

        #region API
        public class Categories
        {
            public bool harassment;
            public bool harassmentthreatening;
            public bool sexual;
            public bool hate;
            public bool hatethreatening;
            public bool illicit;
            public bool illicitviolent;
            public bool selfharmintent;
            public bool selfharminstructions;
            public bool selfharm;
            public bool sexualminors;
            public bool violence;
            public bool violencegraphic;
        }

        public class CategoryScores
        {
            public double harassment;
            public double harassmentthreatening;
            public double sexual;
            public double hate;
            public double hatethreatening;
            public double illicit;
            public double illicitviolent;
            public double selfharmintent;
            public double selfharminstructions;
            public double selfharm;
            public double sexualminors;
            public double violence;
            public double violencegraphic;
        }

        public class Result
        {
            public bool flagged;
            public Categories categories;
            public CategoryScores category_scores;
        }

        public class OmniDataRoot
        {
            public string id;
            public string model;
            public List<Result> results;
        }

        public class Choice
        {
            public int index;
            public Message message;
            public object logprobs;
            public string finish_reason;
        }

        public class CompletionTokensDetails
        {
            public int reasoning_tokens;
            public int audio_tokens;
            public int accepted_prediction_tokens;
            public int rejected_prediction_tokens;
        }

        public class Message
        {
            public string role;
            public string content;
            public object refusal;
        }

        public class PromptTokensDetails
        {
            public int cached_tokens;
            public int audio_tokens;
        }

        public class GPTRoot
        {
            public string id;
            public string @object;
            public int created;
            public string model;
            public List<Choice> choices;
            //public Usage usage;
            //public string service_tier;
            //public string system_fingerprint;
        }

        public class Usage
        {
            public int prompt_tokens;
            public int completion_tokens;
            public int total_tokens;
            public PromptTokensDetails prompt_tokens_details;
            public CompletionTokensDetails completion_tokens_details;
        }

        #endregion API

        #endregion Classes

        #region Hooks
        public class ImageSize
        {
            public int Width { get; }
            public int Height { get; }

            public ImageSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        public struct ImageKey : IEquatable<ImageKey>
        {
            public ulong NetID;
            public int TextureIndex;

            public bool Equals(ImageKey other)
            {
                return NetID == other.NetID && TextureIndex == other.TextureIndex;
            }

            public override bool Equals(object obj)
            {
                if (obj is ImageKey other)
                {
                    return Equals(other);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(NetID, TextureIndex);
            }
        }

        private ImageCodecInfo _imageCodecInfo = null;
        private EncoderParameters _encoderParams = null;

        private Dictionary<uint, ImageSize> _ImageSizeperAsset = new Dictionary<uint, ImageSize>();
        private void OnServerInitialized()
        {
            if (CheckPooledImagesRun != null) // This is only really needed during testing if static instances persist
            {
                ServerMgr.Instance.StopCoroutine(CheckPooledImagesRun);
                CheckPooledImagesRun = null;
            }
            if (CheckImagesRun != null) // This is only really needed during testing if static instances persist
            {
                ServerMgr.Instance.StopCoroutine(CheckImagesRun);
                CheckImagesRun = null;
            }

            permission.RegisterPermission(permWhitelist, this);

            _stringBuilder = new StringBuilder();

            gptModel = config.gptModel.model;
            prompt = config.gptModel.prompt;

            _ModerationAPIWait = CoroutineEx.waitForSeconds(config.moderationAPI.apiCooldown);
            _GPTModelAPIWait = CoroutineEx.waitForSeconds(config.gptModel.apiCooldown);

            _imageCodecInfo = ImageCodecInfo.GetImageEncoders().FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
            _encoderParams = new EncoderParameters(1);
            _encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)config.imageQualityReduction);

            if (config.imageSizeReduction < 25)
            {
                config.imageSizeReduction = 25;
            }

            if (config.imageQualityReduction < 25)
            {
                config.imageSizeReduction = 25;
            }

            if (config.useTempImage)
            {
                _tempModerationImageURL = config.tempModerationImageURL;
            }
            if (GetLoadingImage != null)
            {
                ServerMgr.Instance.StopCoroutine(GetLoadingImage);
            }

            GetLoadingImage = LoadingImageSetup();
            ServerMgr.Instance.StartCoroutine(GetLoadingImage);

            foreach (var prefab in GameManifest.Current.entities)
            {
                var gamePrefab = GameManager.server.FindPrefab(prefab.ToLower());
                if (gamePrefab == null)
                    continue;

                Signage sign = gamePrefab.GetComponent<Signage>();
                if (sign != null)
                {
                    if (sign.paintableSources?.Length < 1) continue;
                    _ImageSizeperAsset[sign.prefabID] = new ImageSize(sign.paintableSources[0].texWidth, sign.paintableSources[0].texHeight);
                    continue;
                }
                PhotoFrame photoFrame = gamePrefab.GetComponent<PhotoFrame>();
                if (photoFrame != null)
                {
                    _ImageSizeperAsset[photoFrame.prefabID] = new ImageSize(photoFrame.PaintableSource.texWidth, photoFrame.PaintableSource.texHeight);
                    continue;
                }
                NeonSign neonSign = gamePrefab.GetComponent<NeonSign>();
                if (neonSign != null)
                {
                    _ImageSizeperAsset[neonSign.prefabID] = new ImageSize(neonSign.paintableSources[0].texWidth, neonSign.paintableSources[0].texHeight);
                    continue;
                }
                WantedPoster wantedPoster = gamePrefab.GetComponent<WantedPoster>();
                if (wantedPoster != null)
                {
                    _ImageSizeperAsset[wantedPoster.prefabID] = new ImageSize(wantedPoster.TextureSize.x, wantedPoster.TextureSize.y);
                    continue;
                }
                CarvablePumpkin carvablePumpkin = gamePrefab.GetComponent<CarvablePumpkin>();
                if (carvablePumpkin != null)
                {
                    _ImageSizeperAsset[carvablePumpkin.prefabID] = new ImageSize(carvablePumpkin.paintableSources[0].texWidth, carvablePumpkin.paintableSources[0].texHeight);
                    continue;
                }
            }

            if (config.batchSettings.imagePooling)
            {
                if (!config.gptModel.enabled)
                {
                    Puts("You require gpt model to use image pooling. Disabling image pooling");
                    config.batchSettings.imagePooling = false;
                }
                else
                {
                    ServerMgr.Instance.InvokeRepeating(StartPooledImageCheck, 0, config.batchSettings.imagePoolingRate * 60);
                }
            }
        }

        private void Unload()
        {
            if (ServerMgr.Instance.IsInvoking(StartPooledImageCheck))
            {
                ServerMgr.Instance.CancelInvoke(StartPooledImageCheck);
            }
            if (CheckPooledImagesRun != null)
            {
                ServerMgr.Instance.StopCoroutine(CheckPooledImagesRun);
            }
            if (GetLoadingImage != null)
            {
                ServerMgr.Instance.StopCoroutine(GetLoadingImage);
            }
            if (CheckImagesRun != null)
            {
                ServerMgr.Instance.StopCoroutine(CheckImagesRun);
            }

            CheckPooledImagesRun = null;
            GetLoadingImage = null;
            CheckImagesRun = null;

            _signCooldown = null;
            _queuedImages = null;
            _signsQueuedPool = null;
        }

        private void StartPooledImageCheck()
        {
            if (CheckPooledImagesRun != null)
            {
                return;
            }
            CheckPooledImagesRun = CheckPooledImages();
            ServerMgr.Instance.StartCoroutine(CheckPooledImagesRun);
        }

        private StringBuilder _formattedTime = new StringBuilder();
        private object? CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (_signCooldown.TryGetValue(player.userID, out float cooldown) && cooldown > Time.time)
            {
                TimeSpan timeRemaining = TimeSpan.FromSeconds(cooldown - Time.time);
                _formattedTime.Clear();
                if (timeRemaining.Days > 0) _formattedTime.Append(timeRemaining.Days).Append("d ");
                if (timeRemaining.Hours > 0) _formattedTime.Append(timeRemaining.Hours).Append("h");
                if (timeRemaining.Minutes > 0) _formattedTime.Append(timeRemaining.Minutes).Append("m ");
                _formattedTime.Append(timeRemaining.Seconds).Append("s");

                ChatMessage(player.IPlayer, "CooldownMessage", _formattedTime.ToString());
                return false;
            }
            return null;
        }

        void OnSignUpdated(Signage sign, BasePlayer player, int textureIndex)
        {
            if (player == null || sign == null) return;
            if (HasPerm(player.UserIDString, permWhitelist)) return;

            if (config.batchSettings.imagePooling)
            {
                _signsQueuedPool[new ImageKey() { NetID = sign.NetworkID.Value, TextureIndex = textureIndex }] = new SignData { playerId = player.userID, sign = sign, textureIndex = textureIndex, crc = sign.GetContentCRCs[textureIndex], type = sign.FileType };
                return;
            }

            _queuedImages.Enqueue(new SignData { playerId = player.userID, sign = sign, textureIndex = textureIndex, crc = sign.GetContentCRCs[textureIndex], type = sign.FileType });

            if (config.hideSign)
            {
                SetImageToSign(sign, textureIndex, _tempModerationImageCRC);
            }

            _signCooldown[player.userID] = Time.time + config.signCooldown;

            if (CheckImagesRun == null)
            {
                CheckImagesRun = CheckImages();
                ServerMgr.Instance.StartCoroutine(CheckImagesRun);
            }
        }

        #endregion Hooks

        #region Methods
        private void SetImageToSign(Signage sign, int textureIndex, uint crc)
        {
            sign.textureIDs[textureIndex] = crc;
            sign.SendNetworkUpdateImmediate();
        }

        readonly string jsonline1 = "{\"model\": \"omni-moderation-latest\",\"input\": [ { \"type\": \"image_url\",\"image_url\": {\"url\": \"data:image/jpeg;base64,";
        readonly string jsonline2 = "\"}}]}";

        private static StringBuilder _stringBuilder = new StringBuilder();
        private string CreateJson(string base64Input)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(jsonline1);
            _stringBuilder.Append(base64Input);
            _stringBuilder.Append(jsonline2);
            return _stringBuilder.ToString();
        }

        readonly string jsongptline1 = "{\"model\": \"";
        readonly string jsongptline2 = "\",\"store\": false,\"messages\": [{\"role\": \"user\", \"content\": [{\"type\": \"text\", \"text\": \"Answer with only 'yes' or 'no' if the image contains any of the specified categories:";
        readonly string jsongptline3 = "\"},{\"type\": \"image_url\", \"image_url\": {\"url\": \"data:image/jpeg;base64,";
        readonly string jsongptline4 = "\"}}]}]}";
        private string gptModel = "gpt-4o-mini";
        private string prompt = "Racism, Pornographic material, Hate Speech";
        private string CreateGPTJson(string base64Input)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(jsongptline1);
            _stringBuilder.Append(gptModel);
            _stringBuilder.Append(jsongptline2);
            _stringBuilder.Append(prompt);
            _stringBuilder.Append(jsongptline3);
            _stringBuilder.Append(base64Input);
            _stringBuilder.Append(jsongptline4);
            return _stringBuilder.ToString();
        }

        private readonly string _moderationAPI = "https://api.openai.com/v1/moderations";
        private readonly string _gptModelAPI = "https://api.openai.com/v1/chat/completions";

        private static WaitForSeconds _ModerationAPIWait = CoroutineEx.waitForSeconds(5);
        private static WaitForSeconds _GPTModelAPIWait = CoroutineEx.waitForSeconds(5);

        private static IEnumerator CheckImagesRun;
        private IEnumerator CheckImages()
        {
            for (int i = 0; i < _queuedImages.Count; i++)
            {
                SignData signData = _queuedImages.Dequeue();
                if (signData.sign == null || signData.sign.GetContentCRCs.Length < 1 || signData.sign.GetContentCRCs[signData.textureIndex] != signData.crc)
                {
                    continue;
                }
                byte[] array = GetImageBytes(signData);
                if (array == null || array.Length == 0)
                {
                    continue;
                }
                string Base64 = Convert.ToBase64String(array);

                if (config.moderationAPI.enabled)
                {
                    yield return CheckModerationAPI(signData, array);
                }

                if (config.gptModel.enabled)
                {
                    yield return CheckGPTModelAPI(signData, array);
                }
                SetImageToSign(signData.sign, signData.textureIndex, signData.crc);
            }
            CheckImagesRun = null;
        }

        private IEnumerator CheckModerationAPI(SignData signData, byte[] array)
        {
            UnityWebRequest www = UnityWebRequest.Post(_moderationAPI, CreateJson(Convert.ToBase64String(array)), "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {config.moderationAPI.apiToken}");
            www.timeout = 20;
            yield return www.SendWebRequest();

            if (IsBadResponse(www))
            {
                yield return _ModerationAPIWait;
                yield break;
            }

            string jsonResponse = www.downloadHandler.text;
            OmniDataRoot omniData = JsonConvert.DeserializeObject<OmniDataRoot>(jsonResponse);
            if (omniData == null || omniData.results == null || omniData.results.Count < 1)
            {
                www.Dispose();
                yield break;
            }

            Result result = omniData.results[0];
            if (result.flagged)
            {
                ReportContent(signData, array);
            }
            www.Dispose();
            yield return false;
        }

        private IEnumerator CheckGPTModelAPI(SignData signData, byte[] array)
        {
            UnityWebRequest www = UnityWebRequest.Post(_gptModelAPI, CreateGPTJson(Convert.ToBase64String(array)), "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {config.gptModel.apiToken}");
            www.timeout = 20;
            yield return www.SendWebRequest();

            if (IsBadResponse(www))
            {
                yield return _GPTModelAPIWait;
                yield break;
            }

            string jsonResponse = www.downloadHandler.text;
            GPTRoot gptData = JsonConvert.DeserializeObject<GPTRoot>(jsonResponse);
            string response = gptData.choices[0]?.message?.content ?? string.Empty;
            if (response.Length > 2)
            {
                response = response.Substring(0, 2);
            }

            if (gptData != null && !string.Equals(response, "no", StringComparison.OrdinalIgnoreCase))
            {
                ReportContent(signData, array);
            }
            www.Dispose();
            yield return false;
        }

        private void ReportContent(SignData signData, byte[] array)
        {
            if (!config.loggingMode)
            {
                signData.sign.ClearContent();
                FileStorage.server.Remove(signData.crc, signData.type, signData.sign.NetworkID);
            }
            _signCooldown[signData.playerId] = Time.time + config.signModerationCooldown;
            SendPlayerWarning(signData.playerId);
            if (config.discordSettings.discordLogging)
            {
                ServerMgr.Instance.StartCoroutine(LogToDiscord(signData, BasePlayer.FindByID(signData.playerId), gptModel, array));
            }
        }
        string badResponse = "Bad Response: ";

        private bool IsBadResponse(UnityWebRequest www)
        {
            if (www.result != UnityWebRequest.Result.Success)
            {
                PrintError(string.Join(badResponse, www.error));
                www.Dispose();
                return true;
            }
            return false;
        }

        #region Image Pooling
        const string jsongptPoolline1 = "{\"model\": \"";
        const string jsongptPoolline2 = "\",\"store\": false,\"messages\": [{\"role\": \"user\", \"content\": [{\"type\": \"text\", \"text\": \"Answer with only 'yes' or 'no' and comma separation per image per image if the images contain any of the specified categories:";
        const string jsongptPoolline3 = "\"},";
        const string jsongptPoolline4 = "{\"type\": \"image_url\", \"image_url\": {\"url\": \"data:image/jpeg;base64,";
        const string jsongptPoolline5 = "\"}},";
        const string jsongptPoolline6 = "]}]}";

        private int _pooledImageChecks = 0;
        private static IEnumerator CheckPooledImagesRun;
        private class PooledSignData
        {
            public byte[] bytes;
            public SignData sign;
        }

        private IEnumerator CheckPooledImages()
        {
            if (_signsQueuedPool.Count < config.batchSettings.minImagesPooled)
            {
                if (config.batchSettings.maxChecksImagesPooled == 0 || _pooledImageChecks < config.batchSettings.maxChecksImagesPooled)
                {
                    if (_signsQueuedPool.Count > 0)
                    {
                        _pooledImageChecks += 1;
                    }

                    CheckPooledImagesRun = null;
                    yield break;
                }

            }
            _pooledImageChecks = 0;

            List<PooledSignData> checkedSigns = Pool.Get<List<PooledSignData>>();

            _stringBuilder.Clear();
            _stringBuilder.Append(jsongptPoolline1);
            _stringBuilder.Append(gptModel);
            _stringBuilder.Append(jsongptPoolline2);
            _stringBuilder.Append(prompt);
            _stringBuilder.Append(jsongptPoolline3);

            foreach (var sign in _signsQueuedPool)
            {
                SignData signData = sign.Value;
                byte[] array = GetImageBytes(signData);
                if (array == null || array.Length == 0)
                {
                    continue;
                }
                string Base64 = Convert.ToBase64String(array);
                _stringBuilder.Append(jsongptPoolline4);
                _stringBuilder.Append(Base64);
                _stringBuilder.Append(jsongptPoolline5);
                checkedSigns.Add(new PooledSignData() { bytes = array, sign = signData });
            }
            _stringBuilder.Remove(_stringBuilder.Length - 1, 1); //remove last comma
            _stringBuilder.Append(jsongptPoolline6);

            _signsQueuedPool.Clear();
            if (checkedSigns.Count < 1)
            {
                Pool.FreeUnmanaged(ref checkedSigns);
                CheckPooledImagesRun = null;
                yield break;
            }
            UnityWebRequest www = UnityWebRequest.Post(_gptModelAPI, _stringBuilder.ToString(), "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {config.gptModel.apiToken}");
            www.timeout = 20;
            yield return www.SendWebRequest();
            
            if (IsBadResponse(www))
            {
                Pool.FreeUnmanaged(ref checkedSigns);
                CheckPooledImagesRun = null;
                yield break;
            }

            string jsonResponse = www.downloadHandler.text;
            GPTRoot gptData = JsonConvert.DeserializeObject<GPTRoot>(jsonResponse);
            string response = gptData.choices[0]?.message?.content?.Replace(" ", string.Empty) ?? string.Empty;

            if (gptData == null || string.IsNullOrEmpty(response))
            {
                www.Dispose();
                Pool.FreeUnmanaged(ref checkedSigns);
                CheckPooledImagesRun = null;
                yield break;
            }
            string[] responses = response.Split(',');

            for (int i = 0; i < responses.Length; i++)
            {
                if (gptData == null || !string.Equals(responses[i], "yes", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i >= checkedSigns.Count)
                {
                    Puts($"GPT has more responses than images {checkedSigns.Count} response:{string.Join(",", responses)}");
                    break;
                }
                SignData signData = checkedSigns[i].sign;
                ReportContent(signData, checkedSigns[i].bytes);
            }
            www.Dispose();
            Pool.FreeUnmanaged(ref checkedSigns);
            CheckPooledImagesRun = null;
        }
        #endregion Image Pooling

        private void SendPlayerWarning(ulong playerID)
        {
            if (!config.chatWarnings)
                return;

            BasePlayer player = BasePlayer.FindByID(playerID);
            if (player == null)
                return;
            ChatMessage(player.IPlayer, "WarningMessage");
        }

        public byte[] ResizeImage(byte[] bytes, int width, int height)
        {
            if (bytes == null || bytes.Length == 0)
            {
                PrintError("Invalid image byte array.");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                PrintError("Width and height must be greater than zero.");
                return null;
            }

            MemoryStream originalStream = Pool.Get<MemoryStream>();
            originalStream.Write(bytes, 0, bytes.Length);

            MemoryStream resizedStream = Pool.Get<MemoryStream>();

            using (var originalImage = Image.FromStream(originalStream))
            using (var resizedImage = new Bitmap(width, height))
                try
                {
                    using (var graphics = Graphics.FromImage(resizedImage))
                    {
                        graphics.Clear(System.Drawing.Color.LightGray);
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                        graphics.SmoothingMode = SmoothingMode.None;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                        graphics.DrawImage(originalImage, 0, 0, width, height);

                        if (_imageCodecInfo == null || _encoderParams == null)
                        {
                            resizedImage.Save(resizedStream, ImageFormat.Jpeg);
                        }
                        else
                        {
                            resizedImage.Save(resizedStream, _imageCodecInfo, _encoderParams);
                        }
                        return resizedStream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    PrintError("Error resizing image: " + ex.Message);
                    Pool.FreeUnmanaged(ref originalStream);
                    Pool.FreeUnmanaged(ref resizedStream);
                    return null;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref originalStream);
                    Pool.FreeUnmanaged(ref resizedStream);
                }
        }

        private bool CheckImage(Result omniResult)
        {
            if (omniResult.categories.harassment && config.moderationAPI.harassment)
            {
                return true;
            }
            if (omniResult.categories.harassmentthreatening && config.moderationAPI.harassmentThreatening)
            {
                return true;
            }
            if (omniResult.categories.sexual && config.moderationAPI.sexual)
            {
                return true;
            }
            if (omniResult.categories.hate && config.moderationAPI.hate)
            {
                return true;
            }
            if (omniResult.categories.hatethreatening && config.moderationAPI.hateThreatening)
            {
                return true;
            }
            if (omniResult.categories.illicit && config.moderationAPI.illicit)
            {
                return true;
            }
            if (omniResult.categories.illicitviolent && config.moderationAPI.illicitViolent)
            {
                return true;
            }
            if (omniResult.categories.selfharmintent && config.moderationAPI.selfHarmIntent)
            {
                return true;
            }
            if (omniResult.categories.selfharminstructions && config.moderationAPI.selfHarmInstructions)
            {
                return true;
            }
            if (omniResult.categories.selfharm && config.moderationAPI.selfHarm)
            {
                return true;
            }
            if (omniResult.categories.sexualminors && config.moderationAPI.sexualMinors)
            {
                return true;
            }
            if (omniResult.categories.violence && config.moderationAPI.violence)
            {
                return true;
            }
            if (omniResult.categories.violencegraphic && config.moderationAPI.violenceGraphic)
            {
                return true;
            }
            return false;
        }
        #endregion Methods

        #region Set Up Loading Image

        private uint _tempModerationImageCRC;
        private string _tempModerationImageURL = "https://i.postimg.cc/Zq5qfgtk/10x10-00000000.png";
        private static IEnumerator GetLoadingImage = null;
        private IEnumerator LoadingImageSetup()
        {
            UnityWebRequest www = UnityWebRequest.Get(_tempModerationImageURL);
            www.timeout = 30;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                PrintError(www.error + " Cannot get image from:" + _tempModerationImageURL);
                www.Dispose();
                yield break;
            }
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(www.downloadHandler.data);
            if (texture != null)
            {
                byte[] bytes = texture.EncodeToPNG();
                if (bytes.Length > ConVar.Server.maxpacketsize_command)
                {
                    float percentage = Mathf.Sqrt(((float)ConVar.Server.maxpacketsize_command / (float)texture.GetSizeInBytes()));
                    bytes = ResizeImage(bytes, (int)(texture.width * percentage), (int)(texture.height * percentage));
                }
                UnityEngine.Object.DestroyImmediate(texture);
                if (bytes != null)
                {
                    _tempModerationImageCRC = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                }
            }
            www.Dispose();
            GetLoadingImage = null;
        }
        #endregion Set Up Loading Image

        #region Discord Logging
        private IEnumerator LogToDiscord(SignData signData, BasePlayer player, string model, byte[] imageBytes)
        {
            var msg = CreateDiscordMessage(player.displayName, player.UserIDString, signData.sign.ShortPrefabName, signData.sign.transform.position, model, signData.crc);
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("payload_json", JsonConvert.SerializeObject(msg))
            };

            if (config.discordSettings.discordImageLogging)
                formData.Add(new MultipartFormFileSection("file1", imageBytes, $"{signData.crc}.png", "image/png"));

            UnityWebRequest wwwpost = UnityWebRequest.Post(config.discordSettings.DiscordWebhook, formData);
            yield return wwwpost.SendWebRequest();
            if (wwwpost.result != UnityWebRequest.Result.Success)
            {
                PrintError("Cannot post log to discord:" + wwwpost.error);
                wwwpost.Dispose();
                yield break;
            }
            wwwpost.Dispose();
        }

        private DiscordMessage CreateDiscordMessage(string playername, string userid, string itemname, Vector3 location, string model, uint crc)
        {
            string steamprofile = "https://steamcommunity.com/profiles/" + userid;
            var fields = new List<DiscordMessage.Fields>()
            {
                new DiscordMessage.Fields("Player: " + playername, $"[{userid}]({steamprofile})", true),
                new DiscordMessage.Fields("Entity", itemname, true),
                new DiscordMessage.Fields("AI Model", model, false),
                new DiscordMessage.Fields("Teleport position", $"```teleportpos {location}```", false)
            };

            var footer = new DiscordMessage.Footer($"Logged @{DateTime.UtcNow:dd/MM/yy HH:mm:ss}");

            DiscordMessage.Image image = new DiscordMessage.Image($"attachment://{crc}.png");

            var embeds = new List<DiscordMessage.Embeds>()
            {
                new DiscordMessage.Embeds("Server - " + (string.IsNullOrEmpty(config.discordSettings.ServerName) ? server.Name : config.discordSettings.ServerName), "A sign has been moderated" , fields, footer, image)
            };
            DiscordMessage msg = new DiscordMessage(config.discordSettings.DiscordUsername, config.discordSettings.AvatarUrl, embeds);
            return msg;
        }

        #region Discord Class
        public class DiscordMessage
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Image
            {
                public string url { get; set; }
                public Image(string url)
                {
                    this.url = url;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public Image image { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer, Image image)
                {
                    this.title = title;
                    this.description = description;
                    this.image = image;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public DiscordMessage(string username, string avatar_url, List<Embeds> embeds)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.embeds = embeds;
            }
        }

        #endregion
        #endregion Discord Logging

        #region Helpers
        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);
        private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);
        private void ChatMessage(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
        }
        private byte[] GetImageBytes(SignData signData)
        {
            byte[] array = FileStorage.server.Get(signData.crc, signData.type, signData.sign.NetworkID);

            if (array == null)
            {
                //Puts($"Cannot get image from sign crc:{signData.sign.GetContentCRCs[signData.textureIndex]} netID:{signData.sign.NetworkID} entity:{signData.sign.ShortPrefabName}");
                return System.Array.Empty<byte>();
            }

            if (_ImageSizeperAsset.TryGetValue(signData.sign.prefabID, out ImageSize imageSize))
            {
                float sizeReduction = (config.imageSizeReduction) / 100;
                array = ResizeImage(array, (int)(imageSize.Width * sizeReduction), (int)(imageSize.Height * sizeReduction));
                if (array == null)
                {
                    //Puts($"Cannot get image from sign crc:{signData.sign.GetContentCRCs[signData.textureIndex]} netID:{signData.sign.NetworkID} entity:{signData.sign.ShortPrefabName}");
                    return System.Array.Empty<byte>();
                }
            }

            return array;
        }

        #endregion Helpers
    }
}