using Oxide.Core;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Rust;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Pollless", "Kechless", "0.0.4")]
    [Description("Players can vote for certain things.")]
    class Pollless : RustPlugin
    {
        #region Variables
        //PERMISSIONS
        const string permission_create = "Pollless.create";
        const string permission_delete = "Pollless.delete";
        const string permission_show = "Pollless.show";

        const string permission_vote = "Pollless.vote";

        const string mainFolder = "Pollless/";
        const string mainFile = "Poll_list";
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error_Insuffarg"] = "Insufficient arguments!",
                ["Cmd_Noperm"] = "You don't have the permsission to do that!",
                ["Error_NameExists"] = "Name you've chosen already exists.",
                ["Error_Number"] = "ERROR: usage /poll show [PAGENUMBER]",
                ["Error_PollNotFound"] = "ERROR: Poll not found!",
                ["Cmd_AlreadyVoted"] = "You have already voted for this poll.",
                ["Error_UsageVote"] = "ERROR: usage /poll vote [POLLINDEX] [YES/NO]",

            }, this);
        }
        #endregion

        #region Config
        private void Init()
        {
            permission.RegisterPermission(permission_create, this);
            permission.RegisterPermission(permission_delete, this);
            permission.RegisterPermission(permission_show, this);
            permission.RegisterPermission(permission_vote, this);

        }

        #endregion

        #region Commands

        // When player do /vote ...
        [ChatCommand("poll")]
        void pollCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 0)
            {
                switch (args[0].ToLower())
                {
                    case "show":
                        if (hasPerm(player, permission_show))
                        {
                            if (args[1] != null)
                            {
                                int number;
                                if (int.TryParse(args[1], out number))
                                {
                                    showPollList(player, number);
                                }
                                else
                                {
                                    SendMessage(player, "Erorr_Number");
                                }


                            }
                        }
                        break;

                    case "create":
                        if (hasPerm(player, permission_create))
                        {
                            if (args[1] != null && args[2] != null)
                            {
                                try
                                {
                                    createPoll(args[1], args[2], player.displayName);
                                }
                                catch (ArgumentException e)
                                {
                                    PrintToChat(player, e.Message);
                                }
                            }
                            else
                            {

                            }
                        }
                        break;
                    case "delete":
                        if (hasPerm(player, permission_delete))
                        {
                            if (args[1] != null)
                            {

                                int number;
                                if (int.TryParse(args[1], out number))
                                {
                                    deletePoll(number);
                                }
                                else
                                {
                                    SendMessage(player, "Erorr_Number");
                                }
                            }
                        }
                        break;
                    case "info":
                        if (hasPerm(player, permission_show))
                        {
                            if (args[1] != null)
                            {
                                int number;
                                if (int.TryParse(args[1], out number))
                                {
                                    getInfoPollByIndex(number);
                                }
                                else
                                {
                                    SendMessage(player, "Erorr_Number");
                                }
                            }
                        }

                        break;
                    case "vote":
                        if (hasPerm(player, permission_vote))
                        {
                            if (args[1] != null && args[2] != null)
                            {
                                int number;
                                if (int.TryParse(args[1], out number))
                                {
                                    votePollByIndex(player, number, args[2]);
                                }
                                else
                                {
                                    SendMessage(player, "Erorr_Number");
                                }

                            }
                        }

                        break;
                }
            }

            else
            {
                SendMessage(player, "Error_Insuffarg");
            }
        }

        #endregion

        #region Hooks

        #endregion

        #region Methodes

        public void votePollByIndex(BasePlayer player, int index, string vote)
        {
            Poll votedPoll = getPollByindex(index);
            if (votedPoll != null)
            {
                if (!votedPoll.hasVoted(player.UserIDString))
                {
                    if (vote.ToLower() == "yes")
                    {
                        votedPoll.addYesVote(player.UserIDString);
                        UpdatePoll(votedPoll, index);

                    }
                    else if (vote.ToLower() == "no")
                    {
                        votedPoll.addNoVote(player.UserIDString);
                        UpdatePoll(votedPoll, index);

                    }
                    else
                    {
                        SendMessage(player, "Error_UsageVote");
                    }
                }
                else
                {
                    SendMessage(player, "Cmd_AlreadyVoted");
                }
            }
            else
            {
                SendMessage(player, "Error_PollNotFound");
            }

        }



        public void getInfoPollByIndex(int index)
        {
            Poll selectedPoll = getPollByindex(index);
            string infoText =
                "Name: " + selectedPoll.getPollName() + Environment.NewLine +
                "Content: " + selectedPoll.getContent() + Environment.NewLine +
                "Creator: " + selectedPoll.getCreator() + Environment.NewLine +
                "Use /poll vote [POLL_INDEX] [YES/NO]" + Environment.NewLine;

        }

        public void votePoll(int index, Boolean voting, string playerID)
        {
            Poll currentPoll = getPollByindex(index);
            if (voting)
            {
                currentPoll.addYesVote(playerID);
            }
            else
            {
                currentPoll.addNoVote(playerID);
            }
            UpdatePoll(currentPoll, index);
        }

        public void UpdatePoll(Poll item, int index)
        {
            List<Poll> list = getPolls();
            list[index] = item;
            updatePollList(list);
        }

        public Poll getPollByindex(int index)
        {
            try
            {
                return getPolls().ElementAt(index);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        // Deletes a poll by given index.
        public void deletePoll(int index)
        {
            List<Poll> oldList = getPolls();
            oldList.RemoveAt(index);
            updatePollList(oldList);
        }

        // Shows all polls
        public void showPollList(BasePlayer player, int page)
        {
            List<Poll> polls = getPolls();
            List<string> list = new List<string>();
            double x = polls.Count / 5;
            int count = (5 * page) - 5;
            int maxPage = Convert.ToInt32(Math.Ceiling(x));
            for (int i = count; i < 5; i++)
            {
                if (polls[i] != null)
                {
                    list.Add("Index: " + polls.IndexOf(polls[i]) + " " + polls[i].ToString());
                }
                else
                {
                    list.Add("----------------" + Environment.NewLine);
                }
            }
            list.Add("Page " + page + "of " + maxPage);
            SendListMessage(player, list);
        }

        // creating a new poll with name check.
        public void createPoll(string pollName, string content, string creator)
        {
            if (!checkPollName(pollName))
            {
                Poll newPoll = new Poll(pollName, content, creator, DateTime.Now);
                addToPollList(newPoll);
            }
            else
            {
                throw new ArgumentException(lang.GetMessage("Error_NameExists", this));
            }
        }

        // checks whether the chosen name exists
        public Boolean checkPollName(string pollName)
        {
            foreach (Poll item in getPolls())
            {
                if (pollName == item.getPollName())
                {
                    return true;
                }
            }
            return false;
        }

        // get the list of the file.
        public List<Poll> getPolls()
        {
            List<Poll> loadedPolls;
            // check if the file exists or a new one will be created.
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(mainFolder + mainFile))
            {
                loadedPolls = new List<Poll>();
                updatePollList(loadedPolls);
            }
            else
            {
                string raw_poll_file = Interface.Oxide.DataFileSystem.ReadObject<string>(mainFolder + mainFile);
                loadedPolls = JsonConvert.DeserializeObject<List<Poll>>(raw_poll_file);
            }
            return loadedPolls;
        }

        //Rewrites the poll_file
        public void updatePollList(List<Poll> list)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(mainFolder + mainFile, JsonConvert.SerializeObject(list));
        }

        // adds a new poll to the list
        public void addToPollList(Poll newPoll)
        {
            List<Poll> currentList = getPolls();
            currentList.Add(newPoll);
            updatePollList(currentList);
        }

        #endregion

        #region Classes
        public class Poll
        {
            private string _pollName;
            private string _content;
            private string _creator;

            private DateTime _dateOfCreation;

            private List<string> _yesVotes;
            private List<string> _noVotes;


            public string getPollName()
            {
                return _pollName;
            }

            public string getContent()
            {
                return _content;
            }

            public string getCreator()
            {
                return _creator;
            }

            public void addYesVote(string playerID)
            {
                _yesVotes.Add(playerID);
            }

            public void addNoVote(string playerID)
            {
                _noVotes.Add(playerID);
            }

            public bool hasVoted(string PlayerID)
            {
                if (_yesVotes.Contains(PlayerID))
                {
                    return true;
                }
                else if (_noVotes.Contains(PlayerID))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }


            public Poll()
            {
            }

            [JsonConstructor]
            public Poll(string pollName, string content, string creator, DateTime date)
            {
                _pollName = pollName;
                _content = content;
                _creator = creator;
                _dateOfCreation = date;

                _yesVotes = new List<string>();
                _noVotes = new List<string>();
            }

            override
            public string ToString()
            {
                return "Name: " + _pollName + "Creator: " + _creator + "Creation: " + _dateOfCreation;
            }
        }

        #endregion

        #region Helpers
        public void SendMessage(BasePlayer player, string message)
        {
            PrintToChat(player, lang.GetMessage(message, this, player.UserIDString));
        }

        public void SendListMessage(BasePlayer player, List<string> list)
        {
            foreach (string item in list)
            {
                PrintToChat(player, item + Environment.NewLine);
            }
        }

        public bool hasPerm(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        #endregion
    }
}
