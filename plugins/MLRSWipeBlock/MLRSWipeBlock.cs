using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
  [Info("MLRS Wipe Block", "Malmo", "1.0.1")]
  [Description("Blocks use of MLRS while wipe block")]
  internal class MLRSWipeBlock : RustPlugin
  {
    #region Dependencies

    [PluginReference]
    private Plugin WipeBlock;

    #endregion

    #region Hooks

    private void OnServerInitialized()
    {
      if (WipeBlock == null || !WipeBlock.IsLoaded)
      {
        PrintWarning("WipeBlock is not loaded, get it at https://umod.org/plugins/wipeblock");
      }
    }

    object CanMountEntity(BasePlayer player, MLRS entity)
    {

      // bypass if admin
      if (player.IsAdmin) return null;

      // bypass if wipeblock is not loaded
      if (WipeBlock == null) return null;

      var timeLeft = GetTimeLeft();
      if (timeLeft > 0)
      {
        ChatMessage(player, "Blocked", GetTimeString(timeLeft));
        return false;
      }

      return null;
    }

    #endregion

    #region Helper Methods

    private int GetTimeLeft()
    {
      var blockedItems = WipeBlock.Call<Dictionary<string, int>>("API_GetTimesLeft");
      if (blockedItems.ContainsKey("mlrs"))
      {
        var timeLeft = blockedItems["mlrs"];

        return timeLeft;
      }

      return 0;
    }

    private string GetTimeString(int time)
    {
      var timeString = string.Empty;
      var days = time / 86400;
      time = time % 86400;
      if (days > 0)
      {
        timeString += days + "d";
      }

      var hours = time / 3600;
      time = time % 3600;
      if (hours > 0)
      {
        if (days > 0)
        {
          timeString += ", ";
        }

        timeString += hours + "h";
      }

      var minutes = time / 60;
      time = time % 60;
      if (minutes > 0)
      {
        if (hours > 0)
        {
          timeString += ", ";
        }

        timeString += minutes + "m";
      }

      var seconds = time;
      if (seconds > 0)
      {
        if (minutes > 0)
        {
          timeString += ", ";
        }

        timeString += seconds + "s";
      }

      return timeString;
    }

    #endregion

    #region Localization

    private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
    player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

    private string GetMessage(IPlayer player, string messageName, params object[] args) =>
        GetMessage(player.Id, messageName, args);

    private string GetMessage(string playerId, string messageName, params object[] args)
    {
      var message = lang.GetMessage(messageName, this, playerId);
      return args.Length > 0 ? string.Format(message, args) : message;
    }

    protected override void LoadDefaultMessages()
    {
      lang.RegisterMessages(new Dictionary<string, string>
      {
        ["Blocked"] = "MLRS is <color=red>wipe blocked</color> for {0}"
      }, this, "en");
    }

    #endregion
  }
}
