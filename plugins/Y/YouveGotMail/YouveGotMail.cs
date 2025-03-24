using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("You've Got Mail", "KajWithAJ", "1.0.2")]
    [Description("Notifies online players when they receive mail in their mailbox.")]
    class YouveGotMail : RustPlugin
    {
        private const string MailboxPermission = "youvegotmail.message";

        private void Init()
        {
            permission.RegisterPermission(MailboxPermission, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ReceivedMail"] = "<color=#FFFFFF>You've received mail from <color=#FFFF00>{0}</color> in your mailbox at <color=#FFFF00>{1}</color>.</color>"
            }, this);
        }

        void OnItemSubmit(Item item, Mailbox mailbox, BasePlayer player)
        {
            if (mailbox.ShortPrefabName == "mailbox.deployed") {
                string coordinates = MapHelper.PositionToString(mailbox.transform.position);
                ulong ownerID = mailbox.OwnerID;
                Puts($"{player.displayName} in mailbox of {ownerID} at {coordinates}");
                

                BasePlayer owner = BasePlayer.FindByID(ownerID);
                if (owner != null) {
                    if (permission.UserHasPermission(owner.UserIDString, MailboxPermission)) {
                        
                        string message = lang.GetMessage("ReceivedMail", this, owner.UserIDString);
                        Player.Message(owner, string.Format(message, player.displayName, coordinates));
                    }
                }
            }
        }
    }
}
