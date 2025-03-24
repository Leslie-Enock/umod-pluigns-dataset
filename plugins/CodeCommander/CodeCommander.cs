using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Code Commander", "Hougan", "0.0.3")]
    [Description("Allow customize executable commands when interacting with code-lock")]
    public class CodeCommander : RustPlugin  
    { 
        #region Classes

        private class CodeCommand
        {
            public class Condition 
            {
                [JsonProperty("Action type (Open - on object interact with code, Unlock - on enter code)")]
                public string Action = ""; 
                [JsonProperty("If object type is door (true - only door, false - all objects)")]
                public bool OnlyOnDoor = true;
                [JsonProperty("If code is same with (None - to not check code)")]
                public string Code = "None";
                [JsonProperty("If already authed players amount smaller then (-1 - to not check authed)")]
                public int MaxAuthed = 3;
                [JsonProperty("If already authed players amount bigger then (-1 - to not check authed)")]
                public int MinAuthed = 3;
                
            }
            [JsonProperty("Execute command (vars: %STEAMID%, %NAME%")]
            public string Command = "";
            [JsonProperty("Message on executing command (vars: %STEAMID%, %NAME%")]
            public string Message = "";
            
            [JsonProperty("Conditions to execute command")]
            public Condition Conditions = new Condition(); 
        }
        
        private class Configuration
        {
            [JsonProperty("List of doors with commands")]
            public HashSet<CodeCommand> DoorCommands;

            public static Configuration Generate()
            {
                return new Configuration()
                {
                    DoorCommands = new HashSet<CodeCommand>
                    {
                        new CodeCommand
                        {
                            Command = "env.time 12",
                            Message = "Congratulations, %NAME%, you turned on the sun!",
                            Conditions = new CodeCommand.Condition
                            {
                                Action = "Unlock",
                                Code = "1337",
                                MaxAuthed = 3,
                                MinAuthed = -1
                            }
                        },
                        new CodeCommand
                        {
                            Command = "",
                            Message = "Remember, that according to the rules of the server, you can only play with two friends!",
                            Conditions = new CodeCommand.Condition
                            {
                                Action = "Unlock",
                                Code = "None",
                                MinAuthed = -1,
                                MaxAuthed = 3 
                            }
                        },
                        new CodeCommand
                        {
                            Command = "",
                            Message = "Remember, you are breaking the server rules! Maximum group size - 3 people!",
                            Conditions = new CodeCommand.Condition
                            {
                                Action = "Unlock",
                                Code      = "None",
                                MinAuthed = 3,
                                MaxAuthed = -1
                            }
                        }
                    }
                };
            }
        }
        
        #endregion

        #region Variables

        private static Configuration Settings;

        #endregion

        #region Initialization

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"An error occurred reading the configuration file!");
                PrintError($"Check it with any JSON Validator!");
                return;
            }
            
            SaveConfig();  
        } 

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        private void OnServerInitialized()
        {
            int errorsCount = Settings.DoorCommands.Count(p => p.Conditions.Action != "Open" && p.Conditions.Action != "Unlock");
            if (errorsCount > 0) PrintError($"There are {errorsCount} unknown actions types in Configuration!");
        }
        
        #endregion

        #region Hooks
        
        private void CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            var codeLock = baseLock.GetComponent<CodeLock>();
            if (codeLock == null) return;
            
            var parentEntity = baseLock.GetComponentInParent<BaseEntity>();
            if (parentEntity == null)
            {
                PrintError($"Unknown entity of code-lock!");
                return;
            }
            
            var isDoor = parentEntity is Door;
            var possibleCommands = Settings.DoorCommands.Where(p => p.Conditions.Action == "Open" && (p.Conditions.OnlyOnDoor && isDoor || !p.Conditions.OnlyOnDoor)).ToList();
            if (possibleCommands.Count == 0) return; 
            
            foreach (var check in possibleCommands)
            {
                var conditions = check.Conditions;
                
                if (conditions.Code      != "None" && conditions.Code                 != codeLock.code) continue;
                if (conditions.MinAuthed != -1     && codeLock.whitelistPlayers.Count < conditions.MinAuthed) continue;
                if (conditions.MaxAuthed != -1     && codeLock.whitelistPlayers.Count > conditions.MaxAuthed) continue;
                
                if (!string.IsNullOrEmpty(check.Command)) Server.Command(PrepareString(player,     check.Command));
                if (!string.IsNullOrEmpty(check.Message)) player.ChatMessage(PrepareString(player, check.Message)); 
            }
        }
        
        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode) => OnCodeEntered(codeLock, player, newCode); 
        
        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            var parentEntity = codeLock.GetParentEntity();
            if (parentEntity == null)
            {
                PrintError($"Unknown entity of code-lock!");
                return;
            }
            
            var isDoor = parentEntity is Door;
            var possibleCommands = Settings.DoorCommands.Where(p => p.Conditions.Action == "Unlock" && (p.Conditions.OnlyOnDoor && isDoor || !p.Conditions.OnlyOnDoor)).ToList();
            if (possibleCommands.Count == 0) return;

            foreach (var check in possibleCommands)
            {
                var conditions = check.Conditions;
                
                if (conditions.Code != "None" && conditions.Code != code) continue;
                if (conditions.MinAuthed != -1 && codeLock.whitelistPlayers.Count < conditions.MinAuthed) continue;
                if (conditions.MaxAuthed != -1 && codeLock.whitelistPlayers.Count > conditions.MaxAuthed) continue;
                
                if (!string.IsNullOrEmpty(check.Command)) Server.Command(PrepareString(player,     check.Command));
                if (!string.IsNullOrEmpty(check.Message)) player.ChatMessage(PrepareString(player, check.Message)); 
            }
        }

        #endregion

        #region Utils

        private string PrepareString(BasePlayer player, string input) => input.Replace("%STEAMID%", player.UserIDString).Replace("%NAME%", player.displayName);

        #endregion
    }
}