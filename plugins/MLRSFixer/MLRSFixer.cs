using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MLRS Fixer", "Aspect.dev", "0.1.3")]
    [Description("Provides commands to fix all MLRS on your server.")]
    internal class MLRSFixer : CovalencePlugin
    {
        public const string fixPermission = "mlrsfixer.fixmlrs";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use this command.",
                ["NoMLRS"] = "There are no broken MLRS vehicles on this server.",
                ["ResetCooldowns"] = "Successfully reset the cooldown on all {COUNT} MLRS vehicles.",
            }, this, "en");
        }

        void Init() => permission.RegisterPermission(fixPermission, this);

        [Command("fixmlrs")]
        void FixMLRS(IPlayer sender, string command, string[] args)
        {
            if (!sender.HasPermission(fixPermission))
            {
                sender.Message(lang.GetMessage("NoPermission", this, sender.Id));
                return;
            }

            var mlrsList = BaseNetworkable.serverEntities.OfType<MLRS>()?.Where(x => x.HasFlag(BaseEntity.Flags.Broken));
            if (mlrsList == null) return;

            var mlrsCount = mlrsList.Count();

            if (mlrsCount < 1)
            {
                sender.Message(lang.GetMessage("NoMLRS", this, sender.Id));
                return;
            }

            foreach (MLRS mlrs in mlrsList) mlrs.SetFlag(BaseEntity.Flags.Broken, false, false, true);

            sender.Message(lang.GetMessage("ResetCooldowns", this, sender.Id).Replace("{COUNT}", mlrsCount.ToString()));
            return;
        }
    }
}
